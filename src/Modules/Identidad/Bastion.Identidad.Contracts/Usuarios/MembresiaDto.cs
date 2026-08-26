using System.ComponentModel.DataAnnotations;

namespace Bastion.Identidad.Contracts.Usuarios;

/// <summary>La pertenencia de un usuario a una empresa, con sus roles ahí.</summary>
/// <param name="EmpresaId">Empresa a la que pertenece.</param>
/// <param name="Roles">Roles que tiene en esa empresa.</param>
public sealed record MembresiaDto(Guid EmpresaId, IReadOnlyList<RolConcedidoDto> Roles);

/// <summary>Un rol concedido, con su nombre para no obligar a una segunda consulta.</summary>
/// <param name="RolId">Identificador del rol.</param>
/// <param name="Codigo">Código estable del rol.</param>
/// <param name="Nombre">Nombre del rol.</param>
public sealed record RolConcedidoDto(Guid RolId, string Codigo, string Nombre);

/// <summary>
/// A qué empresa se da de alta al usuario.
/// </summary>
/// <remarks>
/// <b>Esta es la única petición del módulo que lleva un identificador de empresa en el cuerpo, y
/// no contradice R8.</b> R8 dice que la empresa <i>en la que se opera</i> sale del <i>claim</i>;
/// aquí la empresa no es el contexto de la operación, es su OBJETO —igual que el identificador de
/// un almacén en la ruta—. Quien la pide tiene que tener <c>identidad.pertenencia.conceder</c> en
/// la empresa de su <i>claim</i>, y de momento solo puede conceder pertenencias a esa misma
/// empresa: si pudiera nombrar otra, tendría en la práctica el permiso en todas.
/// </remarks>
public sealed record ConcederPertenenciaDto
{
    /// <summary>Empresa a la que se da de alta.</summary>
    [Required(ErrorMessage = "La empresa es obligatoria.")]
    public Guid EmpresaId { get; init; }
}

/// <summary>Qué rol se asigna o se retira, y en qué empresa.</summary>
public sealed record AsignarRolDto
{
    /// <summary>Empresa en la que se asigna.</summary>
    [Required(ErrorMessage = "La empresa es obligatoria.")]
    public Guid EmpresaId { get; init; }

    /// <summary>Rol que se asigna.</summary>
    [Required(ErrorMessage = "El rol es obligatorio.")]
    public Guid RolId { get; init; }
}
