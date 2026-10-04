using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// La transferencia: sus dos tablas, lo que vuela en la existencia y en la valoración, y la serie
    /// que cuenta también mientras viaja (ADR-0053 §1 y §7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las filas de antes no tienen nada en vuelo</b>, porque antes no había transferencias. Las
    /// tres columnas entran con un valor por omisión de cero, que PostgreSQL guarda en el catálogo
    /// sin reescribir ninguna fila, y se quita después, como en la de la valoración: una fila nueva
    /// que llegara sin decir lo que vuela no entraría.
    /// </para>
    /// <para>
    /// <b>La serie cambia de índice y de <c>CHECK</c></b>, y los dos se quitan antes de añadir la
    /// columna que van a nombrar. Con todo el tránsito a cero, las expresiones nuevas dicen lo mismo
    /// que las de antes sobre cada fila, así que ninguna puede fallar al crearse.
    /// </para>
    /// </remarks>
    public partial class LaTransferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_existencias_numero_de_serie_en_un_sitio",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropCheckConstraint(
                name: "ck_existencias_numero_de_serie_como_mucho_una",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.AddColumn<decimal>(
                name: "en_transito",
                schema: "inventario",
                table: "valoraciones",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_en_transito",
                schema: "inventario",
                table: "valoraciones",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "en_transito",
                schema: "inventario",
                table: "existencias",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "ALTER TABLE inventario.valoraciones ALTER COLUMN en_transito DROP DEFAULT");

            migrationBuilder.Sql(
                "ALTER TABLE inventario.valoraciones ALTER COLUMN valor_en_transito DROP DEFAULT");

            migrationBuilder.Sql(
                "ALTER TABLE inventario.existencias ALTER COLUMN en_transito DROP DEFAULT");

            migrationBuilder.CreateTable(
                name: "transferencias",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<long>(type: "bigint", nullable: true),
                    almacen_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_de_envio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_de_recepcion = table.Column<DateOnly>(type: "date", nullable: true),
                    divisa = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    anula_a_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transferencias", x => x.id);
                    table.CheckConstraint("ck_transferencias_origen_distinto_del_destino", "almacen_origen_id <> almacen_destino_id");
                    table.CheckConstraint("ck_transferencias_recepcion_no_antes_del_envio", "fecha_de_recepcion IS NULL OR fecha_de_recepcion >= fecha_de_envio");
                    table.ForeignKey(
                        name: "fk_transferencias_transferencias_anula_a_id",
                        column: x => x.anula_a_id,
                        principalSchema: "inventario",
                        principalTable: "transferencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lineas_transferencia",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    ubicacion_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ubicacion_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_introducida = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unidad_introducida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor_a_unidad_base = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    codigo_de_lote = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero_de_serie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    valor_que_compensa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lineas_transferencia", x => x.id);
                    table.CheckConstraint("ck_lineas_transferencia_cantidad_y_factor", "cantidad_introducida <> 0 AND factor_a_unidad_base > 0");
                    table.CheckConstraint("ck_lineas_transferencia_numero_desde_uno", "numero > 0");
                    table.ForeignKey(
                        name: "fk_lineas_transferencia_transferencias_transferencia_id",
                        column: x => x.transferencia_id,
                        principalSchema: "inventario",
                        principalTable: "transferencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_valoraciones_en_transito_no_negativo",
                schema: "inventario",
                table: "valoraciones",
                sql: "en_transito >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_valoraciones_sin_transito_no_hay_valor_en_transito",
                schema: "inventario",
                table: "valoraciones",
                sql: "en_transito > 0 OR valor_en_transito = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_valoraciones_valor_en_transito_no_negativo",
                schema: "inventario",
                table: "valoraciones",
                sql: "valor_en_transito >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_existencias_numero_de_serie_en_un_sitio",
                schema: "inventario",
                table: "existencias",
                columns: new[] { "empresa_id", "articulo_id", "numero_de_serie_id" },
                unique: true,
                filter: "(fisico > 0 OR en_transito > 0) AND numero_de_serie_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_existencias_en_transito_no_negativo",
                schema: "inventario",
                table: "existencias",
                sql: "en_transito >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_existencias_numero_de_serie_como_mucho_una",
                schema: "inventario",
                table: "existencias",
                sql: "numero_de_serie_id IS NULL OR fisico + en_transito <= 1");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_transferencia_transferencia_id_numero",
                schema: "inventario",
                table: "lineas_transferencia",
                columns: new[] { "transferencia_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_anula_a_id",
                schema: "inventario",
                table: "transferencias",
                column: "anula_a_id",
                unique: true,
                filter: "anula_a_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_empresa_id_fecha_de_envio",
                schema: "inventario",
                table: "transferencias",
                columns: new[] { "empresa_id", "fecha_de_envio" });

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_empresa_id_fecha_de_recepcion",
                schema: "inventario",
                table: "transferencias",
                columns: new[] { "empresa_id", "fecha_de_recepcion" });

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_serie_id_numero",
                schema: "inventario",
                table: "transferencias",
                columns: new[] { "serie_id", "numero" },
                unique: true,
                filter: "numero IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lineas_transferencia",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "transferencias",
                schema: "inventario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_valoraciones_en_transito_no_negativo",
                schema: "inventario",
                table: "valoraciones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_valoraciones_sin_transito_no_hay_valor_en_transito",
                schema: "inventario",
                table: "valoraciones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_valoraciones_valor_en_transito_no_negativo",
                schema: "inventario",
                table: "valoraciones");

            migrationBuilder.DropIndex(
                name: "ix_existencias_numero_de_serie_en_un_sitio",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropCheckConstraint(
                name: "ck_existencias_en_transito_no_negativo",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropCheckConstraint(
                name: "ck_existencias_numero_de_serie_como_mucho_una",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropColumn(
                name: "en_transito",
                schema: "inventario",
                table: "valoraciones");

            migrationBuilder.DropColumn(
                name: "valor_en_transito",
                schema: "inventario",
                table: "valoraciones");

            migrationBuilder.DropColumn(
                name: "en_transito",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.CreateIndex(
                name: "ix_existencias_numero_de_serie_en_un_sitio",
                schema: "inventario",
                table: "existencias",
                columns: new[] { "empresa_id", "articulo_id", "numero_de_serie_id" },
                unique: true,
                filter: "fisico > 0 AND numero_de_serie_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_existencias_numero_de_serie_como_mucho_una",
                schema: "inventario",
                table: "existencias",
                sql: "numero_de_serie_id IS NULL OR fisico <= 1");
        }
    }
}
