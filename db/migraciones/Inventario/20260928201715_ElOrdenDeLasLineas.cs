using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El número de cada línea de ajuste, que es el orden en que se valora (ADR-0046 §3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las líneas que ya están se numeran por su identificador</b>, dentro de cada documento. No
    /// hay otro orden guardado, y no hace falta más: las de antes del 2.8 valen cero, así que su
    /// orden no cambia ningún valor, y las de un documento del 2.8 en borrador se valorarán en el
    /// que les toque aquí.
    /// </para>
    /// <para>
    /// <b>El valor por omisión solo vive durante la migración</b>, como en la anterior: una línea
    /// nueva que llegara sin número no entraría. Y el índice único va después del relleno, porque
    /// antes todas las líneas de un documento tendrían el mismo número.
    /// </para>
    /// </remarks>
    public partial class ElOrdenDeLasLineas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lineas_ajuste_ajuste_id",
                schema: "inventario",
                table: "lineas_ajuste");

            migrationBuilder.AddColumn<int>(
                name: "numero",
                schema: "inventario",
                table: "lineas_ajuste",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE inventario.lineas_ajuste AS l
                   SET numero = o.numero
                  FROM (SELECT id, row_number() OVER (PARTITION BY ajuste_id ORDER BY id) AS numero
                          FROM inventario.lineas_ajuste) AS o
                 WHERE o.id = l.id
                """);

            migrationBuilder.Sql(
                "ALTER TABLE inventario.lineas_ajuste ALTER COLUMN numero DROP DEFAULT");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_ajuste_ajuste_id_numero",
                schema: "inventario",
                table: "lineas_ajuste",
                columns: new[] { "ajuste_id", "numero" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_lineas_ajuste_numero_desde_uno",
                schema: "inventario",
                table: "lineas_ajuste",
                sql: "numero > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lineas_ajuste_ajuste_id_numero",
                schema: "inventario",
                table: "lineas_ajuste");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lineas_ajuste_numero_desde_uno",
                schema: "inventario",
                table: "lineas_ajuste");

            migrationBuilder.DropColumn(
                name: "numero",
                schema: "inventario",
                table: "lineas_ajuste");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_ajuste_ajuste_id",
                schema: "inventario",
                table: "lineas_ajuste",
                column: "ajuste_id");
        }
    }
}
