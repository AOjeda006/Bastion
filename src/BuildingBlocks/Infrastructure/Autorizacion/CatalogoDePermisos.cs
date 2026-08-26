using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.BuildingBlocks.Infrastructure.Autorizacion;

/// <summary>
/// Todos los permisos que existen en el sistema, juntados en el <i>composition root</i>.
/// </summary>
/// <remarks>
/// Cada módulo declara los suyos en su propio <c>Contracts</c>; el host los une. Es lo que evita
/// que Identidad —que es quien valida los permisos de un rol— tenga que referenciar a los otros
/// quince módulos y romper la frontera del §4 para poder hacer su trabajo.
/// </remarks>
public sealed class CatalogoDePermisos : ICatalogoDePermisos
{
    private readonly HashSet<string> _valores;

    /// <summary>Compone el catálogo a partir de lo que declara cada módulo.</summary>
    /// <param name="permisos">Los permisos de todos los módulos, en texto.</param>
    /// <exception cref="ArgumentException">
    /// Si alguno no tiene la forma <c>modulo.recurso.accion</c>.
    /// </exception>
    public CatalogoDePermisos(IEnumerable<string> permisos)
    {
        ArgumentNullException.ThrowIfNull(permisos);

        // `Permiso.De` lanza si la forma no cuadra, y lanza AL ARRANCAR: un permiso mal escrito en
        // el catálogo tumba el proceso en vez de convertirse en una puerta que no abre nunca.
        Todos = [.. permisos.Select(Permiso.De).DistinctBy(permiso => permiso.Valor)];
        _valores = [.. Todos.Select(permiso => permiso.Valor)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<Permiso> Todos { get; }

    /// <inheritdoc/>
    public bool Contiene(Permiso permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);

        return _valores.Contains(permiso.Valor);
    }
}

/// <summary>Registro de la autorización por permisos en el contenedor.</summary>
public static class RegistroDeAutorizacion
{
    /// <summary>
    /// Cablea el catálogo, el proveedor de políticas, el manejador y el lector del <i>claim</i>.
    /// </summary>
    /// <remarks>
    /// Las cuatro piezas van juntas porque solo sirven juntas: un catálogo sin manejador no
    /// autoriza nada, y un manejador sin proveedor de políticas nunca llega a ejecutarse. Que se
    /// registren en una sola llamada es lo que impide arrancar con la mitad puesta — que es la
    /// forma que tiene la autorización de parecer que está y no estar.
    /// </remarks>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="permisos">Todos los permisos declarados por los módulos.</param>
    public static IServiceCollection AgregarAutorizacionPorPermisos(
        this IServiceCollection servicios,
        IEnumerable<string> permisos)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddSingleton<ICatalogoDePermisos>(new CatalogoDePermisos(permisos));
        servicios.AddSingleton<IAuthorizationPolicyProvider, ProveedorDePoliticasDePermisos>();
        servicios.AddSingleton<IAuthorizationHandler, ManejadorDePermisos>();

        servicios.AddHttpContextAccessor();
        servicios.TryAddScoped<IUsuarioActual, UsuarioActual>();

        return servicios;
    }
}
