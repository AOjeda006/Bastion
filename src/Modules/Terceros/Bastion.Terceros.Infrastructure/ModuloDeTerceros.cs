using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Terceros.Application;
using Bastion.Terceros.Application.Terceros;
using Bastion.Terceros.Infrastructure.Persistencia;
using Bastion.Terceros.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Terceros.Infrastructure;

/// <summary>
/// Registro del módulo Terceros en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// Un módulo no se registra a sí mismo ni alcanza la infraestructura de otro: la construcción del
/// sistema está separada de su uso (`principios/clean-architecture.md`).
/// </remarks>
public static class ModuloDeTerceros
{
    /// <summary>Registra el contexto, los repositorios y los casos de uso del módulo.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    public static IServiceCollection AgregarModuloDeTerceros(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddDbContext<TercerosDbContext>((alcance, opciones) =>
        {
            TercerosDbContext.Configurar(opciones, cadenaDeConexion);

            // Los tres interceptores, por lo mismo que en Organización: la traza (ADR-0012), la
            // marca de última modificación (R14) y los eventos (R12, ADR-0013) entran en el MISMO
            // `SaveChanges` que el cambio. Quitar cualquiera de las tres líneas no rompe nada
            // visible; lo que se pierde es el rastro.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeAuditoria>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeMarcasDeTiempo>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeLaBandeja>());
        });

        // Bajo el tipo del MÓDULO, no bajo `IUnidadTrabajo` a secas: con el tipo común, el segundo
        // módulo que se registrara desplazaría al primero y los casos de uso confirmarían sobre el
        // contexto ajeno — cero filas, sin excepción y sin rastro.
        servicios.AddScoped<IUnidadTrabajoDeTerceros, UnidadDeTrabajoDeTerceros>();
        servicios.AddScoped<IVersionesDeTerceros, VersionesDeTerceros>();

        // El almacén de claves de idempotencia (R10), con la clave del módulo: el filtro del borde
        // resuelve el suyo por el segmento de la ruta, para que la clave y el trabajo caigan en la
        // transacción del MISMO contexto.
        servicios.AgregarAlmacenDeIdempotencia<AlmacenDeIdempotenciaDeTerceros>(
            TercerosDbContext.Esquema);

        servicios.AddScoped<IRepositorioDeTerceros, RepositorioDeTerceros>();

        // Sin cargador de semillas: Terceros no tiene maestros de instalación que sembrar. Un
        // tercero lo da de alta una empresa; no viene con el producto.
        //
        // Y sin `IConsulta...` propia: de momento ningún otro módulo pregunta nada sobre terceros.
        // Cuando Facturación lo necesite, la puerta se declarará en `Terceros.Contracts` y se
        // registrará aquí.
        servicios.AgregarCasosDeUsoDeTerceros();

        return servicios;
    }
}
