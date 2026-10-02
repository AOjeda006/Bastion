using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Los códigos de barras de un artículo.</summary>
public interface IListarCodigosBarrasDelArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<IReadOnlyList<CodigoBarrasDto>>> EjecutarAsync(
        Guid articuloId, CancellationToken cancelacion);
}

/// <summary>Un código de barras concreto, con su versión.</summary>
public interface IObtenerCodigoBarras
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del código de barras.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<CodigoBarrasDto>>> EjecutarAsync(
        Guid id, CancellationToken cancelacion);
}

/// <summary>Busca qué código de barras lleva un GTIN en la empresa.</summary>
public interface IBuscarCodigoBarrasPorGtin
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="gtin">El GTIN tal como llegó: de 8, 12, 13 o 14 cifras.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<IReadOnlyList<CodigoBarrasDto>>> EjecutarAsync(
        string? gtin, CancellationToken cancelacion);
}

/// <summary>Da de alta un código de barras en un artículo.</summary>
public interface IAgregarCodigoBarrasAlArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="peticion">El GTIN, su nivel y sus unidades.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<CodigoBarrasDto>> EjecutarAsync(
        Guid articuloId, AgregarCodigoBarrasDto peticion, CancellationToken cancelacion);
}

/// <summary>Quita un código de barras de un artículo.</summary>
public interface IQuitarCodigoBarras
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del código de barras.</param>
    /// <param name="version">La versión sobre la que se escribe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(
        Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IListarCodigosBarrasDelArticulo"/>
/// <remarks>
/// <b>404 si el artículo no existe, y no una lista vacía</b>, como la de proveedores: un artículo
/// equivocado y un artículo sin códigos son dos respuestas distintas.
/// </remarks>
internal sealed class ListarCodigosBarrasDelArticulo(
    IRepositorioDeArticulos articulos,
    IRepositorioDeCodigosBarras codigos) : IListarCodigosBarrasDelArticulo
{
    public async Task<Resultado<IReadOnlyList<CodigoBarrasDto>>> EjecutarAsync(
        Guid articuloId,
        CancellationToken cancelacion)
    {
        Articulo? articulo = await articulos
            .ObtenerAsync(articuloId, cancelacion)
            .ConfigureAwait(false);

        if (articulo is null)
        {
            return Resultado.Fallo<IReadOnlyList<CodigoBarrasDto>>(
                ErroresDeArticulo.NoEncontrado(articuloId));
        }

        IReadOnlyList<CodigoBarras> delArticulo = await codigos
            .DeArticuloAsync(articuloId, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto<IReadOnlyList<CodigoBarrasDto>>(
            [.. delArticulo.Select(codigo => codigo.ADto())]);
    }
}

/// <inheritdoc cref="IObtenerCodigoBarras"/>
/// <remarks>
/// Con su versión, porque es de aquí de donde sale la ETag que la baja exige (ADR-0051 §6).
/// </remarks>
internal sealed class ObtenerCodigoBarras(
    IRepositorioDeCodigosBarras codigos,
    IVersionesDeCatalogo versiones) : IObtenerCodigoBarras
{
    public async Task<Resultado<ConVersion<CodigoBarrasDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        CodigoBarras? codigo = await codigos
            .ObtenerAsync(id, cancelacion)
            .ConfigureAwait(false);

        return codigo is null
            ? Resultado.Fallo<ConVersion<CodigoBarrasDto>>(ErroresDeCodigoBarras.NoEncontrado(id))
            : Resultado.Correcto(new ConVersion<CodigoBarrasDto>(codigo.ADto(), versiones.De(codigo)));
    }
}

/// <inheritdoc cref="IBuscarCodigoBarrasPorGtin"/>
/// <remarks>
/// <para>
/// <b>Lo que entra se lee con <c>Gtin.Leer</c>, como en el alta</b>, así que el GTIN-12 y su forma
/// de 13 encuentran lo mismo (ADR-0051 §7). Un texto que no es un GTIN recibe el <c>400</c> de su
/// motivo, y no una lista vacía: una lista vacía diría «no lo tiene ningún artículo», y lo que
/// pasa es que eso no es un GTIN. Sin el parámetro, lo mismo: no tiene un largo admitido.
/// </para>
/// <para>
/// <b>Una lista de uno o de ninguno</b>, porque es una búsqueda sobre la colección, y una búsqueda
/// que no encuentra nada no es un recurso que no existe. Más de uno no puede haber: el índice
/// único <c>(empresa_id, gtin)</c> no lo deja.
/// </para>
/// </remarks>
internal sealed class BuscarCodigoBarrasPorGtin(IRepositorioDeCodigosBarras codigos) : IBuscarCodigoBarrasPorGtin
{
    public async Task<Resultado<IReadOnlyList<CodigoBarrasDto>>> EjecutarAsync(
        string? gtin,
        CancellationToken cancelacion)
    {
        LecturaDeGtin lectura = Gtin.Leer(gtin);

        if (!lectura.EsGtin)
        {
            return Resultado.Fallo<IReadOnlyList<CodigoBarrasDto>>(ErroresDeGtin.DelMotivo(lectura.Motivo));
        }

        CodigoBarras? codigo = await codigos
            .DelGtinAsync(lectura.Gtin, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto<IReadOnlyList<CodigoBarrasDto>>(
            codigo is null ? [] : [codigo.ADto()]);
    }
}

/// <inheritdoc cref="IAgregarCodigoBarrasAlArticulo"/>
/// <remarks>
/// <para>
/// <b>El orden es el de lo que cuesta menos decir antes.</b> Primero la empresa activa, porque el
/// <c>EmpresaId</c> de la fila sale del claim y es aquí donde se comprueba (lo declara
/// <c>LosIdentificadoresAjenosTests</c>). Después el artículo, que da <c>404</c>. Después lo que
/// trae la petición: el GTIN, el nivel y las unidades, cada uno con su <c>400</c>. Y por último el
/// duplicado, que es lo único que pregunta por otras filas.
/// </para>
/// <para>
/// <b>La comprobación del duplicado es cortesía</b> (ADR-0051 §5). Da el mismo <c>409</c> que el
/// índice sin llegar al motor, pero entre ella y la escritura cabe otra transacción: quien decide
/// es el índice único, y el borde traduce su nombre al mismo error.
/// </para>
/// <para>
/// <b>El tipo del artículo no limita el código</b> (ADR-0051 §11): GS1 llama artículo comercial
/// también a un servicio.
/// </para>
/// </remarks>
internal sealed class AgregarCodigoBarrasAlArticulo(
    IUsuarioActual usuarioActual,
    IConsultaDeEmpresas empresas,
    IRepositorioDeArticulos articulos,
    IRepositorioDeCodigosBarras codigos,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    TimeProvider reloj) : IAgregarCodigoBarrasAlArticulo
{
    public async Task<Resultado<CodigoBarrasDto>> EjecutarAsync(
        Guid articuloId,
        AgregarCodigoBarrasDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // La empresa sale del CLAIM y no de la petición (R8): `AgregarCodigoBarrasDto` no tiene el
        // campo.
        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<CodigoBarrasDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        Articulo? articulo = await articulos
            .ObtenerAsync(articuloId, cancelacion)
            .ConfigureAwait(false);

        if (articulo is null)
        {
            return Resultado.Fallo<CodigoBarrasDto>(ErroresDeArticulo.NoEncontrado(articuloId));
        }

        LecturaDeGtin lectura = Gtin.Leer(peticion.Gtin);

        if (!lectura.EsGtin)
        {
            return Resultado.Fallo<CodigoBarrasDto>(ErroresDeGtin.DelMotivo(lectura.Motivo));
        }

        NivelDeGtin? nivel = NivelesDeGtin.Leer(peticion.Nivel);

        if (nivel is null)
        {
            return Resultado.Fallo<CodigoBarrasDto>(
                ErroresDeCodigoBarras.NivelNoValido(NivelesDeGtin.Admitidos));
        }

        // Sin unidades, la base lleva la suya, que solo puede ser una. Una agrupación tiene que
        // decirlas: no hay un número que se pueda suponer.
        int unidades = peticion.Unidades
            ?? (nivel == NivelDeGtin.Base ? CodigoBarras.UnidadesDeLaBase : 0);

        if (!CodigoBarras.CuadranLasUnidades(nivel.Value, unidades))
        {
            return Resultado.Fallo<CodigoBarrasDto>(ErroresDeCodigoBarras.UnidadesNoValidas());
        }

        if (await codigos.DelGtinAsync(lectura.Gtin, cancelacion).ConfigureAwait(false) is not null)
        {
            return Resultado.Fallo<CodigoBarrasDto>(ErroresDeCodigoBarras.Duplicado());
        }

        var codigo = CodigoBarras.Nuevo(
            empresaId,
            articuloId,
            lectura.Gtin,
            nivel.Value,
            unidades,
            reloj.GetUtcNow());

        codigos.Agregar(codigo);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(codigo.ADto());
    }
}

/// <inheritdoc cref="IQuitarCodigoBarras"/>
/// <remarks>
/// <b>Se borra la fila</b> (ADR-0051 §8), y la empresa puede volver a dar de alta ese GTIN enseguida:
/// la regla de los 48 meses está derogada. La fila no cambia nunca, así que la versión que se exige
/// no protege de una edición: protege de que alguien borre lo que no ha visto. Una baja repetida da
/// <c>404</c>.
/// </remarks>
internal sealed class QuitarCodigoBarras(
    IRepositorioDeCodigosBarras codigos,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : IQuitarCodigoBarras
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        CodigoBarras? codigo = await codigos
            .ObtenerAsync(id, cancelacion)
            .ConfigureAwait(false);

        if (codigo is null)
        {
            return Resultado.Fallo(ErroresDeCodigoBarras.NoEncontrado(id));
        }

        versiones.Exigir(codigo, version);

        codigos.Eliminar(codigo);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
