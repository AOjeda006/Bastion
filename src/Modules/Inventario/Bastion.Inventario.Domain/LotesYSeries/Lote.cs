using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Inventario.Domain.LotesYSeries;

/// <summary>Un lote de un artículo: lo que el proveedor fabricó junto y etiquetó igual.</summary>
/// <remarks>
/// <para>
/// <b>Se crea la primera vez que un documento lo nombra, y no hay alta aparte</b> (ADR-0048 §2). El
/// lote lo trae la mercancía, no se decide en una pantalla: exigir darlo de alta antes de recibirlo
/// sería un paso que no añade nada y que el almacén se saltaría escribiendo lo mismo dos veces.
/// </para>
/// <para>
/// <b>No hay forma de crearlo desde aquí</b>, como en <c>Existencia</c>: lo crea la sentencia que
/// resuelve los códigos con <c>INSERT … ON CONFLICT DO NOTHING</c>, que es lo único que dos
/// confirmaciones simultáneas del mismo lote nuevo no pueden duplicar. Leerlo y crearlo desde la
/// aplicación dejaría una carrera entre la lectura y el alta.
/// </para>
/// <para>
/// <b>No es una entidad del tipo base ni se audita</b>: no cambia nunca. Lo que se sabe del lote
/// —cuándo entró, de quién— está en el libro, que lo apunta.
/// </para>
/// </remarks>
public sealed class Lote : IDeInquilino
{
    /// <summary>Constructor de materialización: EF Core rellena la fila.</summary>
    private Lote()
    {
    }

    /// <summary>Identificador del lote. Es lo que apuntan la existencia y el libro.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>El artículo del lote. Vive en el esquema de Catálogo.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>El código de la etiqueta, en la forma de <see cref="CodigoGs1"/>.</summary>
    public string Codigo { get; private set; } = string.Empty;
}
