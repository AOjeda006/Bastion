using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Auditoria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CaducidadDeLosRecibos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // En tres pasos y no con el valor por omisión que genera la herramienta. Ese valor es el
            // año 1: todos los recibos que ya existieran estarían vencidos, y la primera purga se
            // llevaría también los de hace un minuto, cuyo cliente todavía puede reintentar. Cada
            // recibo existente caduca a las 24 horas de haberse reclamado, que es lo que habría
            // tenido si el plazo hubiera existido cuando nació (ADR-0034 §4).
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "caduca_en",
                schema: "auditoria",
                table: "claves_de_idempotencia",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE auditoria.claves_de_idempotencia SET caduca_en = creada_en + interval '24 hours';");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "caduca_en",
                schema: "auditoria",
                table: "claves_de_idempotencia",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_claves_de_idempotencia_caduca_en",
                schema: "auditoria",
                table: "claves_de_idempotencia",
                column: "caduca_en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_claves_de_idempotencia_caduca_en",
                schema: "auditoria",
                table: "claves_de_idempotencia");

            migrationBuilder.DropColumn(
                name: "caduca_en",
                schema: "auditoria",
                table: "claves_de_idempotencia");
        }
    }
}
