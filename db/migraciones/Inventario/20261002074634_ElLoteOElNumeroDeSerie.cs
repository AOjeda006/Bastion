using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El lote o el número de serie, nunca los dos, también en el motor: en el libro y en la
    /// existencia (ADR-0050 §3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Se añade validando</b>, y en el libro eso recorre todas las particiones con el cerrojo de
    /// la tabla tomado. Mientras el libro sea pequeño, es lo sencillo. Cuando no lo sea, el camino
    /// es <c>NOT VALID</c> y después <c>VALIDATE CONSTRAINT</c>, que PostgreSQL 17 admite para un
    /// <c>CHECK</c> sobre una tabla particionada.
    /// </para>
    /// <para>
    /// <b>Ninguna fila de antes lo incumple</b>: el dominio no ha dejado escribir nunca las dos
    /// marcas a la vez. Si una lo hiciera, la migración fallaría con el nombre del <c>CHECK</c>, y
    /// eso es lo que tiene que pasar, porque esa fila sería un defecto que nadie ha visto.
    /// </para>
    /// </remarks>
    public partial class ElLoteOElNumeroDeSerie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_movimiento_stock_lote_o_numero_de_serie",
                schema: "inventario",
                table: "movimiento_stock",
                sql: "num_nonnulls(lote_id, numero_de_serie_id) <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_existencias_lote_o_numero_de_serie",
                schema: "inventario",
                table: "existencias",
                sql: "num_nonnulls(lote_id, numero_de_serie_id) <= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_movimiento_stock_lote_o_numero_de_serie",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropCheckConstraint(
                name: "ck_existencias_lote_o_numero_de_serie",
                schema: "inventario",
                table: "existencias");
        }
    }
}
