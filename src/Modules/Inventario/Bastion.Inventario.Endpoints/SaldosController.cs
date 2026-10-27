using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Bastion.BuildingBlocks.Infrastructure.Listados;
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
}
