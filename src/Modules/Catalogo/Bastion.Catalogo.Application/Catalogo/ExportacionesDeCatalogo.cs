using System.Collections.Frozen;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>
/// Un artículo como sale en su exportación: lo que publica el listado, y los tres maestros a los
/// que apunta, con su código y su nombre (respuesta 1 de la puerta del 3.2, ADR-0074).
/// </summary>
/// <param name="Articulo">El artículo, tal como lo publica el listado.</param>
/// <param name="UnidadBase">Su unidad base, de Organización.</param>
/// <param name="ImpuestoPorDefecto">Su impuesto por defecto, de Organización.</param>
/// <param name="Categoria">Su categoría, o nula si no tiene.</param>
public sealed record ArticuloExportado(
    ArticuloDto Articulo,
    MaestroCitado UnidadBase,
    MaestroCitado ImpuestoPorDefecto,
    MaestroCitado? Categoria);

/// <summary>Una categoría como sale en su exportación, con su madre nombrada.</summary>
/// <param name="Categoria">La categoría, tal como la publica el listado.</param>
/// <param name="Padre">La categoría de la que cuelga, o nula si es raíz.</param>
public sealed record CategoriaExportada(CategoriaDto Categoria, MaestroCitado? Padre);

/// <summary>Una tarifa como sale en su exportación, con su divisa nombrada.</summary>
/// <param name="Tarifa">La tarifa, tal como la publica el listado.</param>
/// <param name="Divisa">Su divisa, de Organización.</param>
public sealed record TarifaExportada(TarifaDto Tarifa, MaestroCitado Divisa);

/// <summary>Un código de barras como sale en su exportación, con su artículo nombrado.</summary>
/// <param name="CodigoBarras">El código, tal como lo publica el listado.</param>
/// <param name="Articulo">El artículo del que es.</param>
public sealed record CodigoBarrasExportado(CodigoBarrasDto CodigoBarras, MaestroCitado Articulo);

/// <summary>
/// Una página interna de la exportación de artículos: la del listado, con sus maestros nombrados.
/// </summary>
/// <remarks>
/// <b>Llama al caso de uso del listado</b>, así que el orden, el texto, la categoría y lo que la
/// empresa ve son los suyos por construcción. Lo que añade es el nombre de cada maestro, una
/// pregunta por maestro y página, y solo con el permiso de verlo: sin él va su identificador, como
/// en la pantalla (ADR-0074 §3).
/// </remarks>
public interface IExportarArticulos : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página interna, con qué orden y qué filtro.</param>
    /// <param name="categoriaId">Categoría por la que se acota, o nula para no acotar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<ArticuloExportado>> EjecutarAsync(
        Paginacion paginacion,
        Guid? categoriaId,
        CancellationToken cancelacion);
}

/// <summary>Una página interna de la exportación de categorías, con la madre nombrada.</summary>
/// <remarks>
/// La madre se nombra sin mirar el permiso: es el de ver categorías, el del propio listado, y sin
/// él la exportación no habría llegado aquí.
/// </remarks>
public interface IExportarCategorias : IListado<CategoriaExportada>;

/// <summary>Una página interna de la exportación de tarifas, con la divisa nombrada.</summary>
/// <remarks>La divisa, solo con el permiso de verla, como los maestros del artículo.</remarks>
public interface IExportarTarifas : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página interna, con qué orden y qué filtro.</param>
    /// <param name="codigo">Código por el que se acota, o nulo para no acotar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<TarifaExportada>> EjecutarAsync(
        Paginacion paginacion,
        string? codigo,
        CancellationToken cancelacion);
}

/// <summary>
/// Una página interna de la exportación de los códigos de barras de un artículo, con el artículo
/// nombrado.
/// </summary>
/// <remarks>
/// <para>
/// <b>El listado no se pagina</b>: devuelve los códigos del artículo enteros, que son pocos, y la
/// pantalla los enseña todos. Así que cada página interna es un tramo de esa lista, en su orden, y
/// el total es su longitud. No deja ordenar, como el listado, que no lee <c>?sort=</c>.
/// </para>
/// <para>
/// <b>Un artículo que no existe es el <c>404</c> del listado</b>, y sale antes del primer byte.
/// </para>
/// </remarks>
public interface IExportarCodigosBarrasDelArticulo : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="paginacion">Qué página interna.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<PaginaDe<CodigoBarrasExportado>>> EjecutarAsync(
        Guid articuloId,
        Paginacion paginacion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IExportarArticulos"/>
internal sealed class ExportarArticulos(
    IListarArticulos listar,
    IRepositorioDeCategorias categorias,
    IConsultaDeUnidadesDeMedida unidades,
    IConsultaDeImpuestos impuestos,
    IUsuarioActual usuarioActual) : IExportarArticulos
{
    private static readonly Permiso s_verUnidades = Permiso.De(PermisosDeOrganizacion.UnidadMedidaVer);

    private static readonly Permiso s_verImpuestos = Permiso.De(PermisosDeOrganizacion.ImpuestoVer);

    private static readonly Permiso s_verCategorias = Permiso.De(PermisosDeCatalogo.CategoriaVer);

    public IReadOnlySet<string> CamposOrdenables => listar.CamposOrdenables;

    public async Task<PaginaDe<ArticuloExportado>> EjecutarAsync(
        Paginacion paginacion,
        Guid? categoriaId,
        CancellationToken cancelacion)
    {
        PaginaDe<ArticuloDto> pagina = await listar
            .EjecutarAsync(paginacion, categoriaId, cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deUnidades = await NombresDeMaestros.PedirAsync(
                usuarioActual,
                s_verUnidades,
                pagina.Elementos.Select(articulo => (Guid?)articulo.UnidadBaseId),
                unidades.NombresDeAsync,
                cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deImpuestos = await NombresDeMaestros.PedirAsync(
                usuarioActual,
                s_verImpuestos,
                pagina.Elementos.Select(articulo => (Guid?)articulo.ImpuestoPorDefectoId),
                impuestos.NombresDeAsync,
                cancelacion)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deCategorias = await NombresDeMaestros.PedirAsync(
                usuarioActual,
                s_verCategorias,
                pagina.Elementos.Select(articulo => articulo.CategoriaId),
                categorias.NombresDeAsync,
                cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<ArticuloExportado>(
            [
                .. pagina.Elementos.Select(articulo => new ArticuloExportado(
                    articulo,
                    MaestroCitado.De(articulo.UnidadBaseId, deUnidades),
                    MaestroCitado.De(articulo.ImpuestoPorDefectoId, deImpuestos),
                    MaestroCitado.De(articulo.CategoriaId, deCategorias))),
            ],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IExportarCategorias"/>
internal sealed class ExportarCategorias(
    IListarCategorias listar,
    IRepositorioDeCategorias categorias) : IExportarCategorias
{
    public IReadOnlySet<string> CamposOrdenables => listar.CamposOrdenables;

    public async Task<PaginaDe<CategoriaExportada>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<CategoriaDto> pagina = await listar.EjecutarAsync(paginacion, cancelacion).ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deMadres = await NombresDeMaestros.PedirAsync(
                pagina.Elementos.Select(categoria => categoria.PadreId), categorias.NombresDeAsync, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<CategoriaExportada>(
            [
                .. pagina.Elementos.Select(categoria =>
                    new CategoriaExportada(categoria, MaestroCitado.De(categoria.PadreId, deMadres))),
            ],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IExportarTarifas"/>
internal sealed class ExportarTarifas(
    IListarTarifas listar,
    IConsultaDeDivisas divisas,
    IUsuarioActual usuarioActual) : IExportarTarifas
{
    private static readonly Permiso s_verDivisas = Permiso.De(PermisosDeOrganizacion.DivisaVer);

    public IReadOnlySet<string> CamposOrdenables => listar.CamposOrdenables;

    public async Task<PaginaDe<TarifaExportada>> EjecutarAsync(
        Paginacion paginacion,
        string? codigo,
        CancellationToken cancelacion)
    {
        PaginaDe<TarifaDto> pagina = await listar.EjecutarAsync(paginacion, codigo, cancelacion).ConfigureAwait(false);

        IReadOnlyDictionary<Guid, NombreDeMaestro> deDivisas = await NombresDeMaestros.PedirAsync(
                usuarioActual,
                s_verDivisas,
                pagina.Elementos.Select(tarifa => (Guid?)tarifa.DivisaId),
                divisas.NombresDeAsync,
                cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<TarifaExportada>(
            [
                .. pagina.Elementos.Select(tarifa =>
                    new TarifaExportada(tarifa, MaestroCitado.De(tarifa.DivisaId, deDivisas))),
            ],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IExportarCodigosBarrasDelArticulo"/>
internal sealed class ExportarCodigosBarrasDelArticulo(
    IListarCodigosBarrasDelArticulo listar,
    IConsultaDeArticulos articulos) : IExportarCodigosBarrasDelArticulo
{
    public IReadOnlySet<string> CamposOrdenables => FrozenSet<string>.Empty;

    public async Task<Resultado<PaginaDe<CodigoBarrasExportado>>> EjecutarAsync(
        Guid articuloId,
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(paginacion);

        Resultado<IReadOnlyList<CodigoBarrasDto>> listado = await listar
            .EjecutarAsync(articuloId, cancelacion)
            .ConfigureAwait(false);

        if (!listado.EsCorrecto)
        {
            return Resultado.Fallo<PaginaDe<CodigoBarrasExportado>>(listado.Error!);
        }

        CodigoBarrasDto[] tramo =
            [.. listado.Valor.Skip((paginacion.Pagina - 1) * paginacion.Tamanio).Take(paginacion.Tamanio)];

        // El artículo es el del listado, y su permiso, el de ver artículos, es el de la exportación.
        IReadOnlyDictionary<Guid, NombreDeMaestro> deArticulos = await NombresDeMaestros.PedirAsync(
                tramo.Select(codigo => (Guid?)codigo.ArticuloId), articulos.NombresDeAsync, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new PaginaDe<CodigoBarrasExportado>(
            [
                .. tramo.Select(codigo =>
                    new CodigoBarrasExportado(codigo, MaestroCitado.De(codigo.ArticuloId, deArticulos))),
            ],
            paginacion.Pagina,
            paginacion.Tamanio,
            listado.Valor.Count));
    }
}
