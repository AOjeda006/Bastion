using System.Data;
using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Bloqueos;

/// <summary>
/// R16 tiene dos mitades que se contradicen si se miran de una en una: el dato <b>desaparece</b>
/// de las consultas y <b>sigue estando</b> en la base. Aquí se comprueban las dos a la vez.
/// </summary>
/// <remarks>
/// <para>
/// El artículo 32 de la LOPDGDD llama a esto «bloqueo»: los datos quedan reservados, impidiendo su
/// tratamiento —<b>incluida su visualización</b>— salvo para ponerlos a disposición de jueces,
/// Fiscalía y Administraciones competentes durante el plazo de prescripción. Las dos mitades son
/// obligatorias: borrar la fila incumpliría la segunda, y dejarla visible, la primera.
/// </para>
/// <para>
/// <b>Se lee con SQL en crudo, y tiene que ser así.</b> Cualquier lectura por EF Core pasa por el
/// filtro que este test existe para comprobar: usarla sería preguntarle al acusado. Es la misma
/// razón por la que <c>LaTrazaNoGuardaSecretos</c> lee la tabla directamente.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaFilaBloqueadaSigueEnLaBaseTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Empresas = "/api/v1/organizacion/empresas";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Suprimir_por_la_API_deja_la_fila_entera_con_su_motivo_y_su_fecha()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000076F");
        using HttpClient suyo = cliente;

        using HttpResponseMessage borrado = await cliente.SuprimirAsync($"{Empresas}/{empresa.Id}");
        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(borrado));

        // 1. Por el camino ordinario, no existe.
        using HttpResponseMessage lectura = await cliente.GetAsync($"{Empresas}/{empresa.Id}");
        lectura.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // 2. En la tabla, entera. Y no solo la bandera: la razón social y el domicilio fiscal
        // —los datos personales de un empresario individual— siguen ahí, que es exactamente lo
        // que el art. 32 obliga a conservar mientras corra el plazo.
        IReadOnlyList<string> fila = await FilaAsync(
            "bloqueado::text, motivo_del_bloqueo, bloqueado_en::text, razon_social", empresa.Id);

        fila[0].ShouldBe("true");
        fila[1].ShouldBe("SupresionSolicitada");
        fila[2].ShouldNotBeNullOrWhiteSpace("del instante del bloqueo cuelga el plazo del art. 32");
        fila[3].ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Desbloquear_por_su_puerta_devuelve_la_MISMA_fila_y_no_una_copia()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000077P");
        using HttpClient suyo = cliente;

        IReadOnlyList<string> antes = await FilaAsync("id::text, creado_en::text", empresa.Id);

        using HttpResponseMessage borrado = await cliente.SuprimirAsync($"{Empresas}/{empresa.Id}");
        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(borrado));

        using HttpResponseMessage desbloqueo = await cliente.EnviarConVersionAsync(
            HttpMethod.Post, $"{Empresas}/{empresa.Id}/desbloqueo", etiqueta: null);

        desbloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(desbloqueo));

        // El identificador y la fecha de alta son los de antes: bloquear y desbloquear movieron
        // una columna, no dieron de baja una ficha y de alta otra. Si el «borrado» hubiera sido de
        // verdad y el desbloqueo una recreación, `creado_en` sería otro.
        IReadOnlyList<string> despues = await FilaAsync("id::text, creado_en::text", empresa.Id);
        despues.ShouldBe(antes);

        // Y las tres columnas del bloqueo vuelven a estar como al nacer: sin cuarto estado.
        IReadOnlyList<string> bloqueo = await FilaAsync(
            "bloqueado::text, COALESCE(motivo_del_bloqueo, ''), COALESCE(bloqueado_en::text, '')",
            empresa.Id);

        bloqueo.ShouldBe(["false", string.Empty, string.Empty]);
    }

    private async Task<IReadOnlyList<string>> FilaAsync(string columnas, Guid id)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            $"SELECT {columnas} FROM {OrganizacionDbContext.Esquema}.empresas WHERE id = '{id}'",
            conexion);

        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync(CommandBehavior.Default);

        (await lector.ReadAsync()).ShouldBeTrue($"la fila {id} ya no está en la tabla");

        List<string> valores = [];
        for (int columna = 0; columna < lector.FieldCount; columna++)
        {
            valores.Add(lector.IsDBNull(columna) ? string.Empty : lector.GetString(columna));
        }

        return valores;
    }
}
