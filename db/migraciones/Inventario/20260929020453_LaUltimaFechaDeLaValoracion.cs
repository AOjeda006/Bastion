using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// La fecha del último movimiento de cada valoración, que ningún documento puede tener por
    /// detrás (ADR-0047 §1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Admite nulo, y así se queda</b>: la valoración que nace en el cerrojo todavía no se ha
    /// movido. Añadir una columna que admite nulo y no tiene valor por omisión no reescribe la
    /// tabla.
    /// </para>
    /// <para>
    /// <b>Se rellena con el máximo del libro por clave</b>, que es la empresa, el artículo y el
    /// almacén. El libro solo se lee. Si trae fechas atrasadas de antes de esta migración, no se
    /// arreglan: es de solo añadido, y la regla vale desde la primera confirmación que pase por
    /// ella. Una valoración sin filas en el libro se queda en nulo.
    /// </para>
    /// </remarks>
    public partial class LaUltimaFechaDeLaValoracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ultima_fecha",
                schema: "inventario",
                table: "valoraciones",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE inventario.valoraciones AS v
                   SET ultima_fecha = m.ultima
                  FROM (SELECT empresa_id, articulo_id, almacen_id, max(fecha_de_operacion) AS ultima
                          FROM inventario.movimiento_stock
                         GROUP BY empresa_id, articulo_id, almacen_id) AS m
                 WHERE m.empresa_id = v.empresa_id
                   AND m.articulo_id = v.articulo_id
                   AND m.almacen_id = v.almacen_id
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ultima_fecha",
                schema: "inventario",
                table: "valoraciones");
        }
    }
}
