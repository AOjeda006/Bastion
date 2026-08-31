using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Organizacion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TestigosDeConcurrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "organizacion",
                table: "series",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "organizacion",
                table: "empresas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "organizacion",
                table: "ejercicios",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "organizacion",
                table: "almacenes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "organizacion",
                table: "series");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "organizacion",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "organizacion",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "organizacion",
                table: "almacenes");
        }
    }
}
