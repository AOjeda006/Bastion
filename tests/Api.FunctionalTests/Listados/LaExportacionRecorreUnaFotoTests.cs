using System.Text;
using System.Text.Json;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Listados;

/// <summary>
/// Cómo exporta el borde un listado: todas sus páginas, de una foto, con los criterios del listado,
/// y lo que el listado rechaza, antes del primer byte (ADR-0073).
/// </summary>
/// <remarks>
/// <para>
/// <b>En el carril rápido</b>, con un caso de uso de mentira que apunta lo que le piden, y una foto
/// de mentira que apunta cuándo se abre y cuándo se cierra. Que la foto sea de verdad una foto lo
/// dice <c>LaFotoDeLecturaTests</c>, contra un PostgreSQL; aquí se dice que todas las páginas se
/// piden con ella abierta.
/// </para>
/// <para>
/// <b>Los rechazos se miran dos veces</b>: primero, que no se ha escrito nada en la respuesta, que
/// es lo que permite contestar con un problema; después, ejecutando el resultado, que el problema
/// es el del listado.
/// </para>
/// </remarks>
public sealed class LaExportacionRecorreUnaFotoTests
{
    private static readonly DateTimeOffset s_ahora = new(2026, 10, 27, 23, 30, 0, TimeSpan.Zero);

    private static readonly HojaDeExportacion<int> s_hoja = new(
        "cosas", [new("numero", numero => CampoCsv.Numero((long)numero))]);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(200, 1)]
    [InlineData(201, 2)]
    [InlineData(400, 2)]
    [InlineData(401, 3)]
    public async Task Pide_todas_las_paginas_del_tamanio_maximo_con_la_foto_abierta(int total, int paginas)
    {
        Escena escena = new(total);

        IActionResult resultado = await escena.ExportarAsync("");

        resultado.ShouldBeOfType<EmptyResult>();
        escena.Pedidas.Select(pedida => pedida.Pagina).ShouldBe(Enumerable.Range(1, paginas));
        escena.Pedidas.ShouldAllBe(pedida => pedida.Tamanio == Paginacion.TamanioMaximo);
        escena.Sucesos.ShouldBe(
            ["abre", .. Enumerable.Range(1, paginas).Select(numero => $"página {numero}"), "cierra"]);

        string[] lineas = escena.Escrito().Split("\r\n");
        lineas[0].ShouldBe("numero");
        lineas[1..^1].ShouldBe(
            Enumerable.Range(1, total).Select(numero => $"{numero}"), "todas, en su orden, y una vez");
        lineas[^1].ShouldBe(string.Empty, "la última línea también acaba en CRLF");
    }

    [Fact]
    public async Task El_fichero_se_llama_por_el_recurso_y_el_dia_de_negocio()
    {
        Escena escena = new(total: 3);

        await escena.ExportarAsync("");

        escena.Contexto.Response.ContentType.ShouldBe("text/csv; charset=utf-8");
        escena.Contexto.Response.Headers.ContentDisposition.ToString()
            .ShouldBe("attachment; filename=cosas-2026-10-27.csv; filename*=UTF-8''cosas-2026-10-27.csv");
        escena.Bytes()[..3].ShouldBe(EscritorCsv.Bom.ToArray());
    }

    [Fact]
    public async Task Cada_pagina_lleva_el_orden_y_el_texto_del_listado()
    {
        Escena escena = new(total: 250);

        await escena.ExportarAsync(
            "?q=zz&sort=-codigo", new FiltroDeTexto { Texto = "zz" }, new ConsultaDeListado { Orden = "-codigo" });

        escena.Pedidas.Count.ShouldBe(2);
        escena.Pedidas.ShouldAllBe(
            pedida => pedida.Filtro == "zz" && pedida.Orden == new Orden("codigo", Descendente: true));
    }

    /// <summary>
    /// Por la puerta de un <see cref="IListado{TDto}"/>, la de los listados que no cuelgan de otro
    /// recurso y no pueden fallar: las mismas páginas, con el mismo orden y el mismo texto.
    /// </summary>
    [Fact]
    public async Task Un_listado_que_no_falla_entra_por_su_interfaz_con_sus_criterios()
    {
        Escena escena = new(total: 250);

        IActionResult resultado = await escena.ExportarListadoAsync(
            "?q=zz&sort=-codigo", new FiltroDeTexto { Texto = "zz" }, new ConsultaDeListado { Orden = "-codigo" });

        resultado.ShouldBeOfType<EmptyResult>();
        escena.Sucesos.ShouldBe(["abre", "página 1", "página 2", "cierra"]);
        escena.Pedidas.ShouldAllBe(
            pedida => pedida.Filtro == "zz" && pedida.Orden == new Orden("codigo", Descendente: true));
        escena.Escrito().Split("\r\n").Length.ShouldBe(1 + 250 + 1, "la cabecera, las filas y el final");
    }

    [Fact]
    public async Task Un_texto_que_el_listado_no_mira_es_su_400_sin_abrir_la_foto_ni_escribir()
    {
        Escena escena = new(total: 3);

        IActionResult resultado = await escena.ExportarAsync("?q=zz", texto: null);

        escena.Sucesos.ShouldBeEmpty("la foto no se abre para contestar un 400");
        escena.Bytes().ShouldBeEmpty();
        (await Escena.ContestarAsync(resultado)).ShouldBe((400, "/errors/filtro-no-admitido"));
    }

    [Fact]
    public async Task Un_orden_que_no_se_admite_es_su_400()
    {
        Escena escena = new(total: 3);

        IActionResult resultado = await escena.ExportarAsync(
            "?sort=nombre", criterios: new ConsultaDeListado { Orden = "nombre" });

        escena.Sucesos.ShouldBeEmpty();
        (await Escena.ContestarAsync(resultado)).ShouldBe((400, "/errors/orden-no-admitido"));
    }

    [Fact]
    public async Task Un_fallo_en_la_primera_pagina_sale_como_en_el_listado_antes_del_primer_byte()
    {
        Escena escena = new(total: 3) { FallaEn = 1 };

        IActionResult resultado = await escena.ExportarAsync("");

        escena.Sucesos.ShouldBe(["abre", "página 1", "cierra"]);
        escena.Bytes().ShouldBeEmpty("no se ha escrito nada, así que todavía se puede contestar un 404");
        escena.Contexto.Response.Headers.ContentDisposition.ToString().ShouldBeEmpty();
        (await Escena.ContestarAsync(resultado)).ShouldBe((404, "/errors/cosa-no-encontrada"));
    }

    /// <summary>
    /// La pareja del de arriba: un fallo cuando el fichero ya ha empezado no se puede contestar, y
    /// lanza en vez de cerrar el fichero como si estuviera entero. La foto se cierra igual.
    /// </summary>
    [Fact]
    public async Task Un_fallo_a_mitad_lanza_con_el_fichero_empezado_y_cierra_la_foto()
    {
        Escena escena = new(total: 450) { FallaEn = 2 };

        InvalidOperationException fallo = await Should.ThrowAsync<InvalidOperationException>(
            () => escena.ExportarAsync(""));

        fallo.Message.ShouldContain("página 2");
        escena.Sucesos.ShouldBe(["abre", "página 1", "página 2", "cierra"]);
        escena.Escrito().ShouldEndWith("200\r\n", customMessage: "la primera página ya había salido");
    }

    /// <summary>
    /// Un caso de uso de mentira con <paramref name="total"/> elementos, del 1 al total, y una foto
    /// de mentira, que apuntan lo que les piden en <see cref="Sucesos"/>. El caso de uso entra por
    /// las dos puertas: la del que puede fallar y la del <see cref="IListado{TDto}"/>.
    /// </summary>
    private sealed class Escena(int total) : IListado<int>, IFotoDeLectura
    {
        public DefaultHttpContext Contexto { get; } = new() { Response = { Body = new MemoryStream() } };

        public List<Paginacion> Pedidas { get; } = [];

        public List<string> Sucesos { get; } = [];

        public int? FallaEn { get; init; }

        public IReadOnlySet<string> CamposOrdenables { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "codigo" };

        public Task<IActionResult> ExportarAsync(
            string consulta,
            FiltroDeTexto? texto = null,
            ConsultaDeListado? criterios = null) =>
            ExportacionDeListado.ExportarResultadoAsync(
                Controlador(consulta),
                criterios ?? new ConsultaDeListado(),
                texto,
                this,
                PaginaAsync,
                this,
                s_hoja,
                new RelojParado(s_ahora),
                CancellationToken.None);

        public Task<IActionResult> ExportarListadoAsync(
            string consulta,
            FiltroDeTexto? texto,
            ConsultaDeListado criterios) =>
            ExportacionDeListado.ExportarAsync(
                Controlador(consulta),
                criterios,
                texto,
                this,
                this,
                s_hoja,
                new RelojParado(s_ahora),
                CancellationToken.None);

        async Task<PaginaDe<int>> IListado<int>.EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
            (await PaginaAsync(paginacion, cancelacion)).Valor;

        public Task<IAsyncDisposable> AbrirAsync(CancellationToken cancelacion)
        {
            Sucesos.Add("abre");

            return Task.FromResult<IAsyncDisposable>(new Cierre(Sucesos));
        }

        public byte[] Bytes() => ((MemoryStream)Contexto.Response.Body).ToArray();

        public string Escrito() => Encoding.UTF8.GetString(Bytes().AsSpan(EscritorCsv.Bom.Length));

        /// <summary>Ejecuta el resultado de la exportación, y lee su estado y su <c>type</c>.</summary>
        public static async Task<(int Estado, string? Tipo)> ContestarAsync(IActionResult resultado)
        {
            ServiceCollection servicios = new();
            servicios.AddLogging();
            servicios.AddProblemDetails();

            DefaultHttpContext respuesta = new()
            {
                RequestServices = servicios.BuildServiceProvider(),
                Response = { Body = new MemoryStream() },
            };

            await resultado.ExecuteResultAsync(new ActionContext(respuesta, new RouteData(), new ActionDescriptor()));

            using var problema = JsonDocument.Parse(((MemoryStream)respuesta.Response.Body).ToArray());

            return (respuesta.Response.StatusCode, problema.RootElement.GetProperty("type").GetString());
        }

        private Controlador Controlador(string consulta)
        {
            Contexto.Request.QueryString = new QueryString(consulta.Length == 0 ? null : consulta);

            return new Controlador { ControllerContext = new ControllerContext { HttpContext = Contexto } };
        }

        private Task<Resultado<PaginaDe<int>>> PaginaAsync(Paginacion pedida, CancellationToken cancelacion)
        {
            Pedidas.Add(pedida);
            Sucesos.Add($"página {pedida.Pagina}");

            if (pedida.Pagina == FallaEn)
            {
                return Task.FromResult(Resultado.Fallo<PaginaDe<int>>(
                    ErrorDeOperacion.NoEncontrado("cosa-no-encontrada", "No hay ninguna cosa con ese número.")));
            }

            int cuantos = Math.Max(0, Math.Min(pedida.Tamanio, total - pedida.Salto));
            List<int> elementos = [.. Enumerable.Range(pedida.Salto + 1, cuantos)];

            return Task.FromResult(
                Resultado.Correcto(new PaginaDe<int>(elementos, pedida.Pagina, pedida.Tamanio, total)));
        }
    }

    private sealed class Cierre(List<string> sucesos) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            sucesos.Add("cierra");

            return ValueTask.CompletedTask;
        }
    }

    private sealed class Controlador : ControllerBase;

    private sealed class RelojParado(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
