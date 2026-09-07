using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Organizacion.Contracts.Unidades;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Retiradas;

/// <summary>
/// Las dos decisiones que el ADR-0023 deja sobre las conversiones: que los dos sentidos no puedan
/// contradecirse, y que un par no declarado falle con nombre en vez de componerse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los dos sentidos son dos filas a propósito</b> —el inverso de 12 no cabe en seis decimales,
/// así que la vuelta se declara con su propio redondeo pensado—, y lo que faltaba era el límite:
/// se podía declarar <c>caja→ud = 12</c> y <c>ud→caja = 0,5</c>, y las dos pasaban todo lo que
/// había. Un inventario valorado con esas dos filas cuadra por un lado y descuadra por el otro,
/// sin un solo error.
/// </para>
/// <para>
/// <b>Y no hay transitividad.</b> Con <c>kg→g</c> y <c>g→mg</c> declarados, <c>kg→mg</c> no vale
/// 1000000: no existe. El dominio ya lo decía; lo que faltaba —y es la mitad que se usa— era qué
/// pasa cuando alguien lo pide.
/// </para>
/// <para>
/// Cada caso se lleva sus propias unidades, con código propio: el par tiene índice único y los
/// tests de esta colección comparten base, así que reutilizar códigos haría que el segundo en
/// correr chocara y el fallo se leyera como un error de la API.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaInversaYElResolutorTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Unidades = "/api/v1/organizacion/unidades-de-medida";
    private const string Conversiones = "/api/v1/organizacion/conversiones-de-unidades";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    /// <summary>El caso del ADR: doce por caja y media caja por unidad no pueden convivir.</summary>
    [Fact]
    public async Task La_inversa_que_no_lo_es_se_rechaza_en_el_alta()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(154));
        using HttpClient suyo = cliente;

        (Guid caja, Guid unidad) = await DosUnidadesAsync(cliente, "INV1");

        await DeclararAsync(cliente, caja, unidad, 12m);

        // 0,5 no es el inverso de 12 ni redondeando: 12 × 0,5 = 6, y el margen que la escala
        // explica es de seis millonésimas. La libertad que da declarar los dos sentidos por
        // separado es la de elegir el redondeo, no la de declarar otro número.
        using HttpResponseMessage choque = await cliente.PostAsJsonAsync(
            Conversiones, Nueva(unidad, caja, 0.5m));

        choque.StatusCode.ShouldBe(
            HttpStatusCode.Conflict, await Escenario.Detalle(choque));

        (await TipoDeAsync(choque)).ShouldBe("/errors/conversion-um-inversa-implausible");
    }

    /// <summary>El redondeo legítimo de 1/12 sí entra, y el que no lo es no.</summary>
    /// <remarks>
    /// <para>
    /// <c>0,083333</c> es <c>1/12</c> redondeado a seis decimales: se separa 3,33·10⁻⁷ del valor
    /// exacto, menos de la media unidad del último decimal que la escala puede producir, y el
    /// producto se queda a 4·10⁻⁶ de 1 con un margen de 6,04·10⁻⁶.
    /// </para>
    /// <para>
    /// <b><c>0,083334</c> no entra, y el motivo es aritmético.</b> Se separa 6,67·10⁻⁷ de
    /// <c>1/12</c>, o sea <b>más</b> de la media unidad del último decimal: no es un redondeo de
    /// <c>1/12</c>, es el redondeo de otro número. El producto se va a 8·10⁻⁶ con el mismo margen
    /// de 6,04·10⁻⁶. Admitirlo exigiría ensanchar la tolerancia, y una tolerancia ensanchada para
    /// que quepa un caso concreto es exactamente el número elegido por comodidad que el ADR evita.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("0.083333", true)]
    [InlineData("0.083334", false)]
    public async Task El_redondeo_de_la_inversa_entra_solo_si_la_escala_lo_explica(
        string inverso,
        bool admitido)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(
            admitido ? Escenario.NifInventado(155) : Escenario.NifInventado(156));

        using HttpClient suyo = cliente;

        (Guid caja, Guid unidad) = await DosUnidadesAsync(
            cliente, admitido ? "INV2" : "INV3");

        await DeclararAsync(cliente, caja, unidad, 12m);

        using HttpResponseMessage vuelta = await cliente.PostAsJsonAsync(
            Conversiones,
            Nueva(unidad, caja, decimal.Parse(inverso, CultureInfo.InvariantCulture)));

        vuelta.StatusCode.ShouldBe(
            admitido ? HttpStatusCode.Created : HttpStatusCode.Conflict,
            await Escenario.Detalle(vuelta));
    }

    /// <summary>
    /// El caso frontera del propio margen, que es donde una desigualdad se equivoca de signo.
    /// </summary>
    /// <remarks>
    /// <c>1,001 × 0,999 = 0,999999</c>, o sea exactamente <c>10⁻⁶</c> de 1; y el margen,
    /// <c>5·10⁻⁷ × (1,001 + 0,999) = 5·10⁻⁷ × 2 = 10⁻⁶</c>. Los dos números son <b>el mismo</b>.
    /// Con <c>≤</c> entra, que es lo correcto —el margen es el error máximo que la escala puede
    /// producir, así que separarse exactamente eso es un redondeo legítimo—; con <c>&lt;</c> se
    /// rechazaría el único caso que la escala produce de verdad. El vecino de al lado,
    /// <c>0,998999</c>, es el siguiente factor representable y ya se pasa: no hay hueco entre los
    /// dos donde una desigualdad equivocada pueda esconderse.
    /// </remarks>
    [Theory]
    [InlineData("0.999", true)]
    [InlineData("0.998999", false)]
    public async Task El_par_que_se_separa_exactamente_el_margen_entra_y_el_de_al_lado_no(
        string inverso,
        bool admitido)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(
            admitido ? Escenario.NifInventado(157) : Escenario.NifInventado(158));

        using HttpClient suyo = cliente;

        (Guid ida, Guid vuelta) = await DosUnidadesAsync(
            cliente, admitido ? "FRO1" : "FRO2");

        await DeclararAsync(cliente, ida, vuelta, 1.001m);

        using HttpResponseMessage contraria = await cliente.PostAsJsonAsync(
            Conversiones,
            Nueva(vuelta, ida, decimal.Parse(inverso, CultureInfo.InvariantCulture)));

        contraria.StatusCode.ShouldBe(
            admitido ? HttpStatusCode.Created : HttpStatusCode.Conflict,
            await Escenario.Detalle(contraria));
    }

    /// <summary>
    /// La modificación vuelve a comprobar la inversa, que es por donde se rompe de verdad.
    /// </summary>
    /// <remarks>
    /// Alta de <c>A→B</c>, alta de <c>B→A</c> —las dos pasan, porque en ese momento casan— y
    /// después alguien <b>modifica</b> la primera. Una comprobación que solo mirara el alta
    /// dejaría pasar exactamente este camino, y lo dejaría pasar en verde: el par quedaría
    /// contradiciéndose sin que nadie hubiera visto un error nunca.
    /// </remarks>
    [Fact]
    public async Task La_modificacion_vuelve_a_comprobar_la_inversa()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(159));
        using HttpClient suyo = cliente;

        (Guid caja, Guid unidad) = await DosUnidadesAsync(cliente, "MOD1");

        ConversionUmDto ida = await DeclararAsync(cliente, caja, unidad, 12m);
        await DeclararAsync(cliente, unidad, caja, 0.083333m);

        using HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Conversiones}/{ida.Id}", new ModificarConversionUmDto { Factor = 2m });

        cambio.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "el par ya no casaría: 2 × 0,083333 = 0,166666, que no es 1. La inversa que se dio " +
            "de alta hace un momento sigue estando, y sigue siendo verdad. " +
            await Escenario.Detalle(cambio));

        (await TipoDeAsync(cambio)).ShouldBe("/errors/conversion-um-inversa-implausible");

        // Y un cambio que sí casa entra por la misma puerta: sin esto, un `Modificar` que
        // rechazara siempre pasaría la aserción de arriba.
        using HttpResponseMessage bueno = await cliente.ModificarAsync(
            $"{Conversiones}/{ida.Id}", new ModificarConversionUmDto { Factor = 12.000001m });

        bueno.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(bueno));
    }

    /// <summary>
    /// Una fila retirada sigue restringiendo a su inversa, que es la decisión que el ADR dejaba
    /// abierta.
    /// </summary>
    /// <remarks>
    /// «Sigue resolviendo» significa que sigue siendo verdad. Si retirarla la sacara de esta
    /// comprobación, retirar el sentido incómodo sería la manera de declarar cualquier número en
    /// el otro — y una salida que la propia regla ofrece no es una salida: es un agujero con
    /// permiso.
    /// </remarks>
    [Fact]
    public async Task Una_conversion_retirada_sigue_restringiendo_a_su_inversa()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(160));
        using HttpClient suyo = cliente;

        (Guid caja, Guid unidad) = await DosUnidadesAsync(cliente, "RET1");

        ConversionUmDto ida = await DeclararAsync(cliente, caja, unidad, 12m);

        using HttpResponseMessage retirada = await cliente.AccionarAsync(
            $"{Conversiones}/{ida.Id}", $"{Conversiones}/{ida.Id}/retirada", HttpMethod.Post);

        retirada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(retirada));

        using HttpResponseMessage choque = await cliente.PostAsJsonAsync(
            Conversiones, Nueva(unidad, caja, 0.5m));

        choque.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "retirar el sentido que estorba no es la manera de declarar cualquier número en el " +
            "otro. " + await Escenario.Detalle(choque));

        (await TipoDeAsync(choque)).ShouldBe("/errors/conversion-um-inversa-implausible");
    }

    /// <summary>
    /// El par declarado se resuelve; el que se deduciría encadenando dos, no.
    /// </summary>
    /// <remarks>
    /// El caso que importa no es el par declarado: es <c>kg→g</c> y <c>g→mg</c> declarados,
    /// <c>kg→mg</c> preguntado. La respuesta es el error con nombre y no un 1000000. Las tres
    /// salidas que un resolutor descuidado daría —multiplicar, cero, nulo— son las tres peores:
    /// las dos últimas convierten cualquier existencia en nada y en silencio, y la primera
    /// devuelve un número que nadie declaró como si alguien lo hubiera declarado.
    /// </remarks>
    [Fact]
    public async Task El_par_que_habria_que_encadenar_no_se_resuelve()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(161));
        using HttpClient suyo = cliente;

        Guid kilo = await UnidadAsync(cliente, "CAD-KG", "Kilo de la cadena", 3);
        Guid gramo = await UnidadAsync(cliente, "CAD-G", "Gramo de la cadena", 3);
        Guid miligramo = await UnidadAsync(cliente, "CAD-MG", "Miligramo de la cadena", 3);

        await DeclararAsync(cliente, kilo, gramo, 1000m);
        await DeclararAsync(cliente, gramo, miligramo, 1000m);

        // 1. El par declarado sale, con su factor. Sin esta mitad, un resolutor que fallara
        //    siempre pasaría la de abajo.
        using HttpResponseMessage declarado = await cliente.GetAsync(
            $"{Conversiones}/resolucion?origen={kilo}&destino={gramo}");

        declarado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(declarado));

        ResolucionDeConversionDto resuelta =
            (await declarado.Content.ReadFromJsonAsync<ResolucionDeConversionDto>())!;

        resuelta.Factor.ShouldBe(1000m);
        resuelta.Retirada.ShouldBeFalse();

        // 2. El que habría que componer, no. Y lo que NO puede salir es 1000000.
        using HttpResponseMessage encadenado = await cliente.GetAsync(
            $"{Conversiones}/resolucion?origen={kilo}&destino={miligramo}");

        encadenado.StatusCode.ShouldBe(
            HttpStatusCode.NotFound,
            "kg→mg no está declarado: encadenar kg→g con g→mg multiplicaría el error de dos " +
            "redondeos y devolvería un número que nadie declaró. " +
            await Escenario.Detalle(encadenado));

        (await TipoDeAsync(encadenado)).ShouldBe("/errors/conversion-um-no-declarada");

        string cuerpo = await encadenado.Content.ReadAsStringAsync();

        cuerpo.Contains("1000000", StringComparison.Ordinal).ShouldBeFalse(
            "la respuesta no puede llevar el producto de la cadena ni siquiera de adorno: es " +
            "justo el número que no existe");
    }

    /// <summary>Y el sentido contrario tampoco se deduce invirtiendo.</summary>
    /// <remarks>
    /// Preguntar <c>B→A</c> con solo <c>A→B</c> declarado falla igual: <c>1/12</c> no cabe en seis
    /// decimales, y por eso los dos sentidos son dos filas. Un resolutor que invirtiera aquí
    /// desharía la decisión entera del maestro.
    /// </remarks>
    [Fact]
    public async Task El_sentido_contrario_no_se_deduce_invirtiendo()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(162));
        using HttpClient suyo = cliente;

        (Guid caja, Guid unidad) = await DosUnidadesAsync(cliente, "SIM1");

        await DeclararAsync(cliente, caja, unidad, 12m);

        using HttpResponseMessage contraria = await cliente.GetAsync(
            $"{Conversiones}/resolucion?origen={unidad}&destino={caja}");

        contraria.StatusCode.ShouldBe(
            HttpStatusCode.NotFound, await Escenario.Detalle(contraria));

        (await TipoDeAsync(contraria)).ShouldBe("/errors/conversion-um-no-declarada");
    }

    /// <summary>Una conversión retirada sigue resolviendo, y la respuesta lo dice.</summary>
    [Fact]
    public async Task Una_conversion_retirada_sigue_resolviendo_y_lo_dice()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(163));
        using HttpClient suyo = cliente;

        (Guid caja, Guid unidad) = await DosUnidadesAsync(cliente, "RES1");

        ConversionUmDto conversion = await DeclararAsync(cliente, caja, unidad, 12m);

        using HttpResponseMessage retirada = await cliente.AccionarAsync(
            $"{Conversiones}/{conversion.Id}",
            $"{Conversiones}/{conversion.Id}/retirada",
            HttpMethod.Post);

        retirada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(retirada));

        using HttpResponseMessage resolucion = await cliente.GetAsync(
            $"{Conversiones}/resolucion?origen={caja}&destino={unidad}");

        resolucion.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "resolver es exactamente lo que una fila retirada sigue haciendo: un albarán de hace " +
            "tres años que apunta a esta conversión tiene que poder seguir leyéndose. " +
            await Escenario.Detalle(resolucion));

        ResolucionDeConversionDto resuelta =
            (await resolucion.Content.ReadFromJsonAsync<ResolucionDeConversionDto>())!;

        resuelta.Factor.ShouldBe(12m);
        resuelta.Retirada.ShouldBeTrue(
            "y quien la use para una operación nueva tiene derecho a saber que ese camino ya no " +
            "se ofrece");
    }

    private static CrearConversionUmDto Nueva(Guid origen, Guid destino, decimal factor) =>
        new() { UnidadOrigenId = origen, UnidadDestinoId = destino, Factor = factor };

    private static async Task<string> TipoDeAsync(HttpResponseMessage respuesta)
    {
        JsonNode problema = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!;

        return problema["type"]!.GetValue<string>();
    }

    private static async Task<(Guid Origen, Guid Destino)> DosUnidadesAsync(
        HttpClient cliente,
        string prefijo) =>
        (await UnidadAsync(cliente, $"{prefijo}-O", $"Origen de {prefijo}", 2),
         await UnidadAsync(cliente, $"{prefijo}-D", $"Destino de {prefijo}", 2));

    private static async Task<Guid> UnidadAsync(
        HttpClient cliente,
        string codigo,
        string nombre,
        int decimales)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto { Codigo = codigo, Nombre = nombre, Decimales = decimales });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<UnidadMedidaDto>())!.Id;
    }

    private static async Task<ConversionUmDto> DeclararAsync(
        HttpClient cliente,
        Guid origen,
        Guid destino,
        decimal factor)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Conversiones, Nueva(origen, destino, factor));

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ConversionUmDto>())!;
    }
}
