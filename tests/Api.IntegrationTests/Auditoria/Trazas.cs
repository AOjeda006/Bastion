using System.Text.Json;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Api.IntegrationTests.Auditoria;

/// <summary>
/// Cómo lee un test la traza. <b>De la tabla</b>, que en el 0.7 es el único sitio donde está.
/// </summary>
/// <remarks>
/// No hay endpoint de consulta y no lo hay a propósito: leer la auditoría es de la fase 10. La
/// evidencia de que un cambio deja rastro es esta tabla, no una pantalla — y leerla directamente
/// tiene además la ventaja de que ninguna capa de presentación puede maquillar lo que hay.
/// </remarks>
internal static class Trazas
{
    /// <summary>Las trazas de una fila concreta, en el orden en que ocurrieron.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="entidad">Nombre corto del tipo, como lo escribe el interceptor.</param>
    /// <param name="entidadId">Clave de la fila.</param>
    public static async Task<IReadOnlyList<RegistroDeAuditoria>> DeAsync(
        PostgresConTodosLosModulos postgres,
        string entidad,
        Guid entidadId)
    {
        await using AuditoriaDbContext auditoria = postgres.AbrirAuditoriaEntera();

        return await auditoria.Registros
            .Where(fila => fila.Entidad == entidad && fila.EntidadId == entidadId.ToString())
            .OrderBy(fila => fila.OcurridoEn)
            .ThenBy(fila => fila.Id)
            .ToListAsync();
    }

    /// <summary>Toda la traza de la instalación. Para comprobar que algo NO está en ninguna.</summary>
    /// <param name="postgres">El contenedor.</param>
    public static async Task<IReadOnlyList<RegistroDeAuditoria>> TodasAsync(
        PostgresConTodosLosModulos postgres)
    {
        await using AuditoriaDbContext auditoria = postgres.AbrirAuditoriaEntera();

        return await auditoria.Registros.ToListAsync();
    }

    /// <summary>El valor «antes» o «despues» de una propiedad dentro de una traza.</summary>
    /// <param name="registro">La fila de traza.</param>
    /// <param name="propiedad">Nombre de la propiedad, con su prefijo si es de una poseída.</param>
    /// <param name="momento">"antes" o "despues".</param>
    /// <returns>El valor como texto, o <c>null</c> si esa propiedad no está en la traza.</returns>
    public static string? Valor(RegistroDeAuditoria registro, string propiedad, string momento)
    {
        ArgumentNullException.ThrowIfNull(registro);

        using var documento = JsonDocument.Parse(registro.Valores);

        return documento.RootElement.TryGetProperty(propiedad, out JsonElement detalle)
            && detalle.TryGetProperty(momento, out JsonElement valor)
            ? valor.ToString()
            : null;
    }

    /// <summary>Qué propiedades nombra una traza.</summary>
    /// <param name="registro">La fila de traza.</param>
    public static IReadOnlyList<string> Propiedades(RegistroDeAuditoria registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        using var documento = JsonDocument.Parse(registro.Valores);

        return [.. documento.RootElement.EnumerateObject().Select(propiedad => propiedad.Name)];
    }
}
