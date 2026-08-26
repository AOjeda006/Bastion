using Bastion.Organizacion.Application.Empresas;
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
    IModificarEmpresa modificar,
    IBloquearEmpresa bloquear) : ControladorDeOrganizacion
{
    /// <summary>Devuelve una página de empresas.</summary>
    /// <param name="consulta">Paginación pedida (<c>page</c> y <c>size</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDe<EmpresaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return Ok(await listar.EjecutarAsync(consulta.APaginacion(), cancelacion).ConfigureAwait(false));
    }

    /// <summary>Devuelve una empresa.</summary>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmpresaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        Responder(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Da de alta una empresa.</summary>
    /// <param name="peticion">Datos de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
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
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmpresaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Modificar(
        Guid id,
        [FromBody] ModificarEmpresaDto peticion,
        CancellationToken cancelacion) =>
        Responder(await modificar.EjecutarAsync(id, peticion, cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Bloquea una empresa (R16). No la borra.
    /// </summary>
    /// <remarks>
    /// El verbo es <c>DELETE</c> porque es lo que el cliente quiere decir —«quítame esto de en
    /// medio»— y lo que se hace por debajo es bloquear: una empresa puede ser un empresario
    /// individual, y el art. 32 de la LOPDGDD manda bloquear, no destruir.
    /// </remarks>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Bloquear(Guid id, CancellationToken cancelacion) =>
        ResponderSinContenido(await bloquear.EjecutarAsync(id, cancelacion).ConfigureAwait(false));
}
