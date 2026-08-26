using Bastion.Identidad.Application.Arranque;
using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Application.Usuarios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.Identidad.Application;

/// <summary>
/// Registra los casos de uso del módulo en el contenedor.
/// </summary>
/// <remarks>
/// Uno a uno y a mano, por lo mismo que en Organización: el escaneo de ensamblado ahorra estas
/// líneas y deja de haber un sitio donde mirar qué expone el módulo. Aquí importa el doble, porque
/// un caso de uso de identidad que no se registra no falla al arrancar: falla al iniciar sesión.
/// </remarks>
public static class CasosDeUsoDeIdentidad
{
    /// <summary>Registra los casos de uso del módulo Identidad.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    public static IServiceCollection AgregarCasosDeUsoDeIdentidad(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        // Lo comparten el inicio de sesión, la renovación y el cambio de empresa para que los tres
        // emitan exactamente el mismo token. No tiene interfaz porque no es un caso de uso: es el
        // trozo común de tres, y sacarle una interfaz solo serviría para poder sustituirlo en un
        // test, que es justo lo que no interesa aquí.
        servicios.AddScoped<ConstructorDeSesion>();

        servicios.AddScoped<IIniciarSesion, IniciarSesion>();
        servicios.AddScoped<IRenovarSesion, RenovarSesion>();
        servicios.AddScoped<ICerrarSesion, CerrarSesion>();
        servicios.AddScoped<ICambiarEmpresaActiva, CambiarEmpresaActiva>();

        servicios.AddScoped<ICrearUsuario, CrearUsuario>();
        servicios.AddScoped<IObtenerUsuario, ObtenerUsuario>();
        servicios.AddScoped<IListarUsuarios, ListarUsuarios>();
        servicios.AddScoped<IModificarUsuario, ModificarUsuario>();
        servicios.AddScoped<IBloquearUsuario, BloquearUsuario>();
        servicios.AddScoped<IDesbloquearUsuario, DesbloquearUsuario>();
        servicios.AddScoped<ICambiarContrasenaPropia, CambiarContrasenaPropia>();
        servicios.AddScoped<IRestablecerContrasena, RestablecerContrasena>();

        servicios.AddScoped<IListarPertenencias, ListarPertenencias>();
        servicios.AddScoped<IConcederPertenencia, ConcederPertenencia>();
        servicios.AddScoped<IRetirarPertenencia, RetirarPertenencia>();
        servicios.AddScoped<IAsignarRol, AsignarRol>();
        servicios.AddScoped<IRetirarRol, RetirarRol>();

        servicios.AddScoped<ICrearRol, CrearRol>();
        servicios.AddScoped<IObtenerRol, ObtenerRol>();
        servicios.AddScoped<IListarRoles, ListarRoles>();
        servicios.AddScoped<IModificarRol, ModificarRol>();
        servicios.AddScoped<IListarPermisosDisponibles, ListarPermisosDisponibles>();

        // La puerta de arranque. Va con los demás y no aparte porque es un caso de uso más: lo
        // que la hace especial —que solo se aplica si no hay ningún usuario— está dentro de ella,
        // no en cómo se registra.
        servicios.AddScoped<ISembrarAdministrador, SembrarAdministrador>();

        servicios.TryAddSingleton(TimeProvider.System);

        return servicios;
    }
}
