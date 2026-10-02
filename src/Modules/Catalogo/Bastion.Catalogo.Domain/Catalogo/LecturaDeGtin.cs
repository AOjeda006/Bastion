namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Lo que salió de leer un texto como GTIN: el GTIN, o el motivo por el que no lo es.
/// </summary>
/// <remarks>
/// Es el caso especial que evita devolver un nulo como señal. Pedirle a una lectura lo que no tiene
/// —el GTIN a una rechazada, el motivo a una válida— lanza, porque es un programa que no miró
/// <see cref="EsGtin"/> antes.
/// </remarks>
public sealed class LecturaDeGtin
{
    private readonly Gtin? _gtin;
    private readonly MotivoDeRechazoDelGtin _motivo;

    private LecturaDeGtin(Gtin? gtin, MotivoDeRechazoDelGtin motivo) => (_gtin, _motivo) = (gtin, motivo);

    /// <summary>Si el texto era el GTIN de un artículo.</summary>
    public bool EsGtin => _gtin is not null;

    /// <summary>El GTIN leído.</summary>
    /// <exception cref="InvalidOperationException">La lectura se rechazó.</exception>
    public Gtin Gtin => _gtin ?? throw new InvalidOperationException(
        $"La lectura se rechazó por {_motivo}: no hay GTIN que dar.");

    /// <summary>Por qué el texto no es el GTIN de un artículo.</summary>
    /// <exception cref="InvalidOperationException">La lectura es válida.</exception>
    public MotivoDeRechazoDelGtin Motivo => _gtin is null
        ? _motivo
        : throw new InvalidOperationException("La lectura es un GTIN: no hay motivo de rechazo.");

    internal static LecturaDeGtin Valida(Gtin gtin) => new(gtin, default);

    internal static LecturaDeGtin Rechazada(MotivoDeRechazoDelGtin motivo) => new(null, motivo);
}
