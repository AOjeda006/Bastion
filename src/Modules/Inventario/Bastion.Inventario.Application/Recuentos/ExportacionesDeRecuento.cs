using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Un recuento como sale en su exportación, con su almacén nombrado (ADR-0074).</summary>
/// <param name="Recuento">El recuento, tal como lo publica el listado.</param>
/// <param name="Almacen">El almacén que se cuenta, de Organización.</param>
public sealed record RecuentoExportado(RecuentoResumenDto Recuento, MaestroCitado Almacen);

/// <summary>
/// Una línea de un recuento como sale en su exportación, con sus tres maestros nombrados.
/// </summary>
/// <param name="Linea">La línea, tal como la publica el listado.</param>
/// <param name="Ubicacion">La ubicación que se cuenta, de Organización.</param>
/// <param name="Articulo">El artículo que se cuenta, de Catálogo.</param>
/// <param name="UnidadBase">La unidad base del artículo, de Organización.</param>
public sealed record LineaDeRecuentoExportada(
    LineaDeRecuentoDto Linea,
    MaestroCitado Ubicacion,
    MaestroCitado Articulo,
    MaestroCitado UnidadBase);

/// <summary>
/// Una página interna de la exportación de recuentos: la del listado, con el almacén nombrado.
/// </summary>
/// <remarks>
/// Llama al caso de uso del listado, como la de ajustes, y el almacén se nombra solo con el
/// permiso de verlo (ADR-0074 §3).
/// </remarks>
public interface IExportarRecuentos : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página interna, con qué orden y qué filtro.</param>
    /// <param name="estado">El estado al que se acota, ya validado por el borde, o nulo.</param>
    /// <param name="almacenId">El almacén al que se acota, o nulo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<RecuentoExportado>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? almacenId,
        CancellationToken cancelacion);
}

/// <summary>
/// Una página interna de la exportación de las líneas de un recuento, con sus maestros nombrados.
/// </summary>
/// <remarks>
/// <b>Un recuento que no existe es el <c>404</c> del listado</b>, y sale antes del primer byte.
/// </remarks>
public interface IExportarLineasDeRecuento : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página interna, con qué orden y qué filtro.</param>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="solo">Las líneas a las que se acota, ya validado, o nulo para todas.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<PaginaDe<LineaDeRecuentoExportada>>> EjecutarAsync(
        Paginacion paginacion,
        Guid recuentoId,
        string? solo,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IExportarRecuentos"/>
internal sealed class ExportarRecuentos(IListarRecuentos listar, LosNombresDeLosMaestros nombres)
    : IExportarRecuentos
{
    public IReadOnlySet<string> CamposOrdenables => listar.CamposOrdenables;

    public async Task<PaginaDe<RecuentoExportado>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? almacenId,
        CancellationToken cancelacion)
    {
        PaginaDe<RecuentoResumenDto> pagina = await listar
            .EjecutarAsync(paginacion, estado, almacenId, cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deAlmacenes = await nombres
            .DeAlmacenesAsync(pagina.Elementos.Select(recuento => (Guid?)recuento.AlmacenId), cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<RecuentoExportado>(
            [
                .. pagina.Elementos.Select(recuento =>
                    new RecuentoExportado(recuento, MaestroCitado.De(recuento.AlmacenId, deAlmacenes))),
            ],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IExportarLineasDeRecuento"/>
internal sealed class ExportarLineasDeRecuento(IListarLineasDeRecuento listar, LosNombresDeLosMaestros nombres)
    : IExportarLineasDeRecuento
{
    public IReadOnlySet<string> CamposOrdenables => listar.CamposOrdenables;

    public async Task<Resultado<PaginaDe<LineaDeRecuentoExportada>>> EjecutarAsync(
        Paginacion paginacion,
        Guid recuentoId,
        string? solo,
        CancellationToken cancelacion)
    {
        Resultado<PaginaDe<LineaDeRecuentoDto>> listado = await listar
            .EjecutarAsync(paginacion, recuentoId, solo, cancelacion)
            .ConfigureAwait(false);

        if (!listado.EsCorrecto)
        {
            return Resultado.Fallo<PaginaDe<LineaDeRecuentoExportada>>(listado.Error!);
        }

        PaginaDe<LineaDeRecuentoDto> pagina = listado.Valor;

        IReadOnlyDictionary<Guid, NombreDeMaestro> deUbicaciones = await nombres
            .DeUbicacionesAsync(pagina.Elementos.Select(linea => (Guid?)linea.UbicacionId), cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deArticulos = await nombres
            .DeArticulosAsync(pagina.Elementos.Select(linea => (Guid?)linea.ArticuloId), cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deUnidades = await nombres
            .DeUnidadesAsync(pagina.Elementos.Select(linea => (Guid?)linea.UnidadBaseId), cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new PaginaDe<LineaDeRecuentoExportada>(
            [
                .. pagina.Elementos.Select(linea => new LineaDeRecuentoExportada(
                    linea,
                    MaestroCitado.De(linea.UbicacionId, deUbicaciones),
                    MaestroCitado.De(linea.ArticuloId, deArticulos),
                    MaestroCitado.De(linea.UnidadBaseId, deUnidades))),
            ],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total));
    }
}
