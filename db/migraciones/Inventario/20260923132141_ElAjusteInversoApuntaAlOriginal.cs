using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El ajuste inverso guarda a qué documento compensa, y el motor sostiene que ese documento
    /// existe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Una sola columna, y va en el inverso.</b> El par se podría escribir dos veces —«a quién
    /// anulo» aquí y «quién me anula» en el original— y entonces habría dos sitios para el mismo
    /// hecho, que es como se llega a que discrepen. Con esta columna y el estado del original la
    /// flecha se recorre en los dos sentidos: hacia el original por el identificador, y desde el
    /// original por el índice, buscando quién le apunta.
    /// </para>
    /// <para>
    /// <b>Admite nulos porque casi ningún ajuste es un inverso</b>, y un nulo aquí no es un dato
    /// que falte: dice exactamente lo que hay que decir de un documento normal. Por eso esta
    /// migración no toca ninguna fila existente — las que ya están nacen con la columna nula, que
    /// es la verdad sobre ellas.
    /// </para>
    /// <para>
    /// <b>Clave ajena sí, y es la primera de este módulo.</b> Apunta a la misma tabla y al mismo
    /// esquema, así que no cruza ninguna frontera (§5, regla 4): lo que impedía las claves ajenas
    /// de <c>empresa_id</c>, <c>serie_id</c>, <c>almacen_id</c> y <c>articulo_id</c> era el esquema
    /// ajeno, no un desprecio por la integridad referencial. Y va <c>RESTRICT</c>, no
    /// <c>CASCADE</c> (ADR-0007 §8): borrar el original no puede llevarse por delante al documento
    /// que lo compensa.
    /// </para>
    /// <para>
    /// <b>El índice nace NO único, y eso lo corrige <c>ElInversoEsUnicoEnLaBase</c>.</b> Aquí
    /// hubo escrito que era una decisión medida: que un único sobre <c>anula_a_id</c> saltaría
    /// antes que el testigo de concurrencia y convertiría el <c>412</c> de quien pierde la carrera
    /// en un <c>500</c>. La medición era buena —el <c>INSERT</c> del inverso llega antes que el
    /// <c>UPDATE</c> del original— y la conclusión no: renunciaba a la garantía para conservar un
    /// código de estado, pudiendo tener las dos. La migración siguiente pone el único y el borde
    /// traduce el índice por su nombre. Se deja escrito aquí porque la fila del historial de
    /// migraciones ya no se puede cambiar, y porque el razonamiento equivocado es lo único que
    /// explica por qué hicieron falta dos migraciones para un índice.
    /// </para>
    /// <para>
    /// <b>Lo que el motor no puede decir.</b> Que la fila apuntada esté <c>Anulado</c>, que no haya
    /// dos inversos del mismo original, y que ningún anulado se quede sin nadie que le apunte: las
    /// tres son afirmaciones sobre parejas de filas y sobre un estado, y las sostiene el barrido de
    /// <c>LaDobleFlechaDeLaAnulacionTests</c> en los dos sentidos.
    /// </para>
    /// </remarks>
    public partial class ElAjusteInversoApuntaAlOriginal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "anula_a_id",
                schema: "inventario",
                table: "ajustes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_anula_a_id",
                schema: "inventario",
                table: "ajustes",
                column: "anula_a_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ajustes_ajustes_anula_a_id",
                schema: "inventario",
                table: "ajustes",
                column: "anula_a_id",
                principalSchema: "inventario",
                principalTable: "ajustes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ajustes_ajustes_anula_a_id",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.DropIndex(
                name: "ix_ajustes_anula_a_id",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.DropColumn(
                name: "anula_a_id",
                schema: "inventario",
                table: "ajustes");
        }
    }
}
