namespace Bastion.Organizacion.Contracts.Series;

/// <summary>
/// Qué documentos numera una serie y entre qué fechas, que son las del ejercicio del que cuelga.
/// </summary>
/// <remarks>
/// Son las dos cosas que la sentencia que numera mira además del estado (ADR-0043): el tipo, que
/// contesta <c>serie-de-otro-documento</c> si no casa, y el ejercicio, que contesta
/// <c>fecha-fuera-del-ejercicio-de-la-serie</c> si la fecha del documento no cae dentro.
/// </remarks>
/// <param name="TipoDeDocumento">
/// El nombre del valor de <c>TipoDeDocumento</c>, que es lo que guarda la columna: el enumerado es de
/// <c>Organizacion.Domain</c>, y quien pregunta desde otro módulo no lo ve.
/// </param>
/// <param name="Desde">El primer día del ejercicio de la serie.</param>
/// <param name="Hasta">El último día del ejercicio de la serie.</param>
public sealed record LoQueNumeraUnaSerie(string TipoDeDocumento, DateOnly Desde, DateOnly Hasta);
