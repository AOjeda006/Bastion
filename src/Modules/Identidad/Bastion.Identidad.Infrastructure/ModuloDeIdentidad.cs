using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Identidad.Application;
using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Application.Usuarios;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Identidad.Infrastructure.Persistencia.Repositorios;
using Bastion.Identidad.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.Identidad.Infrastructure;

/// <summary>
/// Registro del módulo Identidad en el contenedor. Lo llama el <i>composition root</i>
/// (<c>src/Api</c>), que es el único proyecto autorizado a ver esta capa.
/// </summary>
public static class ModuloDeIdentidad
{
    /// <summary>Registra el contexto, los repositorios, la seguridad y los casos de uso.</summary>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    /// <param name="jwt">Cómo se firma el token de acceso.</param>
    public static IServiceCollection AgregarModuloDeIdentidad(
        this IServiceCollection servicios,
        string cadenaDeConexion,
        OpcionesDeJwt jwt)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(jwt);

        servicios.AddDbContext<IdentidadDbContext>((alcance, opciones) =>
        {
            IdentidadDbContext.Configurar(opciones, cadenaDeConexion);

            // La traza de cada cambio, DENTRO del mismo SaveChanges que lo produce (ADR-0012). Sin
            // esta línea el módulo sigue funcionando y sigue pasando sus tests de negocio: lo único
            // que cambia es que deja de haber rastro, y eso no se nota mirando la pantalla. Lo nota
            // `UnCambioEnUnMaestroDejaSuRastroTests`.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeAuditoria>());

            // Y la marca de última modificación (R14). Quitar esta línea no rompe nada visible:
            // `modificado_en` se queda con la fecha del alta para siempre. Lo nota
            // `LasMarcasDeTiempoLasPoneElRelojInyectadoTests`.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeMarcasDeTiempo>());

            // Y el de la bandeja de salida. Identidad todavía no emite eventos: el interceptor
            // va igual porque su ausencia no falla, se limita a no publicar nada, y esa es la
            // clase de olvido que no se ve hasta que alguien pregunta por qué no llegó el aviso.
            opciones.AddInterceptors(alcance.GetRequiredService<InterceptorDeLaBandeja>());
        });

        servicios.AddScoped<IUnidadTrabajoDeIdentidad, UnidadDeTrabajoDeIdentidad>();
        servicios.AddScoped<IVersionesDeIdentidad, VersionesDeIdentidad>();

        // Y el almacén de claves de idempotencia (R10), con la clave del módulo: el filtro del
        // borde resuelve el suyo por el segmento de la ruta, para que la clave y el trabajo caigan
        // en la transacción del MISMO contexto.
        servicios.AgregarAlmacenDeIdempotencia<AlmacenDeIdempotenciaDeIdentidad>(
            IdentidadDbContext.Esquema);

        servicios.AddScoped<IRepositorioDeUsuarios, RepositorioDeUsuarios>();
        servicios.AddScoped<IRepositorioDeRoles, RepositorioDeRoles>();
        servicios.AddScoped<IRepositorioDeTokensDeRefresco, RepositorioDeTokensDeRefresco>();

        // El hasher es SINGLETON, y no por ahorro: su resumen de relleno —el que iguala el tiempo
        // de un correo que no existe con el de uno que sí— se calcula en el constructor y cuesta
        // lo que cuesta comprobar una contraseña. Registrado por petición, ese coste se pagaría en
        // cada intento de acceso, dos veces.
        servicios.AddSingleton<IHasherDeContrasenas, HasherDeContrasenas>();

        servicios.AddSingleton(jwt);
        servicios.AddSingleton<IEmisorDeTokens, EmisorDeTokens>();
        servicios.TryAddSingleton(TimeProvider.System);

        servicios.AgregarCasosDeUsoDeIdentidad();

        return servicios;
    }
}
