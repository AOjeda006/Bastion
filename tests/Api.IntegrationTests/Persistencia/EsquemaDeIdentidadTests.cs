using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Terceros.Infrastructure.Persistencia;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// El esquema de Identidad, mirado <b>en la base</b> y no en la configuración de EF Core.
/// </summary>
/// <remarks>
/// <para>
/// Un <c>HasColumnType</c> mal puesto se lee bien; lo que dice si la columna es la que hace falta
/// es <c>information_schema</c>, que es lo que hay de verdad después de migrar.
/// </para>
/// <para>
/// Aquí está además la primera prueba REAL de que el historial por módulo del ítem 0.4 funciona:
/// con un solo módulo migrado, un historial mal ubicado pasaba igual. Con dos, no.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class EsquemaDeIdentidadTests(PostgresConTodosLosModulos postgres)
{
    [Fact]
    public async Task Cada_modulo_tiene_SU_historial_de_migraciones_en_SU_esquema()
    {
        IReadOnlyList<(string Esquema, string Tabla)> historiales = await ConsultarAsync(
            """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_name LIKE '%migrations%' OR table_name LIKE '%migraciones%'
            ORDER BY table_schema
            """);

        // Cuatro módulos migrados contra la MISMA base —Terceros entra en el ítem 1.5—. Con un
        // historial compartido, el segundo en migrar vería las migraciones del primero como suyas
        // y las daría por aplicadas: las tablas no se crearían y el error saldría mucho después,
        // al usarlas.
        historiales.Count.ShouldBe(4, "un historial por módulo, ni uno más ni uno menos");

        historiales.ShouldContain(
            (IdentidadDbContext.Esquema, IdentidadDbContext.TablaDelHistorial));
        historiales.ShouldContain(
            (OrganizacionDbContext.Esquema, OrganizacionDbContext.TablaDelHistorial));
        historiales.ShouldContain(
            (AuditoriaDbContext.Esquema, AuditoriaDbContext.TablaDelHistorial));
        historiales.ShouldContain(
            (TercerosDbContext.Esquema, TercerosDbContext.TablaDelHistorial));
    }

    [Fact]
    public async Task En_public_no_queda_ni_una_tabla_de_ningun_modulo()
    {
        IReadOnlyList<(string Esquema, string Tabla)> enPublic = await ConsultarAsync(
            """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            """);

        // `public` es de todos y por eso no es de nadie: una tabla ahí es una frontera que ya no
        // vigila nadie, y el día que dos módulos quieran llamar igual a la suya, chocan.
        enPublic.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("usuarios")]
    [InlineData("roles")]
    [InlineData("permisos_de_rol")]
    [InlineData("membresias")]
    [InlineData("roles_de_membresia")]
    [InlineData("tokens_de_refresco")]
    public async Task Las_tablas_del_modulo_estan_en_su_esquema_y_en_snake_case(string tabla)
    {
        IReadOnlyList<(string Esquema, string Tabla)> encontradas = await ConsultarAsync(
            $"""
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = '{IdentidadDbContext.Esquema}' AND table_name = '{tabla}'
            """);

        encontradas.Count.ShouldBe(1, $"falta la tabla {IdentidadDbContext.Esquema}.{tabla}");
    }

    [Fact]
    public async Task La_membresia_guarda_el_identificador_de_empresa_y_NO_una_clave_ajena()
    {
        (await TipoDeColumnaAsync("membresias", "empresa_id")).ShouldBe("uuid");

        IReadOnlyList<(string Esquema, string Tabla)> cruzadas = await ConsultarAsync(
            $"""
            SELECT origen.table_schema, destino.table_schema
            FROM information_schema.referential_constraints AS referencia
            JOIN information_schema.table_constraints AS origen
              ON origen.constraint_name = referencia.constraint_name
             AND origen.constraint_schema = referencia.constraint_schema
            JOIN information_schema.table_constraints AS destino
              ON destino.constraint_name = referencia.unique_constraint_name
             AND destino.constraint_schema = referencia.unique_constraint_schema
            WHERE origen.table_schema <> destino.table_schema
            """);

        // El motor dejaría poner la clave ajena a `organizacion.empresas`, y sería más cómoda: se
        // acabaron las filas huérfanas. Lo que se lleva por delante es la frontera —los dos
        // módulos pasarían a migrarse y desplegarse juntos para siempre—, así que el identificador
        // se guarda suelto y quien tiene que decir si existe es el otro módulo, por sus Contracts.
        cruzadas.ShouldBeEmpty("no puede haber claves ajenas entre esquemas (§4, regla 4)");
    }

    [Fact]
    public async Task El_usuario_se_bloquea_y_no_se_borra_asi_que_tiene_donde_apuntarlo()
    {
        // Un estado aparte, y no una fila menos: un usuario es una persona física, así que el
        // art. 32 de la LOPDGDD se aplica entero —bloquear, conservar y poder demostrar quién hizo
        // qué—.
        //
        // Desde el 0.10 esto son las TRES columnas del tipo complejo compartido, las mismas que
        // llevan empresa y almacén. El `estado` de texto que había aquí antes decía lo mismo con
        // otras palabras y en otro sitio, y era la tercera copia de la misma idea.
        (await TipoDeColumnaAsync("usuarios", "bloqueado")).ShouldBe("boolean");
        (await TipoDeColumnaAsync("usuarios", "bloqueado_en")).ShouldBe("timestamp with time zone");
        (await TipoDeColumnaAsync("usuarios", "motivo_del_bloqueo")).ShouldBe("text");

        // Y sigue habiendo DOS bloqueos, no uno: `rechazado_hasta` es el rechazo temporal por
        // intentos fallidos, que se levanta solo. Si algún día una fuerza bruta pudiera dar de
        // baja la cuenta de alguien, sería porque estas columnas se fundieron.
        (await TipoDeColumnaAsync("usuarios", "rechazado_hasta")).ShouldBe("timestamp with time zone");
        (await TipoDeColumnaAsync("usuarios", "estado")).ShouldBeNull();
    }

    [Theory]
    [InlineData("usuarios", "creado_en")]
    // R14: la marca de modificación llega con `EntidadBase`, y el rol —que nunca tuvo fecha—
    // estrena las dos.
    [InlineData("usuarios", "modificado_en")]
    [InlineData("roles", "creado_en")]
    [InlineData("roles", "modificado_en")]
    [InlineData("usuarios", "ultimo_acceso_en")]
    [InlineData("usuarios", "rechazado_hasta")]
    [InlineData("tokens_de_refresco", "expira_en")]
    [InlineData("tokens_de_refresco", "canjeado_en")]
    [InlineData("tokens_de_refresco", "revocado_en")]
    public async Task Los_instantes_llevan_zona_horaria(string tabla, string columna)
    {
        // Son momentos, no fechas de calendario: «cuándo caducó el token» tiene que significar lo
        // mismo en Madrid y en Canarias, y un `timestamp` sin zona significa lo que diga el
        // servidor que lo lea.
        (await TipoDeColumnaAsync(tabla, columna)).ShouldBe("timestamp with time zone");
    }

    [Fact]
    public async Task El_refresco_se_guarda_como_resumen_y_con_su_indice_unico()
    {
        (await TipoDeColumnaAsync("tokens_de_refresco", "hash")).ShouldBe("character varying");

        IReadOnlyList<(string Esquema, string Tabla)> unico = await ConsultarAsync(
            $"""
            SELECT schemaname, indexname
            FROM pg_indexes
            WHERE schemaname = '{IdentidadDbContext.Esquema}'
              AND tablename = 'tokens_de_refresco'
              AND indexdef LIKE 'CREATE UNIQUE INDEX%(hash)'
            """);

        // Guardado en claro, quien lea la tabla se lleva sesiones vivas de catorce días. El índice
        // único es lo que hace que buscar por el resumen siga siendo una búsqueda y no un barrido.
        unico.Count.ShouldBe(1);
    }

    [Fact]
    public async Task El_correo_es_unico_porque_es_con_lo_que_se_entra()
    {
        IReadOnlyList<(string Esquema, string Tabla)> unico = await ConsultarAsync(
            $"""
            SELECT schemaname, indexname
            FROM pg_indexes
            WHERE schemaname = '{IdentidadDbContext.Esquema}'
              AND tablename = 'usuarios'
              AND indexdef LIKE 'CREATE UNIQUE INDEX%(correo)'
            """);

        unico.Count.ShouldBe(1);
    }

    private async Task<string?> TipoDeColumnaAsync(string tabla, string columna)
    {
        IReadOnlyList<(string Esquema, string Tabla)> filas = await ConsultarAsync(
            $"""
            SELECT data_type, data_type
            FROM information_schema.columns
            WHERE table_schema = '{IdentidadDbContext.Esquema}'
              AND table_name = '{tabla}' AND column_name = '{columna}'
            """);

        return filas.Count == 0 ? null : filas[0].Esquema;
    }

    private async Task<IReadOnlyList<(string Esquema, string Tabla)>> ConsultarAsync(string consulta)
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
