using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Acceso;

/// <summary>
/// Quién puede tocar las pertenencias de qué empresa, y el arranque en frío que obliga a que la
/// regla tenga una excepción.
/// </summary>
/// <remarks>
/// <para>
/// La regla es la del <i>claim</i>: se administra la empresa con la que se está operando. Escrita
/// solo así, la <b>segunda</b> empresa del sistema es inalcanzable para siempre —para entrar en
/// ella hay que pertenecer, y para pertenecer hay que estar dentro—, y eso no se ve en ningún test
/// de dominio: hace falta crear una empresa de verdad e intentar entrar.
/// </para>
/// <para>
/// La excepción es la mínima que resuelve el bloqueo: <b>mientras no haya nadie más dentro</b>. Lo
/// que este fichero prueba es que la excepción existe <i>y</i> que se cierra en cuanto deja de ser
/// necesaria.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class PertenenciasEntreEmpresasTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string RutaDeUsuarios = "/api/v1/identidad/usuarios";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Una_empresa_vacia_se_puebla_desde_fuera_y_deja_de_admitirlo_en_cuanto_tiene_a_alguien()
    {
        (HttpClient administrador, SesionDto sesion) = await _api.AbrirComoAdministradorAsync();
        using HttpClient _ = administrador;

        EmpresaDto nueva = await Escenario.CrearEmpresaAsync(administrador, "00000016Q");

        // La empresa acaba de nacer y no tiene a nadie. Quien la ha creado sigue operando en la
        // suya —el `claim` no ha cambiado— y aun así puede dar de alta al primero: sin esto, la
        // empresa recién creada no la puede usar nadie, nunca.
        sesion.EmpresaActivaId.ShouldNotBe(nueva.Id);

        Guid primero = await CrearUsuarioAsync(administrador);
        HttpResponseMessage concedida = await ConcederAsync(administrador, primero, nueva.Id);

        concedida.StatusCode.ShouldBe(
            HttpStatusCode.NoContent, await Escenario.Detalle(concedida));

        // Y ya hay alguien dentro. A partir de aquí, administrar esa empresa exige entrar en ella:
        // si no, tener el permiso en una empresa cualquiera valdría para todas y R8 se caería por
        // la puerta de atrás.
        Guid segundo = await CrearUsuarioAsync(administrador);
        HttpResponseMessage negada = await ConcederAsync(administrador, segundo, nueva.Id);

        negada.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await negada.Content.ReadAsStringAsync()).ShouldContain("/errors/empresa-ajena");
    }

    [Fact]
    public async Task Entrando_en_la_empresa_si_se_administra_la_que_ya_tiene_gente()
    {
        (HttpClient administrador, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000017V");
        using HttpClient _ = administrador;

        // El mismo caso del test anterior, pero con la empresa activa cambiada: es el camino que
        // usa un cliente de verdad, y el que demuestra que lo que cierra la puerta es el `claim` y
        // no una lista de empresas escrita en alguna parte.
        Guid primero = await CrearUsuarioAsync(administrador);
        (await ConcederAsync(administrador, primero, empresa.Id)).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        Guid segundo = await CrearUsuarioAsync(administrador);
        (await ConcederAsync(administrador, segundo, empresa.Id)).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
    }

    private static Task<HttpResponseMessage> ConcederAsync(
        HttpClient cliente,
        Guid usuarioId,
        Guid empresaId) =>
        cliente.PostAsJsonAsync(
            $"{RutaDeUsuarios}/{usuarioId}/pertenencias",
            new ConcederPertenenciaDto { EmpresaId = empresaId });

    private static async Task<Guid> CrearUsuarioAsync(HttpClient administrador)
    {
        string sufijo = Guid.CreateVersion7().ToString("N")[^12..];

        HttpResponseMessage alta = await administrador.PostAsJsonAsync(
            RutaDeUsuarios,
            new CrearUsuarioDto
            {
                Correo = $"pertenencias-{sufijo}@bastion.pruebas",
                Nombre = "Cuenta de prueba",
                Contrasena = Guid.CreateVersion7().ToString("N") + "aA1!",
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<UsuarioDto>())!.Id;
    }
}
