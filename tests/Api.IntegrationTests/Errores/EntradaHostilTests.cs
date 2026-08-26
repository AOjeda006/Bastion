using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Errores;

/// <summary>
/// Qué contesta la API cuando le mandan lo que no espera.
/// </summary>
/// <remarks>
/// <para>
/// Dos cosas a la vez, y las dos importan. La primera: que nada de esto sea un <c>500</c>. Un dato
/// mal escrito por un cliente es un desenlace previsto, y contestarlo con «error interno» convierte
/// la lista de errores del servidor en ruido que nadie mira.
/// </para>
/// <para>
/// La segunda: que la respuesta no traiga nada de dentro. <b>La lista de rastros prohibidos está
/// aquí, en el test, y no en mi cabeza</b>: escrita, se puede ampliar cuando aparezca uno nuevo;
/// recordada, se comprueba distinto cada vez que alguien mira.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class EntradaHostilTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Empresas = "/api/v1/organizacion/empresas";
    private const string Sesiones = "/api/v1/identidad/sesiones";

    // Lo que NO puede salir por la puerta, pase lo que pase dentro. Cada línea es algo que le
    // ahorra trabajo a quien esté sondeando: la versión del motor, la forma de la consulta, la
    // ruta del despliegue, el nombre de la máquina o el tipo que ha estallado.
    private static readonly string[] s_rastrosProhibidos =
    [
        "Npgsql",
        "PostgresException",
        "DbUpdateException",
        "Microsoft.EntityFrameworkCore",
        "System.InvalidOperationException",
        "StackTrace",
        "   at ",
        "SELECT ",
        "INSERT INTO",
        "UPDATE ",
        "DELETE FROM",
        "relation \"",
        "column \"",
        "constraint \"",
        "bastion_pruebas",
        "Host=",
        "Password=",
        "Username=",
        "5432",
        "C:\\",
        "/home/runner",
        "/usr/share",
        ".cs:line",
    ];

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Theory]
    [InlineData("'; DROP TABLE organizacion.empresas; --")]
    [InlineData("00000001R' OR '1'='1")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("../../../../etc/passwd")]
    [InlineData("\u0000\u0001\u0002")]
    [InlineData("𝔘𝔫𝔦𝔠𝔬𝔡𝔢 𝔯𝔞𝔯𝔬")]
    public async Task Un_NIF_hostil_es_un_400_normal_y_corriente(string nif)
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Empresas,
            new CrearEmpresaDto
            {
                Nif = nif,
                RazonSocial = "Prueba",
                DomicilioFiscal = Escenario.Domicilio(),
                DivisaBase = "EUR",
                RegimenDeIva = "General",
            });

        // La inyección no llega a ninguna parte porque la consulta va parametrizada, pero eso no
        // se ve desde fuera: lo que se ve es que contesta 400 y no 500, que es la señal de que la
        // ha parado la validación y no una excepción del motor.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await NadaDeDentro(respuesta);
    }

    [Theory]
    [InlineData("?page=-1")]
    [InlineData("?size=0")]
    [InlineData("?size=100000")]
    [InlineData("?page=abc&size=xyz")]
    [InlineData("?page=99999999999999999999")]
    public async Task Una_paginacion_imposible_es_400_y_no_una_excepcion(string consulta)
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        using HttpResponseMessage respuesta = await cliente.GetAsync(Empresas + consulta);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await NadaDeDentro(respuesta);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("\"solo una cadena\"")]
    [InlineData("{\"nif\": {\"anidado\": true}}")]
    [InlineData("{\"nif\": 12345678}")]
    public async Task Un_cuerpo_que_no_es_el_que_toca_es_400_y_no_dice_por_donde_ha_roto(string cuerpo)
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        using StringContent contenido = new(cuerpo, Encoding.UTF8, "application/json");

        using HttpResponseMessage respuesta = await cliente.PostAsync(Empresas, contenido);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // El mensaje que ASP.NET Core genera para un JSON roto lleva dentro el nombre del
        // parámetro y el tipo de C#. Es información del interior aunque no sea una traza.
        await NadaDeDentro(respuesta);
        (await respuesta.Content.ReadAsStringAsync()).ShouldNotContain("CrearEmpresaDto");
    }

    [Fact]
    public async Task Un_tipo_de_contenido_que_no_es_JSON_es_415_y_tampoco_cuenta_nada()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        using StringContent contenido = new("nif=00000001R", Encoding.UTF8, "text/plain");

        using HttpResponseMessage respuesta = await cliente.PostAsync(Empresas, contenido);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        await NadaDeDentro(respuesta);
    }

    [Fact]
    public async Task Una_cadena_larguisima_no_tumba_nada_y_sale_por_el_400_de_su_campo()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Empresas,
            new CrearEmpresaDto
            {
                Nif = "00000015S",
                RazonSocial = new string('X', 100_000),
                DomicilioFiscal = Escenario.Domicilio(),
                DivisaBase = "EUR",
                RegimenDeIva = "General",
            });

        // El tope lo pone la anotación del contrato, no la columna: llegar a la base y que reviente
        // el `varchar` sería un 500 por algo que se sabía antes de salir del borde.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await NadaDeDentro(respuesta);
    }

    [Fact]
    public async Task Un_identificador_que_no_es_un_GUID_ni_siquiera_llega_a_la_accion()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        using HttpResponseMessage respuesta = await cliente.GetAsync($"{Empresas}/no-soy-un-guid");

        // La restricción `:guid` de la ruta hace que no case ningún endpoint: 404. Sin ella, la
        // cadena llegaría al enlace de modelo y el 404 sería un 400 con el nombre del parámetro.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await NadaDeDentro(respuesta);
    }

    [Theory]
    [InlineData("' OR 1=1 --@bastion.pruebas")]
    [InlineData("\u0000@bastion.pruebas")]
    public async Task Un_correo_hostil_en_el_inicio_de_sesion_no_cuenta_nada_de_dentro(string correo)
    {
        using HttpClient cliente = _api.CrearCliente();

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Sesiones,
            new IniciarSesionDto
            {
                Correo = correo,
                Contrasena = "una contraseña cualquiera",
            });

        // Es la puerta abierta al mundo: aquí llega quien no se ha identificado, así que es donde
        // más barato le sale a alguien probar cosas y donde menos se le puede contar.
        ((int)respuesta.StatusCode).ShouldBeInRange(400, 499);
        await NadaDeDentro(respuesta);
    }

    private async Task NadaDeDentro(HttpResponseMessage respuesta)
    {
        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        foreach (string rastro in s_rastrosProhibidos)
        {
            cuerpo.ShouldNotContain(rastro, Case.Insensitive, $"la respuesta trae «{rastro}»: {cuerpo}");
        }

        // Y los dos secretos de este proceso, que no están en la lista fija porque cambian en cada
        // ejecución: la cadena de conexión y la clave de firma.
        cuerpo.ShouldNotContain(postgres.CadenaDeConexion);
        cuerpo.ShouldNotContain(ApiDeVerdad.ClaveDeFirma);
    }
}
