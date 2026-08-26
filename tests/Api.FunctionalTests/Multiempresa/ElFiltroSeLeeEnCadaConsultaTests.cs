using System.Security.Claims;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Multiempresa;

/// <summary>
/// La trampa del filtro congelado: que la empresa se lea en <b>cada</b> consulta y no una sola vez.
/// </summary>
/// <remarks>
/// <para>
/// EF Core cachea el modelo por tipo de contexto y opciones. Si el filtro se hubiera construido
/// con un <b>valor</b> —el identificador copiado en el constructor, o una expresión armada por
/// reflexión con la instancia dentro—, el modelo se quedaría con el inquilino del PRIMER contexto
/// que lo construyera y se lo serviría a todos los siguientes. Nadie vería un error: la segunda
/// empresa recibiría las filas de la primera con un <c>200</c>.
/// </para>
/// <para>
/// Por eso el filtro lee <c>EmpresaDelFiltro</c>, que es una propiedad de instancia del contexto,
/// y por eso este test hace <b>dos consultas seguidas en el mismo proceso</b> con dos empresas
/// distintas: la segunda es la que delata el congelado. Con una sola, un filtro correcto y uno
/// congelado son indistinguibles.
/// </para>
/// <para>
/// Se mira el SQL que EF Core va a mandar (<c>ToQueryString</c>), no las filas: aquí no hay base
/// de datos, y el parámetro del filtro ya está dentro de esa cadena.
/// </para>
/// </remarks>
public sealed class ElFiltroSeLeeEnCadaConsultaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Dos_consultas_seguidas_con_dos_empresas_llevan_dos_filtros_distintos()
    {
        var primera = Guid.CreateVersion7();
        var segunda = Guid.CreateVersion7();

        string sqlDeLaPrimera = ConsultaDeAlmacenes(primera);
        string sqlDeLaSegunda = ConsultaDeAlmacenes(segunda);

        // Cuando corre la segunda, el modelo YA está construido y cacheado por la primera: es
        // exactamente el instante en el que un filtro congelado deja de filtrar bien.
        sqlDeLaPrimera.ShouldContain(primera.ToString());
        sqlDeLaSegunda.ShouldContain(segunda.ToString());
        sqlDeLaSegunda.ShouldNotContain(primera.ToString());
    }

    [Fact]
    public void Y_el_mismo_orden_al_reves_da_el_mismo_resultado()
    {
        var primera = Guid.CreateVersion7();
        var segunda = Guid.CreateVersion7();

        // El caso de arriba con las empresas cambiadas de sitio. No es el mismo test repetido: un
        // congelado se manifiesta en la SEGUNDA consulta, así que las dos empresas tienen que
        // pasar por ese sitio para que ninguna de las dos lo ocupe siempre.
        string sqlDeLaSegunda = ConsultaDeAlmacenes(segunda);
        string sqlDeLaPrimera = ConsultaDeAlmacenes(primera);

        sqlDeLaSegunda.ShouldContain(segunda.ToString());
        sqlDeLaPrimera.ShouldContain(primera.ToString());
        sqlDeLaPrimera.ShouldNotContain(segunda.ToString());
    }

    [Fact]
    public void Y_el_mismo_contexto_reutilizado_tampoco_se_queda_con_la_primera()
    {
        var primera = Guid.CreateVersion7();
        var segunda = Guid.CreateVersion7();

        using IServiceScope alcance = _api.Services.CreateScope();
        OrganizacionDbContext contexto = alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>();

        Entrar(alcance, primera);
        string sqlDeLaPrimera = contexto.Almacenes.ToQueryString();

        Entrar(alcance, segunda);
        string sqlDeLaSegunda = contexto.Almacenes.ToQueryString();

        // Este es el caso que los dos de arriba NO cubren, y hace falta decirlo: hoy los contextos
        // se registran con `AddDbContext` —uno nuevo por petición—, así que un identificador
        // copiado en el constructor se comporta igual que leerlo en cada consulta y ningún test lo
        // distinguiría. Aquí se reutiliza la MISMA instancia con dos empresas, que es lo que haría
        // `AddDbContextPool` el día que alguien lo active buscando rendimiento. Si el contexto se
        // hubiera quedado con la primera, la segunda petición recibiría las filas de la primera.
        sqlDeLaPrimera.ShouldContain(primera.ToString());
        sqlDeLaSegunda.ShouldContain(segunda.ToString());
        sqlDeLaSegunda.ShouldNotContain(primera.ToString());
    }

    private static void Entrar(IServiceScope alcance, Guid empresaId) =>
        alcance.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimsDeBastion.Empresa, empresaId.ToString())],
                    "pruebas")),
            };

    // Una petición entera de mentira, pero por el camino de verdad: el `claim` que escribe el
    // emisor, el `IHttpContextAccessor` que lo lee y el contexto que el contenedor resuelve. Nada
    // de doblar `IInquilinoActual`: lo que se prueba es la cadena, y un doble la corta justo en el
    // sitio donde podría estar la avería.
    private string ConsultaDeAlmacenes(Guid empresaId)
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        Entrar(alcance, empresaId);

        return alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>()
            .Almacenes
            .ToQueryString();
    }
}
