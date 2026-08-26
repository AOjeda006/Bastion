using System.ComponentModel.DataAnnotations;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Empresas;

/// <summary>Una empresa, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la empresa.</param>
/// <param name="Nif">NIF, ya normalizado.</param>
/// <param name="RazonSocial">Razón social o nombre del empresario individual.</param>
/// <param name="DomicilioFiscal">Domicilio fiscal, estructurado (R17).</param>
/// <param name="DivisaBase">Divisa base en ISO 4217.</param>
/// <param name="RegimenDeIva">Régimen de IVA, como texto.</param>
/// <param name="Estado">Estado de la empresa, como texto.</param>
/// <param name="BloqueadaEn">Cuándo se bloqueó, o nulo si está activa.</param>
public sealed record EmpresaDto(
    Guid Id,
    string Nif,
    string RazonSocial,
    DireccionDto DomicilioFiscal,
    string DivisaBase,
    string RegimenDeIva,
    string Estado,
    DateTimeOffset? BloqueadaEn);

/// <summary>Lo que hace falta para dar de alta una empresa.</summary>
/// <remarks>
/// Los enumerados viajan como TEXTO, no como número: un ordinal es un contrato que se rompe
/// solo con reordenar el enumerado, y el que lo reordena no ve que está rompiendo un cliente
/// (`patrones/repository-y-dto.md`). Que el texto sea uno de los valores admitidos no lo puede
/// comprobar una anotación sin duplicar aquí el enumerado del dominio, así que lo comprueba el
/// caso de uso y lo devuelve como error de ESE campo.
/// </remarks>
public sealed record CrearEmpresaDto
{
    /// <summary>NIF de la empresa. Se normaliza y se valida su carácter de control.</summary>
    [Required(ErrorMessage = "El NIF es obligatorio.")]
    public string Nif { get; init; } = string.Empty;

    /// <summary>Razón social, o nombre del empresario individual.</summary>
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(200, ErrorMessage = "La razón social no puede pasar de {1} caracteres.")]
    public string RazonSocial { get; init; } = string.Empty;

    /// <summary>Domicilio fiscal, en los seis campos de R17.</summary>
    [Required(ErrorMessage = "El domicilio fiscal es obligatorio.")]
    public DireccionDto DomicilioFiscal { get; init; } = new();

    /// <summary>Divisa base en ISO 4217, tres letras.</summary>
    [Required(ErrorMessage = "La divisa base es obligatoria.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "La divisa son tres letras (ISO 4217).")]
    public string DivisaBase { get; init; } = string.Empty;

    /// <summary>Régimen de IVA, como texto.</summary>
    [Required(ErrorMessage = "El régimen de IVA es obligatorio.")]
    public string RegimenDeIva { get; init; } = string.Empty;
}

/// <summary>
/// Lo que se puede cambiar de una empresa ya dada de alta.
/// </summary>
/// <remarks>
/// Sin NIF, y no por olvido: el NIF identifica a la empresa ante la AEAT y aparece en cada
/// factura ya emitida. Cambiarlo no es modificar la empresa, es otra empresa. Al no estar en el
/// contrato, no hay ni siquiera manera de intentarlo.
/// </remarks>
public sealed record ModificarEmpresaDto
{
    /// <summary>Razón social, o nombre del empresario individual.</summary>
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(200, ErrorMessage = "La razón social no puede pasar de {1} caracteres.")]
    public string RazonSocial { get; init; } = string.Empty;

    /// <summary>Domicilio fiscal, en los seis campos de R17.</summary>
    [Required(ErrorMessage = "El domicilio fiscal es obligatorio.")]
    public DireccionDto DomicilioFiscal { get; init; } = new();

    /// <summary>Divisa base en ISO 4217, tres letras.</summary>
    [Required(ErrorMessage = "La divisa base es obligatoria.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "La divisa son tres letras (ISO 4217).")]
    public string DivisaBase { get; init; } = string.Empty;

    /// <summary>Régimen de IVA, como texto.</summary>
    [Required(ErrorMessage = "El régimen de IVA es obligatorio.")]
    public string RegimenDeIva { get; init; } = string.Empty;
}
