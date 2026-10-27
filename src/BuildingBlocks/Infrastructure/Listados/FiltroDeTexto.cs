using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// El texto por el que se acota un listado, tal como viaja en la URL: <c>?q=</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Solo lo enlaza la acción de un listado que lo mira</b>, y por eso es una pieza aparte desde
/// el ítem 3.2. Hasta entonces lo publicaban los 23 listados, y seis no lo miraban: el
/// <c>?q=</c> llegaba, el paginador lo dejaba pasar y la respuesta era la página entera
/// (ADR-0069). Ahora esos seis no lo publican, y si llega uno, el borde lo rechaza con un
/// <c>400</c> <see cref="CodigoDeFiltroNoAdmitido"/> (ADR-0072).
/// </para>
/// <para>
/// Qué campos mira lo decide cada recurso, y ninguno de ellos es sensible: esto viaja en la URL.
/// Buscar por NIF, correo o teléfono va por cuerpo (ADR-0025).
/// </para>
/// </remarks>
public sealed record FiltroDeTexto
{
    /// <summary>Nombre externo del parámetro.</summary>
    public const string NombreDelFiltro = "q";

    /// <summary>Código del error con el que se rechaza un <c>?q=</c> en un listado que no lo mira.</summary>
    /// <remarks>
    /// Público porque es contrato: acaba publicado como <c>/errors/filtro-no-admitido</c> en el
    /// <c>type</c> del ProblemDetails.
    /// </remarks>
    public const string CodigoDeFiltroNoAdmitido = "filtro-no-admitido";

    /// <summary>El texto. En blanco es lo mismo que no mandarlo.</summary>
    [FromQuery(Name = NombreDelFiltro)]
    [StringLength(100, ErrorMessage = "El filtro no puede pasar de {1} caracteres.")]
    public string? Texto { get; init; }

    /// <summary>
    /// Si la URL trae un <c>?q=</c> con algo escrito. Lo mira el borde de un listado que no
    /// enlaza esta pieza, que es el único que puede recibirlo sin leerlo.
    /// </summary>
    /// <remarks>
    /// <b>En blanco no cuenta</b>, como en el paginador: un <c>?q=</c> vacío es «sin filtro», y un
    /// cliente que lo manda siempre vacío no está pidiendo nada que el listado no haga.
    /// </remarks>
    /// <param name="consulta">La cadena de consulta de la petición.</param>
    internal static bool LlegaEn(IQueryCollection consulta) =>
        consulta.TryGetValue(NombreDelFiltro, out StringValues valores)
        && valores.Any(valor => !string.IsNullOrWhiteSpace(valor));

    /// <summary>El <c>400</c> de un <c>?q=</c> en un listado sin filtro de texto.</summary>
    /// <remarks>
    /// Aquí, junto a su código, porque así lo encuentra el barrido del catálogo de errores, que
    /// solo resuelve constantes del mismo fichero.
    /// </remarks>
    internal static ErrorDeOperacion NoAdmitido() => ErrorDeOperacion.Validacion(
        CodigoDeFiltroNoAdmitido,
        "Este listado no tiene filtro de texto, así que no se puede acotar con ?q=.",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [NombreDelFiltro] = ["Este listado no busca por texto."],
        });
}
