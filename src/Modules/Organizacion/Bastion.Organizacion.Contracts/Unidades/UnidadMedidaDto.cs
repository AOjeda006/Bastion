using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Unidades;

/// <summary>Una unidad de medida, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la unidad.</param>
/// <param name="Codigo">Código de la unidad, en mayúsculas.</param>
/// <param name="Nombre">Nombre con el que se la conoce.</param>
/// <param name="Decimales">Cuántos decimales admite una cantidad expresada en esta unidad.</param>
/// <remarks>
/// <b>Aquí los decimales SÍ son una columna</b>, al revés que en una divisa, y el contraste es la
/// mitad de lo que hay que entender de los dos maestros. Los de una divisa los fija una regla
/// fiscal que no elige nadie; los de un kilo los elige quien monta el almacén —hay quien pesa a
/// gramos y quien no— y por eso viajan en la fila.
/// </remarks>
public sealed record UnidadMedidaDto(Guid Id, string Codigo, string Nombre, int Decimales);

/// <summary>Lo que hace falta para dar de alta una unidad de medida.</summary>
public sealed record CrearUnidadMedidaDto
{
    /// <summary>Código de la unidad. Se normaliza a mayúsculas.</summary>
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Nombre con el que se la conoce.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>
    /// Cuántos decimales admite una cantidad expresada en esta unidad.
    /// </summary>
    /// <remarks>
    /// El 0 es lo normal en las unidades que se cuentan —no existe media caja— y el tope de seis
    /// es el mismo que el del factor de conversión, para que convertir no pueda producir una
    /// cantidad que su propia unidad no sabe representar.
    /// </remarks>
    [Range(0, 6, ErrorMessage = "Los decimales van del {1} al {2}.")]
    public int Decimales { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de una unidad de medida.
/// </summary>
/// <remarks>
/// Solo el nombre. Los decimales no: bajarlos de tres a uno dejaría inválidas las existencias ya
/// registradas —los 1,250 kg que hay en una estantería— sin tocarlas ni avisar, y subirlos no
/// arreglaría lo que ya se redondeó. Una unidad con otra precisión es otra unidad.
/// </remarks>
public sealed record ModificarUnidadMedidaDto
{
    /// <summary>Nombre con el que se la conoce.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;
}
