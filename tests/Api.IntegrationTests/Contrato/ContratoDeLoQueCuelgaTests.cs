using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Contrato;

/// <summary>
/// El contrato de lo que cuelga de la ficha —contactos, cuentas, condiciones de pago y límite de
/// crédito— visto desde fuera.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ningún IBAN de este fichero está pegado de ninguna parte.</b> Los tres se han calculado:
/// se escoge un cuerpo de relleno —veinte cifras iguales— y se busca por fuerza bruta el control
/// que cierra el mod-97, o uno que no lo cierre cuando hace falta un inválido. Un IBAN real es un
/// dato personal y, además, un medio de pago: una fixture no se queda en el fichero, viaja al
/// artefacto de resultados, al registro de la CI y al historial de git, para siempre y sin plazo.
/// Que sigan sin tener forma de real lo comprueba <c>NingunDatoConFormaDeRealTests</c>, que es
/// quien conoce la lista de formas prohibidas.
/// </para>
/// <para>
/// <b>Por qué estas reglas viven AQUÍ y no en <c>Terceros.UnitTests</c>.</b> Todas son del borde:
/// dicen qué código sale y por qué campo, no qué invariante sostiene el agregado. Los invariantes
/// —el tope de sesenta días, la única preferente, el IBAN que no se repite— tienen su batería en
/// el dominio, donde se recorren enteros sin levantar un contenedor. Lo que ahí no se puede
/// comprobar es lo que empieza en la capa de aplicación y termina en un <c>ProblemDetails</c>: el
/// proyecto de dominio no referencia <c>Application</c> a propósito (§13), así que una regla de
/// caso de uso que no esté aquí no está en ninguna parte.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ContratoDeLoQueCuelgaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Terceros = "/api/v1/terceros/terceros";

    // Los tres inventados. El cuerpo es relleno —veinte cifras iguales—, que es justo la forma que
    // el barrido del ADR-0030 reconoce como no-real; el control sale del mod-97 y está verificado.
    private const string IbanInventado = "ES7899999999999999999999";
    private const string OtroIbanInventado = "ES8200000000000000000000";
    private const string IbanConElControlMal = "ES7999999999999999999999";

    // Los dos desenlaces del choque, declarados en `RegistroDeSucesos.Observados`.
    private const int SucesoDelChoque = 8500;
    private const int SucesoDelChoqueSinVersion = 8501;

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    static ContratoDeLoQueCuelgaTests() => RegistroDeSucesos.Olvidar();

    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    /// <summary>
    /// Un límite sin divisa se rechaza, y lo que se comprueba es <b>el efecto</b>: que después no
    /// hay ningún límite guardado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Esta es la regla que sostiene la decisión escrita</b> de que la divisa no se hereda en
    /// silencio de la empresa. Sin ella, la decisión estaba en un comentario y en un <c>remarks</c>
    /// y no la guardaba nadie: quien añadiera un <c>?? empresa.DivisaBase</c> en el caso de uso
    /// habría dejado toda la suite verde.
    /// </para>
    /// <para>
    /// <b>Y por eso no basta con mirar el 400.</b> El modo de fallo que importa no es que deje de
    /// contestar 400, es que conteste 200 con una divisa que nadie escribió; se caza leyendo el
    /// recurso después y viendo que sigue vacío. El día que la herencia vuelva, este caso se pone
    /// rojo por las dos afirmaciones a la vez.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_limite_sin_divisa_es_400_y_despues_NO_hay_limite_heredado_de_la_empresa()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(131));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_001));

        using HttpResponseMessage respuesta = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Put,
            "limite-credito",
            new LimiteCreditoDeAltaDto { Cantidad = 5_000m, Divisa = null });

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));

        JsonElement problema = await Problema(respuesta);
        problema.GetProperty("errors").TryGetProperty("divisa", out _).ShouldBeTrue();

        LimiteCreditoDto despues = await LimiteAsync(cliente, tercero.Id);

        despues.Cantidad.ShouldBeNull("un límite rechazado no se guarda a medias");
        despues.Divisa.ShouldBeNull("la divisa de la empresa no se cuela como la del límite");
    }

    /// <summary>
    /// Una divisa sin importe también es 400: retirar el límite es vaciar <b>los dos</b> campos.
    /// </summary>
    /// <remarks>
    /// La rama recíproca de la anterior, y no sobra: sin ella, un cuerpo con divisa y sin cantidad
    /// podría entenderse como «retira el límite» y se estaría tragando en silencio un formulario a
    /// medio rellenar, que es exactamente el sitio donde alguien borró la cantidad sin querer.
    /// </remarks>
    [Fact]
    public async Task Una_divisa_sin_importe_es_400_porque_retirar_el_limite_es_vaciar_LOS_DOS()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(132));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_002));

        using HttpResponseMessage respuesta = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Put,
            "limite-credito",
            new LimiteCreditoDeAltaDto { Cantidad = null, Divisa = "EUR" });

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));

        (await Problema(respuesta)).GetProperty("errors")
            .TryGetProperty("cantidad", out _).ShouldBeTrue();
    }

    /// <summary>
    /// Una divisa que el catálogo no sabe redondear sale por el campo <c>divisa</c>, no por un 500.
    /// </summary>
    /// <remarks>
    /// Es la puerta doble del ADR-0004 vista desde fuera: dentro, pedir un importe en una divisa
    /// desconocida lanza; en el borde, pregunta primero y contesta por el campo. Tres letras que
    /// pasan el <c>StringLength</c> y no existen en ISO 4217 son un error de quien rellena, no del
    /// sistema.
    /// </remarks>
    [Fact]
    public async Task Una_divisa_que_el_catalogo_no_conoce_sale_por_su_campo_y_no_por_un_500()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(133));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_003));

        using HttpResponseMessage respuesta = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Put,
            "limite-credito",
            new LimiteCreditoDeAltaDto { Cantidad = 5_000m, Divisa = "XQZ" });

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));

        (await Problema(respuesta)).GetProperty("errors")
            .TryGetProperty("divisa", out _).ShouldBeTrue();
    }

    /// <summary>
    /// Un límite bien puesto va y vuelve con su divisa, y se lee igual en el <c>GET</c>.
    /// </summary>
    /// <remarks>
    /// El positivo que hace que los tres negativos signifiquen algo: sin él, un borde que
    /// contestara 400 a todo saldría verde en los otros tres casos.
    /// </remarks>
    [Fact]
    public async Task Un_limite_con_su_divisa_va_y_vuelve_y_se_lee_igual_en_el_GET()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(134));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_004));

        using HttpResponseMessage respuesta = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Put,
            "limite-credito",
            new LimiteCreditoDeAltaDto { Cantidad = 5_000m, Divisa = "EUR" });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        LimiteCreditoDto guardado = await LimiteAsync(cliente, tercero.Id);

        guardado.Cantidad.ShouldBe(5_000m);
        guardado.Divisa.ShouldBe("EUR");
    }

    /// <summary>
    /// Un IBAN con el control mal es 400 por su campo, y la respuesta <b>no dice cuál</b> de las
    /// tres condiciones falló.
    /// </summary>
    /// <remarks>
    /// La segunda mitad es la que importa y la que un negativo genérico se dejaría: un mensaje que
    /// dijera «el dígito de control no cuadra» convierte el formulario en el oráculo con el que se
    /// completa un número de cuenta a medio conocer. Se comprueba nombrando las tres —país,
    /// longitud y control— en el mismo texto, que es lo contrario de señalar una.
    /// </remarks>
    [Fact]
    public async Task Un_IBAN_con_el_control_mal_es_400_y_no_dice_CUAL_de_las_tres_condiciones_fallo()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(135));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_005));

        using HttpResponseMessage respuesta = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Post,
            "cuentas-bancarias",
            new CuentaBancariaDeAltaDto { Iban = IbanConElControlMal });

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));

        string dicho = (await Problema(respuesta)).GetProperty("errors")
            .GetProperty("iban")[0].GetString()!;

        dicho.ShouldContain("país");
        dicho.ShouldContain("longitud");
        dicho.ShouldContain("control");
    }

    /// <summary>
    /// Noventa días de plazo son 400, y el mensaje dice por qué —con la ley y con el número—.
    /// </summary>
    /// <remarks>
    /// El tope está en el dominio, donde lanza, y aquí se adelanta para que quien teclea el plazo
    /// lea el motivo en vez de recibir un 500. Lo que se afirma no es solo el código: es que el
    /// texto lleva el <b>60</b> y la norma, porque un «valor no válido» a secas hace que el
    /// usuario pruebe 89, 80, 70 hasta acertar.
    /// </remarks>
    [Fact]
    public async Task Noventa_dias_de_plazo_son_400_y_el_mensaje_lleva_el_tope_y_la_norma()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(136));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_006));

        using HttpResponseMessage respuesta = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Put,
            "condiciones-pago/Cliente",
            new CondicionPagoDeAltaDto { DiasDePlazo = 90 });

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));

        // Los mensajes se leen APLANADOS y no del cuerpo en crudo: el nombre del campo lo pone el
        // enlace de modelo con su convención de mayúsculas, y afirmar la clave sería afirmar un
        // detalle de ASP.NET Core en vez de lo que el usuario lee.
        string dicho = string.Join(" ", MensajesDe(await Problema(respuesta)));

        dicho.ShouldContain("60");
        dicho.ShouldContain("Ley 3/2004");
    }

    /// <summary>
    /// Sesenta días clavados se aceptan: el tope es el máximo legal, no un valor prohibido.
    /// </summary>
    /// <remarks>
    /// El borde de la frontera por el lado bueno. Sin él, un <c>Range(0, 59)</c> mal escrito
    /// dejaría el caso anterior verde y estaría rechazando el plazo que la ley sí permite.
    /// </remarks>
    [Fact]
    public async Task Sesenta_dias_clavados_se_aceptan_porque_el_tope_es_el_maximo_y_no_un_veto()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(137));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_007));

        using HttpResponseMessage respuesta = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Put,
            "condiciones-pago/Cliente",
            new CondicionPagoDeAltaDto { DiasDePlazo = 60 });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(respuesta));

        CondicionPagoDto fijada =
            (await respuesta.Content.ReadFromJsonAsync<CondicionPagoDto>())!;

        fijada.DiasDePlazo.ShouldBe(60);
        fijada.Rol.ShouldBe("Cliente");
    }

    /// <summary>
    /// Colgar una segunda cuenta preferente baja a la primera: al final queda <b>una</b>, y es la
    /// segunda.
    /// </summary>
    /// <remarks>
    /// <b>Por el efecto, no por el código.</b> El índice único parcial de la base también lo
    /// sostiene, pero si esto se comprobara esperando un 409 se estaría afirmando lo contrario de
    /// lo que se quiere: pedir que una cuenta pase a ser la preferente tiene que funcionar. Lo que
    /// se lee es la lista después, que es donde se ve si el agregado bajó a la anterior o si
    /// quedaron dos.
    /// </remarks>
    [Fact]
    public async Task Una_segunda_cuenta_preferente_baja_a_la_primera_y_solo_queda_UNA()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(138));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_008));

        using (HttpResponseMessage primera = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Post,
            "cuentas-bancarias",
            new CuentaBancariaDeAltaDto { Iban = IbanInventado, EsPreferente = true }))
        {
            primera.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(primera));
        }

        using (HttpResponseMessage segunda = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Post,
            "cuentas-bancarias",
            new CuentaBancariaDeAltaDto { Iban = OtroIbanInventado, EsPreferente = true }))
        {
            segunda.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(segunda));
        }

        IReadOnlyList<CuentaBancariaDto> cuentas = await CuentasAsync(cliente, tercero.Id);

        cuentas.Count.ShouldBe(2);
        cuentas.Count(cuenta => cuenta.EsPreferente).ShouldBe(1);
        cuentas.Single(cuenta => cuenta.EsPreferente).Iban.ShouldBe(OtroIbanInventado);
    }

    /// <summary>
    /// El mismo IBAN dos veces en la misma ficha es 409, y la respuesta <b>no lleva el número</b>.
    /// </summary>
    /// <remarks>
    /// Conflicto y no error de campo: el dato está bien escrito, lo que pasa es que ya está. Y sin
    /// el IBAN dentro, por lo mismo que el identificador fiscal duplicado — una respuesta de error
    /// no es sitio para devolver un número de cuenta, y quien la mandó ya lo tiene.
    /// </remarks>
    [Fact]
    public async Task El_mismo_IBAN_dos_veces_en_la_misma_ficha_es_409_y_sin_el_numero_dentro()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(139));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_009));

        using (HttpResponseMessage primera = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Post,
            "cuentas-bancarias",
            new CuentaBancariaDeAltaDto { Iban = IbanInventado }))
        {
            primera.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(primera));
        }

        using HttpResponseMessage repetida = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Post,
            "cuentas-bancarias",
            new CuentaBancariaDeAltaDto { Iban = IbanInventado });

        repetida.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(repetida));
        (await repetida.Content.ReadAsStringAsync()).ShouldNotContain(IbanInventado);
    }

    /// <summary>
    /// Escribir en lo que cuelga sin citar la versión de la <b>ficha</b> es 428.
    /// </summary>
    /// <remarks>
    /// El testigo es el del agregado y no el del hijo, y esta es la comprobación de que el borde lo
    /// exige de verdad en las subrutas nuevas y no solo en el <c>PUT</c> de la ficha. Sin ella,
    /// colgar un contacto sería la única escritura del módulo por la que se puede pisar a otro sin
    /// enterarse.
    /// </remarks>
    [Fact]
    public async Task Colgar_algo_sin_citar_la_version_de_la_ficha_es_428()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(140));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_010));

        using HttpResponseMessage respuesta = await cliente.EnviarConVersionAsync(
            HttpMethod.Post,
            $"{Terceros}/{tercero.Id}/contactos",
            etiqueta: null,
            JsonContent.Create(new ContactoDeAltaDto { Nombre = "Persona de contacto" }));

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.PreconditionRequired, await Escenario.Detalle(respuesta));
    }

    /// <summary>
    /// Colgar un contacto <b>mueve la versión de la ficha</b>, y la etiqueta anterior deja de valer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El caso que faltaba, y el que habría ahorrado el run 34097268237.</b> Los otros diez
    /// miran cada ruta por su lado; este mira el invariante que las cruza: quien cita una versión
    /// para escribir, escribe, y por tanto esa versión avanza. Si no avanza, dos personas pueden
    /// colgar cosas sobre la misma foto sin enterarse — que es exactamente lo que el <c>If-Match</c>
    /// está ahí para impedir.
    /// </para>
    /// <para>
    /// Y se afirma <b>en los dos sentidos</b>, porque cada uno caza un fallo distinto. Que la
    /// etiqueta cambie caza la versión que se queda quieta. Que la vieja dé <b>412</b> caza lo
    /// contrario: un borde que devuelva una etiqueta nueva cada vez —la hora, un contador— sin que
    /// el testigo de la base tenga nada que ver con ella.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Colgar_un_contacto_MUEVE_la_version_de_la_ficha_y_la_vieja_ya_no_vale()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(141));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_011));
        string ficha = $"{Terceros}/{tercero.Id}";

        string antes = await cliente.EtiquetaDeAsync(ficha);

        using (HttpResponseMessage colgado = await cliente.EnviarConVersionAsync(
            HttpMethod.Post,
            $"{ficha}/contactos",
            antes,
            JsonContent.Create(new ContactoDeAltaDto { Nombre = "Persona de contacto" })))
        {
            colgado.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(colgado));
        }

        string despues = await cliente.EtiquetaDeAsync(ficha);

        despues.ShouldNotBe(
            antes, "colgar algo del agregado tiene que mover la versión del agregado");

        // Y la vieja ya no vale: la etiqueta no es un adorno que cambia por su cuenta, sale del
        // testigo que la base compara.
        using HttpResponseMessage conLaVieja = await cliente.EnviarConVersionAsync(
            HttpMethod.Post,
            $"{ficha}/contactos",
            antes,
            JsonContent.Create(new ContactoDeAltaDto { Nombre = "Otra persona" }));

        conLaVieja.StatusCode.ShouldBe(
            HttpStatusCode.PreconditionFailed, await Escenario.Detalle(conLaVieja));
    }

    /// <summary>
    /// Un contacto se cuelga y se descuelga, y la lista queda como estaba.
    /// </summary>
    /// <remarks>
    /// El recorrido entero de la ruta que menos reglas tiene, que es justo por lo que hace falta:
    /// las otras se comprueban por su invariante y esta no tiene ninguno, así que sin este caso el
    /// alta y la baja de contactos serían las únicas escrituras del ítem que nada ejerce.
    /// </remarks>
    [Fact]
    public async Task Un_contacto_se_cuelga_y_se_descuelga_y_la_lista_queda_vacia()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(142));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_012));

        ContactoDto colgado;

        using (HttpResponseMessage alta = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Post,
            "contactos",
            new ContactoDeAltaDto { Nombre = "Persona de contacto", Cargo = "Compras" }))
        {
            alta.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(alta));
            colgado = (await alta.Content.ReadFromJsonAsync<ContactoDto>())!;
        }

        colgado.Nombre.ShouldBe("Persona de contacto");
        colgado.Cargo.ShouldBe("Compras");
        (await ContactosAsync(cliente, tercero.Id)).Count.ShouldBe(1);

        using (HttpResponseMessage baja = await EscribirAsync(
            cliente, tercero.Id, HttpMethod.Delete, $"contactos/{colgado.Id}"))
        {
            baja.StatusCode.ShouldBe(HttpStatusCode.NoContent, await DetalleAsync(baja));
        }

        (await ContactosAsync(cliente, tercero.Id)).ShouldBeEmpty();
    }

    /// <summary>
    /// Ascender una cuenta a preferente baja a la que lo era, y sigue quedando <b>una</b>.
    /// </summary>
    /// <remarks>
    /// La otra puerta a la misma restricción: en
    /// <c>Una_segunda_cuenta_preferente_baja_a_la_primera</c> la preferencia llega en el alta; aquí
    /// llega después, por su propia ruta. Son dos caminos de código distintos hasta el mismo
    /// invariante, y el que se olvida de bajar a la anterior es normalmente este.
    /// </remarks>
    [Fact]
    public async Task Ascender_una_cuenta_a_preferente_baja_a_la_que_lo_era()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(143));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_013));

        await ColgarCuentaAsync(cliente, tercero.Id, IbanInventado, preferente: true);
        CuentaBancariaDto segunda =
            await ColgarCuentaAsync(cliente, tercero.Id, OtroIbanInventado, preferente: false);

        using (HttpResponseMessage ascenso = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Post,
            $"cuentas-bancarias/{segunda.Id}/preferente"))
        {
            ascenso.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(ascenso));
        }

        IReadOnlyList<CuentaBancariaDto> cuentas = await CuentasAsync(cliente, tercero.Id);

        cuentas.Count(cuenta => cuenta.EsPreferente).ShouldBe(1);
        cuentas.Single(cuenta => cuenta.EsPreferente).Id.ShouldBe(segunda.Id);
    }

    /// <summary>
    /// Descolgar una cuenta la quita a ella y deja en pie a las demás.
    /// </summary>
    /// <remarks>
    /// La segunda mitad es la que no sobra. Un borrado que se lleve por delante la colección entera
    /// —o que borre por IBAN en vez de por identificador— pasaría igual de bien un caso que solo
    /// mirase que la borrada ya no está.
    /// </remarks>
    [Fact]
    public async Task Descolgar_una_cuenta_la_quita_a_ella_y_deja_en_pie_a_las_demas()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(144));
        TerceroDto tercero = await CrearAsync(cliente, Escenario.NifInventado(31_000_014));

        CuentaBancariaDto primera =
            await ColgarCuentaAsync(cliente, tercero.Id, IbanInventado, preferente: false);
        CuentaBancariaDto segunda =
            await ColgarCuentaAsync(cliente, tercero.Id, OtroIbanInventado, preferente: false);

        using (HttpResponseMessage baja = await EscribirAsync(
            cliente,
            tercero.Id,
            HttpMethod.Delete,
            $"cuentas-bancarias/{primera.Id}"))
        {
            baja.StatusCode.ShouldBe(HttpStatusCode.NoContent, await DetalleAsync(baja));
        }

        IReadOnlyList<CuentaBancariaDto> cuentas = await CuentasAsync(cliente, tercero.Id);

        cuentas.Select(cuenta => cuenta.Id).ShouldBe([segunda.Id]);
    }

    /// <summary>
    /// Un choque de verdad deja en la traza <b>qué</b> chocó y en qué estado, con su traza — y sin
    /// un solo valor dentro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Esto no es instrumentación de un fallo concreto: es el agujero de la política de errores,
    /// cerrado.</b> Un 412 es de las pocas respuestas que no puede llevar dentro por qué pasó —el
    /// cuerpo dice «alguien guardó antes», nunca <b>qué</b> chocó—, así que el único sitio donde
    /// eso puede constar es la traza. Y el cuerpo publica un <c>traceId</c> justamente para que
    /// alguien lo pegue y aparezca la petición: sin esta anotación, ese <c>traceId</c> era una
    /// promesa que el sistema no cumplía, y cada choque futuro volvía a ser un misterio.
    /// </para>
    /// <para>
    /// <b>Y afirma también lo que NO puede llevar.</b> Entre lo que puede chocar hay tablas con
    /// datos personales; un volcado de valores publicaría el teléfono de alguien en un registro
    /// que se agrega y se conserva. Por eso se comprueban las dos mitades: que el mensaje nombra
    /// la entidad y su estado, y que no lleva ni el identificador fiscal ni la razón social.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_choque_de_verdad_deja_en_la_traza_QUE_choco_y_en_que_estado()
    {
        RegistroDeSucesos.Olvidar();

        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(145));
        string numero = Escenario.NifInventado(31_000_015);
        TerceroDto tercero = await CrearAsync(cliente, numero);
        string ficha = $"{Terceros}/{tercero.Id}";

        // La etiqueta de antes de tocar nada, que es la que va a quedar caducada.
        string vieja = await cliente.EtiquetaDeAsync(ficha);

        using (HttpResponseMessage primera = await cliente.EnviarConVersionAsync(
            HttpMethod.Put, ficha, vieja, JsonContent.Create(Modificacion("Razón cambiada"))))
        {
            primera.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(primera));
        }

        // Y ahora la misma etiqueta otra vez: este es el choque que el diseño espera.
        using HttpResponseMessage choque = await cliente.EnviarConVersionAsync(
            HttpMethod.Put, ficha, vieja, JsonContent.Create(Modificacion("Razón pisada")));

        choque.StatusCode.ShouldBe(
            HttpStatusCode.PreconditionFailed, await Escenario.Detalle(choque));

        JsonElement problema = await Problema(choque);
        string traza = problema.GetProperty("traceId").GetString()!;

        // Con testigo, el cuerpo SÍ puede decir la versión de ahora: es el desenlace 8500.
        problema.TryGetProperty("versionActual", out _).ShouldBeTrue(
            "un choque contra una entidad CON testigo tiene que poder decir la versión actual");

        IReadOnlyList<RegistroDeSucesos.Suceso> anotados = RegistroDeSucesos.Con(SucesoDelChoque);

        anotados.Count.ShouldBe(
            1,
            "el choque tiene que dejar su anotación. Si no hay ninguna, lo primero que hay que " +
            "mirar es el captador y no la traza: `RegistroDeSucesos` solo recoge lo declarado en " +
            "`Observados`");

        string dicho = anotados[0].Mensaje;

        dicho.ShouldContain(
            "Tercero:Modified",
            customMessage:
            "la anotación tiene que decir QUÉ chocó y en qué estado iba; sin eso, el choque " +
            "siguiente vuelve a ser un misterio");
        dicho.ShouldContain(
            traza,
            customMessage:
            "y tiene que llevar la MISMA traza que el cuerpo publica, que es lo que hace que " +
            "pegar el traceId sirva para algo");

        // Nombres y estados, ningún valor. La otra mitad de la regla.
        dicho.ShouldNotContain(numero);
        dicho.ShouldNotContain("Razón cambiada");
        dicho.ShouldNotContain("Razón pisada");
    }

    private static ModificarTerceroDto Modificacion(string razonSocial) => new()
    {
        RazonSocial = razonSocial,
        DomicilioFiscal = Escenario.Domicilio(),
        EsCliente = true,
        EsProveedor = true,
    };

    // El detalle de siempre MÁS lo que la API anotó por dentro sobre choques. Un 412 no puede
    // decir en el cuerpo qué chocó, así que cuando una escritura que debía salir bien devuelve
    // 412, el mensaje del fallo tiene que traerlo de la traza o no hay por dónde empezar.
    private static async Task<string> DetalleAsync(HttpResponseMessage respuesta)
    {
        string detalle = await Escenario.Detalle(respuesta);

        IEnumerable<RegistroDeSucesos.Suceso> choques =
            [.. RegistroDeSucesos.Con(SucesoDelChoque),
             .. RegistroDeSucesos.Con(SucesoDelChoqueSinVersion)];

        string anotado = string.Join(" | ", choques.Select(suceso => suceso.Mensaje));

        return string.IsNullOrEmpty(anotado) ? detalle : $"{detalle} — anotado: {anotado}";
    }

    private async Task<HttpClient> EnUnaEmpresaNuevaAsync(string nif)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(nif);
        _clientes.Add(cliente);

        return cliente;
    }

    private static async Task<TerceroDto> CrearAsync(HttpClient cliente, string numero)
    {
        CrearTerceroDto alta = new()
        {
            Identificacion = new IdentificacionDeAltaDto { Pais = "ES", Numero = numero },
            RazonSocial = "Tercero de prueba",
            DomicilioFiscal = Escenario.Domicilio(),
            EsCliente = true,
            EsProveedor = true,
        };

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Terceros, alta);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<TerceroDto>())!;
    }

    // La versión sale SIEMPRE de la ficha y se lee justo antes de cada escritura: lo que cuelga no
    // tiene ETag propio, y cada escritura que sale bien deja la anterior caducada.
    // La misma, para las rutas que no llevan cuerpo. Y no es `JsonContent.Create<object?>(null)`:
    // eso manda la cadena `null` con `Content-Type: application/json`, que es un cuerpo, solo que
    // uno que no dice nada.
    private static async Task<HttpResponseMessage> EscribirAsync(
        HttpClient cliente,
        Guid terceroId,
        HttpMethod metodo,
        string subruta)
    {
        string ficha = $"{Terceros}/{terceroId}";
        string etiqueta = await cliente.EtiquetaDeAsync(ficha);

        return await cliente.EnviarConVersionAsync(metodo, $"{ficha}/{subruta}", etiqueta);
    }

    private static async Task<HttpResponseMessage> EscribirAsync<T>(
        HttpClient cliente,
        Guid terceroId,
        HttpMethod metodo,
        string subruta,
        T cuerpo)
    {
        string ficha = $"{Terceros}/{terceroId}";
        string etiqueta = await cliente.EtiquetaDeAsync(ficha);

        return await cliente.EnviarConVersionAsync(
            metodo, $"{ficha}/{subruta}", etiqueta, JsonContent.Create(cuerpo));
    }

    private static async Task<LimiteCreditoDto> LimiteAsync(HttpClient cliente, Guid terceroId)
    {
        using HttpResponseMessage respuesta =
            await cliente.GetAsync($"{Terceros}/{terceroId}/limite-credito");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<LimiteCreditoDto>())!;
    }

    private static async Task<CuentaBancariaDto> ColgarCuentaAsync(
        HttpClient cliente,
        Guid terceroId,
        string iban,
        bool preferente)
    {
        using HttpResponseMessage respuesta = await EscribirAsync(
            cliente,
            terceroId,
            HttpMethod.Post,
            "cuentas-bancarias",
            new CuentaBancariaDeAltaDto { Iban = iban, EsPreferente = preferente });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await DetalleAsync(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<CuentaBancariaDto>())!;
    }

    private static async Task<IReadOnlyList<ContactoDto>> ContactosAsync(
        HttpClient cliente,
        Guid terceroId)
    {
        using HttpResponseMessage respuesta =
            await cliente.GetAsync($"{Terceros}/{terceroId}/contactos");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<List<ContactoDto>>())!;
    }

    private static async Task<IReadOnlyList<CuentaBancariaDto>> CuentasAsync(
        HttpClient cliente,
        Guid terceroId)
    {
        using HttpResponseMessage respuesta =
            await cliente.GetAsync($"{Terceros}/{terceroId}/cuentas-bancarias");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<List<CuentaBancariaDto>>())!;
    }

    // Todos los mensajes de `errors`, vengan del campo que vengan.
    private static IEnumerable<string> MensajesDe(JsonElement problema) =>
        problema.GetProperty("errors")
            .EnumerateObject()
            .SelectMany(campo => campo.Value.EnumerateArray())
            .Select(mensaje => mensaje.GetString() ?? string.Empty);

    private static async Task<JsonElement> Problema(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
    }
}
