using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Fechas;
using Bastion.BuildingBlocks.Domain.Fechas;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <summary>Las zonas de una empresa y de sus almacenes, como las guarda Organización.</summary>
/// <remarks>
/// <para>
/// <b>Abre el ámbito de lo bloqueado</b>, con el motivo de <c>ConsultaDeAlmacenes</c>,
/// <see cref="MotivoParaVerLoBloqueado.ResolucionDeUnMaestroApuntado"/>: un documento de un almacén
/// que se bloqueó después sigue necesitando saber qué día es allí, y la empresa bloqueada, igual.
/// Lo que se trae de dentro es una zona, que no dice nada de nadie. La apertura es una, y envuelve
/// solo las lecturas. <b>El filtro de empresa sigue puesto</b>: sin <i>claim</i>, como en el corte,
/// quien pregunta abre antes su propio ámbito.
/// </para>
/// <para>
/// <b>La herencia se resuelve aquí, al leer</b>: el almacén sin zona tiene la de su empresa en este
/// momento, y por eso no se copió nunca a su columna (ADR-0079).
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Organización.</param>
/// <param name="bloqueados">El ámbito de lo bloqueado.</param>
internal sealed class ZonasDeLaEmpresa(OrganizacionDbContext contexto, IAccesoALoBloqueado bloqueados)
    : IZonasDeLaEmpresa
{
    public Task<ZonaHoraria> DeLaEmpresaAsync(Guid empresaId, CancellationToken cancelacion) =>
        ViendoLoBloqueadoAsync(() => LaDeLaEmpresaAsync(empresaId, cancelacion));

    public Task<ZonaHoraria> DelAlmacenAsync(Guid almacenId, CancellationToken cancelacion) =>
        ViendoLoBloqueadoAsync(async () =>
        {
            var fila = await (
                    from almacen in contexto.Almacenes.AsNoTracking()
                    join empresa in contexto.Empresas.AsNoTracking() on almacen.EmpresaId equals empresa.Id
                    where almacen.Id == almacenId
                    select new { Propia = almacen.ZonaHoraria, DeLaEmpresa = empresa.ZonaHoraria })
                .FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"No existe el almacén {almacenId}.");

            return fila.Propia ?? fila.DeLaEmpresa;
        });

    public Task<IReadOnlyList<ZonaHoraria>> TodasAsync(Guid empresaId, CancellationToken cancelacion) =>
        ViendoLoBloqueadoAsync<IReadOnlyList<ZonaHoraria>>(async () =>
        {
            ZonaHoraria deLaEmpresa = await LaDeLaEmpresaAsync(empresaId, cancelacion).ConfigureAwait(false);

            List<ZonaHoraria?> propias = await contexto.Almacenes
                .AsNoTracking()
                .Where(almacen => almacen.EmpresaId == empresaId && almacen.ZonaHoraria != null)
                .Select(almacen => almacen.ZonaHoraria)
                .Distinct()
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            return [.. propias.OfType<ZonaHoraria>().Prepend(deLaEmpresa).Distinct()];
        });

    private async Task<ZonaHoraria> LaDeLaEmpresaAsync(Guid empresaId, CancellationToken cancelacion) =>
        await contexto.Empresas
            .AsNoTracking()
            .Where(empresa => empresa.Id == empresaId)
            .Select(empresa => empresa.ZonaHoraria)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false)
        ?? throw new InvalidOperationException($"No existe la empresa {empresaId}.");

    private async Task<T> ViendoLoBloqueadoAsync<T>(Func<Task<T>> leer)
    {
        using IDisposable _ = bloqueados.ViendoLoBloqueado(
            MotivoParaVerLoBloqueado.ResolucionDeUnMaestroApuntado);

        return await leer().ConfigureAwait(false);
    }
}
