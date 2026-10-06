using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El recuento: sus dos tablas y las dos mitades de su doble flecha con el ajuste (ADR-0055 §8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No hay nada que rellenar</b>: antes no había recuentos, así que <c>ajustes.recuento_id</c>
    /// entra nula en todas las filas, que es lo que dice un ajuste dado de alta por una persona.
    /// </para>
    /// <para>
    /// <b>La clave ajena del ajuste al recuento va al final</b>, porque nombra una tabla que esta
    /// misma migración crea. Entre las dos tablas no hay ciclo: el recuento no apunta a su ajuste, y
    /// sus líneas apuntan a las del ajuste, que ya existían.
    /// </para>
    /// </remarks>
    public partial class ElRecuento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recuento_id",
                schema: "inventario",
                table: "ajustes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recuentos",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_del_ajuste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<long>(type: "bigint", nullable: true),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_de_apertura = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_de_confirmacion = table.Column<DateOnly>(type: "date", nullable: true),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    divisa = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    motivo_del_descarte = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    motivo_de_la_anulacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recuentos", x => x.id);
                    table.CheckConstraint("ck_recuentos_confirmacion_no_antes_de_la_apertura", "fecha_de_confirmacion IS NULL OR fecha_de_confirmacion >= fecha_de_apertura");
                    table.CheckConstraint("ck_recuentos_numerado_si_se_confirmo", "(numero IS NOT NULL AND fecha_de_confirmacion IS NOT NULL) = (estado IN ('Confirmado', 'Anulado'))");
                });

            migrationBuilder.CreateTable(
                name: "lineas_recuento",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recuento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    ubicacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_de_lote = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero_de_serie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    unidad_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    coste_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    contado = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    teorico_al_contar = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    linea_de_ajuste_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lineas_recuento", x => x.id);
                    table.CheckConstraint("ck_lineas_recuento_contado_con_su_teorico", "(contado IS NULL) = (teorico_al_contar IS NULL)");
                    table.CheckConstraint("ck_lineas_recuento_contado_no_negativo", "contado IS NULL OR contado >= 0");
                    table.CheckConstraint("ck_lineas_recuento_coste_solo_al_anadir", "coste_unitario IS NULL OR (origen = 'Anadida' AND coste_unitario >= 0)");
                    table.CheckConstraint("ck_lineas_recuento_lote_o_serie", "codigo_de_lote IS NULL OR numero_de_serie IS NULL");
                    table.CheckConstraint("ck_lineas_recuento_numero_desde_uno", "numero > 0");
                    table.CheckConstraint("ck_lineas_recuento_serie_en_cero_o_uno", "numero_de_serie IS NULL OR contado IS NULL OR contado IN (0, 1)");
                    table.ForeignKey(
                        name: "fk_lineas_recuento_lineas_ajuste_linea_de_ajuste_id",
                        column: x => x.linea_de_ajuste_id,
                        principalSchema: "inventario",
                        principalTable: "lineas_ajuste",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lineas_recuento_recuentos_recuento_id",
                        column: x => x.recuento_id,
                        principalSchema: "inventario",
                        principalTable: "recuentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_recuento_id",
                schema: "inventario",
                table: "ajustes",
                column: "recuento_id",
                unique: true,
                filter: "recuento_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_recuento_linea_de_ajuste_id",
                schema: "inventario",
                table: "lineas_recuento",
                column: "linea_de_ajuste_id",
                unique: true,
                filter: "linea_de_ajuste_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_recuento_recuento_id_numero",
                schema: "inventario",
                table: "lineas_recuento",
                columns: new[] { "recuento_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lineas_recuento_una_por_clave",
                schema: "inventario",
                table: "lineas_recuento",
                columns: new[] { "recuento_id", "articulo_id", "ubicacion_id", "codigo_de_lote", "numero_de_serie" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_lineas_recuento_una_por_serie",
                schema: "inventario",
                table: "lineas_recuento",
                columns: new[] { "recuento_id", "articulo_id", "numero_de_serie" },
                unique: true,
                filter: "numero_de_serie IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_recuentos_empresa_id_fecha_de_confirmacion",
                schema: "inventario",
                table: "recuentos",
                columns: new[] { "empresa_id", "fecha_de_confirmacion" });

            migrationBuilder.CreateIndex(
                name: "ix_recuentos_serie_id_numero",
                schema: "inventario",
                table: "recuentos",
                columns: new[] { "serie_id", "numero" },
                unique: true,
                filter: "numero IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_recuentos_uno_en_curso_por_almacen",
                schema: "inventario",
                table: "recuentos",
                columns: new[] { "empresa_id", "almacen_id" },
                unique: true,
                filter: "estado = 'EnCurso'");

            migrationBuilder.AddForeignKey(
                name: "fk_ajustes_recuentos_recuento_id",
                schema: "inventario",
                table: "ajustes",
                column: "recuento_id",
                principalSchema: "inventario",
                principalTable: "recuentos",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ajustes_recuentos_recuento_id",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.DropTable(
                name: "lineas_recuento",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "recuentos",
                schema: "inventario");

            migrationBuilder.DropIndex(
                name: "ix_ajustes_recuento_id",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.DropColumn(
                name: "recuento_id",
                schema: "inventario",
                table: "ajustes");
        }
    }
}
