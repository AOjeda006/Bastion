using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Catalogo.Application;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Catalogo.Infrastructure;

/// <summary>
/// Registro del módulo Catálogo en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// Un módulo no se registra a sí mismo ni alcanza la infraestructura de otro: la construcción del
/// sistema está separada de su uso (`principios/clean-architecture.md`).
/// </remarks>
public static class ModuloDeCatalogo
{
    /// <summary>Registra el contexto, los repositorios y los casos de uso del módulo.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    public static IServiceCollection AgregarModuloDeCatalogo(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddDbContext<CatalogoDbContext>((alcance, opciones) =>
        {
            CatalogoDbContext.Configurar(opciones, cadenaDeConexion);

            // Los tres interceptores, por lo mismo que en los otros módulos: la traza (ADR-0012),
            // la marca de última modificación (R14) y los eventos (R12, ADR-0013) entran en el
            // MISMO `SaveChanges` que el cambio. Quitar cualquiera de las tres líneas no rompe
            // nada visible; lo que se pierde es el rastro.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeAuditoria>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeMarcasDeTiempo>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeLaBandeja>());
        });

        // Bajo el tipo del MÓDULO, no bajo `IUnidadTrabajo` a secas: con el tipo común, el segundo
        // módulo que se registrara desplazaría al primero y los casos de uso confirmarían sobre el
        // contexto ajeno — cero filas, sin excepción y sin rastro.
        servicios.AddScoped<IUnidadTrabajoDeCatalogo, UnidadDeTrabajoDeCatalogo>();
        servicios.AddScoped<IVersionesDeCatalogo, VersionesDeCatalogo>();

        // El almacén de claves de idempotencia (R10), con la clave del módulo: el filtro del borde
        // resuelve el suyo por el segmento de la ruta, para que la clave y el trabajo caigan en la
        // transacción del MISMO contexto.
        servicios.AgregarAlmacenDeIdempotencia<AlmacenDeIdempotenciaDeCatalogo>(
            CatalogoDbContext.Esquema);

        servicios.AddScoped<IRepositorioDeArticulos, RepositorioDeArticulos>();
        servicios.AddScoped<IRepositorioDeCategorias, RepositorioDeCategorias>();
        servicios.AddScoped<IRepositorioDeTarifas, RepositorioDeTarifas>();
        servicios.AddScoped<IRepositorioDeLineasDeTarifa, RepositorioDeLineasDeTarifa>();

        // SIN `IConsultaDeLoBloqueado`, y hay que leer por qué en vez de darlo por un olvido. Los
        // otros tres módulos aportan su trozo al listado del art. 32 porque tienen entidades
        // bloqueables: empresa, usuario y tercero guardan datos de personas. Catálogo no guarda
        // ninguno —un artículo no tiene nombre de nadie, ni identificador fiscal, ni domicilio—,
        // así que no tiene nada bloqueado que enseñar. Registrar una consulta que siempre devuelve
        // vacío sería peor que no registrarla: pondría en el informe del artículo 32 un módulo que
        // no participa, y en verde. Lo comprueba `ElCatalogoNoGuardaDatosDeNadieTests`, que
        // recorre el modelo; si algún día aparece aquí un dato de una persona, la respuesta cambia
        // y ese test lo dice antes.
        //
        // Sin cargador de semillas: Catálogo no tiene maestros de instalación. Qué artículos vende
        // una empresa lo decide ella, al revés que las unidades de medida.
        //
        // Y sin `IConsulta...` propia todavía: el primer consumidor será Terceros en el ítem 1.10,
        // por `Tercero.TarifaAsignada`. La puerta se declarará entonces en `Catalogo.Contracts`.
        servicios.AgregarCasosDeUsoDeCatalogo();

        return servicios;
    }
}
