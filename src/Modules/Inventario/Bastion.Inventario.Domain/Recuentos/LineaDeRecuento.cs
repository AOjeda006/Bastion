using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Domain.Recuentos;

/// <summary>
/// Lo que el recuento dice de una clave del almacén: lo que se contó, en la unidad base de su
/// artículo, y el teórico que había cuando se contó.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin contar no es cero</b> (ADR-0055 §5): <see cref="Contado"/> es <c>null</c> hasta que alguien
/// cuenta, y un recuento con una línea así no se confirma. Contar cero es un dato.
/// </para>
/// <para>
/// <b>El teórico de cuando se contó no es el que decide</b>: decide el de la confirmación, leído con
/// las valoraciones bloqueadas. Este sirve para enseñar en la pantalla qué líneas han cambiado
/// desde que alguien las contó, antes de confirmar.
/// </para>
/// </remarks>
public sealed class LineaDeRecuento : EntidadBase
{
    /// <summary>
    /// Lo que cabe en una cantidad: <c>numeric(18,6)</c> deja doce cifras enteras.
    /// </summary>
    public const decimal TopeDeLoContado = 1_000_000_000_000m;

    private LineaDeRecuento(
        Guid id,
        Guid recuentoId,
        int numero,
        ClaveDelRecuento clave,
        Guid unidadBaseId,
        OrigenDeLaLinea origen,
        decimal? costeUnitario,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        RecuentoId = recuentoId;
        Numero = numero;
        UbicacionId = clave.UbicacionId;
        ArticuloId = clave.ArticuloId;
        CodigoDeLote = clave.CodigoDeLote;
        NumeroDeSerie = clave.NumeroDeSerie;
        UnidadBaseId = unidadBaseId;
        Origen = origen;
        CosteUnitario = costeUnitario;
    }

    /// <summary>Constructor de materialización para EF Core.</summary>
    private LineaDeRecuento()
    {
    }

    /// <summary>Identificador de la línea.</summary>
    public Guid Id { get; private set; }

    /// <summary>El recuento del que cuelga.</summary>
    public Guid RecuentoId { get; private set; }

    /// <summary>Su posición en el recuento. La precarga los reparte por ubicación, artículo, lote y serie.</summary>
    public int Numero { get; private set; }

    /// <summary>El hueco del almacén.</summary>
    public Guid UbicacionId { get; private set; }

    /// <summary>El artículo.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>El código del lote, o <c>null</c>.</summary>
    public string? CodigoDeLote { get; private set; }

    /// <summary>El número de serie, o <c>null</c>. Nunca con lote.</summary>
    public string? NumeroDeSerie { get; private set; }

    /// <summary>La unidad en la que se cuenta: la base del artículo, que es la del ajuste (ADR-0055 §1.2).</summary>
    public Guid UnidadBaseId { get; private set; }

    /// <summary>Si la puso el alta o la añadió quien cuenta.</summary>
    public OrigenDeLaLinea Origen { get; private set; }

    /// <summary>
    /// El coste de una unidad base, en la divisa del recuento, o <c>null</c>. Solo en una línea
    /// añadida, y solo se usa si sube (ADR-0055 §6).
    /// </summary>
    public decimal? CosteUnitario { get; private set; }

    /// <summary>Lo que se contó, en unidad base, o <c>null</c> si todavía no se ha contado.</summary>
    public decimal? Contado { get; private set; }

    /// <summary>El teórico de la clave cuando se contó, o <c>null</c> si no se ha contado.</summary>
    public decimal? TeoricoAlContar { get; private set; }

    /// <summary>
    /// La línea del ajuste que movió su diferencia, o <c>null</c> si cuadraba o el recuento no se ha
    /// confirmado. Es la mitad de la doble flecha que va del recuento al ajuste (ADR-0055 §8).
    /// </summary>
    public Guid? LineaDeAjusteId { get; private set; }

    /// <summary>La clave que cuenta.</summary>
    public ClaveDelRecuento Clave => ClaveDelRecuento.De(UbicacionId, ArticuloId, CodigoDeLote, NumeroDeSerie);

    /// <summary>Si ya se contó, aunque fuera un cero.</summary>
    public bool EstaContada => Contado is not null;

    /// <summary>Si una cantidad se puede contar en una línea.</summary>
    /// <remarks>
    /// <b>Lo pregunta también el caso de uso</b>, antes de llamar al dominio, para contestar un
    /// <c>400</c> en vez del <c>500</c> que daría la excepción. Es la misma regla, escrita una vez.
    /// </remarks>
    /// <param name="contado">Lo contado, en unidad base.</param>
    /// <param name="esUnaSerie">Si la línea lleva número de serie.</param>
    /// <returns>Si cabe: no negativo, con seis decimales como mucho, bajo el tope, y cero o uno en una serie.</returns>
    public static bool SePuedeContar(decimal contado, bool esUnaSerie) =>
        contado >= 0m
        && contado < TopeDeLoContado
        && decimal.Round(contado, MovimientoStock.DecimalesDeCantidad) == contado
        && (!esUnaSerie || contado is 0m or 1m);

    internal static LineaDeRecuento Crear(
        Guid recuentoId,
        int numero,
        ClaveDelRecuento clave,
        Guid unidadBaseId,
        OrigenDeLaLinea origen,
        decimal? costeUnitario,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(clave);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numero);

        if (unidadBaseId == Guid.Empty)
        {
            throw new ArgumentException("Una línea se cuenta en la unidad base de su artículo.", nameof(unidadBaseId));
        }

        if (costeUnitario is { } coste && (origen != OrigenDeLaLinea.Anadida || coste < 0m))
        {
            throw new ArgumentException(
                "Solo una clave añadida lleva coste, y no negativo: la precargada entra al precio " +
                "medio de su clave (ADR-0055 §6).",
                nameof(costeUnitario));
        }

        return new LineaDeRecuento(
            Guid.CreateVersion7(),
            recuentoId,
            numero,
            clave,
            unidadBaseId,
            origen,
            costeUnitario is { } redondeable
                ? decimal.Round(redondeable, Importe.Decimales, MidpointRounding.AwayFromZero)
                : null,
            momento);
    }

    internal void Contar(decimal contado, decimal teorico)
    {
        if (!SePuedeContar(contado, NumeroDeSerie is not null))
        {
            throw new ArgumentOutOfRangeException(
                nameof(contado),
                contado,
                "Lo contado va en unidad base, no es negativo, lleva seis decimales como mucho y, en " +
                "un número de serie, es cero o uno (ADR-0055 §6).");
        }

        Contado = contado;
        TeoricoAlContar = teorico;
    }

    internal void AnotarLineaDeAjuste(Guid lineaDeAjusteId)
    {
        if (LineaDeAjusteId is not null)
        {
            throw new InvalidOperationException(
                "La línea del ajuste se anota una vez, al confirmar: su diferencia ya está en el libro.");
        }

        LineaDeAjusteId = lineaDeAjusteId;
    }
}
