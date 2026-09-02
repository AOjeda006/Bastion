using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Divisas;

/// <summary>La cotización de un par de divisas en un día, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la cotización.</param>
/// <param name="DivisaOrigenId">Divisa de la que se convierte.</param>
/// <param name="DivisaDestinoId">Divisa a la que se convierte.</param>
/// <param name="Fecha">Día al que corresponde la cotización.</param>
/// <param name="Tasa">Cuántas unidades de destino cuesta una de origen.</param>
/// <remarks>
/// <b>Lleva las DOS divisas, y el §7 solo nombraba una.</b> Allí la terna era «(fecha, divisa,
/// tasa)», con una base implícita; pero la divisa base es un campo de cada empresa y la R8 deja
/// que dos sociedades de la misma instalación tengan bases distintas. Con una sola columna, la
/// misma fila significaría «dólares por euro» para una y «dólares por libra» para otra, y nada en
/// el esquema diría cuál. La desviación está anotada donde vive la entidad.
/// </remarks>
public sealed record TipoCambioDto(
    Guid Id,
    Guid DivisaOrigenId,
    Guid DivisaDestinoId,
    DateOnly Fecha,
    decimal Tasa);

/// <summary>Lo que hace falta para registrar la cotización de un día.</summary>
public sealed record CrearTipoCambioDto
{
    /// <summary>Divisa de la que se convierte.</summary>
    [Required(ErrorMessage = "La divisa de origen es obligatoria.")]
    public Guid DivisaOrigenId { get; init; }

    /// <summary>Divisa a la que se convierte.</summary>
    [Required(ErrorMessage = "La divisa de destino es obligatoria.")]
    public Guid DivisaDestinoId { get; init; }

    /// <summary>Día al que corresponde la cotización.</summary>
    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateOnly Fecha { get; init; }

    /// <summary>
    /// Cuántas unidades de destino cuesta una de origen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se redondea a seis decimales, que es la precisión con la que publica el BCE. El límite
    /// superior no es un tope de negocio sino una red: una tasa de siete cifras enteras es
    /// siempre un dedazo, y vale más rechazarla que convertir un importe por un millón.
    /// </para>
    /// <para>
    /// <b><c>ParseLimitsInInvariantCulture</c> no es opcional aquí, es lo que hace que esto
    /// funcione.</b> Sin él, <see cref="RangeAttribute"/> interpreta las dos cadenas con la
    /// cultura ACTUAL, y Bastion corre en <c>es-ES</c>, donde el punto separa millares: el
    /// atributo revienta al construirse y la acción entera contesta <c>500</c> a cualquier
    /// petición con cuerpo. No se descubrió leyendo esto —se descubrió con
    /// <c>LaPuertaDeCadaAccionTests</c>—, y para que no vuelva a colarse hay una regla escrita en
    /// <c>LosLimitesSeLeenEnCulturaInvarianteTests</c>.
    /// </para>
    /// </remarks>
    [Range(
        typeof(decimal),
        "0.000001",
        "1000000",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "La tasa va de {1} a {2}.")]
    public decimal Tasa { get; init; }
}

/// <summary>
/// Lo que se puede rectificar de una cotización.
/// </summary>
/// <remarks>
/// Solo la tasa. El par y el día son la identidad de la fila —hay un índice único sobre los
/// tres—, así que cambiarlos no sería corregir esta cotización sino inventar otra.
/// </remarks>
public sealed record ModificarTipoCambioDto
{
    /// <summary>Cuántas unidades de destino cuesta una de origen.</summary>
    [Range(
        typeof(decimal),
        "0.000001",
        "1000000",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "La tasa va de {1} a {2}.")]
    public decimal Tasa { get; init; }
}
