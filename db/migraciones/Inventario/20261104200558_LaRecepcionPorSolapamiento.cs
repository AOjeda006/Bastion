using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LaRecepcionPorSolapamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transferencias_recepcion_no_antes_del_envio",
                schema: "inventario",
                table: "transferencias");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transferencias_recepcion_no_antes_del_envio",
                schema: "inventario",
                table: "transferencias",
                sql: "fecha_de_recepcion IS NULL OR fecha_de_recepcion >= fecha_de_envio - 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transferencias_recepcion_no_antes_del_envio",
                schema: "inventario",
                table: "transferencias");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transferencias_recepcion_no_antes_del_envio",
                schema: "inventario",
                table: "transferencias",
                sql: "fecha_de_recepcion IS NULL OR fecha_de_recepcion >= fecha_de_envio");
        }
    }
}
