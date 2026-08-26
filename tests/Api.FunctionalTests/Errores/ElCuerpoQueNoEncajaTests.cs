using System.Net;
using System.Text;
using System.Text.Json;
using Bastion.Api.FunctionalTests.Salud;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Errores;

/// <summary>
/// Qué contesta la API a un cuerpo que no encaja con lo que la acción espera, y qué NO cuenta al
/// contestarlo.
/// </summary>
/// <remarks>
/// <para>
/// Va por <c>POST /api/v1/identidad/sesiones</c> porque es la única puerta anónima que recibe un
/// cuerpo: así el 400 se puede ejercitar <b>sin base de datos</b>, que es lo que permite que este
/// fichero viva entre los tests rápidos y no entre los de integración. El cuerpo ni siquiera llega
/// al caso de uso: lo para el enlace de modelo antes.
/// </para>
/// <para>
/// Lo que se comprueba son las dos mitades de la misma decisión. La primera: que el texto del
/// deserializador —que nombra el tipo de C# con su espacio de nombres— no salga por la puerta. La
/// segunda, y por eso está aquí y no en un test aparte: que sacarlo <b>no se lleve por delante</b>
/// los mensajes del contrato, que son los que dicen qué corregir.
/// </para>
/// </remarks>
public sealed class ElCuerpoQueNoEncajaTests : IDisposable
{
    private const string Sesiones = "/api/v1/identidad/sesiones";

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Theory]
    [InlineData("[]")]
    [InlineData("\"solo una cadena\"")]
    [InlineData("{\"correo\": {\"anidado\": true}}")]
    [InlineData("{\"correo\": 12345}")]
    [InlineData("{\"empresaId\": \"no soy un guid\"}")]
    public async Task Un_cuerpo_con_otra_forma_es_400_y_no_nombra_ni_un_tipo_de_C(string cuerpo)
    {
        using HttpClient cliente = _api.CreateClient();
        using StringContent contenido = new(cuerpo, Encoding.UTF8, "application/json");

        using HttpResponseMessage respuesta = await cliente.PostAsync(Sesiones, contenido);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        string texto = await respuesta.Content.ReadAsStringAsync();

        // El nombre del contrato, su espacio de nombres y los tipos del marco. Cualquiera de los
        // tres le dice a quien esté sondeando con qué está hablando por dentro.
        texto.ShouldNotContain("IniciarSesionDto");
        texto.ShouldNotContain("Bastion.");
        texto.ShouldNotContain("System.");
    }

    [Fact]
    public async Task El_400_del_enlace_de_modelo_sale_por_la_politica_central_con_su_traza()
    {
        using HttpClient cliente = _api.CreateClient();
        using StringContent contenido = new("[]", Encoding.UTF8, "application/json");

        using HttpResponseMessage respuesta = await cliente.PostAsync(Sesiones, contenido);

        JsonElement problema = await Problema(respuesta);

        // El MISMO `type` que devuelve un caso de uso cuando falla por campo: quien lea la
        // respuesta no tiene que saber si el fallo lo vio el enlace de modelo o el dominio.
        problema.GetProperty("type").GetString().ShouldBe("/errors/datos-no-validos");
        problema.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Los_mensajes_del_contrato_siguen_saliendo_enteros()
    {
        using HttpClient cliente = _api.CreateClient();
        using StringContent contenido = new("{}", Encoding.UTF8, "application/json");

        using HttpResponseMessage respuesta = await cliente.PostAsync(Sesiones, contenido);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        JsonElement errores = (await Problema(respuesta)).GetProperty("errors");

        // Se recogen todos los motivos, sin mirar bajo qué clave: cómo se llama el campo en la
        // respuesta lo decide el enlace de modelo, y lo que este test defiende es el MENSAJE.
        List<string> motivos = [.. errores
            .EnumerateObject()
            .SelectMany(campo => campo.Value.EnumerateArray())
            .Select(motivo => motivo.GetString() ?? string.Empty)];

        // Escritos en el DTO, en castellano y para quien está fuera. Sustituirlos por un «no es
        // válido» genérico obligaría a ir al OpenAPI para saber qué falta.
        motivos.ShouldContain("El correo es obligatorio.");
        motivos.ShouldContain("La contraseña es obligatoria.");
    }

    private static async Task<JsonElement> Problema(HttpResponseMessage respuesta)
    {
        using var documento = JsonDocument.Parse(
            await respuesta.Content.ReadAsStringAsync());

        return documento.RootElement.Clone();
    }
}
