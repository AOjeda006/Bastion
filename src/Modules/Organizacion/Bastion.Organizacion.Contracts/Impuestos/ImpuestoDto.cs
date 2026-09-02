using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Impuestos;

/// <summary>Un tramo de un tipo impositivo, tal como sale de la API.</summary>
/// <param name="Id">Identificador del tramo.</param>
/// <param name="Codigo">Código del impuesto, en mayúsculas. <b>Se repite entre tramos.</b></param>
/// <param name="Nombre">Nombre con el que se le conoce.</param>
/// <param name="Tipo">Clase de impuesto, como texto.</param>
/// <param name="Porcentaje">El tipo aplicable, en tanto por ciento.</param>
/// <param name="VigenteDesde">Primer día en que rige, incluido.</param>
/// <param name="VigenteHasta">Último día en que rige, incluido, o nulo si sigue vigente.</param>
/// <param name="CuentaRepercutido">Cuenta contable del IVA repercutido, o nula.</param>
/// <param name="CuentaSoportado">Cuenta contable del IVA soportado, o nula.</param>
/// <remarks>
/// <b>No lleva empresa, y su ausencia es la regla</b> (R8). El tipo general del IVA es el 21 % para
/// todas las sociedades que operan en España: lo fija el BOE, no el usuario. Es uno de los cinco
/// maestros que se comparten entre sociedades, y esa decisión está escrita —con su motivo— en el
/// barrido que comprueba que ninguna entidad se queda sin filtro por descuido.
/// </remarks>
public sealed record ImpuestoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Tipo,
    decimal Porcentaje,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    string? CuentaRepercutido,
    string? CuentaSoportado);

/// <summary>
/// Lo que hace falta para abrir un tramo de un tipo impositivo.
/// </summary>
/// <remarks>
/// Se da de alta un TRAMO, no un impuesto: el código se repite a propósito. El 1 de septiembre de
/// 2012 el IVA general pasó del 18 % al 21 %, y las facturas anteriores siguen llevando el 18 %
/// para siempre. Por eso no hay forma de cambiar el porcentaje de uno ya guardado.
/// </remarks>
public sealed record CrearImpuestoDto
{
    /// <summary>Código del impuesto. Se normaliza a mayúsculas.</summary>
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Nombre con el que se le conoce.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Clase de impuesto, como texto.</summary>
    [Required(ErrorMessage = "El tipo de impuesto es obligatorio.")]
    public string Tipo { get; init; } = string.Empty;

    /// <summary>
    /// El tipo aplicable, en tanto por ciento.
    /// </summary>
    /// <remarks>
    /// El 0 es un valor legítimo —una operación exenta tributa al 0 %, que no es lo mismo que no
    /// tributar— y por eso el rango empieza en cero y no en uno. El signo lo pone la clase: una
    /// retención resta por ser una retención, no por venir con el número en negativo.
    /// </remarks>
    [Range(0, 100, ErrorMessage = "El porcentaje va del {1} al {2}.")]
    public decimal Porcentaje { get; init; }

    /// <summary>Primer día en que rige, incluido.</summary>
    [Required(ErrorMessage = "La fecha de inicio de vigencia es obligatoria.")]
    public DateOnly VigenteDesde { get; init; }

    /// <summary>Último día en que rige, incluido. Nulo si el tramo queda abierto.</summary>
    public DateOnly? VigenteHasta { get; init; }

    /// <summary>Cuenta contable del impuesto repercutido, o nula si todavía no se sabe.</summary>
    [StringLength(9, ErrorMessage = "La cuenta no puede pasar de {1} caracteres.")]
    public string? CuentaRepercutido { get; init; }

    /// <summary>Cuenta contable del impuesto soportado, o nula si todavía no se sabe.</summary>
    [StringLength(9, ErrorMessage = "La cuenta no puede pasar de {1} caracteres.")]
    public string? CuentaSoportado { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de un tramo ya abierto.
/// </summary>
/// <remarks>
/// Ni el porcentaje ni las fechas ni el código. Un tramo describe lo que decía el BOE en un
/// periodo, y eso ya pasó: corregirlo cambiaría la cuota de facturas emitidas hace años sin dejar
/// rastro de que valían otra cosa. Lo que sí se corrige es cómo se llama y a qué cuenta va.
/// </remarks>
public sealed record ModificarImpuestoDto
{
    /// <summary>Nombre con el que se le conoce.</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Cuenta contable del impuesto repercutido, o nula.</summary>
    [StringLength(9, ErrorMessage = "La cuenta no puede pasar de {1} caracteres.")]
    public string? CuentaRepercutido { get; init; }

    /// <summary>Cuenta contable del impuesto soportado, o nula.</summary>
    [StringLength(9, ErrorMessage = "La cuenta no puede pasar de {1} caracteres.")]
    public string? CuentaSoportado { get; init; }
}

/// <summary>Lo que hace falta para cerrar un tramo vigente.</summary>
public sealed record CerrarImpuestoDto
{
    /// <summary>Último día en que el tramo rige, incluido.</summary>
    [Required(ErrorMessage = "El último día de vigencia es obligatorio.")]
    public DateOnly UltimoDia { get; init; }
}
