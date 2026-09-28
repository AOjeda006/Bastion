using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El stock no baja de cero, y lo guarda el motor: la restricción que el borde traduce a
    /// <c>422</c> <c>stock-insuficiente</c> (ADR-0046 §4).
    /// </summary>
    /// <remarks>
    /// <b>No arregla nada de lo que ya hay.</b> Si una base de desarrollo tiene una existencia por
    /// debajo de cero, la migración falla con el nombre de la restricción, y se arregla rehaciendo
    /// la base o dándole la entrada que falta. Corregir el saldo aquí sería escribir una existencia
    /// que el libro no suma, y el cuadre la denunciaría en la primera pasada.
    /// </remarks>
    public partial class ElFisicoNoBajaDeCero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_existencias_fisico_no_negativo",
                schema: "inventario",
                table: "existencias",
                sql: "fisico >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_existencias_fisico_no_negativo",
                schema: "inventario",
                table: "existencias");
        }
    }
}
