using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Terceros.Infrastructure.Migrations
{
    /// <summary>
    /// El tercero, con una tarifa por rol: la de venta, que es la que tenía, y la de compra, nueva.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La de venta es <c>tarifa_asignada_id</c> renombrada, sin tocar una fila</b> (3.3). Toda
    /// tarifa de antes es de venta —la migración de Catálogo del mismo ítem se lo pone a cada tramo
    /// (ADR-0076)—, así que toda asignación de antes es la de venta del tercero, sea cliente o no.
    /// Que la de venta pida un cliente lo mira cada asignación nueva, y un cliente que deja de serlo
    /// conserva la suya.
    /// </para>
    /// <para>
    /// <b>Un renombrado y no un alta con su copia.</b> <c>RenameColumn</c> es un
    /// <c>ALTER TABLE … RENAME COLUMN</c>, que no reescribe la tabla ni lee una fila; soltar la
    /// columna y crear otra perdería cada asignación, con las filas intactas, y el recorrido de
    /// <c>LasMigracionesSobreTablasConFilasTests</c> no lo vería, porque cuenta filas y no valores.
    /// Lo ve <c>LaTarifaDelTerceroSeConservaAlMigrarTests</c>.
    /// </para>
    /// <para>
    /// <b>La de compra entra nula</b>: no había tarifas de compra que asignar. Sin clave ajena,
    /// como la de venta: apunta a <c>catalogo.tarifas</c>, en otro esquema (§5, regla 4).
    /// </para>
    /// </remarks>
    public partial class LaTarifaDeVentaYLaDeCompraDelTercero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "tarifa_asignada_id",
                schema: "terceros",
                table: "terceros",
                newName: "tarifa_de_venta_id");

            migrationBuilder.AddColumn<Guid>(
                name: "tarifa_de_compra_id",
                schema: "terceros",
                table: "terceros",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// La vuelta devuelve la de venta a su nombre de antes, con su valor, y pierde la de compra,
        /// porque el esquema de antes no la tiene: es lo mismo que pierde el tipo de la tarifa al
        /// deshacer la migración de Catálogo (ADR-0076).
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tarifa_de_compra_id",
                schema: "terceros",
                table: "terceros");

            migrationBuilder.RenameColumn(
                name: "tarifa_de_venta_id",
                schema: "terceros",
                table: "terceros",
                newName: "tarifa_asignada_id");
        }
    }
}
