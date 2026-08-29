using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Auditoria.Infrastructure;

/// <summary>
/// Registro del módulo Auditoría en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
/// <remarks>
/// <para>
/// Este módulo no tiene todavía casos de uso ni repositorios: aporta el esquema, las tablas y sus
/// migraciones. Consultar la traza es de la fase 10, y no se adelanta.
/// </para>
/// <para>
/// Desde el 0.8 aporta además <b>las dos tablas de la bandeja de salida</b> y el contexto con el
/// que el publicador las lee. No es que la bandeja sea auditoría: es que el §5 lista dieciséis
/// módulos y ninguno es la bandeja, así que no hay esquema del que pudiera ser, y quien crea una
/// tabla tiene que ser el dueño de un esquema. Este módulo ya es el dueño de la otra tabla que
/// escriben todos los contextos dentro de su transacción. El razonamiento entero, y las
/// alternativas descartadas, en el ADR-0013.
/// </para>
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

        // El contexto con el que el trabajo de fondo lee la bandeja. Se registra AQUÍ y no en los
        // bloques comunes porque los bloques comunes traen EF Core pero no el proveedor: allí no
        // se sabe contra qué base corre el sistema, y elegirlo es cosa de la Infrastructure de un
        // módulo. Sin interceptores, por lo mismo que el de arriba: lo que escribe está
        // clasificado como no auditable, así que engancharlos no produciría ni una fila.
        servicios.AddDbContext<ContextoDeLaBandeja>(opciones => opciones
            .UseNpgsql(cadenaDeConexion)
            .UseSnakeCaseNamingConvention());

        return servicios;
    }
}
