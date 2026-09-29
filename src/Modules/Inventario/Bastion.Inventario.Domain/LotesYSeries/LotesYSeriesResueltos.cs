namespace Bastion.Inventario.Domain.LotesYSeries;

/// <summary>
/// La fila de cada lote y de cada serie que nombra un documento, ya creada o encontrada en la base
/// (ADR-0048 §2).
/// </summary>
/// <remarks>
/// <b>Entra en la confirmación como el número y la valoración</b>: la resuelve el caso de uso,
/// dentro de la transacción que ya tiene abierta, porque el dominio no habla con la base. Lo que sí
/// se sostiene en el dominio es que no hay forma de confirmar sin ella: cada código que el documento
/// nombra tiene que venir resuelto, o la confirmación no transita.
/// </remarks>
public sealed record LotesYSeriesResueltos
{
    /// <summary>Crea la resolución.</summary>
    /// <param name="lotes">La fila de cada lote, por artículo y código.</param>
    /// <param name="series">La fila de cada número de serie, por artículo y número.</param>
    public LotesYSeriesResueltos(
        IReadOnlyDictionary<CodigoDeUnArticulo, Guid> lotes,
        IReadOnlyDictionary<CodigoDeUnArticulo, Guid> series)
    {
        ArgumentNullException.ThrowIfNull(lotes);
        ArgumentNullException.ThrowIfNull(series);

        Lotes = lotes;
        Series = series;
    }

    /// <summary>La de un documento que no nombra lotes ni series.</summary>
    public static LotesYSeriesResueltos Ninguno { get; } = new(
        new Dictionary<CodigoDeUnArticulo, Guid>(),
        new Dictionary<CodigoDeUnArticulo, Guid>());

    /// <summary>La fila de cada lote, por artículo y código.</summary>
    public IReadOnlyDictionary<CodigoDeUnArticulo, Guid> Lotes { get; }

    /// <summary>La fila de cada número de serie, por artículo y número.</summary>
    public IReadOnlyDictionary<CodigoDeUnArticulo, Guid> Series { get; }
}
