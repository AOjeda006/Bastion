using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Impuestos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeImpuestos"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeImpuestos(OrganizacionDbContext contexto) : IConsultaDeImpuestos
{
    // Se proyectan las DOS fechas y se decide en memoria, en vez de llamar a `Impuesto.RigeEl` en
    // el `Where`: ese método es del dominio y EF Core no sabe traducirlo a SQL. Traducirlo a mano
    // dentro de la consulta dejaría la regla de vigencia escrita en dos sitios, y el día que una
    // cambiara la otra seguiría contestando lo de antes.
    //
    // Y no lleva filtro de empresa porque no puede: un tramo de impuesto es un maestro de
    // INSTALACIÓN (R8), el mismo 21 % para todas las sociedades. Tampoco es bloqueable, así que
    // aquí no hay ningún 404 de R16 que un consumidor pudiera confundir con «no existe».
    public async Task<EstadoDeMaestro> EstadoDeAsync(
        Guid impuestoId,
        DateOnly enLaFechaDeDevengo,
        CancellationToken cancelacion)
    {
        var tramo = await contexto.Impuestos
            .Where(impuesto => impuesto.Id == impuestoId)
            .Select(impuesto => new { impuesto.VigenteDesde, impuesto.VigenteHasta })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (tramo is null)
        {
            return EstadoDeMaestro.NoExiste;
        }

        bool rige = enLaFechaDeDevengo >= tramo.VigenteDesde
            && (tramo.VigenteHasta is null || enLaFechaDeDevengo <= tramo.VigenteHasta);

        return rige ? EstadoDeMaestro.SeOfreceParaLoNuevo : EstadoDeMaestro.SoloResuelveLoViejo;
    }
}
