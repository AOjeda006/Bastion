using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El lote y el número de serie: sus dos tablas, y su sitio en la existencia, en el libro y en la
    /// línea del ajuste (ADR-0048).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No hay nada que rellenar.</b> Hoy no existe ningún lote ni ninguna serie: las columnas nuevas
    /// admiten nulo, que es lo que eran todas las filas, y añadir una columna que admite nulo y no
    /// tiene valor por omisión no reescribe la tabla. El libro no recibe ni una sentencia de cambio.
    /// </para>
    /// <para>
    /// <b>El índice de la clave se rehace con la serie dentro</b>, con los nulos contando como un
    /// valor, y entran el <c>CHECK</c> de la serie y su índice único parcial (ADR-0048 §3). Las filas
    /// de hoy los cumplen todas: ninguna lleva serie.
    /// </para>
    /// <para>
    /// <b>Las claves ajenas son del esquema</b>, de la existencia y del libro a las dos tablas nuevas.
    /// Sobre el libro, que está particionado, PostgreSQL la pone en el padre y la hereda cada partición.
    /// </para>
    /// </remarks>
    public partial class LosLotesYLasSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_existencias_una_por_clave",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.AddColumn<Guid>(
                name: "lote_id",
                schema: "inventario",
                table: "movimiento_stock",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "serie_id",
                schema: "inventario",
                table: "movimiento_stock",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "codigo_de_lote",
                schema: "inventario",
                table: "lineas_ajuste",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_de_serie",
                schema: "inventario",
                table: "lineas_ajuste",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "serie_id",
                schema: "inventario",
                table: "existencias",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lotes",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "numeros_de_serie",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_numeros_de_serie", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_stock_lote_id",
                schema: "inventario",
                table: "movimiento_stock",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_stock_serie_id",
                schema: "inventario",
                table: "movimiento_stock",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_existencias_lote_id",
                schema: "inventario",
                table: "existencias",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_existencias_serie_en_un_sitio",
                schema: "inventario",
                table: "existencias",
                columns: new[] { "empresa_id", "articulo_id", "serie_id" },
                unique: true,
                filter: "fisico > 0 AND serie_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_existencias_serie_id",
                schema: "inventario",
                table: "existencias",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "ix_existencias_una_por_clave",
                schema: "inventario",
                table: "existencias",
                columns: new[] { "empresa_id", "articulo_id", "almacen_id", "ubicacion_id", "lote_id", "serie_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_existencias_serie_como_mucho_una",
                schema: "inventario",
                table: "existencias",
                sql: "serie_id IS NULL OR fisico <= 1");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_empresa_id_articulo_id_codigo",
                schema: "inventario",
                table: "lotes",
                columns: new[] { "empresa_id", "articulo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_numeros_de_serie_empresa_id_articulo_id_numero",
                schema: "inventario",
                table: "numeros_de_serie",
                columns: new[] { "empresa_id", "articulo_id", "numero" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_existencias_lotes_lote_id",
                schema: "inventario",
                table: "existencias",
                column: "lote_id",
                principalSchema: "inventario",
                principalTable: "lotes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_existencias_numeros_de_serie_serie_id",
                schema: "inventario",
                table: "existencias",
                column: "serie_id",
                principalSchema: "inventario",
                principalTable: "numeros_de_serie",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_movimiento_stock_lotes_lote_id",
                schema: "inventario",
                table: "movimiento_stock",
                column: "lote_id",
                principalSchema: "inventario",
                principalTable: "lotes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_movimiento_stock_numeros_de_serie_serie_id",
                schema: "inventario",
                table: "movimiento_stock",
                column: "serie_id",
                principalSchema: "inventario",
                principalTable: "numeros_de_serie",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_existencias_lotes_lote_id",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropForeignKey(
                name: "fk_existencias_numeros_de_serie_serie_id",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropForeignKey(
                name: "fk_movimiento_stock_lotes_lote_id",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropForeignKey(
                name: "fk_movimiento_stock_numeros_de_serie_serie_id",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropTable(
                name: "lotes",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "numeros_de_serie",
                schema: "inventario");

            migrationBuilder.DropIndex(
                name: "ix_movimiento_stock_lote_id",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropIndex(
                name: "ix_movimiento_stock_serie_id",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropIndex(
                name: "ix_existencias_lote_id",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropIndex(
                name: "ix_existencias_serie_en_un_sitio",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropIndex(
                name: "ix_existencias_serie_id",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropIndex(
                name: "ix_existencias_una_por_clave",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropCheckConstraint(
                name: "ck_existencias_serie_como_mucho_una",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropColumn(
                name: "lote_id",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropColumn(
                name: "serie_id",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropColumn(
                name: "codigo_de_lote",
                schema: "inventario",
                table: "lineas_ajuste");

            migrationBuilder.DropColumn(
                name: "numero_de_serie",
                schema: "inventario",
                table: "lineas_ajuste");

            migrationBuilder.DropColumn(
                name: "serie_id",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.CreateIndex(
                name: "ix_existencias_una_por_clave",
                schema: "inventario",
                table: "existencias",
                columns: new[] { "empresa_id", "articulo_id", "almacen_id", "ubicacion_id", "lote_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
