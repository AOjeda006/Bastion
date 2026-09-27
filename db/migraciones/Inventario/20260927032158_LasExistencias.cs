using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// La proyección del libro: la fila viva de cada existencia, sus instantáneas mensuales y el
    /// corte de cada empresa (ADR-0044).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las tres tablas salen del andamiaje tal cual</b>, y las dos cosas que no son lo de siempre
    /// las sabe decir EF Core: la columna generada del disponible y el índice único que no
    /// distingue nulos. Ninguna de las dos pide un <c>Sql()</c>.
    /// </para>
    /// <para>
    /// <b>Lo que sí lleva <c>Sql()</c> es el relleno</b>: la fila viva de cada clave que ya tuviera
    /// movimientos, con la suma de todos ellos. Sin él, una base que ya tuviera libro arrancaría
    /// con el libro diciendo una cosa y la proyección otra, y el cuadre lo denunciaría en la primera
    /// pasada. Suma el libro <b>entero</b> y no solo hasta hoy, porque es lo que habría escrito la
    /// sentencia que anota el libro si hubiera existido: una fila con fecha futura de antes de esta
    /// migración sale en el cuadre, que es donde tiene que salir.
    /// </para>
    /// <para>
    /// <b>Los identificadores del relleno son UUID v4</b>, de <c>gen_random_uuid()</c>, porque
    /// PostgreSQL 17 no sabe generar un v7 y la fila no pasa por el cliente. Son los únicos: los de
    /// después los pone la aplicación. Y el relleno no crea ninguna instantánea, porque ninguna
    /// empresa tiene corte todavía.
    /// </para>
    /// </remarks>
    public partial class LasExistencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cortes_de_la_instantanea",
                schema: "inventario",
                columns: table => new
                {
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hasta_el_mes = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cortes_de_la_instantanea", x => x.empresa_id);
                    table.CheckConstraint("ck_cortes_de_la_instantanea_mes_es_primer_dia", "extract(day from hasta_el_mes) = 1");
                });

            migrationBuilder.CreateTable(
                name: "existencias",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ubicacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fisico = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    reservado = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    disponible = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, computedColumnSql: "fisico - reservado", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_existencias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "instantaneas_mensuales",
                schema: "inventario",
                columns: table => new
                {
                    existencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mes = table.Column<DateOnly>(type: "date", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fisico = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instantaneas_mensuales", x => new { x.existencia_id, x.mes });
                    table.CheckConstraint("ck_instantaneas_mensuales_mes_es_primer_dia", "extract(day from mes) = 1");
                    table.ForeignKey(
                        name: "fk_instantaneas_mensuales_existencias_existencia_id",
                        column: x => x.existencia_id,
                        principalSchema: "inventario",
                        principalTable: "existencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_existencias_una_por_clave",
                schema: "inventario",
                table: "existencias",
                columns: new[] { "empresa_id", "articulo_id", "almacen_id", "ubicacion_id", "lote_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_instantaneas_mensuales_empresa_id",
                schema: "inventario",
                table: "instantaneas_mensuales",
                column: "empresa_id");

            migrationBuilder.Sql(
                """
                INSERT INTO inventario.existencias
                    (id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id, fisico, reservado)
                SELECT gen_random_uuid(), m.empresa_id, m.articulo_id, m.almacen_id, m.ubicacion_id,
                       NULL, sum(m.cantidad_en_unidad_base), 0
                FROM inventario.movimiento_stock AS m
                GROUP BY m.empresa_id, m.articulo_id, m.almacen_id, m.ubicacion_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cortes_de_la_instantanea",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "instantaneas_mensuales",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "existencias",
                schema: "inventario");
        }
    }
}
