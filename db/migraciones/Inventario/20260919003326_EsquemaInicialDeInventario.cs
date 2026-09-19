using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El esquema `inventario`: el libro mayor de existencias (R3), particionado por mes, y el
    /// primer documento que lo escribe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La tabla del libro la crea un `Sql()` y no un `CreateTable()`.</b> EF Core no sabe
    /// generar `PARTITION BY RANGE`, así que el `CreateTable` que salió del andamiaje se ha
    /// sustituido por el DDL a mano. Las columnas, la clave y las dos `CHECK` son exactamente las
    /// que el modelo declara —la instantánea sigue siendo la de EF y `has-pending-model-changes`
    /// sigue comparando lo mismo—; lo único que EF no puede expresar es la partición. Las otras
    /// dos tablas, `ajustes` y `lineas_ajuste`, salen del andamiaje sin tocar.
    /// </para>
    /// <para>
    /// <b>La clave primaria del libro lleva la fecha porque PostgreSQL lo exige:</b> toda
    /// restricción única de una tabla particionada tiene que contener la clave de partición. No es
    /// una elección de modelado.
    /// </para>
    /// <para>
    /// <b>Y esta migración deja la tabla USABLE, no solo creada.</b> Llama ella misma a
    /// `asegurar_particiones_de_movimientos()`, que es la misma función que el migrador llama en
    /// cada despliegue. Sin esa llamada, una base recién migrada solo tendría la partición por
    /// defecto y la primera fila caería ahí — incluida la que inventa
    /// `LasMigracionesSobreTablasConFilasTests`. Que la llamen las dos es la razón de que la
    /// función viva en la base y no en C#.
    /// </para>
    /// </remarks>
    /// <inheritdoc />
    public partial class EsquemaInicialDeInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.EnsureSchema(
                name: "inventario");

            migrationBuilder.CreateTable(
                name: "ajustes",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_de_operacion = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ajustes", x => x.id);
                });

            migrationBuilder.Sql(
                """
                CREATE TABLE inventario.movimiento_stock (
                    id uuid NOT NULL,
                    fecha_de_operacion date NOT NULL,
                    empresa_id uuid NOT NULL,
                    almacen_id uuid NOT NULL,
                    ubicacion_id uuid NOT NULL,
                    articulo_id uuid NOT NULL,
                    cantidad_en_unidad_base numeric(18,6) NOT NULL,
                    cantidad_introducida numeric(18,6) NOT NULL,
                    unidad_introducida_id uuid NOT NULL,
                    factor_a_unidad_base numeric(18,6) NOT NULL,
                    documento_origen_tipo character varying(20) NOT NULL,
                    documento_origen_id uuid NOT NULL,
                    coste_unitario_cantidad numeric(18,4) NOT NULL,
                    coste_unitario_divisa character varying(3) NOT NULL,
                    creado_en timestamp with time zone NOT NULL,
                    modificado_en timestamp with time zone NOT NULL,
                    CONSTRAINT pk_movimiento_stock PRIMARY KEY (id, fecha_de_operacion),
                    CONSTRAINT ck_movimiento_stock_cantidad_no_nula
                        CHECK (cantidad_introducida <> 0 AND cantidad_en_unidad_base <> 0),
                    CONSTRAINT ck_movimiento_stock_cantidad_por_factor
                        CHECK (cantidad_en_unidad_base
                               = round(cantidad_introducida * factor_a_unidad_base, 6))
                ) PARTITION BY RANGE (fecha_de_operacion);
                """);

            // LA PARTICIÓN POR DEFECTO, y su papel es el de una red y no el de un sitio donde
            // vivir. Un mes sin partición no puede reventar el `INSERT` en producción: la fila cae
            // aquí y no se pierde. Que esta partición esté VACÍA es lo que comprueban los casos de
            // integración, sin salvedades para nadie.
            migrationBuilder.Sql(
                """
                CREATE TABLE inventario.movimiento_stock_por_defecto
                    PARTITION OF inventario.movimiento_stock DEFAULT;
                """);

            // LO QUE HACE QUE «SOLO SE ANADE» SEA VERDAD, y es el mismo mecanismo que protege
            // `auditoria.registros` desde el 0.5, por el mismo motivo que está escrito allí: un
            // REVOKE no vale, porque los permisos los da y los quita el mismo dueño de la tabla,
            // que es el usuario con el que se conecta la aplicación. Un permiso que el interesado
            // puede devolverse a sí mismo es una frase, no una guarda.
            //
            // No es lógica de negocio en un disparador: es una restricción de integridad, de la
            // misma familia que un CHECK. Mira la OPERACION y la rechaza; no decide nada, no
            // calcula nada y no depende de ningún dato.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION inventario.movimientos_son_de_solo_anadido() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION
                        'inventario.movimiento_stock es de solo anadido: % no esta permitido sobre esta tabla.',
                        TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;
                """);

            // EL DE FILA VA UNA VEZ, SOBRE EL PADRE, Y SE HEREDA. Medido contra
            // postgres:17.6-alpine: un disparador `FOR EACH ROW` creado en el padre aparece en las
            // particiones que ya existen Y en las que se creen después.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER movimientos_sin_modificar_ni_borrar
                    BEFORE UPDATE OR DELETE ON inventario.movimiento_stock
                    FOR EACH ROW EXECUTE FUNCTION inventario.movimientos_son_de_solo_anadido();
                """);

            // EL DE TRUNCATE NO SE HEREDA, Y ESTE ES EL AGUJERO QUE HABRIA QUEDADO ABIERTO.
            //
            // Medido: con el disparador solo en el padre, `TRUNCATE inventario.movimiento_stock`
            // sale rechazado, pero `TRUNCATE inventario.movimiento_stock_2026_09` SE EJECUTA y se
            // lleva las filas de ese mes. Los disparadores `FOR EACH STATEMENT` no se clonan a las
            // particiones. Así que va uno en el padre, uno aquí en la de por defecto, y uno en
            // cada partición mensual — ese lo pone la función que las crea, que es la otra razón
            // de que crear una partición sea una función y no un `CREATE TABLE` suelto.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER movimientos_sin_vaciar
                    BEFORE TRUNCATE ON inventario.movimiento_stock
                    FOR EACH STATEMENT EXECUTE FUNCTION inventario.movimientos_son_de_solo_anadido();
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER movimientos_sin_vaciar
                    BEFORE TRUNCATE ON inventario.movimiento_stock_por_defecto
                    FOR EACH STATEMENT EXECUTE FUNCTION inventario.movimientos_son_de_solo_anadido();
                """);

            // QUIEN CREA LAS PARTICIONES, Y POR QUE VIVE EN LA BASE.
            //
            // La llaman dos: esta migración, para que el mes de la instalación exista desde el
            // primer instante, y el migrador en CADA despliegue (ADR-0021), para que la pista se
            // mantenga doce meses por delante. Escrita en C# no la podría llamar la migración;
            // escrita dos veces serían dos verdades.
            //
            // Los meses se calculan con `current_date`, no se escriben a mano: una base instalada
            // en 2028 necesita las particiones de 2028, no las del día en que se escribió esto.
            //
            // N = 12 por omisión: agotar la pista exige un año natural entero sin desplegar. El
            // valor vive AQUI y en ningún otro sitio; quien llama, llama sin argumento.
            //
            // Y el día que se agote, el mensaje dice el mes. Medido: una vez que la partición por
            // defecto tiene una fila de un mes, `CREATE TABLE … PARTITION OF` de ese mes falla con
            // `check_violation`, así que el despliegue se para —el migrador sale con 1 y la API no
            // arranca— en vez de degradarse en silencio. La función NO mueve esas filas para
            // arreglarse: tendría que desarmar el disparador de solo añadido, y un migrador que se
            // cura solo esconde la avería que hay que ver.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION inventario.asegurar_particiones_de_movimientos(
                    meses_por_delante integer DEFAULT 12) RETURNS integer
                LANGUAGE plpgsql AS $$
                DECLARE
                    creadas integer := 0;
                    desplazamiento integer;
                    inicio date;
                    fin date;
                    nombre text;
                BEGIN
                    IF meses_por_delante < 0 THEN
                        RAISE EXCEPTION 'los meses por delante no pueden ser negativos: %', meses_por_delante
                            USING ERRCODE = 'invalid_parameter_value';
                    END IF;

                    FOR desplazamiento IN 0..meses_por_delante LOOP
                        inicio := (date_trunc('month', current_date)
                                   + make_interval(months => desplazamiento))::date;
                        fin := (inicio + interval '1 month')::date;
                        nombre := 'movimiento_stock_' || to_char(inicio, 'YYYY_MM');

                        IF NOT EXISTS (
                            SELECT 1 FROM pg_catalog.pg_class c
                            JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                            WHERE n.nspname = 'inventario' AND c.relname = nombre)
                        THEN
                            BEGIN
                                EXECUTE format(
                                    'CREATE TABLE inventario.%I PARTITION OF inventario.movimiento_stock FOR VALUES FROM (%L) TO (%L)',
                                    nombre, inicio, fin);
                            EXCEPTION WHEN check_violation THEN
                                RAISE EXCEPTION
                                    'no se puede crear la particion % porque la particion por defecto ya tiene filas de ese mes. La pista de particiones se agoto: hay filas en inventario.movimiento_stock_por_defecto que hay que reubicar antes de volver a desplegar.',
                                    nombre
                                    USING ERRCODE = 'object_not_in_prerequisite_state';
                            END;

                            creadas := creadas + 1;
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM pg_catalog.pg_trigger t
                            JOIN pg_catalog.pg_class c ON c.oid = t.tgrelid
                            JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                            WHERE n.nspname = 'inventario' AND c.relname = nombre
                              AND t.tgname = 'movimientos_sin_vaciar')
                        THEN
                            EXECUTE format(
                                'CREATE TRIGGER movimientos_sin_vaciar BEFORE TRUNCATE ON inventario.%I FOR EACH STATEMENT EXECUTE FUNCTION inventario.movimientos_son_de_solo_anadido()',
                                nombre);
                        END IF;
                    END LOOP;

                    RETURN creadas;
                END;
                $$;
                """);

            migrationBuilder.Sql("SELECT inventario.asegurar_particiones_de_movimientos();");

            migrationBuilder.CreateTable(
                name: "lineas_ajuste",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ajuste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ubicacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_introducida = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unidad_introducida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor_a_unidad_base = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    coste_unitario_cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    coste_unitario_divisa = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lineas_ajuste", x => x.id);
                    table.CheckConstraint("ck_lineas_ajuste_cantidad_y_factor", "cantidad_introducida <> 0 AND factor_a_unidad_base > 0");
                    table.ForeignKey(
                        name: "fk_lineas_ajuste_ajustes_ajuste_id",
                        column: x => x.ajuste_id,
                        principalSchema: "inventario",
                        principalTable: "ajustes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_empresa_id_fecha_de_operacion",
                schema: "inventario",
                table: "ajustes",
                columns: new[] { "empresa_id", "fecha_de_operacion" });

            migrationBuilder.CreateIndex(
                name: "ix_lineas_ajuste_ajuste_id",
                schema: "inventario",
                table: "lineas_ajuste",
                column: "ajuste_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_stock_documento_origen_tipo_documento_origen_id",
                schema: "inventario",
                table: "movimiento_stock",
                columns: new[] { "documento_origen_tipo", "documento_origen_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_stock_empresa_id_articulo_id_ubicacion_id_fecha_",
                schema: "inventario",
                table: "movimiento_stock",
                columns: new[] { "empresa_id", "articulo_id", "ubicacion_id", "fecha_de_operacion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "lineas_ajuste",
                schema: "inventario");

            // `DROP TABLE` del padre se lleva las particiones y sus disparadores. Las DOS
            // funciones no se van con ella, y una función huérfana haría fallar el siguiente `Up`
            // con un «ya existe» — que es el motivo por el que el `Down` del esquema de auditoría
            // lleva su `DROP FUNCTION` desde el 0.5.
            migrationBuilder.Sql("DROP TABLE IF EXISTS inventario.movimiento_stock CASCADE;");
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS inventario.asegurar_particiones_de_movimientos(integer);");
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS inventario.movimientos_son_de_solo_anadido();");

            migrationBuilder.DropTable(
                name: "ajustes",
                schema: "inventario");
        }
    }
}
