using Bastion.Auditoria.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Auditoria.Infrastructure;

/// <summary>
/// Registro del módulo Auditoría en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// Este módulo no tiene todavía casos de uso ni repositorios: en el 0.7 solo aporta el esquema, la
/// tabla y su migración. Consultar la traza es de la fase 10, y no se adelanta.
/// </remarks>
public static class ModuloDeAuditoria
{
    /// <summary>Registra el contexto del módulo.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarModuloDeAuditoria(
        this IServiceCollection servicios,
        string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        // Sin interceptor: este contexto no escribe nada que auditar, y ponérselo sería declarar
        // una recursión que nunca ocurre. Lo que sí lleva es el filtro de empresa, en su
        // `OnModelCreating`.
        servicios.AddDbContext<AuditoriaDbContext>(
            opciones => AuditoriaDbContext.Configurar(opciones, cadenaDeConexion));

        return servicios;
    }
}
