using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// La valoración: su tabla, el valor y el precio medio de cada fila del libro, y el valor de cada
    /// línea de ajuste (ADR-0046 §5, §6 y §8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las filas del libro de antes valen cero, y el libro no recibe ni una sentencia de
    /// cambio.</b> Las dos columnas entran con un valor por omisión de cero, que PostgreSQL guarda en
    /// el catálogo sin reescribir ninguna fila, y el valor por omisión se quita después: una fila
    /// nueva que llegara sin valorar no entraría. No se revaloran, porque revalorar es reproducir la
    /// historia, y las salidas de antes no congelaron ningún precio.
    /// </para>
    /// <para>
    /// <b>Las valoraciones nacen de la existencia</b>: cada clave con existencias recibe la cantidad
    /// que suman sus ubicaciones, valor cero y la divisa de sus filas del libro, o el euro si no
    /// tuviera ninguna. Con cantidad y valor cero, el precio medio es cero, así que una entrada sin
    /// coste en una clave de antes se valora a cero, y no se rechaza.
    /// </para>
    /// <para>
    /// <b>Las líneas confirmadas reciben valor cero</b>, que es lo que valen sus filas, y las de un
    /// inverso, un valor que compensa de cero. Así, anular un ajuste de antes compensa cero y no
    /// necesita un camino propio.
    /// </para>
    /// </remarks>
    public partial class LaValoracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "precio_medio",
                schema: "inventario",
                table: "movimiento_stock",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valor",
                schema: "inventario",
                table: "movimiento_stock",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "ALTER TABLE inventario.movimiento_stock ALTER COLUMN precio_medio DROP DEFAULT");

            migrationBuilder.Sql(
                "ALTER TABLE inventario.movimiento_stock ALTER COLUMN valor DROP DEFAULT");

            migrationBuilder.AddColumn<decimal>(
                name: "valor",
                schema: "inventario",
                table: "lineas_ajuste",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_que_compensa",
                schema: "inventario",
                table: "lineas_ajuste",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "valoraciones",
                schema: "inventario",
                columns: table => new
                {
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    divisa = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_valoraciones", x => new { x.empresa_id, x.articulo_id, x.almacen_id });
                    table.CheckConstraint("ck_valoraciones_cantidad_no_negativa", "cantidad >= 0");
                    table.CheckConstraint("ck_valoraciones_sin_cantidad_no_hay_valor", "cantidad > 0 OR valor = 0");
                    table.CheckConstraint("ck_valoraciones_valor_no_negativo", "valor >= 0");
                });

            migrationBuilder.Sql(
                """
                UPDATE inventario.lineas_ajuste AS l
                   SET valor = 0,
                       valor_que_compensa = CASE WHEN a.anula_a_id IS NULL THEN NULL ELSE 0 END
                  FROM inventario.ajustes AS a
                 WHERE a.id = l.ajuste_id
                   AND a.estado <> 'Borrador'
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO inventario.valoraciones
                    (empresa_id, articulo_id, almacen_id, cantidad, valor, divisa)
                SELECT e.empresa_id, e.articulo_id, e.almacen_id, sum(e.fisico), 0,
                       COALESCE(
                           (SELECT min(m.divisa)
                              FROM inventario.movimiento_stock AS m
                             WHERE m.empresa_id = e.empresa_id
                               AND m.articulo_id = e.articulo_id
                               AND m.almacen_id = e.almacen_id),
                           'EUR')
                  FROM inventario.existencias AS e
                 GROUP BY e.empresa_id, e.articulo_id, e.almacen_id
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "valoraciones",
                schema: "inventario");

            migrationBuilder.DropColumn(
                name: "precio_medio",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropColumn(
                name: "valor",
                schema: "inventario",
                table: "movimiento_stock");

            migrationBuilder.DropColumn(
                name: "valor",
                schema: "inventario",
                table: "lineas_ajuste");

            migrationBuilder.DropColumn(
                name: "valor_que_compensa",
                schema: "inventario",
                table: "lineas_ajuste");
        }
    }
}
