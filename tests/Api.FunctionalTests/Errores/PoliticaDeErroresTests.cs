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
    [InlineData(RutasQueFallan.FaltaLaPrecondicion, (HttpStatusCode)428, "/errors/falta-if-match")]
    [InlineData(RutasQueFallan.DemasiadoGrande, HttpStatusCode.RequestEntityTooLarge, "/errors/cuerpo-demasiado-grande")]
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

    /// <summary>
    /// La carrera que impidió un índice DECLARADO sale con el mismo <c>412</c> y el mismo
    /// <c>type</c> que da el testigo de concurrencia.
    /// </summary>
    /// <remarks>
    /// <b>Es la mitad de arriba de una cadena que ningún carril prueba entero.</b> Que el perdedor
    /// de dos anulaciones simultáneas choque contra <c>ix_ajustes_anula_a_id</c> lo comprueba
    /// <c>LaAnulacionConContraDocumentoTests.Dos_anulaciones_simultaneas_dejan_un_solo_inverso</c>,
    /// contra PostgreSQL de verdad y sin borde por medio; que esa excepción salga como <c>412</c>
    /// se comprueba aquí, con el borde de verdad y sin base. Los dos afirman el mismo nombre de
    /// índice escrito a mano, que es la costura por la que se sujetan.
    /// </remarks>
    [Fact]
    public async Task Una_carrera_que_impidio_un_indice_declarado_sale_412_y_no_500()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.CarreraPerdida, UriKind.Relative));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        using var problema = JsonDocument.Parse(cuerpo);
        problema.RootElement.GetProperty("type").GetString().ShouldBe("/errors/version-obsoleta");

        // Y sin un solo rastro del interior, que aquí no es gratis: el mensaje del motor lleva el
        // nombre del índice y el de la tabla, y el de EF Core lleva la frase de `SaveChanges`.
        cuerpo.ShouldNotContain(RutasQueFallan.IndiceDeclarado);
        foreach (string rastro in s_rastrosDelInterior)
        {
            cuerpo.ShouldNotContain(rastro);
        }
    }

    /// <summary>
    /// El MISMO <c>23505</c> sobre un índice que nadie declaró sigue siendo un <c>500</c>.
    /// </summary>
    /// <remarks>
    /// <b>Sin este caso, el de arriba no dice lo que parece.</b> Un manejador que tradujera todo
    /// <c>23505</c> lo pondría igual de verde, y con él dos altas con el mismo NIF contestarían
    /// <c>412</c>: el cliente releería, volvería a mandar lo mismo y no saldría de ahí jamás. Lo
    /// que se afirma aquí es que la traducción es una lista de decisiones y no una regla general.
    /// </remarks>
    [Fact]
    public async Task Una_violacion_de_unicidad_sin_declarar_sigue_siendo_500()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.UnicidadSinDeclarar, UriKind.Relative));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        (await respuesta.Content.ReadAsStringAsync())
            .ShouldNotContain(RutasQueFallan.IndiceSinDeclarar);
    }

    /// <summary>
    /// La restricción DECLARADA que rechaza una escritura sale con el error que su módulo declaró:
    /// el stock que no baja de cero es un <c>422</c> <c>stock-insuficiente</c>, y no un <c>500</c>.
    /// </summary>
    /// <remarks>
    /// <b>Es la mitad de arriba, como la de la carrera.</b> Que dos salidas simultáneas choquen
    /// contra <c>ck_existencias_fisico_no_negativo</c> lo comprueba el carril de integración contra
    /// PostgreSQL; que esa excepción salga como <c>422</c> se comprueba aquí. Los dos afirman el
    /// mismo nombre, escrito a mano.
    /// </remarks>
    [Fact]
    public async Task Una_restriccion_declarada_sale_con_su_error_y_no_500()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.ReglaGuardadaPorLaBase, UriKind.Relative));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        using var problema = JsonDocument.Parse(cuerpo);
        problema.RootElement.GetProperty("type").GetString().ShouldBe("/errors/stock-insuficiente");
        problema.RootElement.GetProperty("status").GetInt32().ShouldBe(422);

        // Sin el nombre de la restricción ni el de la tabla, que el mensaje del motor trae.
        cuerpo.ShouldNotContain(RutasQueFallan.RestriccionDeclarada);
        foreach (string rastro in s_rastrosDelInterior)
        {
            cuerpo.ShouldNotContain(rastro);
        }
    }

    /// <summary>El MISMO <c>23514</c> sobre una restricción que nadie declaró sigue siendo un <c>500</c>.</summary>
    /// <remarks>
    /// Sin este caso, un manejador que tradujera todo <c>23514</c> pondría el de arriba igual de
    /// verde, y un defecto —una fila del libro sin cantidad— le diría al cliente que no hay stock.
    /// </remarks>
    [Fact]
    public async Task Una_restriccion_sin_declarar_sigue_siendo_500()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.RestriccionSinDeclarar, UriKind.Relative));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        (await respuesta.Content.ReadAsStringAsync())
            .ShouldNotContain(RutasQueFallan.RestriccionNoDeclarada);
    }

    /// <summary>
    /// El índice único DECLARADO como regla sale con su error: la serie que ya está en otro sitio
    /// es un <c>422</c> <c>numero-de-serie-en-existencias</c>, ni el <c>412</c> de la carrera ni un
    /// <c>500</c>.
    /// </summary>
    /// <remarks>
    /// <b>Es la mitad de arriba otra vez</b>, ahora con un <c>23505</c> (ADR-0048 §3). Que la misma
    /// serie en dos almacenes choque contra ese índice lo comprueba el carril de integración.
    /// </remarks>
    [Fact]
    public async Task Un_indice_declarado_como_regla_sale_con_su_error_y_no_412_ni_500()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.UnicidadQueGuardaUnaRegla, UriKind.Relative));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        using var problema = JsonDocument.Parse(cuerpo);
        problema.RootElement.GetProperty("type").GetString()
            .ShouldBe("/errors/numero-de-serie-en-existencias");
        problema.RootElement.GetProperty("status").GetInt32().ShouldBe(422);

        cuerpo.ShouldNotContain(RutasQueFallan.IndiceDeclaradoComoRegla);
        foreach (string rastro in s_rastrosDelInterior)
        {
            cuerpo.ShouldNotContain(rastro);
        }
    }

    /// <summary>
    /// El MISMO nombre con el código de la otra clase sigue siendo un <c>500</c>: lo declarado es
    /// el nombre con su clase.
    /// </summary>
    /// <remarks>
    /// Sin este caso, un manejador que buscara solo por nombre pondría el de arriba igual de verde.
    /// PostgreSQL no impide que un índice y un <c>CHECK</c> se llamen igual.
    /// </remarks>
    [Fact]
    public async Task El_nombre_declarado_con_la_otra_clase_sigue_siendo_500()
    {
        using HttpResponseMessage respuesta = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.UnicidadConOtraClase, UriKind.Relative));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        (await respuesta.Content.ReadAsStringAsync())
            .ShouldNotContain(RutasQueFallan.IndiceDeclaradoComoRegla);
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

    /// <summary>
    /// El estado actual viaja en la extensión <c>actual</c>, con los nombres del contrato, y un
    /// conflicto sin él no la lleva (ADR-0055 §11).
    /// </summary>
    /// <remarks>
    /// <b>Las dos rutas en el mismo caso</b>: un borde que publicara <c>actual</c> siempre, vacío o
    /// nulo, pasaría la primera mitad, y uno que no la publicara nunca, la segunda.
    /// </remarks>
    [Fact]
    public async Task El_estado_actual_viaja_en_su_extension_y_sin_el_no_hay_extension()
    {
        using HttpResponseMessage conEstado = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.ConflictoConElEstadoActual, UriKind.Relative));

        conEstado.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using (var cuerpo = JsonDocument.Parse(await conEstado.Content.ReadAsStringAsync()))
        {
            JsonElement actual = cuerpo.RootElement.GetProperty("actual");

            actual.GetProperty("estado").GetString().ShouldBe("Confirmado");
            actual.GetProperty("lineasPendientes").GetInt32().ShouldBe(3);
            cuerpo.RootElement.GetProperty("type").GetString().ShouldBe("/errors/pedido-ya-confirmado");
        }

        using HttpResponseMessage sinEstado = await api.CreateClient()
            .GetAsync(new Uri(RutasQueFallan.Conflicto, UriKind.Relative));

        using var sinElCuerpo = JsonDocument.Parse(await sinEstado.Content.ReadAsStringAsync());

        sinElCuerpo.RootElement.TryGetProperty("actual", out _).ShouldBeFalse(
            "un conflicto sin estado actual publica la extensión igualmente, y el cliente tendría " +
            "que distinguir «vacío» de «no lo sé»");
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
