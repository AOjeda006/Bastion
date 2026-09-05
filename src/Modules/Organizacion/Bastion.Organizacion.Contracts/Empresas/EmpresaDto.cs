using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Contracts.Direcciones;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Empresas;

/// <summary>Una empresa, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la empresa.</param>
/// <param name="Nif">NIF, ya normalizado.</param>
/// <param name="RazonSocial">Razón social o nombre del empresario individual.</param>
/// <param name="DomicilioFiscal">Domicilio fiscal, estructurado (R17).</param>
/// <param name="DivisaBase">Divisa base en ISO 4217.</param>
/// <param name="RegimenDeIva">Régimen de IVA, como texto.</param>
/// <remarks>
/// <b>No lleva estado ni fecha de bloqueo, y su ausencia es la regla</b> (R16, desde el 0.10). El
/// filtro de repositorio deja fuera lo bloqueado, así que todo lo que sale por un camino ordinario
/// está activo por construcción: un campo `Estado` solo podría decir «activo», y un campo que solo
/// puede decir una cosa no informa, confunde. Lo bloqueado se ve abriendo un ámbito declarado, y
/// quien lo abra tiene delante la entidad entera.
/// </remarks>
public sealed record EmpresaDto(
    Guid Id,
    string Nif,
    string RazonSocial,
    DireccionDto DomicilioFiscal,
    string DivisaBase,
    string RegimenDeIva);

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

/// <summary>
/// El criterio con el que se busca una empresa, y por dónde seguir. Viaja en el <b>cuerpo</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué esto no es un <c>GET</c> con parámetros.</b> El primer criterio que alguien quiere
/// sobre una empresa es el NIF, y un NIF identifica a una persona física cuando la empresa es un
/// empresario individual. En la cadena de consulta acabaría en el historial del navegador, en el
/// enlace que se copia, en el <c>Referer</c> que se manda al sitio siguiente y en el registro de
/// acceso del servidor de delante. El listado sin criterio sigue siendo un <c>GET</c> porque
/// <c>page</c>, <c>size</c>, <c>sort</c> y <c>q</c> no llevan nada personal (ADR-0025).
/// </para>
/// <para>
/// <b>El vocabulario nace con tope</b>, por lo mismo que <c>TamanioMaximo</c> existe: dos
/// criterios, cada uno con su longitud máxima, y un tamaño de tramo acotado. Un buscador sin tope
/// es una descarga de la tabla que se escribe en una línea.
/// </para>
/// </remarks>
public sealed record BuscarEmpresasDto
{
    /// <summary>NIF exacto. Se normaliza igual que en el alta, así que admite puntos y guiones.</summary>
    /// <remarks>
    /// Es una coincidencia EXACTA y no un «empieza por», a propósito: un NIF parcial no es un
    /// criterio de búsqueda, es un barrido del censo de nueve en nueve caracteres.
    /// </remarks>
    [StringLength(20, ErrorMessage = "El NIF no puede pasar de {1} caracteres.")]
    public string? Nif { get; init; }

    /// <summary>Trozo de la razón social. No distingue mayúsculas ni acentos de más.</summary>
    [StringLength(100, ErrorMessage = "La razón social buscada no puede pasar de {1} caracteres.")]
    public string? RazonSocial { get; init; }

    /// <summary>Por dónde seguir, tal como lo devolvió el tramo anterior. Nulo para empezar.</summary>
    [StringLength(64, ErrorMessage = "El cursor no puede pasar de {1} caracteres.")]
    public string? Cursor { get; init; }

    /// <summary>Cuántas empresas se piden en este tramo.</summary>
    [Range(1, Paginacion.TamanioMaximo, ErrorMessage = "El tamaño va de {1} a {2}.")]
    public int Tamanio { get; init; } = Paginacion.TamanioPorDefecto;
}
