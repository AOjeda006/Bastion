using Bastion.Organizacion.Application.Almacenes;
using Bastion.Organizacion.Application.Bloqueos;
using Bastion.Organizacion.Application.Divisas;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Application.Impuestos;
using Bastion.Organizacion.Application.Series;
using Bastion.Organizacion.Application.Ubicaciones;
using Bastion.Organizacion.Application.Unidades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.Organizacion.Application;

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
public static class CasosDeUsoDeOrganizacion
{
    /// <summary>Registra los casos de uso del módulo Organización.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    public static IServiceCollection AgregarCasosDeUsoDeOrganizacion(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddScoped<ICrearEmpresa, CrearEmpresa>();
        servicios.AddScoped<IObtenerEmpresa, ObtenerEmpresa>();
        servicios.AddScoped<IListarEmpresas, ListarEmpresas>();
        servicios.AddScoped<IBuscarEmpresas, BuscarEmpresas>();
        servicios.AddScoped<IModificarEmpresa, ModificarEmpresa>();
        servicios.AddScoped<IBloquearEmpresa, BloquearEmpresa>();
        servicios.AddScoped<IDesbloquearEmpresa, DesbloquearEmpresa>();

        servicios.AddScoped<ICrearEjercicio, CrearEjercicio>();
        servicios.AddScoped<IObtenerEjercicio, ObtenerEjercicio>();
        servicios.AddScoped<IListarEjercicios, ListarEjercicios>();
        servicios.AddScoped<IModificarEjercicio, ModificarEjercicio>();
        servicios.AddScoped<IEliminarEjercicio, EliminarEjercicio>();
        servicios.AddScoped<ICerrarEjercicio, CerrarEjercicio>();
        servicios.AddScoped<IReabrirEjercicio, ReabrirEjercicio>();

        servicios.AddScoped<ICrearSerie, CrearSerie>();
        servicios.AddScoped<IObtenerSerie, ObtenerSerie>();
        servicios.AddScoped<IListarSeries, ListarSeries>();
        servicios.AddScoped<IModificarSerie, ModificarSerie>();
        servicios.AddScoped<IEliminarSerie, EliminarSerie>();

        servicios.AddScoped<ICrearAlmacen, CrearAlmacen>();
        servicios.AddScoped<IObtenerAlmacen, ObtenerAlmacen>();
        servicios.AddScoped<IListarAlmacenes, ListarAlmacenes>();
        servicios.AddScoped<IModificarAlmacen, ModificarAlmacen>();
        servicios.AddScoped<IBloquearAlmacen, BloquearAlmacen>();
        servicios.AddScoped<IDesbloquearAlmacen, DesbloquearAlmacen>();

        // El acceso reservado del art. 32 (ADR-0027). Va suelto y no dentro de un recurso porque
        // no es de ninguno: lista las tres entidades bloqueables del módulo a la vez, y es el
        // ÚNICO caso de uso de lectura que abre el ámbito de bloqueo.
        servicios.AddScoped<IListarLoBloqueado, ListarLoBloqueado>();

        servicios.AddScoped<ICrearImpuesto, CrearImpuesto>();
        servicios.AddScoped<IObtenerImpuesto, ObtenerImpuesto>();
        servicios.AddScoped<IListarImpuestos, ListarImpuestos>();
        servicios.AddScoped<IModificarImpuesto, ModificarImpuesto>();
        servicios.AddScoped<ICerrarImpuesto, CerrarImpuesto>();

        servicios.AddScoped<ICrearDivisa, CrearDivisa>();
        servicios.AddScoped<IObtenerDivisa, ObtenerDivisa>();
        servicios.AddScoped<IListarDivisas, ListarDivisas>();
        servicios.AddScoped<IModificarDivisa, ModificarDivisa>();

        servicios.AddScoped<ICrearTipoCambio, CrearTipoCambio>();
        servicios.AddScoped<IObtenerTipoCambio, ObtenerTipoCambio>();
        servicios.AddScoped<IListarTiposDeCambio, ListarTiposDeCambio>();
        servicios.AddScoped<IModificarTipoCambio, ModificarTipoCambio>();

        servicios.AddScoped<ICrearUnidadMedida, CrearUnidadMedida>();
        servicios.AddScoped<IObtenerUnidadMedida, ObtenerUnidadMedida>();
        servicios.AddScoped<IListarUnidadesDeMedida, ListarUnidadesDeMedida>();
        servicios.AddScoped<IModificarUnidadMedida, ModificarUnidadMedida>();

        servicios.AddScoped<ICrearConversionUm, CrearConversionUm>();
        servicios.AddScoped<IObtenerConversionUm, ObtenerConversionUm>();
        servicios.AddScoped<IListarConversionesUm, ListarConversionesUm>();
        servicios.AddScoped<IModificarConversionUm, ModificarConversionUm>();

        servicios.AddScoped<ICrearUbicacion, CrearUbicacion>();
        servicios.AddScoped<IObtenerUbicacion, ObtenerUbicacion>();
        servicios.AddScoped<IListarUbicaciones, ListarUbicaciones>();
        servicios.AddScoped<IModificarUbicacion, ModificarUbicacion>();
        servicios.AddScoped<IBloquearUbicacion, BloquearUbicacion>();
        servicios.AddScoped<IDesbloquearUbicacion, DesbloquearUbicacion>();

        // El reloj como servicio, y no `DateTimeOffset.UtcNow` esparcido por los casos de uso:
        // así un test puede fijar el instante y comprobar la fecha de bloqueo sin esperar a que
        // pase el tiempo. `TimeProvider` es el tipo de la BCL para esto desde .NET 8; un puerto
        // propio con la misma forma solo añadiría una capa que traducir. `TryAdd` y no `Add` para
        // que un test que ya haya puesto un reloj falso conserve el suyo.
        servicios.TryAddSingleton(TimeProvider.System);

        return servicios;
    }
}
