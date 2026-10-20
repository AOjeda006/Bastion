using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Lineas;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Unidades;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El factor de cada línea de la transferencia lo pone el servidor al abrir el borrador, y lo que se
/// guarda es lo que manda después (ADR-0068 §2 y §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Hasta el ítem 2.19 el factor lo traía la petición</b>, como el del ajuste hasta el 2.17. Ahora
/// la línea dice su unidad, y el alta busca la conversión de esa unidad a la base de su artículo con
/// la misma consulta que el ajuste: el par exacto, sin la inversa y sin encadenar. Sin ella, un
/// <c>422</c>; con ella retirada, un <c>409</c>. Los dos nombran la primera línea que falla, con las
/// dos unidades del par.
/// </para>
/// <para>
/// <b>Y el factor se copia, no se vuelve a preguntar.</b> Enviar y recibir usan el que se guardó al
/// abrir, y el inverso de una anulación copia el del original. Si se volviera a preguntar, una
/// transferencia en vuelo con su conversión retirada no podría llegar, y con su factor cambiado
/// entraría en el destino otra cantidad de la que salió del origen.
/// </para>
/// <para>
/// <b>Cada rechazo lleva su pareja</b>: la misma petición, con la conversión en regla, pasa.
/// </para>
/// <para>
/// <b>Semillas: del 901 al 906, del bloque del 2.19</b>, empresas y maestros con el mismo número. El
/// 899 y el 900 son de <c>LasExistenciasSonLaSumaDelLibroTests</c>, y los NIF 901 a 905 de la
/// importación llevan delante 33 000 000, así que no chocan. Del 907 al 910 son de
/// <c>LasLineasDeLaTransferenciaTests</c>, y del 911 al 913, de <c>LaTransferenciaDescartadaTests</c>.
/// Del 914 en adelante, libres. Las unidades nuevas llevan el código <c>K</c> y su semilla, como en
/// el ajuste.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElFactorDeLaTransferenciaTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    public async Task La_linea_en_la_unidad_base_lleva_factor_uno_y_la_otra_el_de_su_conversion()
    {
        EscenaDeTransferencia escena = await MontarAsync(901, "FTR-A");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
            escena.Cliente, "K901", 12m, escena.UnidadId);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 41m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Resultado<TransferenciaDto> alta = await IntentarAbrirAsync(
            escena, modulo, [Linea(escena, 5m, escena.UnidadId), Linea(escena, 3m, caja)]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        // EN LA MISMA PETICIÓN, para que se vea que el factor es de cada línea y no del documento.
        (await escena.LaTransferenciaAsync(postgres, alta.Valor.Id)).Lineas
            .Select(linea => linea.FactorAUnidadBase)
            .ShouldBe([1m, 12m], "la base no pregunta y vale 1; la caja vale lo que dice su conversión");

        (await modulo.EnviarAsync(alta.Valor.Id)).EsCorrecto.ShouldBeTrue();
        (await modulo.RecibirAsync(alta.Valor.Id, EscenaDeTransferencia.Hoy)).EsCorrecto.ShouldBeTrue();

        (await escena.FilasDelLibroAsync(postgres, alta.Valor.Id))
            .Select(fila => fila.CantidadEnUnidadBase)
            .ShouldBe(
                [-36m, -5m, 5m, 36m],
                "el libro cuenta en unidad base: cinco sueltas y tres cajas de doce, que salen y llegan");
    }

    [Fact]
    public async Task Una_unidad_sin_conversion_a_la_base_es_422_aunque_la_haya_al_reves()
    {
        EscenaDeTransferencia escena = await MontarAsync(902, "FTR-B");

        // AL REVÉS: de la base a la unidad nueva, que es la fila que NO sirve. El alta pide el par
        // exacto; si mirara la inversa, esta línea pasaría con el factor que no es.
        Guid alReves = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K902");
        await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, escena.UnidadId, alReves, 4m);

        Guid sinNada = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K902S");

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeTransferenciaDto[] lineas =
        [
            Linea(escena, 5m, escena.UnidadId),
            Linea(escena, 3m, alReves),
            Linea(escena, 1m, sinNada),
        ];

        // DOS LÍNEAS QUE FALLAN, y se nombra la primera por su orden.
        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, lineas),
            "transferencia-sin-conversion",
            TipoDeError.ReglaDeNegocio,
            new ConversionDeLaLineaDto(2, alReves, escena.UnidadId));

        // CON LA SUYA, la segunda pasa y la que falla es la tercera.
        await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, alReves, escena.UnidadId, 0.25m);

        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, lineas),
            "transferencia-sin-conversion",
            TipoDeError.ReglaDeNegocio,
            new ConversionDeLaLineaDto(3, sinNada, escena.UnidadId));

        (await TransferenciasDeAsync(escena)).ShouldBe(0, "un alta rechazada no deja borrador");

        // LA PAREJA: sin la tercera, el documento abre, con el factor de la fila en su sentido.
        Resultado<TransferenciaDto> alta = await IntentarAbrirAsync(escena, modulo, lineas[..2]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        (await escena.LaTransferenciaAsync(postgres, alta.Valor.Id)).Lineas
            .Select(linea => linea.FactorAUnidadBase)
            .ShouldBe([1m, 0.25m], "el de la unidad a la base, no el 4 de la base a la unidad");
    }

    [Fact]
    public async Task Una_conversion_retirada_es_409_y_antes_de_retirarla_la_misma_linea_pasa()
    {
        EscenaDeTransferencia escena = await MontarAsync(903, "FTR-C");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K903");
        ConversionUmDto conversion =
            await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 6m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        LineaDeTransferenciaDto linea = Linea(escena, 1m, caja);

        Resultado<TransferenciaDto> antes = await IntentarAbrirAsync(escena, modulo, [linea]);

        antes.EsCorrecto.ShouldBeTrue($"«{antes.Error?.Codigo}»");

        await RetirarAsync(escena, conversion);

        // RETIRADA NO ES AUSENTE: el par sigue existiendo y el código lo dice, para que quien
        // teclea sepa que lo que tiene que hacer es reincorporarla y no darla de alta otra vez.
        ExigirElError(
            await IntentarAbrirAsync(escena, modulo, [Linea(escena, 2m, escena.UnidadId), linea]),
            "transferencia-conversion-retirada",
            TipoDeError.Conflicto,
            new ConversionDeLaLineaDto(2, caja, escena.UnidadId));

        (await TransferenciasDeAsync(escena)).ShouldBe(1, "solo la de antes de retirarla");
    }

    [Fact]
    public async Task Enviar_y_recibir_usan_el_factor_que_se_guardo_aunque_la_conversion_cambie_o_se_retire()
    {
        EscenaDeTransferencia escena = await MontarAsync(904, "FTR-D");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K904");
        ConversionUmDto conversion =
            await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 12m);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 60m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Guid conCambio = await AbrirAsync(escena, modulo, Linea(escena, 2m, caja));
        Guid conRetirada = await AbrirAsync(escena, modulo, Linea(escena, 3m, caja));

        await CambiarElFactorAsync(escena, conversion, 10m);

        (await modulo.EnviarAsync(conCambio)).EsCorrecto.ShouldBeTrue();
        (await modulo.RecibirAsync(conCambio, EscenaDeTransferencia.Hoy)).EsCorrecto.ShouldBeTrue();

        (await escena.FilasDelLibroAsync(postgres, conCambio))
            .Select(fila => fila.CantidadEnUnidadBase)
            .ShouldBe([-24m, 24m], "dos cajas de las doce con que se abrió, no de las diez de ahora");

        await RetirarAsync(escena, conversion);

        Resultado<TransferenciaDto> enviada = await modulo.EnviarAsync(conRetirada);

        enviada.EsCorrecto.ShouldBeTrue($"«{enviada.Error?.Codigo}»: enviar no vuelve a preguntar");

        Resultado<TransferenciaDto> recibida = await modulo.RecibirAsync(conRetirada, EscenaDeTransferencia.Hoy);

        recibida.EsCorrecto.ShouldBeTrue($"«{recibida.Error?.Codigo}»: recibir tampoco");

        (await escena.FilasDelLibroAsync(postgres, conRetirada))
            .Select(fila => fila.CantidadEnUnidadBase)
            .ShouldBe([-36m, 36m], "tres cajas de doce: la conversión retirada no se mira");
    }

    [Fact]
    public async Task El_inverso_copia_el_factor_del_original_aunque_su_conversion_se_haya_retirado()
    {
        EscenaDeTransferencia escena = await MontarAsync(905, "FTR-E");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K905");
        ConversionUmDto conversion =
            await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 12m);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 36m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        // ENVIADA, el inverso solo devuelve al origen lo que salió.
        Guid transferenciaId = await AbrirAsync(escena, modulo, Linea(escena, 3m, caja));

        (await modulo.EnviarAsync(transferenciaId)).EsCorrecto.ShouldBeTrue();

        await RetirarAsync(escena, conversion);

        IReadOnlyList<MovimientoStock> filas = await ExigirElInversoAsync(escena, modulo, transferenciaId, caja);

        filas.Select(fila => (fila.AlmacenId, fila.CantidadEnUnidadBase))
            .ShouldBe([(escena.AlmacenA, 36m)], "vuelve al origen lo que salió, y el saldo de A es el de antes");
    }

    [Fact]
    public async Task El_inverso_copia_el_factor_del_original_aunque_su_conversion_haya_cambiado()
    {
        EscenaDeTransferencia escena = await MontarAsync(906, "FTR-F");

        Guid caja = await LosMaestrosPorLaApi.CrearUnidadAsync(escena.Cliente, "K906");
        ConversionUmDto conversion =
            await LosMaestrosPorLaApi.CrearConversionAsync(escena.Cliente, caja, escena.UnidadId, 12m);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 36m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        // RECIBIDA, el inverso niega las dos patas.
        Guid transferenciaId = await AbrirAsync(escena, modulo, Linea(escena, 3m, caja));

        (await modulo.EnviarAsync(transferenciaId)).EsCorrecto.ShouldBeTrue();
        (await modulo.RecibirAsync(transferenciaId, EscenaDeTransferencia.Hoy)).EsCorrecto.ShouldBeTrue();

        await CambiarElFactorAsync(escena, conversion, 10m);

        IReadOnlyList<MovimientoStock> filas = await ExigirElInversoAsync(escena, modulo, transferenciaId, caja);

        filas.Select(fila => (fila.AlmacenId, fila.CantidadEnUnidadBase))
            .ShouldBe(
                [(escena.AlmacenB, -36m), (escena.AlmacenA, 36m)],
                "sale del destino lo que entró y vuelve al origen: las dos puntas, como antes de enviar");
    }

    /// <summary>
    /// Anula la transferencia de tres cajas de doce y exige que la línea del inverso lleve el mismo
    /// factor y la cantidad negada.
    /// </summary>
    /// <returns>Las filas del libro del inverso, de la salida a la entrada.</returns>
    private async Task<IReadOnlyList<MovimientoStock>> ExigirElInversoAsync(
        EscenaDeTransferencia escena, ElModuloDeInventario modulo, Guid transferenciaId, Guid caja)
    {
        Resultado<AnulacionDeTransferenciaDto> anulacion =
            await modulo.AnularLaTransferenciaAsync(transferenciaId, "Se equivocó de almacén");

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»: anular no vuelve a preguntar");

        Transferencia inverso = await escena.LaTransferenciaAsync(postgres, anulacion.Valor.Inverso.Id);
        LineaDeTransferencia inversa = inverso.Lineas.ShouldHaveSingleItem();

        inversa.UnidadIntroducidaId.ShouldBe(caja);
        inversa.CantidadIntroducida.ShouldBe(-3m);
        inversa.FactorAUnidadBase.ShouldBe(12m, "el del original, no el de la conversión de ahora");

        return await escena.FilasDelLibroAsync(postgres, anulacion.Valor.Inverso.Id);
    }

    private static LineaDeTransferenciaDto Linea(EscenaDeTransferencia escena, decimal cantidad, Guid unidadId) =>
        new(escena.UbicacionA, escena.UbicacionB, escena.ArticuloId, cantidad, unidadId);

    private static Task<Resultado<TransferenciaDto>> IntentarAbrirAsync(
        EscenaDeTransferencia escena,
        ElModuloDeInventario modulo,
        IReadOnlyList<LineaDeTransferenciaDto> lineas) =>
        escena.IntentarAbrirAsync(modulo, escena.AlmacenA, escena.AlmacenB, lineas);

    private static async Task<Guid> AbrirAsync(
        EscenaDeTransferencia escena, ElModuloDeInventario modulo, LineaDeTransferenciaDto linea)
    {
        Resultado<TransferenciaDto> alta = await IntentarAbrirAsync(escena, modulo, [linea]);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    private static void ExigirElError(
        Resultado<TransferenciaDto> alta, string codigo, TipoDeError tipo, ConversionDeLaLineaDto actual)
    {
        alta.EsCorrecto.ShouldBeFalse($"tenía que salir «{codigo}»");

        ErrorDeOperacion error = alta.Error.ShouldNotBeNull();

        error.Codigo.ShouldBe(codigo, error.Mensaje);
        error.Tipo.ShouldBe(tipo);
        error.Actual.ShouldBe(actual, "la línea que falla, desde uno, y las dos unidades del par que no está");
    }

    private static async Task RetirarAsync(EscenaDeTransferencia escena, ConversionUmDto conversion)
    {
        string recurso = $"{LosMaestrosPorLaApi.Conversiones}/{conversion.Id}";

        using HttpResponseMessage retirada = await escena.Cliente.AccionarAsync(
            recurso, $"{recurso}/retirada", HttpMethod.Post);

        retirada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(retirada));
    }

    private static async Task CambiarElFactorAsync(
        EscenaDeTransferencia escena, ConversionUmDto conversion, decimal factor)
    {
        using HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            $"{LosMaestrosPorLaApi.Conversiones}/{conversion.Id}", new ModificarConversionUmDto { Factor = factor });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        (await cambio.Content.ReadFromJsonAsync<ConversionUmDto>())!.Factor.ShouldBe(factor);
    }

    private async Task<int> TransferenciasDeAsync(EscenaDeTransferencia escena)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Transferencias.CountAsync();
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
