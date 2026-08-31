using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Identidad.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TestigosDeConcurrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identidad",
                table: "usuarios",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identidad",
                table: "roles",
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
                schema: "identidad",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identidad",
                table: "roles");
        }
    }
}
