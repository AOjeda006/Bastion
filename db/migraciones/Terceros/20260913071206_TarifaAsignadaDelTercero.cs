using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Terceros.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TarifaAsignadaDelTercero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La otra mitad del cruce mutuo del ítem 1.10, y la misma ausencia: `tarifa_asignada_id`
            // apunta a `catalogo.tarifas` y NO lleva clave ajena, porque entre esquemas no se cruza
            // (§5, regla 4). Quien impide que ahí acabe una tarifa inventada, de otra empresa o
            // caducada es `IConsultaDeTarifas`, preguntado antes de escribir.
            //
            // Anulable, y eso sí es una decisión de dominio: no tener tarifa asignada es el estado
            // normal de la mayoría de los terceros —se les aplica la general— y no un dato que
            // falte.
            migrationBuilder.AddColumn<Guid>(
                name: "tarifa_asignada_id",
                schema: "terceros",
                table: "terceros",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tarifa_asignada_id",
                schema: "terceros",
                table: "terceros");
        }
    }
}
