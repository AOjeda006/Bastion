using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ElDescarteDeLaTransferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transferencias_empresa_id_fecha_de_envio",
                schema: "inventario",
                table: "transferencias");

            migrationBuilder.DropIndex(
                name: "ix_transferencias_empresa_id_fecha_de_recepcion",
                schema: "inventario",
                table: "transferencias");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "descartado_en",
                schema: "inventario",
                table: "transferencias",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_empresa_id_fecha_de_envio",
                schema: "inventario",
                table: "transferencias",
                columns: new[] { "empresa_id", "fecha_de_envio" },
                filter: "descartado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_empresa_id_fecha_de_recepcion",
                schema: "inventario",
                table: "transferencias",
                columns: new[] { "empresa_id", "fecha_de_recepcion" },
                filter: "descartado_en IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transferencias_empresa_id_fecha_de_envio",
                schema: "inventario",
                table: "transferencias");

            migrationBuilder.DropIndex(
                name: "ix_transferencias_empresa_id_fecha_de_recepcion",
                schema: "inventario",
                table: "transferencias");

            migrationBuilder.DropColumn(
                name: "descartado_en",
                schema: "inventario",
                table: "transferencias");

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
        }
    }
}
