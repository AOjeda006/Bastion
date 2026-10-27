using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Pruebas.Comun;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Listados;

/// <summary>
/// Un <c>?q=</c> a cada listado de la API: el que lo publica contesta, y el que no lo publica lo
/// rechaza con un <c>400</c> en vez de devolver la página entera (ADR-0072).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que el carril funcional no puede ver.</b> <c>SoloPublicaElTextoQuienLoMiraTests</c> fija
/// qué listados publican el texto, que es lo que dice el contrato. Que el listado que lo publica
/// sepa filtrar por él lo dice su repositorio, al declarar el filtro en sus criterios, y si no lo
/// declara el paginador lanza: un <c>500</c> que solo sale cuando la consulta llega a la base. El
/// barrido le manda a cada uno lo que un cliente le mandaría.
/// </para>
/// <para>
/// <b>Los listados salen del explorador</b>, como en el funcional (<c>ListadosDeLaApi</c>), así
/// que uno nuevo entra solo en el barrido. Un parámetro de ruta se rellena con un identificador
/// que no existe: el listado que cuelga de otro recurso contesta entonces un <c>404</c> sin llegar
/// a filtrar, y por eso un <c>404</c> también vale. De los diecisiete con texto, solo las líneas
/// de un recuento cuelgan de algo, y filtran en memoria, sin paginador.
/// </para>
/// <para>
/// <b>Semillas: la 930</b>, la primera del bloque del 3.2, que va de la 930 a la 949. Del 931 al
/// 934 son de <c>ElPuertoDelArticuloContraLaBaseTests</c>, del 935 al 939 de
/// <c>LasExportacionesDeOrganizacionTests</c>, y del 940 al 949 quedan libres.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed partial class ElTextoSoloLoRecibeQuienLoMiraTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string FiltroNoAdmitido = "/errors/filtro-no-admitido";

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Cada_listado_contesta_al_texto_segun_lo_publique()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(930));
        using HttpClient suyo = cliente;

        List<ApiDescription> listados = [.. ListadosDeLaApi.De(_api.Services).Where(ListadosDeLaApi.SeConsulta)];
        List<ApiDescription> conTexto = [.. listados.Where(ListadosDeLaApi.PublicaElTexto)];
        List<ApiDescription> sinTexto = [.. listados.Where(listado => !ListadosDeLaApi.PublicaElTexto(listado))];

        // EL ARNÉS: los dos lados con algo dentro. Un barrido que no ve listados sale verde.
        conTexto.ShouldNotBeEmpty("el barrido no ve ningún listado que publique ?q=");
        sinTexto.ShouldNotBeEmpty("el barrido no ve ningún listado sin ?q=");

        List<string> fallos = [];

        foreach (ApiDescription listado in conTexto)
        {
            using HttpResponseMessage respuesta = await cliente.GetAsync(Url(listado, "?q=zz"));

            if (respuesta.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NotFound))
            {
                fallos.Add($"{ListadosDeLaApi.Nombre(listado)} ?q=zz: {await Escenario.Detalle(respuesta)}");
            }
        }

        foreach (ApiDescription listado in sinTexto)
        {
            using HttpResponseMessage escrito = await cliente.GetAsync(Url(listado, "?q=zz"));

            if (escrito.StatusCode != HttpStatusCode.BadRequest
                || await TipoDelProblemaAsync(escrito) != FiltroNoAdmitido)
            {
                fallos.Add($"{ListadosDeLaApi.Nombre(listado)} ?q=zz: {await Escenario.Detalle(escrito)}");
            }

            // LA PAREJA: el mismo listado, con el texto en blanco, no se rechaza. Es lo que manda un
            // formulario con la caja vacía, y lo que distingue «no se admite» de «este listado da
            // 400 a todo».
            using HttpResponseMessage enBlanco = await cliente.GetAsync(Url(listado, "?q=%20"));

            if (enBlanco.StatusCode == HttpStatusCode.BadRequest)
            {
                fallos.Add($"{ListadosDeLaApi.Nombre(listado)} ?q=%20: {await Escenario.Detalle(enBlanco)}");
            }
        }

        fallos.ShouldBeEmpty(string.Join(Environment.NewLine, fallos));
    }

    /// <summary>La URL de un listado, con sus parámetros de ruta puestos a algo que no existe.</summary>
    private static string Url(ApiDescription listado, string consulta) =>
        "/" + ParametroDeRuta().Replace(listado.RelativePath!, _ => Guid.CreateVersion7().ToString()) + consulta;

    private static async Task<string?> TipoDelProblemaAsync(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return problema.RootElement.TryGetProperty("type", out JsonElement tipo) ? tipo.GetString() : null;
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex ParametroDeRuta();
}
