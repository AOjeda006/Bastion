using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Contracts;
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
    IEliminarEjercicio eliminar,
    ICerrarEjercicio cerrar,
    IReabrirEjercicio reabrir) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de ejercicios.</summary>
    /// <param name="consulta">Paginación, orden y filtro (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.EjercicioVer)]
    [ProducesResponseType(typeof(PaginaDe<EjercicioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(consulta, listar, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve un ejercicio.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.EjercicioVer)]
    [ProducesResponseType(typeof(EjercicioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Abre un ejercicio.</summary>
    /// <remarks>
    /// El <c>409</c> viene de dos sitios y el <c>type</c> los separa, porque se arreglan distinto:
    /// la empresa ya tiene un ejercicio con ese año (<c>ejercicio-duplicado</c>, se cambia el año)
    /// o el intervalo se pisa con el de otro (<c>ejercicio-solapado</c>, se mueven las fechas).
    /// El segundo lo contesta el caso de uso preguntando antes, pero quien de verdad lo impide es
    /// una restricción de exclusión de la base: es la única que cubre dos peticiones a la vez.
    /// </remarks>
    /// <param name="peticion">Datos del ejercicio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeOrganizacion.EjercicioCrear)]
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
    /// <remarks>
    /// Mover un ejercicio puede ponerlo encima de otro, así que el <c>409</c> también puede ser
    /// <c>ejercicio-solapado</c> además de <c>ejercicio-cerrado</c>. Dejar las fechas como están
    /// <b>no</b> es un solape: un ejercicio no se estorba a sí mismo.
    /// </remarks>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Las fechas nuevas.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.EjercicioModificar)]
    [ProducesResponseType(typeof(EjercicioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarEjercicioDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>Borra un ejercicio que todavía no tiene series.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.EjercicioEliminar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Eliminar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => eliminar.EjecutarAsync(id, version, cancelacion));

    /// <summary>Cierra el ejercicio (R9).</summary>
    /// <remarks>
    /// <para>
    /// Sin puerta HTTP en el 0.4 porque cerrar un ejercicio sin autorización es dejar que
    /// cualquiera congele el año. Con su permiso detrás, se abre.
    /// </para>
    /// <para>
    /// <b>Desde el 2.6 puede contestar 409</b>, y por dos motivos distintos: porque queda algún
    /// borrador con fecha dentro —y entonces el error nombra a los módulos— o porque el ejercicio
    /// ya estaba cerrado. Hasta este ítem cerrar era idempotente y no tenía precondiciones.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/cierre")]
    [ExigePermiso(PermisosDeOrganizacion.EjercicioCerrar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Cerrar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => cerrar.EjecutarAsync(id, version, cancelacion));

    /// <summary>Reabre un ejercicio cerrado, diciendo por qué (R9).</summary>
    /// <remarks>
    /// <para>
    /// <b>Tiene ruta propia y no es el borrado del sub-recurso del cierre</b>, que es la forma que
    /// tenía hasta el 2.6. La razón es el cuerpo: desde este ítem la reapertura exige un motivo, y
    /// un <c>DELETE</c> con cuerpo no es fiable —hay intermediarios y clientes que lo descartan por
    /// el camino, y el motivo llegaría vacío sin que nadie hubiera hecho nada mal—. Un
    /// <c>POST</c> a un recurso que nombra el hecho lo lleva sin discusión.
    /// </para>
    /// <para>
    /// Lleva permiso propio y distinto del de cerrar: reabrir vuelve a admitir apuntes en un
    /// periodo del que probablemente ya se presentaron modelos.
    /// </para>
    /// <para>
    /// Sigue exigiendo <c>If-Match</c>, como las otras tres escrituras del recurso: que la
    /// petición traiga cuerpo no la convierte en una operación sobre un recurso nuevo. Lo que se
    /// modifica es el ejercicio, y su versión es la que hay que traer.
    /// </para>
    /// </remarks>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="peticion">El motivo por el que se reabre.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/reapertura")]
    [ExigePermiso(PermisosDeOrganizacion.EjercicioReabrir)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Reabrir(
        Guid id,
        [FromBody] ReabrirEjercicioDto peticion,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => reabrir.EjecutarAsync(id, peticion, version, cancelacion));
}
