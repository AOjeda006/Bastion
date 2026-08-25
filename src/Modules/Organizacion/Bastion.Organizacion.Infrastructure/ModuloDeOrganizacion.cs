using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Organizacion.Infrastructure;

/// <summary>
/// Registro del módulo Organización en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// Un módulo no se registra a sí mismo ni alcanza la infraestructura de otro: la construcción
/// del sistema está separada de su uso (<c>principios/clean-architecture.md</c>).
/// </remarks>
public static class ModuloDeOrganizacion
{
    /// <summary>Registra el contexto, los repositorios y los casos de uso del módulo.</summary>
    public static IServiceCollection AgregarModuloDeOrganizacion(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddDbContext<OrganizacionDbContext>(
            opciones => OrganizacionDbContext.Configurar(opciones, cadenaDeConexion));

        return servicios;
    }
}
