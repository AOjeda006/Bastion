using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Unidades;

/// <summary>Una conversión entre dos unidades de medida, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la conversión.</param>
/// <param name="UnidadOrigenId">Unidad desde la que se convierte.</param>
/// <param name="UnidadDestinoId">Unidad a la que se convierte.</param>
/// <param name="Factor">Por cuánto hay que multiplicar para pasar de origen a destino.</param>
/// <param name="Retirada">
/// Si la fila se ha retirado: no se ofrece para operaciones nuevas, pero sigue resolviendo lo que
/// ya apunta a ella (ADR-0023). Sale en el DTO porque el listado puede traerla —con
/// <c>?retiradas=true</c>— y porque el <c>GET</c> por identificador la devuelve siempre: sin este
/// campo, quien la lee no tiene manera de distinguirla de una que sí se ofrece.
/// </param>
/// <remarks>
/// <b>La inversa no sale sola, y es a propósito.</b> Doce unidades por caja se declara en un
/// sentido; el otro sería 1/12, que no cabe en seis decimales, y una inversa calculada convertiría
/// doce unidades en 0,999996 cajas. Si hace falta el camino de vuelta, se da de alta con el
/// factor que corresponda. Tampoco hay transitividad: de caja a palé no se deduce de caja a
/// unidad más unidad a palé.
/// </remarks>
public sealed record ConversionUmDto(
    Guid Id,
    Guid UnidadOrigenId,
    Guid UnidadDestinoId,
    decimal Factor,
    bool Retirada);

/// <summary>Lo que hace falta para dar de alta una conversión.</summary>
public sealed record CrearConversionUmDto
{
    /// <summary>Unidad desde la que se convierte.</summary>
    [Required(ErrorMessage = "La unidad de origen es obligatoria.")]
    public Guid UnidadOrigenId { get; init; }

    /// <summary>Unidad a la que se convierte.</summary>
    [Required(ErrorMessage = "La unidad de destino es obligatoria.")]
    public Guid UnidadDestinoId { get; init; }

    /// <summary>
    /// Por cuánto hay que multiplicar para pasar de origen a destino.
    /// </summary>
    /// <remarks>
    /// <b><c>ParseLimitsInInvariantCulture</c> no es adorno: sin él esta acción devuelve 500.</b>
    /// <see cref="RangeAttribute"/> convierte sus dos límites con la cultura ACTUAL, y en
    /// <c>es-ES</c> —que es la de Bastion— el punto separa millares, así que <c>"0.000001"</c> no
    /// es un decimal y el atributo lanza dentro del enlazado de modelos. Lo cuenta entero
    /// <c>TipoCambioDto</c>, y lo vigila <c>LosLimitesSeLeenEnCulturaInvarianteTests</c>.
    /// </remarks>
    [Range(
        typeof(decimal),
        "0.000001",
        "1000000",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "El factor va de {1} a {2}.")]
    public decimal Factor { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de una conversión.
/// </summary>
/// <remarks>
/// Solo el factor. El par de unidades es la identidad de la fila —hay un índice único sobre él—,
/// así que cambiarlo no sería corregir esta conversión sino inventar otra.
/// </remarks>
public sealed record ModificarConversionUmDto
{
    /// <summary>Por cuánto hay que multiplicar para pasar de origen a destino.</summary>
    [Range(
        typeof(decimal),
        "0.000001",
        "1000000",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "El factor va de {1} a {2}.")]
    public decimal Factor { get; init; }
}

/// <summary>
/// La conversión que resuelve un par de unidades, tal como sale del resolutor.
/// </summary>
/// <param name="ConversionId">Identificador de la fila que resuelve el par.</param>
/// <param name="UnidadOrigenId">Unidad desde la que se convierte.</param>
/// <param name="UnidadDestinoId">Unidad a la que se convierte.</param>
/// <param name="Factor">Por cuánto hay que multiplicar para pasar de origen a destino.</param>
/// <param name="Retirada">
/// Si la fila que resuelve está retirada (ADR-0023). Sale porque una fila retirada <b>sigue
/// resolviendo</b> —es lo que sigue haciendo— y quien la use para una operación nueva tiene
/// derecho a saber que ese camino ya no se ofrece.
/// </param>
/// <remarks>
/// <b>No compone cadenas, y ese es el punto entero</b> (ADR-0023, decisión 3). Tener
/// <c>kg→g</c> y <c>g→mg</c> no da <c>kg→mg</c>: preguntarlo devuelve un error con nombre y no un
/// 1000000. Encadenar multiplicaría el error de redondeo, el orden de composición sería una
/// entrada invisible cuando hay varios caminos, y el número que saliera dependería de un detalle
/// de implementación que nadie ve.
/// </remarks>
public sealed record ResolucionDeConversionDto(
    Guid ConversionId,
    Guid UnidadOrigenId,
    Guid UnidadDestinoId,
    decimal Factor,
    bool Retirada);
