using Bastion.BuildingBlocks.Domain.Fechas;

namespace Bastion.BuildingBlocks.Application.Fechas;

/// <summary>
/// Las zonas horarias de una empresa: la suya, y la que rige en cada uno de sus almacenes
/// (ADR-0079).
/// </summary>
/// <remarks>
/// <para>
/// <b>Vive en el bloque común y no en el <c>Contracts</c> de Organización</b>, como
/// <c>IConsultaDeLoBloqueado</c>. Preguntan Catálogo, Inventario y Terceros, pero también la
/// exportación de un listado, que es del bloque común y no puede conocer ningún módulo. Contesta
/// Organización, que es la dueña de las dos zonas.
/// </para>
/// <para>
/// <b>Se pregunta cada vez, y no se guarda.</b> La zona se lee al comprobar: cambiarla cambia lo
/// que se acepta desde ese momento, y nada de lo que ya está fechado.
/// </para>
/// <para>
/// <b>Lo bloqueado también contesta</b>, como contesta su estado (ADR-0037): anular un ajuste de un
/// almacén que se bloqueó después tiene que saber qué día es allí. Lo que sale es una zona, que no
/// dice nada de nadie.
/// </para>
/// <para>
/// <b>Lo que no existe lanza.</b> Quien pregunta ya leyó la empresa del <i>claim</i>, o el almacén
/// de su documento, así que un identificador que no está es un invariante roto, y no una entrada
/// que haya que contestar.
/// </para>
/// </remarks>
public interface IZonasDeLaEmpresa
{
    /// <summary>La zona de una empresa: la que rige lo que no tiene almacén.</summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Su zona.</returns>
    /// <exception cref="InvalidOperationException">La empresa no existe.</exception>
    Task<ZonaHoraria> DeLaEmpresaAsync(Guid empresaId, CancellationToken cancelacion);

    /// <summary>La zona que rige en un almacén: la suya, o la de su empresa si no tiene.</summary>
    /// <param name="almacenId">El almacén.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Su zona efectiva.</returns>
    /// <exception cref="InvalidOperationException">El almacén no existe.</exception>
    Task<ZonaHoraria> DelAlmacenAsync(Guid almacenId, CancellationToken cancelacion);

    /// <summary>
    /// Todas las que rigen en una empresa: la suya y la efectiva de cada almacén, sin repetir.
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Las zonas, la de la empresa la primera, y al menos esa.</returns>
    /// <exception cref="InvalidOperationException">La empresa no existe.</exception>
    Task<IReadOnlyList<ZonaHoraria>> TodasAsync(Guid empresaId, CancellationToken cancelacion);
}
