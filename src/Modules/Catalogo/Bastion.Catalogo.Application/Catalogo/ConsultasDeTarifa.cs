using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Devuelve un tramo de tarifa por su identificador.</summary>
public interface IObtenerTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<TarifaDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>
/// Devuelve una página de tramos de tarifa, opcionalmente acotada a un código.
/// </summary>
/// <remarks>
/// <b>El acotado por código va aparte del <c>?q=</c></b>, por lo mismo que el de categoría en el
/// listado de artículos: «los que digan esto» y «los de esta tarifa» son dos preguntas, y aquí la
/// segunda es la que de verdad se hace — un código de tarifa tiene varios tramos y verlos juntos,
/// en orden, es cómo se comprueba que la sucesión no ha dejado ningún día sin cubrir.
/// </remarks>
public interface IListarTarifas : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y qué filtro.</param>
    /// <param name="codigo">Código por el que se acota, o nulo para no acotar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<TarifaDto>> EjecutarAsync(
        Paginacion paginacion,
        string? codigo,
        CancellationToken cancelacion);
}

/// <summary>Devuelve una línea de tarifa por su identificador.</summary>
public interface IObtenerLineaTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la línea.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<LineaTarifaDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion);
}

/// <summary>Devuelve una página de líneas de un tramo de tarifa.</summary>
public interface IListarLineasDeTarifa : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y qué filtro.</param>
    /// <param name="tarifaId">Tramo cuyas líneas se piden.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<PaginaDe<LineaTarifaDto>>> EjecutarAsync(
        Paginacion paginacion,
        Guid tarifaId,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerTarifa"/>
internal sealed class ObtenerTarifa(
    IRepositorioDeTarifas tarifas,
    IVersionesDeCatalogo versiones) : IObtenerTarifa
{
    public async Task<Resultado<ConVersion<TarifaDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        Tarifa? tarifa = await tarifas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return tarifa is null
            ? Resultado.Fallo<ConVersion<TarifaDto>>(ErroresDeTarifa.NoEncontrada(id))
            : Resultado.Correcto(new ConVersion<TarifaDto>(tarifa.ADto(), versiones.De(tarifa)));
    }
}

/// <inheritdoc cref="IListarTarifas"/>
internal sealed class ListarTarifas(IRepositorioDeTarifas tarifas) : IListarTarifas
{
    public IReadOnlySet<string> CamposOrdenables => tarifas.CamposOrdenables;

    public async Task<PaginaDe<TarifaDto>> EjecutarAsync(
        Paginacion paginacion,
        string? codigo,
        CancellationToken cancelacion)
    {
        // Normalizado, que es la forma sobre la que está guardado: acotar por lo que escribió el
        // usuario dejaría fuera sus propias tarifas por haberlas tecleado en minúscula.
        string? acotado = string.IsNullOrWhiteSpace(codigo) ? null : Tarifa.NormalizarCodigo(codigo);

        PaginaDe<Tarifa> pagina = await tarifas
            .ListarAsync(paginacion, acotado, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<TarifaDto>(
            [.. pagina.Elementos.Select(tarifa => tarifa.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IObtenerLineaTarifa"/>
internal sealed class ObtenerLineaTarifa(
    IRepositorioDeLineasDeTarifa lineas,
    IVersionesDeCatalogo versiones) : IObtenerLineaTarifa
{
    public async Task<Resultado<ConVersion<LineaTarifaDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        LineaTarifa? linea = await lineas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return linea is null
            ? Resultado.Fallo<ConVersion<LineaTarifaDto>>(ErroresDeTarifa.LineaNoEncontrada(id))
            : Resultado.Correcto(new ConVersion<LineaTarifaDto>(linea.ADto(), versiones.De(linea)));
    }
}

/// <inheritdoc cref="IListarLineasDeTarifa"/>
/// <remarks>
/// <b>Comprueba que el tramo existe antes de listar, y por eso devuelve <c>Resultado</c>.</b> Sin
/// esa comprobación, pedir las líneas de un identificador inventado devolvería una página vacía con
/// un <c>200</c>: indistinguible de una tarifa recién abierta y todavía sin líneas, que es
/// exactamente la confusión que hace perder una tarde.
/// </remarks>
internal sealed class ListarLineasDeTarifa(
    IRepositorioDeTarifas tarifas,
    IRepositorioDeLineasDeTarifa lineas) : IListarLineasDeTarifa
{
    public IReadOnlySet<string> CamposOrdenables => lineas.CamposOrdenables;

    public async Task<Resultado<PaginaDe<LineaTarifaDto>>> EjecutarAsync(
        Paginacion paginacion,
        Guid tarifaId,
        CancellationToken cancelacion)
    {
        if (await tarifas.ObtenerAsync(tarifaId, cancelacion).ConfigureAwait(false) is null)
        {
            return Resultado.Fallo<PaginaDe<LineaTarifaDto>>(
                ErroresDeTarifa.NoEncontrada(tarifaId));
        }

        PaginaDe<LineaTarifa> pagina = await lineas
            .ListarAsync(paginacion, tarifaId, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new PaginaDe<LineaTarifaDto>(
            [.. pagina.Elementos.Select(linea => linea.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total));
    }
}
