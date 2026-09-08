using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
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
    /// <summary>Devuelve una página de categorías.</summary>
    /// <remarks>
    /// <b>Devuelve la lista plana con el padre de cada una, no el árbol montado.</b> El árbol de
    /// una empresa cabe entero en una o dos páginas —diez niveles de profundidad como máximo, y en
    /// la práctica dos o tres— y componerlo es una vuelta por la lista en el cliente. Servirlo
    /// montado obligaría al servidor a recorrerlo entero en cada lectura para devolver justo lo que
    /// se puede recomponer sin él.
    /// </remarks>
    /// <param name="consulta">Paginación, orden y filtro (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeCatalogo.CategoriaVer)]
    [ProducesResponseType(typeof(PaginaDe<CategoriaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(consulta, listar, cancelacion).ConfigureAwait(false);
    }

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
