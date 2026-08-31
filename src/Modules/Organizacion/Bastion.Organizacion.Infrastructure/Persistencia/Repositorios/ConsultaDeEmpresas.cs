using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Empresas;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeEmpresas"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método, no hace un <c>JOIN</c> contra un esquema ajeno ni una petición
/// HTTP a sí mismo (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeEmpresas(OrganizacionDbContext contexto) : IConsultaDeEmpresas
{
    // «Activa» lo pone el filtro de R16, no esta consulta: ver `RepositorioDeEmpresas`.
    public Task<bool> EstaActivaAsync(Guid empresaId, CancellationToken cancelacion) =>
        contexto.Empresas.AnyAsync(empresa => empresa.Id == empresaId, cancelacion);

    // Orden explícito: «la primera» tiene que ser siempre la misma, o la semilla elegiría una
    // empresa distinta en cada arranque según lo que devolviera PostgreSQL.
    public async Task<Guid?> PrimeraActivaAsync(CancellationToken cancelacion) =>
        await contexto.Empresas
            .OrderBy(empresa => empresa.Id)
            .Select(empresa => (Guid?)empresa.Id)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
}
