using Bastion.Inventario.Domain.Ajustes;
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
/// <b>Hoy el único documento de este módulo es el ajuste.</b> Cuando lleguen los albaranes y los
/// recuentos, se suman <b>aquí</b>: el cierre no se entera y no hay que acordarse de tocarlo. Que
/// este método se quede corto el día que aparezca un documento nuevo es el modo de fallo de esta
/// clase, y contra eso está el barrido que compara los módulos registrados con los declarados.
/// </para>
/// <para>
/// <b>La fecha que decide es <c>FechaDeOperacion</c></b>, no <c>CreadoEn</c>: es la fecha del
/// documento, la que la R9 llama «la fecha del movimiento» y la única que se compara contra el
/// intervalo del ejercicio. <c>CreadoEn</c> es cuándo se tecleó, que es otra cosa y no decide nada.
/// </para>
/// </remarks>
internal sealed class LosDocumentosDeInventarioEnUnPeriodo(InventarioDbContext contexto)
    : IDocumentosDeUnPeriodo
{
    /// <inheritdoc/>
    public string Modulo => "Inventario";

    /// <inheritdoc/>
    public Task<bool> HayBorradoresEnAsync(
        Guid empresaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion) =>
        contexto.Ajustes.AnyAsync(
            ajuste => ajuste.EmpresaId == empresaId
                && ajuste.Estado == EstadoDeAjuste.Borrador
                && ajuste.FechaDeOperacion >= desde
                && ajuste.FechaDeOperacion <= hasta,
            cancelacion);

    /// <inheritdoc/>
    public Task<bool> HayDocumentosEnAsync(
        Guid empresaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion) =>
        // Sin filtrar por estado: los anulados también cuentan. Un ajuste anulado sigue siendo una
        // fila del libro —se corrige con un inverso, no se borra (R11)— y sacarlo del intervalo de
        // su ejercicio dejaría al inverso y al original en periodos distintos.
        contexto.Ajustes.AnyAsync(
            ajuste => ajuste.EmpresaId == empresaId
                && ajuste.FechaDeOperacion >= desde
                && ajuste.FechaDeOperacion <= hasta,
            cancelacion);
}
