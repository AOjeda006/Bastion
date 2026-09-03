using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Mvc;

// Alias porque la propiedad `Orden` de este tipo y el tipo `Orden` se llaman igual, y en
// posicion de tipo gana el miembro. Renombrar la propiedad la alejaria de `Paginacion.Orden`,
// que es lo que produce; el alias deja los dos nombres donde tienen que estar.
using OrdenPedido = Bastion.BuildingBlocks.Contracts.Paginacion.Orden;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Los parámetros de un listado tal como viajan en la URL: <c>?page=&amp;size=&amp;sort=&amp;q=</c>.
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
/// <para>
/// <b>Estos cuatro nombres son todos los parámetros de consulta que tiene un listado</b>, y eso lo
/// vigila una regla del carril funcional, no una costumbre: ninguna acción de listado declara como
/// parámetro de consulta ningún campo de la lista de sensibles (ADR-0025). El día que alguien
/// añada un <c>?nif=</c> «porque es cómodo», ese NIF acabaría en el historial del navegador, en el
/// enlace que se copia y en el registro de acceso del servidor de delante.
/// </para>
/// <para>
/// Vive en el bloque común desde el ítem 1.3; estaba duplicada en Identidad y en Organización.
/// </para>
/// </remarks>
public sealed record ConsultaPaginada
{
    /// <summary>Código del error con el que se rechaza un <c>?sort=</c> que no se admite.</summary>
    /// <remarks>
    /// Público porque es contrato: acaba publicado como <c>/errors/orden-no-admitido</c> en el
    /// <c>type</c> del ProblemDetails, y un cliente puede ramificar sobre él.
    /// </remarks>
    public const string CodigoDeOrdenNoAdmitido = "orden-no-admitido";

    /// <summary>Nombre externo del parámetro de orden.</summary>
    public const string NombreDelOrden = "sort";

    /// <summary>Nombre externo del parámetro de filtro.</summary>
    public const string NombreDelFiltro = "q";

    /// <summary>Número de página, empezando en 1.</summary>
    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    /// <summary>Cuántos elementos se piden.</summary>
    [FromQuery(Name = "size")]
    [Range(1, Paginacion.TamanioMaximo, ErrorMessage = "El tamaño de página va de {1} a {2}.")]
    public int Tamanio { get; init; } = Paginacion.TamanioPorDefecto;

    /// <summary>Por qué campo se ordena: <c>?sort=codigo</c> o <c>?sort=-codigo</c>.</summary>
    /// <remarks>
    /// Sin anotación de validación, y no por olvido: qué nombres valen depende del recurso, y una
    /// anotación no puede saberlo. Lo comprueba <see cref="APaginacion"/> contra la lista que
    /// declara el propio recurso.
    /// </remarks>
    [FromQuery(Name = NombreDelOrden)]
    public string? Orden { get; init; }

    /// <summary>Texto por el que se acota el listado.</summary>
    /// <remarks>
    /// Qué campos mira lo decide cada recurso, y ninguno de ellos es sensible: esto viaja en la
    /// URL. Buscar por NIF, correo o teléfono va por cuerpo (ADR-0025).
    /// </remarks>
    [FromQuery(Name = NombreDelFiltro)]
    [StringLength(100, ErrorMessage = "El filtro no puede pasar de {1} caracteres.")]
    public string? Filtro { get; init; }

    /// <summary>
    /// La paginación que entiende la capa de aplicación, o el error si el orden pedido no se
    /// admite.
    /// </summary>
    /// <remarks>
    /// El rechazo es un <c>400</c> con la lista de lo que sí vale, no un orden por omisión
    /// silencioso: quien escribió <c>?sort=nombre</c> creyendo que existe merece enterarse de que
    /// no, en vez de recibir una página bien formada ordenada por otra cosa.
    /// </remarks>
    /// <param name="camposOrdenables">Los nombres que este recurso admite en <c>?sort=</c>.</param>
    public Resultado<Paginacion> APaginacion(IReadOnlySet<string> camposOrdenables)
    {
        ArgumentNullException.ThrowIfNull(camposOrdenables);

        var orden = OrdenPedido.Leer(Orden);

        if (orden is not null && !camposOrdenables.Contains(orden.Campo))
        {
            return Resultado.Fallo<Paginacion>(ErrorDeOperacion.Validacion(
                CodigoDeOrdenNoAdmitido,
                $"Por ese campo no se puede ordenar. Los que valen: {string.Join(", ", camposOrdenables.Order(StringComparer.Ordinal))}.",
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    [NombreDelOrden] = [$"El campo {orden.Campo} no está entre los ordenables."],
                }));
        }

        return Resultado.Correcto(new Paginacion
        {
            Pagina = Pagina,
            Tamanio = Tamanio,
            Orden = orden,
            Filtro = Filtro,
        });
    }
}
