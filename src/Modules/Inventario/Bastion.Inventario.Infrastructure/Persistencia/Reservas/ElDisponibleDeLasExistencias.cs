using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Inventario.Contracts.Existencias;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Reservas;

/// <inheritdoc cref="IConsultaDeExistencias"/>
/// <remarks>
/// <para>
/// Lo que Inventario contesta cuando otro módulo quiere saber cuánto puede comprometer. Vive aquí,
/// en el módulo dueño del libro y de las reservas, y se resuelve en proceso (§4, reglas de frontera
/// 1 y 3).
/// </para>
/// <para>
/// <b>Una sola sentencia</b>, sin transacción ni cerrojo: las tres cifras salen de la misma foto, y
/// quien decide con ellas reserva, que las vuelve a mirar bajo cerrojo (ADR-0059 §3).
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario.</param>
/// <param name="inquilino">De dónde sale la empresa, porque la sentencia cruda no pasa por el filtro.</param>
/// <param name="reloj">De dónde sale «ahora», que decide qué reservas han caducado.</param>
internal sealed class ElDisponibleDeLasExistencias(
    InventarioDbContext contexto, IInquilinoActual inquilino, TimeProvider reloj) : IConsultaDeExistencias
{
    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, DisponibleDeUnArticulo>> DisponibleDeAsync(
        Guid almacenId, IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(articulos);

        if (articulos.Count == 0)
        {
            return new Dictionary<Guid, DisponibleDeUnArticulo>();
        }

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está leyendo el disponible dentro de un ámbito sin inquilino, y una existencia es " +
            "siempre de una empresa: sin ella la sentencia sumaría la de cualquiera.");

        List<FilaDelDisponible> filas = await contexto.Database
            .SqlQueryRaw<FilaDelDisponible>(
                LoReservado.SqlDelDisponible, empresaId, reloj.GetUtcNow(), almacenId, articulos.ToArray())
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas.ToDictionary(
            fila => fila.ArticuloId,
            fila => new DisponibleDeUnArticulo(fila.Fisico, fila.Reservado, fila.Disponible));
    }
}
