using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Fechas;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Fechas;

/// <summary>
/// La zona horaria de la empresa y la del almacén, por HTTP y en la base: cuál tiene cada uno,
/// cuál rige, y que nada fuera de la lista llega a guardarse (ADR-0079).
/// </summary>
/// <remarks>
/// <para>
/// <b>La herencia es en vivo, y eso es lo que se prueba primero.</b> Un almacén sin zona propia
/// guarda un nulo, no una copia de la de su empresa: si la empresa cambia, la zona que rige en el
/// almacén cambia con ella sin tocarlo. Y la pantalla tiene que poder distinguir las dos cosas,
/// así que la ficha devuelve la propia y la efectiva.
/// </para>
/// <para>
/// <b>Semillas:</b> los NIF de este fichero son <c>NifInventado(35_000_001)</c> a
/// <c>NifInventado(35_000_014)</c>. El bloque de los 35 millones es suyo.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaZonaDeLaEmpresaYDelAlmacenTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const int BaseDeNifs = 35_000_000;
    private const string Empresas = "/api/v1/organizacion/empresas";
    private const string Almacenes = "/api/v1/organizacion/almacenes";

    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Una_empresa_nace_peninsular_y_cambia_de_zona_por_su_ficha()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(
            Escenario.NifInventado(BaseDeNifs + 1));
        using HttpClient suyo = cliente;

        empresa.ZonaHoraria.ShouldBe(ZonaHorariaDto.Madrid);

        using HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Empresas}/{empresa.Id}", Modificacion(empresa, ZonaHorariaDto.Canarias));

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        (await LeerAsync<EmpresaDto>(cliente, $"{Empresas}/{empresa.Id}")).ZonaHoraria
            .ShouldBe(ZonaHorariaDto.Canarias);
    }

    [Fact]
    public async Task Modificar_una_empresa_sin_decir_su_zona_es_un_400_del_campo()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(
            Escenario.NifInventado(BaseDeNifs + 2));
        using HttpClient suyo = cliente;

        using HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Empresas}/{empresa.Id}", Modificacion(empresa, zona: null));

        cambio.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // La clave de un [Required] la pone MVC con el nombre de la propiedad de C#, «ZonaHoraria»,
        // y no con el del cuerpo, como la ponen los casos de uso: es así para todos los
        // [Required] de la API, y el PLAN lo anota como propuesta. Aquí se afirma el campo, no la
        // grafía.
        (await ErroresAsync(cambio)).EnumerateObject().Select(campo => campo.Name)
            .ShouldContain(
                campo => string.Equals(campo, "zonaHoraria", StringComparison.OrdinalIgnoreCase),
                await Escenario.Detalle(cambio));
    }

    /// <summary>
    /// Una zona que el contrato no publica no llega al dominio: ni un identificador de otra zona,
    /// ni el mismo en minúsculas, ni un número, que el conversor por omisión leería como la
    /// primera de la lista o como una que no existe.
    /// </summary>
    /// <param name="zona">El valor del campo, tal cual va en el JSON.</param>
    /// <param name="semilla">La del NIF de la empresa del caso, una por fila.</param>
    [Theory]
    [InlineData("\"Europe/London\"", 11)]
    [InlineData("\"atlantic/canary\"", 12)]
    [InlineData("0", 13)]
    [InlineData("7", 14)]
    public async Task Una_zona_que_no_se_publica_es_un_400_y_no_un_500(string zona, int semilla)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(
            Escenario.NifInventado(BaseDeNifs + semilla));
        using HttpClient suyo = cliente;

        using StringContent cuerpo = new(
            $$"""{"codigo":"Z1","nombre":"Zona","tipo":"Virtual","zonaHoraria":{{zona}}}""",
            Encoding.UTF8,
            "application/json");
        using HttpResponseMessage alta = await cliente.PostAsync(Almacenes, cuerpo);

        alta.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(alta));
    }

    [Fact]
    public async Task Un_almacen_sin_zona_hereda_la_de_su_empresa_en_vivo_y_puede_volver_a_heredarla()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(
            Escenario.NifInventado(BaseDeNifs + 3));
        using HttpClient suyo = cliente;

        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Almacenes, new CrearAlmacenDto { Codigo = "HER", Nombre = "Heredero", Tipo = "Virtual" });
        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
        AlmacenDto almacen = (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;
        string ruta = $"{Almacenes}/{almacen.Id}";

        almacen.ZonaHoraria.ShouldBeNull();
        almacen.ZonaEfectiva.ShouldBe(ZonaHorariaDto.Madrid);

        // La empresa cambia, y el almacén, sin tocarlo, con ella.
        using (HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Empresas}/{empresa.Id}", Modificacion(empresa, ZonaHorariaDto.Canarias)))
        {
            cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        }

        AlmacenDto leido = await LeerAsync<AlmacenDto>(cliente, ruta);
        leido.ZonaHoraria.ShouldBeNull();
        leido.ZonaEfectiva.ShouldBe(ZonaHorariaDto.Canarias);
        (await ListadoAsync(cliente)).Single(fila => fila.Id == almacen.Id).ZonaEfectiva
            .ShouldBe(ZonaHorariaDto.Canarias);

        // Con la suya, rige la suya.
        using (HttpResponseMessage propia = await cliente.ModificarAsync(
            ruta,
            new ModificarAlmacenDto { Nombre = "Heredero", Tipo = "Virtual", ZonaHoraria = ZonaHorariaDto.Ceuta }))
        {
            propia.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(propia));
            AlmacenDto respuesta = (await propia.Content.ReadFromJsonAsync<AlmacenDto>())!;
            respuesta.ZonaHoraria.ShouldBe(ZonaHorariaDto.Ceuta);
            respuesta.ZonaEfectiva.ShouldBe(ZonaHorariaDto.Ceuta);
        }

        // Y sin ella otra vez, vuelve a heredar: la de la empresa, la de ahora.
        using (HttpResponseMessage vuelta = await cliente.ModificarAsync(
            ruta, new ModificarAlmacenDto { Nombre = "Heredero", Tipo = "Virtual" }))
        {
            vuelta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(vuelta));
        }

        leido = await LeerAsync<AlmacenDto>(cliente, ruta);
        leido.ZonaHoraria.ShouldBeNull();
        leido.ZonaEfectiva.ShouldBe(ZonaHorariaDto.Canarias);
    }

    /// <summary>
    /// La base es la última línea: lo que no está en la lista no se guarda aunque llegue por SQL.
    /// La lista del <c>CHECK</c> sale del dominio, y cada tabla se prueba con una zona que el
    /// sistema conoce y el negocio no admite, y con el nulo, que solo admite la del almacén.
    /// </summary>
    [Fact]
    public async Task La_base_no_guarda_una_zona_fuera_de_la_lista_ni_una_empresa_sin_zona()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(
            Escenario.NifInventado(BaseDeNifs + 4));
        using HttpClient suyo = cliente;

        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Almacenes, new CrearAlmacenDto { Codigo = "SQL", Nombre = "Por SQL", Tipo = "Virtual" });
        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
        AlmacenDto almacen = (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;

        (await Should.ThrowAsync<PostgresException>(() => CambiarZonaAsync("empresas", empresa.Id, "Europe/London")))
            .ConstraintName.ShouldBe("ck_empresas_zona_horaria");

        (await Should.ThrowAsync<PostgresException>(() => CambiarZonaAsync("empresas", empresa.Id, null)))
            .SqlState.ShouldBe(PostgresErrorCodes.NotNullViolation);

        (await Should.ThrowAsync<PostgresException>(() => CambiarZonaAsync("almacenes", almacen.Id, "Europe/London")))
            .ConstraintName.ShouldBe("ck_almacenes_zona_horaria");

        // Las tres admitidas, y el nulo en el almacén, sí entran: la restricción no rechaza de más.
        foreach (string admitida in new[] { "Europe/Madrid", "Atlantic/Canary", "Africa/Ceuta" })
        {
            await CambiarZonaAsync("empresas", empresa.Id, admitida);
            await CambiarZonaAsync("almacenes", almacen.Id, admitida);
        }

        await CambiarZonaAsync("almacenes", almacen.Id, null);
    }

    private static ModificarEmpresaDto Modificacion(EmpresaDto empresa, ZonaHorariaDto? zona) => new()
    {
        RazonSocial = empresa.RazonSocial,
        DomicilioFiscal = Escenario.Domicilio(),
        DivisaBase = empresa.DivisaBase,
        RegimenDeIva = empresa.RegimenDeIva,
        ZonaHoraria = zona,
    };

    private static async Task<T> LeerAsync<T>(HttpClient cliente, string ruta)
    {
        using HttpResponseMessage lectura = await cliente.GetAsync(ruta);
        lectura.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(lectura));

        return (await lectura.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<IReadOnlyList<AlmacenDto>> ListadoAsync(HttpClient cliente)
    {
        using var pagina = JsonDocument.Parse(
            await cliente.GetStringAsync($"{Almacenes}?size=100"));

        return pagina.RootElement.GetProperty("elementos").Deserialize<List<AlmacenDto>>(s_web)!;
    }

    private static async Task<JsonElement> ErroresAsync(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return problema.RootElement.GetProperty("errors").Clone();
    }

    // La tabla va por una lista cerrada de dos nombres y nunca por la entrada de nadie: un
    // identificador no se parametriza, y aquí lo escribe el propio caso.
    private async Task CambiarZonaAsync(string tabla, Guid id, string? zona)
    {
        string sentencia = tabla switch
        {
            "empresas" => "UPDATE organizacion.empresas SET zona_horaria = @zona WHERE id = @id",
            "almacenes" => "UPDATE organizacion.almacenes SET zona_horaria = @zona WHERE id = @id",
            _ => throw new ArgumentOutOfRangeException(nameof(tabla), tabla, "Solo empresas o almacenes."),
        };

        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();
        await using NpgsqlCommand cambio = new(sentencia, conexion);
        cambio.Parameters.AddWithValue("zona", (object?)zona ?? DBNull.Value);
        cambio.Parameters.AddWithValue("id", id);

        (await cambio.ExecuteNonQueryAsync()).ShouldBe(1);
    }
}
