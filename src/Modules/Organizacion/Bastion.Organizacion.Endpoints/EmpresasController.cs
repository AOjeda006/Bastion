using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Organizacion.Endpoints;

/// <summary>
/// Empresas, bajo <c>/api/v1/organizacion/empresas</c>.
/// </summary>
/// <remarks>
/// El controlador no tiene lógica: enlaza, llama al caso de uso y traduce el desenlace. Cada
/// operación es un tipo distinto inyectado por separado (§3), de modo que lo que esta clase puede
/// hacer se lee en su constructor.
/// </remarks>
public sealed class EmpresasController(
    ICrearEmpresa crear,
    IObtenerEmpresa obtener,
    IListarEmpresas listar,
    IBuscarEmpresas buscar,
    IModificarEmpresa modificar,
    IBloquearEmpresa bloquear,
    IDesbloquearEmpresa desbloquear) : ControladorDeOrganizacion
{
    /// <summary>
    /// Las columnas de la exportación: lo que el listado publica de cada empresa, sin su
    /// identificador, y el domicilio en sus seis campos.
    /// </summary>
    /// <remarks>
    /// El NIF sale porque el listado lo enseña; lo que no hace es filtrar ni ordenar por él, porque
    /// iría en la URL (ADR-0025), y la exportación tiene los mismos criterios que el listado. El
    /// régimen va con el valor del DTO, sin traducir (*La puerta del 3.2*).
    /// </remarks>
    internal static readonly HojaDeExportacion<EmpresaDto> HojaDeEmpresas = new(
        "empresas",
        [
            new("nif", empresa => CampoCsv.Texto(empresa.Nif)),
            new("razon_social", empresa => CampoCsv.Texto(empresa.RazonSocial)),
            .. ColumnasDeDireccion.De<EmpresaDto>("domicilio", empresa => empresa.DomicilioFiscal),
            new("divisa_base", empresa => CampoCsv.Valor(empresa.DivisaBase)),
            new("regimen_de_iva", empresa => CampoCsv.Valor(empresa.RegimenDeIva)),
        ]);

    /// <summary>Devuelve una página de empresas.</summary>
    /// <param name="pagina">La página (<c>page</c>, <c>size</c>).</param>
    /// <param name="consulta">El orden (<c>sort</c>).</param>
    /// <param name="texto">El filtro de texto (<c>q</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeOrganizacion.EmpresaVer)]
    [ProducesResponseType(typeof(PaginaDe<EmpresaDto>), StatusCodes.Status200OK)]
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

    /// <summary>Descarga el listado de empresas entero, con su orden y su texto, en CSV (ADR-0073).</summary>
    /// <param name="consulta">El orden (<c>sort</c>).</param>
    /// <param name="texto">El filtro de texto (<c>q</c>).</param>
    /// <param name="foto">La foto del módulo, de la que salen todas las páginas.</param>
    /// <param name="reloj">El reloj, que da el día del nombre del fichero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("exportacion")]
    [ExigePermiso(PermisosDeOrganizacion.EmpresaVer)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/csv")]
    public async Task<IActionResult> Exportar(
        [FromQuery] ConsultaDeListado consulta,
        [FromQuery] FiltroDeTexto texto,
        [FromServices] IFotoDeOrganizacion foto,
        [FromServices] TimeProvider reloj,
        CancellationToken cancelacion) =>
        await ExportarListadoAsync(consulta, texto, listar, foto, HojaDeEmpresas, reloj, cancelacion)
            .ConfigureAwait(false);

    /// <summary>Busca empresas por un criterio que no puede ir en la URL.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es un <c>POST</c> que no crea nada</b>, y choca con la lectura ingenua de REST. Es el
    /// precio, se paga a sabiendas y está argumentado en el ADR-0025: el primer criterio que
    /// alguien quiere sobre una empresa es el NIF, y un NIF en la cadena de consulta acaba en el
    /// historial del navegador, en el enlace que se copia, en el <c>Referer</c> y en el registro
    /// de acceso del servidor de delante. El listado sin criterio sigue siendo un <c>GET</c>.
    /// </para>
    /// <para>
    /// <b>La respuesta no lleva enlace a lo siguiente</b>, lleva cursor. Un enlace lo compondría
    /// el servidor con el criterio dentro, y habría devuelto el NIF a una URL él solo.
    /// </para>
    /// </remarks>
    /// <param name="peticion">Criterio y por dónde seguir.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("buscar")]
    [ExigePermiso(PermisosDeOrganizacion.EmpresaVer)]
    [ProducesResponseType(typeof(TramoDe<EmpresaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar(
        [FromBody] BuscarEmpresasDto peticion,
        CancellationToken cancelacion) =>
        Responder(await buscar.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Devuelve una empresa.</summary>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.EmpresaVer)]
    [ProducesResponseType(typeof(EmpresaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta una empresa.</summary>
    /// <param name="peticion">Datos de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeOrganizacion.EmpresaCrear)]
    [ProducesResponseType(typeof(EmpresaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearEmpresaDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            empresa => empresa.Id);

    /// <summary>Cambia los datos de una empresa.</summary>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.EmpresaModificar)]
    [ProducesResponseType(typeof(EmpresaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarEmpresaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>
    /// Bloquea una empresa (R16). No la borra.
    /// </summary>
    /// <remarks>
    /// El verbo es <c>DELETE</c> porque es lo que el cliente quiere decir —«quítame esto de en
    /// medio»— y lo que se hace por debajo es bloquear: una empresa puede ser un empresario
    /// individual, y el art. 32 de la LOPDGDD manda bloquear, no destruir.
    /// </remarks>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ExigePermiso(PermisosDeOrganizacion.EmpresaBloquear)]
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

    /// <summary>Devuelve una empresa bloqueada a la actividad.</summary>
    /// <remarks>
    /// <para>
    /// <b>No existe el <c>DELETE</c> inverso, así que es un <c>POST</c> a un sub-recurso.</b> El
    /// estado de la ficha no es un recurso que se sustituya con <c>PUT</c>: es el desenlace de una
    /// operación con nombre propio, y por eso tiene permiso propio.
    /// </para>
    /// <para>
    /// En el 0.4 esta operación existía en el dominio y no tenía puerta HTTP, porque abrirla sin
    /// autorización habría dejado a cualquiera revirtiendo bloqueos del art. 32. Con el permiso
    /// detrás, ese motivo desaparece.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>Es la única escritura del sistema sin <c>If-Match</c>.</b> No es un descuido ni una
    /// excepción de comodidad: desde el 0.10 un recurso bloqueado no se lee por ningún camino
    /// ordinario, y el <c>ETag</c> se obtiene leyendo. Exigir aquí una versión sería exigir una
    /// llave que no se puede conseguir, y dejaría la puerta cerrada para siempre. Lo que el
    /// <c>If-Match</c> evita —pisar el cambio de otro— aquí no puede pasar: mientras el recurso
    /// está bloqueado ninguna otra escritura llega hasta él, y desbloquear dos veces deja el mismo
    /// resultado que desbloquear una (ADR-0017).
    /// </remarks>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{id:guid}/desbloqueo")]
    [ExigePermiso(PermisosDeOrganizacion.EmpresaDesbloquear)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desbloquear(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await desbloquear.EjecutarAsync(id, cancelacion).ConfigureAwait(false));
}
