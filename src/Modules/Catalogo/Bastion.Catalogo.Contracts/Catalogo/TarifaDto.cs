using System.ComponentModel.DataAnnotations;

namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>Un tramo de tarifa, tal como sale de la API.</summary>
/// <remarks>
/// <b>El código se repite entre tramos y el identificador no.</b> Quien quiera «la tarifa PVP»
/// pide todos sus tramos y mira cuál rige; quien quiera el precio de un artículo no hace nada de
/// eso y pregunta por <c>/tarifas/{codigo}/precio</c>, que resuelve el tramo por la fecha.
/// </remarks>
/// <param name="Id">Identificador del tramo.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="Codigo">Código, en mayúsculas. Se repite entre tramos de la misma tarifa.</param>
/// <param name="Nombre">Nombre con el que se muestra.</param>
/// <param name="Tipo">
/// <c>Venta</c> o <c>Compra</c>, como texto. Lo comparten todos los tramos del código.
/// </param>
/// <param name="Divisa">Divisa en la que se expresan sus precios, en ISO 4217.</param>
/// <param name="VigenteDesde">Primer día en que rige, incluido.</param>
/// <param name="VigenteHasta">Último día en que rige, incluido; nulo mientras siga vigente.</param>
public sealed record TarifaDto(
    Guid Id,
    Guid EmpresaId,
    string Codigo,
    string Nombre,
    string Tipo,
    string Divisa,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta);

/// <summary>Lo que hace falta para dar de alta un tramo de tarifa.</summary>
/// <remarks>
/// <b>No lleva empresa</b>: sale del <i>claim</i> y nunca del cuerpo (R8).
/// </remarks>
public sealed record CrearTarifaDto
{
    /// <summary>Código de la tarifa. Se normaliza a mayúsculas y se repite entre tramos.</summary>
    [Required(ErrorMessage = "El código de la tarifa es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Nombre con el que se muestra.</summary>
    [Required(ErrorMessage = "El nombre de la tarifa es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Si es de venta o de compra: <c>Venta</c> o <c>Compra</c>.</summary>
    /// <remarks>
    /// <b>Es obligatorio y no tiene valor por omisión</b>: una tarifa de compra abierta como de
    /// venta propondría a un proveedor los precios de un cliente. Y es <b>el mismo</b> que el de los
    /// tramos que ya tenga el código; uno distinto es un <c>409</c>.
    /// </remarks>
    [Required(ErrorMessage = "El tipo de la tarifa es obligatorio.")]
    public string Tipo { get; init; } = string.Empty;

    /// <summary>
    /// Divisa en la que se expresan los precios de esta tarifa, en ISO 4217: tres letras.
    /// </summary>
    /// <remarks>
    /// <b>Es obligatoria y no se hereda de la empresa</b>, por lo mismo que la del límite de
    /// crédito del ítem 1.6: heredarla en silencio deja todas las tarifas ya guardadas
    /// reinterpretadas el día que alguien cambie la divisa base. Y <b>puede no ser la de la
    /// empresa</b>: una tarifa de exportación en dólares es legítima, y la divisa viaja con cada
    /// precio resuelto para que nadie la confunda. Tiene que estar dada de alta en Organización y
    /// no retirada.
    /// </remarks>
    [Required(ErrorMessage = "La divisa de la tarifa es obligatoria.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "La divisa son tres letras (ISO 4217).")]
    public string Divisa { get; init; } = string.Empty;

    /// <summary>Primer día en que rige, incluido.</summary>
    [Required(ErrorMessage = "La fecha desde la que rige es obligatoria.")]
    public DateOnly VigenteDesde { get; init; }

    /// <summary>
    /// Último día en que rige, incluido; nulo para dejarla abierta.
    /// </summary>
    /// <remarks>
    /// <b>Los dos extremos son cerrados</b>, igual que los de un tramo de impuesto: «hasta el 31 de
    /// diciembre» incluye el 31. Un tramo abierto se solapa con cualquier otro posterior del mismo
    /// código, que es exactamente lo que la restricción de exclusión impide.
    /// </remarks>
    public DateOnly? VigenteHasta { get; init; }
}

/// <summary>Lo que se puede cambiar de un tramo de tarifa.</summary>
/// <remarks>
/// <b>Solo el nombre.</b> Ni el código, ni el tipo, ni la divisa, ni el tramo de fechas: los cuatro
/// describen los precios que ya se aplicaron bajo esta fila, y cambiarlos los reescribiría hacia
/// atrás. Subir precios es cerrar este tramo y crear el siguiente con el mismo código.
/// </remarks>
public sealed record ModificarTarifaDto
{
    /// <summary>Nombre con el que se muestra.</summary>
    [Required(ErrorMessage = "El nombre de la tarifa es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;
}

/// <summary>Lo que hace falta para cerrar un tramo de tarifa.</summary>
public sealed record CerrarTarifaDto
{
    /// <summary>Último día en que rige, incluido.</summary>
    [Required(ErrorMessage = "El último día de vigencia es obligatorio.")]
    public DateOnly UltimoDia { get; init; }
}
