using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.Organizacion.Domain.Unidades;

/// <summary>
/// Cuántas unidades de destino hay en <b>una</b> de origen: una caja son doce unidades.
/// </summary>
/// <remarks>
/// <para>
/// <b>La dirección está escrita y no se invierte sola.</b> <c>Factor</c> unidades de
/// <see cref="UnidadDestinoId"/> por <b>una</b> de <see cref="UnidadOrigenId"/>. Tentador sería
/// que CAJA→UD con factor 12 valiera también para UD→CAJA dividiendo, y es justo lo que no se
/// hace: el inverso de 12 no cabe en seis decimales, así que ir y volver no devuelve la cantidad
/// de partida, y el descuadre aparece en el inventario y no en ningún error. Si hace falta la
/// vuelta, se da de alta con su propio factor y su propio redondeo pensado.
/// </para>
/// <para>
/// <b>No hay transitividad.</b> Tener CAJA→UD y UD→G no da CAJA→G: encadenar dos factores
/// redondeados multiplica el error, y el sistema no va a inventar una conversión que nadie ha
/// declarado. Las que hagan falta se dan de alta.
/// </para>
/// <para>
/// Maestro de la instalación, como las unidades que relaciona (R8). El factor es
/// <see cref="decimal"/> por la R6: en coma flotante, 0,1 kg tres veces no son 0,3 kg.
/// </para>
/// </remarks>
public sealed class ConversionUM : EntidadBase
{
    /// <summary>Decimales del factor.</summary>
    public const int DecimalesDelFactor = 6;

    private ConversionUM(
        Guid id,
        Guid unidadOrigenId,
        Guid unidadDestinoId,
        decimal factor,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        UnidadOrigenId = unidadOrigenId;
        UnidadDestinoId = unidadDestinoId;
        Factor = factor;
    }

    private ConversionUM()
    {
    }

    /// <summary>Identificador de la conversión.</summary>
    public Guid Id { get; private set; }

    /// <summary>Unidad de la que se parte: la que vale <b>una</b>.</summary>
    public Guid UnidadOrigenId { get; private set; }

    /// <summary>Unidad a la que se llega: en la que se expresa <see cref="Factor"/>.</summary>
    public Guid UnidadDestinoId { get; private set; }

    /// <summary>Unidades de destino que hay en una de origen.</summary>
    public decimal Factor { get; private set; }

    /// <summary>Da de alta una conversión entre dos unidades.</summary>
    /// <param name="unidadOrigenId">Unidad de la que se parte.</param>
    /// <param name="unidadDestinoId">Unidad a la que se llega.</param>
    /// <param name="factor">Unidades de destino por una de origen.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static ConversionUM Crear(
        Guid unidadOrigenId,
        Guid unidadDestinoId,
        decimal factor,
        DateTimeOffset momento)
    {
        if (unidadOrigenId == Guid.Empty)
        {
            throw new ArgumentException("Una conversión parte de una unidad.", nameof(unidadOrigenId));
        }

        if (unidadDestinoId == Guid.Empty)
        {
            throw new ArgumentException("Una conversión llega a una unidad.", nameof(unidadDestinoId));
        }

        if (unidadOrigenId == unidadDestinoId)
        {
            throw new ArgumentException(
                "Origen y destino son la misma unidad: esa conversión vale uno por definición, y " +
                "la fila solo puede sobrar o mentir.",
                nameof(unidadDestinoId));
        }

        return new ConversionUM(
            Guid.CreateVersion7(), unidadOrigenId, unidadDestinoId, FactorValido(factor), momento);
    }

    /// <summary>Corrige el factor. Las unidades no: eso sería otra conversión.</summary>
    /// <param name="factor">Unidades de destino por una de origen.</param>
    public void Modificar(decimal factor) => Factor = FactorValido(factor);

    private static decimal FactorValido(decimal factor)
    {
        // Cero convertiría cualquier existencia en nada, y en silencio.
        if (factor <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(factor), factor, "Un factor de conversión es mayor que cero.");
        }

        return decimal.Round(factor, DecimalesDelFactor, MidpointRounding.AwayFromZero);
    }
}
