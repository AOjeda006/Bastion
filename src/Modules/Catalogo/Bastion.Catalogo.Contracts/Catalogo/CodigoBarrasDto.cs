using System.ComponentModel.DataAnnotations;

namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>Un código de barras de un artículo, tal como sale de la API.</summary>
/// <remarks>
/// <b>El GTIN sale en su forma de catorce</b>, con los ceros de delante, que es como se guarda y
/// como lo compara el índice. Un GTIN-13 dado de alta como <c>4006381333931</c> sale como
/// <c>04006381333931</c>: es el mismo número, y la pantalla decide cómo enseñarlo.
/// </remarks>
/// <param name="Id">Identificador de la fila.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="ArticuloId">Artículo que lleva el código.</param>
/// <param name="Gtin">El GTIN, en catorce cifras.</param>
/// <param name="Nivel">Dónde va impreso, como texto: <c>Base</c>, <c>Caja</c> o <c>Palet</c>.</param>
/// <param name="Unidades">Unidades base que lleva: una en la base, dos o más en una agrupación.</param>
public sealed record CodigoBarrasDto(
    Guid Id,
    Guid EmpresaId,
    Guid ArticuloId,
    string Gtin,
    string Nivel,
    int Unidades);

/// <summary>Lo que hace falta para dar de alta un código de barras en un artículo.</summary>
/// <remarks>
/// <para>
/// <b>No lleva empresa ni artículo</b>: la empresa sale del claim (R8), y el artículo va en la ruta.
/// </para>
/// <para>
/// <b>El GTIN no lleva <c>[Required]</c>, y es a propósito.</b> Con él, un GTIN que falta daría el
/// <c>400</c> genérico de los datos no válidos. Sin él, lo lee el caso de uso como cualquier otro, y
/// cada GTIN que no sirve recibe el <c>type</c> de su motivo: también el que no llegó, que no tiene
/// un largo admitido.
/// </para>
/// </remarks>
public sealed record AgregarCodigoBarrasDto
{
    /// <summary>El GTIN, de 8, 12, 13 o 14 cifras. Los espacios de los extremos no cuentan.</summary>
    public string? Gtin { get; init; }

    /// <summary>Dónde va impreso: <c>Base</c>, <c>Caja</c> o <c>Palet</c>.</summary>
    /// <remarks>
    /// Como texto y no como número, por lo mismo que el tipo del artículo: el ordinal ataría el
    /// contrato al orden del enumerado.
    /// </remarks>
    [Required(ErrorMessage = "El nivel es obligatorio.")]
    public string Nivel { get; init; } = string.Empty;

    /// <summary>
    /// Cuántas unidades base lleva. En la base es una, y si no se manda, es esa. En una caja o un
    /// palé, dos o más, y hay que mandarlas.
    /// </summary>
    public int? Unidades { get; init; }
}
