using System.ComponentModel.DataAnnotations;

namespace Bastion.BuildingBlocks.Contracts.Direcciones;

/// <summary>
/// Una dirección estructurada, en los seis campos que exige R17.
/// </summary>
/// <remarks>
/// <para>
/// Los mismos seis campos de entrada y de salida a propósito: una dirección no se lee distinta
/// de como se escribe, y dos formas separadas solo servirían para que una se quedara atrás.
/// </para>
/// <para>
/// Las longitudes están escritas aquí como números y no tomadas de <c>Direccion</c>, que es
/// quien manda: este proyecto no referencia NADA, porque lo referencia el <c>Contracts</c> de cada
/// módulo y arrastrar el dominio lo abriría por la puerta de atrás a todo el que leyera un
/// contrato ajeno. La copia no se queda desfasada porque hay un test que compara las dos: si
/// alguien cambia el dominio y no esto, se pone rojo.
/// </para>
/// <para>
/// <b>Vive en el bloque común desde el ítem 1.5</b>, y no en <c>Organizacion.Contracts</c>, por lo
/// mismo que <c>Paginacion</c> se movió en el 1.3 (ADR-0029): la necesitan Organización —empresa y
/// almacén— y Terceros, y en la fase 5 la necesitará Facturación. La alternativa era una segunda
/// copia con los mismos seis campos y los mismos topes, que es la que se separa de la primera el
/// día que una se toque. Un módulo no puede referenciar el <c>Contracts</c> de otro para tomar
/// prestado un DTO común: eso lo ata a un módulo con el que no tiene nada que ver.
/// </para>
/// </remarks>
public sealed record DireccionDto
{
    /// <summary>Nombre de la vía.</summary>
    [Required(ErrorMessage = "La calle es obligatoria.")]
    [StringLength(70, ErrorMessage = "La calle no puede pasar de {1} caracteres.")]
    public string Calle { get; init; } = string.Empty;

    /// <summary>Número, portal, piso. Opcional: hay direcciones que no lo tienen.</summary>
    [StringLength(16, ErrorMessage = "El número no puede pasar de {1} caracteres.")]
    public string? Numero { get; init; }

    /// <summary>Código postal, tal como lo escribe el país de la dirección.</summary>
    [Required(ErrorMessage = "El código postal es obligatorio.")]
    [StringLength(16, ErrorMessage = "El código postal no puede pasar de {1} caracteres.")]
    public string CodigoPostal { get; init; } = string.Empty;

    /// <summary>Población.</summary>
    [Required(ErrorMessage = "La población es obligatoria.")]
    [StringLength(35, ErrorMessage = "La población no puede pasar de {1} caracteres.")]
    public string Poblacion { get; init; } = string.Empty;

    /// <summary>Provincia, región o estado. Opcional: no todos los países la usan.</summary>
    [StringLength(35, ErrorMessage = "La subdivisión no puede pasar de {1} caracteres.")]
    public string? Subdivision { get; init; }

    /// <summary>País en ISO 3166-1 alfa-2, dos letras.</summary>
    [Required(ErrorMessage = "El país es obligatorio.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "El país son dos letras (ISO 3166-1 alfa-2).")]
    public string Pais { get; init; } = string.Empty;
}
