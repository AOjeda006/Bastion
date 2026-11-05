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

    /// <summary>
    /// Hoy en todo lo que puede necesitar quien rellena un formulario: el de la empresa, el más
    /// adelantado, y el de cada zona admitida, todos del mismo instante.
    /// </summary>
    /// <remarks>
    /// Es lo que publica la API para el frontal, que no calcula el día (ADR-0079 §12). Las tres zonas
    /// admitidas, y no solo las de la empresa: un almacén puede cambiar de zona mientras la pantalla
    /// tiene la lista de antes, y su día tiene que estar igual.
    /// </remarks>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Los días.</returns>
    Task<HoyEnTodas> EnTodasAsync(CancellationToken cancelacion);
}

/// <summary>Hoy, de una vez, en la empresa activa y en cada zona admitida.</summary>
/// <param name="DeLaEmpresa">Hoy en la zona de la empresa.</param>
/// <param name="ElMasAdelantado">Hoy en la más adelantada de las zonas que rigen en la empresa.</param>
/// <param name="EnCadaZona">Hoy en cada zona admitida, en el orden de la lista.</param>
public sealed record HoyEnTodas(DateOnly DeLaEmpresa, DateOnly ElMasAdelantado, IReadOnlyList<HoyEnUnaZona> EnCadaZona);

/// <summary>Hoy en una zona, y el instante en que deja de serlo.</summary>
/// <param name="Zona">La zona.</param>
/// <param name="Dia">El día.</param>
/// <param name="AcabaEn">El principio del día siguiente en la misma zona, en UTC.</param>
public sealed record HoyEnUnaZona(ZonaHoraria Zona, DateOnly Dia, DateTimeOffset AcabaEn);

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

    /// <inheritdoc/>
    public async Task<HoyEnTodas> EnTodasAsync(CancellationToken cancelacion)
    {
        ZonaHoraria suya = await zonas.DeLaEmpresaAsync(usuario.EmpresaId, cancelacion).ConfigureAwait(false);
        IReadOnlyList<ZonaHoraria> todas =
            await zonas.TodasAsync(usuario.EmpresaId, cancelacion).ConfigureAwait(false);

        // Una sola lectura del reloj: dos podrían caer a los dos lados de una medianoche, y la
        // empresa diría un día y su propia zona, otro.
        DateTimeOffset ahora = reloj.GetUtcNow();

        return new HoyEnTodas(
            DiaDeNegocio.De(ahora, suya),
            DiaDeNegocio.ElMasAdelantado(ahora, todas),
            [.. ZonaHoraria.Admitidas.Select(zona => EnLaZona(ahora, zona))]);
    }

    private static HoyEnUnaZona EnLaZona(DateTimeOffset ahora, ZonaHoraria zona)
    {
        DateOnly dia = DiaDeNegocio.De(ahora, zona);

        return new HoyEnUnaZona(zona, dia, DiaDeNegocio.Inicio(dia.AddDays(1), zona));
    }
}
