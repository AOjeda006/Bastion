using Bastion.Organizacion.Application;
using Bastion.Organizacion.Application.Almacenes;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Application.Series;
using Bastion.Organizacion.Contracts.Empresas;
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

        // La unidad de trabajo es la del MÓDULO, y se registra bajo el tipo del MÓDULO. Bajo
        // `IUnidadTrabajo` a secas, el segundo módulo que se registrara desplazaría al primero y
        // los casos de uso de Organización confirmarían sobre el contexto ajeno: cero filas, sin
        // excepción y sin rastro.
        servicios.AddScoped<IUnidadTrabajoDeOrganizacion, UnidadDeTrabajoDeOrganizacion>();

        servicios.AddScoped<IRepositorioDeEmpresas, RepositorioDeEmpresas>();
        servicios.AddScoped<IRepositorioDeEjercicios, RepositorioDeEjercicios>();
        servicios.AddScoped<IRepositorioDeSeries, RepositorioDeSeries>();
        servicios.AddScoped<IRepositorioDeAlmacenes, RepositorioDeAlmacenes>();

        // Lo ÚNICO que este módulo expone a los demás, y va bajo el tipo de su `Contracts`. Se
        // registra aquí porque quien lo implementa es esta capa; quien lo consume —Identidad, al
        // guardar una pertenencia— no sabe que existe este ensamblado.
        servicios.AddScoped<IConsultaDeEmpresas, ConsultaDeEmpresas>();

        servicios.AgregarCasosDeUsoDeOrganizacion();

        return servicios;
    }
}
