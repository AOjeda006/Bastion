using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Terceros.Application.Terceros;
using Bastion.Terceros.Contracts;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Terceros.Endpoints;

/// <summary>
/// Terceros —clientes, proveedores, o las dos cosas— bajo <c>/api/v1/terceros/terceros</c>.
/// </summary>
/// <remarks>
/// El controlador no tiene lógica: enlaza, llama al caso de uso y traduce el desenlace. Cada
/// operación es un tipo distinto inyectado por separado (§3), de modo que lo que esta clase puede
/// hacer se lee en su constructor.
/// </remarks>
public sealed class TercerosController(
    ICrearTercero crear,
    IObtenerTercero obtener,
    IListarTerceros listar,
    IBuscarTerceros buscar,
    IModificarTercero modificar,
    IBloquearTercero bloquear,
    IDesbloquearTercero desbloquear) : ControladorDeTerceros
{
    /// <summary>Devuelve una página de terceros.</summary>
    /// <remarks>
    /// Sigue siendo un <c>GET</c> porque <c>page</c>, <c>size</c>, <c>sort</c> y <c>q</c> no llevan
    /// nada personal: <c>q</c> busca por razón social y nombre comercial, que es lo que se lee en
    /// una pantalla, no una llave con la que cruzar ficheros.
    /// </remarks>
    /// <param name="consulta">Paginación, orden y filtro (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeTerceros.TerceroVer)]
    [ProducesResponseType(typeof(PaginaDe<TerceroDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(consulta, listar, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Busca terceros por un criterio que no puede ir en la URL.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es un <c>POST</c> que no crea nada</b>, y choca con la lectura ingenua de REST. Es el
    /// precio, se paga a sabiendas y está argumentado en el ADR-0025. Aquí muerde más que en
    /// empresas: el identificador fiscal de un cliente es muy a menudo el DNI de una persona
    /// física, y buscar por él es lo que quien usa esta pantalla va a hacer todos los días. Por la
    /// cadena de consulta quedaría escrito todos los días —historial, enlace copiado,
    /// <c>Referer</c>, registro de acceso del servidor de delante—.
    /// </para>
    /// <para>
    /// <b>La respuesta no lleva enlace a lo siguiente</b>, lleva cursor. Un enlace lo compondría el
    /// servidor con el criterio dentro, y habría devuelto el identificador a una URL él solo.
    /// </para>
    /// </remarks>
    /// <param name="peticion">Criterio y por dónde seguir.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("buscar")]
    [ExigePermiso(PermisosDeTerceros.TerceroVer)]
    [ProducesResponseType(typeof(TramoDe<TerceroDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar(
        [FromBody] BuscarTercerosDto peticion,
        CancellationToken cancelacion) =>
        Responder(await buscar.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Devuelve un tercero.</summary>
    /// <param name="id">Identificador del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeTerceros.TerceroVer)]
    [ProducesResponseType(typeof(TerceroDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta un tercero.</summary>
    /// <remarks>
    /// El <c>409</c> significa «esta empresa ya tiene un tercero con ese identificador fiscal», y
    /// <b>no dice si el que lo ocupa está activo o bloqueado</b>. Es una propiedad, no una
    /// redacción: si las dos respuestas se distinguieran, cualquiera con este formulario podría
    /// recorrer identificadores y sacar la lista de quién está dado de baja, que es lo que el
    /// art. 32 de la LOPDGDD reserva.
    /// </remarks>
    /// <param name="peticion">Datos del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeTerceros.TerceroCrear)]
    [ProducesResponseType(typeof(TerceroDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearTerceroDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            tercero => tercero.Id);

    /// <summary>Cambia los datos de un tercero.</summary>
    /// <remarks>
    /// Sin el identificador fiscal, y no por olvido: aparece en cada factura ya emitida a ese
    /// tercero. Cambiarlo no es modificar al tercero, es otro tercero. Al no estar en el contrato,
    /// no hay manera de intentarlo.
    /// </remarks>
    /// <param name="id">Identificador del tercero.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeTerceros.TerceroModificar)]
    [ProducesResponseType(typeof(TerceroDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarTerceroDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>
    /// Bloquea un tercero. No lo borra.
    /// </summary>
    /// <remarks>
    /// Es lo que procede cuando alguien ejerce su derecho de supresión: sus datos se identifican y
    /// se reservan (art. 32 de la LOPDGDD). Borrar la fila dejaría sin cuadrar las facturas que ya
    /// se le emitieron (R15), que es lo que la ley no permite tocar.
    /// </remarks>
    /// <param name="id">Identificador del tercero.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ExigePermiso(PermisosDeTerceros.TerceroBloquear)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Bloquear(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => bloquear.EjecutarAsync(id, version, cancelacion));

    /// <summary>Devuelve un tercero bloqueado a la operativa.</summary>
    /// <remarks>
    /// <b>Sin <c>If-Match</c>, como los demás desbloqueos.</b> No es un descuido ni una excepción
    /// de comodidad: un recurso bloqueado no se lee por ningún camino ordinario y el <c>ETag</c> se
    /// obtiene leyendo, así que exigir aquí una versión sería exigir una llave que no se puede
    /// conseguir y dejaría la puerta cerrada para siempre. Lo que el <c>If-Match</c> evita —pisar
    /// el cambio de otro— aquí no puede pasar: mientras el recurso está bloqueado ninguna otra
    /// escritura llega hasta él, y desbloquear dos veces deja el mismo resultado que desbloquear
    /// una (ADR-0017).
    /// </remarks>
    /// <param name="id">Identificador del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/desbloqueo")]
    [ExigePermiso(PermisosDeTerceros.TerceroDesbloquear)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desbloquear(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await desbloquear.EjecutarAsync(id, cancelacion).ConfigureAwait(false));
}
