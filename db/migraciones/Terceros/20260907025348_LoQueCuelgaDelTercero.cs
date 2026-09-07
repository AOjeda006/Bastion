using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Terceros.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoQueCuelgaDelTercero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "criterio_de_caja",
                schema: "terceros",
                table: "terceros",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "limite_credito_cantidad",
                schema: "terceros",
                table: "terceros",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "limite_credito_divisa",
                schema: "terceros",
                table: "terceros",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "recargo_de_equivalencia",
                schema: "terceros",
                table: "terceros",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sujeto_a_retencion_irpf",
                schema: "terceros",
                table: "terceros",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "territorio_fiscal",
                schema: "terceros",
                table: "terceros",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "condiciones_pago",
                schema: "terceros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tercero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dias_de_plazo = table.Column<int>(type: "integer", nullable: false),
                    dia_de_pago_fijo = table.Column<int>(type: "integer", nullable: true),
                    descuento_por_pronto_pago = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condiciones_pago", x => x.id);
                    table.CheckConstraint("ck_condiciones_pago_plazo_legal", "dias_de_plazo BETWEEN 0 AND 60");
                    table.ForeignKey(
                        name: "fk_condiciones_pago_terceros_tercero_id",
                        column: x => x.tercero_id,
                        principalSchema: "terceros",
                        principalTable: "terceros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contactos",
                schema: "terceros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tercero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cargo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contactos", x => x.id);
                    table.ForeignKey(
                        name: "fk_contactos_terceros_tercero_id",
                        column: x => x.tercero_id,
                        principalSchema: "terceros",
                        principalTable: "terceros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cuentas_bancarias",
                schema: "terceros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tercero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    bic = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    alias = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    es_preferente = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuentas_bancarias", x => x.id);
                    table.ForeignKey(
                        name: "fk_cuentas_bancarias_terceros_tercero_id",
                        column: x => x.tercero_id,
                        principalSchema: "terceros",
                        principalTable: "terceros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // AÑADIR, RELLENAR Y CERRAR, en tres pasos y no en uno.
            //
            // El andamiaje genera `ADD COLUMN ... NOT NULL DEFAULT x` para poder rellenar las
            // filas que ya existan, y eso deja el DEFAULT puesto en la tabla PARA SIEMPRE: una
            // forma nueva de valor generado por el servidor en un modelo donde lo único que lo
            // genera son los testigos de concurrencia del ADR-0015, y una diferencia entre el
            // esquema y el modelo que ningún barrido sobre el modelo puede ver. Lo comprueba
            // `Lo_que_toda_fila_tiene_que_llevar_es_NOT_NULL_y_sin_DEFAULT`.
            //
            // El valor de relleno es el régimen común, que es lo que tienen las fichas que se
            // dieron de alta antes de que existiera el campo: si alguna tributa en Canarias, eso
            // ya era verdad y nadie lo sabía — no lo inventa esta migración.
            migrationBuilder.Sql(
                """
                UPDATE terceros.terceros
                   SET territorio_fiscal       = 'PeninsulaYBaleares',
                       recargo_de_equivalencia = FALSE,
                       criterio_de_caja        = FALSE,
                       sujeto_a_retencion_irpf = FALSE
                 WHERE territorio_fiscal IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "territorio_fiscal",
                schema: "terceros",
                table: "terceros",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false);

            migrationBuilder.AlterColumn<bool>(
                name: "recargo_de_equivalencia",
                schema: "terceros",
                table: "terceros",
                type: "boolean",
                nullable: false);

            migrationBuilder.AlterColumn<bool>(
                name: "criterio_de_caja",
                schema: "terceros",
                table: "terceros",
                type: "boolean",
                nullable: false);

            migrationBuilder.AlterColumn<bool>(
                name: "sujeto_a_retencion_irpf",
                schema: "terceros",
                table: "terceros",
                type: "boolean",
                nullable: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_terceros_limite_credito_completo",
                schema: "terceros",
                table: "terceros",
                sql: "(limite_credito_cantidad IS NULL) = (limite_credito_divisa IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_terceros_territorio_fiscal",
                schema: "terceros",
                table: "terceros",
                sql: "territorio_fiscal IN ('PeninsulaYBaleares', 'Canarias', 'CeutaYMelilla', 'UnionEuropea', 'TercerosPaises')");

            migrationBuilder.CreateIndex(
                name: "ix_condiciones_pago_tercero_id_rol",
                schema: "terceros",
                table: "condiciones_pago",
                columns: new[] { "tercero_id", "rol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contactos_tercero_id",
                schema: "terceros",
                table: "contactos",
                column: "tercero_id");

            migrationBuilder.CreateIndex(
                name: "ix_cuentas_bancarias_tercero_id_iban",
                schema: "terceros",
                table: "cuentas_bancarias",
                columns: new[] { "tercero_id", "iban" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cuentas_bancarias_una_preferente_por_tercero",
                schema: "terceros",
                table: "cuentas_bancarias",
                column: "tercero_id",
                unique: true,
                filter: "es_preferente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condiciones_pago",
                schema: "terceros");

            migrationBuilder.DropTable(
                name: "contactos",
                schema: "terceros");

            migrationBuilder.DropTable(
                name: "cuentas_bancarias",
                schema: "terceros");

            migrationBuilder.DropCheckConstraint(
                name: "ck_terceros_limite_credito_completo",
                schema: "terceros",
                table: "terceros");

            migrationBuilder.DropCheckConstraint(
                name: "ck_terceros_territorio_fiscal",
                schema: "terceros",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "criterio_de_caja",
                schema: "terceros",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "limite_credito_cantidad",
                schema: "terceros",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "limite_credito_divisa",
                schema: "terceros",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "recargo_de_equivalencia",
                schema: "terceros",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "sujeto_a_retencion_irpf",
                schema: "terceros",
                table: "terceros");

            migrationBuilder.DropColumn(
                name: "territorio_fiscal",
                schema: "terceros",
                table: "terceros");
        }
    }
}
