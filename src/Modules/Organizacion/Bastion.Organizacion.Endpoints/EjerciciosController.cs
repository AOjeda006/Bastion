using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>Ejercicios contables, bajo <c>/api/v1/organizacion/ejercicios</c>.</summary>
public sealed class EjerciciosController(
    ICrearEjercicio crear,
    IObtenerEjercicio obtener,
    IListarEjercicios listar,
    IModificarEjercicio modificar,
    IEliminarEjercicio eliminar) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de ejercicios.</summary>
    /// <param name="consulta">Paginación pedida (<c>page</c> y <c>size</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDe<EjercicioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return Ok(await listar.EjecutarAsync(consulta.APaginacion(), cancelacion).ConfigureAwait(false));
    }

    /// <summary>Devuelve un ejercicio.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EjercicioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        Responder(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Abre un ejercicio.</summary>
    /// <param name="peticion">Datos del ejercicio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [ProducesResponseType(typeof(EjercicioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearEjercicioDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            ejercicio => ejercicio.Id);

    /// <summary>Cambia las fechas de un ejercicio abierto.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="peticion">Las fechas nuevas.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EjercicioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Modificar(
        Guid id,
        [FromBody] ModificarEjercicioDto peticion,
        CancellationToken cancelacion) =>
        Responder(await modificar.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Borra un ejercicio que todavía no tiene series.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await eliminar.EjecutarAsync(id, cancelacion).ConfigureAwait(false));
}
