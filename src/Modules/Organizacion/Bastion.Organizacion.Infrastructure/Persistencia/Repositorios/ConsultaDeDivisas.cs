using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeDivisas"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeDivisas(OrganizacionDbContext contexto) : IConsultaDeDivisas
{
    // Dos respuestas hoy, por el mismo motivo que en las unidades: la retirada es del ítem 1.7.
    public async Task<EstadoDeMaestro> EstadoDeAsync(Guid divisaId, CancellationToken cancelacion) =>
        await contexto.Divisas
            .AnyAsync(divisa => divisa.Id == divisaId, cancelacion)
            .ConfigureAwait(false)
            ? EstadoDeMaestro.SeOfreceParaLoNuevo
            : EstadoDeMaestro.NoExiste;
}
