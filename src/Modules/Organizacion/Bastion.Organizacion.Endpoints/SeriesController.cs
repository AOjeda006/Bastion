using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.Organizacion.Application.Series;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>Series de numeración, bajo <c>/api/v1/organizacion/series</c>.</summary>
public sealed class SeriesController(
    ICrearSerie crear,
    IObtenerSerie obtener,
    IListarSeries listar,
    IModificarSerie modificar,
    IEliminarSerie eliminar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de series.</summary>
    /// <param name="consulta">Paginación pedida (<c>page</c> y <c>size</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.SerieVer)]
    [ProducesResponseType(typeof(PaginaDe<SerieDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return Ok(await listar.EjecutarAsync(consulta.APaginacion(), cancelacion).ConfigureAwait(false));
    }

    /// <summary>Devuelve una serie.</summary>
    /// <param name="id">Identificador de la serie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.SerieVer)]
    [ProducesResponseType(typeof(SerieDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        Responder(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Crea una serie.</summary>
    /// <param name="peticion">Datos de la serie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [ExigePermiso(PermisosDeOrganizacion.SerieCrear)]
    [ProducesResponseType(typeof(SerieDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearSerieDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            serie => serie.Id);

    /// <summary>Cambia el formato de una serie activa.</summary>
    /// <param name="id">Identificador de la serie.</param>
    /// <param name="peticion">El formato nuevo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.SerieModificar)]
    [ProducesResponseType(typeof(SerieDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Modificar(
        Guid id,
        [FromBody] ModificarSerieDto peticion,
        CancellationToken cancelacion) =>
        Responder(await modificar.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Suprime una serie que todavía no ha numerado.
    /// </summary>
    /// <remarks>
    /// <c>204</c> mientras es un borrador; <c>409</c> en cuanto ha numerado una sola vez, porque
    /// entonces ya forma parte del libro registro (§9 y R11).
    /// </remarks>
    /// <param name="id">Identificador de la serie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.SerieEliminar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await eliminar.EjecutarAsync(id, cancelacion).ConfigureAwait(false));
}
