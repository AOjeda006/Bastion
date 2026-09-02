using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.Organizacion.Domain.Divisas;

/// <summary>
/// Cuánto valía una divisa en otra un día concreto.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lleva las DOS divisas escritas, y el §7 solo pedía una.</b> Su nota dice «TipoCambio (fecha,
/// divisa, tasa)», que da por supuesta una divisa de referencia implícita. No la hay: la divisa
/// base es un campo <b>de cada empresa</b> (§7.1), y la R8 permite —de hecho es su motivo— que en
/// la misma instalación convivan sociedades con bases distintas. Con una sola columna, la tasa
/// «1,08» no dice si es dólares por euro o euros por dólar, y la respuesta dependería de qué
/// empresa la lea: el mismo número saldría bien en una y del revés en otra. Escribir origen y
/// destino cuesta una columna y quita la ambigüedad entera, y sigue admitiendo el caso sencillo,
/// que es tener solo filas contra el euro.
/// </para>
/// <para>
/// <b>La dirección se lee así:</b> <c>Tasa</c> unidades de <see cref="DivisaDestinoId"/> por
/// <b>una</b> unidad de <see cref="DivisaOrigenId"/>. USD por EUR a 1,08 significa que un euro
/// compra 1,08 dólares.
/// </para>
/// <para>
/// <b>Se puede corregir, y no reescribe nada.</b> Un tipo mal tecleado se arregla, porque es el
/// dato de un día y no un asiento. Lo que impide que la corrección viaje hacia atrás es que el
/// documento que usó un cambio se queda con el número copiado encima —eso llega en la fase 5, no
/// aquí—, no que esta fila sea inmutable. Confundir las dos cosas lleva a tablas que no se pueden
/// arreglar y a documentos que se reescriben solos.
/// </para>
/// <para>
/// La tasa es <see cref="decimal"/> por la R6, con seis decimales, que es lo que publica el Banco
/// Central Europeo. En coma flotante, convertir y volver a convertir no devuelve el importe de
/// partida, y esa diferencia aparece como un descuadre de céntimos que nadie sabe de dónde sale.
/// </para>
/// </remarks>
public sealed class TipoCambio : EntidadBase
{
    /// <summary>Decimales de la tasa: los que publica el BCE.</summary>
    public const int DecimalesDeLaTasa = 6;

    private TipoCambio(
        Guid id,
        Guid divisaOrigenId,
        Guid divisaDestinoId,
        DateOnly fecha,
        decimal tasa,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        DivisaOrigenId = divisaOrigenId;
        DivisaDestinoId = divisaDestinoId;
        Fecha = fecha;
        Tasa = tasa;
    }

    private TipoCambio()
    {
    }

    /// <summary>Identificador del tipo de cambio.</summary>
    public Guid Id { get; private set; }

    /// <summary>Divisa de la que se parte: la que vale <b>una</b> unidad.</summary>
    public Guid DivisaOrigenId { get; private set; }

    /// <summary>Divisa a la que se llega: en la que se expresa <see cref="Tasa"/>.</summary>
    public Guid DivisaDestinoId { get; private set; }

    /// <summary>Día al que corresponde. Fecha de negocio: sin hora y sin zona (R14).</summary>
    public DateOnly Fecha { get; private set; }

    /// <summary>Unidades de destino por una unidad de origen.</summary>
    public decimal Tasa { get; private set; }

    /// <summary>Registra un tipo de cambio.</summary>
    /// <param name="divisaOrigenId">Divisa de la que se parte.</param>
    /// <param name="divisaDestinoId">Divisa a la que se llega.</param>
    /// <param name="fecha">Día al que corresponde.</param>
    /// <param name="tasa">Unidades de destino por una unidad de origen.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static TipoCambio Crear(
        Guid divisaOrigenId,
        Guid divisaDestinoId,
        DateOnly fecha,
        decimal tasa,
        DateTimeOffset momento)
    {
        if (divisaOrigenId == Guid.Empty)
        {
            throw new ArgumentException("Un tipo de cambio parte de una divisa.", nameof(divisaOrigenId));
        }

        if (divisaDestinoId == Guid.Empty)
        {
            throw new ArgumentException("Un tipo de cambio llega a una divisa.", nameof(divisaDestinoId));
        }

        // Una divisa contra sí misma vale uno por definición, y guardarla abriría la puerta a
        // guardarla valiendo otra cosa. La conversión trivial la resuelve quien convierte.
        if (divisaOrigenId == divisaDestinoId)
        {
            throw new ArgumentException(
                "Origen y destino son la misma divisa: una divisa vale exactamente uno de sí misma, " +
                "y esa fila solo puede sobrar o mentir.",
                nameof(divisaDestinoId));
        }

        return new TipoCambio(
            Guid.CreateVersion7(), divisaOrigenId, divisaDestinoId, fecha, TasaValida(tasa), momento);
    }

    /// <summary>Corrige la tasa. Ni las divisas ni la fecha: eso sería otra fila.</summary>
    /// <param name="tasa">Unidades de destino por una unidad de origen.</param>
    public void Modificar(decimal tasa) => Tasa = TasaValida(tasa);

    private static decimal TasaValida(decimal tasa)
    {
        // Cero no es «no hay cambio»: es una divisa que no vale nada, y convertir con ella anula
        // el importe en silencio. Negativo no tiene ni lectura.
        if (tasa <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tasa), tasa, "Un tipo de cambio es mayor que cero.");
        }

        return decimal.Round(tasa, DecimalesDeLaTasa, MidpointRounding.AwayFromZero);
    }
}
