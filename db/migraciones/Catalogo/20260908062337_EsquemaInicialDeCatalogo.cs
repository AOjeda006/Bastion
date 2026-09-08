using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Catalogo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicialDeCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalogo");

            migrationBuilder.CreateTable(
                name: "categorias",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias", x => x.id);
                    table.CheckConstraint("ck_categorias_padre_distinto_de_si_misma", "padre_id IS NULL OR padre_id <> id");
                    table.ForeignKey(
                        name: "fk_categorias_categorias_padre_id",
                        column: x => x.padre_id,
                        principalSchema: "catalogo",
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "articulos",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unidad_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    impuesto_por_defecto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_articulos", x => x.id);
                    table.CheckConstraint("ck_articulos_tipo", "tipo IN ('Bien', 'Servicio')");
                    table.ForeignKey(
                        name: "fk_articulos_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalSchema: "catalogo",
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_articulos_categoria_id",
                schema: "catalogo",
                table: "articulos",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_articulos_empresa_id_categoria_id",
                schema: "catalogo",
                table: "articulos",
                columns: new[] { "empresa_id", "categoria_id" });

            migrationBuilder.CreateIndex(
                name: "ix_articulos_empresa_id_codigo",
                schema: "catalogo",
                table: "articulos",
                columns: new[] { "empresa_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categorias_empresa_id_codigo",
                schema: "catalogo",
                table: "categorias",
                columns: new[] { "empresa_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categorias_empresa_id_padre_id",
                schema: "catalogo",
                table: "categorias",
                columns: new[] { "empresa_id", "padre_id" });

            migrationBuilder.CreateIndex(
                name: "ix_categorias_padre_id",
                schema: "catalogo",
                table: "categorias",
                column: "padre_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "articulos",
                schema: "catalogo");

            migrationBuilder.DropTable(
                name: "categorias",
                schema: "catalogo");
        }
    }
}
