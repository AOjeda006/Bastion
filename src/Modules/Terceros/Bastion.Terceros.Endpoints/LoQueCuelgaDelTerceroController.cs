using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.Terceros.Application.Terceros;
using Bastion.Terceros.Contracts;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Terceros.Endpoints;

/// <summary>
/// Lo que cuelga de un tercero —contactos, cuentas bancarias, condiciones de pago y límite de
/// crédito— bajo <c>/api/v1/terceros/terceros/{terceroId}/…</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un controlador y no cuatro</b>, porque los cuatro son partes del MISMO agregado y comparten
/// las tres decisiones que importan: el <c>If-Match</c> es el de la ficha, el permiso es el de la
/// ficha, y el <c>404</c> es el de la ficha. Repartirlos en cuatro clases obligaría a repetir esas
/// tres decisiones cuatro veces, que es como se acaba teniendo cuatro versiones de la misma.
/// </para>
/// <para>
/// <b>Rutas anidadas y no de primer nivel.</b> Un contacto no se busca por su identificador ni
/// existe sin su ficha; su URL lo dice. Y así el <c>terceroId</c> viaja en la ruta, que es donde
/// un identificador opaco puede ir sin problema — el que no puede ir en una URL es el IBAN, y por
/// eso <b>aquí no hay ninguna búsqueda por IBAN</b>: se listan las cuentas de una ficha que ya se
/// ha nombrado, que es lo que la pantalla necesita.
/// </para>
/// <para>
/// <b>Ninguna escritura admite <c>Idempotency-Key</c>, y todas exigen <c>If-Match</c>.</b> Son dos
/// mecanismos distintos y pedir los dos a la vez está prohibido por el carril: la clave de
/// idempotencia protege un ALTA, que no tiene versión previa que citar. Aquí ni siquiera colgar un
/// contacto es un alta suelta — es una modificación del agregado, que sí tiene versión —, así que
/// el reintento de un cliente que perdió la respuesta se estrella contra un <c>412</c> en vez de
/// duplicar la fila, que es exactamente lo que hace falta.
/// </para>
/// <para>
/// <b>Cada escritura lleva su propio permiso</b>, ninguno reutiliza <c>tercero.modificar</c>:
/// decidir a qué cuenta se paga y decidir cuánto se fía no son la misma facultad que corregir un
/// domicilio, y con un permiso compartido no habría manera de expresarlo.
/// </para>
/// <para>
/// <b>El testigo de versión es el de la FICHA en todas las escrituras.</b> Ni los contactos ni las
/// cuentas ni las condiciones tienen versión propia: lo que se está cambiando es el agregado, y su
/// versión es la que se lee en el <c>ETag</c> de <c>GET /terceros/{id}</c>. Con un testigo por
/// hijo, colgar un contacto mientras otro cambia la razón social no chocaría, y la segunda
/// escritura pisaría a la primera sin que ninguna versión visible cambiara.
/// </para>
/// </remarks>
[ApiController]
[Route(ControladorDeTerceros.Prefijo + "/terceros/{terceroId:guid}")]
public sealed class LoQueCuelgaDelTerceroController(
    IListarContactos listarContactos,
    IAgregarContacto agregarContacto,
    IQuitarContacto quitarContacto,
    IListarCuentasBancarias listarCuentas,
    IAgregarCuentaBancaria agregarCuenta,
    IMarcarCuentaPreferente marcarPreferente,
    IQuitarCuentaBancaria quitarCuenta,
    IListarCondicionesPago listarCondiciones,
    IFijarCondicionPago fijarCondicion,
    IObtenerLimiteCredito obtenerLimite,
    IFijarLimiteCredito fijarLimite) : ControladorDeTerceros
{
    /// <summary>Devuelve los contactos de un tercero.</summary>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("contactos")]
    [ExigePermiso(PermisosDeTerceros.TerceroVer)]
    [ProducesResponseType(typeof(IReadOnlyList<ContactoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarContactos(
        Guid terceroId,
        CancellationToken cancelacion) =>
        Responder(await listarContactos.EjecutarAsync(terceroId, cancelacion).ConfigureAwait(false));

    /// <summary>Cuelga un contacto de un tercero.</summary>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="ifMatch">Versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">Datos del contacto.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("contactos")]
    [ExigePermiso(PermisosDeTerceros.ContactoAgregar)]
    [ProducesResponseType(typeof(ContactoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> AgregarContacto(
        Guid terceroId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ContactoDeAltaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => agregarContacto.EjecutarAsync(terceroId, version, peticion, cancelacion));

    /// <summary>Quita un contacto de un tercero.</summary>
    /// <remarks>
    /// <b>Aquí sí se borra la fila</b>, y no choca con el art. 32: el contacto no es el interesado
    /// de la ficha, y nada de lo emitido cuelga de él. Conservarlo no protegería ninguna cuenta y
    /// sí mantendría el nombre y el teléfono de alguien que dejó de tener relación con el negocio.
    /// </remarks>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="contactoId">Identificador del contacto.</param>
    /// <param name="ifMatch">Versión de la FICHA sobre la que se escribe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("contactos/{contactoId:guid}")]
    [ExigePermiso(PermisosDeTerceros.ContactoQuitar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> QuitarContacto(
        Guid terceroId,
        Guid contactoId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => quitarContacto.EjecutarAsync(terceroId, contactoId, version, cancelacion));

    /// <summary>Devuelve las cuentas bancarias de un tercero.</summary>
    /// <remarks>
    /// <b>El IBAN sale entero.</b> Quien tiene el permiso de terceros de esta empresa es quien va a
    /// pagar por esa cuenta, y una pantalla que enseñara <c>ES99****1234</c> no serviría para
    /// comprobar que el número es el que puso el proveedor en su factura. Lo que no lo enseña es el
    /// registro, que es donde se queda para siempre.
    /// </remarks>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("cuentas-bancarias")]
    [ExigePermiso(PermisosDeTerceros.TerceroVer)]
    [ProducesResponseType(typeof(IReadOnlyList<CuentaBancariaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarCuentasBancarias(
        Guid terceroId,
        CancellationToken cancelacion) =>
        Responder(await listarCuentas.EjecutarAsync(terceroId, cancelacion).ConfigureAwait(false));

    /// <summary>Cuelga una cuenta bancaria de un tercero.</summary>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="ifMatch">Versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">Datos de la cuenta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("cuentas-bancarias")]
    [ExigePermiso(PermisosDeTerceros.CuentaBancariaAgregar)]
    [ProducesResponseType(typeof(CuentaBancariaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> AgregarCuentaBancaria(
        Guid terceroId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] CuentaBancariaDeAltaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => agregarCuenta.EjecutarAsync(terceroId, version, peticion, cancelacion));

    /// <summary>Hace preferente una de las cuentas del tercero.</summary>
    /// <remarks>
    /// <b>Como mucho una preferente por ficha</b>, y eso lo sostienen dos cosas a la vez: el
    /// agregado, que baja la anterior en la misma operación, y un índice único parcial
    /// <c>(tercero_id) WHERE es_preferente</c>, que es lo que queda cuando llegan dos peticiones a
    /// la vez. Sobre si la restricción ve las filas bloqueadas, <b>es la misma decisión del ítem
    /// 1.5</b>: sin predicado de bloqueo. Aquí además no podría ser otra, porque el bloqueo vive en
    /// el tercero y las cuentas que compiten son siempre del mismo.
    /// </remarks>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="cuentaId">Identificador de la cuenta.</param>
    /// <param name="ifMatch">Versión de la FICHA sobre la que se escribe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("cuentas-bancarias/{cuentaId:guid}/preferente")]
    [ExigePermiso(PermisosDeTerceros.CuentaBancariaPreferente)]
    [ProducesResponseType(typeof(CuentaBancariaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> MarcarCuentaPreferente(
        Guid terceroId,
        Guid cuentaId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => marcarPreferente.EjecutarAsync(terceroId, cuentaId, version, cancelacion));

    /// <summary>Quita una cuenta bancaria del tercero.</summary>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="cuentaId">Identificador de la cuenta.</param>
    /// <param name="ifMatch">Versión de la FICHA sobre la que se escribe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("cuentas-bancarias/{cuentaId:guid}")]
    [ExigePermiso(PermisosDeTerceros.CuentaBancariaQuitar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> QuitarCuentaBancaria(
        Guid terceroId,
        Guid cuentaId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancelacion) =>
        ResponderSinContenidoExigiendoVersionAsync(
            ifMatch,
            version => quitarCuenta.EjecutarAsync(terceroId, cuentaId, version, cancelacion));

    /// <summary>Devuelve las condiciones de pago del tercero.</summary>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("condiciones-pago")]
    [ExigePermiso(PermisosDeTerceros.TerceroVer)]
    [ProducesResponseType(typeof(IReadOnlyList<CondicionPagoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarCondicionesPago(
        Guid terceroId,
        CancellationToken cancelacion) =>
        Responder(
            await listarCondiciones.EjecutarAsync(terceroId, cancelacion).ConfigureAwait(false));

    /// <summary>Fija la condición de pago de un rol.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es un <c>PUT</c> y el rol va en la ruta</b>: la condición de un rol es un recurso —hay
    /// una, o no hay ninguna—, así que fijarla dos veces con el mismo cuerpo deja el mismo estado.
    /// Con el rol en el cuerpo, un <c>PUT</c> sobre la del cliente podría cambiar la del proveedor.
    /// </para>
    /// <para>
    /// <b>El plazo son días desde la ENTREGA</b>, y como mucho sesenta (art. 4 de la Ley 3/2004),
    /// que no es un valor por omisión configurable: es un invariante, y un plazo mayor no es una
    /// condición peor, es una cláusula nula. Aquí no se calcula ningún vencimiento: Terceros no ve
    /// entregas.
    /// </para>
    /// </remarks>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="rol">De qué cara es: <c>Cliente</c> o <c>Proveedor</c>.</param>
    /// <param name="ifMatch">Versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">El plazo, el día fijo y el descuento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("condiciones-pago/{rol}")]
    [ExigePermiso(PermisosDeTerceros.CondicionPagoFijar)]
    [ProducesResponseType(typeof(CondicionPagoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> FijarCondicionPago(
        Guid terceroId,
        string rol,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] CondicionPagoDeAltaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => fijarCondicion.EjecutarAsync(
                terceroId, rol, version, peticion, cancelacion));

    /// <summary>Devuelve el límite de crédito del tercero.</summary>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("limite-credito")]
    [ExigePermiso(PermisosDeTerceros.TerceroVer)]
    [ProducesResponseType(typeof(LimiteCreditoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerLimiteCredito(
        Guid terceroId,
        CancellationToken cancelacion) =>
        Responder(await obtenerLimite.EjecutarAsync(terceroId, cancelacion).ConfigureAwait(false));

    /// <summary>Fija —o retira— el límite de crédito del tercero.</summary>
    /// <remarks>
    /// <b>Solo el importe con su divisa.</b> Consumo, disponible y bloqueo por exceso no son de
    /// este ítem: eso necesita ver los pedidos y las facturas pendientes, que es la fase 6.
    /// </remarks>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="ifMatch">Versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">El importe con su divisa, o los dos campos vacíos para retirarlo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("limite-credito")]
    [ExigePermiso(PermisosDeTerceros.LimiteCreditoFijar)]
    [ProducesResponseType(typeof(LimiteCreditoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> FijarLimiteCredito(
        Guid terceroId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] LimiteCreditoDeAltaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => fijarLimite.EjecutarAsync(terceroId, version, peticion, cancelacion));
}
