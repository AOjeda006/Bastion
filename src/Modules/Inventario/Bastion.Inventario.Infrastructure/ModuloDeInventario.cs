using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Inventario.Application;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Inventario.Infrastructure;

/// <summary>
/// Registro del módulo Inventario en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// Un módulo no se registra a sí mismo ni alcanza la infraestructura de otro: la construcción del
/// sistema está separada de su uso (`principios/clean-architecture.md`).
/// </remarks>
public static class ModuloDeInventario
{
    /// <summary>Registra el contexto, el repositorio y los casos de uso del módulo.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarModuloDeInventario(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddDbContext<InventarioDbContext>((alcance, opciones) =>
        {
            InventarioDbContext.Configurar(opciones, cadenaDeConexion);

            // Los tres interceptores, como en los demás módulos: la traza (ADR-0012), la marca de
            // última modificación (R14) y los eventos (R12, ADR-0013) entran en el MISMO
            // `SaveChanges` que el cambio.
            //
            // Los dos primeros no tienen nada que hacer con el libro —no se audita y sus filas no
            // se modifican nunca—, y aun así se registran: el contexto también escribe la traza,
            // el evento y el recibo de idempotencia del DOCUMENTO, que son tablas compartidas y sí
            // pasan por ellos. Quitarlos «porque el libro no los usa» apagaría la auditoría del
            // ajuste sin que nada fallara.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeAuditoria>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeMarcasDeTiempo>());
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeLaBandeja>());
        });

        // Bajo el tipo del MÓDULO, no bajo `IUnidadTrabajo` a secas, por lo mismo que en los otros
        // cuatro: con el tipo común la última inscripción gana, y confirmar sobre el contexto ajeno
        // devuelve cero sin excepción y sin rastro.
        servicios.AddScoped<IUnidadTrabajoDeInventario, UnidadDeTrabajoDeInventario>();

        // UNO para los dos agregados —el documento y el libro—, y el porqué está en su interfaz:
        // se guardan en la misma transacción, así que dos repositorios con dos unidades de trabajo
        // sugerirían que pueden guardarse por separado.
        servicios.AddScoped<IRepositorioDeAjustes, RepositorioDeAjustes>();

        // LOS DOS EVENTOS DEL DOCUMENTO, con su nombre escrito a mano: el catálogo no lo saca del
        // tipo a propósito, porque renombrar la clase rompería las filas que ya están en la cola.
        // Uno POR DOCUMENTO y no por movimiento: uno por fila convertiría la bandeja en una segunda
        // copia de la tabla que más crece del sistema.
        servicios.DeclararEvento<AjusteConfirmado>(AjusteConfirmado.Nombre);
        servicios.DeclararEvento<AjusteAnulado>(AjusteAnulado.Nombre);

        // SIN almacén de idempotencia (R10), y la ausencia sigue teniendo fecha: ese almacén lo
        // resuelve el filtro del borde por el segmento de la ruta, y este módulo todavía no tiene
        // borde. Entra con los endpoints, no antes.
        //
        // EL NUMERADOR, en cambio, entra hoy: no lo pide el borde, lo pide el caso de uso que
        // confirma. Va bajo el tipo del MÓDULO por lo mismo que la unidad de trabajo: su sentencia
        // corre en la transacción de ESTE contexto, y con el tipo común la última inscripción
        // ganaría y el número saldría de una transacción que no es la del documento.
        servicios.AddScoped<INumeradorDeSeriesDeInventario, NumeradorDeSeriesDeInventario>();

        // Sin `IConsultaDeLoBloqueado` y sin cargador de semillas, por el mismo motivo que Catálogo:
        // el libro no guarda datos de ninguna persona -no es bloqueable, y su configuración lo dice
        // con su porqué-, y qué existencias tiene una empresa no es un maestro de la instalación.
        servicios.AgregarCasosDeUsoDeInventario();

        return servicios;
    }
}
