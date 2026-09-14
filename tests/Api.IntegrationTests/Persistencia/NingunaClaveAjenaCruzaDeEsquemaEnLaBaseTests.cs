using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ninguna clave ajena de la base cruza de esquema: se le pregunta a PostgreSQL, una vez, por todas
/// las que lo hacen, y la respuesta tiene que ser el conjunto vacío.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta regla ya estaba activa y nadie la había escrito.</b> La mutación 8 del ítem 1.10 puso
/// una clave ajena de Catálogo a Terceros con <c>migrationBuilder.Sql</c>, y de las cuatro guardias
/// que había solo la vio <c>EsquemaDeIdentidadTests</c>, por ser la única consulta sin filtro de
/// esquema: la de Organización mira las que salen de Organización, la de Terceros las que salen de
/// Terceros, y la del carril funcional recorre el modelo de EF Core, que no sabe nada del SQL
/// escrito a mano. La frontera la sostenía un caso cuyo nombre habla de la membresía.
/// </para>
/// <para>
/// <b>Descubrimiento del universo, no lista escrita a mano.</b> La consulta no nombra ningún
/// esquema, así que un módulo nuevo entra solo, y lee el catálogo del motor, así que ve lo que
/// hay después de migrar venga de donde venga. Una lista de esquemas se quedaría atrás justo con
/// el módulo que alguien olvidó añadir, que es el que no tiene a nadie mirándolo.
/// </para>
/// <para>
/// <b>Por los OID de <c>pg_constraint</c> y no por <c>information_schema</c></b>: allí la clave
/// ajena y la restricción a la que apunta se emparejan por NOMBRE, y PostgreSQL deja repetir el
/// nombre de una restricción en tablas distintas del mismo esquema. Un emparejamiento por nombre
/// puede cruzar dos filas que no tienen nada que ver.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class NingunaClaveAjenaCruzaDeEsquemaEnLaBaseTests(PostgresConTodosLosModulos postgres)
{
    private const string LasQueCruzanDeEsquema =
        """
        SELECT restriccion.conname, origen.nspname, destino.nspname
        FROM pg_catalog.pg_constraint AS restriccion
        JOIN pg_catalog.pg_class AS tabla_de_origen ON tabla_de_origen.oid = restriccion.conrelid
        JOIN pg_catalog.pg_namespace AS origen ON origen.oid = tabla_de_origen.relnamespace
        JOIN pg_catalog.pg_class AS tabla_de_destino ON tabla_de_destino.oid = restriccion.confrelid
        JOIN pg_catalog.pg_namespace AS destino ON destino.oid = tabla_de_destino.relnamespace
        WHERE restriccion.contype = 'f' AND origen.oid <> destino.oid
        ORDER BY restriccion.conname
        """;

    [Fact]
    public async Task Ninguna_clave_ajena_de_la_base_cruza_de_esquema()
    {
        await using NpgsqlConnection conexion = await AbrirAsync();

        IReadOnlyList<string> cruzadas = Nombradas(await LeerAsync(conexion, null, LasQueCruzanDeEsquema));

        // Una clave ajena entre esquemas ata dos módulos a migrarse y desplegarse juntos, y lo
        // hace en silencio: compila, migra y funciona. Lo que cruza de módulo se guarda como
        // identificador y lo valida el puerto del dueño (ADR-0024).
        cruzadas.ShouldBeEmpty(
            "hay claves ajenas que cruzan de esquema (§5, regla 4): " + string.Join(", ", cruzadas));
    }

    [Fact]
    public async Task La_base_que_se_pregunta_tiene_claves_ajenas_en_mas_de_un_esquema()
    {
        await using NpgsqlConnection conexion = await AbrirAsync();

        // Sin claves ajenas, o con todas en un solo esquema, «ninguna cruza» sale verde por no
        // haber nada que mirar: contra una base sin migrar o con un solo módulo migrado.
        IReadOnlyList<string> esquemas = [.. (await LeerAsync(
            conexion,
            null,
            """
            SELECT DISTINCT origen.nspname
            FROM pg_catalog.pg_constraint AS restriccion
            JOIN pg_catalog.pg_class AS tabla_de_origen ON tabla_de_origen.oid = restriccion.conrelid
            JOIN pg_catalog.pg_namespace AS origen ON origen.oid = tabla_de_origen.relnamespace
            WHERE restriccion.contype = 'f'
            """)).Select(fila => fila[0])];

        esquemas.Count.ShouldBeGreaterThan(
            1, "la base tiene claves ajenas en menos de dos esquemas: " + string.Join(", ", esquemas));
    }

    [Fact]
    public async Task La_pregunta_ve_una_clave_ajena_escrita_a_mano_entre_dos_esquemas()
    {
        await using NpgsqlConnection conexion = await AbrirAsync();
        await using NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync();

        // El mismo DDL que escribiría un `migrationBuilder.Sql`, que es justo lo que la regla del
        // carril funcional no puede ver. En dos esquemas de usar y tirar y dentro de una
        // transacción que se deshace: el DDL de PostgreSQL es transaccional, así que no queda
        // nada y no se bloquea ninguna tabla de los módulos.
        await using (NpgsqlCommand ddl = new(
            """
            CREATE SCHEMA canario_dueno;
            CREATE TABLE canario_dueno.cosas (id uuid PRIMARY KEY);
            CREATE SCHEMA canario_ajeno;
            CREATE TABLE canario_ajeno.referencias (
                cosa_id uuid NOT NULL,
                CONSTRAINT fk_canario_entre_esquemas FOREIGN KEY (cosa_id)
                    REFERENCES canario_dueno.cosas (id));
            """,
            conexion,
            transaccion))
        {
            await ddl.ExecuteNonQueryAsync();
        }

        IReadOnlyList<string> cruzadas =
            Nombradas(await LeerAsync(conexion, transaccion, LasQueCruzanDeEsquema));

        await transaccion.RollbackAsync();

        // Contiene, y no «es igual a»: este caso prueba que la pregunta ve lo escrito a mano. Si la
        // base no está limpia lo dice la regla de arriba, y el arnés no tiene por qué ponerse rojo
        // con ella: un rojo doble por la misma causa ya no dice cuál de los dos está roto.
        cruzadas.ShouldContain("fk_canario_entre_esquemas: canario_ajeno -> canario_dueno");
    }

    private async Task<NpgsqlConnection> AbrirAsync()
    {
        NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();
        return conexion;
    }

    private static IReadOnlyList<string> Nombradas(IReadOnlyList<string[]> filas) =>
        [.. filas.Select(fila => $"{fila[0]}: {fila[1]} -> {fila[2]}")];

    private static async Task<IReadOnlyList<string[]>> LeerAsync(
        NpgsqlConnection conexion, NpgsqlTransaction? transaccion, string consulta)
    {
        await using NpgsqlCommand orden = new(consulta, conexion, transaccion);
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        List<string[]> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add([.. Enumerable.Range(0, lector.FieldCount).Select(lector.GetString)]);
        }

        return filas;
    }
}
