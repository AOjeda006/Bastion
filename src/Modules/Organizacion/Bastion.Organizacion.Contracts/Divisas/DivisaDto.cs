using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Divisas;

/// <summary>Una divisa que esta instalación usa, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la divisa.</param>
/// <param name="Codigo">Código ISO 4217 de tres letras, en mayúsculas.</param>
/// <param name="Nombre">Nombre con el que se la conoce.</param>
/// <param name="Decimales">A cuántos decimales se redondea un importe en esta divisa.</param>
/// <remarks>
/// <b><paramref name="Decimales"/> sale pero no se guarda.</b> Cuántos decimales tiene un euro no
/// lo decide quien monta la instalación: son dos, y el yen no tiene ninguno. Ese dato vive en el
/// catálogo del código, con su caso dorado por divisa, porque lo necesita el cálculo de una cuota
/// —que no puede ir a la base a preguntarlo— y porque una fila editable dejaría redondear el euro
/// a tres decimales sin que nada protestara.
/// </remarks>
public sealed record DivisaDto(Guid Id, string Codigo, string Nombre, int Decimales);

/// <summary>
/// Lo que hace falta para dar de alta una divisa.
/// </summary>
/// <remarks>
/// Solo se admiten divisas que el catálogo sepa redondear. Una que no esté se rechaza en vez de
/// entrar con un redondeo supuesto: el error de suponer dos decimales sobre un dinar kuwaití
/// —que tiene tres— no se ve hasta que hay que cuadrar una liquidación.
/// </remarks>
public sealed record CrearDivisaDto
{
    /// <summary>Código ISO 4217 de tres letras. Se normaliza a mayúsculas.</summary>
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "El código ISO 4217 tiene {1} letras.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Nombre con el que se la conoce.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;
}

/// <summary>
/// Lo que se puede cambiar de una divisa.
/// </summary>
/// <remarks>
/// Solo el nombre. El código es ISO y lo llevan escrito las cotizaciones que apuntan a esta fila;
/// los decimales no son una columna.
/// </remarks>
public sealed record ModificarDivisaDto
{
    /// <summary>Nombre con el que se la conoce.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;
}
