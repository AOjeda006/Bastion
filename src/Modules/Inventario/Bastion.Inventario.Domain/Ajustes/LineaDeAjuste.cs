using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;

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
        int numero,
        Guid ubicacionId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        decimal? costeUnitario,
        string? codigoDeLote,
        string? numeroDeSerie,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        AjusteId = ajusteId;
        Numero = numero;
        UbicacionId = ubicacionId;
        ArticuloId = articuloId;
        CantidadIntroducida = cantidadIntroducida;
        UnidadIntroducidaId = unidadIntroducidaId;
        FactorAUnidadBase = factorAUnidadBase;
        CosteUnitario = costeUnitario;
        CodigoDeLote = codigoDeLote;
        NumeroDeSerie = numeroDeSerie;
    }

    /// <summary>Constructor de materialización para EF Core.</summary>
    private LineaDeAjuste()
    {
    }

    /// <summary>Identificador de la línea.</summary>
    public Guid Id { get; private set; }

    /// <summary>Ajuste al que pertenece.</summary>
    public Guid AjusteId { get; private set; }

    /// <summary>La posición de la línea en su documento, desde uno.</summary>
    /// <remarks>
    /// <b>Es el orden en que se valora</b> (ADR-0046 §3): dentro de un documento, la segunda línea
    /// del mismo artículo valora contra lo que dejó la primera. Sin guardarlo, el orden sería el
    /// que el motor devuelve al leer, y ese no es el que se escribió: los identificadores se crean
    /// en el mismo milisegundo, la versión 7 no ordena dentro de él, y EF Core no ordena las filas
    /// de una colección dentro de su documento.
    /// </remarks>
    public int Numero { get; private set; }

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

    /// <summary>El código del lote, tal como lo dice la etiqueta, o <see langword="null"/>.</summary>
    /// <remarks>
    /// <b>El borrador guarda el texto, y el libro, la fila</b> (ADR-0048 §2). El lote se crea la
    /// primera vez que se confirma un documento que lo nombra, así que mientras la línea es borrador
    /// puede no existir todavía, y un borrador tirado no deja lotes huérfanos.
    /// </remarks>
    public string? CodigoDeLote { get; private set; }

    /// <summary>El número de serie, tal como lo dice la etiqueta, o <see langword="null"/>.</summary>
    /// <remarks>Como el lote: texto en el borrador y fila en el libro.</remarks>
    public string? NumeroDeSerie { get; private set; }

    /// <summary>
    /// Lo que la línea sumó a la valoración al confirmarse, o lo que le restó, en la divisa del
    /// documento: el valor de su fila del libro. <see langword="null"/> mientras es borrador.
    /// </summary>
    /// <remarks>
    /// <b>Se guarda en la línea y no se busca en el libro</b>, porque la fila del libro no sabe de
    /// qué línea sale, y emparejarlas por orden no es fiable: sus identificadores se crean en el
    /// mismo milisegundo (ADR-0046 §6). Lo necesita el inverso, que compensa este importe exacto y
    /// no el coste por la cantidad.
    /// </remarks>
    public decimal? Valor { get; private set; }

    /// <summary>
    /// En la línea de un inverso, el valor de la línea original con el signo cambiado: lo que esta
    /// línea compensa. <see langword="null"/> en cualquier otra.
    /// </summary>
    /// <remarks>
    /// Al confirmar, el inverso se valora contra esto y no contra un coste. Si entre el original y la
    /// anulación otras salidas se llevaron parte de ese valor, <see cref="Valor"/> se queda con lo
    /// que quedaba, y las dos cifras dicen la diferencia (ADR-0046 §6).
    /// </remarks>
    public decimal? ValorQueCompensa { get; private set; }

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
    /// <para>
    /// <b>El lote y la serie, lo mismo</b>: el borde los rechaza antes con su <c>type</c>, y aquí se
    /// vuelven a mirar para que no exista una línea con un código que la etiqueta no puede llevar,
    /// ni con los dos a la vez, ni con una serie que mueva otra cosa que una unidad base (ADR-0048
    /// §1, §2 y §3).
    /// </para>
    /// </remarks>
    /// <param name="ajusteId">Ajuste al que pertenece.</param>
    /// <param name="numero">Su posición en el documento, desde uno.</param>
    /// <param name="ubicacionId">Hueco del almacén.</param>
    /// <param name="articuloId">Artículo que se mueve.</param>
    /// <param name="cantidadIntroducida">Cantidad con signo, tal como se escribió.</param>
    /// <param name="unidadIntroducidaId">Unidad en la que se escribió.</param>
    /// <param name="factorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
    /// <param name="costeUnitario">Coste de una unidad base, o <c>null</c>. Solo si sube.</param>
    /// <param name="codigoDeLote">El código del lote, o <c>null</c>.</param>
    /// <param name="numeroDeSerie">El número de serie, o <c>null</c>. Nunca con lote.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La línea.</returns>
    public static LineaDeAjuste Crear(
        Guid ajusteId,
        int numero,
        Guid ubicacionId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        decimal? costeUnitario,
        string? codigoDeLote,
        string? numeroDeSerie,
        DateTimeOffset momento)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numero);

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

        string? lote = LeerCodigo(codigoDeLote, nameof(codigoDeLote));
        string? serie = LeerCodigo(numeroDeSerie, nameof(numeroDeSerie));

        if (lote is not null && serie is not null)
        {
            throw new ArgumentException(
                "Una línea lleva lote o número de serie, no los dos: el artículo tiene una sola " +
                "marca, y el esquema guarda las dos columnas solo para no mezclarlas (ADR-0048 §1).",
                nameof(numeroDeSerie));
        }

        if (serie is not null
            && Math.Abs(MovimientoStock.EnUnidadBase(cantidadIntroducida, factorAUnidadBase)) != 1m)
        {
            throw new ArgumentException(
                "Una línea con número de serie mueve una unidad base, arriba o abajo: la serie es " +
                "una unidad, y dos unidades con el mismo número no existen (ADR-0048 §3).",
                nameof(numeroDeSerie));
        }

        return new LineaDeAjuste(
            Guid.CreateVersion7(),
            ajusteId,
            numero,
            ubicacionId,
            articuloId,
            cantidadIntroducida,
            unidadIntroducidaId,
            factorAUnidadBase,
            costeUnitario is { } redondeable
                ? decimal.Round(redondeable, Importe.Decimales, MidpointRounding.AwayFromZero)
                : null,
            lote,
            serie,
            momento);
    }

    /// <summary>
    /// La línea del inverso que compensa a esta: la cantidad con el signo cambiado, sin coste y con
    /// el valor de esta, también con el signo cambiado (ADR-0046 §6). Y con su mismo número, así
    /// que el inverso se escribe en el orden del original.
    /// </summary>
    /// <param name="ajusteId">El inverso.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La línea del inverso.</returns>
    /// <exception cref="InvalidOperationException">Esta línea no tiene valor: no se ha confirmado.</exception>
    internal LineaDeAjuste Compensada(Guid ajusteId, DateTimeOffset momento)
    {
        if (Valor is not { } valor)
        {
            throw new InvalidOperationException(
                "Una línea sin valor no se compensa: su documento no llegó a confirmarse (ADR-0046 §6).");
        }

        LineaDeAjuste inversa = Crear(
            ajusteId,
            Numero,
            UbicacionId,
            ArticuloId,
            -CantidadIntroducida,
            UnidadIntroducidaId,
            FactorAUnidadBase,
            costeUnitario: null,
            CodigoDeLote,
            NumeroDeSerie,
            momento);

        inversa.ValorQueCompensa = -valor;

        return inversa;
    }

    /// <summary>El código normalizado, o <see langword="null"/> si no se escribió.</summary>
    /// <param name="codigo">Lo que se escribió.</param>
    /// <param name="parametro">De qué parámetro viene, para el mensaje.</param>
    /// <returns>El código en la forma de GS1, o <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">Se escribió algo que no es un código GS1.</exception>
    private static string? LeerCodigo(string? codigo, string parametro) =>
        codigo is null
            ? null
            : CodigoGs1.Normalizar(codigo)
                ?? throw new ArgumentException(
                    $"«{codigo}» no es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres " +
                    "del conjunto 82 (ADR-0048 §2).",
                    parametro);

    /// <summary>Anota el valor con el que la línea entró en el libro, al confirmar.</summary>
    /// <param name="valor">El valor de su fila del libro, en la divisa del documento.</param>
    /// <exception cref="InvalidOperationException">La línea ya tenía valor.</exception>
    internal void AnotarValor(decimal valor)
    {
        if (Valor is not null)
        {
            throw new InvalidOperationException(
                "El valor de una línea se anota una vez, al confirmar: su fila del libro ya está " +
                "escrita, y el libro no se reescribe (R2).");
        }

        Valor = valor;
    }
}
