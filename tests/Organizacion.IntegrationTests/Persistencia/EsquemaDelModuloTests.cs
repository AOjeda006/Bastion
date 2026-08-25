using System.Data;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// Comprueba el esquema MIRANDO LA BASE DE DATOS, no la configuración de EF Core. Una
/// configuración que dice una cosa y una tabla que hace otra es exactamente el fallo que estos
/// tests existen para encontrar.
/// </summary>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class EsquemaDelModuloTests(PostgresDeVerdad postgres)
{
    [Fact]
    public async Task El_historial_de_migraciones_vive_en_el_esquema_del_modulo_y_no_en_public()
    {
        // LA TRAMPA. EF Core deja el historial en `public.__EFMigrationsHistory` por omisión, y
        // ese sitio es COMPARTIDO: el segundo módulo que migre encontraría allí las migraciones
        // del primero, se creería al día y no aplicaría las suyas. El fallo no es un error, es
        // un esquema incompleto en silencio.
        //
        // Y se comprueba mirando la tabla, no la configuración: `MigrationsHistoryTable` puede
        // estar escrito y no llegar al proveedor si el cableado no lo pasa.
        IReadOnlyList<(string Esquema, string Tabla)> historiales =
            await ConsultarAsync(
                """
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_name LIKE '%migrations%' OR table_name LIKE '%migraciones%'
                ORDER BY table_schema
                """);

        historiales.Count.ShouldBe(1, "debería haber exactamente un historial, el del módulo");
        historiales[0].Esquema.ShouldBe(OrganizacionDbContext.Esquema);
        historiales[0].Tabla.ShouldBe(OrganizacionDbContext.TablaDelHistorial);
    }

    [Fact]
    public async Task En_el_esquema_public_no_hay_ni_una_tabla_del_modulo()
    {
        IReadOnlyList<(string Esquema, string Tabla)> enPublic =
            await ConsultarAsync(
                """
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_schema = 'public'
                """);

        enPublic.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("empresas")]
    [InlineData("ejercicios")]
    [InlineData("series")]
    [InlineData("almacenes")]
    public async Task Las_cuatro_tablas_estan_en_el_esquema_del_modulo_y_en_snake_case(string tabla)
    {
        IReadOnlyList<(string Esquema, string Tabla)> encontradas =
            await ConsultarAsync(
                $"""
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_schema = '{OrganizacionDbContext.Esquema}' AND table_name = '{tabla}'
                """);

        encontradas.Count.ShouldBe(1, $"falta la tabla {OrganizacionDbContext.Esquema}.{tabla}");
    }

    [Theory]
    // R8: `empresa_id` en toda entidad transaccional, desde la PRIMERA tabla. La columna es de
    // hoy; el filtro global que la aplica siempre es del ítem 0.6.
    [InlineData("ejercicios", "empresa_id")]
    [InlineData("series", "empresa_id")]
    [InlineData("almacenes", "empresa_id")]
    public async Task Toda_entidad_transaccional_lleva_su_empresa_desde_la_primera_tabla(
        string tabla, string columna)
    {
        (await TipoDeColumnaAsync(tabla, columna)).ShouldBe("uuid");
    }

    [Fact]
    public async Task La_empresa_no_lleva_empresa_id_porque_ella_es_el_inquilino()
    {
        (await TipoDeColumnaAsync("empresas", "empresa_id")).ShouldBeNull();
    }

    [Theory]
    [InlineData("fecha_de_inicio")]
    [InlineData("fecha_de_fin")]
    public async Task Las_fechas_de_negocio_son_date_y_no_timestamptz(string columna)
    {
        // Un ejercicio contable no tiene zona horaria: empieza el 1 de enero en Madrid y en
        // Canarias. Guardarlo como `timestamp with time zone` obligaría a elegir una, y el
        // 1 de enero a las 00:00 en Madrid ya es el 31 de diciembre en UTC-1.
        (await TipoDeColumnaAsync("ejercicios", columna)).ShouldBe("date");
    }

    [Theory]
    [InlineData("empresas", "bloqueada_en")]
    [InlineData("almacenes", "bloqueado_en")]
    public async Task El_instante_de_bloqueo_si_lleva_zona_horaria_porque_es_un_momento(
        string tabla, string columna)
    {
        // De esta fecha arranca el plazo de prescripción del art. 32 de la LOPDGDD: es un punto
        // en la línea del tiempo, no una fecha de calendario. La distinción con `date` no es
        // estilística, y por eso las dos están probadas.
        (await TipoDeColumnaAsync(tabla, columna)).ShouldBe("timestamp with time zone");
    }

    [Fact]
    public async Task El_NIF_lleva_tope_porque_su_longitud_es_una_regla_y_no_una_estimacion()
    {
        (await TipoDeColumnaAsync("empresas", "nif")).ShouldBe("character varying");
        (await LongitudDeColumnaAsync("empresas", "nif")).ShouldBe(9);
    }

    [Theory]
    [InlineData("empresas", "razon_social")]
    [InlineData("almacenes", "nombre")]
    [InlineData("series", "formato")]
    public async Task Lo_que_no_tiene_un_limite_de_negocio_es_text_y_no_un_varchar_inventado(
        string tabla, string columna)
    {
        (await TipoDeColumnaAsync(tabla, columna)).ShouldBe("text");
    }

    [Theory]
    // R17: la dirección va en campos separados. Si algún día alguien la aplasta en un `text`,
    // estos nombres de columna dejan de existir y el test cae.
    [InlineData("domicilio_fiscal_calle")]
    [InlineData("domicilio_fiscal_numero")]
    [InlineData("domicilio_fiscal_codigo_postal")]
    [InlineData("domicilio_fiscal_poblacion")]
    [InlineData("domicilio_fiscal_subdivision")]
    [InlineData("domicilio_fiscal_pais")]
    public async Task El_domicilio_fiscal_esta_en_campos_estructurados(string columna)
    {
        (await TipoDeColumnaAsync("empresas", columna)).ShouldBe("character varying");
    }

    [Fact]
    public async Task Los_topes_de_la_direccion_son_los_del_rulebook_de_SEPA()
    {
        (await LongitudDeColumnaAsync("empresas", "domicilio_fiscal_calle")).ShouldBe(70);
        (await LongitudDeColumnaAsync("empresas", "domicilio_fiscal_codigo_postal")).ShouldBe(16);
        (await LongitudDeColumnaAsync("empresas", "domicilio_fiscal_poblacion")).ShouldBe(35);
        (await LongitudDeColumnaAsync("empresas", "domicilio_fiscal_pais")).ShouldBe(2);
    }

    [Fact]
    public async Task El_contador_de_la_serie_es_una_columna_y_NO_una_secuencia_de_PostgreSQL()
    {
        // `nextval` no se revierte al deshacer la transacción: una confirmación fallida dejaría
        // un hueco PERMANENTE en la numeración, y R5 dice «correlativa y sin huecos». Que no
        // exista ninguna secuencia en el esquema es la comprobación, no que el modelo lo diga.
        (await TipoDeColumnaAsync("series", "contador")).ShouldBe("bigint");

        IReadOnlyList<(string Esquema, string Tabla)> secuencias = await ConsultarAsync(
            $"""
            SELECT sequence_schema, sequence_name
            FROM information_schema.sequences
            WHERE sequence_schema = '{OrganizacionDbContext.Esquema}'
            """);

        secuencias.ShouldBeEmpty("R5 prohíbe los huecos, y `nextval` los deja");
    }

    [Fact]
    public async Task Los_enumerados_se_guardan_como_texto_y_no_como_numero()
    {
        // Un enumerado guardado por su valor entero deja de significar nada en cuanto alguien
        // reordena el enumerado. En un ERP los datos duran más que el código.
        (await TipoDeColumnaAsync("empresas", "regimen_de_iva")).ShouldBe("text");
        (await TipoDeColumnaAsync("empresas", "estado")).ShouldBe("text");
        (await TipoDeColumnaAsync("series", "tipo_de_documento")).ShouldBe("text");
        (await TipoDeColumnaAsync("almacenes", "tipo")).ShouldBe("text");
    }

    [Fact]
    public async Task No_hay_ninguna_clave_foranea_que_salga_del_esquema_del_modulo()
    {
        // §3 del plan maestro: sin claves foráneas entre esquemas. Una que cruzara ataría dos
        // módulos por la base de datos y haría imposible extraer uno sin tocar el otro.
        IReadOnlyList<(string Esquema, string Tabla)> cruzadas = await ConsultarAsync(
            $"""
            SELECT origen.table_schema, origen.table_name
            FROM information_schema.referential_constraints AS referencia
            JOIN information_schema.table_constraints AS origen
              ON origen.constraint_name = referencia.constraint_name
            JOIN information_schema.table_constraints AS destino
              ON destino.constraint_name = referencia.unique_constraint_name
            WHERE origen.table_schema = '{OrganizacionDbContext.Esquema}'
              AND destino.table_schema <> '{OrganizacionDbContext.Esquema}'
            """);

        cruzadas.ShouldBeEmpty();
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

    private async Task<int?> LongitudDeColumnaAsync(string tabla, string columna)
    {
        IReadOnlyList<(string Uno, string Dos)> filas = await ConsultarAsync(
            $"""
            SELECT COALESCE(character_maximum_length::text, ''), ''
            FROM information_schema.columns
            WHERE table_schema = '{OrganizacionDbContext.Esquema}'
              AND table_name = '{tabla}' AND column_name = '{columna}'
            """);

        return filas.Count == 0 || filas[0].Uno.Length == 0
            ? null
            : int.Parse(filas[0].Uno, System.Globalization.CultureInfo.InvariantCulture);
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
