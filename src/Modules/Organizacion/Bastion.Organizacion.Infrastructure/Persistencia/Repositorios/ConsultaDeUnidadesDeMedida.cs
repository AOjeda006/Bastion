using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Unidades;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeUnidadesDeMedida"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeUnidadesDeMedida(OrganizacionDbContext contexto)
    : IConsultaDeUnidadesDeMedida
{
    // Hoy solo hay dos respuestas posibles, y la tercera —`SoloResuelveLoViejo`— llega con la
    // retirada del ítem 1.7, que es cuando `UnidadMedida` tendrá con qué contestarla. La rama no se
    // escribe todavía a propósito: un `if` sobre una columna que no existe no se puede probar, y un
    // camino que ningún test recorre es peor que uno que no está.
    public async Task<EstadoDeMaestro> EstadoDeAsync(Guid unidadId, CancellationToken cancelacion) =>
        await contexto.UnidadesDeMedida
            .AnyAsync(unidad => unidad.Id == unidadId, cancelacion)
            .ConfigureAwait(false)
            ? EstadoDeMaestro.SeOfreceParaLoNuevo
            : EstadoDeMaestro.NoExiste;
}
