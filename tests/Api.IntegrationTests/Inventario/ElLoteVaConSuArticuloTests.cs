using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Organizacion.Contracts.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El lote es de su artículo y de su código, se crea la primera vez que entra y el libro lo lleva
/// en la clave, también en el inverso (ADR-0048 §2 y §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que se mira es la tabla de los lotes y la existencia</b>, no la respuesta: el lote no sale
/// en ningún DTO que devuelva la API (ver las decisiones del 2.9 en <c>docs/PLAN.md</c>). Las
/// lecturas van por EF Core con el filtro de la empresa puesto, como en <see cref="LasExistencias"/>.
/// </para>
/// <para>
/// <b>El código conserva la caja y pierde los espacios de los extremos</b>, que es lo que dice
/// GS1: «l-a1» y «L-A1» son dos lotes, y « L-A1 » es el segundo.
/// </para>
/// <para>
/// <b>Semillas: las empresas, del 572 al 575; los maestros de instalación, del 576 al 580.</b> El
/// tramo anterior, del 560 al 571, es de <c>UnNumeroDeSerieEnUnSoloSitioTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElLoteVaConSuArticuloTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    public async Task El_mismo_codigo_es_el_mismo_lote_y_la_caja_distingue_dos()
    {
        EscenaTrazable escena = await MontarAsync(572, "LTA", 576, "PorLote");

        await escena.MoverAsync(
            postgres,
            escena.AlmacenA,
            [
                escena.Entrada(escena.UbicacionA1, 2m, lote: "l-a1"),
                escena.Entrada(escena.UbicacionA1, 3m, lote: "L-A1"),
            ]);

        // OTRO DOCUMENTO, CON ESPACIOS: el código se recorta, y entonces es el lote que ya había.
        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 4m, lote: " L-A1 ")]);

        IReadOnlyList<Lote> lotes = await LotesAsync(escena);

        lotes.Select(lote => lote.Codigo).Order(StringComparer.Ordinal).ShouldBe(["L-A1", "l-a1"]);

        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, escena.EmpresaId);

        vivas.Count.ShouldBe(2, "una fila por lote, en el mismo hueco");
        vivas.Single(fila => fila.LoteId == LoteDe(lotes, "l-a1")).Fisico.ShouldBe(2m);
        vivas.Single(fila => fila.LoteId == LoteDe(lotes, "L-A1")).Fisico.ShouldBe(7m);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    [Fact]
    public async Task Una_salida_de_un_lote_que_no_hay_es_stock_insuficiente_y_no_deja_el_lote()
    {
        EscenaTrazable escena = await MontarAsync(573, "LTB", 577, "PorLote");

        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 5m, lote: "L-1")]);

        // EN EL HUECO HAY CINCO, PERO DE OTRO LOTE. La salida no tiene camino propio (ADR-0048 §2):
        // crea su lote y su fila a cero, y la restricción del stock la para al restar.
        Guid salida = await escena.AbrirAsync(
            postgres, escena.AlmacenA, [escena.Salida(escena.UbicacionA1, 1m, lote: "L-2")]);

        using HttpResponseMessage rechazo = await escena.ConfirmarPorLaApiAsync(salida);

        rechazo.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(rechazo));
        (await rechazo.Content.ReadAsStringAsync()).ShouldContain("/errors/stock-insuficiente");

        // Y EL LOTE QUE CREÓ SE FUE CON LA TRANSACCIÓN: un lote que nadie movió no se queda.
        (await LotesAsync(escena)).Select(lote => lote.Codigo).ShouldBe(["L-1"]);

        (await LasExistencias.VivasAsync(postgres, escena.EmpresaId)).ShouldHaveSingleItem().Fisico.ShouldBe(5m);
        (await escena.ContadorAsync()).ShouldBe(1, "el número que tomó la salida volvió a la serie");
    }

    [Fact]
    public async Task El_inverso_copia_el_lote_y_la_serie()
    {
        EscenaTrazable deLote = await MontarAsync(574, "LTC", 578, "PorLote");

        (Guid articuloDeSerie, Guid unidadDeSerie) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(deLote.Cliente, 579, "PorNumeroSerie");

        EscenaTrazable deSerie = deLote with { ArticuloId = articuloDeSerie, UnidadId = unidadDeSerie };

        Guid original = await deLote.MoverAsync(
            postgres,
            deLote.AlmacenA,
            [
                deLote.Entrada(deLote.UbicacionA1, 5m, lote: "L-1"),
                deSerie.Entrada(deLote.UbicacionA1, 1m, serie: "SN-1"),
            ]);

        using HttpResponseMessage anulacion = await deLote.AnularPorLaApiAsync(original);

        anulacion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(anulacion));

        // EL PAR SUMA CERO POR CLAVE, CON EL LOTE Y LA SERIE DENTRO. Si el inverso los perdiera,
        // sus filas caerían en la clave sin lote, que no tiene nada que restar, y el motor lo
        // habría parado por el stock.
        IReadOnlyList<ApunteDelLibro> libro = await LasExistencias.LibroAsync(postgres, deLote.EmpresaId);

        libro.Count.ShouldBe(4);

        IGrouping<ClaveDeExistencia, ApunteDelLibro>[] claves = [.. libro.GroupBy(apunte => apunte.Clave)];

        claves.Length.ShouldBe(2, "el original y su inverso caen en las mismas dos claves");

        ClaveDeExistencia delLote = claves.Single(clave => clave.Key.ArticuloId == deLote.ArticuloId).Key;
        ClaveDeExistencia deLaSerie = claves.Single(clave => clave.Key.ArticuloId == articuloDeSerie).Key;

        delLote.LoteId.ShouldNotBeNull();
        delLote.NumeroDeSerieId.ShouldBeNull();
        deLaSerie.NumeroDeSerieId.ShouldNotBeNull();
        deLaSerie.LoteId.ShouldBeNull();

        foreach (IGrouping<ClaveDeExistencia, ApunteDelLibro> clave in claves)
        {
            clave.Count().ShouldBe(2);
            clave.Sum(apunte => apunte.Cantidad).ShouldBe(0m);
        }

        (await LasExistencias.VivasAsync(postgres, deLote.EmpresaId)).ShouldAllBe(fila => fila.Fisico == 0m);

        await ExigirQueCuadraAsync(deLote, existencias: 2);
    }

    /// <summary>
    /// El primer lote de un código entra en dos almacenes a la vez, con dos transacciones de verdad:
    /// las dos salen bien, y con el mismo lote (ADR-0048 §2).
    /// </summary>
    /// <remarks>
    /// <b>La segunda espera en el índice único de los lotes</b>, al crear la fila que la primera ya
    /// creó sin confirmar. Cuando la primera confirma, su <c>ON CONFLICT DO NOTHING</c> no hace nada
    /// y su lectura, que toma otra foto, ve el lote de la primera. Van por dos series de ajustes y
    /// por dos almacenes para que esa sea la única espera.
    /// </remarks>
    [Fact]
    public async Task El_primer_lote_en_dos_almacenes_a_la_vez_es_un_solo_lote()
    {
        EscenaTrazable escena = await MontarAsync(575, "LTD", 580, "PorLote");

        SerieDto otraSerie = await escena.OtraSerieAsync("LTD-2");

        await using ElModuloDeInventario unos = new(postgres, escena.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, escena.EmpresaId);

        Guid alA = await escena.AbrirAsync(
            unos, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 3m, lote: "L-1")]);

        Guid alB = await escena.AbrirAsync(
            otros, escena.AlmacenB, [escena.Entrada(escena.UbicacionB1, 4m, lote: "L-1")], otraSerie.Id);

        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await unos.ConfirmarYQuedarseDentroAsync(alA);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(alB);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                unos.ProcesoDeLaBase,
                laOtra,
                "la entrada en vuelo",
                "ha creado el lote sin ver que la otra lo estaba creando");

            await enVuelo.CommitAsync();

            Resultado<AjusteDto> otra = await laOtra.WaitAsync(TimeSpan.FromSeconds(30));

            otra.EsCorrecto.ShouldBeTrue($"«{otra.Error?.Codigo}»");
        }

        Lote elLote = (await LotesAsync(escena)).ShouldHaveSingleItem();

        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, escena.EmpresaId);

        vivas.ShouldAllBe(fila => fila.LoteId == elLote.Id);
        vivas.Single(fila => fila.AlmacenId == escena.AlmacenA).Fisico.ShouldBe(3m);
        vivas.Single(fila => fila.AlmacenId == escena.AlmacenB).Fisico.ShouldBe(4m);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    private static Guid LoteDe(IReadOnlyList<Lote> lotes, string codigo) =>
        lotes.Single(lote => lote.Codigo == codigo).Id;

    private async Task<EscenaTrazable> MontarAsync(int semilla, string codigo, int maestro, string trazabilidad)
    {
        EscenaTrazable escena = await EscenaTrazable.MontarAsync(_api, semilla, codigo, maestro, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }

    /// <summary>Los lotes de la empresa de la escena, leídos con su filtro.</summary>
    private async Task<IReadOnlyList<Lote>> LotesAsync(EscenaTrazable escena)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Lotes.AsNoTracking().ToListAsync();
    }

    private async Task ExigirQueCuadraAsync(EscenaTrazable escena, int existencias)
    {
        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(existencias);
        cuadre.Descuadres.ShouldBeEmpty();
    }
}
