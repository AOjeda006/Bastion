using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La confirmación del recuento cuando llega a la vez que otra escritura (ADR-0055 §3 y ADR-0057):
/// otra confirmación del mismo recuento, y un ajuste que mueve una de sus claves.
/// </summary>
/// <remarks>
/// <para>
/// <b>En las dos carreras la primera ya ha escrito, y se admite</b> con las dos condiciones de la regla
/// de las carreras, porque lo que se prueba es lo que la segunda ve de lo que escribió la primera:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>el código distingue la causa</b>: la segunda confirmación es un <c>412</c>, y no el <c>409</c> del
/// teórico que daría si no hubiera esperado en la cabecera; la que llega con un ajuste en vuelo es un
/// <c>409</c> del teórico, y no el <c>200</c> que daría con el teórico de antes;
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>la mutación que quita el cerrojo, sola, pone el caso rojo</b>: sin el de la cabecera, la segunda
/// espera en el contador de la serie y sale con el <c>409</c> del teórico; sin la segunda lectura del
/// teórico, la que llega con el ajuste confirma con lo de antes.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>Semillas: el 807 y el 808</b>, empresas y maestros con el mismo número. El reparto del bloque del
/// 2.12 está en la cabecera de <c>ElPuertoDelArticuloContraLaBaseTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasCarrerasDeLaConfirmacionDelRecuentoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private static readonly TimeSpan s_plazo = TimeSpan.FromSeconds(30);

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

    /// <summary>
    /// Dos confirmaciones del mismo recuento a la vez: la segunda espera en la cabecera, lee la versión
    /// que dejó la primera y es un <c>412</c>; hay un número de recuento y un ajuste, no dos.
    /// </summary>
    [Fact]
    public async Task Dos_confirmaciones_a_la_vez_la_segunda_espera_en_la_cabecera_y_es_un_412()
    {
        EscenaDeRecuento escena = await MontarAsync(807, "RCK-V");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 7m);

        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);
        string huella = ficha.HuellaDelTeorico!;
        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);

        // TRANSACCIÓN 1: confirma, con la cabecera y la valoración bloqueadas, los dos números tomados y
        // el ajuste y su libro escritos, y NO suelta.
        (Resultado<RecuentoDto> primera, IDbContextTransaction enVuelo) =
            await modulo.ConfirmarElRecuentoYQuedarseDentroAsync(abierto.Id, etiqueta, huella);

        await using (enVuelo)
        {
            primera.EsCorrecto.ShouldBeTrue($"«{primera.Error?.Codigo}»");

            // TRANSACCIÓN 2: la misma confirmación por la API, con la misma versión y la misma huella.
            Task<HttpResponseMessage> segunda = escena.ConfirmarAsync(abierto.Id, etiqueta, huella);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                modulo.ProcesoDeLaBase,
                segunda,
                "la confirmación en vuelo",
                "ha confirmado sin esperar al cerrojo de la cabecera");

            await enVuelo.CommitAsync();

            using HttpResponseMessage respuesta = await segunda.WaitAsync(s_plazo);

            // SIN EL CERROJO DE LA CABECERA ESTO ES UN 409 DEL TEÓRICO: la segunda lee la versión de
            // antes, que casa, y se para en el contador; al soltarlo, el libro ya tiene el ajuste de la
            // primera y su huella ya no es la que trae.
            await EscenaDeRecuento.ExigirElProblemaAsync(
                respuesta, HttpStatusCode.PreconditionFailed, "version-obsoleta", "la segunda confirmación");
        }

        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(1, "un recuento, un número");
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes + 1, "y un ajuste");

        await using (InventarioDbContext contexto = postgres.AbrirInventario(de.EmpresaId))
        {
            (await contexto.Ajustes.CountAsync(ajuste => ajuste.RecuentoId == abierto.Id)).ShouldBe(1);
        }

        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(7m, "la diferencia, una vez");
    }

    /// <summary>
    /// Un ajuste que mueve una clave del recuento y no ha publicado: la confirmación espera en la
    /// valoración y, cuando el ajuste publica, es un <c>409</c> del teórico con lo que movió; con la
    /// huella de ahora confirma, y su ajuste mueve hasta lo contado.
    /// </summary>
    [Fact]
    public async Task Un_ajuste_en_vuelo_sobre_su_clave_frena_la_confirmacion_en_la_valoracion_y_es_un_409_del_teorico()
    {
        EscenaDeRecuento escena = await MontarAsync(808, "RCK-T");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        // EL AJUSTE VA EN OTRA SERIE: con la misma, la confirmación se pararía en su contador, antes
        // de la valoración, y el caso no vería el cerrojo que quiere ver.
        SerieDto otraSerie = await LosMaestrosPorLaApi.CrearSerieEnAsync(escena.Cliente, de.Ejercicio.Id, "RCK-T-AJ2");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 8m);

        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);

        Guid ajusteId = await de.AbrirUnAjusteAsync(
            modulo,
            de.AlmacenA,
            new LineaDeAjusteDto(de.UbicacionA, de.ArticuloId, 1m, de.UnidadId, 1m, 2.50m),
            otraSerie.Id);

        // TRANSACCIÓN 1: el ajuste confirmado, con la valoración bloqueada y su fila del libro escrita,
        // y SIN publicar.
        (Resultado<AjusteDto> ajuste, IDbContextTransaction enVuelo) = await modulo.ConfirmarYQuedarseDentroAsync(ajusteId);
        LineasEnConflictoDto cambiadas;

        await using (enVuelo)
        {
            ajuste.EsCorrecto.ShouldBeTrue($"«{ajuste.Error?.Codigo}»");

            // TRANSACCIÓN 2: la confirmación por la API, con la huella que sigue valiendo, porque el
            // ajuste no ha publicado. El teórico sin cerrojo casa, y se para en la valoración.
            Task<HttpResponseMessage> confirmacion =
                escena.ConfirmarAsync(abierto.Id, etiqueta, ficha.HuellaDelTeorico!);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                modulo.ProcesoDeLaBase,
                confirmacion,
                "el ajuste en vuelo",
                "ha confirmado sin esperar a la valoración de su clave");

            await enVuelo.CommitAsync();

            using HttpResponseMessage respuesta = await confirmacion.WaitAsync(s_plazo);

            // SIN LA SEGUNDA LECTURA DEL TEÓRICO ESTO ES UN 200, y el ajuste del recuento movería tres,
            // de cinco a ocho, sobre un físico que ya era seis.
            await EscenaDeRecuento.ExigirElProblemaAsync(
                respuesta, HttpStatusCode.Conflict, "recuento-teorico-cambiado", "confirmar con un ajuste en vuelo");

            cambiadas = await EscenaDeRecuento.LasLineasEnConflictoAsync(respuesta);
        }

        LineaDeRecuentoDto cambiada = cambiadas.Lineas.ShouldHaveSingleItem();

        cambiada.TeoricoAlContar.ShouldBe(5m);
        cambiada.Teorico.ShouldBe(6m, "lo que dejó el ajuste al publicar");
        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(0, "el 409 se deshace con sus números");

        // CON LA HUELLA DE AHORA, confirma, y su ajuste mueve lo que falta hasta lo contado.
        using HttpResponseMessage otraVez = await escena.ConfirmarAsync(abierto.Id, etiqueta, cambiadas.HuellaDelTeorico!);

        RecuentoDto confirmado = await EscenaDeTransferencia.LeerAsync<RecuentoDto>(otraVez);

        await using (InventarioDbContext contexto = postgres.AbrirInventario(de.EmpresaId))
        {
            Ajuste delRecuento = await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.Id == confirmado.AjusteId);

            delRecuento.Lineas.ShouldHaveSingleItem().CantidadIntroducida.ShouldBe(2m);
        }

        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(8m);
    }

    private async Task<EscenaDeRecuento> MontarAsync(int semilla, string codigo, string trazabilidad = "Ninguna")
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, semilla, codigo, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
