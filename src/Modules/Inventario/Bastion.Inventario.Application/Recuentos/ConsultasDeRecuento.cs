using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>Los valores que admiten los criterios propios de los dos listados del recuento.</summary>
/// <remarks>
/// <b>Constantes, para que el borde los valide con el mismo texto</b>: el patrón de cada criterio
/// se compone aquí, y el del estado con el nombre de cada valor del enumerado, así que renombrar
/// uno no deja el borde validando el viejo.
/// </remarks>
public static class LosFiltrosDelRecuento
{
    /// <summary>Solo las líneas que nadie ha contado todavía.</summary>
    public const string SinContar = "sin-contar";

    /// <summary>Solo las líneas cuyo teórico ya no es el de cuando se contaron.</summary>
    public const string TeoricoCambiado = "teorico-cambiado";

    /// <summary>Lo que admite el criterio de las líneas.</summary>
    public const string PatronDeLasLineas = "^(" + SinContar + "|" + TeoricoCambiado + ")$";

    /// <summary>Lo que admite el criterio del estado: el nombre de cada estado, tal como se publica.</summary>
    public const string PatronDelEstado =
        "^(" + nameof(EstadoDeRecuento.EnCurso)
        + "|" + nameof(EstadoDeRecuento.Confirmado)
        + "|" + nameof(EstadoDeRecuento.Anulado)
        + "|" + nameof(EstadoDeRecuento.Descartado) + ")$";

    /// <summary>El único campo por el que se ordenan las líneas.</summary>
    public const string CampoNumero = "numero";
}

/// <summary>Devuelve la ficha de un recuento, con su versión.</summary>
public interface IObtenerRecuento
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La ficha y su versión, o <c>404</c>.</returns>
    Task<Resultado<ConVersion<RecuentoDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de recuentos, opcionalmente acotada a un estado y a un almacén.</summary>
public interface IListarRecuentos : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y qué filtro.</param>
    /// <param name="estado">El estado al que se acota, ya validado por el borde, o nulo.</param>
    /// <param name="almacenId">El almacén al que se acota, o nulo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La página.</returns>
    Task<PaginaDe<RecuentoResumenDto>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? almacenId,
        CancellationToken cancelacion);
}

/// <summary>Devuelve una página de las líneas de un recuento, con su teórico.</summary>
public interface IListarLineasDeRecuento : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño y con qué orden.</param>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="solo">Las líneas a las que se acota, ya validado por el borde, o nulo para todas.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La página, o <c>404</c> si el recuento no existe.</returns>
    Task<Resultado<PaginaDe<LineaDeRecuentoDto>>> EjecutarAsync(
        Paginacion paginacion,
        Guid recuentoId,
        string? solo,
        CancellationToken cancelacion);
}

/// <summary>Devuelve una línea de un recuento, con su versión.</summary>
public interface IObtenerLineaDeRecuento
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="recuentoId">El recuento de la ruta.</param>
    /// <param name="lineaId">La línea.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La línea y su versión, que es la que pide contarla o quitarla.</returns>
    Task<Resultado<ConVersion<LineaDeRecuentoDto>>> EjecutarAsync(
        Guid recuentoId,
        Guid lineaId,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerRecuento"/>
/// <remarks>
/// <b>La ficha lee las existencias del almacén entero</b> mientras el recuento está en curso: las
/// tres cuentas y la huella son de todas las líneas, y la página de líneas solo enseña unas pocas
/// (ADR-0055 §2).
/// </remarks>
/// <param name="recuentos">El documento y las existencias.</param>
/// <param name="versiones">La versión de la cabecera, que es su <c>ETag</c>.</param>
internal sealed class ObtenerRecuento(
    IRepositorioDeRecuentos recuentos,
    IVersionesDeInventario versiones) : IObtenerRecuento
{
    public async Task<Resultado<ConVersion<RecuentoDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Recuento? recuento = await recuentos.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (recuento is null)
        {
            return Resultado.Fallo<ConVersion<RecuentoDto>>(ErroresDeRecuento.NoEncontrado(id));
        }

        ElTeoricoDeLasLineas teorico = await ElTeoricoDeLasLineas
            .LeerAsync(recuento, recuentos, articulos: null, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new ConVersion<RecuentoDto>(recuento.ADto(teorico), versiones.De(recuento)));
    }
}

/// <inheritdoc cref="IListarRecuentos"/>
/// <param name="recuentos">Los documentos.</param>
internal sealed class ListarRecuentos(IRepositorioDeRecuentos recuentos) : IListarRecuentos
{
    public IReadOnlySet<string> CamposOrdenables => recuentos.CamposOrdenables;

    public async Task<PaginaDe<RecuentoResumenDto>> EjecutarAsync(
        Paginacion paginacion,
        string? estado,
        Guid? almacenId,
        CancellationToken cancelacion)
    {
        EstadoDeRecuento? acotado = estado is null ? null : Enum.Parse<EstadoDeRecuento>(estado);

        PaginaDe<Recuento> pagina = await recuentos
            .ListarAsync(paginacion, acotado, almacenId, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<RecuentoResumenDto>(
            [.. pagina.Elementos.Select(recuento => recuento.AResumen())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IListarLineasDeRecuento"/>
/// <remarks>
/// <para>
/// <b>La página se corta aquí, sobre la lista entera</b> (ADR-0055 §13): el teórico de una línea sale
/// de las existencias del almacén, y el filtro de las de teórico cambiado no se puede escribir en
/// SQL sin leerlas todas igual. Las líneas ya vienen con el documento.
/// </para>
/// <para>
/// <b>El texto busca en el lote y en el número de serie</b>, sin distinguir mayúsculas, que es lo
/// que hace el <c>ILIKE</c> de los listados que filtran en la base.
/// </para>
/// </remarks>
/// <param name="recuentos">El documento y las existencias.</param>
internal sealed class ListarLineasDeRecuento(IRepositorioDeRecuentos recuentos) : IListarLineasDeRecuento
{
    private static readonly IReadOnlySet<string> s_ordenables =
        new HashSet<string>([LosFiltrosDelRecuento.CampoNumero], StringComparer.Ordinal);

    public IReadOnlySet<string> CamposOrdenables => s_ordenables;

    public async Task<Resultado<PaginaDe<LineaDeRecuentoDto>>> EjecutarAsync(
        Paginacion paginacion,
        Guid recuentoId,
        string? solo,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(paginacion);

        Recuento? recuento = await recuentos.ObtenerAsync(recuentoId, cancelacion).ConfigureAwait(false);

        if (recuento is null)
        {
            return Resultado.Fallo<PaginaDe<LineaDeRecuentoDto>>(ErroresDeRecuento.NoEncontrado(recuentoId));
        }

        ElTeoricoDeLasLineas teorico = await ElTeoricoDeLasLineas
            .LeerAsync(recuento, recuentos, articulos: null, cancelacion)
            .ConfigureAwait(false);

        IEnumerable<LineaDeRecuento> acotadas = solo switch
        {
            null => recuento.Lineas,
            LosFiltrosDelRecuento.SinContar => recuento.LineasSinContar(),
            LosFiltrosDelRecuento.TeoricoCambiado =>
                recuento.Lineas.Where(linea => Mapeos.TieneElTeoricoCambiado(linea, teorico)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(solo), solo, "un criterio de líneas que el borde tenía que haber rechazado con un 400"),
        };

        List<LineaDeRecuento> lineas = string.IsNullOrWhiteSpace(paginacion.Filtro)
            ? [.. acotadas]
            : [.. acotadas.Where(linea => Contiene(linea.CodigoDeLote, paginacion.Filtro)
                || Contiene(linea.NumeroDeSerie, paginacion.Filtro))];

        IEnumerable<LineaDeRecuento> ordenadas = paginacion.Orden?.Descendente == true
            ? lineas.OrderByDescending(linea => linea.Numero)
            : lineas.OrderBy(linea => linea.Numero);

        return Resultado.Correcto(new PaginaDe<LineaDeRecuentoDto>(
            [.. ordenadas.Skip(paginacion.Salto).Take(paginacion.Tamanio).Select(linea => linea.ADto(teorico))],
            paginacion.Pagina,
            paginacion.Tamanio,
            lineas.Count));
    }

    private static bool Contiene(string? texto, string buscado) =>
        texto is not null && texto.Contains(buscado.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <inheritdoc cref="IObtenerLineaDeRecuento"/>
/// <remarks>
/// <b>Lee solo las existencias de su artículo</b>: el teórico de una clave no depende de las demás.
/// </remarks>
/// <param name="recuentos">El documento y las existencias.</param>
/// <param name="versiones">La versión de la línea, que es la que exige contarla (ADR-0055 §4).</param>
internal sealed class ObtenerLineaDeRecuento(
    IRepositorioDeRecuentos recuentos,
    IVersionesDeInventario versiones) : IObtenerLineaDeRecuento
{
    public async Task<Resultado<ConVersion<LineaDeRecuentoDto>>> EjecutarAsync(
        Guid recuentoId,
        Guid lineaId,
        CancellationToken cancelacion)
    {
        Recuento? recuento = await recuentos.ObtenerAsync(recuentoId, cancelacion).ConfigureAwait(false);

        if (recuento is null)
        {
            return Resultado.Fallo<ConVersion<LineaDeRecuentoDto>>(ErroresDeRecuento.NoEncontrado(recuentoId));
        }

        LineaDeRecuento? linea = recuento.Lineas.FirstOrDefault(candidata => candidata.Id == lineaId);

        if (linea is null)
        {
            return Resultado.Fallo<ConVersion<LineaDeRecuentoDto>>(
                ErroresDeRecuento.LineaNoEncontrada(recuentoId, lineaId));
        }

        ElTeoricoDeLasLineas teorico = await ElTeoricoDeLasLineas
            .LeerAsync(recuento, recuentos, [linea.ArticuloId], cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new ConVersion<LineaDeRecuentoDto>(linea.ADto(teorico), versiones.De(linea)));
    }
}
