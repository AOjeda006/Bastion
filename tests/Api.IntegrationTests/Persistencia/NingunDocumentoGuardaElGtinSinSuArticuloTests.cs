using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ninguna tabla de la base guarda un GTIN sin el artículo al lado: se le pregunta a PostgreSQL por
/// toda columna que por su nombre lleve un código de barras, en todos los esquemas (ADR-0051 §8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué importa.</b> Quitar un GTIN borra su fila, y la empresa puede darlo de alta enseguida en
/// otro artículo: la regla de los 48 meses está derogada. Un documento que guardara el GTIN en lugar
/// del artículo se quedaría huérfano al quitarlo, o peor, pasaría a señalar al artículo nuevo. Así
/// que el documento que lo guarde guardará también el artículo.
/// </para>
/// <para>
/// <b>Hoy no hay ninguno, y por eso es un caso y no una frase.</b> Los documentos de la fase 3, el
/// albarán y la factura, llegarán sin avisar a este caso. La consulta no nombra ningún esquema: un
/// módulo nuevo entra solo, y lo que llegue por <c>migrationBuilder.Sql</c> también, porque se lee el
/// catálogo del motor después de migrar y no el modelo de EF Core.
/// </para>
/// <para>
/// <b>Una columna lleva un GTIN si uno de los trozos de su nombre lo dice</b>: <c>gtin</c>,
/// <c>ean</c>, <c>upc</c>, <c>barcode</c>, <c>barra</c> o <c>barras</c>, con cifras detrás o sin ellas, entre
/// guiones bajos o en los extremos. Por trozos y no por subcadena, para que <c>oceano</c> no sea un
/// <c>ean</c>. Cada nombre es un selector tecleado, así que cada uno tiene su columna en el canario.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class NingunDocumentoGuardaElGtinSinSuArticuloTests(PostgresConTodosLosModulos postgres)
{
    // La propia del código de barras: la que el barrido tiene que encontrar para mirar algo.
    private const string LaDelCodigoDeBarras = "catalogo.codigos_barras.gtin";

    // Las tablas de todos los esquemas menos los del motor, y sin las particiones, que repiten las
    // columnas de su tabla madre.
    private const string LasColumnasConUnGtin =
        """
        SELECT esquema.nspname || '.' || tabla.relname || '.' || columna.attname,
               CASE WHEN EXISTS (
                   SELECT 1 FROM pg_catalog.pg_attribute AS vecina
                   WHERE vecina.attrelid = tabla.oid
                     AND vecina.attname = 'articulo_id'
                     AND vecina.attnum > 0
                     AND NOT vecina.attisdropped)
               THEN 'con el artículo' ELSE 'sin el artículo' END
        FROM pg_catalog.pg_attribute AS columna
        JOIN pg_catalog.pg_class AS tabla ON tabla.oid = columna.attrelid
        JOIN pg_catalog.pg_namespace AS esquema ON esquema.oid = tabla.relnamespace
        WHERE tabla.relkind IN ('r', 'p', 'v', 'm', 'f')
          AND NOT tabla.relispartition
          AND columna.attnum > 0
          AND NOT columna.attisdropped
          AND esquema.nspname NOT IN ('pg_catalog', 'information_schema')
          AND esquema.nspname NOT LIKE 'pg\_%'
          AND columna.attname ~ '(^|_)(gtin|ean|upc|barcode|barras?)[0-9]*(_|$)'
        ORDER BY 1
        """;

    [Fact]
    public async Task Ninguna_columna_guarda_un_gtin_sin_el_articulo_al_lado()
    {
        await using NpgsqlConnection conexion = await AbrirAsync();

        IReadOnlyList<string> huerfanas = SinElArticulo(await LeerAsync(conexion, null));

        huerfanas.ShouldBeEmpty(
            "hay columnas que guardan un GTIN sin el artículo (ADR-0051 §8): " + string.Join(", ", huerfanas));
    }

    [Fact]
    public async Task El_barrido_encuentra_la_columna_del_propio_codigo_de_barras_con_su_articulo()
    {
        await using NpgsqlConnection conexion = await AbrirAsync();

        // La pareja de la regla de arriba: si el barrido no encuentra ni la columna que sí guarda
        // GTIN, «ninguna sin el artículo» sale verde por no mirar nada. Contra una base sin migrar,
        // con el patrón roto o con la tabla renombrada.
        IReadOnlyList<(string Columna, string Articulo)> encontradas = await LeerAsync(conexion, null);

        encontradas.ShouldContain(
            (LaDelCodigoDeBarras, "con el artículo"),
            "el barrido no encuentra la columna del GTIN: " + string.Join(", ", encontradas));
    }

    [Fact]
    public async Task El_barrido_ve_un_documento_que_todavia_no_existe()
    {
        await using NpgsqlConnection conexion = await AbrirAsync();
        await using NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync();

        // El DDL de un documento de la fase 3 que nadie ha escrito todavía, en un esquema de usar y
        // tirar y dentro de una transacción que se deshace: el DDL de PostgreSQL es transaccional,
        // así que no queda nada. Una línea sin el artículo, con cada nombre que el patrón dice
        // reconocer, y otra con el artículo al lado. Y dos columnas que contienen los nombres sin
        // serlo, que no tienen que salir.
        await using (NpgsqlCommand ddl = new(
            """
            CREATE SCHEMA canario_ventas;
            CREATE TABLE canario_ventas.lineas_sin_articulo (
                id uuid PRIMARY KEY,
                gtin character(14),
                ean13 character(13),
                upc_a character(12),
                barcode text,
                codigo_de_barras text,
                codigo_barra text,
                oceano text,
                barrio text);
            CREATE TABLE canario_ventas.lineas_con_articulo (
                id uuid PRIMARY KEY,
                articulo_id uuid NOT NULL,
                gtin character(14));
            """,
            conexion,
            transaccion))
        {
            await ddl.ExecuteNonQueryAsync();
        }

        IReadOnlyList<(string Columna, string Articulo)> encontradas = await LeerAsync(conexion, transaccion);

        await transaccion.RollbackAsync();

        // Contiene, y no «es igual a»: este caso prueba que el barrido ve lo que no existe. Si la
        // base no está limpia lo dice la regla, y un rojo doble por la misma causa ya no dice cuál
        // de los dos está roto.
        string[] delCanario = [.. encontradas
            .Where(encontrada => encontrada.Columna.StartsWith("canario_ventas.", StringComparison.Ordinal))
            .Select(encontrada => $"{encontrada.Columna}: {encontrada.Articulo}")];

        delCanario.ShouldBe(
            [
                "canario_ventas.lineas_con_articulo.gtin: con el artículo",
                "canario_ventas.lineas_sin_articulo.barcode: sin el artículo",
                "canario_ventas.lineas_sin_articulo.codigo_barra: sin el artículo",
                "canario_ventas.lineas_sin_articulo.codigo_de_barras: sin el artículo",
                "canario_ventas.lineas_sin_articulo.ean13: sin el artículo",
                "canario_ventas.lineas_sin_articulo.gtin: sin el artículo",
                "canario_ventas.lineas_sin_articulo.upc_a: sin el artículo",
            ],
            ignoreOrder: true);
        SinElArticulo(encontradas).ShouldContain("canario_ventas.lineas_sin_articulo.gtin");
    }

    private async Task<NpgsqlConnection> AbrirAsync()
    {
        NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();
        return conexion;
    }

    private static IReadOnlyList<string> SinElArticulo(IReadOnlyList<(string Columna, string Articulo)> encontradas) =>
        [.. encontradas.Where(encontrada => encontrada.Articulo == "sin el artículo").Select(encontrada => encontrada.Columna)];

    private static async Task<IReadOnlyList<(string Columna, string Articulo)>> LeerAsync(
        NpgsqlConnection conexion, NpgsqlTransaction? transaccion)
    {
        await using NpgsqlCommand orden = new(LasColumnasConUnGtin, conexion, transaccion);
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        List<(string, string)> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add((lector.GetString(0), lector.GetString(1)));
        }

        return filas;
    }
}
