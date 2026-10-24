using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Corte;

/// <summary>
/// El avance del corte contra la base: la lista de empresas, y el paso del avance y el cuadre de una.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las empresas salen de las existencias, y no de Organización</b> (ADR-0063 §7). Una empresa
/// sin existencias no tiene instantáneas que rehacer, y preguntárselo a otro módulo sería cruzar
/// una frontera para leer una lista que este ya tiene.
/// </para>
/// <para>
/// <b>Sin inquilino solo se leen esa lista y, al final de la vuelta, el corte de cada empresa</b>,
/// con el motivo <see cref="MotivoSinInquilino.AvanceDelCorte"/>, y el ámbito se cierra antes de
/// devolverlos. El avance de cada empresa recibe su identificador y no abre ninguno: el recálculo y
/// el cuadre comparan la empresa ellos mismos en cada tabla que tocan.
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
    public async Task<MesAvanzado?> AvanzarUnMesAsync(
        Guid empresaId, DateOnly hastaElMes, CancellationToken cancelacion)
    {
        long inicio = reloj.GetTimestamp();

        MesDeLasInstantaneas? hecho = await instantaneas
            .AvanzarUnMesAsync(empresaId, hastaElMes, cancelacion)
            .ConfigureAwait(false);

        return hecho is null ? null : new MesAvanzado(hecho.Mes, hecho.Escritas, reloj.GetElapsedTime(inicio));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Solo lo que no se ha cuadrado.</b> Si el corte no se ha movido desde el último cuadre, no
    /// hay nada nuevo que comparar, y cuadrar cada hora todas las empresas sería repetir la misma
    /// respuesta veinticuatro veces al día. Lo que quedó a medias, en cambio, se repite hasta que
    /// acaba.
    /// </remarks>
    public async Task<CuadreHecho?> CuadrarLoPendienteAsync(Guid empresaId, CancellationToken cancelacion)
    {
        long inicio = reloj.GetTimestamp();

        CuadreDeLoPendiente? hecho = await cuadre
            .CuadrarLoPendienteAsync(empresaId, cancelacion)
            .ConfigureAwait(false);

        return hecho is null
            ? null
            : new CuadreHecho(hecho.Desde, hecho.Hasta, reloj.GetElapsedTime(inicio), hecho.Cuadre);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Una lectura, y las demás casi nunca.</b> La primera trae las empresas con existencias, su
    /// corte y hasta dónde está cuadrado. La fecha del primer movimiento solo se busca para las que no
    /// tienen corte, que son las que el avance no ha conseguido avanzar nunca: con 10⁶ movimientos,
    /// buscarla cuesta 0,66 s (ADR-0064, *Medido*), y hacerlo cada hora para todas no diría nada que
    /// el corte no diga.
    /// </para>
    /// <para>
    /// <b>Lo que falta por cuadrar empieza donde empezaría el cuadre</b> (ítem 2.20): el mes
    /// siguiente al cuadrado, y si no tiene nada cuadrado, el de
    /// <see cref="ElCuadreDeLasExistencias.SqlDelPrimerMesPorCuadrar"/>, la misma sentencia. Esa
    /// recorre el libro de la empresa, y solo se lanza para las que tienen corte y nada cuadrado: tras
    /// rehacer el corte, o con un cuadre que falla en su primer mes.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<CorteDeUnaEmpresa>> CortesAsync(CancellationToken cancelacion)
    {
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.AvanceDelCorte);

        var cortes = await contexto.Existencias
            .AsNoTracking()
            .Select(existencia => existencia.EmpresaId)
            .Distinct()
            .OrderBy(empresaId => empresaId)
            .Select(empresaId => new
            {
                EmpresaId = empresaId,
                HastaElMes = contexto.CortesDeLaInstantanea
                    .Where(corte => corte.EmpresaId == empresaId)
                    .Select(corte => (DateOnly?)corte.HastaElMes)
                    .FirstOrDefault(),
                CuadradoHasta = contexto.CortesDeLaInstantanea
                    .Where(corte => corte.EmpresaId == empresaId)
                    .Select(corte => corte.CuadradoHasta)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        Dictionary<Guid, DateOnly> sinCuadrar = [];

        foreach (var corte in cortes)
        {
            if (corte.HastaElMes is not { } hasta || corte.CuadradoHasta == hasta)
            {
                continue;
            }

            sinCuadrar[corte.EmpresaId] = corte.CuadradoHasta is { } cuadrado
                ? cuadrado.AddMonths(1)
                : (await contexto.Database
                        .SqlQueryRaw<DateOnly>(
                            ElCuadreDeLasExistencias.SqlDelPrimerMesPorCuadrar, corte.EmpresaId, hasta)
                        .ToListAsync(cancelacion)
                        .ConfigureAwait(false))
                    .Single();
        }

        Guid[] sinCorte = [.. cortes.Where(corte => corte.HastaElMes is null).Select(corte => corte.EmpresaId)];

        Dictionary<Guid, DateOnly> primeros = sinCorte.Length == 0
            ? []
            : await contexto.Movimientos
                .AsNoTracking()
                .Where(movimiento => sinCorte.Contains(movimiento.EmpresaId))
                .GroupBy(movimiento => movimiento.EmpresaId)
                .Select(grupo => new
                {
                    EmpresaId = grupo.Key,
                    Primero = grupo.Min(movimiento => movimiento.FechaDeOperacion),
                })
                .ToDictionaryAsync(grupo => grupo.EmpresaId, grupo => grupo.Primero, cancelacion)
                .ConfigureAwait(false);

        return [.. cortes.Select(corte => new CorteDeUnaEmpresa(
            corte.EmpresaId,
            corte.HastaElMes,
            primeros.TryGetValue(corte.EmpresaId, out DateOnly primero) ? primero : null,
            sinCuadrar.TryGetValue(corte.EmpresaId, out DateOnly desde) ? desde : null))];
    }
}
