using System.Security.Claims;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Multiempresa;

/// <summary>
/// Falla cerrado: sin empresa activa y sin ambito abierto, no hay consulta que valga.
/// </summary>
/// <remarks>
/// <para>
/// Es la mitad de R8 que no se puede ver por HTTP, porque desde 0.5 ninguna ruta es anonima y
/// todas traen su <i>claim</i>. Y es justo la mitad que hay que fijar: el dia que alguien publique
/// una ruta sin autenticacion, o un trabajo de fondo consulte sin declarar su motivo, lo que
/// decide si eso es un 500 ruidoso o un volcado silencioso de los datos de todas las empresas es
/// esta propiedad.
/// </para>
/// <para>
/// Se prueba sobre el <c>IInquilinoActual</c> que resuelve el contenedor de verdad, no sobre uno
/// construido a mano: lo que se afirma es que el sistema que se despliega falla cerrado.
/// </para>
/// </remarks>
public sealed class SinEmpresaNoSeConsultaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Sin_claim_y_sin_ambito_la_empresa_del_filtro_lanza()
    {
        using IServiceScope alcance = _api.Services.CreateScope();
        IInquilinoActual inquilino = alcance.ServiceProvider.GetRequiredService<IInquilinoActual>();

        // Lanzar es la parte buena. Devolver nulo aqui haria que el filtro se leyera como
        // "sin inquilino" y la consulta saliera con un 200 y las filas de todo el mundo.
        Should.Throw<FaltaLaEmpresaActivaException>(() => _ = inquilino.EmpresaDelFiltro);
    }

    [Fact]
    public void Con_un_ambito_abierto_a_proposito_devuelve_nulo_y_no_lanza()
    {
        using IServiceScope alcance = _api.Services.CreateScope();
        IInquilinoActual inquilino = alcance.ServiceProvider.GetRequiredService<IInquilinoActual>();

        using (inquilino.SinInquilino(MotivoSinInquilino.SemillaDeArranque))
        {
            inquilino.EmpresaDelFiltro.ShouldBeNull();
        }

        // Y al cerrarlo vuelve a estar cerrado: un ambito que no se cierra bien deja el proceso
        // entero sin filtro, y eso no se nota hasta que hay un segundo inquilino.
        Should.Throw<FaltaLaEmpresaActivaException>(() => _ = inquilino.EmpresaDelFiltro);
    }

    [Fact]
    public void Dos_ambitos_anidados_se_cierran_por_orden_y_el_de_fuera_sigue_abierto()
    {
        using IServiceScope alcance = _api.Services.CreateScope();
        IInquilinoActual inquilino = alcance.ServiceProvider.GetRequiredService<IInquilinoActual>();

        using (inquilino.SinInquilino(MotivoSinInquilino.SemillaDeArranque))
        {
            using (inquilino.SinInquilino(MotivoSinInquilino.UnicidadGlobal))
            {
                inquilino.EmpresaDelFiltro.ShouldBeNull();
            }

            // Anidar es lo normal: la semilla abre el suyo y por dentro llama a la comprobacion de
            // unicidad, que abre el suyo. Si el de dentro dejara el campo en nulo al cerrarse,
            // cerraria tambien el de fuera, en silencio y a mitad de la semilla.
            inquilino.EmpresaDelFiltro.ShouldBeNull();
        }

        Should.Throw<FaltaLaEmpresaActivaException>(() => _ = inquilino.EmpresaDelFiltro);
    }

    [Fact]
    public void Con_claim_devuelve_la_empresa_del_claim_y_no_otra()
    {
        var empresaId = Guid.CreateVersion7();

        using IServiceScope alcance = _api.Services.CreateScope();

        alcance.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimsDeBastion.Empresa, empresaId.ToString())],
                    "pruebas")),
            };

        IInquilinoActual inquilino = alcance.ServiceProvider.GetRequiredService<IInquilinoActual>();

        inquilino.HayEmpresaActiva.ShouldBeTrue();
        inquilino.EmpresaDelFiltro.ShouldBe(empresaId);
    }
}
