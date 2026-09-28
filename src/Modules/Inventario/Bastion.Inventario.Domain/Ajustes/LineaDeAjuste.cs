using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.Inventario.Domain.Ajustes;

/// <summary>
/// Lo que un ajuste dice de <b>un</b> artículo en <b>una</b> ubicación: cuánto entra o sale, en
/// qué unidad se escribió y a qué coste unitario.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es hija del ajuste, y el movimiento no lo es.</b> La línea se edita mientras el documento
/// está en borrador y se guarda con él en la misma transacción, que es lo que hace una colección
/// de agregado. La fila del libro que sale de ella al confirmar vive fuera, y el motivo entero
/// está en <c>Ajuste.Confirmar</c>.
/// </para>
/// <para>
/// <b>La cantidad lleva signo</b> y no hay un campo «tipo de movimiento»: un ajuste que sube
/// existencias es positivo y uno que las baja es negativo. Un enumerado <c>Entrada|Salida</c> al
/// lado de una cantidad siempre positiva sería un segundo sitio donde guardar el signo, y dos
/// sitios se contradicen.
/// </para>
/// </remarks>
public sealed class LineaDeAjuste : EntidadBase
{
    private LineaDeAjuste(
        Guid id,
        Guid ajusteId,
        Guid ubicacionId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        decimal? costeUnitario,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        AjusteId = ajusteId;
        UbicacionId = ubicacionId;
        ArticuloId = articuloId;
        CantidadIntroducida = cantidadIntroducida;
        UnidadIntroducidaId = unidadIntroducidaId;
        FactorAUnidadBase = factorAUnidadBase;
        CosteUnitario = costeUnitario;
    }

    /// <summary>Constructor de materialización para EF Core.</summary>
    private LineaDeAjuste()
    {
    }

    /// <summary>Identificador de la línea.</summary>
    public Guid Id { get; private set; }

    /// <summary>Ajuste al que pertenece.</summary>
    public Guid AjusteId { get; private set; }

    /// <summary>Hueco concreto del almacén al que afecta.</summary>
    public Guid UbicacionId { get; private set; }

    /// <summary>Artículo que se mueve.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>Cantidad tal como se escribió, con signo.</summary>
    public decimal CantidadIntroducida { get; private set; }

    /// <summary>Unidad en la que se escribió la cantidad.</summary>
    public Guid UnidadIntroducidaId { get; private set; }

    /// <summary>Cuántas unidades base hay en una de las introducidas.</summary>
    public decimal FactorAUnidadBase { get; private set; }

    /// <summary>Coste de una unidad base, en la divisa del ajuste, o <c>null</c>.</summary>
    /// <remarks>
    /// <b>Solo lo lleva una línea que sube, y puede faltar.</b> Una salida se valora al precio
    /// medio, y un coste escrito ahí no se usaría (ADR-0046 §7). Es un decimal y no un
    /// <c>Importe</c> porque la divisa es la de la cabecera: con una por línea, el documento
    /// podría mezclarlas.
    /// </remarks>
    public decimal? CosteUnitario { get; private set; }

    /// <summary>Crea una línea de ajuste ya validada.</summary>
    /// <remarks>
    /// <para>
    /// La cantidad y el factor se comprueban igual que en el libro, y eso es a propósito: una línea
    /// que el documento acepta y el libro rechaza dejaría un ajuste imposible de confirmar, con el
    /// fallo apareciendo en la transición y no donde se escribió el dato.
    /// </para>
    /// <para>
    /// <b>El coste se comprueba aquí y no en el libro.</b> Las salidas confirmadas antes del 2.8
    /// llevan coste, y sus filas no se tocan. El borde rechaza lo mismo antes, con
    /// <c>ajuste-coste-no-valido</c>, así que aquí llega como defecto de quien llama.
    /// </para>
    /// </remarks>
    /// <param name="ajusteId">Ajuste al que pertenece.</param>
    /// <param name="ubicacionId">Hueco del almacén.</param>
    /// <param name="articuloId">Artículo que se mueve.</param>
    /// <param name="cantidadIntroducida">Cantidad con signo, tal como se escribió.</param>
    /// <param name="unidadIntroducidaId">Unidad en la que se escribió.</param>
    /// <param name="factorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
    /// <param name="costeUnitario">Coste de una unidad base, o <c>null</c>. Solo si sube.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La línea.</returns>
    public static LineaDeAjuste Crear(
        Guid ajusteId,
        Guid ubicacionId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        decimal? costeUnitario,
        DateTimeOffset momento)
    {
        if (cantidadIntroducida == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidadIntroducida),
                "Una línea de ajuste de cero no ajusta nada: deja constancia de un movimiento que " +
                "no ocurrió y la suma no la distingue de no haberla escrito.");
        }

        if (factorAUnidadBase <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(factorAUnidadBase),
                "El factor a unidad base tiene que ser mayor que cero: con cero o negativo, la " +
                "cantidad en unidad base dejaría de ser la introducida.");
        }

        if (costeUnitario is { } coste && (cantidadIntroducida < 0m || coste < 0m))
        {
            throw new ArgumentException(
                "Solo una línea que sube lleva coste, y no negativo: la que baja se valora al " +
                "precio medio (ADR-0046 §7).",
                nameof(costeUnitario));
        }

        return new LineaDeAjuste(
            Guid.CreateVersion7(),
            ajusteId,
            ubicacionId,
            articuloId,
            cantidadIntroducida,
            unidadIntroducidaId,
            factorAUnidadBase,
            costeUnitario is { } redondeable
                ? decimal.Round(redondeable, Importe.Decimales, MidpointRounding.AwayFromZero)
                : null,
            momento);
    }
}
