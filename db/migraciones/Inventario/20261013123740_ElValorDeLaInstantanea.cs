using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El valor de la instantánea: la suma de la columna <c>valor</c> del libro de su existencia hasta
    /// el último día de su mes (ADR-0063 §1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las filas que ya hay se rellenan desde el libro</b>, con la misma clave y el mismo límite
    /// que su <c>fisico</c>. En producción no hay ninguna, porque ninguna empresa tiene corte todavía;
    /// el relleno está para que la migración diga la verdad en cualquier base, no para un caso que
    /// exista hoy.
    /// </para>
    /// <para>
    /// <b>La columna entra con cero por omisión y lo pierde al final</b>, como el valor del libro en
    /// <c>LaValoracion</c>: una instantánea nueva que llegara sin valor no entraría.
    /// </para>
    /// </remarks>
    public partial class ElValorDeLaInstantanea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "valor",
                schema: "inventario",
                table: "instantaneas_mensuales",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE inventario.instantaneas_mensuales AS i
                SET valor = coalesce((
                    SELECT sum(m.valor)
                    FROM inventario.movimiento_stock AS m
                    JOIN inventario.existencias AS e ON e.id = i.existencia_id
                    WHERE m.empresa_id = e.empresa_id
                        AND m.articulo_id = e.articulo_id
                        AND m.almacen_id = e.almacen_id
                        AND m.ubicacion_id = e.ubicacion_id
                        AND m.lote_id IS NOT DISTINCT FROM e.lote_id
                        AND m.numero_de_serie_id IS NOT DISTINCT FROM e.numero_de_serie_id
                        AND m.fecha_de_operacion < i.mes + interval '1 month'), 0)
                """);

            migrationBuilder.Sql(
                "ALTER TABLE inventario.instantaneas_mensuales ALTER COLUMN valor DROP DEFAULT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "valor",
                schema: "inventario",
                table: "instantaneas_mensuales");
        }
    }
}
