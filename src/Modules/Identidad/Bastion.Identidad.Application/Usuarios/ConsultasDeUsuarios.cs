using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Comun;
using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Contracts.Comun;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Domain.Roles;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Usuarios;

/// <summary>Devuelve un usuario por su identificador.</summary>
public interface IObtenerUsuario
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<UsuarioDto>> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <summary>Devuelve una página de usuarios de la empresa activa.</summary>
public interface IListarUsuarios
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página se pide y de qué tamaño.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<UsuarioDto>> EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion);
}

/// <summary>Devuelve las pertenencias de un usuario, con los roles de cada una.</summary>
public interface IListarPertenencias
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<IReadOnlyList<MembresiaDto>>> EjecutarAsync(Guid usuarioId, CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerUsuario"/>
internal sealed class ObtenerUsuario(IRepositorioDeUsuarios usuarios) : IObtenerUsuario
{
    public async Task<Resultado<UsuarioDto>> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        Usuario? usuario = await usuarios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        return usuario is null
            ? Resultado.Fallo<UsuarioDto>(ErroresDeUsuario.NoEncontrado(id))
            : Resultado.Correcto(usuario.ADto());
    }
}

/// <inheritdoc cref="IListarUsuarios"/>
/// <remarks>
/// Lista los de LA EMPRESA ACTIVA, no los del sistema. La empresa sale del <i>claim</i> y no hay
/// parámetro que la cambie: hasta que llegue el filtro global del ítem 0.6, este es el sitio donde
/// el alcance de la consulta queda atado (R8).
/// </remarks>
internal sealed class ListarUsuarios(
    IUsuarioActual usuarioActual,
    IRepositorioDeUsuarios usuarios) : IListarUsuarios
{
    public async Task<PaginaDe<UsuarioDto>> EjecutarAsync(
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        PaginaDe<Usuario> pagina = await usuarios
            .ListarDeEmpresaAsync(usuarioActual.EmpresaId, paginacion, cancelacion)
            .ConfigureAwait(false);

        return new PaginaDe<UsuarioDto>(
            [.. pagina.Elementos.Select(usuario => usuario.ADto())],
            pagina.Pagina,
            pagina.Tamanio,
            pagina.Total);
    }
}

/// <inheritdoc cref="IListarPertenencias"/>
internal sealed class ListarPertenencias(
    IRepositorioDeUsuarios usuarios,
    IRepositorioDeRoles roles) : IListarPertenencias
{
    public async Task<Resultado<IReadOnlyList<MembresiaDto>>> EjecutarAsync(
        Guid usuarioId,
        CancellationToken cancelacion)
    {
        Usuario? usuario = await usuarios.ObtenerAsync(usuarioId, cancelacion).ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo<IReadOnlyList<MembresiaDto>>(ErroresDeUsuario.NoEncontrado(usuarioId));
        }

        Guid[] rolIds = [.. usuario.Membresias
            .SelectMany(membresia => membresia.Roles.Select(rol => rol.RolId))
            .Distinct()];

        IReadOnlyList<Rol> encontrados = rolIds.Length == 0
            ? []
            : await roles.PorIdsAsync(rolIds, cancelacion).ConfigureAwait(false);

        var porId = encontrados.ToDictionary(rol => rol.Id);

        return Resultado.Correcto<IReadOnlyList<MembresiaDto>>(
            [.. usuario.Membresias
                .Select(membresia => membresia.ADto(porId))
                .OrderBy(membresia => membresia.EmpresaId)]);
    }
}
