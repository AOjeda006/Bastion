using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Catalogo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LaRetiradaDelArticulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "retirada",
                schema: "catalogo",
                table: "articulos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "retirada",
                schema: "catalogo",
                table: "articulos");
        }
    }
}
