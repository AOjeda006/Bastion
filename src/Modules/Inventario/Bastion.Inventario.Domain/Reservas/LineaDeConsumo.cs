using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Domain.Reservas;

/// <summary>
/// Lo que saca un albarán de <b>un</b> hueco al consumir una reserva: cuánto, en la unidad base de la
/// reserva, y de qué lote o serie (ADR-0059 §6).
/// </summary>
/// <remarks>
/// <b>No es una entidad</b>: no se guarda. Cada línea se convierte en una fila del libro, y el
/// consumo guarda solo la suma. El artículo y el almacén no van aquí porque son los de la reserva.
/// </remarks>
public sealed record LineaDeConsumo
{
    /// <summary>Construye la línea y comprueba su forma.</summary>
    /// <param name="ubicacionId">El hueco del que sale.</param>
    /// <param name="cantidad">Cuánto sale, en la unidad base de la reserva. Positiva.</param>
    /// <param name="codigoDeLote">El código del lote, o <c>null</c>.</param>
    /// <param name="numeroDeSerie">El número de serie, o <c>null</c>. Nunca con lote.</param>
    public LineaDeConsumo(Guid ubicacionId, decimal cantidad, string? codigoDeLote, string? numeroDeSerie)
    {
        if (ubicacionId == Guid.Empty)
        {
            throw new ArgumentException("Una línea de consumo saca de un hueco.", nameof(ubicacionId));
        }

        // SEIS DECIMALES, LOS DE LA FILA DEL LIBRO: con siete, la fila redondearía al registrarse y
        // el consumo guardaría una cantidad que no es la suma de sus filas.
        if (!Reserva.EsUnaCantidadValida(cantidad))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidad),
                cantidad,
                $"Una línea de consumo saca una cantidad positiva, con {MovimientoStock.DecimalesDeCantidad} " +
                "decimales como mucho: lo que baja el libro es la fila que escribe.");
        }

        string? lote = LeerCodigo(codigoDeLote, nameof(codigoDeLote));
        string? serie = LeerCodigo(numeroDeSerie, nameof(numeroDeSerie));

        if (lote is not null && serie is not null)
        {
            throw new ArgumentException(
                "Una línea lleva lote o número de serie, no los dos (ADR-0048 §1).", nameof(numeroDeSerie));
        }

        if (serie is not null && cantidad != 1m)
        {
            throw new ArgumentException(
                "Una línea con número de serie saca una unidad base (ADR-0048 §3).", nameof(numeroDeSerie));
        }

        UbicacionId = ubicacionId;
        Cantidad = cantidad;
        CodigoDeLote = lote;
        NumeroDeSerie = serie;
    }

    /// <summary>El hueco del que sale.</summary>
    public Guid UbicacionId { get; }

    /// <summary>Cuánto sale, en la unidad base de la reserva. Positiva: el signo lo pone la fila.</summary>
    public decimal Cantidad { get; }

    /// <summary>El código del lote, recortado, o <c>null</c>.</summary>
    public string? CodigoDeLote { get; }

    /// <summary>El número de serie, recortado, o <c>null</c>.</summary>
    public string? NumeroDeSerie { get; }

    private static string? LeerCodigo(string? codigo, string parametro) =>
        codigo is null ? null : CodigoGs1.Normalizar(codigo)
            ?? throw new ArgumentException($"«{codigo}» no es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres del conjunto 82 (ADR-0048 §2).", parametro);
}
