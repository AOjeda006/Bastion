using System.Data;
using System.Globalization;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;
using Npgsql;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// Los cinco maestros que el ítem 0.15 trae del §7, mirados en la base de datos: sus tablas, los
/// tipos de sus columnas, y la única regla del módulo que un índice no sabe expresar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué hay aquí un test que ESCRIBE.</b> Todo lo demás de este proyecto mira
/// <c>information_schema</c> y no toca una fila; con la restricción de exclusión eso no basta.
/// Que exista una restricción llamada <c>impuestos_sin_tramos_solapados</c> y que sea de tipo
/// <c>x</c> no dice que <b>rechace</b> nada: una expresión mal escrita —el rango al revés, el
/// operador de solape puesto sobre la columna equivocada— crea una restricción perfectamente
/// válida que acepta los dos tramos pisándose. La única forma de saber que la puerta cierra es
/// empujarla.
/// </para>
/// <para>
/// <b>Y otros que escriben y esperan que les dejen.</b> Una restricción que lo rechazara todo
/// también pasaría el caso de arriba. Por eso cada rechazo tiene su gemelo: dos tramos que se
/// suceden entran, y dos códigos distintos conviven en las mismas fechas.
/// </para>
/// </remarks>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class MaestrosDelSeptimoApartadoTests(PostgresDeVerdad postgres) : IAsyncLifetime
{
    private const string Impuestos = "impuestos";

    /// <inheritdoc/>
    // Los tests de esta colección comparten contenedor y corren en serie. Vaciar la tabla ANTES
    // —y no después— es lo que hace que cada caso empiece igual aunque el anterior se caiga a
    // mitad y deje filas puestas.
    public async Task InitializeAsync() =>
        await EjecutarAsync($"DELETE FROM {OrganizacionDbContext.Esquema}.{Impuestos}");

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(Impuestos)]
    [InlineData("divisas")]
    [InlineData("tipos_de_cambio")]
    [InlineData("unidades_de_medida")]
    [InlineData("conversiones_de_unidades")]
    [InlineData("ubicaciones")]
    public async Task Los_seis_maestros_estan_en_el_esquema_del_modulo(string tabla)
    {
        IReadOnlyList<(string Uno, string Dos)> encontradas = await ConsultarAsync(
            $"""
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = '{OrganizacionDbContext.Esquema}' AND table_name = '{tabla}'
            """);

        encontradas.Count.ShouldBe(1, $"falta la tabla {OrganizacionDbContext.Esquema}.{tabla}");
    }

    [Theory]
    [InlineData(Impuestos, "vigente_desde")]
    [InlineData(Impuestos, "vigente_hasta")]
    [InlineData("tipos_de_cambio", "fecha")]
    public async Task Las_fechas_de_los_maestros_son_de_calendario(string tabla, string columna)
    {
        // El 1 de septiembre de 2012 el IVA general pasó del 18 % al 21 % en Madrid y en Canarias
        // el mismo día, y el BCE publica la cotización de un día, no de un instante (R14).
        (await TipoDeColumnaAsync(tabla, columna)).ShouldBe("date");
    }

    [Theory]
    [InlineData(Impuestos, "porcentaje", 5, 2)]
    [InlineData("tipos_de_cambio", "tasa", 19, 6)]
    [InlineData("conversiones_de_unidades", "factor", 19, 6)]
    public async Task El_dinero_y_lo_que_lo_multiplica_son_numeric_y_nunca_flotantes(
        string tabla, string columna, int precision, int escala)
    {
        // R6. Un `double precision` no puede representar 0,1, y el error que arrastra acaba en la
        // casilla de un modelo 303. Que el tipo sea `numeric` no basta: un `numeric` sin escala
        // declarada admitiría cualquier cosa, y la tasa del BCE viene con seis decimales.
        (await TipoDeColumnaAsync(tabla, columna)).ShouldBe("numeric");
        (await PrecisionYEscalaAsync(tabla, columna)).ShouldBe((precision, escala));
    }

    [Fact]
    public async Task La_divisa_NO_guarda_sus_decimales_y_la_unidad_de_medida_SI()
    {
        // La distinción entera de los dos maestros, comprobada por lo que hay y por lo que no.
        // Cuántos decimales tiene un euro es una regla fiscal —dos, y no los elige nadie—, así que
        // vive en el catálogo del código con su caso dorado; cuántos admite un kilo es una
        // preferencia de quien monta el almacén, y esa sí es una columna.
        (await TipoDeColumnaAsync("divisas", "decimales")).ShouldBeNull(
            "los decimales de una divisa los fija el catálogo, no una fila que alguien pueda editar");

        (await TipoDeColumnaAsync("unidades_de_medida", "decimales")).ShouldBe("integer");
    }

    [Fact]
    public async Task La_restriccion_que_impide_el_solape_existe_y_es_de_exclusion()
    {
        IReadOnlyList<(string Tipo, string Metodo)> restricciones = await ConsultarAsync(
            $"""
            SELECT restriccion.contype::text, COALESCE(metodo.amname::text, '')
            FROM pg_constraint AS restriccion
            JOIN pg_class AS relacion ON relacion.oid = restriccion.conrelid
            JOIN pg_namespace AS esquema ON esquema.oid = relacion.relnamespace
            LEFT JOIN pg_class AS soporte ON soporte.oid = restriccion.conindid
            LEFT JOIN pg_am AS metodo ON metodo.oid = soporte.relam
            WHERE esquema.nspname = '{OrganizacionDbContext.Esquema}'
              AND relacion.relname = '{Impuestos}'
              AND restriccion.conname = '{ConfiguracionDeImpuesto.RestriccionDeSolape}'
            """);

        restricciones.Count.ShouldBe(
            1,
            $"la migración crea `{ConfiguracionDeImpuesto.RestriccionDeSolape}` con SQL en bruto, " +
            "porque EF Core no sabe escribir un EXCLUDE");

        restricciones[0].Tipo.ShouldBe("x", "`x` es exclusión; `u` sería un índice único, que no vale");
        restricciones[0].Metodo.ShouldBe("gist", "el operador de solape de un rango solo lo indexa gist");
    }

    [Fact]
    public async Task Dos_tramos_del_mismo_impuesto_no_pueden_pisarse()
    {
        await InsertarTramoAsync("IVA-GEN", "2012-09-01", null);

        PostgresException rechazo = await Should.ThrowAsync<PostgresException>(
            () => InsertarTramoAsync("IVA-GEN", "2020-01-01", "2020-12-31"));

        // El tramo abierto llega hasta el infinito, así que cualquiera posterior se le monta
        // encima. Es el caso que de verdad se da: alguien da de alta la subida al 21 % y se
        // olvida de cerrar el tramo del 18 %.
        rechazo.SqlState.ShouldBe(PostgresErrorCodes.ExclusionViolation);
        rechazo.ConstraintName.ShouldBe(ConfiguracionDeImpuesto.RestriccionDeSolape);
    }

    [Fact]
    public async Task El_solape_de_un_solo_dia_tambien_se_rechaza()
    {
        // El rango es CERRADO por los dos lados, igual que `Impuesto.RigeEl`: un tramo que acaba
        // el 31 de diciembre y otro que empieza ese mismo día se pisan un día, y ese día habría
        // dos porcentajes vigentes a la vez. Con un rango medio abierto esto pasaría.
        await InsertarTramoAsync("IVA-RED", "2019-01-01", "2019-12-31");

        PostgresException rechazo = await Should.ThrowAsync<PostgresException>(
            () => InsertarTramoAsync("IVA-RED", "2019-12-31", "2020-12-31"));

        rechazo.SqlState.ShouldBe(PostgresErrorCodes.ExclusionViolation);
    }

    [Fact]
    public async Task Dos_tramos_seguidos_del_mismo_impuesto_entran_sin_problema()
    {
        // El gemelo del anterior, y lo que impide que la restricción sea una puerta tapiada: la
        // sucesión de tramos es el caso NORMAL de un maestro fiscal —el 18 % hasta el 31 de agosto
        // de 2012, el 21 % desde el 1 de septiembre—, y tiene que caber.
        await InsertarTramoAsync("IVA-SUC", "2010-07-01", "2012-08-31");
        await InsertarTramoAsync("IVA-SUC", "2012-09-01", null);

        (await CuantosTramosAsync("IVA-SUC")).ShouldBe(2);
    }

    [Fact]
    public async Task Dos_impuestos_distintos_conviven_en_las_mismas_fechas()
    {
        // El otro gemelo: la restricción excluye por PAR (código, rango). Si excluyera solo por
        // rango, el IVA general y el reducido no podrían estar vigentes a la vez, que es
        // justamente lo que llevan haciendo desde siempre.
        await InsertarTramoAsync("IGIC-GEN", "2012-09-01", null);
        await InsertarTramoAsync("IGIC-RED", "2012-09-01", null);

        (await CuantosTramosAsync("IGIC-GEN")).ShouldBe(1);
        (await CuantosTramosAsync("IGIC-RED")).ShouldBe(1);
    }

    // SQL en bruto y no el `DbContext`: este proyecto abre sus contextos con un inquilino que
    // lanza en cuanto alguien le pregunta (ver `PostgresDeVerdad`), y con razón. Lo que se prueba
    // aquí es la restricción de la BASE, que no sabe nada de EF ni tiene por qué.
    private async Task InsertarTramoAsync(string codigo, string desde, string? hasta)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            $"""
            INSERT INTO {OrganizacionDbContext.Esquema}.{Impuestos}
                (id, codigo, nombre, tipo, porcentaje, vigente_desde, vigente_hasta,
                 creado_en, modificado_en)
            VALUES
                (gen_random_uuid(), @codigo, 'Tramo de prueba', 'Iva', 21.00,
                 @desde::date, @hasta::date, now(), now())
            """,
            conexion);

        orden.Parameters.AddWithValue("codigo", codigo);
        orden.Parameters.AddWithValue("desde", desde);
        orden.Parameters.AddWithValue("hasta", (object?)hasta ?? DBNull.Value);

        await orden.ExecuteNonQueryAsync();
    }

    private async Task<int> CuantosTramosAsync(string codigo)
    {
        IReadOnlyList<(string Cuantos, string Nada)> filas = await ConsultarAsync(
            $"""
            SELECT count(*)::text, ''
            FROM {OrganizacionDbContext.Esquema}.{Impuestos}
            WHERE codigo = '{codigo}'
            """);

        return int.Parse(filas[0].Cuantos, CultureInfo.InvariantCulture);
    }

    private async Task<string?> TipoDeColumnaAsync(string tabla, string columna)
    {
        IReadOnlyList<(string Uno, string Dos)> filas = await ConsultarAsync(
            $"""
            SELECT data_type, data_type
            FROM information_schema.columns
            WHERE table_schema = '{OrganizacionDbContext.Esquema}'
              AND table_name = '{tabla}' AND column_name = '{columna}'
            """);

        return filas.Count == 0 ? null : filas[0].Uno;
    }

    private async Task<(int Precision, int Escala)> PrecisionYEscalaAsync(
        string tabla, string columna)
    {
        IReadOnlyList<(string Uno, string Dos)> filas = await ConsultarAsync(
            $"""
            SELECT COALESCE(numeric_precision::text, ''), COALESCE(numeric_scale::text, '')
            FROM information_schema.columns
            WHERE table_schema = '{OrganizacionDbContext.Esquema}'
              AND table_name = '{tabla}' AND column_name = '{columna}'
            """);

        filas.Count.ShouldBe(1, $"no existe la columna {tabla}.{columna}");

        return (
            int.Parse(filas[0].Uno, CultureInfo.InvariantCulture),
            int.Parse(filas[0].Dos, CultureInfo.InvariantCulture));
    }

    private async Task EjecutarAsync(string sql)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(sql, conexion);
        await orden.ExecuteNonQueryAsync();
    }

    private async Task<IReadOnlyList<(string, string)>> ConsultarAsync(string sql)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(sql, conexion);
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync(CommandBehavior.Default);

        List<(string, string)> filas = [];
        while (await lector.ReadAsync())
        {
            filas.Add((lector.GetString(0), lector.GetString(1)));
        }

        return filas;
    }
}
