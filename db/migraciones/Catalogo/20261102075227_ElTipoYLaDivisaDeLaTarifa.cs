using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Catalogo.Infrastructure.Migrations
{
    /// <summary>
    /// La tarifa, de venta o de compra, y con su divisa por el código ISO 4217 en lugar del
    /// identificador del maestro de Organización (ADR-0076).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las dos columnas entran nulas, se rellenan y se cierran</b>, como la trazabilidad del
    /// artículo. Lo que propone EF Core es soltar <c>divisa_id</c> y añadir las dos columnas
    /// <c>NOT NULL</c> con la cadena vacía por omisión: la divisa de cada tarifa guardada se
    /// perdería, y la cadena vacía no es ningún tipo, así que el <c>CHECK</c> rechazaría la primera
    /// fila que ya estuviera. Todo tramo existente es de venta, que es lo que ha sido siempre: hasta
    /// hoy no había otra clase de tarifa, y la única que un tercero podía tener asignada era la que
    /// le propone precios de venta.
    /// </para>
    /// <para>
    /// <b>La divisa se traduce leyendo <c>organizacion.divisas</c>, una vez y solo aquí.</b> Es la
    /// única lectura entre esquemas de las migraciones, y no rompe la frontera que la regla prohíbe
    /// en las consultas: no la hace el módulo en marcha sino el despliegue, que aplica las
    /// migraciones de Organización antes que las de Catálogo. Si no hay tarifas, no se lee nada, y
    /// una base que solo tuviera Catálogo migra igual. Si las hay y Organización no está, o alguna
    /// apunta a una divisa que no existe, <b>la migración se para</b> diciendo cuáles: sin el código
    /// no hay nada que guardar en su lugar, y la transacción de la migración deja la tabla como
    /// estaba, con su <c>divisa_id</c>.
    /// </para>
    /// <para>
    /// <b>Cada parada lleva el código de lo que le pasa, y ninguno es transitorio</b>:
    /// <c>undefined_table</c> si falta Organización, y <c>foreign_key_violation</c> si un tramo apunta
    /// a una divisa que no está, que es lo que habría dicho una clave ajena. No
    /// <c>object_not_in_prerequisite_state</c>, que era el primero que pedía el nombre: Npgsql lo
    /// cuenta como transitorio, y la estrategia de ejecución de EF Core lo envuelve en un error que
    /// dice «probablemente un fallo transitorio» y propone activar los reintentos, que repetirían una
    /// parada que no se arregla sola (ADR-0076).
    /// </para>
    /// <para>
    /// <b>El tipo va con el código</b>, y lo impide <c>tarifas_un_tipo_por_codigo</c>, el gemelo de
    /// <c>tarifas_sin_tramos_solapados</c>: dos filas de la misma empresa y el mismo código no pueden
    /// tener tipos distintos. Necesita <c>btree_gist</c>, que puso la migración de las tarifas.
    /// </para>
    /// </remarks>
    public partial class ElTipoYLaDivisaDeLaTarifa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo",
                schema: "catalogo",
                table: "tarifas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("UPDATE catalogo.tarifas SET tipo = 'Venta'");

            migrationBuilder.Sql("ALTER TABLE catalogo.tarifas ALTER COLUMN tipo SET NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tarifas_tipo",
                schema: "catalogo",
                table: "tarifas",
                sql: "tipo IN ('Venta', 'Compra')");

            migrationBuilder.AddColumn<string>(
                name: "divisa",
                schema: "catalogo",
                table: "tarifas",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    sin_divisa text;
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM catalogo.tarifas) THEN
                        RETURN;
                    END IF;

                    IF to_regclass('organizacion.divisas') IS NULL THEN
                        RAISE EXCEPTION
                            'catalogo.tarifas tiene tramos, y su divisa solo se traduce a su código con organizacion.divisas, que no está en esta base. Aplique antes las migraciones de Organización.'
                            USING ERRCODE = 'undefined_table';
                    END IF;

                    UPDATE catalogo.tarifas AS t
                       SET divisa = d.codigo
                      FROM organizacion.divisas AS d
                     WHERE d.id = t.divisa_id;

                    SELECT string_agg(t.id::text || ' (divisa ' || t.divisa_id::text || ')', ', ' ORDER BY t.id)
                      INTO sin_divisa
                      FROM catalogo.tarifas AS t
                     WHERE t.divisa IS NULL;

                    IF sin_divisa IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Estos tramos de tarifa apuntan a una divisa que no está en organizacion.divisas, y sin ella no hay código que guardar: %. Dé de alta esas divisas o corrija esos tramos, y vuelva a migrar.',
                            sin_divisa
                            USING ERRCODE = 'foreign_key_violation';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql("ALTER TABLE catalogo.tarifas ALTER COLUMN divisa SET NOT NULL");

            migrationBuilder.DropColumn(
                name: "divisa_id",
                schema: "catalogo",
                table: "tarifas");

            migrationBuilder.Sql(
                """
                ALTER TABLE catalogo.tarifas
                    ADD CONSTRAINT tarifas_un_tipo_por_codigo
                    EXCLUDE USING gist (
                        empresa_id WITH =,
                        codigo WITH =,
                        tipo WITH <>
                    );
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// La vuelta, con la misma lectura y la misma parada: el código vuelve a ser el
        /// identificador de su divisa, y una tarifa cuyo código no esté en Organización no se queda
        /// sin divisa en silencio. El tipo se pierde, porque el esquema de antes no lo tiene.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE catalogo.tarifas DROP CONSTRAINT tarifas_un_tipo_por_codigo");

            migrationBuilder.AddColumn<Guid>(
                name: "divisa_id",
                schema: "catalogo",
                table: "tarifas",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    sin_divisa text;
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM catalogo.tarifas) THEN
                        RETURN;
                    END IF;

                    IF to_regclass('organizacion.divisas') IS NULL THEN
                        RAISE EXCEPTION
                            'catalogo.tarifas tiene tramos, y su divisa solo vuelve a su identificador con organizacion.divisas, que no está en esta base.'
                            USING ERRCODE = 'undefined_table';
                    END IF;

                    UPDATE catalogo.tarifas AS t
                       SET divisa_id = d.id
                      FROM organizacion.divisas AS d
                     WHERE d.codigo = t.divisa;

                    SELECT string_agg(t.id::text || ' (divisa ' || t.divisa || ')', ', ' ORDER BY t.id)
                      INTO sin_divisa
                      FROM catalogo.tarifas AS t
                     WHERE t.divisa_id IS NULL;

                    IF sin_divisa IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Estos tramos de tarifa tienen una divisa que no está en organizacion.divisas, y sin ella no hay identificador que guardar: %.',
                            sin_divisa
                            USING ERRCODE = 'foreign_key_violation';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql("ALTER TABLE catalogo.tarifas ALTER COLUMN divisa_id SET NOT NULL");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tarifas_tipo",
                schema: "catalogo",
                table: "tarifas");

            migrationBuilder.DropColumn(
                name: "divisa",
                schema: "catalogo",
                table: "tarifas");

            migrationBuilder.DropColumn(
                name: "tipo",
                schema: "catalogo",
                table: "tarifas");
        }
    }
}
