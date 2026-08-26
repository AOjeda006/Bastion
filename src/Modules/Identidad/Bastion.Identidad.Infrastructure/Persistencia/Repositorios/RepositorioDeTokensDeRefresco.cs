using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Domain.Sesiones;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Identidad.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeTokensDeRefresco"/>
internal sealed class RepositorioDeTokensDeRefresco(IdentidadDbContext contexto)
    : IRepositorioDeTokensDeRefresco
{
    // SIN filtrar por vigencia. Una fila ya canjeada tiene que salir de aquí: encontrarla es
    // exactamente la señal de reutilización. Si esta consulta añadiera `&& fila.CanjeadoEn == null`
    // —que es lo que parece más limpio—, un token robado y ya usado se vería igual que uno
    // inventado, la detección desaparecería y ningún test lo notaría, porque las dos cosas
    // devuelven el mismo error.
    public Task<TokenDeRefresco?> ObtenerPorHashAsync(string hash, CancellationToken cancelacion) =>
        contexto.TokensDeRefresco.FirstOrDefaultAsync(fila => fila.Hash == hash, cancelacion);

    // Las que se van a revocar: no las revocadas ya. Volver a revocarlas no haría daño —el
    // dominio conserva el primer motivo— pero traerlas es traer la historia entera de la cadena
    // cada vez.
    public async Task<IReadOnlyList<TokenDeRefresco>> DeLaFamiliaAsync(
        Guid familiaId,
        CancellationToken cancelacion) =>
        await contexto.TokensDeRefresco
            .Where(fila => fila.FamiliaId == familiaId && fila.RevocadoEn == null)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TokenDeRefresco>> DelUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancelacion) =>
        await contexto.TokensDeRefresco
            .Where(fila => fila.UsuarioId == usuarioId && fila.RevocadoEn == null)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public void Agregar(TokenDeRefresco token) => contexto.TokensDeRefresco.Add(token);
}
