using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Organizacion.Application;
using Bastion.Organizacion.Application.Almacenes;
using Bastion.Organizacion.Application.Divisas;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Application.Impuestos;
using Bastion.Organizacion.Application.Series;
using Bastion.Organizacion.Application.Ubicaciones;
using Bastion.Organizacion.Application.Unidades;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Infrastructure.Semillas;
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

        servicios.AddDbContext<OrganizacionDbContext>((alcance, opciones) =>
        {
            OrganizacionDbContext.Configurar(opciones, cadenaDeConexion);

            // La traza de cada cambio, DENTRO del mismo SaveChanges que lo produce (ADR-0012). Sin
            // esta línea el módulo sigue funcionando y sigue pasando sus tests de negocio: lo único
            // que cambia es que deja de haber rastro, y eso no se nota mirando la pantalla. Lo nota
            // `UnCambioEnUnMaestroDejaSuRastroTests`.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeAuditoria>());

            // Y la marca de última modificación (R14). Quitar esta línea no rompe nada visible:
            // `modificado_en` se queda con la fecha del alta para siempre. Lo nota
            // `LasMarcasDeTiempoLasPoneElRelojInyectadoTests`.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeMarcasDeTiempo>());

            // Y los eventos que registre un agregado, en ese mismo SaveChanges (R12, ADR-0013).
            // Quitar esta línea deja los eventos muriéndose en memoria sin que nada falle: lo nota
            // `ElEventoVaEnLaMismaTransaccionTests`.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeLaBandeja>());
        });

        // La unidad de trabajo es la del MÓDULO, y se registra bajo el tipo del MÓDULO. Bajo
        // `IUnidadTrabajo` a secas, el segundo módulo que se registrara desplazaría al primero y
        // los casos de uso de Organización confirmarían sobre el contexto ajeno: cero filas, sin
        // excepción y sin rastro.
        servicios.AddScoped<IUnidadTrabajoDeOrganizacion, UnidadDeTrabajoDeOrganizacion>();
        servicios.AddScoped<IVersionesDeOrganizacion, VersionesDeOrganizacion>();

        // Y el almacén de claves de idempotencia (R10), con la clave del módulo: el filtro del
        // borde resuelve el suyo por el segmento de la ruta, para que la clave y el trabajo caigan
        // en la transacción del MISMO contexto.
        servicios.AgregarAlmacenDeIdempotencia<AlmacenDeIdempotenciaDeOrganizacion>(
            OrganizacionDbContext.Esquema);

        servicios.AddScoped<IRepositorioDeEmpresas, RepositorioDeEmpresas>();
        servicios.AddScoped<IRepositorioDeEjercicios, RepositorioDeEjercicios>();
        servicios.AddScoped<IRepositorioDeSeries, RepositorioDeSeries>();
        servicios.AddScoped<IRepositorioDeAlmacenes, RepositorioDeAlmacenes>();
        servicios.AddScoped<IRepositorioDeImpuestos, RepositorioDeImpuestos>();
        servicios.AddScoped<IRepositorioDeDivisas, RepositorioDeDivisas>();
        servicios.AddScoped<IRepositorioDeTiposDeCambio, RepositorioDeTiposDeCambio>();
        servicios.AddScoped<IRepositorioDeUnidadesDeMedida, RepositorioDeUnidadesDeMedida>();
        servicios.AddScoped<IRepositorioDeConversiones, RepositorioDeConversiones>();
        servicios.AddScoped<IRepositorioDeUbicaciones, RepositorioDeUbicaciones>();

        // El cargador de las semillas del §12. Lo resuelve el MIGRADOR, no el arranque de la API:
        // se registra aquí porque es quien tiene el contexto, y se invoca desde `src/Api`, que es
        // el único proyecto que ve esta capa.
        servicios.AddScoped<CargadorDeSemillasDeOrganizacion>();

        // Lo ÚNICO que este módulo expone a los demás, y va bajo el tipo de su `Contracts`. Se
        // registra aquí porque quien lo implementa es esta capa; quien lo consume —Identidad, al
        // guardar una pertenencia— no sabe que existe este ensamblado.
        servicios.AddScoped<IConsultaDeEmpresas, ConsultaDeEmpresas>();

        // Los eventos que emite este módulo, con el nombre que llevan en la cola. Se declaran
        // AQUÍ y no en los bloques comunes: un catálogo central obligaría a tocar código común
        // para publicar un evento nuevo, que es justo la frontera del §4.
        servicios.DeclararEvento<EmpresaCreada>(EmpresaCreada.Nombre);

        servicios.AgregarCasosDeUsoDeOrganizacion();

        return servicios;
    }
}
