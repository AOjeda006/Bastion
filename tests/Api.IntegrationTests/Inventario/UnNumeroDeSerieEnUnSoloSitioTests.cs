using System.Globalization;
using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Organizacion.Contracts.Series;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Un número de serie no puede estar en dos sitios a la vez, y lo sostiene el motor: un
/// <c>CHECK</c> en la fila y un índice único parcial entre filas (ADR-0048 §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada pieza tiene su caso, y el caso dice cuál salta.</b> Las dos se traducen al mismo
/// <c>422</c>, <c>numero-de-serie-en-existencias</c>, así que por la API no se distinguen. Por eso
/// los dos primeros casos confirman el mismo borrador dos veces: con <see cref="ElModuloDeInventario"/>,
/// que deja pasar la excepción del motor con su nombre, y después por la API, que la traduce. El
/// rechazo del motor deshace la transacción, así que el borrador sigue siéndolo para el segundo
/// intento.
/// </para>
/// <para>
/// <b>El índice solo mira donde hay</b> (<c>WHERE fisico &gt; 0</c>): una serie que salió de un
/// sitio puede entrar en otro, y es el contraste del caso del índice. Sin el filtro, la fila que se
/// quedó a cero seguiría ocupando el número.
/// </para>
/// <para>
/// <b>La anulación no tiene camino propio</b> (ADR-0046 §1, ADR-0048 §5): su inverso es un
/// movimiento más, y se para con las mismas dos piezas y con la del stock.
/// </para>
/// <para>
/// <b>Semillas: las empresas, del 560 al 565; los maestros de instalación, del 566 al 571.</b> El
/// tramo siguiente, del 572 al 580, es de <c>ElLoteVaConSuArticuloTests</c>, y el anterior, del
/// 540 al 549, de <c>LaMarcaNoCambiaConMovimientosTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class UnNumeroDeSerieEnUnSoloSitioTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Serie = "SN-0001";
    private const string EnExistencias = "/errors/numero-de-serie-en-existencias";

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
    public async Task La_misma_serie_dos_veces_en_el_mismo_hueco_la_para_el_check_y_sale_422()
    {
        EscenaTrazable escena = await MontarAsync(560, "NSA", 566);

        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 1m, serie: Serie)]);

        Guid otraVez = await escena.AbrirAsync(
            postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 1m, serie: Serie)]);

        // EN EL MISMO HUECO ES LA MISMA FILA, que pasaría a tener dos: lo para el CHECK, y no el
        // índice, que no choca con la propia fila.
        await ExigirQueElMotorLaParaAsync(escena, otraVez, "23514", "ck_existencias_numero_de_serie_como_mucho_una");

        await ExigirElRechazoDeLaApiAsync(escena, otraVez, EnExistencias);

        (await escena.ContadorAsync()).ShouldBe(1, "los dos rechazos devolvieron su número a la serie");

        Existencia unica = (await LasExistencias.VivasAsync(postgres, escena.EmpresaId)).ShouldHaveSingleItem();

        unica.Fisico.ShouldBe(1m);
    }

    [Fact]
    public async Task La_misma_serie_en_otro_hueco_la_para_el_indice_y_sale_422()
    {
        EscenaTrazable escena = await MontarAsync(561, "NSB", 567);

        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 1m, serie: Serie)]);

        Guid enOtroHueco = await escena.AbrirAsync(
            postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA2, 1m, serie: Serie)]);

        // EN OTRO HUECO ES OTRA FILA, con una unidad, que el CHECK no ve mal: lo para el índice.
        await ExigirQueElMotorLaParaAsync(escena, enOtroHueco, "23505", "ix_existencias_numero_de_serie_en_un_sitio");

        await ExigirElRechazoDeLaApiAsync(escena, enOtroHueco, EnExistencias);

        (await escena.ContadorAsync()).ShouldBe(1, "los dos rechazos devolvieron su número a la serie");

        Existencia unica = (await LasExistencias.VivasAsync(postgres, escena.EmpresaId)).ShouldHaveSingleItem(
            "la fila del otro hueco nació en la transacción rechazada y se fue con ella");

        unica.UbicacionId.ShouldBe(escena.UbicacionA1);
        unica.Fisico.ShouldBe(1m);
    }

    [Fact]
    public async Task Una_serie_que_salio_de_un_hueco_puede_entrar_en_otro()
    {
        EscenaTrazable escena = await MontarAsync(562, "NSC", 568);

        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 1m, serie: Serie)]);
        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Salida(escena.UbicacionA1, 1m, serie: Serie)]);

        // Es el contraste del caso del índice: el hueco de antes se quedó con su fila a cero, y el
        // índice solo mira las filas con algo.
        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA2, 1m, serie: Serie)]);

        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, escena.EmpresaId);

        vivas.Count.ShouldBe(2);
        vivas.Select(fila => fila.NumeroDeSerieId).Distinct().ShouldHaveSingleItem().ShouldNotBeNull();
        vivas.Single(fila => fila.UbicacionId == escena.UbicacionA1).Fisico.ShouldBe(0m);
        vivas.Single(fila => fila.UbicacionId == escena.UbicacionA2).Fisico.ShouldBe(1m);

        await ExigirQueCuadraAsync(escena, existencias: 2);
    }

    [Fact]
    public async Task Anular_la_salida_de_una_serie_que_ya_esta_en_otro_hueco_es_422()
    {
        EscenaTrazable escena = await MontarAsync(563, "NSD", 569);

        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 1m, serie: Serie)]);

        Guid salida = await escena.MoverAsync(
            postgres, escena.AlmacenA, [escena.Salida(escena.UbicacionA1, 1m, serie: Serie)]);

        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA2, 1m, serie: Serie)]);

        // EL INVERSO DE LA SALIDA LA DEVUELVE AL PRIMER HUECO, y la serie ya está en el segundo.
        using HttpResponseMessage rechazo = await escena.AnularPorLaApiAsync(salida);

        rechazo.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(rechazo));
        (await rechazo.Content.ReadAsStringAsync()).ShouldContain(EnExistencias);

        (await InversosDeAsync(salida)).ShouldBe(0);
        (await escena.ContadorAsync()).ShouldBe(3, "el número que tomó el inverso volvió a la serie");

        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, escena.EmpresaId);

        vivas.Single(fila => fila.UbicacionId == escena.UbicacionA1).Fisico.ShouldBe(0m);
        vivas.Single(fila => fila.UbicacionId == escena.UbicacionA2).Fisico.ShouldBe(1m);
    }

    [Fact]
    public async Task Anular_la_entrada_de_una_serie_que_ya_salio_es_stock_insuficiente()
    {
        EscenaTrazable escena = await MontarAsync(564, "NSE", 570);

        Guid entrada = await escena.MoverAsync(
            postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 1m, serie: Serie)]);

        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Salida(escena.UbicacionA1, 1m, serie: Serie)]);

        // EL INVERSO DE LA ENTRADA LA SACA OTRA VEZ, y ya no está: es la excepción de la R2, y la
        // para la restricción del stock, no la de la serie, que solo mira por arriba.
        using HttpResponseMessage rechazo = await escena.AnularPorLaApiAsync(entrada);

        rechazo.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(rechazo));
        (await rechazo.Content.ReadAsStringAsync()).ShouldContain("/errors/stock-insuficiente");

        (await InversosDeAsync(entrada)).ShouldBe(0);
        (await escena.ContadorAsync()).ShouldBe(2, "el número que tomó el inverso volvió a la serie");

        (await LasExistencias.VivasAsync(postgres, escena.EmpresaId)).ShouldHaveSingleItem().Fisico.ShouldBe(0m);
    }

    /// <summary>
    /// La misma serie entra en dos almacenes a la vez, con dos transacciones de verdad: la que llega
    /// segunda espera en el índice y recibe su <c>23505</c> (ADR-0048 §3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La serie ya existe antes de la carrera</b>: entró y salió del primer hueco. Es lo que
    /// hace que la única espera sea la del índice parcial. Con una serie nueva, la segunda esperaría
    /// antes, en el índice único de <c>numeros_de_serie</c>, al crear su fila, y el caso no sabría
    /// cuál de los dos la ha parado.
    /// </para>
    /// <para>
    /// <b>Y nada más las cruza.</b> Van por dos series de ajustes, así que no se esperan en el
    /// contador, y por dos almacenes, así que tampoco en la valoración. El ejercicio y la marca se
    /// leen con cerrojo compartido, que no espera a otro compartido.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_misma_serie_en_dos_almacenes_a_la_vez_entra_en_uno_solo()
    {
        EscenaTrazable escena = await MontarAsync(565, "NSF", 571);

        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 1m, serie: Serie)]);
        await escena.MoverAsync(postgres, escena.AlmacenA, [escena.Salida(escena.UbicacionA1, 1m, serie: Serie)]);

        SerieDto otraSerie = await escena.OtraSerieAsync("NSF-2");

        await using ElModuloDeInventario unos = new(postgres, escena.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, escena.EmpresaId);

        Guid alA = await escena.AbrirAsync(
            unos, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 1m, serie: Serie)]);

        Guid alB = await escena.AbrirAsync(
            otros, escena.AlmacenB, [escena.Entrada(escena.UbicacionB1, 1m, serie: Serie)], otraSerie.Id);

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
                "ha metido la serie en su almacén sin ver que la otra la estaba metiendo en el suyo");

            await enVuelo.CommitAsync();

            PostgresException choque = await Should.ThrowAsync<PostgresException>(
                () => laOtra.WaitAsync(TimeSpan.FromSeconds(30)));

            choque.SqlState.ShouldBe("23505", choque.MessageText);
            choque.ConstraintName.ShouldBe(
                "ix_existencias_numero_de_serie_en_un_sitio",
                "es el nombre que el borde traduce a 422: con otro, la segunda entrada saldría 500 o 412");
        }

        Existencia dondeEsta = (await LasExistencias.VivasAsync(postgres, escena.EmpresaId))
            .Where(fila => fila.Fisico > 0)
            .ShouldHaveSingleItem();

        dondeEsta.AlmacenId.ShouldBe(escena.AlmacenA);

        (await escena.ContadorAsync(otraSerie.Id)).ShouldBe(0, "la perdedora no gastó su número");

        await ExigirQueCuadraAsync(escena, existencias: 1);
    }

    private async Task<EscenaTrazable> MontarAsync(int semilla, string codigo, int maestro)
    {
        EscenaTrazable escena =
            await EscenaTrazable.MontarAsync(_api, semilla, codigo, maestro, "PorNumeroSerie");

        _clientes.Add(escena.Cliente);

        return escena;
    }

    /// <summary>Confirma con el módulo, que deja pasar la excepción del motor, y exige su nombre.</summary>
    private async Task ExigirQueElMotorLaParaAsync(
        EscenaTrazable escena, Guid ajusteId, string estado, string restriccion)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        PostgresException choque =
            await Should.ThrowAsync<PostgresException>(() => modulo.ConfirmarAsync(ajusteId));

        choque.SqlState.ShouldBe(estado, choque.MessageText);
        choque.ConstraintName.ShouldBe(restriccion);
    }

    private static async Task ExigirElRechazoDeLaApiAsync(EscenaTrazable escena, Guid ajusteId, string tipo)
    {
        using HttpResponseMessage rechazo = await escena.ConfirmarPorLaApiAsync(ajusteId);

        rechazo.StatusCode.ShouldBe(
            HttpStatusCode.UnprocessableContent,
            $"es una regla, no una carrera perdida ni un fallo. {await Escenario.Detalle(rechazo)}");

        (await rechazo.Content.ReadAsStringAsync()).ShouldContain(tipo);
    }

    private async Task ExigirQueCuadraAsync(EscenaTrazable escena, int existencias)
    {
        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(existencias);
        cuadre.Descuadres.ShouldBeEmpty();
    }

    /// <summary>Cuántos documentos apuntan a este como su original.</summary>
    private Task<long> InversosDeAsync(Guid ajusteId) => ElLibro.EscalarAsync<long>(
        postgres,
        string.Format(
            CultureInfo.InvariantCulture,
            "SELECT count(*) FROM inventario.ajustes WHERE anula_a_id = '{0}'",
            ajusteId));
}
