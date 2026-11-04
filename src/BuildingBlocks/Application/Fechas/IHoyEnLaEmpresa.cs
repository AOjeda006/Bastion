using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Fechas;

namespace Bastion.BuildingBlocks.Application.Fechas;

/// <summary>Hoy en la empresa activa: el día de su zona, y el más adelantado de las suyas.</summary>
/// <remarks>
/// <para>
/// Es lo que necesita lo que no tiene almacén y atiende una petición: la empresa sale del
/// <i>claim</i>, como en todo lo demás (R8), y la zona, de <see cref="IZonasDeLaEmpresa"/>. El día
/// lo calcula <see cref="DiaDeNegocio"/>, que sigue siendo el único sitio que lo hace.
/// </para>
/// <para>
/// <b>Lo que tiene almacén no pasa por aquí</b>: pide la zona del almacén y llama a
/// <see cref="DiaDeNegocio"/> con ella, con el mismo instante con el que fecha lo demás. Ni lo que
/// corre sin petición, como el corte, que no tiene <i>claim</i> y recibe la empresa.
/// </para>
/// </remarks>
public interface IHoyEnLaEmpresa
{
    /// <summary>Hoy en la zona de la empresa activa.</summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El día.</returns>
    Task<DateOnly> DeLaEmpresaAsync(CancellationToken cancelacion);

    /// <summary>
    /// Hoy en la más adelantada de las zonas que rigen en la empresa activa: la suya y la de cada
    /// almacén.
    /// </summary>
    /// <remarks>Para lo que llama futuro a una fecha que pudo ser hoy en cualquiera de ellas.</remarks>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El día.</returns>
    Task<DateOnly> ElMasAdelantadoAsync(CancellationToken cancelacion);
}

/// <inheritdoc cref="IHoyEnLaEmpresa"/>
/// <param name="zonas">De dónde sale la zona.</param>
/// <param name="usuario">De dónde sale la empresa.</param>
/// <param name="reloj">De dónde sale el instante.</param>
public sealed class HoyEnLaEmpresa(IZonasDeLaEmpresa zonas, IUsuarioActual usuario, TimeProvider reloj)
    : IHoyEnLaEmpresa
{
    /// <inheritdoc/>
    public async Task<DateOnly> DeLaEmpresaAsync(CancellationToken cancelacion) =>
        DiaDeNegocio.Hoy(
            reloj, await zonas.DeLaEmpresaAsync(usuario.EmpresaId, cancelacion).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<DateOnly> ElMasAdelantadoAsync(CancellationToken cancelacion)
    {
        IReadOnlyList<ZonaHoraria> todas =
            await zonas.TodasAsync(usuario.EmpresaId, cancelacion).ConfigureAwait(false);

        return DiaDeNegocio.ElMasAdelantado(reloj.GetUtcNow(), todas);
    }
}
