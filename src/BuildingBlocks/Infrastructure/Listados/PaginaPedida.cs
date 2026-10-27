using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Qué página de un listado se pide, tal como viaja en la URL: <c>?page=&amp;size=</c>.
/// </summary>
/// <remarks>
/// <para>
/// Se llaman <c>page</c> y <c>size</c> porque así los fija el §9 del plan maestro. Los números
/// salen de <see cref="Paginacion"/>, que es quien los define; aquí solo se anotan, y el tope de
/// <c>size</c> es lo que impide que <c>?size=100000</c> se lleve la tabla.
/// </para>
/// <para>
/// <b>Es una pieza aparte desde el ítem 3.2</b> porque la exportación de un listado no la lleva:
/// saca todas las páginas, y un <c>?page=</c> publicado y sin leer sería una mentira del contrato
/// (ADR-0072).
/// </para>
/// </remarks>
public sealed record PaginaPedida
{
    /// <summary>Nombre externo del número de página.</summary>
    public const string NombreDeLaPagina = "page";

    /// <summary>Nombre externo del tamaño de página.</summary>
    public const string NombreDelTamanio = "size";

    /// <summary>Número de página, empezando en 1.</summary>
    /// <remarks>
    /// El tope es <see cref="Paginacion.PaginaMaxima"/> y no <c>int.MaxValue</c>: con él, el salto
    /// cabe en un <c>int</c> con cualquier tamaño admitido. Sin él, una página enorme era un
    /// <c>500</c>, y esta anotación es la guarda de todos sus lectores.
    /// </remarks>
    [FromQuery(Name = NombreDeLaPagina)]
    [Range(1, Paginacion.PaginaMaxima, ErrorMessage = "La página va de {1} a {2}.")]
    public int Pagina { get; init; } = 1;

    /// <summary>Cuántos elementos se piden.</summary>
    [FromQuery(Name = NombreDelTamanio)]
    [Range(1, Paginacion.TamanioMaximo, ErrorMessage = "El tamaño de página va de {1} a {2}.")]
    public int Tamanio { get; init; } = Paginacion.TamanioPorDefecto;
}
