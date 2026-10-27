using Bastion.Api.FunctionalTests.Salud;
using Bastion.Pruebas.Comun;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Listados;

/// <summary>
/// Solo publica <c>?q=</c> el listado que filtra por texto, y los que no lo publican son una lista
/// cerrada, escrita aquí.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hasta el ítem 3.2 lo publicaban los 23 listados</b>, porque venía en el mismo tipo que la
/// página y el orden, y seis no lo miraban: el <c>?q=</c> llegaba, el paginador lo dejaba pasar y
/// la respuesta era la página entera, con un <c>200</c> (ADR-0069). Ahora el texto es una pieza
/// aparte que solo enlaza quien lo mira (ADR-0072), y esto dice cuáles no la enlazan.
/// </para>
/// <para>
/// <b>La lista es cerrada a propósito.</b> Un listado nuevo sin texto la pone roja y obliga a
/// decidir aquí si de verdad no tiene nada que buscar. Uno nuevo con texto no la toca, y lo que
/// le pide entonces es el carril de integración: <c>ElTextoSoloLoRecibeQuienLoMiraTests</c> le
/// manda un <c>?q=</c> a cada listado que lo publica y exige que conteste, que es lo que no hace
/// uno cuyo repositorio no declara filtro, porque el paginador lanza.
/// </para>
/// </remarks>
public sealed class SoloPublicaElTextoQuienLoMiraTests : IDisposable
{
    /// <summary>Los listados que no publican <c>?q=</c>, con el verbo y la ruta.</summary>
    /// <remarks>
    /// Ninguno tiene un campo de texto que escriba una persona y que tenga sentido buscar: las
    /// líneas de una tarifa son un artículo y un precio; el saldo, cantidades; la transferencia,
    /// dos almacenes y una fecha; la conversión, dos unidades y un factor; el ejercicio, un año; y
    /// el tipo de cambio, dos divisas y un valor.
    /// </remarks>
    private static readonly SortedSet<string> s_sinTexto = new(StringComparer.Ordinal)
    {
        "GET /api/v1/catalogo/tarifas/{tarifaId}/lineas",
        "GET /api/v1/inventario/saldos",
        "GET /api/v1/inventario/transferencias",
        "GET /api/v1/organizacion/conversiones-de-unidades",
        "GET /api/v1/organizacion/ejercicios",
        "GET /api/v1/organizacion/tipos-de-cambio",
    };

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Los_listados_sin_filtro_de_texto_son_los_seis_declarados()
    {
        SortedSet<string> sinTexto = new(
            ListadosDeLaApi.De(_api.Services)
                .Where(ListadosDeLaApi.SeConsulta)
                .Where(listado => !ListadosDeLaApi.PublicaElTexto(listado))
                .Select(ListadosDeLaApi.Nombre),
            StringComparer.Ordinal);

        sinTexto.ShouldBe(
            s_sinTexto,
            customMessage: "los listados que no publican ?q= no son los declarados: " +
            string.Join(", ", sinTexto));
    }

    /// <summary>
    /// El arnés de la regla de arriba: que vea listados de los dos lados, que lo que aparta sean
    /// búsquedas por el cuerpo, y que el texto sea un parámetro de consulta con forma de cadena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La igualdad de arriba ya se pone roja si el barrido deja de ver listados o deja de ver el
    /// <c>q</c> en todos. Lo que no ve es un <c>q</c> que siga publicándose con otro tipo, o el día
    /// en que el lado con texto se quede vacío porque todos los listados se pasaron al otro y
    /// alguien copió la lista nueva sin mirarla.
    /// </para>
    /// <para>
    /// <b>Y lo que aparta el <c>GET</c></b>: las búsquedas por <c>POST .../buscar</c>, que también
    /// devuelven una colección y no leen nada de la URL. Si no hubiera ninguna, el filtro no
    /// apartaría nada y sobraría; y si una publicara el texto, apartarla lo escondería.
    /// </para>
    /// </remarks>
    [Fact]
    public void El_barrido_ve_listados_con_texto_y_sin_texto()
    {
        List<ApiDescription> todos = [.. ListadosDeLaApi.De(_api.Services)];
        List<ApiDescription> listados = [.. todos.Where(ListadosDeLaApi.SeConsulta)];
        List<ApiDescription> porElCuerpo = [.. todos.Where(listado => !ListadosDeLaApi.SeConsulta(listado))];

        listados.Where(ListadosDeLaApi.PublicaElTexto).ShouldNotBeEmpty(
            "ningún listado publica ?q=, así que el barrido no está viendo el parámetro");

        listados.Where(listado => !ListadosDeLaApi.PublicaElTexto(listado)).ShouldNotBeEmpty(
            "todos los listados publican ?q=, y hay seis que no lo miran");

        porElCuerpo.ShouldNotBeEmpty("no hay búsquedas por el cuerpo, así que quedarse con los GET no aparta nada");
        porElCuerpo.Where(ListadosDeLaApi.PublicaElTexto).Select(ListadosDeLaApi.Nombre).ShouldBeEmpty(
            "una búsqueda por el cuerpo publica ?q=, y la regla de arriba no la ve");

        listados
            .Where(ListadosDeLaApi.PublicaElTexto)
            .SelectMany(listado => listado.ParameterDescriptions)
            .Where(parametro => parametro.Name == ListadosDeLaApi.Texto)
            .Select(parametro => parametro.Type)
            .Distinct()
            .ShouldBe([typeof(string)], "el texto viaja como una cadena");
    }
}
