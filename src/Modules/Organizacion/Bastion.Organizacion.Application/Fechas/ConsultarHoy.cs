using Bastion.BuildingBlocks.Application.Fechas;
using Bastion.Organizacion.Contracts.Fechas;

namespace Bastion.Organizacion.Application.Fechas;

/// <summary>Devuelve hoy en la empresa activa y en cada zona admitida (ADR-0079 §12).</summary>
public interface IConsultarHoy
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<HoyDto> EjecutarAsync(CancellationToken cancelacion);
}

/// <inheritdoc cref="IConsultarHoy"/>
/// <remarks>
/// Solo traduce: los días los calcula <see cref="IHoyEnLaEmpresa"/>, que es el mismo que usa todo
/// lo que fecha sin almacén, así que lo que lee la pantalla y lo que comprueba el servidor no
/// pueden salir de dos reglas.
/// </remarks>
internal sealed class ConsultarHoy(IHoyEnLaEmpresa hoy) : IConsultarHoy
{
    public async Task<HoyDto> EjecutarAsync(CancellationToken cancelacion)
    {
        HoyEnTodas dias = await hoy.EnTodasAsync(cancelacion).ConfigureAwait(false);

        return new HoyDto(
            dias.DeLaEmpresa,
            dias.ElMasAdelantado,
            [.. dias.EnCadaZona.Select(enLaZona =>
                new HoyEnUnaZonaDto(enLaZona.Zona.AlContrato(), enLaZona.Dia, enLaZona.AcabaEn))]);
    }
}
