using Bastion.BuildingBlocks.Contracts.Fechas;
using Bastion.BuildingBlocks.Domain.Fechas;

namespace Bastion.BuildingBlocks.Application.Fechas;

/// <summary>
/// La traducción entre la zona del contrato y la del dominio, en los dos sentidos.
/// </summary>
/// <remarks>
/// Por el identificador IANA y no por la posición: las dos listas se escriben en dos proyectos, y
/// un orden distinto en una de ellas cambiaría la zona de todo lo que pasara por aquí sin que
/// nada fallara. Lo que no está en las dos lanza, y el caso que compara las listas lo pone rojo
/// antes de que llegue a ninguna petición.
/// </remarks>
public static class MapeoDeZonas
{
    /// <summary>La zona del dominio que nombra la del contrato.</summary>
    /// <param name="zona">La zona, como llegó en la petición.</param>
    /// <returns>La misma zona, en el dominio.</returns>
    /// <exception cref="ArgumentOutOfRangeException">El valor no es un miembro del enumerado.</exception>
    public static ZonaHoraria AlDominio(this ZonaHorariaDto zona) => zona switch
    {
        ZonaHorariaDto.Madrid => ZonaHoraria.De("Europe/Madrid"),
        ZonaHorariaDto.Canarias => ZonaHoraria.De("Atlantic/Canary"),
        ZonaHorariaDto.Ceuta => ZonaHoraria.De("Africa/Ceuta"),
        _ => throw new ArgumentOutOfRangeException(nameof(zona), zona, "No es una zona del contrato."),
    };

    /// <summary>La zona del contrato que nombra la del dominio.</summary>
    /// <param name="zona">La zona del dominio.</param>
    /// <returns>La misma zona, en el contrato.</returns>
    /// <exception cref="ArgumentOutOfRangeException">El contrato no la publica.</exception>
    public static ZonaHorariaDto AlContrato(this ZonaHoraria zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        return zona.Iana switch
        {
            "Europe/Madrid" => ZonaHorariaDto.Madrid,
            "Atlantic/Canary" => ZonaHorariaDto.Canarias,
            "Africa/Ceuta" => ZonaHorariaDto.Ceuta,
            _ => throw new ArgumentOutOfRangeException(nameof(zona), zona.Iana, "El contrato no publica esa zona."),
        };
    }
}
