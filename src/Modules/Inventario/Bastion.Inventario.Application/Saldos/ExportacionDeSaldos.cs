using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Saldos;

namespace Bastion.Inventario.Application.Saldos;

/// <summary>
/// Un saldo como sale en su exportación: el día que contesta, el saldo, y su artículo y su almacén
/// nombrados (ADR-0074).
/// </summary>
/// <param name="Fecha">El día cuyo final contesta, el mismo en todas las filas del fichero.</param>
/// <param name="Saldo">El saldo, tal como lo publica la consulta.</param>
/// <param name="Articulo">El artículo, de Catálogo.</param>
/// <param name="Almacen">El almacén, de Organización.</param>
public sealed record SaldoExportado(DateOnly Fecha, SaldoDto Saldo, MaestroCitado Articulo, MaestroCitado Almacen);

/// <summary>
/// Una página interna de la exportación de los saldos a una fecha, con sus maestros nombrados.
/// </summary>
/// <remarks>
/// <para>
/// <b>El día se fija una vez, antes de la primera página</b>, con <see cref="DiaDe"/>, y cada
/// página lo recibe ya resuelto. Sin fecha, el día es hoy, y una exportación que cruzara la
/// medianoche UTC escribiría las primeras páginas de un día y las últimas del siguiente, con dos
/// fechas en la misma columna y unos saldos que no cuadran entre sí. Por eso aquí la fecha no es
/// opcional.
/// </para>
/// <para>
/// <b>Una fecha futura es el <c>400</c> de la consulta</b>, y sale antes del primer byte.
/// </para>
/// </remarks>
public interface IExportarSaldos : IOrdenaPor
{
    /// <summary>El día que contesta la exportación: el pedido, u hoy si no se pide.</summary>
    /// <param name="fecha">El día pedido, o nulo.</param>
    DateOnly DiaDe(DateOnly? fecha);

    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="fecha">El día, ya fijado con <see cref="DiaDe"/>.</param>
    /// <param name="almacenId">El almacén al que se acota, o nulo para todos.</param>
    /// <param name="articuloId">El artículo al que se acota, o nulo para todos.</param>
    /// <param name="paginacion">Qué página interna.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<PaginaDe<SaldoExportado>>> EjecutarAsync(
        DateOnly fecha,
        Guid? almacenId,
        Guid? articuloId,
        Paginacion paginacion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IExportarSaldos"/>
internal sealed class ExportarSaldos(IConsultarSaldos consultar, LosNombresDeLosMaestros nombres, TimeProvider reloj)
    : IExportarSaldos
{
    public IReadOnlySet<string> CamposOrdenables => consultar.CamposOrdenables;

    public DateOnly DiaDe(DateOnly? fecha) => fecha ?? DiaDeNegocio.Hoy(reloj);

    public async Task<Resultado<PaginaDe<SaldoExportado>>> EjecutarAsync(
        DateOnly fecha,
        Guid? almacenId,
        Guid? articuloId,
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        Resultado<SaldosAUnaFechaDto> consulta = await consultar
            .EjecutarAsync(fecha, almacenId, articuloId, paginacion, cancelacion)
            .ConfigureAwait(false);

        if (!consulta.EsCorrecto)
        {
            return Resultado.Fallo<PaginaDe<SaldoExportado>>(consulta.Error!);
        }

        PaginaDe<SaldoDto> pagina = consulta.Valor.Saldos;

        IReadOnlyDictionary<Guid, NombreDeMaestro> deArticulos = await nombres
            .DeArticulosAsync(pagina.Elementos.Select(saldo => (Guid?)saldo.ArticuloId), cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deAlmacenes = await nombres
            .DeAlmacenesAsync(pagina.Elementos.Select(saldo => (Guid?)saldo.AlmacenId), cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new PaginaDe<SaldoExportado>(
            [
                .. pagina.Elementos.Select(saldo => new SaldoExportado(
                    consulta.Valor.Fecha,
                    saldo,
                    MaestroCitado.De(saldo.ArticuloId, deArticulos),
                    MaestroCitado.De(saldo.AlmacenId, deAlmacenes))),
            ],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total));
    }
}
