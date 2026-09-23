using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// Un original no puede tener dos inversos, y quien lo impide pasa a ser el motor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Esto corrige a la migración anterior, que dejó el índice sin unicidad a propósito.</b>
    /// El argumento de entonces era una medición buena con una conclusión mal sacada: es cierto
    /// que anular INSERTA el inverso antes de tocar el original, y por tanto que con un índice
    /// único quien pierde la carrera choca contra él y no contra el testigo de concurrencia; de
    /// ahí se concluyó que el único convertiría un <c>412</c> en un <c>500</c> y se renunció a la
    /// garantía para conservar el código de estado. No hacía falta elegir: la garantía la da el
    /// índice y el código de estado lo arregla el borde, traduciendo ESTE índice por su nombre al
    /// mismo <c>412</c> con el mismo <c>version-obsoleta</c>
    /// (<c>ManejadorDeCarreraPerdidaEnLaBase</c>, declarado en <c>ModuloDeInventario</c>).
    /// </para>
    /// <para>
    /// <b>Y es lo que hace el resto de la casa.</b> Cuatro líneas más abajo, en la misma
    /// configuración, <c>(serie_id, numero)</c> es único y su comentario ya decía esto mismo: se
    /// acepta lo que le pase a la carrera a cambio de que la base no deje entrar el dato malo. Con
    /// el índice fuera, «un solo inverso» en producción dependía de que todos los caminos futuros
    /// —el ítem 2.12, la fase 5— tocaran también la fila del original, y de que nadie escribiera
    /// nunca uno que no lo hiciera. El barrido que lo vigila mira la base de los tests, no la de
    /// nadie más.
    /// </para>
    /// <para>
    /// <b>Filtrado por <c>anula_a_id IS NOT NULL</c></b>, porque en PostgreSQL los nulos no chocan
    /// entre sí y sin el filtro el índice cargaría con una fila por cada ajuste normal —que son
    /// casi todos— sin que ninguna de ellas pueda impedir nada. Con el filtro, el índice tiene
    /// exactamente tantas entradas como anulaciones hay.
    /// </para>
    /// <para>
    /// <b>Ninguna fila existente lo incumple, y se comprobó en vez de suponerlo.</b> Un recuento
    /// agrupado por <c>anula_a_id</c> sobre <c>bastion_dev</c> no encontró duplicados por una
    /// razón más fuerte que la suerte: la columna nace en la migración anterior, sin desplegar
    /// todavía en ninguna base —el historial de <c>bastion_dev</c> va por el esquema inicial— y la
    /// tabla está vacía. Aunque tuviera filas, el único camino que rellena esa columna es la
    /// anulación, que pasa por el testigo de concurrencia del original. Que la creación del índice
    /// aguante sobre una tabla CON filas no se deja al despliegue: lo ejerce
    /// <c>LasMigracionesSobreTablasConFilasTests</c>, que aplica cada migración una a una sobre
    /// datos inventados.
    /// </para>
    /// <para>
    /// <b>La vuelta atrás devuelve el índice no único</b>, no lo borra: la mitad que recorre la
    /// flecha al revés —del original a su inverso— lo necesitaba ya antes de esta migración.
    /// </para>
    /// </remarks>
    public partial class ElInversoEsUnicoEnLaBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ajustes_anula_a_id",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_anula_a_id",
                schema: "inventario",
                table: "ajustes",
                column: "anula_a_id",
                unique: true,
                filter: "anula_a_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ajustes_anula_a_id",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_anula_a_id",
                schema: "inventario",
                table: "ajustes",
                column: "anula_a_id");
        }
    }
}
