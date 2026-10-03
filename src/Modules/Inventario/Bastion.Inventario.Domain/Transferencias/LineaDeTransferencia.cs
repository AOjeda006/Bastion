using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Domain.Transferencias;

/// <summary>
/// Lo que una transferencia dice de <b>un</b> artículo: cuánto va del origen al destino, de qué
/// ubicación a qué ubicación y con qué lote o serie.
/// </summary>
/// <remarks>
/// <para>
/// <b>Una línea escribe dos filas del libro</b>, una por pata: la salida del origen al enviar y la
/// entrada en el destino al recibir. Las dos llevan la misma cantidad y el mismo valor, con el signo
/// cambiado, y la línea guarda los dos datos que las unen: la cantidad y el valor que viaja.
/// </para>
/// <para>
/// <b>La cantidad lleva signo, como en el ajuste</b>: positiva en una transferencia, negativa en su
/// inverso. Así una sola regla da las dos patas: el origen mueve la cantidad negada, y el destino la
/// cantidad tal cual.
/// </para>
/// </remarks>
public sealed class LineaDeTransferencia : EntidadBase
{
    private LineaDeTransferencia(
        Guid id,
        Guid transferenciaId,
        int numero,
        Guid ubicacionOrigenId,
        Guid ubicacionDestinoId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        string? codigoDeLote,
        string? numeroDeSerie,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        TransferenciaId = transferenciaId;
        Numero = numero;
        UbicacionOrigenId = ubicacionOrigenId;
        UbicacionDestinoId = ubicacionDestinoId;
        ArticuloId = articuloId;
        CantidadIntroducida = cantidadIntroducida;
        UnidadIntroducidaId = unidadIntroducidaId;
        FactorAUnidadBase = factorAUnidadBase;
        CodigoDeLote = codigoDeLote;
        NumeroDeSerie = numeroDeSerie;
    }

    /// <summary>Constructor de materialización para EF Core.</summary>
    private LineaDeTransferencia()
    {
    }

    /// <summary>Identificador de la línea.</summary>
    public Guid Id { get; private set; }

    /// <summary>Transferencia a la que pertenece.</summary>
    public Guid TransferenciaId { get; private set; }

    /// <summary>La posición de la línea en su documento, desde uno: el orden en que se valora.</summary>
    public int Numero { get; private set; }

    /// <summary>De qué hueco del almacén de origen sale.</summary>
    public Guid UbicacionOrigenId { get; private set; }

    /// <summary>A qué hueco del almacén de destino llega.</summary>
    /// <remarks>
    /// <b>Va desde el alta</b> (ADR-0053 §1): el tránsito vive en la existencia del destino, y esa
    /// fila necesita su ubicación desde el envío. La recepción, que es entera, no pregunta nada.
    /// </remarks>
    public Guid UbicacionDestinoId { get; private set; }

    /// <summary>Artículo que se mueve.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>Cantidad tal como se escribió, con signo: positiva, salvo en un inverso.</summary>
    public decimal CantidadIntroducida { get; private set; }

    /// <summary>Unidad en la que se escribió la cantidad.</summary>
    public Guid UnidadIntroducidaId { get; private set; }

    /// <summary>Cuántas unidades base hay en una de las introducidas.</summary>
    public decimal FactorAUnidadBase { get; private set; }

    /// <summary>El código del lote, tal como lo dice la etiqueta, o <see langword="null"/>.</summary>
    public string? CodigoDeLote { get; private set; }

    /// <summary>El número de serie, tal como lo dice la etiqueta, o <see langword="null"/>.</summary>
    public string? NumeroDeSerie { get; private set; }

    /// <summary>
    /// El valor que viajó del origen al destino, con el signo de la cantidad, en la divisa del
    /// documento. <see langword="null"/> mientras es borrador.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>En una transferencia es lo que salió del origen</b>: lo valora su precio medio al enviar, y
    /// la recepción lo mete entero en el destino (ADR-0053 §2).
    /// </para>
    /// <para>
    /// <b>En un inverso es lo que volvió</b>: de una enviada, el valor exacto que estaba en tránsito,
    /// y de una recibida, lo que la salida del destino se llevó, que el tope puede dejar por debajo
    /// de <see cref="ValorQueCompensa"/> (§6).
    /// </para>
    /// </remarks>
    public decimal? Valor { get; private set; }

    /// <summary>
    /// En la línea de un inverso, el valor de la línea original con el signo cambiado: lo que esta
    /// línea compensa. <see langword="null"/> en cualquier otra.
    /// </summary>
    public decimal? ValorQueCompensa { get; private set; }

    /// <summary>La cantidad en unidad base, con signo, redondeada como la del libro.</summary>
    public decimal CantidadEnUnidadBase =>
        MovimientoStock.EnUnidadBase(CantidadIntroducida, FactorAUnidadBase);

    /// <summary>Crea una línea de transferencia ya validada.</summary>
    /// <remarks>
    /// Comprueba lo mismo que la del ajuste: la cantidad y el factor como el libro, el lote y la
    /// serie como la etiqueta, los dos nunca juntos, y una serie que mueve una sola unidad base. El
    /// signo de la cantidad lo mira el documento, que sabe si es un inverso.
    /// </remarks>
    /// <param name="transferenciaId">Transferencia a la que pertenece.</param>
    /// <param name="numero">Su posición en el documento, desde uno.</param>
    /// <param name="ubicacionOrigenId">Hueco del almacén de origen.</param>
    /// <param name="ubicacionDestinoId">Hueco del almacén de destino.</param>
    /// <param name="articuloId">Artículo que se mueve.</param>
    /// <param name="cantidadIntroducida">Cantidad con signo, tal como se escribió.</param>
    /// <param name="unidadIntroducidaId">Unidad en la que se escribió.</param>
    /// <param name="factorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
    /// <param name="codigoDeLote">El código del lote, o <c>null</c>.</param>
    /// <param name="numeroDeSerie">El número de serie, o <c>null</c>. Nunca con lote.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La línea.</returns>
    public static LineaDeTransferencia Crear(
        Guid transferenciaId,
        int numero,
        Guid ubicacionOrigenId,
        Guid ubicacionDestinoId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        string? codigoDeLote,
        string? numeroDeSerie,
        DateTimeOffset momento)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numero);

        if (cantidadIntroducida == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidadIntroducida),
                "Una línea de transferencia de cero no lleva nada de un almacén a otro.");
        }

        if (factorAUnidadBase <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(factorAUnidadBase),
                "El factor a unidad base tiene que ser mayor que cero: con cero o negativo, la " +
                "cantidad en unidad base dejaría de ser la introducida.");
        }

        if (MovimientoStock.EnUnidadBase(cantidadIntroducida, factorAUnidadBase) == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidadIntroducida),
                "La cantidad en unidad base redondea a cero: las dos filas del libro no moverían nada.");
        }

        string? lote = LeerCodigo(codigoDeLote, nameof(codigoDeLote));
        string? serie = LeerCodigo(numeroDeSerie, nameof(numeroDeSerie));

        if (lote is not null && serie is not null)
        {
            throw new ArgumentException(
                "Una línea lleva lote o número de serie, no los dos (ADR-0048 §1).",
                nameof(numeroDeSerie));
        }

        if (serie is not null
            && Math.Abs(MovimientoStock.EnUnidadBase(cantidadIntroducida, factorAUnidadBase)) != 1m)
        {
            throw new ArgumentException(
                "Una línea con número de serie mueve una unidad base: la serie es una unidad " +
                "(ADR-0048 §3).",
                nameof(numeroDeSerie));
        }

        return new LineaDeTransferencia(
            Guid.CreateVersion7(),
            transferenciaId,
            numero,
            ubicacionOrigenId,
            ubicacionDestinoId,
            articuloId,
            cantidadIntroducida,
            unidadIntroducidaId,
            factorAUnidadBase,
            lote,
            serie,
            momento);
    }

    /// <summary>
    /// La línea del inverso que compensa a esta: la cantidad con el signo cambiado, las mismas
    /// ubicaciones, el mismo lote o la misma serie, y el valor de esta, negado, como valor que
    /// compensa (ADR-0053 §5). Con su mismo número, así que el inverso se escribe en el orden del
    /// original.
    /// </summary>
    /// <param name="transferenciaId">El inverso.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La línea del inverso.</returns>
    /// <exception cref="InvalidOperationException">Esta línea no tiene valor: no se ha enviado.</exception>
    internal LineaDeTransferencia Compensada(Guid transferenciaId, DateTimeOffset momento)
    {
        if (Valor is not { } valor)
        {
            throw new InvalidOperationException(
                "Una línea sin valor no se compensa: su documento no llegó a enviarse (ADR-0053 §5).");
        }

        LineaDeTransferencia inversa = Crear(
            transferenciaId,
            Numero,
            UbicacionOrigenId,
            UbicacionDestinoId,
            ArticuloId,
            -CantidadIntroducida,
            UnidadIntroducidaId,
            FactorAUnidadBase,
            CodigoDeLote,
            NumeroDeSerie,
            momento);

        inversa.ValorQueCompensa = -valor;

        return inversa;
    }

    /// <summary>Anota el valor que viajó, al enviar o al compensar.</summary>
    /// <param name="valor">El valor, con el signo de la cantidad, en la divisa del documento.</param>
    /// <exception cref="InvalidOperationException">La línea ya tenía valor.</exception>
    /// <exception cref="ArgumentException">El valor lleva el signo contrario al de la cantidad.</exception>
    internal void AnotarValor(decimal valor)
    {
        if (Valor is not null)
        {
            throw new InvalidOperationException(
                "El valor de una línea se anota una vez: su fila del libro ya está escrita, y el libro " +
                "no se reescribe (R2).");
        }

        // CERO VALE CON CUALQUIER SIGNO: el origen pudo tener la clave sin valor (ADR-0046 §8).
        if (valor * CantidadIntroducida < 0m)
        {
            throw new ArgumentException(
                "El valor que viaja va con el signo de la cantidad: del origen al destino, positivo.",
                nameof(valor));
        }

        Valor = valor;
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
}
