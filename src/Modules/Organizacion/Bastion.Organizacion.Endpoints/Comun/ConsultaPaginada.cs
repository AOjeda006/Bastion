using System.ComponentModel.DataAnnotations;
using Bastion.Organizacion.Contracts.Comun;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints.Comun;

/// <summary>
/// Los parámetros de paginación tal como viajan en la URL.
/// </summary>
/// <remarks>
/// <para>
/// Se llaman <c>page</c> y <c>size</c> porque así los fija el §9 del plan maestro. Vive en el
/// borde y no en <c>Contracts</c> por dos motivos: los nombres externos solo importan aquí, y las
/// anotaciones solo sirven en lo que MVC enlaza. Un objeto construido a mano en el controlador se
/// saltaría la validación entera, y el tope de <c>size</c> —que es lo que impide que
/// <c>?size=100000</c> se lleve la tabla— dejaría de existir sin que se notara.
/// </para>
/// <para>
/// Los números salen de <see cref="Paginacion"/>, que es quien los define; aquí solo se anotan.
/// </para>
/// </remarks>
public sealed record ConsultaPaginada
{
    /// <summary>Número de página, empezando en 1.</summary>
    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    /// <summary>Cuántos elementos se piden.</summary>
    [FromQuery(Name = "size")]
    [Range(1, Paginacion.TamanioMaximo, ErrorMessage = "El tamaño de página va de {1} a {2}.")]
    public int Tamanio { get; init; } = Paginacion.TamanioPorDefecto;

    /// <summary>La paginación que entiende la capa de aplicación.</summary>
    public Paginacion APaginacion() => new() { Pagina = Pagina, Tamanio = Tamanio };
}
