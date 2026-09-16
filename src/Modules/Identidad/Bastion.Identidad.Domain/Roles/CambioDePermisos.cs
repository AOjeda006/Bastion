namespace Bastion.Identidad.Domain.Roles;

/// <summary>Lo que <see cref="Rol.Alinear"/> ha cambiado en un rol.</summary>
/// <param name="Concedidos">Los permisos que no tenía y ahora tiene, en orden ordinal.</param>
/// <param name="Retirados">Los que tenía y ya no, en orden ordinal.</param>
public sealed record CambioDePermisos(IReadOnlyList<string> Concedidos, IReadOnlyList<string> Retirados)
{
    /// <summary>Si ha cambiado algo.</summary>
    public bool HayCambios => Concedidos.Count > 0 || Retirados.Count > 0;
}
