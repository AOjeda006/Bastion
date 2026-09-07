using System.Globalization;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Retiradas;

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
public sealed class ConversionUM : EntidadBase, IRetirable
{
    /// <summary>Decimales del factor.</summary>
    public const int DecimalesDelFactor = 6;

    /// <summary>Factor más pequeño que se admite: el más pequeño que cabe en seis decimales.</summary>
    /// <remarks>
    /// No es un número elegido: es <c>10⁻⁶</c>, el escalón de la escala en la que se guarda. Por
    /// debajo, el redondeo lo convierte en cero, y un factor cero convierte cualquier existencia
    /// en nada y en silencio. Es el suelo del rango que el ADR-0023 cita.
    /// </remarks>
    public const decimal FactorMinimo = 0.000001m;

    /// <summary>Factor más grande que se admite.</summary>
    /// <remarks>
    /// El techo del rango del ADR-0023, y está aquí porque su decisión 2 se apoya en él: «como el
    /// rango declarado del factor es <c>[0,000001, 1000000]</c>, la inversa de cualquier factor
    /// válido cae también dentro del rango: la regla es <b>total</b>, no tiene casos en los que se
    /// calle». Sin el tope, un factor de <c>10¹⁵</c> tendría una inversa que no cabe en seis
    /// decimales —se guardaría como cero, y ni siquiera se podría declarar—, y la comprobación de
    /// la inversa tendría un caso en el que callarse. El argumento estaba escrito; lo que faltaba
    /// era lo que lo sostiene.
    /// </remarks>
    public const decimal FactorMaximo = 1000000m;

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

    /// <inheritdoc />
    public bool EstaRetirada { get; private set; }

    /// <inheritdoc />
    public void Retirar() => EstaRetirada = true;

    /// <inheritdoc />
    public void Reincorporar() => EstaRetirada = false;

    /// <summary>Deja el factor en la forma exacta en la que se guarda.</summary>
    /// <remarks>
    /// No valida, por lo mismo que <c>Divisa.NormalizarCodigo</c>: quien tiene que comparar este
    /// factor con otro —la comprobación de la inversa, que relaciona dos filas— necesita compararlo
    /// ya redondeado, porque redondeado es como va a quedar guardado, y una pregunta no tiene por
    /// qué reventar.
    /// </remarks>
    /// <param name="factor">Factor tal como lo escribieron.</param>
    public static decimal RedondearFactor(decimal factor) =>
        decimal.Round(factor, DecimalesDelFactor, MidpointRounding.AwayFromZero);

    private static decimal FactorValido(decimal factor)
    {
        // Se redondea PRIMERO y se comprueba el número que se va a guardar. Al revés —que es como
        // estaba— `0,0000001` pasaba la comprobación, porque es mayor que cero, y se guardaba
        // como cero: exactamente el fallo que la línea de abajo dice impedir, entrando por la
        // puerta de al lado. Una guarda que mira un valor distinto del que se persiste no guarda
        // nada.
        decimal redondeado = RedondearFactor(factor);

        // Cero convertiría cualquier existencia en nada, y en silencio.
        if (redondeado < FactorMinimo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(factor),
                factor,
                $"Un factor de conversión no baja de " +
                $"{FactorMinimo.ToString(CultureInfo.InvariantCulture)}: con seis decimales, " +
                "por debajo de ahí el número que se guarda es cero.");
        }

        return redondeado <= FactorMaximo
            ? redondeado
            : throw new ArgumentOutOfRangeException(
                nameof(factor),
                factor,
                $"Un factor de conversión no pasa de " +
                $"{FactorMaximo.ToString(CultureInfo.InvariantCulture)}. El tope no es " +
                "estético: el ADR-0023 apoya en este rango que la tolerancia de la inversa sea " +
                "total, y un factor mayor tiene una inversa que no cabe en seis decimales, así " +
                "que la vuelta no se podría declarar y la regla se quedaría sin caso que mirar.");
    }
}
