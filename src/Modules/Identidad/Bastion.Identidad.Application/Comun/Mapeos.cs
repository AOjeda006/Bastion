using Bastion.Identidad.Contracts.Roles;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Domain.Roles;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Comun;

/// <summary>
/// Pasa las entidades del dominio a los DTOs que se publican.
/// </summary>
/// <remarks>
/// <para>
/// A mano y en un solo sitio, sin librería de mapeo. Un mapeador por convención copia lo que
/// encuentra, y lo que encuentra en <see cref="Usuario"/> incluye
/// <see cref="Usuario.HashDeContrasena"/>: el día que alguien añadiera un campo del mismo nombre
/// al DTO, el resumen empezaría a viajar en cada respuesta sin que ningún compilador dijera nada.
/// Escrito a mano, publicar un campo cuesta escribir su línea.
/// </para>
/// <para>
/// Es la misma razón por la que este fichero es corto y aburrido: es la lista de lo que sale.
/// </para>
/// </remarks>
internal static class Mapeos
{
    /// <summary>Pasa un usuario a su DTO.</summary>
    /// <param name="usuario">Usuario del dominio.</param>
    internal static UsuarioDto ADto(this Usuario usuario) => new(
        usuario.Id,
        usuario.Correo.Valor,
        usuario.Nombre,
        usuario.Estado.ToString(),
        usuario.BloqueadoEn,
        usuario.CreadoEn,
        usuario.UltimoAccesoEn);

    /// <summary>Pasa un rol a su DTO.</summary>
    /// <param name="rol">Rol del dominio.</param>
    internal static RolDto ADto(this Rol rol) => new(
        rol.Id,
        rol.Codigo,
        rol.Nombre,
        rol.EsDelSistema,
        [.. rol.Permisos.Select(permiso => permiso.Permiso).Order(StringComparer.Ordinal)]);

    /// <summary>Pasa una pertenencia a su DTO, resolviendo el nombre de cada rol.</summary>
    /// <param name="membresia">Pertenencia del dominio.</param>
    /// <param name="roles">Roles ya resueltos, indexados por identificador.</param>
    internal static MembresiaDto ADto(this Membresia membresia, IReadOnlyDictionary<Guid, Rol> roles) => new(
        membresia.EmpresaId,
        [.. membresia.Roles
            .Where(concedido => roles.ContainsKey(concedido.RolId))
            .Select(concedido => new RolConcedidoDto(
                concedido.RolId,
                roles[concedido.RolId].Codigo,
                roles[concedido.RolId].Nombre))
            .OrderBy(rol => rol.Codigo, StringComparer.Ordinal)]);
}
