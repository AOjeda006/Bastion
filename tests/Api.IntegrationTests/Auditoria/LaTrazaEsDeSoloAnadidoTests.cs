using Bastion.Api.IntegrationTests.Persistencia;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Auditoria;

/// <summary>
/// Que la tabla de traza sea de solo añadido, <b>comprobado contra el motor</b>.
/// </summary>
/// <remarks>
/// <para>
/// Leer la migración no es la prueba: demostraría que el disparador está escrito, no que rechace
/// nada. Estos tres casos mandan las tres órdenes que destruirían una traza —<c>UPDATE</c>,
/// <c>DELETE</c> y <c>TRUNCATE</c>— y exigen que PostgreSQL las rechace.
/// </para>
/// <para>
/// <b>Y por qué no basta un <c>REVOKE</c>.</b> Los permisos los da y los quita el dueño de la
/// tabla, que es el mismo usuario con el que se conecta la aplicación: un permiso que el
/// interesado puede devolverse a sí mismo es una frase, no una guarda. Lo que impide un
/// <c>UPDATE</c> es algo que lo rechace en el motor.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaTrazaEsDeSoloAnadidoTests(PostgresConTodosLosModulos postgres)
{
    // `restrict_violation`. Se compara el SQLSTATE y no el texto: el texto está en español y en
    // una versión, el código es el contrato.
    private const string RestriccionViolada = "23001";

    [Fact]
    public async Task Un_UPDATE_sobre_una_fila_de_traza_lo_rechaza_el_motor()
    {
        // Sobre una fila que EXISTE. Un disparador de fila no se dispara si el `WHERE` no encaja
        // con nada, así que un `UPDATE` a un identificador inventado saldría con cero filas y sin
        // error, y este test daría verde sin haber ejercido nada.
        Guid id = await UnaFilaCualquieraAsync();

        PostgresException error = await Should.ThrowAsync<PostgresException>(() => EjecutarAsync(
            $"UPDATE auditoria.registros SET entidad = 'reescrito' WHERE id = '{id}'"));

        error.SqlState.ShouldBe(RestriccionViolada);
        error.MessageText.ShouldContain("auditoria.registros");
        error.MessageText.ShouldContain("UPDATE");
    }

    [Fact]
    public async Task Un_DELETE_sobre_una_fila_de_traza_lo_rechaza_el_motor()
    {
        Guid id = await UnaFilaCualquieraAsync();

        PostgresException error = await Should.ThrowAsync<PostgresException>(() => EjecutarAsync(
            $"DELETE FROM auditoria.registros WHERE id = '{id}'"));

        error.SqlState.ShouldBe(RestriccionViolada);
        error.MessageText.ShouldContain("DELETE");
    }

    [Fact]
    public async Task Un_TRUNCATE_de_la_tabla_lo_rechaza_el_motor()
    {
        // El segundo disparador, y no es una repetición: los de fila NO ven un `TRUNCATE`. Es
        // justo la orden que usaría quien quiere vaciar la tabla de un golpe, y con un solo
        // disparador de fila se saldría con la suya sin tocar ninguna guarda.
        PostgresException error = await Should.ThrowAsync<PostgresException>(
            () => EjecutarAsync("TRUNCATE auditoria.registros"));

        error.SqlState.ShouldBe(RestriccionViolada);
        error.MessageText.ShouldContain("TRUNCATE");
    }

    [Fact]
    public async Task Un_INSERT_sin_empresa_y_sin_motivo_lo_rechaza_la_tabla()
    {
        // La otra restricción de la tabla, y va aquí porque se comprueba igual: por el efecto y
        // por una vía que no pasa por C#. La invariante está escrita en el constructor de
        // `RegistroDeAuditoria`, pero un constructor no protege de un `INSERT` a mano.
        PostgresException error = await Should.ThrowAsync<PostgresException>(() => EjecutarAsync(
            """
            INSERT INTO auditoria.registros
                (id, correlacion_id, ocurrido_en, empresa_id, sin_inquilino, usuario_id,
                 entidad, entidad_id, cambio, valores)
            VALUES
                (gen_random_uuid(), gen_random_uuid(), now(), NULL, NULL, NULL,
                 'Inventada', 'x', 'Alta', '{}'::jsonb)
            """));

        // `check_violation`.
        error.SqlState.ShouldBe("23514");
        error.ConstraintName.ShouldBe("ck_registros_empresa_o_motivo");
    }

    [Fact]
    public async Task Y_con_empresa_Y_motivo_a_la_vez_tambien()
    {
        PostgresException error = await Should.ThrowAsync<PostgresException>(() => EjecutarAsync(
            """
            INSERT INTO auditoria.registros
                (id, correlacion_id, ocurrido_en, empresa_id, sin_inquilino, usuario_id,
                 entidad, entidad_id, cambio, valores)
            VALUES
                (gen_random_uuid(), gen_random_uuid(), now(), gen_random_uuid(), 'SemillaDeArranque', NULL,
                 'Inventada', 'x', 'Alta', '{}'::jsonb)
            """));

        error.SqlState.ShouldBe("23514");
        error.ConstraintName.ShouldBe("ck_registros_empresa_o_motivo");
    }

    // La semilla de arranque ya deja traza al crear la primera empresa y la primera cuenta, así
    // que siempre hay filas. Si algún día no las hubiera, esto revienta con un mensaje que lo
    // dice, en vez de dar verde por no tener nada contra lo que probar.
    private async Task<Guid> UnaFilaCualquieraAsync()
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new("SELECT id FROM auditoria.registros LIMIT 1", conexion);
        object? id = await orden.ExecuteScalarAsync();

        id.ShouldNotBeNull("la tabla de traza está vacía: no hay contra qué probar el solo añadido");

        return (Guid)id;
    }

    private async Task EjecutarAsync(string sentencia)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(sentencia, conexion);
        await orden.ExecuteNonQueryAsync();
    }
}
