using Bastion.BuildingBlocks.Domain.Fechas;

namespace Bastion.Api.IntegrationTests.Fechas;

/// <summary>
/// «Hoy» para un caso de este carril: el día en la zona que tiene una empresa dada de alta sin
/// decir la suya, que es como se dan de alta todas las de aquí salvo las que prueban la zona.
/// </summary>
/// <remarks>
/// Hasta el 3.5 los casos calculaban el día UTC, que era el que usaba el servidor. Desde el
/// ADR-0079 el servidor pregunta a la zona del almacén, y un caso que siguiera en UTC vería otro día
/// entre las 22:00 y la medianoche UTC en verano, y entre las 23:00 y la medianoche en invierno: un
/// rojo que solo sale de noche. Por eso el día sale de aquí, y no de cada caso.
/// </remarks>
internal static class ElDiaDeLaPrueba
{
    /// <summary>Hoy en la zona por omisión, con el reloj de verdad, que es el de la API.</summary>
    internal static DateOnly Hoy() => DiaDeNegocio.Hoy(TimeProvider.System, ZonaHoraria.PorOmision);
}
