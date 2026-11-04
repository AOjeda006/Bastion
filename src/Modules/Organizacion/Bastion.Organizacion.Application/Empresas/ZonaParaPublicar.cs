using Bastion.BuildingBlocks.Domain.Fechas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>La zona de la empresa, para publicar la que rige en sus almacenes.</summary>
internal static class ZonaParaPublicar
{
    /// <summary>La zona de la empresa, o la de por omisión si no se ve.</summary>
    /// <remarks>
    /// <b>Solo para leer, nunca para fechar.</b> Si la empresa no se ve —se bloqueó con una sesión
    /// abierta, que deja de renovarse—, devuelve la de por omisión: lo que se publica es la ficha
    /// de un almacén, y una zona aproximada en una ficha que ya no se puede tocar no fecha nada.
    /// Lo que fecha un documento pregunta con <see cref="IRepositorioDeEmpresas.ZonaHorariaDeAsync"/>
    /// y para si es nula.
    /// </remarks>
    /// <param name="empresas">El repositorio.</param>
    /// <param name="id">La empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Su zona, o la de por omisión.</returns>
    internal static async Task<ZonaHoraria> ZonaParaPublicarAsync(
        this IRepositorioDeEmpresas empresas, Guid id, CancellationToken cancelacion) =>
        await empresas.ZonaHorariaDeAsync(id, cancelacion).ConfigureAwait(false) ?? ZonaHoraria.PorOmision;
}
