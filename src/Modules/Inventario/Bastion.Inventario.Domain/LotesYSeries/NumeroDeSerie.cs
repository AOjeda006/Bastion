using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Inventario.Domain.LotesYSeries;

/// <summary>Un número de serie de un artículo: una unidad concreta, con nombre propio.</summary>
/// <remarks>
/// <para>
/// <b>Se crea como el lote, la primera vez que un documento lo nombra</b> (ADR-0048 §2), y por lo
/// mismo no hay forma de crearlo desde aquí.
/// </para>
/// <para>
/// <b>Lo que hace de él una unidad no está en esta fila</b>, sino en la existencia: como mucho una
/// unidad base por fila, y como mucho una fila con existencias por serie (ADR-0048 §3). Esta tabla
/// solo le da identidad, y la identidad de una serie no cambia cuando la unidad se vende.
/// </para>
/// </remarks>
public sealed class NumeroDeSerie : IDeInquilino
{
    /// <summary>Constructor de materialización: EF Core rellena la fila.</summary>
    private NumeroDeSerie()
    {
    }

    /// <summary>Identificador de la serie. Es lo que apuntan la existencia y el libro.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>El artículo de la serie. Vive en el esquema de Catálogo.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>El número de la etiqueta, en la forma de <see cref="CodigoGs1"/>.</summary>
    public string Numero { get; private set; } = string.Empty;
}
