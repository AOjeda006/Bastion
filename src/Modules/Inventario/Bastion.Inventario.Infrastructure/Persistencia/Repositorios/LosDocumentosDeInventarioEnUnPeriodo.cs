using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Organizacion.Contracts.Ejercicios;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IDocumentosDeUnPeriodo"/>
/// <remarks>
/// <para>
/// Lo que Inventario contesta cuando Organización pregunta si un intervalo está libre. Vive aquí,
/// en el módulo dueño de las tablas, y se resuelve en proceso: Organización llama a un método y no
/// sabe que detrás hay un <c>DbContext</c> (§4, reglas de frontera 1 y 3).
/// </para>
/// <para>
/// <b>Hoy los documentos de este módulo son el ajuste, la transferencia, que llegó en el 2.11, y el
/// recuento, que llegó en el 2.12.</b> Cuando lleguen los albaranes, se suman <b>aquí</b>: el cierre
/// no se entera y no hay que acordarse de tocarlo. Que este método se quede corto el día que aparezca un documento
/// nuevo es el modo de fallo de esta clase, y contra eso está el barrido que compara los módulos
/// registrados con los declarados.
/// </para>
/// <para>
/// <b>La fecha que decide es <c>FechaDeOperacion</c></b>, no <c>CreadoEn</c>: es la fecha del
/// documento, la que la R9 llama «la fecha del movimiento» y la única que se compara contra el
/// intervalo del ejercicio. <c>CreadoEn</c> es cuándo se tecleó, que es otra cosa y no decide nada.
/// </para>
/// <para>
/// <b>La transferencia tiene dos</b>, y escribe una fila del libro en cada una (ADR-0053 §3). Un
/// borrador cuenta por la de envío, que es la única que tiene; un documento cuenta si cualquiera de
/// las dos cae dentro del intervalo (§12). Contar solo la de envío dejaría cerrar el ejercicio de una
/// recepción del 3 de enero cuyo envío fue el 30 de diciembre.
/// </para>
/// <para>
/// <b>El recuento solo cuenta confirmado o anulado, y por su fecha de confirmación</b> (ADR-0055
/// §1.5). Uno en curso no tiene fecha de documento: la tendrá el día que se confirme, y su ajuste
/// llevará la misma. Por eso no es un borrador del ejercicio en el que se abrió, y no impide
/// cerrarlo. Uno confirmado sin diferencias no tiene ajuste que hable por él, y por eso no basta con
/// mirar los ajustes.
/// </para>
/// </remarks>
internal sealed class LosDocumentosDeInventarioEnUnPeriodo(InventarioDbContext contexto)
    : IDocumentosDeUnPeriodo
{
    /// <inheritdoc/>
    public string Modulo => "Inventario";

    /// <inheritdoc/>
    public async Task<bool> HayBorradoresEnAsync(
        Guid empresaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion) =>
        await contexto.Ajustes
            .AnyAsync(
                ajuste => ajuste.EmpresaId == empresaId
                    && ajuste.Estado == EstadoDeAjuste.Borrador
                    && ajuste.FechaDeOperacion >= desde
                    && ajuste.FechaDeOperacion <= hasta,
                cancelacion)
            .ConfigureAwait(false)
        || await contexto.Transferencias
            .AnyAsync(
                transferencia => transferencia.EmpresaId == empresaId
                    && transferencia.Estado == EstadoDeTransferencia.Borrador
                    && transferencia.FechaDeEnvio >= desde
                    && transferencia.FechaDeEnvio <= hasta,
                cancelacion)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<bool> HayDocumentosEnAsync(
        Guid empresaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion) =>
        // Sin filtrar por estado: los anulados también cuentan. Un ajuste anulado sigue siendo una
        // fila del libro —se corrige con un inverso, no se borra (R11)— y sacarlo del intervalo de
        // su ejercicio dejaría al inverso y al original en periodos distintos.
        await contexto.Ajustes
            .AnyAsync(
                ajuste => ajuste.EmpresaId == empresaId
                    && ajuste.FechaDeOperacion >= desde
                    && ajuste.FechaDeOperacion <= hasta,
                cancelacion)
            .ConfigureAwait(false)
        || await contexto.Transferencias
            .AnyAsync(
                transferencia => transferencia.EmpresaId == empresaId
                    && ((transferencia.FechaDeEnvio >= desde && transferencia.FechaDeEnvio <= hasta)
                        || (transferencia.FechaDeRecepcion >= desde && transferencia.FechaDeRecepcion <= hasta)),
                cancelacion)
            .ConfigureAwait(false)
        // Sin filtrar por estado, por lo mismo que el ajuste: el anulado sigue numerado. Uno en
        // curso o descartado no tiene fecha de confirmación, y el `NULL` no cae en ningún intervalo.
        || await contexto.Recuentos
            .AnyAsync(
                recuento => recuento.EmpresaId == empresaId
                    && recuento.FechaDeConfirmacion >= desde
                    && recuento.FechaDeConfirmacion <= hasta,
                cancelacion)
            .ConfigureAwait(false);
}
