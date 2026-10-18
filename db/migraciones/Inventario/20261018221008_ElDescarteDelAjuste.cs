using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ElDescarteDelAjuste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ajustes_empresa_id_fecha_de_operacion",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "descartado_en",
                schema: "inventario",
                table: "ajustes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_empresa_id_fecha_de_operacion",
                schema: "inventario",
                table: "ajustes",
                columns: new[] { "empresa_id", "fecha_de_operacion" },
                filter: "descartado_en IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ajustes_empresa_id_fecha_de_operacion",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.DropColumn(
                name: "descartado_en",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_empresa_id_fecha_de_operacion",
                schema: "inventario",
                table: "ajustes",
                columns: new[] { "empresa_id", "fecha_de_operacion" });
        }
    }
}
