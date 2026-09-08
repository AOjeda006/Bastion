using Bastion.Catalogo.Application.Catalogo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.Catalogo.Application;

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
public static class CasosDeUsoDeCatalogo
{
    /// <summary>Registra los casos de uso del módulo Catálogo.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    public static IServiceCollection AgregarCasosDeUsoDeCatalogo(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddScoped<ICrearArticulo, CrearArticulo>();
        servicios.AddScoped<IObtenerArticulo, ObtenerArticulo>();
        servicios.AddScoped<IListarArticulos, ListarArticulos>();
        servicios.AddScoped<IModificarArticulo, ModificarArticulo>();

        servicios.AddScoped<ICrearCategoria, CrearCategoria>();
        servicios.AddScoped<IObtenerCategoria, ObtenerCategoria>();
        servicios.AddScoped<IListarCategorias, ListarCategorias>();
        servicios.AddScoped<IModificarCategoria, ModificarCategoria>();

        // Mismo criterio que en los otros tres módulos: el reloj como servicio, y `TryAdd` para
        // que un test que ya haya puesto un reloj falso conserve el suyo.
        servicios.TryAddSingleton(TimeProvider.System);

        return servicios;
    }
}
