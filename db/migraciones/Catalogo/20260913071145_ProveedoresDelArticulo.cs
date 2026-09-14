using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Catalogo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProveedoresDelArticulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------------------------
            // LA CLAVE AJENA QUE NO ESTÁ, y hay que leerla antes de «arreglarla».
            //
            // `tercero_id` apunta a `terceros.terceros` y NO lleva `ForeignKey`. No es un olvido
            // ni una optimización: es la regla 4 del §5 —ninguna consulta cruza esquemas— y la
            // frontera entre módulos llevada hasta abajo. Una clave ajena aquí ataría los dos
            // módulos por debajo de la capa que el compilador vigila: el orden de las migraciones
            // pasaría a importar, ningún módulo se podría desplegar sin el otro, y la frontera
            // quedaría abierta justo por donde nadie mira.
            //
            // Lo que ocupa su sitio es `IConsultaDeTerceros`, preguntado ANTES de escribir
            // (ADR-0024). Si alguien añade la clave ajena aquí, lo que se rompe no es esta tabla:
            // es la posibilidad de que estos dos módulos vivan en dos bases distintas.
            // ---------------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "articulos_proveedor",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tercero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia_del_proveedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_articulos_proveedor", x => x.id);
                    table.ForeignKey(
                        name: "fk_articulos_proveedor_articulos_articulo_id",
                        column: x => x.articulo_id,
                        principalSchema: "catalogo",
                        principalTable: "articulos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_articulos_proveedor_articulo_id",
                schema: "catalogo",
                table: "articulos_proveedor",
                column: "articulo_id");

            migrationBuilder.CreateIndex(
                name: "ix_articulos_proveedor_empresa_id_articulo_id_tercero_id",
                schema: "catalogo",
                table: "articulos_proveedor",
                columns: new[] { "empresa_id", "articulo_id", "tercero_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_articulos_proveedor_empresa_id_tercero_id",
                schema: "catalogo",
                table: "articulos_proveedor",
                columns: new[] { "empresa_id", "tercero_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "articulos_proveedor",
                schema: "catalogo");
        }
    }
}
