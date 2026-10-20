namespace Bastion.Inventario.Contracts.Lineas;

/// <summary>
/// La línea de la petición que paró el alta, en la extensión <c>actual</c> del problema
/// (ADR-0065 §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>La llevan los <c>409</c> y los <c>422</c> de una línea</b>: la ubicación bloqueada, el artículo
/// que no se almacena, la unidad retirada y la trazabilidad que no casa. Un <c>400</c> dice lo mismo
/// en <c>errors</c>, con la ruta del campo, porque solo una validación tiene campos.
/// </para>
/// <para>
/// <b>Es de los dos documentos con líneas</b>, el ajuste y la transferencia (ADR-0068 §6), y no se
/// publica en el OpenAPI: <c>actual</c> no tiene esquema.
/// </para>
/// </remarks>
/// <param name="Linea">Su número, desde uno, que es el que enseña la ficha.</param>
public sealed record LineaQueFallaDto(int Linea);

/// <summary>
/// La conversión que le falta a una línea, o que está retirada, en la extensión <c>actual</c> del
/// problema (ADR-0065 §2).
/// </summary>
/// <remarks>
/// <b>Con las dos unidades</b>: quien lo lee sabe qué conversión dar de alta, o cuál está retirada,
/// sin volver a preguntar por el artículo.
/// </remarks>
/// <param name="Linea">Su número, desde uno.</param>
/// <param name="UnidadIntroducidaId">La unidad en la que se escribió la línea.</param>
/// <param name="UnidadBaseId">La unidad base de su artículo.</param>
public sealed record ConversionDeLaLineaDto(int Linea, Guid UnidadIntroducidaId, Guid UnidadBaseId);

/// <summary>
/// La ubicación bloqueada de una línea de transferencia, con su punta, en la extensión
/// <c>actual</c> del problema (ADR-0068 §6).
/// </summary>
/// <remarks>
/// <b>La línea no basta</b>, porque lleva dos ubicaciones: sin la punta, la pantalla no sabría en
/// cuál de las dos celdas poner el error.
/// </remarks>
/// <param name="Linea">Su número, desde uno.</param>
/// <param name="Punta"><see cref="Origen"/> o <see cref="Destino"/>.</param>
public sealed record UbicacionDeLaLineaDto(int Linea, string Punta)
{
    /// <summary>La ubicación de donde sale.</summary>
    public const string Origen = "Origen";

    /// <summary>La ubicación a donde llega.</summary>
    public const string Destino = "Destino";
}
