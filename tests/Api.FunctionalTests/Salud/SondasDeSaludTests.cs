using System.Net;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Salud;

/// <summary>
/// Las dos sondas son distintas A PROPÓSITO. VIDA responde "el proceso responde" y no mira
/// ninguna dependencia; DISPONIBILIDAD responde "puedo atender tráfico" y sí mira la base de
/// datos. Si la de vida mirase la base, un corte de PostgreSQL haría que el orquestador
/// reiniciara la API en bucle — y reiniciar la API no arregla la base.
/// </summary>
public sealed class SondasDeSaludTests(ApiSinDependencias api) : IClassFixture<ApiSinDependencias>
{
    private static readonly Uri s_vida = new("/health/live", UriKind.Relative);
    private static readonly Uri s_disponibilidad = new("/health/ready", UriKind.Relative);

    [Fact]
    public async Task SondaDeVida_SinBaseDeDatos_RespondeCorrecto()
    {
        using HttpClient cliente = api.CreateClient();

        using HttpResponseMessage respuesta = await cliente.GetAsync(s_vida);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SondaDeDisponibilidad_SinBaseDeDatos_RespondeServicioNoDisponible()
    {
        using HttpClient cliente = api.CreateClient();

        using HttpResponseMessage respuesta = await cliente.GetAsync(s_disponibilidad);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task SondaDeDisponibilidad_SinBaseDeDatos_DiceCualDependenciaFalla()
    {
        using HttpClient cliente = api.CreateClient();

        using HttpResponseMessage respuesta = await cliente.GetAsync(s_disponibilidad);
        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        // Un "Unhealthy" a secas no sirve a las tres de la mañana: hay que saber QUÉ falla.
        cuerpo.ShouldContain("base-de-datos");
    }
}
