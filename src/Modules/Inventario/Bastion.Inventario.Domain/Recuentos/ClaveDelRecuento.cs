using Bastion.Inventario.Domain.LotesYSeries;

namespace Bastion.Inventario.Domain.Recuentos;

/// <summary>
/// Lo que cuenta una línea: una ubicación, un artículo, y su lote o su número de serie. El almacén
/// es el del recuento.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la clave de la existencia sin el almacén</b>, y por eso dos líneas del mismo recuento no
/// pueden llevar la misma: contarían dos veces lo mismo, y el ajuste la movería dos veces.
/// </para>
/// <para>
/// <b>Los códigos entran normalizados, y solo por <see cref="De"/></b>: el constructor es privado y
/// las propiedades no tienen <c>init</c>, así que ni un <c>with</c> puede saltarse la
/// normalización. La igualdad del registro compara los códigos byte a byte, y dos claves que solo
/// difieren en un espacio al final son la misma.
/// </para>
/// </remarks>
public sealed record ClaveDelRecuento
{
    private ClaveDelRecuento(Guid ubicacionId, Guid articuloId, string? codigoDeLote, string? numeroDeSerie)
    {
        UbicacionId = ubicacionId;
        ArticuloId = articuloId;
        CodigoDeLote = codigoDeLote;
        NumeroDeSerie = numeroDeSerie;
    }

    /// <summary>El hueco del almacén.</summary>
    public Guid UbicacionId { get; }

    /// <summary>El artículo.</summary>
    public Guid ArticuloId { get; }

    /// <summary>El código del lote, normalizado, o <c>null</c>.</summary>
    public string? CodigoDeLote { get; }

    /// <summary>El número de serie, normalizado, o <c>null</c>. Nunca con lote.</summary>
    public string? NumeroDeSerie { get; }

    /// <summary>Construye la clave, con los códigos normalizados como los de GS1.</summary>
    /// <param name="ubicacionId">El hueco del almacén.</param>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="codigoDeLote">El código del lote, o <c>null</c>.</param>
    /// <param name="numeroDeSerie">El número de serie, o <c>null</c>.</param>
    /// <returns>La clave.</returns>
    public static ClaveDelRecuento De(
        Guid ubicacionId,
        Guid articuloId,
        string? codigoDeLote = null,
        string? numeroDeSerie = null)
    {
        if (ubicacionId == Guid.Empty)
        {
            throw new ArgumentException("Una línea cuenta un hueco del almacén.", nameof(ubicacionId));
        }

        if (articuloId == Guid.Empty)
        {
            throw new ArgumentException("Una línea cuenta un artículo.", nameof(articuloId));
        }

        string? lote = LeerCodigo(codigoDeLote, nameof(codigoDeLote));
        string? serie = LeerCodigo(numeroDeSerie, nameof(numeroDeSerie));

        if (lote is not null && serie is not null)
        {
            throw new ArgumentException(
                "Una línea lleva lote o número de serie, no los dos: el artículo tiene una sola " +
                "marca (ADR-0048 §1).",
                nameof(numeroDeSerie));
        }

        return new ClaveDelRecuento(ubicacionId, articuloId, lote, serie);
    }

    private static string? LeerCodigo(string? codigo, string parametro) =>
        codigo is null
            ? null
            : CodigoGs1.Normalizar(codigo)
                ?? throw new ArgumentException(
                    $"«{codigo}» no es un código GS1: de 1 a {CodigoGs1.LargoMaximo} caracteres " +
                    "del conjunto 82 (ADR-0048 §2).",
                    parametro);
}
