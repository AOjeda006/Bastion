using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Bloqueos;

/// <summary>
/// El alta contra un tercero <b>bloqueado</b> y el alta contra uno que <b>ya existe</b> contestan
/// lo mismo, y se demuestra poniendo las dos respuestas una al lado de la otra.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una propiedad, no una redacción.</b> Un test que afirmara «el mensaje no dice
/// bloqueado» comprobaría la redacción de hoy: cambiarla por «ese identificador está reservado»
/// lo dejaría verde y la fuga estaría abierta. Lo que hay que demostrar es indistinguibilidad, y
/// eso solo se ve comparando: mismo código de estado, mismo <c>type</c> y mismo cuerpo salvo el
/// identificador de traza, que es lo único que puede diferir sin significar nada porque son dos
/// peticiones distintas.
/// </para>
/// <para>
/// <b>Por qué importa tanto.</b> Si las dos respuestas se distinguieran, cualquiera con permiso
/// para dar de alta un tercero podría recorrer identificadores fiscales y sacar la lista de quién
/// está dado de baja en esa empresa — que es exactamente la información que el artículo 32 de la
/// LOPDGDD manda reservar. El formulario de alta sería el censo de bajas.
/// </para>
/// <para>
/// <b>Y hay una tercera respuesta en la comparación</b>, que es la que impide el falso verde: el
/// alta contra un identificador LIBRE tiene que salir <c>201</c>. Sin ella, un servidor que
/// contestara <c>409</c> a todo —o que estuviera roto de la misma manera en los dos casos— pasaría
/// este test con las dos respuestas idénticas y sin haber comprobado nada.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElConflictoQueNoRevelaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Terceros = "/api/v1/terceros/terceros";
    private const int SucesoDelIdentificadorOcupado = 8400;

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
    public async Task El_alta_contra_uno_bloqueado_y_contra_uno_activo_contestan_lo_MISMO()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(102));

        // Uno activo y uno bloqueado, con identificadores distintos: el escenario en el que la
        // fuga sería explotable. Un mismo número no puede estar activo y bloqueado a la vez, así
        // que la comparación NO puede tolerar que el número salga en el cuerpo — y no sale.
        TerceroDto activo = await CrearAsync(cliente, Numero(11));
        TerceroDto bloqueado = await CrearAsync(cliente, Numero(12));
        await BloquearAsync(cliente, bloqueado.Id);

        // El testigo del barrido: contra un identificador libre esto NO es un conflicto. Sin esta
        // línea, dos 409 idénticos por cualquier otro motivo pasarían por indistinguibilidad.
        using HttpResponseMessage libre = await cliente.PostAsJsonAsync(Terceros, Alta(Numero(13)));
        libre.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(libre));

        using HttpResponseMessage contraElActivo =
            await cliente.PostAsJsonAsync(Terceros, Alta(activo.Identificacion.Numero));
        using HttpResponseMessage contraElBloqueado =
            await cliente.PostAsJsonAsync(Terceros, Alta(bloqueado.Identificacion.Numero));

        contraElActivo.StatusCode.ShouldBe(
            HttpStatusCode.Conflict, await Escenario.Detalle(contraElActivo));

        contraElBloqueado.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "el alta contra un tercero bloqueado tiene que contestar el MISMO conflicto que " +
            "contra uno que ya existe: " + await Escenario.Detalle(contraElBloqueado));

        contraElBloqueado.Content.Headers.ContentType?.ToString()
            .ShouldBe(contraElActivo.Content.Headers.ContentType?.ToString());

        string cuerpoDelActivo = await Comparable(contraElActivo);
        string cuerpoDelBloqueado = await Comparable(contraElBloqueado);

        // Entero contra entero, con el identificador de traza sustituido en los dos por la misma
        // constante. Es lo único que se normaliza, y hay que mirarlo con desconfianza: un test que
        // decide qué trozos no cuenta es un test que decide qué se puede filtrar.
        cuerpoDelBloqueado.ShouldBe(
            cuerpoDelActivo,
            "las dos respuestas de conflicto se distinguen, así que el formulario de alta dice " +
            "quién está bloqueado." + Environment.NewLine +
            "   contra el activo:    " + cuerpoDelActivo + Environment.NewLine +
            "   contra el bloqueado: " + cuerpoDelBloqueado);

        // Y no son iguales por estar vacías: el `type` es el del catálogo (ADR-0030), que es lo
        // que el frontal traduce.
        JsonNode problema = JsonNode.Parse(await contraElActivo.Content.ReadAsStringAsync())!;
        problema["type"]!.GetValue<string>().ShouldBe("/errors/tercero-duplicado");
    }

    /// <summary>
    /// Lo que la respuesta calla, la traza lo dice: por dentro sí queda escrito cuál de los dos
    /// era.
    /// </summary>
    /// <remarks>
    /// Es la otra mitad del artículo 32 y va en la dirección contraria a la del caso de arriba.
    /// Reservar los datos obliga a poder responder quién miró una ficha reservada y cuándo; lo que
    /// no obliga —ni permite— es contárselo a quien rellenó el formulario. Sin este caso, «la traza
    /// lo registra» sería una promesa escrita en un comentario: borrar la anotación no pondría
    /// nada rojo.
    /// </remarks>
    [Fact]
    public async Task La_traza_SI_dice_cual_de_los_dos_era_y_no_lleva_el_identificador_dentro()
    {
        RegistroDeSucesos.Olvidar();

        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(103));

        TerceroDto activo = await CrearAsync(cliente, Numero(21));
        TerceroDto bloqueado = await CrearAsync(cliente, Numero(22));
        await BloquearAsync(cliente, bloqueado.Id);

        using HttpResponseMessage contraElActivo =
            await cliente.PostAsJsonAsync(Terceros, Alta(activo.Identificacion.Numero));
        using HttpResponseMessage contraElBloqueado =
            await cliente.PostAsJsonAsync(Terceros, Alta(bloqueado.Identificacion.Numero));

        contraElActivo.StatusCode.ShouldBe(
            HttpStatusCode.Conflict, await Escenario.Detalle(contraElActivo));
        contraElBloqueado.StatusCode.ShouldBe(
            HttpStatusCode.Conflict, await Escenario.Detalle(contraElBloqueado));

        IReadOnlyList<RegistroDeSucesos.Suceso> anotados =
            RegistroDeSucesos.Con(SucesoDelIdentificadorOcupado);

        anotados.Count.ShouldBe(
            2,
            "los dos choques tienen que dejar su anotación. Si no hay ninguna, lo primero que hay " +
            "que mirar es el captador y no la traza: `RegistroDeSucesos` solo recoge lo declarado " +
            "en `Observados`");

        // Uno dice que la ficha que estorbaba estaba bloqueada y el otro que no: POR DENTRO sí se
        // distinguen, que es justo lo contrario de lo que hacen las respuestas.
        anotados.Count(suceso => suceso.Mensaje.EndsWith("bloqueada: True.", StringComparison.Ordinal))
            .ShouldBe(1, "una de las dos anotaciones tiene que decir que la ficha estaba bloqueada");

        anotados.Count(suceso => suceso.Mensaje.EndsWith("bloqueada: False.", StringComparison.Ordinal))
            .ShouldBe(1, "y la otra, que no lo estaba");

        // Y ninguna de las dos lleva el identificador fiscal dentro: un NIF es un dato personal, y
        // el registro se agrega, se exporta y se conserva con menos ceremonia que la base de datos.
        foreach (RegistroDeSucesos.Suceso suceso in anotados)
        {
            suceso.Mensaje.ShouldNotContain(activo.Identificacion.Numero);
            suceso.Mensaje.ShouldNotContain(bloqueado.Identificacion.Numero);
        }
    }

    /// <summary>
    /// La unicidad abarca también lo bloqueado, y por eso desbloquear nunca se encuentra una
    /// colisión que resolver.
    /// </summary>
    /// <remarks>
    /// Es la decisión del ítem comprobada <b>por el efecto</b>, y no por lo que diga la
    /// configuración. Si el índice fuera parcial —<c>WHERE bloqueado = false</c>—, el segundo alta
    /// pasaría, el desbloqueo dejaría dos filas con la misma llave y alguien tendría que deshacer
    /// a mano un empate con datos personales por medio. Aquí el segundo alta choca, y esa es la
    /// contrapartida que se ha elegido pagar: el identificador de un tercero bloqueado sigue
    /// ocupado durante todo el plazo del art. 32.
    /// </remarks>
    [Fact]
    public async Task Bloquear_un_tercero_no_libera_su_identificador_y_por_eso_desbloquear_no_choca()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(104));

        TerceroDto tercero = await CrearAsync(cliente, Numero(31));
        await BloquearAsync(cliente, tercero.Id);

        using HttpResponseMessage segundoIntento =
            await cliente.PostAsJsonAsync(Terceros, Alta(tercero.Identificacion.Numero));

        segundoIntento.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "el identificador de un tercero bloqueado sigue ocupado: " +
            await Escenario.Detalle(segundoIntento));

        using HttpResponseMessage desbloqueo = await cliente.EnviarConVersionAsync(
            HttpMethod.Post, $"{Terceros}/{tercero.Id}/desbloqueo", etiqueta: null);

        desbloqueo.StatusCode.ShouldBe(
            HttpStatusCode.NoContent, await Escenario.Detalle(desbloqueo));

        // Y vuelve entero, con su identificador: no ha habido ningún empate que deshacer.
        TerceroDto vuelto =
            (await cliente.GetFromJsonAsync<TerceroDto>($"{Terceros}/{tercero.Id}"))!;

        vuelto.Identificacion.Numero.ShouldBe(tercero.Identificacion.Numero);
    }

    /// <summary>
    /// El mismo identificador en otra empresa se da de alta sin conflicto: la llave lleva la
    /// empresa delante.
    /// </summary>
    /// <remarks>
    /// Sin esto, una unicidad escrita solo sobre el identificador pasaría los tres casos de arriba
    /// —todos ocurren dentro de una empresa— y el primer cliente que fuera proveedor de dos
    /// empresas del grupo se encontraría un conflicto incomprensible.
    /// </remarks>
    [Fact]
    public async Task El_mismo_identificador_en_otra_empresa_se_da_de_alta_sin_conflicto()
    {
        string numero = Numero(41);

        HttpClient una = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(105));
        await CrearAsync(una, numero);

        HttpClient otra = await EnUnaEmpresaNuevaAsync(Escenario.NifInventado(106));
        TerceroDto elMismo = await CrearAsync(otra, numero);

        elMismo.Identificacion.Numero.ShouldBe(numero);
    }

    // El cuerpo del ProblemDetails con el identificador de traza sustituido, que es lo único que
    // puede diferir entre dos peticiones sin significar nada. Las claves se ordenan porque el
    // orden del JSON no es parte del contrato y una diferencia de orden no sería una fuga; el
    // resto del cuerpo entra tal cual, incluido cualquier campo que alguien añada mañana.
    private static async Task<string> Comparable(HttpResponseMessage respuesta)
    {
        JsonObject problema =
            JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!.AsObject();

        if (problema.ContainsKey("traceId"))
        {
            problema["traceId"] = "«el de cada petición»";
        }

        SortedDictionary<string, string> ordenado = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonNode?> campo in problema)
        {
            ordenado[campo.Key] = campo.Value?.ToJsonString() ?? "null";
        }

        return JsonSerializer.Serialize(ordenado);
    }

    // Identificadores extranjeros, y a propósito. Lo que estos casos necesitan son números libres
    // y distintos entre sí, no caracteres de control: con NIF españoles habría que ir generando
    // valores válidos y el test hablaría de dos cosas a la vez. La forma española tiene su propia
    // batería, generada y en las dos direcciones, en `Terceros.UnitTests`.
    private static string Numero(int orden) =>
        "PRUEBA"
        + Guid.CreateVersion7().ToString("N")[^8..].ToUpperInvariant()
        + orden.ToString(CultureInfo.InvariantCulture);

    private static CrearTerceroDto Alta(string numero) => new()
    {
        Identificacion = new IdentificacionDeAltaDto { Pais = "FR", Numero = numero },
        RazonSocial = "Tercero de prueba",
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

    private static async Task<TerceroDto> CrearAsync(HttpClient cliente, string numero)
    {
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Terceros, Alta(numero));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<TerceroDto>())!;
    }

    private static async Task BloquearAsync(HttpClient cliente, Guid id)
    {
        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Terceros}/{id}");

        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));

        // Y desaparece del camino ordinario, que es la mitad de R16 sin la cual «bloqueado» sería
        // una columna sin efecto y todo lo que viene después no probaría nada.
        using HttpResponseMessage lectura = await cliente.GetAsync($"{Terceros}/{id}");
        lectura.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
