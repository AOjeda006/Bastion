using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Corte;

/// <summary>
/// El avance del corte contra la base: la lista de empresas, y el recálculo y el cuadre de una.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las empresas salen de las existencias, y no de Organización</b> (ADR-0063 §7). Una empresa
/// sin existencias no tiene instantáneas que rehacer, y preguntárselo a otro módulo sería cruzar
/// una frontera para leer una lista que este ya tiene.
/// </para>
/// <para>
/// <b>Sin inquilino solo se lee esa lista</b>, con el motivo
/// <see cref="MotivoSinInquilino.AvanceDelCorte"/>, y el ámbito se cierra antes de devolverla. El
/// avance de cada empresa recibe su identificador y no abre ninguno: el recálculo y el cuadre
/// comparan la empresa ellos mismos en cada tabla que tocan.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario, uno por empresa que se avanza.</param>
/// <param name="inquilino">Quien abre el ámbito sin empresa para leer la lista.</param>
/// <param name="instantaneas">El recálculo de las instantáneas.</param>
/// <param name="cuadre">El cuadre de las existencias.</param>
/// <param name="reloj">Con qué se mide lo que tarda cada parte.</param>
internal sealed class ElAvanceDelCorte(
    InventarioDbContext contexto,
    IInquilinoActual inquilino,
    LasInstantaneasMensuales instantaneas,
    ElCuadreDeLasExistencias cuadre,
    TimeProvider reloj) : IAvanceDelCorte
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> EmpresasConExistenciasAsync(CancellationToken cancelacion)
    {
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.AvanceDelCorte);

        return await contexto.Existencias
            .AsNoTracking()
            .Select(existencia => existencia.EmpresaId)
            .Distinct()
            .OrderBy(empresaId => empresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>El cuadre va solo detrás de un avance.</b> Si el corte ya llegaba no hay nada nuevo que
    /// comparar, y cuadrar cada hora todas las empresas sería leer el libro entero veinticuatro
    /// veces al día para repetir la misma respuesta.
    /// </remarks>
    public async Task<AvanceHecho?> AvanzarAsync(
        Guid empresaId, DateOnly hastaElMes, CancellationToken cancelacion)
    {
        long inicio = reloj.GetTimestamp();

        int? repuestas = await instantaneas
            .AvanzarAsync(empresaId, hastaElMes, cancelacion)
            .ConfigureAwait(false);

        if (repuestas is null)
        {
            return null;
        }

        TimeSpan duracionDelAvance = reloj.GetElapsedTime(inicio);
        long inicioDelCuadre = reloj.GetTimestamp();

        CuadreDeLasExistencias cifras = await cuadre
            .CuadrarAsync(empresaId, cancelacion)
            .ConfigureAwait(false);

        return new AvanceHecho(
            repuestas.Value, duracionDelAvance, reloj.GetElapsedTime(inicioDelCuadre), cifras);
    }
}
