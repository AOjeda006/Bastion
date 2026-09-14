using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Terceros.Contracts.Terceros;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Quiénes suministran un artículo.</summary>
public interface IListarProveedoresDelArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<IReadOnlyList<ArticuloProveedorDto>>> EjecutarAsync(
        Guid articuloId, CancellationToken cancelacion);
}

/// <summary>Un suministro concreto, con su versión.</summary>
public interface IObtenerProveedorDelArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del suministro.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<ArticuloProveedorDto>>> EjecutarAsync(
        Guid id, CancellationToken cancelacion);
}

/// <summary>Declara que un tercero suministra un artículo.</summary>
public interface IAgregarProveedorAlArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="peticion">El tercero y su referencia.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ArticuloProveedorDto>> EjecutarAsync(
        Guid articuloId, AgregarProveedorDto peticion, CancellationToken cancelacion);
}

/// <summary>Cambia la referencia con la que un proveedor llama a un artículo.</summary>
public interface IModificarProveedorDelArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del suministro.</param>
    /// <param name="version">La versión sobre la que se escribe.</param>
    /// <param name="peticion">La referencia nueva.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ArticuloProveedorDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarProveedorDto peticion,
        CancellationToken cancelacion);
}

/// <summary>Quita un proveedor de un artículo.</summary>
public interface IQuitarProveedorDelArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del suministro.</param>
    /// <param name="version">La versión sobre la que se escribe.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(
        Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IListarProveedoresDelArticulo"/>
/// <remarks>
/// <para>
/// <b>ESTE ES EL SITIO DONDE EL ARTÍCULO 32 SE CUMPLE EN UNA LECTURA DE CATÁLOGO, y hay que decir
/// por qué está aquí y no en el repositorio ni en la pantalla.</b> El bloqueo del tercero vive en
/// <c>terceros.terceros</c>, en otro esquema, y ninguna consulta cruza esquemas (§5, regla 4): no
/// hay <c>WHERE</c> que pueda filtrarlo. La única vía es preguntar por el puerto, y preguntar es
/// exactamente lo que un repositorio no hace. Y en la pantalla no puede estar porque lo que la
/// pantalla no pinta sigue viajando en el JSON.
/// </para>
/// <para>
/// <b>Una sola pregunta para toda la lista</b>, no una por fila: <c>CualesSePuedenTratarAsync</c>
/// recibe el conjunto. Y devuelve <b>los que sí</b>, así que se conserva la intersección: un fallo
/// que contestara de menos —o vacío— enseñaría de menos, que es el lado por el que un filtro de
/// protección de datos tiene que caerse.
/// </para>
/// <para>
/// <b>404 si el artículo no existe, y no una lista vacía.</b> Un listado que cuelga de otro recurso
/// tiene dos «no hay nada» distintos —el padre no existe, o no tiene ninguno— y contestarlos igual
/// haría indistinguible un identificador equivocado de un artículo sin proveedores.
/// </para>
/// </remarks>
internal sealed class ListarProveedoresDelArticulo(
    IRepositorioDeArticulos articulos,
    IRepositorioDeProveedoresDeArticulo proveedores,
    IConsultaDeTerceros terceros) : IListarProveedoresDelArticulo
{
    public async Task<Resultado<IReadOnlyList<ArticuloProveedorDto>>> EjecutarAsync(
        Guid articuloId,
        CancellationToken cancelacion)
    {
        Articulo? articulo = await articulos
            .ObtenerAsync(articuloId, cancelacion)
            .ConfigureAwait(false);

        if (articulo is null)
        {
            return Resultado.Fallo<IReadOnlyList<ArticuloProveedorDto>>(
                ErroresDeArticulo.NoEncontrado(articuloId));
        }

        IReadOnlyList<ArticuloProveedor> guardados = await proveedores
            .DeArticuloAsync(articuloId, cancelacion)
            .ConfigureAwait(false);

        if (guardados.Count == 0)
        {
            return Resultado.Correcto<IReadOnlyList<ArticuloProveedorDto>>([]);
        }

        IReadOnlySet<Guid> tratables = await terceros
            .CualesSePuedenTratarAsync(
                [.. guardados.Select(suministro => suministro.TerceroId)],
                cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto<IReadOnlyList<ArticuloProveedorDto>>(
        [
            .. guardados
                .Where(suministro => tratables.Contains(suministro.TerceroId))
                .Select(suministro => suministro.ADto()),
        ]);
    }
}

/// <inheritdoc cref="IObtenerProveedorDelArticulo"/>
/// <remarks>
/// <b>Aquí NO se pregunta por el estado del tercero, y la asimetría con el listado es deliberada.</b>
/// A esta lectura se llega con el identificador de una fila concreta, que solo se puede haber
/// obtenido del listado —que ya filtró— o de la respuesta de haberla creado. Filtrar también aquí
/// costaría una consulta a otro módulo en cada apertura y devolvería un <c>404</c> sobre una fila
/// que existe, con lo que quien tuviera el enlace de una fila recién bloqueada no podría ni
/// quitarla. Lo que esta lectura publica del tercero es su identificador, que quien pregunta ya
/// tenía.
/// </remarks>
internal sealed class ObtenerProveedorDelArticulo(
    IRepositorioDeProveedoresDeArticulo proveedores,
    IVersionesDeCatalogo versiones) : IObtenerProveedorDelArticulo
{
    public async Task<Resultado<ConVersion<ArticuloProveedorDto>>> EjecutarAsync(
        Guid id,
        CancellationToken cancelacion)
    {
        ArticuloProveedor? suministro = await proveedores
            .ObtenerAsync(id, cancelacion)
            .ConfigureAwait(false);

        return suministro is null
            ? Resultado.Fallo<ConVersion<ArticuloProveedorDto>>(
                ErroresDeArticuloProveedor.NoEncontrado(id))
            : Resultado.Correcto(
                new ConVersion<ArticuloProveedorDto>(
                    suministro.ADto(), versiones.De(suministro)));
    }
}

/// <inheritdoc cref="IAgregarProveedorAlArticulo"/>
/// <remarks>
/// <para>
/// <b>Es el primer consumidor de <c>IConsultaDeTerceros</c>, y la mitad de ida del primer cruce
/// mutuo del proyecto.</b> <c>ArticuloProveedor.TerceroId</c> no tiene clave ajena —cruza
/// esquemas—, así que sin esta pregunta la columna aceptaría cualquier <c>uuid</c>: compilaría,
/// migraría y serviría peticiones, y el fallo saldría en el primer pedido de compra, tres fases
/// después. Es la cuarta vía del ADR-0024, tapada aquí.
/// </para>
/// <para>
/// <b>El puerto contesta tres estados y este caso de uso los junta en dos respuestas.</b> Los dos
/// que no autorizan —y detrás de <c>NoExiste</c> hay tres situaciones: inventado, de otra empresa
/// o bloqueado— salen por un solo error sin parámetros, con el motivo escrito en
/// <c>ErroresDeArticuloProveedor.TerceroNoValido</c>: distinguirlos convertiría este formulario en
/// el censo de las bajas del art. 32.
/// </para>
/// <para>
/// <b>Y el orden importa: primero el estado, después el duplicado.</b> Estuvo al revés, con un
/// argumento que parecía bueno —«un 409 solo se da sobre algo que ya se aceptó una vez, así que no
/// cuenta nada nuevo»— y que la pregunta del art. 32 hecha con una prueba desmontó. El listado
/// esconde el suministro de un tercero bloqueado; si al volver a añadirlo la respuesta fuera «ya
/// está» en vez de «ese tercero no vale», la diferencia entre lo que enseña el listado y lo que
/// contesta el alta diría exactamente quién está bloqueado. Sí cuenta algo nuevo: lo que el
/// listado acaba de callar. Preguntando antes por el estado, al bloqueado le toca el mismo 400 que a
/// uno inventado, y el 409 queda para quien el listado sí enseña.
/// </para>
/// </remarks>
internal sealed class AgregarProveedorAlArticulo(
    IUsuarioActual usuarioActual,
    IRepositorioDeArticulos articulos,
    IRepositorioDeProveedoresDeArticulo proveedores,
    IConsultaDeTerceros terceros,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    TimeProvider reloj) : IAgregarProveedorAlArticulo
{
    public async Task<Resultado<ArticuloProveedorDto>> EjecutarAsync(
        Guid articuloId,
        AgregarProveedorDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Articulo? articulo = await articulos
            .ObtenerAsync(articuloId, cancelacion)
            .ConfigureAwait(false);

        if (articulo is null)
        {
            return Resultado.Fallo<ArticuloProveedorDto>(
                ErroresDeArticulo.NoEncontrado(articuloId));
        }

        EstadoDelTercero estado = await terceros
            .EstadoDeAsync(peticion.TerceroId, RolDeTercero.Proveedor, cancelacion)
            .ConfigureAwait(false);

        if (estado != EstadoDelTercero.Disponible)
        {
            return Resultado.Fallo<ArticuloProveedorDto>(
                ErroresDeArticuloProveedor.TerceroNoValido());
        }

        if (await proveedores
            .YaLoSuministraAsync(articuloId, peticion.TerceroId, cancelacion)
            .ConfigureAwait(false))
        {
            return Resultado.Fallo<ArticuloProveedorDto>(
                ErroresDeArticuloProveedor.YaEsProveedor(peticion.TerceroId));
        }

        var suministro = ArticuloProveedor.Nuevo(
            usuarioActual.EmpresaId,
            articuloId,
            peticion.TerceroId,
            peticion.ReferenciaDelProveedor,
            reloj.GetUtcNow());

        proveedores.Agregar(suministro);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(suministro.ADto());
    }
}

/// <inheritdoc cref="IModificarProveedorDelArticulo"/>
/// <remarks>
/// <b>No vuelve a preguntar por el tercero</b>, y es la segunda mitad de la misma decisión que en
/// los maestros retirados: lo que aquí se cambia es un código nuestro para un suministro que ya
/// existe, y volver a preguntar convertiría el bloqueo de un tercero en un congelador de las filas
/// que ya apuntaban a él. Reservar no es borrar: lo que colgaba se queda, y se puede corregir.
/// </remarks>
internal sealed class ModificarProveedorDelArticulo(
    IRepositorioDeProveedoresDeArticulo proveedores,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : IModificarProveedorDelArticulo
{
    public async Task<Resultado<ArticuloProveedorDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarProveedorDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        ArticuloProveedor? suministro = await proveedores
            .ObtenerAsync(id, cancelacion)
            .ConfigureAwait(false);

        if (suministro is null)
        {
            return Resultado.Fallo<ArticuloProveedorDto>(
                ErroresDeArticuloProveedor.NoEncontrado(id));
        }

        versiones.Exigir(suministro, version);

        suministro.CambiarReferencia(peticion.ReferenciaDelProveedor);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(suministro.ADto());
    }
}

/// <inheritdoc cref="IQuitarProveedorDelArticulo"/>
/// <remarks>
/// <b>Se borra la fila, y no choca con nada de lo que el art. 32 protege.</b> Lo que desaparece no
/// es la ficha de nadie: es un hecho entre dos —«este tercero suministra esto»— que ha dejado de
/// ser verdad. Conservarlo en baja lógica no protegería ninguna cuenta y mantendría apuntando a una
/// persona desde un módulo que no guarda datos de personas. El rastro de que existió, y de quién lo
/// quitó, está en la traza (ADR-0012).
/// </remarks>
internal sealed class QuitarProveedorDelArticulo(
    IRepositorioDeProveedoresDeArticulo proveedores,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : IQuitarProveedorDelArticulo
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        ArticuloProveedor? suministro = await proveedores
            .ObtenerAsync(id, cancelacion)
            .ConfigureAwait(false);

        if (suministro is null)
        {
            return Resultado.Fallo(ErroresDeArticuloProveedor.NoEncontrado(id));
        }

        versiones.Exigir(suministro, version);

        proveedores.Eliminar(suministro);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
