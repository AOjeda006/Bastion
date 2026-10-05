using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El cuadre compara lo que vuela con las líneas de las transferencias enviadas: por la clave entera
/// en la existencia del destino, y en cantidad y en valor en su valoración (ítem 2.11, ADR-0053 §11).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que se estropea suma en el destino lo mismo que antes.</b> Una unidad en vuelo pasa de un
/// lote a otro, o la fila en vuelo pasa a ser de otra serie, en las filas del destino. Un cuadre que
/// sumara el tránsito por hueco no vería nada, y cada caso lo afirma antes de mirar el de verdad: es
/// la pareja que dice que el tránsito se cuadra con el lote y la serie dentro.
/// </para>
/// <para>
/// <b>Y otro artículo de la empresa tiene el mismo código</b>, «L-1» o «S-1», sin nada en vuelo. El
/// código de la línea se traduce al lote o a la serie de su artículo, y sin el artículo saldrían dos
/// claves en vuelo por una línea: el cuadre recién enviado ya no saldría limpio.
/// </para>
/// <para>
/// <b>La valoración del destino gana una unidad y un euro en vuelo</b> que ninguna línea lleva, y
/// cada columna sale con su descuadre. Que el estado de la transferencia cuenta lo ven los casos de
/// <c>LaTransferenciaTests</c>, que cuadran después de recibir y de anular con líneas que ya no
/// vuelan, y el de aquí, que cuadra con un borrador cuyas líneas no han salido.
/// </para>
/// <para>
/// <b>Lo que vuela se cuenta como el libro</b>: tecleado por su factor y redondeado a seis
/// decimales. 2,5 × 0,333333 son 0,8333325, y el libro guarda 0,833333; sin el redondeo, truncado o
/// con cuatro decimales sale otra cantidad. Y vuela sin valor, porque entró a coste cero: la
/// valoración del destino tiene cantidad en vuelo y nada de valor, y también tiene que compararse.
/// </para>
/// <para>
/// <b>Semillas.</b> La 721, la empresa del lote y, con el mismo número, la unidad y el tramo de
/// impuesto de su artículo; la 722, los del otro artículo con lote. La 723, la empresa y el artículo
/// del redondeo. La 724, la empresa y el artículo de la serie; la 725, los del otro artículo con
/// serie. La 726 es de <c>LaTransferenciaTests</c>, y del 727 al 729 quedan libres.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElCuadreDelTransitoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    // LO TECLEADO Y SU FACTOR, cuyo producto tiene un decimal más de los que guarda el libro.
    private const decimal Tecleado = 2.5m;
    private const decimal Factor = 0.333333m;
    private const decimal EnUnidadBase = 0.833333m;

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
    public async Task El_cuadre_ve_el_lote_en_vuelo_aunque_el_destino_sume_lo_que_debe()
    {
        EscenaDeTransferencia escena = await MontarAsync(721, "CDT", "PorLote");
        EscenaDeTransferencia otro = await ConOtroArticuloAsync(escena, 722, "PorLote");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 10m, lote: "L-1");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 3m, 12m, lote: "L-2");
        await otro.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 7m, lote: "L-1");

        Guid transferenciaId;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            Resultado<TransferenciaDto> alta = await escena.IntentarAbrirAsync(
                modulo,
                escena.AlmacenA,
                escena.AlmacenB,
                [
                    escena.Linea(escena.UbicacionA, escena.UbicacionB, 2m, lote: "L-1"),
                    escena.Linea(escena.UbicacionA, escena.UbicacionB, 1m, lote: "L-2"),
                ]);

            alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

            transferenciaId = alta.Valor.Id;
        }

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(transferenciaId))
        {
            (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(envio)).Estado.ShouldBe("Enviada");
        }

        CuadreDeLasExistencias limpio = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        limpio.ExistenciasEnTransitoComparadas.ShouldBe(2, "un lote en vuelo por línea, cada uno con su clave");
        limpio.ValoracionesEnTransitoComparadas.ShouldBe(1, "las dos líneas van al mismo artículo y almacén");
        limpio.Descuadres.ShouldBeEmpty("recién enviada, todo cuadra: lo de abajo lo pone el caso");

        decimal enVuelo = (await escena.LaValoracionDeAsync(postgres, escena.AlmacenB)).ValorEnTransito.Cantidad;

        enVuelo.ShouldBeGreaterThan(0m, "el valor viaja con la línea (ADR-0053 §2)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        // LA PAREJA DEL ARTÍCULO: dos lotes «L-1» en la empresa, uno por artículo.
        (await contexto.Lotes.CountAsync(lote => lote.Codigo == "L-1"))
            .ShouldBe(2, "el otro artículo tiene su «L-1», y la línea no lo lleva");

        Guid l1 = await LoteDeAsync(contexto, escena, "L-1");
        Guid l2 = await LoteDeAsync(contexto, escena, "L-2");

        Existencia[] enElDestino = await contexto.Existencias
            .AsNoTracking()
            .Where(viva => viva.AlmacenId == escena.AlmacenB)
            .ToArrayAsync();

        Guid filaDeL1 = enElDestino.Single(viva => viva.LoteId == l1).Id;
        Guid filaDeL2 = enElDestino.Single(viva => viva.LoteId == l2).Id;

        // EL TRÁNSITO SE ESTROPEA EN UNA TRANSACCIÓN QUE SE DESHACE, y el cuadre corre dentro de ella.
        await using IDbContextTransaction estropeando = await contexto.Database.BeginTransactionAsync();

        decimal antes = await LoQueVuelaHaciaAsync(contexto, escena.AlmacenB);

        // Una unidad en vuelo del L-1 pasa al L-2, en las filas del destino.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET en_transito = en_transito - 1 WHERE id = {0}", filaDeL1)).ShouldBe(1);

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET en_transito = en_transito + 1 WHERE id = {0}", filaDeL2)).ShouldBe(1);

        // Y la valoración del destino gana una unidad y un euro que no viajan en ninguna línea.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.valoraciones " +
            "SET en_transito = en_transito + 1, valor_en_transito = valor_en_transito + 1 " +
            "WHERE empresa_id = {0} AND articulo_id = {1} AND almacen_id = {2}",
            escena.EmpresaId,
            escena.ArticuloId,
            escena.AlmacenB)).ShouldBe(1);

        // LA PAREJA: sumado por hueco, el destino espera lo mismo que antes. Lo que salga abajo no lo
        // encuentra la suma, sino el lote de la clave.
        (await LoQueVuelaHaciaAsync(contexto, escena.AlmacenB))
            .ShouldBe(antes, "el caso estropea sin cambiar lo que vuela hacia el hueco");

        CuadreDeLasExistencias sucio = await LasExistencias.CuadrarEnAsync(contexto, escena.EmpresaId);

        sucio.ExistenciasEnTransitoComparadas.ShouldBe(2);
        sucio.ValoracionesEnTransitoComparadas.ShouldBe(1);

        Hallados(sucio, escena).ShouldBe(
            [
                new Hallado("existencia-en-transito", escena.UbicacionB, l1, null, null, 2m, 1m, 1),
                new Hallado("existencia-en-transito", escena.UbicacionB, l2, null, null, 1m, 2m, 1),
                new Hallado("valoracion-en-transito-cantidad", null, null, null, null, 3m, 4m, 1),
                new Hallado("valoracion-en-transito-valor", null, null, null, null, enVuelo, enVuelo + 1m, 1),
            ],
            ignoreOrder: true,
            customMessage: "las cuatro, las del tránsito, cada existencia con su lote, y ninguna más");

        await estropeando.RollbackAsync();

        // Y DESHECHO, OTRA VEZ LIMPIO: lo que el cuadre encontró era lo que el caso puso.
        CuadreDeLasExistencias otraVez = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        otraVez.ExistenciasEnTransitoComparadas.ShouldBe(2);
        otraVez.ValoracionesEnTransitoComparadas.ShouldBe(1);
        otraVez.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_cuadre_ve_la_serie_en_vuelo_aunque_el_destino_sume_lo_que_debe()
    {
        EscenaDeTransferencia escena = await MontarAsync(724, "CDS", "PorNumeroSerie");
        EscenaDeTransferencia otro = await ConOtroArticuloAsync(escena, 725, "PorNumeroSerie");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 10m, serie: "S-1");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 12m, serie: "S-2");
        await otro.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 9m, serie: "S-1");

        // LA S-2 SE VA, y deja su número sin unidades en ningún sitio: es la serie que el caso pone en
        // la fila en vuelo, y con unidades en A el índice de la serie no lo dejaría.
        await SacarLaSerieAsync(escena, "S-2");

        (await escena.EnviarAsync(postgres, 1m, serie: "S-1")).Estado.ShouldBe("Enviada");

        CuadreDeLasExistencias limpio = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        limpio.ExistenciasEnTransitoComparadas.ShouldBe(1);
        limpio.ValoracionesEnTransitoComparadas.ShouldBe(1);
        limpio.Descuadres.ShouldBeEmpty("recién enviada, todo cuadra: la «S-1» del otro artículo no vuela");

        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        // LA PAREJA DEL ARTÍCULO: dos números «S-1» en la empresa, uno por artículo.
        (await contexto.NumerosDeSerie.CountAsync(serie => serie.Numero == "S-1"))
            .ShouldBe(2, "el otro artículo tiene su «S-1», y la línea no lo lleva");

        Guid s1 = await SerieDeAsync(contexto, escena, "S-1");
        Guid s2 = await SerieDeAsync(contexto, escena, "S-2");

        Guid filaEnVuelo = (await contexto.Existencias
            .AsNoTracking()
            .SingleAsync(viva => viva.AlmacenId == escena.AlmacenB)).Id;

        await using IDbContextTransaction estropeando = await contexto.Database.BeginTransactionAsync();

        decimal antes = await LoQueVuelaHaciaAsync(contexto, escena.AlmacenB);

        // La fila en vuelo pasa a ser de la S-2, que no ha salido de ningún sitio.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET numero_de_serie_id = {0} WHERE id = {1}", s2, filaEnVuelo))
            .ShouldBe(1);

        // LA PAREJA: el hueco espera lo mismo que antes, y la valoración no se ha tocado.
        (await LoQueVuelaHaciaAsync(contexto, escena.AlmacenB))
            .ShouldBe(antes, "el caso estropea sin cambiar lo que vuela hacia el hueco");

        CuadreDeLasExistencias sucio = await LasExistencias.CuadrarEnAsync(contexto, escena.EmpresaId);

        sucio.ExistenciasEnTransitoComparadas.ShouldBe(2, "la serie que dice la línea y la que dice la fila");
        sucio.ValoracionesEnTransitoComparadas.ShouldBe(1);

        Hallados(sucio, escena).ShouldBe(
            [
                new Hallado("existencia-en-transito", escena.UbicacionB, null, s1, null, 1m, 0m, 0),
                new Hallado("existencia-en-transito", escena.UbicacionB, null, s2, null, 0m, 1m, 1),
            ],
            ignoreOrder: true,
            customMessage: "las dos claves de la serie, cada una con lo suyo, y ninguna más");

        await estropeando.RollbackAsync();

        CuadreDeLasExistencias otraVez = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        otraVez.ExistenciasEnTransitoComparadas.ShouldBe(1);
        otraVez.ValoracionesEnTransitoComparadas.ShouldBe(1);
        otraVez.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task Lo_que_vuela_cuadra_redondeado_y_sin_valor_y_un_borrador_no_vuela()
    {
        EscenaDeTransferencia escena = await MontarAsync(723, "CDR", "Ninguna");

        // A COSTE CERO: lo que sale de A no vale nada, y vuela sin valor.
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 0m);

        Guid enviadaId;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            Resultado<TransferenciaDto> alta = await escena.IntentarAbrirAsync(
                modulo,
                escena.AlmacenA,
                escena.AlmacenB,
                [new LineaDeTransferenciaDto(
                    escena.UbicacionA, escena.UbicacionB, escena.ArticuloId, Tecleado, escena.UnidadId, Factor)]);

            alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

            enviadaId = alta.Valor.Id;
        }

        using (HttpResponseMessage envio = await escena.EnviarPorLaApiAsync(enviadaId))
        {
            (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(envio)).Estado.ShouldBe("Enviada");
        }

        // Y OTRA DE A A B QUE SE QUEDA EN BORRADOR: su línea no ha salido, y no vuela.
        Guid borradorId = await escena.AbrirAsync(postgres, 1m);

        (await escena.LaTransferenciaAsync(postgres, borradorId)).Estado.ShouldBe(EstadoDeTransferencia.Borrador);

        // LAS PAREJAS. El producto no cabe en el libro, y lo guardado es el redondeado; la valoración
        // del destino tiene cantidad en vuelo, y nada de valor.
        (Tecleado * Factor).ShouldBe(0.8333325m, "siete decimales: el redondeo tiene que hacer algo");

        (await LasExistencias.VivasAsync(postgres, escena.EmpresaId))
            .Single(viva => viva.AlmacenId == escena.AlmacenB)
            .EnTransito.ShouldBe(EnUnidadBase, "seis decimales, alejándose del cero, como el libro");

        Valoracion delDestino = await escena.LaValoracionDeAsync(postgres, escena.AlmacenB);

        delDestino.EnTransito.ShouldBe(EnUnidadBase);
        delDestino.ValorEnTransito.Cantidad.ShouldBe(0m, "lo que entró a coste cero vuela sin valor");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        cuadre.ExistenciasEnTransitoComparadas.ShouldBe(1, "la clave de la enviada, y no la del borrador");
        cuadre.ValoracionesEnTransitoComparadas.ShouldBe(1, "la del destino, aunque no tenga valor en vuelo");
        cuadre.Descuadres.ShouldBeEmpty("lo enviado cuenta como el libro, y lo que no ha salido no cuenta");
    }

    /// <summary>Lo que vuela hacia un almacén según sus filas vivas, sumado sin el lote ni la serie.</summary>
    private static Task<decimal> LoQueVuelaHaciaAsync(InventarioDbContext contexto, Guid almacenId) =>
        contexto.Existencias
            .AsNoTracking()
            .Where(viva => viva.AlmacenId == almacenId)
            .SumAsync(viva => viva.EnTransito);

    /// <summary>El lote del artículo de la escena con este código.</summary>
    private static async Task<Guid> LoteDeAsync(InventarioDbContext contexto, EscenaDeTransferencia escena, string codigo) =>
        (await contexto.Lotes
            .AsNoTracking()
            .SingleAsync(lote => lote.ArticuloId == escena.ArticuloId && lote.Codigo == codigo)).Id;

    /// <summary>El número de serie del artículo de la escena con este número.</summary>
    private static async Task<Guid> SerieDeAsync(InventarioDbContext contexto, EscenaDeTransferencia escena, string numero) =>
        (await contexto.NumerosDeSerie
            .AsNoTracking()
            .SingleAsync(serie => serie.ArticuloId == escena.ArticuloId && serie.Numero == numero)).Id;

    /// <summary>
    /// Los descuadres, sin el artículo ni el almacén, después de exigir que son los del artículo de la
    /// escena en B: el otro artículo no tiene nada en vuelo, y nada se ha estropeado en A.
    /// </summary>
    private static IEnumerable<Hallado> Hallados(CuadreDeLasExistencias cuadre, EscenaDeTransferencia escena)
    {
        cuadre.Descuadres.ShouldAllBe(descuadre =>
            descuadre.AlmacenId == escena.AlmacenB && descuadre.ArticuloId == escena.ArticuloId);

        return cuadre.Descuadres.Select(descuadre => new Hallado(
            descuadre.Que,
            descuadre.UbicacionId,
            descuadre.LoteId,
            descuadre.NumeroDeSerieId,
            descuadre.Mes,
            descuadre.Esperado,
            descuadre.Guardado,
            descuadre.Filas));
    }

    private static async Task<EscenaDeTransferencia> ConOtroArticuloAsync(
        EscenaDeTransferencia escena, int maestro, string trazabilidad)
    {
        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(escena.Cliente, maestro, trazabilidad);

        return escena with { ArticuloId = articuloId, UnidadId = unidadId };
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo, string trazabilidad)
    {
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }

    /// <summary>Saca de A la unidad de una serie con un ajuste confirmado por la API.</summary>
    private async Task SacarLaSerieAsync(EscenaDeTransferencia escena, string serie)
    {
        Guid ajusteId;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            ajusteId = await escena.AbrirUnAjusteAsync(
                modulo,
                escena.AlmacenA,
                new LineaDeAjusteDto(escena.UbicacionA, escena.ArticuloId, -1m, escena.UnidadId, 1m, null, null, serie));
        }

        using HttpResponseMessage confirmado = await escena.ConfirmarElAjustePorLaApiAsync(ajusteId);

        confirmado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(confirmado));
    }

    /// <summary>Un descuadre, sin el artículo ni el almacén, que son los mismos en todos.</summary>
    private sealed record Hallado(
        string Que,
        Guid? UbicacionId,
        Guid? LoteId,
        Guid? NumeroDeSerieId,
        DateOnly? Mes,
        decimal? Esperado,
        decimal? Guardado,
        long Filas);
}
