using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Organizacion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetiradaDeLosMaestrosDeInstalacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "retirada",
                schema: "organizacion",
                table: "unidades_de_medida",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "retirada",
                schema: "organizacion",
                table: "tipos_de_cambio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "retirada",
                schema: "organizacion",
                table: "divisas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "retirada",
                schema: "organizacion",
                table: "conversiones_de_unidades",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "retirada",
                schema: "organizacion",
                table: "unidades_de_medida");

            migrationBuilder.DropColumn(
                name: "retirada",
                schema: "organizacion",
                table: "tipos_de_cambio");

            migrationBuilder.DropColumn(
                name: "retirada",
                schema: "organizacion",
                table: "divisas");

            migrationBuilder.DropColumn(
                name: "retirada",
                schema: "organizacion",
                table: "conversiones_de_unidades");
        }
    }
}
