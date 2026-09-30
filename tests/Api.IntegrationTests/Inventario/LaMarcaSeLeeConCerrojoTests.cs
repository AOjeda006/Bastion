using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Bastion.Inventario.Contracts.Ajustes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La marca del artículo cambia con el primer movimiento en vuelo, y ninguno de los dos se cuela:
/// la carrera del ADR-0048 §4, en los dos órdenes y con dos transacciones de verdad.
/// </summary>
/// <remarks>
/// <para>
/// <b>Catálogo pregunta «¿tiene movimientos?» y después cambia; Inventario lee la marca y después
/// confirma.</b> Sin cerrojos, cada uno decide con lo que el otro todavía no ha confirmado: el
/// artículo cambia de marca con un movimiento dentro, o el movimiento entra con la marca vieja.
/// Catálogo toma la fila del artículo con <c>FOR NO KEY UPDATE</c> antes de preguntar, e Inventario
/// la lee con <c>FOR SHARE</c> al confirmar. Las dos cláusulas chocan, así que el segundo espera al
/// primero y decide con lo que el primero dejó.
/// </para>
/// <para>
/// <b>Cuando Catálogo va primero, lo hace un doble a medida, y con piezas de verdad</b>: el cerrojo
/// es el de <see cref="CerrojoDeArticulos"/>, y el cambio, el del dominio del artículo, sobre el
/// mismo contexto y en la misma transacción. Lo que no hay es <c>ModificarArticulo</c>, porque para
/// en medio hace falta la transacción en la mano, y el caso de uso la toma él. Ese contexto no
/// lleva la traza de la API, que se engancha al montar el contenedor, y el caso no la mira.
/// </para>
/// <para>
/// <b>El que va primero se para con el cerrojo tomado y, si puede, sin nada escrito.</b> Catálogo
/// se para antes del <c>UPDATE</c>, así que quien espera solo puede estar esperando al cerrojo.
/// La confirmación no se puede parar a medias: se para entera y sin confirmar, y lo que ha escrito
/// son tablas de Inventario que el <c>PUT</c> no toca. Lo único que comparten es la fila del
/// artículo.
/// </para>
/// <para>
/// <b>Semillas: las empresas, del 581 al 583; los maestros de instalación, del 585 al 587.</b> El
/// tramo anterior, del 572 al 580, es de <c>ElLoteVaConSuArticuloTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaMarcaSeLeeConCerrojoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string NoCasa = "ajuste-trazabilidad-no-casa";

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
    /// Un borrador escrito con la marca de antes no se confirma: la confirmación vuelve a leerla.
    /// </summary>
    /// <remarks>
    /// Es la otra mitad de <c>LaMarcaNoCambiaConMovimientosTests.Un_borrador_no_es_un_movimiento</c>:
    /// allí la marca cambia con un borrador dentro, y aquí ese borrador ya no casa.
    /// </remarks>
    [Fact]
    public async Task Un_borrador_escrito_con_la_marca_de_antes_no_se_confirma()
    {
        EscenaTrazable escena = await MontarAsync(581, "MCA", 585);

        Guid borrador = await escena.AbrirAsync(
            postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 4m)]);

        using (HttpResponseMessage cambio = await CambiarLaMarcaAsync(escena, "PorLote"))
        {
            cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        }

        using HttpResponseMessage rechazo = await escena.ConfirmarPorLaApiAsync(borrador);

        rechazo.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(rechazo));
        (await rechazo.Content.ReadAsStringAsync()).ShouldContain("/errors/" + NoCasa);

        await ExigirQueNoSeHaMovidoNadaAsync(escena);
    }

    /// <summary>
    /// Catálogo tiene la fila del artículo para cambiar la marca, y la confirmación que llega
    /// después espera, lee la marca nueva y se rechaza.
    /// </summary>
    [Fact]
    public async Task La_confirmacion_espera_al_cambio_de_marca_que_ya_estaba_dentro_y_lo_ve()
    {
        EscenaTrazable escena = await MontarAsync(582, "MCB", 586);

        Guid borrador = await escena.AbrirAsync(
            postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 4m)]);

        await using CatalogoDbContext catalogo = postgres.AbrirCatalogo(escena.EmpresaId);
        await using ElModuloDeInventario inventario = new(postgres, escena.EmpresaId);

        await using (IDbContextTransaction cambiando = await catalogo.Database.BeginTransactionAsync())
        {
            (await new CerrojoDeArticulos(catalogo, new InquilinoFijo(escena.EmpresaId))
                    .TomarEnExclusivaAsync(escena.ArticuloId, CancellationToken.None))
                .ShouldBeTrue("el artículo es de la empresa del caso");

            int procesoDeCatalogo = ((NpgsqlConnection)catalogo.Database.GetDbConnection()).ProcessID;

            Task<Resultado<AjusteDto>> confirmacion = inventario.ConfirmarAsync(borrador);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                procesoDeCatalogo,
                confirmacion,
                "el cambio de marca",
                "ha leído la marca sin esperar a que Catálogo acabara de cambiarla");

            // AHORA SÍ ESCRIBE, con la confirmación ya esperando: el cambio que Catálogo habría
            // hecho después de preguntar y no encontrar movimientos.
            Articulo articulo = await catalogo.Articulos.SingleAsync(fila => fila.Id == escena.ArticuloId);

            articulo.Modificar(
                articulo.Descripcion,
                articulo.Tipo,
                Trazabilidad.PorLote,
                articulo.ImpuestoPorDefectoId,
                articulo.CategoriaId);

            await catalogo.SaveChangesAsync();
            await cambiando.CommitAsync();

            Resultado<AjusteDto> resultado = await confirmacion.WaitAsync(TimeSpan.FromSeconds(30));

            resultado.EsCorrecto.ShouldBeFalse("ha leído la marca vieja, la de después del cerrojo no");
            resultado.Error!.Codigo.ShouldBe(NoCasa);
        }

        (await LeerAsync(escena)).Trazabilidad.ShouldBe("PorLote");

        await ExigirQueNoSeHaMovidoNadaAsync(escena);
    }

    /// <summary>
    /// La confirmación tiene la marca leída con cerrojo, y el cambio de marca que llega después
    /// espera, ve el movimiento y se rechaza.
    /// </summary>
    [Fact]
    public async Task El_cambio_de_marca_espera_a_la_confirmacion_que_ya_estaba_dentro_y_la_ve()
    {
        EscenaTrazable escena = await MontarAsync(583, "MCC", 587);

        Guid borrador = await escena.AbrirAsync(
            postgres, escena.AlmacenA, [escena.Entrada(escena.UbicacionA1, 4m)]);

        await using ElModuloDeInventario inventario = new(postgres, escena.EmpresaId);

        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await inventario.ConfirmarYQuedarseDentroAsync(borrador);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            Task<HttpResponseMessage> cambio = CambiarLaMarcaAsync(escena, "PorLote");

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                inventario.ProcesoDeLaBase,
                cambio,
                "la confirmación en vuelo",
                "ha preguntado por los movimientos sin ver el que la confirmación estaba a punto de escribir");

            await enVuelo.CommitAsync();

            using HttpResponseMessage rechazo = await cambio.WaitAsync(TimeSpan.FromSeconds(30));

            rechazo.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(rechazo));
            (await rechazo.Content.ReadAsStringAsync())
                .ShouldContain("/errors/articulo-trazabilidad-con-movimientos");
        }

        (await LeerAsync(escena)).Trazabilidad.ShouldBe("Ninguna");
    }

    private async Task<EscenaTrazable> MontarAsync(int semilla, string codigo, int maestro)
    {
        EscenaTrazable escena = await EscenaTrazable.MontarAsync(_api, semilla, codigo, maestro, "Ninguna");

        _clientes.Add(escena.Cliente);

        return escena;
    }

    private static string Ruta(EscenaTrazable escena) => $"{LosMaestrosPorLaApi.Articulos}/{escena.ArticuloId}";

    private static async Task<ArticuloDto> LeerAsync(EscenaTrazable escena) =>
        (await escena.Cliente.GetFromJsonAsync<ArticuloDto>(Ruta(escena)))!;

    /// <summary>El <c>PUT</c> de la ficha, igual salvo la marca, citando la versión que acaba de leer.</summary>
    private static async Task<HttpResponseMessage> CambiarLaMarcaAsync(EscenaTrazable escena, string marca)
    {
        ArticuloDto articulo = await LeerAsync(escena);

        return await escena.Cliente.ModificarAsync(
            Ruta(escena),
            new ModificarArticuloDto
            {
                Descripcion = articulo.Descripcion,
                Tipo = articulo.Tipo,
                Trazabilidad = marca,
                ImpuestoPorDefectoId = articulo.ImpuestoPorDefectoId,
                CategoriaId = articulo.CategoriaId,
            });
    }

    private async Task ExigirQueNoSeHaMovidoNadaAsync(EscenaTrazable escena)
    {
        (await LasExistencias.LibroAsync(postgres, escena.EmpresaId)).ShouldBeEmpty();
        (await escena.ContadorAsync()).ShouldBe(0, "el rechazo devolvió su número a la serie");
    }
}
