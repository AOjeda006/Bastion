using Bastion.Terceros.Application.Terceros;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.Terceros.Application;

/// <summary>
/// Registra los casos de uso del módulo en el contenedor.
/// </summary>
/// <remarks>
/// <para>
/// Uno a uno y a mano, no por escaneo de ensamblado. El escaneo ahorra estas líneas y a cambio
/// deja de haber un sitio donde mirar qué expone el módulo; y el día que uno deja de registrarse
/// —porque se le cambió el nombre y ya no casa con la convención— no lo dice el compilador, lo
/// dice una petición en producción.
/// </para>
/// <para>
/// <c>Scoped</c>: cada caso de uso comparte el <c>DbContext</c> de la petición, que es lo que hace
/// que la unidad de trabajo confirme lo que ese caso de uso ha hecho y nada más.
/// </para>
/// </remarks>
public static class CasosDeUsoDeTerceros
{
    /// <summary>Registra los casos de uso del módulo Terceros.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    public static IServiceCollection AgregarCasosDeUsoDeTerceros(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddScoped<ICrearTercero, CrearTercero>();
        servicios.AddScoped<IObtenerTercero, ObtenerTercero>();
        servicios.AddScoped<IListarTerceros, ListarTerceros>();
        servicios.AddScoped<IBuscarTerceros, BuscarTerceros>();
        servicios.AddScoped<IModificarTercero, ModificarTercero>();
        servicios.AddScoped<IBloquearTercero, BloquearTercero>();
        servicios.AddScoped<IDesbloquearTercero, DesbloquearTercero>();

        // Lo que cuelga de la ficha. Un tipo por operación, igual que arriba: lo que un
        // controlador puede hacer se lee en su constructor.
        servicios.AddScoped<IListarContactos, ListarContactos>();
        servicios.AddScoped<IAgregarContacto, AgregarContacto>();
        servicios.AddScoped<IQuitarContacto, QuitarContacto>();

        servicios.AddScoped<IListarCuentasBancarias, ListarCuentasBancarias>();
        servicios.AddScoped<IAgregarCuentaBancaria, AgregarCuentaBancaria>();
        servicios.AddScoped<IMarcarCuentaPreferente, MarcarCuentaPreferente>();
        servicios.AddScoped<IQuitarCuentaBancaria, QuitarCuentaBancaria>();

        servicios.AddScoped<IListarCondicionesPago, ListarCondicionesPago>();
        servicios.AddScoped<IFijarCondicionPago, FijarCondicionPago>();

        servicios.AddScoped<IObtenerLimiteCredito, ObtenerLimiteCredito>();
        servicios.AddScoped<IFijarLimiteCredito, FijarLimiteCredito>();

        // Mismo criterio que en Organización: el reloj como servicio, y `TryAdd` para que un test
        // que ya haya puesto un reloj falso conserve el suyo.
        servicios.TryAddSingleton(TimeProvider.System);

        return servicios;
    }
}
