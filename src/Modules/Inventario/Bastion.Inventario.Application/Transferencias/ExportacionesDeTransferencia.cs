using System.Collections.Frozen;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Transferencias;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>
/// Una transferencia como sale en su exportación, con sus dos almacenes nombrados (ADR-0074).
/// </summary>
/// <param name="Transferencia">La transferencia, tal como la publica el listado.</param>
/// <param name="AlmacenOrigen">El almacén del que sale, de Organización.</param>
/// <param name="AlmacenDestino">El almacén al que llega, de Organización.</param>
public sealed record TransferenciaExportada(
    TransferenciaResumenDto Transferencia,
    MaestroCitado AlmacenOrigen,
    MaestroCitado AlmacenDestino);

/// <summary>
/// Una línea de una transferencia como sale en su exportación, con sus cuatro maestros nombrados y
/// la divisa de su valor.
/// </summary>
/// <param name="Linea">La línea, tal como la publica la ficha.</param>
/// <param name="Divisa">La divisa de la transferencia, que es la de su valor.</param>
/// <param name="UbicacionOrigen">La ubicación de la que sale, de Organización.</param>
/// <param name="UbicacionDestino">La ubicación a la que llega, de Organización.</param>
/// <param name="Articulo">El artículo que se mueve, de Catálogo.</param>
/// <param name="UnidadIntroducida">La unidad en que se tecleó la cantidad, de Organización.</param>
public sealed record LineaDeTransferenciaExportada(
    LineaDeFichaDeTransferenciaDto Linea,
    string Divisa,
    MaestroCitado UbicacionOrigen,
    MaestroCitado UbicacionDestino,
    MaestroCitado Articulo,
    MaestroCitado UnidadIntroducida);

/// <summary>
/// Una página interna de la exportación de transferencias: la del listado, con los almacenes
/// nombrados.
/// </summary>
/// <remarks>
/// El origen y el destino son el mismo maestro, así que van en una sola pregunta por página, y
/// solo con el permiso de ver almacenes (ADR-0074 §3).
/// </remarks>
public interface IExportarTransferencias : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página interna y con qué orden.</param>
    /// <param name="estado">El estado al que se acota, ya validado por el borde, o nulo.</param>
    /// <param name="origenId">El almacén de origen al que se acota, o nulo.</param>
    /// <param name="destinoId">El almacén de destino al que se acota, o nulo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<TransferenciaExportada>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? origenId,
        Guid? destinoId,
        CancellationToken cancelacion);
}

/// <summary>
/// Una página interna de la exportación de las líneas de una transferencia, con sus maestros
/// nombrados.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las líneas no tienen listado</b>: van dentro de la ficha, enteras, que es lo que enseña la
/// pantalla. Así que cada página interna es un tramo de esa lista, en su orden, y el total es su
/// longitud. No deja ordenar, como la ficha, que no lee <c>?sort=</c>.
/// </para>
/// <para>
/// <b>Una transferencia que no existe es el <c>404</c> de la ficha</b>, y sale antes del primer
/// byte.
/// </para>
/// </remarks>
public interface IExportarLineasDeTransferencia : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="transferenciaId">La transferencia.</param>
    /// <param name="paginacion">Qué página interna.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<PaginaDe<LineaDeTransferenciaExportada>>> EjecutarAsync(
        Guid transferenciaId,
        Paginacion paginacion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IExportarTransferencias"/>
internal sealed class ExportarTransferencias(IListarTransferencias listar, LosNombresDeLosMaestros nombres)
    : IExportarTransferencias
{
    public IReadOnlySet<string> CamposOrdenables => listar.CamposOrdenables;

    public async Task<PaginaDe<TransferenciaExportada>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? origenId,
        Guid? destinoId,
        CancellationToken cancelacion)
    {
        PaginaDe<TransferenciaResumenDto> pagina = await listar
            .EjecutarAsync(paginacion, estado, origenId, destinoId, cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deAlmacenes = await nombres
            .DeAlmacenesAsync(
                pagina.Elementos.SelectMany(transferencia =>
                    new Guid?[] { transferencia.AlmacenOrigenId, transferencia.AlmacenDestinoId }),
                cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<TransferenciaExportada>(
            [
                .. pagina.Elementos.Select(transferencia => new TransferenciaExportada(
                    transferencia,
                    MaestroCitado.De(transferencia.AlmacenOrigenId, deAlmacenes),
                    MaestroCitado.De(transferencia.AlmacenDestinoId, deAlmacenes))),
            ],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IExportarLineasDeTransferencia"/>
internal sealed class ExportarLineasDeTransferencia(IObtenerTransferencia obtener, LosNombresDeLosMaestros nombres)
    : IExportarLineasDeTransferencia
{
    public IReadOnlySet<string> CamposOrdenables => FrozenSet<string>.Empty;

    public async Task<Resultado<PaginaDe<LineaDeTransferenciaExportada>>> EjecutarAsync(
        Guid transferenciaId,
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(paginacion);

        Resultado<ConVersion<FichaDeTransferenciaDto>> ficha = await obtener
            .EjecutarAsync(transferenciaId, cancelacion)
            .ConfigureAwait(false);

        if (!ficha.EsCorrecto)
        {
            return Resultado.Fallo<PaginaDe<LineaDeTransferenciaExportada>>(ficha.Error!);
        }

        FichaDeTransferenciaDto transferencia = ficha.Valor.Recurso;

        LineaDeFichaDeTransferenciaDto[] tramo =
            [.. transferencia.Lineas.Skip((paginacion.Pagina - 1) * paginacion.Tamanio).Take(paginacion.Tamanio)];

        IReadOnlyDictionary<Guid, NombreDeMaestro> deUbicaciones = await nombres
            .DeUbicacionesAsync(
                tramo.SelectMany(linea => new Guid?[] { linea.UbicacionOrigenId, linea.UbicacionDestinoId }),
                cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deArticulos = await nombres
            .DeArticulosAsync(tramo.Select(linea => (Guid?)linea.ArticuloId), cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deUnidades = await nombres
            .DeUnidadesAsync(tramo.Select(linea => (Guid?)linea.UnidadIntroducidaId), cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new PaginaDe<LineaDeTransferenciaExportada>(
            [
                .. tramo.Select(linea => new LineaDeTransferenciaExportada(
                    linea,
                    transferencia.Divisa,
                    MaestroCitado.De(linea.UbicacionOrigenId, deUbicaciones),
                    MaestroCitado.De(linea.UbicacionDestinoId, deUbicaciones),
                    MaestroCitado.De(linea.ArticuloId, deArticulos),
                    MaestroCitado.De(linea.UnidadIntroducidaId, deUnidades))),
            ],
            paginacion.Pagina,
            paginacion.Tamanio,
            transferencia.Lineas.Count));
    }
}
