using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Series;

/// <summary>Una serie de numeración, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la serie.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="EjercicioId">Ejercicio en el que numera.</param>
/// <param name="TipoDeDocumento">Qué documentos numera, como texto.</param>
/// <param name="Codigo">Código de la serie, en mayúsculas.</param>
/// <param name="Formato">Plantilla con la que se compone el número del documento.</param>
/// <param name="Contador">Último número asignado. Cero mientras no haya numerado nada.</param>
/// <param name="Estado">Estado de la serie, como texto.</param>
public sealed record SerieDto(
    Guid Id,
    Guid EmpresaId,
    Guid EjercicioId,
    string TipoDeDocumento,
    string Codigo,
    string Formato,
    long Contador,
    string Estado);

/// <summary>Lo que hace falta para crear una serie.</summary>
/// <remarks>
/// Sin contador: una serie nace en cero y sube de uno en uno cuando numera. Dejar que el cliente
/// lo fije sería dejarle abrir huecos en la numeración desde el primer día, y R11 no admite
/// huecos.
/// </remarks>
public sealed record CrearSerieDto
{
    /// <summary>Empresa a la que pertenece la serie.</summary>
    [Required(ErrorMessage = "La empresa es obligatoria.")]
    public Guid EmpresaId { get; init; }

    /// <summary>Ejercicio en el que numera.</summary>
    [Required(ErrorMessage = "El ejercicio es obligatorio.")]
    public Guid EjercicioId { get; init; }

    /// <summary>Qué documentos numera, como texto.</summary>
    [Required(ErrorMessage = "El tipo de documento es obligatorio.")]
    public string TipoDeDocumento { get; init; } = string.Empty;

    /// <summary>Código de la serie. Se normaliza a mayúsculas.</summary>
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Plantilla con la que se compone el número del documento.</summary>
    [Required(ErrorMessage = "El formato es obligatorio.")]
    [StringLength(60, ErrorMessage = "El formato no puede pasar de {1} caracteres.")]
    public string Formato { get; init; } = string.Empty;
}

/// <summary>
/// Lo que se puede cambiar de una serie: su formato.
/// </summary>
/// <remarks>
/// Ni el código, ni el ejercicio, ni el tipo de documento: los tres están en cada factura ya
/// emitida por esta serie y en lo que se ha declarado a Hacienda con ella.
/// </remarks>
public sealed record ModificarSerieDto
{
    /// <summary>Plantilla con la que se compone el número del documento.</summary>
    [Required(ErrorMessage = "El formato es obligatorio.")]
    [StringLength(60, ErrorMessage = "El formato no puede pasar de {1} caracteres.")]
    public string Formato { get; init; } = string.Empty;
}
