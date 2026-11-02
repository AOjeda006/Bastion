using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Auditoria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LaCausaDeCadaEvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las tres, nulas y sin rellenar (3.4, hueco 3). De una fila escrita antes no se sabe
            // quién la causó ni en qué traza, y una correlación inventada juntaría su efecto con
            // cosas que no tienen nada que ver: el despachador le da una nueva al entregarla, y
            // esa sí es verdad, porque es la de lo que pasa a partir de ahí.
            migrationBuilder.AddColumn<string>(
                name: "contexto_de_traza",
                schema: "auditoria",
                table: "bandeja_de_salida",
                type: "character varying(55)",
                maxLength: 55,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correlacion_id",
                schema: "auditoria",
                table: "bandeja_de_salida",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "usuario_id",
                schema: "auditoria",
                table: "bandeja_de_salida",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contexto_de_traza",
                schema: "auditoria",
                table: "bandeja_de_salida");

            migrationBuilder.DropColumn(
                name: "correlacion_id",
                schema: "auditoria",
                table: "bandeja_de_salida");

            migrationBuilder.DropColumn(
                name: "usuario_id",
                schema: "auditoria",
                table: "bandeja_de_salida");
        }
    }
}
