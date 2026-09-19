using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Confirma un ajuste y mueve el libro.</summary>
public interface IConfirmarAjuste
{
    /// <summary>Ejecuta la confirmación.</summary>
    /// <param name="ajusteId">El documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El ajuste confirmado, o el motivo por el que no se confirma.</returns>
    Task<Resultado<AjusteDto>> EjecutarAsync(Guid ajusteId, CancellationToken cancelacion);
}

/// <summary>
/// La transición que hace verdad el documento: cabecera y filas del libro, en el mismo
/// <c>COMMIT</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta es la transacción que dobla la R12 a sabiendas</b>, y está decidido y escrito en
/// <c>docs/PLAN.md</c>: se modifican dos agregados —el <c>Ajuste</c> y las filas del libro— en una
/// sola transacción. La alternativa era publicar un evento y que un suscriptor escribiera los
/// movimientos, y eso deja una ventana con un ajuste confirmado y el stock sin mover: rompería la
/// R3, que dice que el libro <b>es</b> la verdad. De las dos, es peor romper la R3.
/// </para>
/// <para>
/// <b>Los movimientos no se construyen aquí.</b> Los devuelve <c>Ajuste.Confirmar</c>, que es lo
/// que hace imposible confirmar sin generarlos: si este caso de uso se olvidara de guardarlos,
/// tendría delante una lista sin usar en vez de un documento confirmado y un libro quieto.
/// </para>
/// <para>
/// <b>Y el evento va por documento, no por movimiento.</b> Uno por fila convertiría la bandeja en
/// una segunda copia de la tabla que más crece del sistema.
/// </para>
/// </remarks>
/// <param name="ajustes">Dónde viven el documento y el libro.</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class ConfirmarAjuste(
    IRepositorioDeAjustes ajustes,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IConfirmarAjuste
{
    /// <inheritdoc/>
    public async Task<Resultado<AjusteDto>> EjecutarAsync(
        Guid ajusteId,
        CancellationToken cancelacion)
    {
        Ajuste? ajuste = await ajustes.ObtenerAsync(ajusteId, cancelacion).ConfigureAwait(false);

        if (ajuste is null)
        {
            return Resultado.Fallo<AjusteDto>(ErroresDeAjuste.NoEncontrado(ajusteId));
        }

        // El estado se comprueba ANTES de transitar, aunque la transición también lo compruebe.
        // No es una comprobación repetida por costumbre: el dominio lanza —una transición
        // imposible es una invariante rota— y lo que el borde necesita para un ajuste ya
        // confirmado es un 409 con su motivo, no un 500 (ADR-0004).
        if (ajuste.Estado != EstadoDeAjuste.Borrador)
        {
            return Resultado.Fallo<AjusteDto>(
                ErroresDeAjuste.NoEstaEnBorrador(ajusteId, ajuste.Estado.ToString()));
        }

        var evento = new AjusteConfirmado(
            ajuste.Id,
            ajuste.EmpresaId,
            ajuste.AlmacenId,
            ajuste.FechaDeOperacion,
            ajuste.Lineas.Count);

        IReadOnlyList<MovimientoStock> movimientos = ajuste.Confirmar(evento, reloj.GetUtcNow());

        ajustes.AgregarMovimientos(movimientos);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(ajuste.ADto());
    }
}
