using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El descarte del borrador contra la base (ADR-0065 §5): la marca, el filtro que lo esconde de
/// todo, y la versión antes que el estado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que solo se ve contra la base es el filtro.</b> El dominio ya dice que un descartado no se
/// confirma; lo que dice esto es que no se llega a preguntar, porque nadie lo encuentra: confirmar y
/// anular contestan el <c>404</c> de un ajuste que no existe, y no el <c>409</c> de uno que está en
/// otro estado ni el <c>500</c> del dominio. Y que la fila se queda, con su marca y sin número.
/// </para>
/// <para>
/// <b>Cada operación va en un módulo nuevo</b>, que es un contexto nuevo, como una petición. Con el
/// mismo, el rastreador devolvería el ajuste que ya tiene en memoria y el filtro no llegaría a
/// ejercerse.
/// </para>
/// <para>
/// <b>Semillas: la 884, la 885 y la 886</b>, del bloque del 2.17 (su reparto está en la cabecera de
/// <c>ElFactorDelAjusteTests</c>). Del 896 al 898, libres.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElBorradorDescartadoTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    public async Task El_descartado_no_lo_encuentran_ni_la_ficha_ni_confirmar_ni_anular_ni_el_cierre()
    {
        EscenaDeTransferencia escena = await MontarAsync(884, "DSC-A");
        Guid ajusteId = await AbrirAsync(escena);

        // LA PAREJA DE CADA AUSENCIA, antes de descartar: vivo, el borrador se ve y para el cierre.
        (await SeVeAsync(escena, ajusteId)).ShouldBeTrue("recién abierto, el borrador se ve");
        (await HayBorradoresHoyAsync(escena)).ShouldBeTrue("un borrador con fecha dentro para el cierre");

        (await DescartarAsync(escena, ajusteId, await LaVersionAsync(escena, ajusteId)))
            .EsCorrecto.ShouldBeTrue();

        (await SeVeAsync(escena, ajusteId)).ShouldBeFalse(
            "ni el listado ni la ficha traen un borrador tirado: lo esconde el repositorio");

        ExigirElNoEncontrado(await ConfirmarAsync(escena, ajusteId), "confirmar no lo encuentra");
        ExigirElNoEncontrado(await AnularAsync(escena, ajusteId), "anular tampoco");

        (await HayBorradoresHoyAsync(escena)).ShouldBeFalse(
            "un borrador tirado no tiene por qué parar un cierre (ADR-0065 §5)");
        (await HayDocumentosHoyAsync(escena)).ShouldBeFalse(
            "ni cuenta como documento del periodo: no numeró y no movió nada");

        (await LaFilaAsync(ajusteId)).ShouldBe(
            "Borrador||descartado",
            "la fila se queda, en borrador, sin número y con su marca: no es un estado nuevo");
    }

    [Fact]
    public async Task Con_la_version_de_antes_de_confirmarlo_es_412_y_con_la_de_ahora_409()
    {
        EscenaDeTransferencia escena = await MontarAsync(885, "DSC-B");
        Guid ajusteId = await AbrirAsync(escena);
        VersionDeRecurso vista = await LaVersionAsync(escena, ajusteId);

        (await ConfirmarAsync(escena, ajusteId)).EsCorrecto.ShouldBeTrue();

        VersionDeRecurso ahora = await LaVersionAsync(escena, ajusteId);
        ahora.ShouldNotBe(vista, "confirmar cambia la fila, y con ella su testigo");

        // LA VERSIÓN ANTES QUE EL ESTADO (ADR-0057 §3): quien vio el borrador recibe el 412, que le
        // dice que lo que vio ya no es lo que hay, y no el 409 del estado que no ha visto.
        ErrorDeOperacion obsoleta = (await DescartarAsync(escena, ajusteId, vista)).Error.ShouldNotBeNull();
        obsoleta.Tipo.ShouldBe(TipoDeError.VersionObsoleta, obsoleta.Mensaje);

        ErrorDeOperacion confirmado = (await DescartarAsync(escena, ajusteId, ahora)).Error.ShouldNotBeNull();
        confirmado.Codigo.ShouldBe("ajuste-no-esta-en-borrador", confirmado.Mensaje);
        confirmado.Tipo.ShouldBe(TipoDeError.Conflicto);

        (await LaFilaAsync(ajusteId)).ShouldBe(
            "Confirmado|1|vivo", "ninguno de los dos rechazos escribe nada");
    }

    [Fact]
    public async Task Descartar_dos_veces_es_404_y_la_marca_es_la_primera()
    {
        EscenaDeTransferencia escena = await MontarAsync(886, "DSC-C");
        Guid ajusteId = await AbrirAsync(escena);
        VersionDeRecurso version = await LaVersionAsync(escena, ajusteId);

        (await DescartarAsync(escena, ajusteId, version)).EsCorrecto.ShouldBeTrue();
        string marca = await LaMarcaAsync(ajusteId);

        ExigirElNoEncontrado(
            await DescartarAsync(escena, ajusteId, version),
            "el segundo no lo encuentra, y por eso el borde no le pide clave de idempotencia");

        (await LaMarcaAsync(ajusteId)).ShouldBe(marca, "la segunda vez no repinta el instante");
    }

    private static void ExigirElNoEncontrado(Resultado resultado, string porQue)
    {
        ErrorDeOperacion error = resultado.Error.ShouldNotBeNull(porQue);

        error.Codigo.ShouldBe("ajuste-no-encontrado", $"{porQue}: {error.Mensaje}");
        error.Tipo.ShouldBe(TipoDeError.NoEncontrado);
    }

    private static void ExigirElNoEncontrado<T>(Resultado<T> resultado, string porQue)
    {
        ErrorDeOperacion error = resultado.Error.ShouldNotBeNull(porQue);

        error.Codigo.ShouldBe("ajuste-no-encontrado", $"{porQue}: {error.Mensaje}");
        error.Tipo.ShouldBe(TipoDeError.NoEncontrado);
    }

    private async Task<Guid> AbrirAsync(EscenaDeTransferencia escena)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        return await escena.AbrirUnAjusteAsync(
            modulo,
            escena.AlmacenA,
            new LineaDeAjusteDto(escena.UbicacionA, escena.ArticuloId, 4m, escena.UnidadId, 2m));
    }

    private async Task<Resultado> DescartarAsync(
        EscenaDeTransferencia escena, Guid ajusteId, VersionDeRecurso version)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        return await modulo.Descarte.EjecutarAsync(ajusteId, version, CancellationToken.None);
    }

    private async Task<Resultado<AjusteDto>> ConfirmarAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        return await modulo.ConfirmarAsync(ajusteId);
    }

    private async Task<Resultado<AnulacionDto>> AnularAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        return await modulo.AnularAsync(ajusteId, "No era este");
    }

    /// <summary>La versión del ajuste, como la publicaría su ficha.</summary>
    private async Task<VersionDeRecurso> LaVersionAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        Ajuste ajuste = await contexto.Ajustes.SingleAsync(fila => fila.Id == ajusteId);

        return new VersionesDeInventario(contexto).De(ajuste);
    }

    private async Task<bool> SeVeAsync(EscenaDeTransferencia escena, Guid ajusteId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Ajustes.AnyAsync(fila => fila.Id == ajusteId);
    }

    private async Task<bool> HayBorradoresHoyAsync(EscenaDeTransferencia escena)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await new LosDocumentosDeInventarioEnUnPeriodo(contexto).HayBorradoresEnAsync(
            escena.EmpresaId, EscenaDeTransferencia.Hoy, EscenaDeTransferencia.Hoy, CancellationToken.None);
    }

    private async Task<bool> HayDocumentosHoyAsync(EscenaDeTransferencia escena)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await new LosDocumentosDeInventarioEnUnPeriodo(contexto).HayDocumentosEnAsync(
            escena.EmpresaId, EscenaDeTransferencia.Hoy, EscenaDeTransferencia.Hoy, CancellationToken.None);
    }

    /// <summary>La fila tal cual, sin el filtro: estado, número y si lleva la marca.</summary>
    private async Task<string> LaFilaAsync(Guid ajusteId) =>
        (await ElLibro.TextosAsync(
            postgres,
            "SELECT estado || '|' || coalesce(numero::text, '') || '|' || " +
            "CASE WHEN descartado_en IS NULL THEN 'vivo' ELSE 'descartado' END " +
            $"FROM inventario.ajustes WHERE id = '{ajusteId}'"))
        .ShouldHaveSingleItem();

    /// <summary>El instante de la marca, en texto, para compararlo consigo mismo.</summary>
    private async Task<string> LaMarcaAsync(Guid ajusteId) =>
        (await ElLibro.TextosAsync(
            postgres, $"SELECT descartado_en::text FROM inventario.ajustes WHERE id = '{ajusteId}'"))
        .ShouldHaveSingleItem();

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
