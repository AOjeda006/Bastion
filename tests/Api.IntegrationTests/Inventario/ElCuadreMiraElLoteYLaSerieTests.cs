using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El cuadre compara las copias con el libro por la clave entera, con el lote y la serie dentro
/// (ítem 2.9, ADR-0048 §6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que se estropea suma en su hueco lo mismo que antes.</b> Una unidad pasa de un lote a otro
/// en la fila viva y en la instantánea, y la serie que estaba le deja su unidad a la que había
/// salido. Un cuadre que agrupara por artículo, almacén y ubicación no vería nada, y el caso lo
/// afirma antes de mirar el de verdad: es la pareja que dice que lo que sale, sale por el lote y
/// la serie.
/// </para>
/// <para>
/// <b>La propiedad solo lo ve por la cuenta.</b> <c>ElSaldoEsLaSumaDelLibroPorPropiedadTests</c>
/// cuenta las claves comparadas y exige que no salga ningún descuadre, porque sus copias no mienten.
/// Aquí mienten, y lo que se afirma es que cada descuadre sale con su lote o su serie.
/// </para>
/// <para>
/// <b>Semillas: la empresa, la 596; los maestros de instalación, la 597 y la 598.</b> La 599 queda
/// libre. Del 590 al 595 son los terceros artículos de la propiedad.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElCuadreMiraElLoteYLaSerieTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    [Fact]
    public async Task El_cuadre_ve_el_lote_y_la_serie_aunque_el_hueco_sume_lo_que_debe()
    {
        EscenaTrazable deLote = await EscenaTrazable.MontarAsync(_api, 596, "CTZ", 597, "PorLote");

        _clientes.Add(deLote.Cliente);

        (Guid articuloDeSerie, Guid unidadDeSerie) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(deLote.Cliente, 598, "PorNumeroSerie");

        EscenaTrazable deSerie = deLote with { ArticuloId = articuloDeSerie, UnidadId = unidadDeSerie };
        Guid hueco = deLote.UbicacionA1;

        await deLote.MoverAsync(
            postgres,
            deLote.AlmacenA,
            [
                deLote.Entrada(hueco, 5m, lote: "L-1"),
                deLote.Entrada(hueco, 3m, lote: "L-2"),
                deSerie.Entrada(hueco, 1m, serie: "S-1"),
                deSerie.Entrada(hueco, 1m, serie: "S-2"),
            ]);

        // LA S-2 SALE Y SU FILA SE QUEDA A CERO: es la que el caso sube cuando baja la S-1.
        await deLote.MoverAsync(postgres, deLote.AlmacenA, [deSerie.Salida(hueco, 1m, serie: "S-2")]);

        DateOnly esteMes = LasExistencias.MesDe(EscenaTrazable.Hoy);

        (await LasExistencias.RecalcularAsync(postgres, deLote.EmpresaId, esteMes))
            .ShouldBe(4, "una por clave, la de este mes: también la de la S-2, a cero");

        CuadreDeLasExistencias limpio = await LasExistencias.CuadrarAsync(postgres, deLote.EmpresaId);

        limpio.ExistenciasComparadas.ShouldBe(4, "dos lotes y dos series, cada uno con su clave");
        limpio.InstantaneasComparadas.ShouldBe(4);
        limpio.Descuadres.ShouldBeEmpty("recién recalculado, todo cuadra: lo de abajo lo pone el caso");

        await using InventarioDbContext contexto = postgres.AbrirInventario(deLote.EmpresaId);

        Guid l1 = (await contexto.Lotes.AsNoTracking().SingleAsync(lote => lote.Codigo == "L-1")).Id;
        Guid l2 = (await contexto.Lotes.AsNoTracking().SingleAsync(lote => lote.Codigo == "L-2")).Id;

        NumeroDeSerie[] series = await contexto.NumerosDeSerie.AsNoTracking().ToArrayAsync();
        Guid s1 = series.Single(serie => serie.Numero == "S-1").Id;
        Guid s2 = series.Single(serie => serie.Numero == "S-2").Id;

        Existencia[] vivas = await contexto.Existencias.AsNoTracking().ToArrayAsync();
        Guid filaDeL1 = vivas.Single(viva => viva.LoteId == l1).Id;
        Guid filaDeL2 = vivas.Single(viva => viva.LoteId == l2).Id;
        Guid filaDeS1 = vivas.Single(viva => viva.NumeroDeSerieId == s1).Id;
        Guid filaDeS2 = vivas.Single(viva => viva.NumeroDeSerieId == s2).Id;

        // LAS COPIAS SE ESTROPEAN EN UNA TRANSACCIÓN QUE SE DESHACE, y el cuadre corre dentro de ella.
        await using IDbContextTransaction estropeando = await contexto.Database.BeginTransactionAsync();

        IReadOnlyList<PorHueco> antes = await PorHuecoAsync(contexto);

        // Una unidad del L-2 pasa al L-1, en la fila viva y en la instantánea de este mes.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET fisico = fisico + 1 WHERE id = {0}", filaDeL1)).ShouldBe(1);

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET fisico = fisico - 1 WHERE id = {0}", filaDeL2)).ShouldBe(1);

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.instantaneas_mensuales SET fisico = fisico + 1 " +
            "WHERE existencia_id = {0} AND mes = {1}",
            filaDeL1,
            esteMes)).ShouldBe(1);

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.instantaneas_mensuales SET fisico = fisico - 1 " +
            "WHERE existencia_id = {0} AND mes = {1}",
            filaDeL2,
            esteMes)).ShouldBe(1);

        // Y la S-1 le deja su unidad a la S-2, que había salido.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET fisico = 0 WHERE id = {0}", filaDeS1)).ShouldBe(1);

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET fisico = 1 WHERE id = {0}", filaDeS2)).ShouldBe(1);

        // LA PAREJA: por hueco, las copias dicen lo mismo que antes. Lo que salga abajo no lo
        // encuentra la suma del hueco, sino el lote y la serie de la clave.
        (await PorHuecoAsync(contexto)).ShouldBe(antes, "el caso estropea sin cambiar la suma de ningún hueco");

        CuadreDeLasExistencias sucio = await LasExistencias.CuadrarEnAsync(contexto, deLote.EmpresaId);

        sucio.ExistenciasComparadas.ShouldBe(4);
        sucio.InstantaneasComparadas.ShouldBe(4);
        sucio.ValoracionesComparadas.ShouldBe(2, "una por artículo, y ninguna estropeada");

        sucio.Descuadres.ShouldAllBe(descuadre => descuadre.AlmacenId == deLote.AlmacenA);

        sucio.Descuadres
            .Select(descuadre => new Hallado(
                descuadre.Que,
                descuadre.ArticuloId,
                descuadre.UbicacionId,
                descuadre.LoteId,
                descuadre.NumeroDeSerieId,
                descuadre.Mes,
                descuadre.Esperado,
                descuadre.Guardado,
                descuadre.Filas))
            .ShouldBe(
                [
                    new Hallado("existencia", deLote.ArticuloId, hueco, l1, null, null, 5m, 6m, 1),
                    new Hallado("existencia", deLote.ArticuloId, hueco, l2, null, null, 3m, 2m, 1),
                    new Hallado("instantanea", deLote.ArticuloId, hueco, l1, null, esteMes, 5m, 6m, 1),
                    new Hallado("instantanea", deLote.ArticuloId, hueco, l2, null, esteMes, 3m, 2m, 1),
                    new Hallado("existencia", articuloDeSerie, hueco, null, s1, null, 1m, 0m, 1),
                    new Hallado("existencia", articuloDeSerie, hueco, null, s2, null, 0m, 1m, 1),
                ],
                ignoreOrder: true,
                customMessage: "las seis, cada una con su lote o su serie, y ninguna más");

        await estropeando.RollbackAsync();

        // Y DESHECHO, OTRA VEZ LIMPIO: lo que el cuadre encontró era lo que el caso puso.
        CuadreDeLasExistencias otraVez = await LasExistencias.CuadrarAsync(postgres, deLote.EmpresaId);

        otraVez.ExistenciasComparadas.ShouldBe(4);
        otraVez.Descuadres.ShouldBeEmpty();
    }

    /// <summary>Las filas vivas y las instantáneas sumadas por hueco, sin el lote ni la serie.</summary>
    private static async Task<IReadOnlyList<PorHueco>> PorHuecoAsync(InventarioDbContext contexto)
    {
        Existencia[] vivas = await contexto.Existencias.AsNoTracking().ToArrayAsync();
        InstantaneaMensual[] fotos = await contexto.InstantaneasMensuales.AsNoTracking().ToArrayAsync();
        Dictionary<Guid, Existencia> porId = vivas.ToDictionary(viva => viva.Id);

        return
        [
            .. vivas
                .Select(viva => (viva.ArticuloId, viva.UbicacionId, Mes: (DateOnly?)null, viva.Fisico))
                .Concat(fotos.Select(foto => (
                    porId[foto.ExistenciaId].ArticuloId,
                    porId[foto.ExistenciaId].UbicacionId,
                    Mes: (DateOnly?)foto.Mes,
                    foto.Fisico)))
                .GroupBy(fila => (fila.ArticuloId, fila.UbicacionId, fila.Mes))
                .Select(grupo => new PorHueco(
                    grupo.Key.ArticuloId, grupo.Key.UbicacionId, grupo.Key.Mes, grupo.Sum(fila => fila.Fisico)))
                .OrderBy(fila => fila.ArticuloId)
                .ThenBy(fila => fila.UbicacionId)
                .ThenBy(fila => fila.Mes),
        ];
    }

    /// <summary>Lo que suma un hueco en la fila viva (sin mes) o en la instantánea de un mes.</summary>
    private sealed record PorHueco(Guid ArticuloId, Guid UbicacionId, DateOnly? Mes, decimal Fisico);

    /// <summary>Un descuadre, sin el almacén, que es el mismo en todos.</summary>
    private sealed record Hallado(
        string Que,
        Guid ArticuloId,
        Guid? UbicacionId,
        Guid? LoteId,
        Guid? NumeroDeSerieId,
        DateOnly? Mes,
        decimal Esperado,
        decimal Guardado,
        long Filas);
}
