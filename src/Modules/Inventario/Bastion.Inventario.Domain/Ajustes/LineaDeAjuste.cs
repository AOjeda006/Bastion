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
        Importe costeUnitario,
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

    /// <summary>Coste de una unidad base, con su divisa (R6).</summary>
    public Importe CosteUnitario { get; private set; } = null!;

    /// <summary>Crea una línea de ajuste ya validada.</summary>
    /// <remarks>
    /// Las tres comprobaciones son las mismas que las del libro, y eso es a propósito: una línea
    /// que el documento acepta y el libro rechaza dejaría un ajuste imposible de confirmar, con el
    /// fallo apareciendo en la transición y no donde se escribió el dato.
    /// </remarks>
    /// <param name="ajusteId">Ajuste al que pertenece.</param>
    /// <param name="ubicacionId">Hueco del almacén.</param>
    /// <param name="articuloId">Artículo que se mueve.</param>
    /// <param name="cantidadIntroducida">Cantidad con signo, tal como se escribió.</param>
    /// <param name="unidadIntroducidaId">Unidad en la que se escribió.</param>
    /// <param name="factorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
    /// <param name="costeUnitario">Coste de una unidad base.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La línea.</returns>
    public static LineaDeAjuste Crear(
        Guid ajusteId,
        Guid ubicacionId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        Importe costeUnitario,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(costeUnitario);

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

        return new LineaDeAjuste(
            Guid.CreateVersion7(),
            ajusteId,
            ubicacionId,
            articuloId,
            cantidadIntroducida,
            unidadIntroducidaId,
            factorAUnidadBase,
            costeUnitario,
            momento);
    }
}
