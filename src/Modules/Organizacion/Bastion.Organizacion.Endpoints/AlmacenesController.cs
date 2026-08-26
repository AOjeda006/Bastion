using Bastion.Organizacion.Application.Almacenes;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>Almacenes, bajo <c>/api/v1/organizacion/almacenes</c>.</summary>
public sealed class AlmacenesController(
    ICrearAlmacen crear,
    IObtenerAlmacen obtener,
    IListarAlmacenes listar,
    IModificarAlmacen modificar,
    IBloquearAlmacen bloquear) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de almacenes.</summary>
    /// <param name="consulta">Paginación pedida (<c>page</c> y <c>size</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDe<AlmacenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return Ok(await listar.EjecutarAsync(consulta.APaginacion(), cancelacion).ConfigureAwait(false));
    }

    /// <summary>Devuelve un almacén.</summary>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AlmacenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        Responder(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta un almacén.</summary>
    /// <param name="peticion">Datos del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [ProducesResponseType(typeof(AlmacenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearAlmacenDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            almacen => almacen.Id);

    /// <summary>Cambia los datos de un almacén.</summary>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AlmacenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Modificar(
        Guid id,
        [FromBody] ModificarAlmacenDto peticion,
        CancellationToken cancelacion) =>
        Responder(await modificar.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Bloquea un almacén. No lo borra.
    /// </summary>
    /// <remarks>
    /// Cada movimiento de existencias apunta a su almacén para siempre: borrar la fila rompería el
    /// histórico de valoración, que no se puede reconstruir después.
    /// </remarks>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Bloquear(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await bloquear.EjecutarAsync(id, cancelacion).ConfigureAwait(false));
}
