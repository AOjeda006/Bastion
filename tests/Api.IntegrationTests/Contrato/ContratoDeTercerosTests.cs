using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Contrato;

/// <summary>
/// El contrato del módulo de Terceros visto desde fuera: lo que se manda, lo que vuelve y con qué
/// código.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ningún identificador fiscal de este fichero está pegado de ninguna parte.</b> Los españoles
/// salen de un número y del cálculo de su letra —<see cref="Escenario.NifInventado"/>—, y los extranjeros
/// son cadenas que empiezan por <c>PRUEBA</c>. Un NIF real es un dato personal, y una fixture no
/// se queda en el fichero: viaja al artefacto de resultados, al registro de la CI y al historial
/// de git, para siempre y sin plazo.
/// </para>
/// <para>
/// El algoritmo entero —NIF, NIE y CIF, con las veinte iniciales de persona jurídica y las dos
/// clases de carácter de control— tiene su batería generada en <c>Terceros.UnitTests</c>, que es
/// donde se puede recorrer entera sin levantar un contenedor. Aquí solo se comprueba que ese
/// algoritmo está <b>enchufado</b> al borde: que un control mal puesto sale por un 400 con su
/// campo, y que un identificador bueno nace verificado.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ContratoDeTercerosTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Terceros = "/api/v1/terceros/terceros";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    [Fact]
    public async Task Crear_un_tercero_devuelve_201_con_Location_que_lleva_al_recurso()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(110));

        using HttpResponseMessage creacion =
            await cliente.PostAsJsonAsync(Terceros, Alta("ES", Escenario.NifInventado(30_000_001)));

        creacion.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(creacion));
        creacion.Headers.Location.ShouldNotBeNull();

        // Y el enlace lleva de verdad al recurso: un `Location` que no se puede seguir es una
        // cabecera decorativa.
        using HttpResponseMessage seguido = await cliente.GetAsync(creacion.Headers.Location);
        seguido.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(seguido));
    }

    [Fact]
    public async Task El_identificador_espanol_se_valida_de_verdad_y_nace_verificado()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(111));

        TerceroDto tercero = await CrearAsync(cliente, "ES", Escenario.NifInventado(30_000_002));

        tercero.Identificacion.Pais.ShouldBe("ES");
        tercero.Identificacion.Verificacion.ShouldBe("VerificadoPorAlgoritmo");
    }

    /// <summary>
    /// Lo que no se puede validar se marca como no validado; no se da por bueno.
    /// </summary>
    /// <remarks>
    /// Es la mitad del criterio del ítem que se pierde si el estado fuera un campo del cuerpo:
    /// quien da el alta pondría «verificado» porque le consta, y el maestro acabaría con la mitad
    /// de las fichas pareciendo comprobadas sin serlo. El contrato de alta no tiene el campo, así
    /// que no hay manera de intentarlo — y esto comprueba lo que el servidor decide en su lugar.
    /// </remarks>
    [Fact]
    public async Task El_identificador_extranjero_nace_marcado_como_NO_verificado()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(112));

        TerceroDto tercero = await CrearAsync(cliente, "pt", "prueba-000-111-222");

        tercero.Identificacion.Pais.ShouldBe("PT");
        tercero.Identificacion.Numero.ShouldBe("PRUEBA000111222", "se normaliza igual que un NIF");
        tercero.Identificacion.Verificacion.ShouldBe("NoVerificado");
    }

    [Fact]
    public async Task Un_identificador_espanol_con_el_control_mal_es_400_del_campo_del_formulario()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(113));

        // El número es bueno y la letra es la de al lado en la tabla: exactamente el error que
        // comete quien teclea, y el que un validador de mentira deja pasar.
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Terceros, Alta("ES", Escenario.NifConElControlCambiado(30_000_003)));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        JsonElement problema = await Problema(respuesta);
        problema.GetProperty("type").GetString().ShouldBe("/errors/datos-no-validos");

        // El nombre del campo es el del CUERPO y no el de la clase de dominio: lo va a pintar un
        // formulario junto al recuadro que hay que corregir.
        problema.GetProperty("errors").GetProperty("identificacion.numero")
            .GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Un_tercero_que_no_es_ni_cliente_ni_proveedor_es_400_y_dice_que_marque_uno()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(114));

        CrearTerceroDto sinPapel = Alta("ES", Escenario.NifInventado(30_000_004)) with
        {
            EsCliente = false,
            EsProveedor = false,
        };

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Terceros, sinPapel);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // La invariante está en el dominio y allí LANZA. Aquí se adelanta porque quien rellena el
        // formulario no ha hecho nada absurdo —ha dejado dos casillas sin marcar— y merece que se
        // le diga cuáles, no un 500.
        (await Problema(respuesta)).GetProperty("errors")
            .TryGetProperty("esCliente", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task El_domicilio_fiscal_va_y_vuelve_en_los_seis_campos_de_R17()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(115));

        TerceroDto tercero = await CrearAsync(cliente, "ES", Escenario.NifInventado(30_000_005));

        // Los seis, y no una cadena con saltos de línea: sobre esto se calcula el IVA por
        // territorio y se imprime una factura.
        tercero.DomicilioFiscal.Calle.ShouldBe("Gran Vía");
        tercero.DomicilioFiscal.Numero.ShouldBe("31");
        tercero.DomicilioFiscal.CodigoPostal.ShouldBe("28013");
        tercero.DomicilioFiscal.Poblacion.ShouldBe("Madrid");
        tercero.DomicilioFiscal.Subdivision.ShouldBe("Madrid");
        tercero.DomicilioFiscal.Pais.ShouldBe("ES");
    }

    [Fact]
    public async Task El_listado_viene_paginado_con_su_total_y_filtra_por_nombre()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(116));

        await CrearAsync(cliente, "ES", Escenario.NifInventado(30_000_006), "Ferretería del Norte");
        await CrearAsync(cliente, "ES", Escenario.NifInventado(30_000_007), "Ferretería del Sur");
        await CrearAsync(cliente, "ES", Escenario.NifInventado(30_000_008), "Panadería Central");

        PaginaDe<TerceroDto> todos =
            (await cliente.GetFromJsonAsync<PaginaDe<TerceroDto>>($"{Terceros}?page=1&size=50"))!;

        todos.Total.ShouldBe(3);

        // `q` busca por razón social y nombre comercial, que es lo que se lee en una pantalla. Va
        // en la URL a propósito y no es una excepción del ADR-0025: un trozo de nombre comercial
        // no es una llave con la que cruzar ficheros.
        PaginaDe<TerceroDto> filtrados = (await cliente.GetFromJsonAsync<PaginaDe<TerceroDto>>(
            $"{Terceros}?page=1&size=50&q=ferreter"))!;

        filtrados.Total.ShouldBe(2);
        filtrados.Elementos.ShouldAllBe(tercero => tercero.RazonSocial.StartsWith("Ferretería"));
    }

    /// <summary>
    /// Buscar por identificador fiscal va por el <b>cuerpo</b>, y encuentra lo que se dio de alta
    /// aunque se teclee con puntos y guiones.
    /// </summary>
    /// <remarks>
    /// Las dos mitades importan. Que vaya por el cuerpo es el ADR-0025 cobrado donde más muerde:
    /// el identificador de un cliente es muy a menudo el DNI de una persona física. Y que la
    /// búsqueda lea el número <b>por el mismo camino</b> que el alta es lo que impide el fallo que
    /// nadie sabe explicar: una ficha que existe y que no se encuentra.
    /// </remarks>
    [Fact]
    public async Task La_busqueda_por_identificador_va_por_el_CUERPO_y_lo_lee_igual_que_el_alta()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(117));

        string nif = Escenario.NifInventado(30_000_009);
        TerceroDto alta = await CrearAsync(cliente, "ES", nif);

        TramoDe<TerceroDto> tramo = await BuscarAsync(
            cliente,
            new BuscarTercerosDto { Numero = Salpicado(nif) });

        tramo.Elementos.Count.ShouldBe(1, "el mismo identificador escrito con ruido de teclado");
        tramo.Elementos[0].Id.ShouldBe(alta.Id);
    }

    [Fact]
    public async Task Una_busqueda_sin_ningun_criterio_es_400_y_dice_donde_esta_el_listado()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(118));

        using HttpResponseMessage respuesta =
            await cliente.PostAsJsonAsync($"{Terceros}/buscar", new BuscarTercerosDto());

        // Sin criterio, esto sería el listado entero pedido por un camino que no pagina por
        // número. No se rechaza por purismo: tener dos formas de pedir lo mismo garantiza que una
        // de las dos envejezca sin que nadie la mire.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Problema(respuesta)).GetProperty("errors")
            .TryGetProperty("numero", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Un_cursor_compuesto_a_mano_es_400_y_no_un_tramo_vacio()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(119));

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            $"{Terceros}/buscar",
            new BuscarTercerosDto { Nombre = "lo que sea", Cursor = "esto-no-es-un-cursor" });

        // Un tramo vacío diría «no hay más» a quien se equivocó de cadena, y el recorrido se
        // cortaría por la mitad sin que nadie se enterase.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Problema(respuesta)).GetProperty("errors")
            .TryGetProperty("cursor", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task El_cursor_del_tramo_anterior_trae_el_siguiente_y_no_repite()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(120));

        await CrearAsync(cliente, "ES", Escenario.NifInventado(30_000_010), "Recorrido Uno");
        await CrearAsync(cliente, "ES", Escenario.NifInventado(30_000_011), "Recorrido Dos");

        TramoDe<TerceroDto> primero = await BuscarAsync(
            cliente, new BuscarTercerosDto { Nombre = "Recorrido", Tamanio = 1 });

        primero.Elementos.Count.ShouldBe(1);
        primero.CursorSiguiente.ShouldNotBeNullOrWhiteSpace("hay un segundo tercero que traer");

        TramoDe<TerceroDto> segundo = await BuscarAsync(
            cliente,
            new BuscarTercerosDto
            {
                Nombre = "Recorrido",
                Tamanio = 1,
                Cursor = primero.CursorSiguiente,
            });

        segundo.Elementos.Count.ShouldBe(1);
        segundo.Elementos[0].Id.ShouldNotBe(
            primero.Elementos[0].Id, "el cursor avanza, no reinicia");

        // Y el cursor no lleva el criterio dentro: si lo llevara, un NIF acabaría en una cadena
        // que se copia y se comparte, que es lo que el ADR-0025 saca de la URL entrando por la
        // puerta de al lado.
        primero.CursorSiguiente.ShouldNotContain("Recorrido");
    }

    /// <summary>
    /// La búsqueda por identificador NO ve lo bloqueado, que es la puerta trasera más cómoda que
    /// tendría el art. 32.
    /// </summary>
    [Fact]
    public async Task Un_tercero_bloqueado_no_aparece_en_la_busqueda_por_su_identificador()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(121));

        string nif = Escenario.NifInventado(30_000_012);
        TerceroDto tercero = await CrearAsync(cliente, "ES", nif);

        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Terceros}/{tercero.Id}");
        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));

        TramoDe<TerceroDto> tramo = await BuscarAsync(
            cliente, new BuscarTercerosDto { Numero = nif });

        tramo.Elementos.ShouldBeEmpty("lo bloqueado se lista por su camino, con su permiso y su traza");
    }

    [Fact]
    public async Task Modificar_exige_la_version_y_devuelve_el_recurso_entero_sin_tocar_el_identificador()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(122));

        TerceroDto tercero = await CrearAsync(cliente, "ES", Escenario.NifInventado(30_000_013));

        using HttpResponseMessage sinVersion = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Terceros}/{tercero.Id}",
            etiqueta: null,
            JsonContent.Create(Cambio()));

        sinVersion.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired);

        using HttpResponseMessage conVersion =
            await cliente.ModificarAsync($"{Terceros}/{tercero.Id}", Cambio());

        conVersion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(conVersion));

        TerceroDto modificado = (await conVersion.Content.ReadFromJsonAsync<TerceroDto>())!;

        modificado.RazonSocial.ShouldBe("Razón Social Nueva");
        modificado.EsProveedor.ShouldBeTrue();

        // El identificador fiscal no está en `ModificarTerceroDto`, así que no hay manera de
        // intentarlo: aparece en cada factura ya emitida, y cambiarlo no es modificar al tercero,
        // es otro tercero.
        modificado.Identificacion.ShouldBe(tercero.Identificacion);
    }

    [Fact]
    public async Task Un_tercero_que_no_existe_es_404_con_ProblemDetails()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(123));

        using HttpResponseMessage respuesta = await cliente.GetAsync($"{Terceros}/{Guid.NewGuid()}");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Problema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/tercero-no-encontrado");
    }

    [Fact]
    public async Task El_estado_de_verificacion_viaja_como_TEXTO_y_no_como_numero()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(124));

        using HttpResponseMessage creacion =
            await cliente.PostAsJsonAsync(Terceros, Alta("ES", Escenario.NifInventado(30_000_014)));

        creacion.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(creacion));

        // Un ordinal es un contrato que se rompe solo con reordenar el enumerado, y quien lo
        // reordena no ve que está rompiendo a nadie.
        (await creacion.Content.ReadAsStringAsync())
            .ShouldContain("\"verificacion\":\"VerificadoPorAlgoritmo\"");
    }

    // El mismo identificador tal como lo escribe una persona, con el ruido que el alta normaliza.
    private static string Salpicado(string identificador) =>
        " " + identificador[..4] + "." + identificador[4..8] + "-" + identificador[8..] + " ";

    private static ModificarTerceroDto Cambio() => new()
    {
        RazonSocial = "Razón Social Nueva",
        NombreComercial = "Comercial Nueva",
        DomicilioFiscal = Escenario.Domicilio(),
        EsCliente = true,
        EsProveedor = true,
    };

    private static CrearTerceroDto Alta(
        string pais,
        string numero,
        string razonSocial = "Tercero de prueba") => new()
        {
            Identificacion = new IdentificacionDeAltaDto { Pais = pais, Numero = numero },
            RazonSocial = razonSocial,
            DomicilioFiscal = Escenario.Domicilio(),
            EsCliente = true,
            EsProveedor = false,
        };

    private async Task<HttpClient> EnUnaEmpresaNuevaAsync(string nif)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(nif);
        _clientes.Add(cliente);

        return cliente;
    }

    private static async Task<TerceroDto> CrearAsync(
        HttpClient cliente,
        string pais,
        string numero,
        string razonSocial = "Tercero de prueba")
    {
        using HttpResponseMessage respuesta =
            await cliente.PostAsJsonAsync(Terceros, Alta(pais, numero, razonSocial));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<TerceroDto>())!;
    }

    private static async Task<TramoDe<TerceroDto>> BuscarAsync(
        HttpClient cliente,
        BuscarTercerosDto peticion)
    {
        using HttpResponseMessage respuesta =
            await cliente.PostAsJsonAsync($"{Terceros}/buscar", peticion);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<TramoDe<TerceroDto>>())!;
    }

    private static async Task<JsonElement> Problema(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
    }
}
