using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Compras.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Compras.Infrastructure;

/// <summary>
/// Registro del módulo Compras en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// Desde el 3.3 registra solo el contexto. La unidad de trabajo, los repositorios, los casos de
/// uso, los eventos y el almacén de idempotencia llegan con el pedido del 3.7, que es quien los
/// usa: una inscripción sin nadie que la resuelva no la comprueba nada.
/// </remarks>
public static class ModuloDeCompras
{
    /// <summary>Registra el contexto del módulo.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarModuloDeCompras(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddDbContext<ComprasDbContext>((alcance, opciones) =>
        {
            ComprasDbContext.Configurar(opciones, cadenaDeConexion);

            // Los tres interceptores, como en los demás módulos: la traza (ADR-0012), la marca de
            // última modificación (R14) y los eventos (R12, ADR-0013) entran en el MISMO
            // `SaveChanges` que el cambio. Van desde el primer día aunque el módulo no escriba
            // nada todavía, para que el pedido no estrene un contexto sin rastro.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeAuditoria>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeMarcasDeTiempo>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeLaBandeja>());
        });

        return servicios;
    }
}
