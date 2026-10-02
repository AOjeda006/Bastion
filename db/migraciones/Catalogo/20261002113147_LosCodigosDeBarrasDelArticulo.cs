using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Catalogo.Infrastructure.Migrations
{
    /// <summary>
    /// Los códigos de barras del artículo: el GTIN de la base y de cada agrupación, con su nivel y sus
    /// unidades base (ADR-0051, con los nombres del ADR-0052).
    /// </summary>
    /// <remarks>
    /// <b>Una tabla nueva, así que no hay filas que rellenar.</b> El índice único
    /// <c>(empresa_id, gtin)</c> lleva nombre propio porque el borde lo traduce por su nombre a un
    /// <c>409</c>, y los tres <c>CHECK</c> repiten en el motor lo que el dominio ya garantiza.
    /// </remarks>
    public partial class LosCodigosDeBarrasDelArticulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "codigos_barras",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gtin = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unidades = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codigos_barras", x => x.id);
                    table.CheckConstraint("ck_codigos_barras_gtin_catorce_cifras", "gtin ~ '^[0-9]{14}$'");
                    table.CheckConstraint("ck_codigos_barras_nivel", "nivel IN ('Base', 'Caja', 'Palet')");
                    table.CheckConstraint("ck_codigos_barras_unidades_segun_el_nivel", "(nivel = 'Base' AND unidades = 1) OR (nivel <> 'Base' AND unidades >= 2)");
                    table.ForeignKey(
                        name: "fk_codigos_barras_articulos_articulo_id",
                        column: x => x.articulo_id,
                        principalSchema: "catalogo",
                        principalTable: "articulos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_codigos_barras_articulo_id",
                schema: "catalogo",
                table: "codigos_barras",
                column: "articulo_id");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_barras_gtin_uno_por_empresa",
                schema: "catalogo",
                table: "codigos_barras",
                columns: new[] { "empresa_id", "gtin" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codigos_barras",
                schema: "catalogo");
        }
    }
}
