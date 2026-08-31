using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Comun;
using Bastion.Identidad.Contracts.Comun;
using Bastion.Identidad.Contracts.Roles;
using Bastion.Identidad.Domain.Roles;

namespace Bastion.Identidad.Application.Roles;

/// <summary>Crea un rol.</summary>
public interface ICrearRol
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Código, nombre y permisos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<RolDto>> EjecutarAsync(CrearRolDto peticion, CancellationToken cancelacion);
}

/// <summary>Devuelve un rol por su identificador.</summary>
public interface IObtenerRol
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del rol.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConVersion<RolDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de roles.</summary>
public interface IListarRoles
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página se pide y de qué tamaño.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<RolDto>> EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion);
}

/// <summary>Cambia el nombre y los permisos de un rol.</summary>
public interface IModificarRol
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del rol.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Nombre y la lista ENTERA de permisos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<RolDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarRolDto peticion,
        CancellationToken cancelacion);
}

/// <summary>Devuelve el catálogo de permisos que se pueden conceder.</summary>
/// <remarks>
/// Es lo que alimenta la pantalla de edición de roles. Sin esta consulta, la lista de casillas
/// habría que escribirla en el frontal, y el día que un módulo añadiera un permiso habría que
/// acordarse de añadirlo también allí: un permiso que existe en el servidor y no en la pantalla es
/// un permiso que nadie puede conceder.
/// </remarks>
public interface IListarPermisosDisponibles
{
    /// <summary>Ejecuta el caso de uso.</summary>
    IReadOnlyList<string> Ejecutar();
}

/// <inheritdoc cref="ICrearRol"/>
internal sealed class CrearRol(
    IRepositorioDeRoles roles,
    ICatalogoDePermisos catalogo,
    IUnidadTrabajoDeIdentidad unidadTrabajo) : ICrearRol
{
    public async Task<Resultado<RolDto>> EjecutarAsync(CrearRolDto peticion, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Resultado<List<Permiso>> permisos = ValidarPermisos.De(peticion.Permisos, catalogo);

        if (!permisos.EsCorrecto)
        {
            return Resultado.Fallo<RolDto>(permisos.Error!);
        }

        string codigo = Rol.NormalizarCodigo(peticion.Codigo);

        if (await roles.ExisteConCodigoAsync(codigo, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<RolDto>(ErroresDeRol.CodigoYaUsado(codigo));
        }

        var rol = Rol.Crear(codigo, peticion.Nombre);
        rol.FijarPermisos(permisos.Valor);

        roles.Agregar(rol);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(rol.ADto());
    }
}

/// <inheritdoc cref="IObtenerRol"/>
internal sealed class ObtenerRol(
    IRepositorioDeRoles roles,
    IVersionesDeIdentidad versiones) : IObtenerRol
{
    public async Task<Resultado<ConVersion<RolDto>>> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Rol? rol = await roles.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return rol is null
            ? Resultado.Fallo<ConVersion<RolDto>>(ErroresDeRol.NoEncontrado(id))
            : Resultado.Correcto(new ConVersion<RolDto>(rol.ADto(), versiones.De(rol)));
    }
}

/// <inheritdoc cref="IListarRoles"/>
internal sealed class ListarRoles(IRepositorioDeRoles roles) : IListarRoles
{
    public async Task<PaginaDe<RolDto>> EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion)
    {
        PaginaDe<Rol> pagina = await roles.ListarAsync(paginacion, cancelacion).ConfigureAwait(false);

        return new PaginaDe<RolDto>(
            [.. pagina.Elementos.Select(rol => rol.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IModificarRol"/>
internal sealed class ModificarRol(
    IRepositorioDeRoles roles,
    ICatalogoDePermisos catalogo,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    IVersionesDeIdentidad versiones) : IModificarRol
{
    public async Task<Resultado<RolDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarRolDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Resultado<List<Permiso>> permisos = ValidarPermisos.De(peticion.Permisos, catalogo);

        if (!permisos.EsCorrecto)
        {
            return Resultado.Fallo<RolDto>(permisos.Error!);
        }

        Rol? rol = await roles.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (rol is null)
        {
            return Resultado.Fallo<RolDto>(ErroresDeRol.NoEncontrado(id));
        }

        versiones.Exigir(rol, version);

        rol.Renombrar(peticion.Nombre);
        rol.FijarPermisos(permisos.Valor);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(rol.ADto());
    }
}

/// <inheritdoc cref="IListarPermisosDisponibles"/>
internal sealed class ListarPermisosDisponibles(ICatalogoDePermisos catalogo) : IListarPermisosDisponibles
{
    public IReadOnlyList<string> Ejecutar() =>
        [.. catalogo.Todos.Select(permiso => permiso.Valor).Order(StringComparer.Ordinal)];
}

/// <summary>Contrasta una lista de permisos pedida contra el catálogo del sistema.</summary>
/// <remarks>
/// <para>
/// <b>Un rol no puede conceder un permiso que no existe.</b> Sin esta comprobación, un error de
/// escritura —<c>organizacion.empresa.editar</c> en vez de <c>modificar</c>— crearía un rol que
/// parece dar acceso y no lo da; el fallo se descubriría en producción, con el usuario mirando un
/// 403 que nadie sabe explicar. Y al revés: un permiso escrito de más queda en la base para
/// siempre y aparecerá concedido el día que alguien registre ese nombre de verdad.
/// </para>
/// <para>
/// El catálogo lo compone el <i>composition root</i> juntando lo que declara cada módulo en su
/// <c>Contracts</c>. Por eso Identidad no referencia a los otros quince (§4).
/// </para>
/// </remarks>
internal static class ValidarPermisos
{
    internal static Resultado<List<Permiso>> De(IReadOnlyList<string> pedidos, ICatalogoDePermisos catalogo)
    {
        var errores = new ErroresPorCampo();
        List<Permiso> validos = [];

        foreach (string texto in pedidos ?? [])
        {
            if (!Permiso.Intentar(texto, out Permiso? permiso))
            {
                errores.Agregar("permisos", $"«{texto}» no tiene la forma modulo.recurso.accion.");
                continue;
            }

            if (!catalogo.Contiene(permiso!))
            {
                errores.Agregar("permisos", $"«{texto}» no es un permiso que exista en el sistema.");
                continue;
            }

            validos.Add(permiso!);
        }

        return errores.Hay
            ? Resultado.Fallo<List<Permiso>>(errores.AError())
            : Resultado.Correcto(validos);
    }
}

/// <summary>Los errores de negocio de los roles.</summary>
internal static class ErroresDeRol
{
    /// <summary>No existe ese rol.</summary>
    /// <param name="id">Identificador que se pidió.</param>
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "rol-no-encontrado",
        $"No hay ningún rol con el identificador {id}.");

    /// <summary>Ya hay un rol con ese código.</summary>
    /// <param name="codigo">Código ya normalizado.</param>
    internal static ErrorDeOperacion CodigoYaUsado(string codigo) => ErrorDeOperacion.Conflicto(
        "codigo-de-rol-ya-usado",
        $"Ya hay un rol con el código {codigo}.");
}
