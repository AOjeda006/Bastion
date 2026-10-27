using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Catalogo.Application;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Catalogo.Endpoints;

/// <summary>Categorías, bajo <c>/api/v1/catalogo/categorias</c>.</summary>
public sealed class CategoriasController(
    ICrearCategoria crear,
    IObtenerCategoria obtener,
    IListarCategorias listar,
    IModificarCategoria modificar) : ControladorDeCatalogo
{
    /// <summary>
    /// Las columnas de la exportación: lo que el listado publica de cada categoría, sin sus
    /// identificadores, y la madre con su código y su nombre (ADR-0074).
    /// </summary>
    /// <remarks>
    /// La lista plana con la madre de cada una, como el listado: el árbol se recompone con el
    /// código de la madre, que es lo que se lee, y no con su identificador.
    /// </remarks>
    internal static readonly HojaDeExportacion<CategoriaExportada> HojaDeCategorias = new(
        "categorias",
        [
            new("codigo", fila => CampoCsv.Texto(fila.Categoria.Codigo)),
            new("nombre", fila => CampoCsv.Texto(fila.Categoria.Nombre)),
            .. ColumnasDeMaestro.De<CategoriaExportada>("padre", fila => fila.Padre),
        ]);

    /// <summary>Devuelve una página de categorías.</summary>
    /// <remarks>
    /// <b>Devuelve la lista plana con el padre de cada una, no el árbol montado.</b> El árbol de
    /// una empresa cabe entero en una o dos páginas —diez niveles de profundidad como máximo, y en
    /// la práctica dos o tres— y componerlo es una vuelta por la lista en el cliente. Servirlo
    /// montado obligaría al servidor a recorrerlo entero en cada lectura para devolver justo lo que
    /// se puede recomponer sin él.
    /// </remarks>
    /// <param name="pagina">La página (<c>page</c>, <c>size</c>).</param>
    /// <param name="consulta">El orden (<c>sort</c>).</param>
    /// <param name="texto">El filtro de texto (<c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeCatalogo.CategoriaVer)]
    [ProducesResponseType(typeof(PaginaDe<CategoriaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] PaginaPedida pagina,
        [FromQuery] ConsultaDeListado consulta,
        [FromQuery] FiltroDeTexto texto,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(pagina, consulta, texto, listar, cancelacion)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Descarga el listado de categorías entero, con su orden y su texto, en CSV (ADR-0073).
    /// </summary>
    /// <param name="consulta">El orden (<c>sort</c>).</param>
    /// <param name="texto">El filtro de texto (<c>q</c>).</param>
    /// <param name="exportar">El caso de uso del listado, con la madre nombrada.</param>
    /// <param name="foto">La foto del módulo, de la que salen todas las páginas.</param>
    /// <param name="reloj">El reloj, que da el día del nombre del fichero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("exportacion")]
    [ExigePermiso(PermisosDeCatalogo.CategoriaVer)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/csv")]
    public async Task<IActionResult> Exportar(
        [FromQuery] ConsultaDeListado consulta,
        [FromQuery] FiltroDeTexto texto,
        [FromServices] IExportarCategorias exportar,
        [FromServices] IFotoDeCatalogo foto,
        [FromServices] TimeProvider reloj,
        CancellationToken cancelacion) =>
        await ExportarListadoAsync(consulta, texto, exportar, foto, HojaDeCategorias, reloj, cancelacion)
            .ConfigureAwait(false);

    /// <summary>Devuelve una categoría.</summary>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.CategoriaVer)]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta una categoría.</summary>
    /// <param name="peticion">Datos de la categoría.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeCatalogo.CategoriaCrear)]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearCategoriaDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            categoria => categoria.Id);

    /// <summary>Cambia el nombre de una categoría, o la mueve de sitio en el árbol.</summary>
    /// <remarks>
    /// <b>Es la operación que puede cerrar un ciclo</b> —mover una rama debajo de su propia
    /// descendencia—, y por eso la comprobación del árbol corre aquí y no solo en el alta. Los tres
    /// desenlaces se distinguen por el <c>type</c>: <c>categoria-ciclo</c>,
    /// <c>categoria-padre-no-encontrado</c> y <c>categoria-demasiado-profunda</c>.
    /// </remarks>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.CategoriaModificar)]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarCategoriaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));
}
