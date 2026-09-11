using System.ComponentModel.DataAnnotations;
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

/// <summary>Tarifas y sus líneas, bajo <c>/api/v1/catalogo/tarifas</c>.</summary>
/// <remarks>
/// <b>Las líneas cuelgan de la tarifa para listarlas y crearlas, y NO para leerlas una a una.</b>
/// Crear y listar van bajo <c>/tarifas/{tarifaId}/lineas</c> porque la tarifa es de verdad parte de
/// la pregunta —«qué líneas tiene ésta», «añade una a ésta»—, y el caso de uso la comprueba. Leer o
/// modificar una línea concreta va bajo <c>/tarifas/lineas/{id}</c>, en plano, porque una línea se
/// identifica sola: una ruta anidada que llevara la tarifa sin comprobar que es la suya sería una
/// ruta que puede mentir, y comprobarla solo para poder escribirla sería trabajo para sostener una
/// forma.
/// </remarks>
public sealed class TarifasController(
    ICrearTarifa crear,
    IObtenerTarifa obtener,
    IListarTarifas listar,
    IModificarTarifa modificar,
    ICerrarTarifa cerrar,
    ICrearLineaTarifa crearLinea,
    IObtenerLineaTarifa obtenerLinea,
    IListarLineasDeTarifa listarLineas,
    IModificarLineaTarifa modificarLinea,
    IResolverPrecio resolver) : ControladorDeCatalogo
{
    /// <summary>Devuelve una página de tramos de tarifa.</summary>
    /// <param name="consulta">
    /// Paginación, orden, filtro y código (<c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c>,
    /// <c>codigo</c>). El filtro de texto mira el código y el nombre.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeCatalogo.TarifaVer)]
    [ProducesResponseType(typeof(PaginaDe<TarifaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultaDeTarifas consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoAsync(
            consulta,
            listar,
            (paginacion, testigo) => listar.EjecutarAsync(paginacion, consulta.Codigo, testigo),
            cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve un tramo de tarifa.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.TarifaVer)]
    [ProducesResponseType(typeof(TarifaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(await obtener.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Abre un tramo de tarifa.</summary>
    /// <remarks>
    /// El <c>409</c> puede venir de dos sitios y el <c>type</c> los separa: la divisa está retirada
    /// (<c>tarifa-divisa-retirada</c>) o la vigencia se pisa con la de otro tramo del mismo código
    /// (<c>tarifa-vigencias-solapadas</c>). El segundo lo contesta el caso de uso preguntando antes,
    /// pero quien de verdad lo impide es una restricción de exclusión de la base: es la única que
    /// cubre dos peticiones a la vez.
    /// </remarks>
    /// <param name="peticion">Datos del tramo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeCatalogo.TarifaCrear)]
    [ProducesResponseType(typeof(TarifaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearTarifaDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crear.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false),
            nameof(Obtener),
            tarifa => tarifa.Id);

    /// <summary>Cambia el nombre de un tramo de tarifa.</summary>
    /// <remarks>
    /// Ni el código, ni la divisa, ni la vigencia: no están en el cuerpo ni en
    /// <c>Tarifa.Modificar</c>. Subir precios es cerrar este tramo y abrir el siguiente.
    /// </remarks>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.TarifaModificar)]
    [ProducesResponseType(typeof(TarifaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Modificar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarTarifaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>Cierra un tramo de tarifa el día indicado.</summary>
    /// <remarks>
    /// Es <c>PUT</c> sobre un subrecurso y no un <c>PATCH</c> de la tarifa: cerrar es la operación
    /// con la que una tarifa da paso a la siguiente, y tiene su propio cuerpo de un solo campo.
    /// Exige <c>If-Match</c> como cualquier otra escritura sobre algo que ya existe.
    /// </remarks>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">El último día en que rige.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("{id:guid}/cierre")]
    [ExigePermiso(PermisosDeCatalogo.TarifaCerrar)]
    [ProducesResponseType(typeof(TarifaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> Cerrar(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] CerrarTarifaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => cerrar.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>Devuelve una página de líneas de un tramo de tarifa.</summary>
    /// <param name="tarifaId">Tramo cuyas líneas se piden.</param>
    /// <param name="consulta">Paginación y orden (<c>page</c>, <c>size</c>, <c>sort</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{tarifaId:guid}/lineas")]
    [ExigePermiso(PermisosDeCatalogo.TarifaVer)]
    [ProducesResponseType(typeof(PaginaDe<LineaTarifaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarLineas(
        Guid tarifaId,
        [FromQuery] ConsultaPaginada consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return await ResponderListadoDeResultadoAsync(
            consulta,
            listarLineas,
            (paginacion, testigo) => listarLineas.EjecutarAsync(paginacion, tarifaId, testigo),
            cancelacion).ConfigureAwait(false);
    }

    /// <summary>Devuelve una línea de tarifa.</summary>
    /// <param name="id">Identificador de la línea.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("lineas/{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.TarifaVer)]
    [ProducesResponseType(typeof(LineaTarifaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerLinea(Guid id, CancellationToken cancelacion) =>
        ResponderConVersion(
            await obtenerLinea.EjecutarAsync(id, cancelacion).ConfigureAwait(false));

    /// <summary>Añade una línea a un tramo de tarifa.</summary>
    /// <remarks>
    /// El <c>400</c> tiene cuatro <c>type</c> distintos y cada uno se arregla de otra manera: la
    /// línea trae los dos destinos o ninguno (<c>tarifa-linea-articulo-o-categoria</c>), trae precio
    /// y descuento o ninguno de los dos (<c>tarifa-linea-precio-o-descuento</c>), o es el primer
    /// tramo de su destino y no empieza en cero (<c>tarifa-linea-primer-tramo-sin-cero</c>). El
    /// <c>409</c> es el tramo repetido (<c>tarifa-linea-tramo-duplicado</c>).
    /// </remarks>
    /// <param name="tarifaId">Tramo al que se le añade la línea.</param>
    /// <param name="peticion">Datos de la línea.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("{tarifaId:guid}/lineas")]
    [AdmiteIdempotencia]
    [ExigePermiso(PermisosDeCatalogo.LineaTarifaAgregar)]
    [ProducesResponseType(typeof(LineaTarifaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearLinea(
        Guid tarifaId,
        [FromBody] CrearLineaTarifaDto peticion,
        CancellationToken cancelacion) =>
        ResponderCreado(
            await crearLinea.EjecutarAsync(tarifaId, peticion, cancelacion).ConfigureAwait(false),
            nameof(ObtenerLinea),
            linea => linea.Id);

    /// <summary>Cambia el precio o el descuento de una línea.</summary>
    /// <remarks>
    /// Ni el destino ni la cantidad desde la que se aplica: no están en el cuerpo. Que la cantidad
    /// no se pueda mover es lo que hace imposible abrir un hueco en una tabla de precios ya escrita.
    /// </remarks>
    /// <param name="id">Identificador de la línea.</param>
    /// <param name="ifMatch">Versión sobre la que se escribe, tal como la devolvió el ETag.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("lineas/{id:guid}")]
    [ExigePermiso(PermisosDeCatalogo.LineaTarifaModificar)]
    [ProducesResponseType(typeof(LineaTarifaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public Task<IActionResult> ModificarLinea(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ModificarLineaTarifaDto peticion,
        CancellationToken cancelacion) =>
        ResponderExigiendoVersionAsync(
            ifMatch,
            version => modificarLinea.EjecutarAsync(id, version, peticion, cancelacion));

    /// <summary>Dice qué precio le pone esta tarifa a un artículo para una cantidad y una fecha.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es el extremo caliente, y sus tres fallos son tres <c>type</c> distintos porque son tres
    /// arreglos distintos</b>: el código no existe (<c>tarifa-no-encontrada</c>, y hay que corregir
    /// el código), existe y ninguno de sus tramos cubre esa fecha (<c>tarifa-no-vigente</c>, y hay
    /// que abrir el tramo que falta) o la tarifa rige pero no dice nada de ese artículo
    /// (<c>tarifa-sin-linea-aplicable</c>, y hay que poner la línea). <b>Ninguno de los tres es un
    /// cero</b>: un precio cero que nadie ha escrito entra en un documento, suma cero al total y el
    /// descuadre aparece semanas después sin autor.
    /// </para>
    /// <para>
    /// La respuesta lleva la <b>divisa</b> de la tarifa —que puede no ser la de la empresa, y se
    /// acepta a propósito— y el <b>origen</b> del precio: si ha ganado una línea del artículo o de
    /// una categoría, cuál, y a cuántos saltos del artículo estaba. Eso último es lo que permite
    /// afirmar desde fuera que la precedencia elige el antepasado más cercano y no el más profundo.
    /// </para>
    /// </remarks>
    /// <param name="codigo">Código de la tarifa.</param>
    /// <param name="articulo">Artículo al que se le quiere poner precio.</param>
    /// <param name="cantidad">Cantidad por la que se pregunta.</param>
    /// <param name="fecha">Día para el que se resuelve. Si no se dice, hoy.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("{codigo}/precio")]
    [ExigePermiso(PermisosDeCatalogo.TarifaVer)]
    [ProducesResponseType(typeof(PrecioResueltoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Precio(
        string codigo,
        [FromQuery] Guid articulo,

        // El tope por abajo es cero y se pone aquí, en el borde: por debajo de cero no hay tramo
        // —el primero de cada destino empieza en cero— y la resolución contestaría «sin línea
        // aplicable», que sería un mensaje verdadero contestando a una pregunta que no tiene
        // sentido. Vale más decir que la cantidad no puede ser negativa.
        [FromQuery]
        [Range(typeof(decimal), "0", "79228162514264337593543950335",
            ParseLimitsInInvariantCulture = true,
            ErrorMessage = "La cantidad por la que se pregunta no puede ser negativa.")]
        decimal cantidad,
        [FromQuery] DateOnly? fecha,
        CancellationToken cancelacion) =>
        Responder(await resolver
            .EjecutarAsync(codigo, articulo, cantidad, fecha, cancelacion)
            .ConfigureAwait(false));
}
