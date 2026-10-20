using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Transferencias;

namespace Bastion.Inventario.Application.Transferencias;

/// <summary>Los valores que admite el criterio propio del listado de transferencias.</summary>
/// <remarks>
/// <b>Una constante, para que el borde valide con el mismo texto</b>, como la del ajuste: el patrón se
/// compone con el nombre de cada estado, así que renombrar uno no deja el borde validando el viejo.
/// </remarks>
public static class LosFiltrosDeLaTransferencia
{
    /// <summary>Lo que admite el criterio del estado: el nombre de cada estado, tal como se publica.</summary>
    public const string PatronDelEstado =
        "^(" + nameof(EstadoDeTransferencia.Borrador)
        + "|" + nameof(EstadoDeTransferencia.Enviada)
        + "|" + nameof(EstadoDeTransferencia.Recibida)
        + "|" + nameof(EstadoDeTransferencia.Anulada) + ")$";
}

/// <summary>Devuelve la ficha de una transferencia, con sus líneas y su versión.</summary>
public interface IObtenerTransferencia
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la transferencia.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La ficha y su versión, o <c>404</c>.</returns>
    Task<Resultado<ConVersion<FichaDeTransferenciaDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>
/// Devuelve una página de transferencias, opcionalmente acotada a un estado, a un almacén de origen y
/// a uno de destino.
/// </summary>
public interface IListarTransferencias : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño y con qué orden.</param>
    /// <param name="estado">El estado al que se acota, ya validado por el borde, o nulo.</param>
    /// <param name="origenId">El almacén de origen al que se acota, o nulo.</param>
    /// <param name="destinoId">El almacén de destino al que se acota, o nulo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La página.</returns>
    Task<PaginaDe<TransferenciaResumenDto>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? origenId,
        Guid? destinoId,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerTransferencia"/>
/// <remarks>
/// <b>La versión es la que pide el descarte</b> en su <c>If-Match</c> (ADR-0068 §5). Una descartada no
/// se encuentra: la esconde el filtro del contexto, y la ficha contesta <c>404</c>.
/// </remarks>
/// <param name="transferencias">El documento, con sus líneas.</param>
/// <param name="versiones">La versión del documento, que es su <c>ETag</c>.</param>
internal sealed class ObtenerTransferencia(
    IRepositorioDeTransferencias transferencias,
    IVersionesDeInventario versiones) : IObtenerTransferencia
{
    public async Task<Resultado<ConVersion<FichaDeTransferenciaDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Transferencia? transferencia = await transferencias.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return transferencia is null
            ? Resultado.Fallo<ConVersion<FichaDeTransferenciaDto>>(ErroresDeTransferencia.NoEncontrada(id))
            : Resultado.Correcto(new ConVersion<FichaDeTransferenciaDto>(
                transferencia.AFicha(), versiones.De(transferencia)));
    }
}

/// <inheritdoc cref="IListarTransferencias"/>
/// <param name="transferencias">Los documentos.</param>
internal sealed class ListarTransferencias(IRepositorioDeTransferencias transferencias) : IListarTransferencias
{
    public IReadOnlySet<string> CamposOrdenables => transferencias.CamposOrdenables;

    public async Task<PaginaDe<TransferenciaResumenDto>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? origenId,
        Guid? destinoId,
        CancellationToken cancelacion)
    {
        EstadoDeTransferencia? acotado = estado is null ? null : Enum.Parse<EstadoDeTransferencia>(estado);

        PaginaDe<Transferencia> pagina = await transferencias
            .ListarAsync(paginacion, acotado, origenId, destinoId, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<TransferenciaResumenDto>(
            [.. pagina.Elementos.Select(transferencia => transferencia.AResumen())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}
