using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El descarte de la transferencia en borrador contra la base (ADR-0068 §5): la marca, el filtro que
/// la esconde de todo, y la versión antes que el estado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que solo se ve contra la base es el filtro</b>, como en el ajuste. El dominio ya dice que
/// una descartada no se envía; lo que dice esto es que no se llega a preguntar, porque nadie la
/// encuentra: enviar, recibir y anular contestan el <c>404</c> de una transferencia que no existe, y
/// no el <c>409</c> de una que está en otro estado ni el <c>500</c> del dominio. Y que la fila se
/// queda, con su marca y sin número.
/// </para>
/// <para>
/// <b>Cada operación va en un módulo nuevo</b>, que es un contexto nuevo, como una petición. Con el
/// mismo, el rastreador devolvería la transferencia que ya tiene en memoria y el filtro no llegaría a
/// ejercerse.
/// </para>
/// <para>
/// <b>Semillas: del 911 al 913</b>, del bloque del 2.19. Su reparto está en la cabecera de
/// <c>ElFactorDeLaTransferenciaTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaTransferenciaDescartadaTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    public async Task La_descartada_no_la_encuentran_ni_la_ficha_ni_enviar_ni_recibir_ni_anular_ni_el_cierre()
    {
        EscenaDeTransferencia escena = await MontarAsync(911, "DTR-A");
        Guid transferenciaId = await escena.AbrirAsync(postgres, 4m);

        // LA PAREJA DE CADA AUSENCIA, antes de descartar: viva, el borrador se ve y para el cierre.
        (await SeVeAsync(escena, transferenciaId)).ShouldBeTrue("recién abierta, el borrador se ve");
        (await HayBorradoresHoyAsync(escena)).ShouldBeTrue("un borrador con fecha dentro para el cierre");
        (await HayDocumentosHoyAsync(escena)).ShouldBeTrue("y cuenta como documento del periodo");

        (await DescartarAsync(escena, transferenciaId, await LaVersionAsync(escena, transferenciaId)))
            .EsCorrecto.ShouldBeTrue();

        (await SeVeAsync(escena, transferenciaId)).ShouldBeFalse(
            "ni el listado ni la ficha traen un borrador tirado: lo esconde el repositorio");

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            ExigirLaNoEncontrada(await modulo.EnviarAsync(transferenciaId), "enviar no la encuentra");
        }

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            ExigirLaNoEncontrada(
                await modulo.RecibirAsync(transferenciaId, EscenaDeTransferencia.Hoy), "recibir tampoco");
        }

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            ExigirLaNoEncontrada(
                await modulo.AnularLaTransferenciaAsync(transferenciaId, "No era esta"), "ni anular");
        }

        (await HayBorradoresHoyAsync(escena)).ShouldBeFalse(
            "un borrador tirado no tiene por qué parar un cierre (ADR-0068 §5, que enmienda el ADR-0053 §12)");
        (await HayDocumentosHoyAsync(escena)).ShouldBeFalse(
            "ni cuenta como documento del periodo: no numeró y no movió nada");

        (await LaFilaAsync(transferenciaId)).ShouldBe(
            "Borrador||descartado",
            "la fila se queda, en borrador, sin número y con su marca: no es un estado nuevo");
    }

    [Fact]
    public async Task Con_la_version_de_antes_de_enviarla_es_412_y_con_la_de_ahora_409()
    {
        EscenaDeTransferencia escena = await MontarAsync(912, "DTR-B");
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);
        Guid transferenciaId = await escena.AbrirAsync(postgres, 4m);
        VersionDeRecurso vista = await LaVersionAsync(escena, transferenciaId);

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            (await modulo.EnviarAsync(transferenciaId)).EsCorrecto.ShouldBeTrue();
        }

        VersionDeRecurso ahora = await LaVersionAsync(escena, transferenciaId);
        ahora.ShouldNotBe(vista, "enviar cambia la fila, y con ella su testigo");

        // LA VERSIÓN ANTES QUE EL ESTADO (ADR-0057 §3): quien vio el borrador recibe el 412, que le
        // dice que lo que vio ya no es lo que hay, y no el 409 del estado que no ha visto.
        ErrorDeOperacion obsoleta = (await DescartarAsync(escena, transferenciaId, vista)).Error.ShouldNotBeNull();
        obsoleta.Tipo.ShouldBe(TipoDeError.VersionObsoleta, obsoleta.Mensaje);

        ErrorDeOperacion enviada = (await DescartarAsync(escena, transferenciaId, ahora)).Error.ShouldNotBeNull();
        enviada.Codigo.ShouldBe("transferencia-no-esta-en-borrador", enviada.Mensaje);
        enviada.Tipo.ShouldBe(TipoDeError.Conflicto);

        (await LaFilaAsync(transferenciaId)).ShouldBe(
            "Enviada|1|vivo", "ninguno de los dos rechazos escribe nada");
    }

    [Fact]
    public async Task Descartar_dos_veces_es_404_y_la_marca_es_la_primera()
    {
        EscenaDeTransferencia escena = await MontarAsync(913, "DTR-C");
        Guid transferenciaId = await escena.AbrirAsync(postgres, 4m);
        VersionDeRecurso version = await LaVersionAsync(escena, transferenciaId);

        (await DescartarAsync(escena, transferenciaId, version)).EsCorrecto.ShouldBeTrue();
        string marca = await LaMarcaAsync(transferenciaId);

        ExigirLaNoEncontrada(
            await DescartarAsync(escena, transferenciaId, version),
            "la segunda no la encuentra, y por eso el borde no le pide clave de idempotencia");

        (await LaMarcaAsync(transferenciaId)).ShouldBe(marca, "la segunda vez no repinta el instante");
    }

    private static void ExigirLaNoEncontrada(Resultado resultado, string porQue)
    {
        ErrorDeOperacion error = resultado.Error.ShouldNotBeNull(porQue);

        error.Codigo.ShouldBe("transferencia-no-encontrada", $"{porQue}: {error.Mensaje}");
        error.Tipo.ShouldBe(TipoDeError.NoEncontrado);
    }

    private static void ExigirLaNoEncontrada<T>(Resultado<T> resultado, string porQue)
    {
        ErrorDeOperacion error = resultado.Error.ShouldNotBeNull(porQue);

        error.Codigo.ShouldBe("transferencia-no-encontrada", $"{porQue}: {error.Mensaje}");
        error.Tipo.ShouldBe(TipoDeError.NoEncontrado);
    }

    private async Task<Resultado> DescartarAsync(
        EscenaDeTransferencia escena, Guid transferenciaId, VersionDeRecurso version)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        return await modulo.DescarteDeTransferencia.EjecutarAsync(transferenciaId, version, CancellationToken.None);
    }

    /// <summary>La versión de la transferencia, como la publicaría su ficha.</summary>
    private async Task<VersionDeRecurso> LaVersionAsync(EscenaDeTransferencia escena, Guid transferenciaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        Transferencia transferencia = await contexto.Transferencias.SingleAsync(fila => fila.Id == transferenciaId);

        return new VersionesDeInventario(contexto).De(transferencia);
    }

    private async Task<bool> SeVeAsync(EscenaDeTransferencia escena, Guid transferenciaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        return await contexto.Transferencias.AnyAsync(fila => fila.Id == transferenciaId);
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
    private async Task<string> LaFilaAsync(Guid transferenciaId) =>
        (await ElLibro.TextosAsync(
            postgres,
            "SELECT estado || '|' || coalesce(numero::text, '') || '|' || " +
            "CASE WHEN descartado_en IS NULL THEN 'vivo' ELSE 'descartado' END " +
            $"FROM inventario.transferencias WHERE id = '{transferenciaId}'"))
        .ShouldHaveSingleItem();

    /// <summary>El instante de la marca, en texto, para compararlo consigo mismo.</summary>
    private async Task<string> LaMarcaAsync(Guid transferenciaId) =>
        (await ElLibro.TextosAsync(
            postgres,
            $"SELECT descartado_en::text FROM inventario.transferencias WHERE id = '{transferenciaId}'"))
        .ShouldHaveSingleItem();

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
