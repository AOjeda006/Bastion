using System.Text.Json;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.Pruebas.Comun;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Fechas;

/// <summary>
/// El documento de la API publica las zonas horarias que admite el dominio, y ninguna más: ni una
/// zona que el dominio no lea, ni el nulo como si fuera una de ellas.
/// </summary>
/// <remarks>
/// <para>
/// Lee <c>docs/api/openapi.json</c>, el versionado: la CI lo regenera y falla si difiere, así que
/// lo que se afirma aquí es lo que se genera. Y es lo que lee el cliente del frontal, que es quien
/// sufriría una lista equivocada.
/// </para>
/// <para>
/// <b>Por qué el nulo.</b> El generador añade el nulo a la lista del enumerado cuando alguna
/// propiedad anulable lo usa, y la lista es compartida: la zona de la empresa, que no puede faltar,
/// salía anulable en el cliente generado. Lo quita un transformador (<c>ContratoDeLaApi</c>), y
/// estos casos son su afirmación roja: sin él, el primero falla.
/// </para>
/// </remarks>
public sealed class ElContratoPublicaLasZonasAdmitidasTests
{
    private const string RutaDelContrato = "docs/api/openapi.json";

    [Fact]
    public void El_enumerado_del_contrato_son_las_zonas_admitidas_y_nada_mas()
    {
        using JsonDocument documento = Documento();

        string?[] publicadas =
        [
            .. Esquema(documento, "ZonaHorariaDto").GetProperty("enum").EnumerateArray()
                .Select(valor => valor.ValueKind == JsonValueKind.Null ? null : valor.GetString()),
        ];

        publicadas.ShouldNotBeEmpty();
        publicadas.ShouldBe([.. ZonaHoraria.Admitidas.Select(zona => zona.Iana)]);
    }

    [Fact]
    public void La_zona_de_la_empresa_no_es_anulable_y_la_propia_del_almacen_si()
    {
        using JsonDocument documento = Documento();

        JsonElement deLaEmpresa = Propiedad(documento, "EmpresaDto", "zonaHoraria");
        deLaEmpresa.TryGetProperty("oneOf", out _).ShouldBeFalse("la empresa siempre tiene zona");
        deLaEmpresa.GetProperty("$ref").GetString().ShouldBe("#/components/schemas/ZonaHorariaDto");

        JsonElement efectiva = Propiedad(documento, "AlmacenDto", "zonaEfectiva");
        efectiva.TryGetProperty("oneOf", out _).ShouldBeFalse("en un almacén siempre rige alguna");

        JsonElement propia = Propiedad(documento, "AlmacenDto", "zonaHoraria");
        propia.GetProperty("oneOf").EnumerateArray()
            .Select(rama => rama.TryGetProperty("type", out JsonElement tipo) ? tipo.GetString() : "referencia")
            .ShouldBe(["null", "referencia"], ignoreOrder: true);
    }

    private static JsonElement Esquema(JsonDocument documento, string nombre) =>
        documento.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(nombre);

    private static JsonElement Propiedad(JsonDocument documento, string esquema, string propiedad) =>
        Esquema(documento, esquema).GetProperty("properties").GetProperty(propiedad);

    private static JsonDocument Documento()
    {
        string ruta = Path.Combine(
            RaizDelRepositorio.Ruta(), RutaDelContrato.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(ruta).ShouldBeTrue($"no existe {RutaDelContrato}. Se genera con: bash scripts/generar-openapi.sh");

        return JsonDocument.Parse(File.ReadAllText(ruta));
    }
}
