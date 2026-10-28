using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application;
using Bastion.Inventario.Application.Saldos;
using Bastion.Inventario.Contracts;
using Bastion.Inventario.Contracts.Saldos;
using Bastion.Inventario.Endpoints.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints;

/// <summary>El saldo a una fecha, bajo <c>/api/v1/inventario/saldos</c>.</summary>
/// <remarks>
/// <para>
/// <b>Una sola acción, de lectura</b> (ADR-0063 §8): el saldo de cada artículo en cada almacén al
/// final de un día, con lo que viaja hacia él dicho aparte. No es un recurso que se cree ni se
/// modifique; es la suma del libro.
/// </para>
/// <para>
/// <b>La respuesta no es una página a secas</b>: lleva la fecha que contesta al lado de la página,
/// porque sin fecha la pide hoy, y una página vacía no tendría dónde decirlo.
/// </para>
/// </remarks>
/// <param name="consultar">La consulta del saldo.</param>
public sealed class SaldosController(IConsultarSaldos consultar) : ControladorDeInventario
{
    /// <summary>
    /// Las columnas de la exportación: el día que contesta, el artículo y el almacén con su código
    /// y su nombre, y lo que la consulta publica de cada saldo (ADR-0074).
    /// </summary>
    /// <remarks>
    /// <b>El día va en cada fila</b>: la consulta lo publica una vez, al lado de la
    /// página, y un fichero no tiene ese sitio. Es el mismo en todas, porque se fija antes de la
    /// primera página.
    /// </remarks>
    internal static readonly HojaDeExportacion<SaldoExportado> HojaDeSaldos = new(
        "saldos",
        [
            new("fecha", fila => CampoCsv.Fecha(fila.Fecha)),
            .. ColumnasDeMaestro.De<SaldoExportado>("articulo", fila => fila.Articulo),
            .. ColumnasDeMaestro.De<SaldoExportado>("almacen", fila => fila.Almacen),
            new("cantidad", fila => CampoCsv.Numero(fila.Saldo.Cantidad)),
            new("valor", fila => CampoCsv.Numero(fila.Saldo.Valor)),
            new("cantidad_en_transito", fila => CampoCsv.Numero(fila.Saldo.CantidadEnTransito)),
            new("valor_en_transito", fila => CampoCsv.Numero(fila.Saldo.ValorEnTransito)),
            new("divisa", fila => CampoCsv.Texto(fila.Saldo.Divisa)),
        ]);

    /// <summary>Devuelve una página de saldos a una fecha, con la fecha que contesta.</summary>
    /// <remarks>
    /// Sin <c>fecha</c>, hoy en UTC. Una fecha posterior a hoy es un <c>400</c>,
    /// <c>saldo-con-fecha-futura</c>. Una anterior al primer movimiento contesta ceros, y no un error.
    /// </remarks>
    /// <param name="pagina">La página (<c>page</c>, <c>size</c>).</param>
    /// <param name="consulta">
    /// La fecha, el almacén y el artículo (<c>fecha</c>, <c>almacen</c>, <c>articulo</c>). No admite
    /// orden: va por artículo y almacén. Sin filtro de texto: un <c>q</c> con algo escrito es un
    /// <c>400</c>.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet]
    [ExigePermiso(PermisosDeInventario.SaldoVer)]
    [ProducesResponseType(typeof(SaldosAUnaFechaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar(
        [FromQuery] PaginaPedida pagina,
        [FromQuery] ConsultaDeSaldos consulta,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        Resultado<Paginacion> pedido =
            RespuestasDeListado.Componer(this, pagina, consulta, texto: null, consultar);

        if (!pedido.EsCorrecto)
        {
            return pedido.Error!.AResultadoDeAccion();
        }

        return Responder(await consultar
            .EjecutarAsync(consulta.Fecha, consulta.Almacen, consulta.Articulo, pedido.Valor, cancelacion)
            .ConfigureAwait(false));
    }

    /// <summary>
    /// Descarga los saldos a una fecha enteros, con su almacén y su artículo, en CSV (ADR-0073).
    /// </summary>
    /// <remarks>
    /// <b>El día se fija aquí, una vez</b>: sin <c>fecha</c> es hoy, y una exportación que cruzara
    /// la medianoche no puede escribir dos. Una fecha futura es el <c>400</c> de la consulta, antes
    /// del primer byte.
    /// </remarks>
    /// <param name="consulta">
    /// La fecha, el almacén y el artículo (<c>fecha</c>, <c>almacen</c>, <c>articulo</c>). Sin
    /// orden ni filtro de texto, como la consulta.
    /// </param>
    /// <param name="exportar">El caso de uso de la consulta, con los maestros nombrados.</param>
    /// <param name="foto">La foto del módulo, de la que salen todas las páginas.</param>
    /// <param name="reloj">El reloj, que da el día del nombre del fichero.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpGet("exportacion")]
    [ExigePermiso(PermisosDeInventario.SaldoVer)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/csv")]
    public async Task<IActionResult> Exportar(
        [FromQuery] ConsultaDeSaldos consulta,
        [FromServices] IExportarSaldos exportar,
        [FromServices] IFotoDeInventario foto,
        [FromServices] TimeProvider reloj,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(exportar);

        DateOnly dia = exportar.DiaDe(consulta.Fecha);

        return await ExportarListadoDeResultadoAsync(
            consulta,
            texto: null,
            exportar,
            (paginacion, testigo) => exportar.EjecutarAsync(
                dia, consulta.Almacen, consulta.Articulo, paginacion, testigo),
            foto,
            HojaDeSaldos,
            reloj,
            cancelacion).ConfigureAwait(false);
    }
}
