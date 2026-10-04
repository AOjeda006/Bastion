using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Application.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Inventario.Application;

/// <summary>Registra los casos de uso del módulo Inventario.</summary>
/// <remarks>
/// Vive en este ensamblado porque las implementaciones son <c>internal</c>: fuera del módulo solo
/// se ve la interfaz, así que quien las registra tiene que verlas desde dentro.
/// </remarks>
public static class CasosDeUsoDeInventario
{
    /// <summary>Añade los casos de uso al contenedor.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarCasosDeUsoDeInventario(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddScoped<IAbrirAjuste, AbrirAjuste>();
        servicios.AddScoped<IConfirmarAjuste, ConfirmarAjuste>();
        servicios.AddScoped<IAnularAjuste, AnularAjuste>();
        servicios.AddScoped<IMovimientosDelDocumento, MovimientosDelDocumento>();
        servicios.AddScoped<IAbrirTransferencia, AbrirTransferencia>();
        servicios.AddScoped<IEnviarTransferencia, EnviarTransferencia>();
        servicios.AddScoped<IRecibirTransferencia, RecibirTransferencia>();
        servicios.AddScoped<IAnularTransferencia, AnularTransferencia>();

        // SIN ESTADO, así que una para todo el proceso. Es la costura para FIFO (ADR-0046 §10):
        // cambiar de método es cambiar esta línea.
        servicios.AddSingleton<IValoracionDeExistencias, ElPrecioMedioPonderado>();

        return servicios;
    }
}
