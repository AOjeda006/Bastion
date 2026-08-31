using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Errores;

public sealed class PoliticaDeErroresTests(ApiConRutasQueFallan api) : IClassFixture<ApiConRutasQueFallan>
{
    // Fragmentos que solo existen DENTRO del sistema. Ninguno puede asomar en una respuesta.
    private static readonly string[] s_rastrosDelInterior =
    [
        "organizacion.usuario",
        "clave.pem",
        "SELECT",
        "InvalidOperationException",
        "BadHttpRequestException",
        "Bastion.Api.FunctionalTests",
        "   at ",
    ];

    // §9: type estable por clase de error de negocio, y el código de estado que le toca.
    [Theory]
    [InlineData(RutasQueFallan.Validacion, HttpStatusCode.BadRequest, "/errors/fecha-fuera-de-ejercicio")]
    [InlineData(RutasQueFallan.Permiso, HttpStatusCode.Forbidden, "/errors/sin-permiso-de-facturacion")]
    [InlineData(RutasQueFallan.NoEncontrado, HttpStatusCode.NotFound, "/errors/articulo-no-encontrado")]
    [InlineData(RutasQueFallan.Conflicto, HttpStatusCode.Conflict, "/errors/pedido-ya-confirmado")]
    [InlineData(RutasQueFallan.ReglaDeNegocio, HttpStatusCode.UnprocessableContent, "/errors/stock-insuficiente")]
    [InlineData(RutasQueFallan.NoAutenticado, HttpStatusCode.Unauthorized, "/errors/sesion-caducada")]
    [InlineData(RutasQueFallan.VersionObsoleta, HttpStatusCode.PreconditionFailed, "/errors/version-obsoleta")]
    [InlineData(RutasQueFallan.FaltaLaVersion, (HttpStatusCode)428, "/errors/falta-if-match")]
    public async Task CadaClaseDeError_SeTraduceASuCodigoDeEstadoYASuTypeEstable(
        string ruta, HttpStatusCode estadoEsperado, string tipoEsperado)
    {
        using HttpResponseMessage respuesta = await api.CreateClient().GetAsync(new Uri(ruta, UriKind.Relative));

        respuesta.StatusCode.ShouldBe(estadoEsperado);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        cuerpo.RootElement.GetProperty("type").GetString().ShouldBe(tipoEsperado);
        cuerpo.RootElement.GetProperty("status").GetInt32().ShouldBe((int)estadoEsperado);
    }

    // La teoría de arriba enumera las clases A MANO, y una lista a mano se queda corta: añadir una
    // clase de error nueva y no añadir su fila la dejaría sin comprobar, y el síntoma sería un
    // `NotSupportedException` desde dentro del manejador de errores —o sea, un 500 justo cuando ya
    // había un error que contar—. Estos dos tests son el guardián de esa lista.
    [Fact]
    public void TodaClaseDeError_TieneCodigoDeEstadoYTitulo()
    {
        foreach (TipoDeError tipo in Enum.GetValues<TipoDeError>())
        {
            Should.NotThrow(() => PoliticaDeErrores.CodigoDeEstadoDe(tipo), $"falta el estado de {tipo}");
            Should.NotThrow(() => PoliticaDeErrores.TituloDe(tipo), $"falta el título de {tipo}");
        }
    }

    [Fact]
    public void LaTeoriaDeArriba_TieneUnaFilaPorClaseDeError()
    {
        int filas = typeof(PoliticaDeErroresTests)
            .GetMethod(nameof(CadaClaseDeError_SeTraduceASuCodigoDeEstadoYASuTypeEstable))!
            .GetCustomAttributes(typeof(InlineDataAttribute), inherit: false)
            .Length;

        filas.ShouldBe(
            Enum.GetValues<TipoDeError>().Length,
            "hay una clase de error sin ruta que la ejerza: mientras no la tenga, nadie ha visto " +
            "nunca la respuesta que produce");
    }

    [Fact]
    public async Task UnErrorDeNegocio_LlevaLosCamposDelRfc9457()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.ReglaDeNegocio, UriKind.Relative));

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        JsonElement problema = cuerpo.RootElement;

        problema.GetProperty("type").GetString().ShouldBe("/errors/stock-insuficiente");
        problema.GetProperty("title").GetString().ShouldBe("Regla de negocio incumplida");
        problema.GetProperty("status").GetInt32().ShouldBe(422);
        problema.GetProperty("detail").GetString()
            .ShouldBe("No hay bastante stock disponible para servir la línea.");
        problema.GetProperty("instance").GetString().ShouldBe(RutasQueFallan.ReglaDeNegocio);
        problema.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    // Entrada hostil de verdad: se manda basura por la cadena de consulta, esa basura acaba
    // dentro del mensaje de la excepción, y se lee lo que vuelve. Esto no se detecta leyendo
    // el código de la política; se detecta mirando la respuesta.
    [Fact]
    public async Task UnaExcepcionNoControlada_RespondeQuinientosSinNadaDelInterior()
    {
        string veneno = $"veneno-{Guid.NewGuid():N}";
        Uri ruta = new($"{RutasQueFallan.Estalla}?veneno={veneno}", UriKind.Relative);

        using HttpResponseMessage respuesta = await api.CreateClient().GetAsync(ruta);
        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        respuesta.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        cuerpo.ShouldNotContain(veneno);
        foreach (string rastro in s_rastrosDelInterior)
        {
            cuerpo.ShouldNotContain(rastro);
        }

        using var problema = JsonDocument.Parse(cuerpo);
        problema.RootElement.GetProperty("type").GetString().ShouldBe("/errors/error-interno");
        problema.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnaPeticionMalFormada_RespondeCuatrocientosSinNadaDelInterior()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.PeticionMala, UriKind.Relative));
        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        cuerpo.ShouldNotContain("Failed to read parameter");
        foreach (string rastro in s_rastrosDelInterior)
        {
            cuerpo.ShouldNotContain(rastro);
        }
    }

    // Los dos destinatarios que NO comparten texto: el de fuera necesita saber qué hacer, el de
    // dentro qué ha pasado. Un solo test lo afirma en las dos direcciones a la vez.
    [Fact]
    public async Task ElDetalleInterno_ViveEnElRegistroYNoEnLaRespuesta()
    {
        string veneno = $"veneno-{Guid.NewGuid():N}";
        Uri ruta = new($"{RutasQueFallan.Estalla}?veneno={veneno}", UriKind.Relative);

        using HttpResponseMessage respuesta = await api.CreateClient().GetAsync(ruta);
        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        cuerpo.ShouldNotContain(veneno);
        api.Registro.Lineas().ShouldContain(linea => linea.Contains(veneno, StringComparison.Ordinal));
        api.Registro.Lineas().ShouldContain(
            linea => linea.Contains("organizacion.usuario", StringComparison.Ordinal));
    }

    // El identificador de traza de la respuesta y el @tr del registro tienen que ser EL MISMO,
    // o pedirle a alguien "dame el traceId" no sirve para encontrar nada.
    [Fact]
    public async Task ElTraceIdDeLaRespuesta_EsElMismoQueElArrobaTrDelRegistro()
    {
        string traza = ActivityTraceId.CreateRandom().ToHexString();
        string tramo = ActivitySpanId.CreateRandom().ToHexString();

        using HttpRequestMessage peticion = new(
            HttpMethod.Get, new Uri(RutasQueFallan.Estalla, UriKind.Relative));
        peticion.Headers.Add("traceparent", $"00-{traza}-{tramo}-01");

        using HttpResponseMessage respuesta = await api.CreateClient().SendAsync(peticion);

        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("traceId").GetString().ShouldBe(traza);

        api.Registro.Lineas().ShouldContain(
            linea => linea.Contains($"\"@tr\":\"{traza}\"", StringComparison.Ordinal));
    }

    // La política es central: también responde ProblemDetails donde no hay endpoint que la
    // invoque, que es justo donde un try/catch por controlador nunca llegaría.
    //
    // Y a un anónimo le responde 401, no 404: desde 0.5 la política de respaldo alcanza también a
    // las peticiones que no casan con ningún endpoint. No es un efecto colateral que se tolere,
    // es lo que se quiere —quien no se ha identificado no puede ir probando rutas para averiguar
    // cuáles existen—. El 404 lo ve quien SÍ se ha identificado, y eso se comprueba en
    // Api.IntegrationTests, que es donde hay con qué identificarse.
    [Fact]
    public async Task UnaRutaQueNoExiste_LeResponde401AlAnonimoYTambienEnProblemDetails()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri("/api/v1/esto-no-existe", UriKind.Relative));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("status").GetInt32().ShouldBe(401);
        problema.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
