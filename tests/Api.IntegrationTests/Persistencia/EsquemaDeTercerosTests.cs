using Bastion.Terceros.Infrastructure.Persistencia;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// El esquema de Terceros, mirado <b>en la base</b>. Y con un motivo que los otros módulos no
/// tienen: aquí hay un índice que el modelo de EF Core no conoce.
/// </summary>
/// <remarks>
/// <para>
/// <b>El índice único de (empresa, país, número) está escrito a mano en la migración</b>, porque
/// EF Core 10 no sabe indexar miembros de un tipo complejo: ni por selector
/// —<c>fila =&gt; new { fila.EmpresaId, fila.Identificacion.Pais }</c>, que no es una expresión de
/// acceso a miembro válida— ni por nombres —<c>HasIndex("EmpresaId", "Identificacion.Pais")</c>,
/// que no encuentra la propiedad—. Se probaron las dos y las dos fallan.
/// </para>
/// <para>
/// <b>Eso deja un agujero de vigilancia, y este fichero es quien lo tapa.</b> Como el modelo no
/// declara el índice, <c>has-pending-model-changes</c> no puede echarlo en falta: alguien que
/// borrara esas líneas de la migración dejaría el modelo y la base de acuerdo, la comprobación de
/// migraciones en verde, y la unicidad del maestro de terceros sin nadie que la sostuviera. Aquí
/// se lee de <c>pg_index</c>, que es lo que hay de verdad después de migrar.
/// </para>
/// <para>
/// <b>Y se afirma además que su predicado parcial está vacío</b>, que no es un detalle: es la
/// decisión del ítem. Un <c>WHERE bloqueado = false</c> convertiría la unicidad en parcial, el
/// identificador de un tercero bloqueado quedaría libre, y desbloquearlo podría chocar contra la
/// ficha que ocupó su sitio — un empate con datos personales dentro que alguien tendría que
/// deshacer a mano. Lo comprueba por el efecto <c>ElConflictoQueNoRevelaTests</c>; aquí se
/// comprueba la causa.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class EsquemaDeTercerosTests(PostgresConTodosLosModulos postgres)
{
    private const string Tabla = "terceros";

    [Fact]
    public async Task La_tabla_del_modulo_esta_en_SU_esquema_y_en_snake_case()
    {
        IReadOnlyList<(string Primera, string Segunda)> encontradas = await ConsultarAsync(
            $"""
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = '{TercerosDbContext.Esquema}'
            ORDER BY table_name
            """);

        // La tabla y su historial, y nada más: el módulo entra con un solo agregado, así que una
        // tabla de sorpresa aquí sería una entidad que se ha colado sin pasar por el plan.
        encontradas.Select(fila => fila.Segunda).ShouldBe(
            [TercerosDbContext.TablaDelHistorial, Tabla],
            ignoreOrder: true);
    }

    /// <summary>
    /// El índice único de (empresa, país, número), leído de la base y con su predicado vacío.
    /// </summary>
    [Fact]
    public async Task La_unicidad_del_identificador_esta_EN_LA_BASE_y_abarca_tambien_lo_bloqueado()
    {
        IReadOnlyList<(string Nombre, string Definicion)> indices = await ConsultarAsync(
            $"""
            SELECT indice.relname, pg_get_indexdef(indice.oid)
            FROM pg_index AS declaracion
            JOIN pg_class AS indice ON indice.oid = declaracion.indexrelid
            JOIN pg_class AS tabla ON tabla.oid = declaracion.indrelid
            JOIN pg_namespace AS esquema ON esquema.oid = tabla.relnamespace
            WHERE esquema.nspname = '{TercerosDbContext.Esquema}'
              AND tabla.relname = '{Tabla}'
              AND declaracion.indisunique
              AND NOT declaracion.indisprimary
              AND declaracion.indpred IS NULL
            """);

        // UNO, y con las tres columnas en ese orden: la empresa delante, porque toda consulta del
        // módulo entra por ella (R8) y un índice que la llevara detrás no serviría para nada más.
        indices.Count.ShouldBe(
            1,
            "falta el índice único de (empresa, país, número) SIN predicado parcial. Está escrito " +
            "a mano en la migración porque EF Core no sabe indexar miembros de un tipo complejo, " +
            "así que `has-pending-model-changes` no lo echa de menos y este test es lo único que " +
            "lo nota. Lo que hay en la tabla: " + await IndicesDeLaTablaAsync());

        indices[0].Definicion.ShouldContain("(empresa_id, identificacion_pais, identificacion_numero)");
        indices[0].Definicion.ShouldNotContain(" WHERE ");
    }

    [Theory]
    [InlineData("identificacion_pais", "character varying")]
    [InlineData("identificacion_numero", "character varying")]
    [InlineData("identificacion_verificacion", "text")]
    public async Task El_identificador_fiscal_son_TRES_columnas_y_no_una_cadena_suelta(
        string columna,
        string tipo)
    {
        // Aplanado en la misma tabla y no en una aparte: es un tipo complejo (ADR-0016), o sea un
        // valor sin identidad propia. Con el estado de verificación al lado del número, no hay
        // forma de leer uno y olvidarse del otro — que es justo lo que este ítem existe para
        // impedir.
        (await TipoDeColumnaAsync(Tabla, columna)).ShouldBe(tipo);
    }

    [Theory]
    [InlineData("bloqueado", "boolean")]
    [InlineData("bloqueado_en", "timestamp with time zone")]
    [InlineData("motivo_del_bloqueo", "text")]
    [InlineData("creado_en", "timestamp with time zone")]
    [InlineData("modificado_en", "timestamp with time zone")]
    public async Task El_bloqueo_y_las_marcas_son_las_MISMAS_columnas_que_en_los_demas_modulos(
        string columna,
        string tipo)
    {
        // Los mismos tres campos del bloqueo y las mismas dos marcas de R14, con los mismos
        // nombres. No es simetría decorativa: `LaFilaBloqueadaSigueEnLaBase` y el listado del
        // art. 32 leen por nombre de columna, y un módulo que las llamara de otra manera se
        // quedaría fuera de esas comprobaciones sin que nada se pusiera rojo.
        (await TipoDeColumnaAsync(Tabla, columna)).ShouldBe(tipo);
    }

    [Fact]
    public async Task La_empresa_se_guarda_como_identificador_y_NO_como_clave_ajena()
    {
        (await TipoDeColumnaAsync(Tabla, "empresa_id")).ShouldBe("uuid");

        IReadOnlyList<(string Primera, string Segunda)> ajenas = await ConsultarAsync(
            $"""
            SELECT origen.constraint_name, origen.table_name
            FROM information_schema.table_constraints AS origen
            WHERE origen.table_schema = '{TercerosDbContext.Esquema}'
              AND origen.constraint_type = 'FOREIGN KEY'
            """);

        // Quien dice si esa empresa existe y está operativa es Organización por sus Contracts, y
        // lo comprueba `CrearTercero` antes de construir el agregado (ADR-0024). Una clave ajena a
        // `organizacion.empresas` ataría los dos módulos a migrarse y desplegarse juntos para
        // siempre, que es exactamente la frontera que el §4 levanta.
        ajenas.ShouldBeEmpty("no puede haber claves ajenas entre esquemas (§4, regla 4)");
    }

    private async Task<string> IndicesDeLaTablaAsync()
    {
        IReadOnlyList<(string Nombre, string Definicion)> todos = await ConsultarAsync(
            $"""
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = '{TercerosDbContext.Esquema}' AND tablename = '{Tabla}'
            """);

        return Environment.NewLine + string.Join(
            Environment.NewLine, todos.Select(indice => "   " + indice.Definicion));
    }

    private async Task<string?> TipoDeColumnaAsync(string tabla, string columna)
    {
        IReadOnlyList<(string Primera, string Segunda)> filas = await ConsultarAsync(
            $"""
            SELECT data_type, data_type
            FROM information_schema.columns
            WHERE table_schema = '{TercerosDbContext.Esquema}'
              AND table_name = '{tabla}' AND column_name = '{columna}'
            """);

        return filas.Count == 0 ? null : filas[0].Primera;
    }

    private async Task<IReadOnlyList<(string Primera, string Segunda)>> ConsultarAsync(string consulta)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(consulta, conexion);
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        List<(string, string)> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add((lector.GetString(0), lector.GetString(1)));
        }

        return filas;
    }
}
