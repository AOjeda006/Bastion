using Bastion.BuildingBlocks.Application;
using Bastion.Organizacion.Application;
using Bastion.Organizacion.Application.Almacenes;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Application.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Organizacion.Infrastructure;

/// <summary>
/// Registro del módulo Organización en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// Un módulo no se registra a sí mismo ni alcanza la infraestructura de otro: la construcción
/// del sistema está separada de su uso (`principios/clean-architecture.md`).
/// </remarks>
public static class ModuloDeOrganizacion
{
    /// <summary>Registra el contexto, los repositorios y los casos de uso del módulo.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    public static IServiceCollection AgregarModuloDeOrganizacion(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddDbContext<OrganizacionDbContext>(
            opciones => OrganizacionDbContext.Configurar(opciones, cadenaDeConexion));

        // La unidad de trabajo es la del MÓDULO: envuelve el contexto de este módulo y confirma
        // lo que se ha hecho en él. Se registra aquí, junto a su contexto, y no en el bloque
        // común, porque el bloque común no sabe cuántos contextos hay ni cuál toca.
        servicios.AddScoped<IUnidadTrabajo, UnidadDeTrabajoDeOrganizacion>();

        servicios.AddScoped<IRepositorioDeEmpresas, RepositorioDeEmpresas>();
        servicios.AddScoped<IRepositorioDeEjercicios, RepositorioDeEjercicios>();
        servicios.AddScoped<IRepositorioDeSeries, RepositorioDeSeries>();
        servicios.AddScoped<IRepositorioDeAlmacenes, RepositorioDeAlmacenes>();

        servicios.AgregarCasosDeUsoDeOrganizacion();

        return servicios;
    }
}
