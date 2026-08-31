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

    // Una sola consulta con `IN`, no una por identificador: el selector se arma en cada login y en
    // cada renovación, y un usuario de seis empresas no puede costar seis idas y vueltas.
    //
    // Quien llama abre ya un ámbito sin inquilino (`AutenticacionYSesion`), y hace falta: sin él,
    // el filtro de R8 sobre `Empresa` la restringe a su propia clave y esto devolvería una sola
    // fila —la activa—, dejando el resto del desplegable sin nombre. El de R16 sí sigue puesto, y
    // eso es lo que se quiere: lo bloqueado no sale.
    public async Task<IReadOnlyDictionary<Guid, string>> RazonesSocialesDeAsync(
        IReadOnlyCollection<Guid> empresaIds,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(empresaIds);

        if (empresaIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await contexto.Empresas
            .Where(empresa => empresaIds.Contains(empresa.Id))
            .ToDictionaryAsync(empresa => empresa.Id, empresa => empresa.RazonSocial, cancelacion)
            .ConfigureAwait(false);
    }
}
