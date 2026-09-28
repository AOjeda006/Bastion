using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// La divisa pasa a la cabecera del ajuste, y el coste deja de ser obligatorio: una salida se
    /// valora al precio medio y no lo trae (ADR-0046 §7 y §8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Se renombra y no se recrea.</b> Lo que EF Core propone es borrar las columnas del coste y
    /// crearlas de nuevo, y eso se llevaría el coste de cada fila. En el libro, además, no habría
    /// forma de volver a escribirlo, porque el libro no admite un <c>UPDATE</c>. Así que las
    /// columnas se renombran, y el libro no recibe ni una sentencia de cambio sobre sus filas.
    /// </para>
    /// <para>
    /// <b>La cabecera recibe la divisa de sus líneas</b>, que la línea ya guardaba. Si un ajuste no
    /// tuviera líneas —no llega ni a abrirse—, recibe el euro, que es lo que Bastion factura hoy. Si
    /// sus líneas tuvieran dos divisas, se queda con la primera por orden alfabético.
    /// </para>
    /// <para>
    /// <b>Las líneas en borrador que bajan stock pierden su coste</b>, que ya no se usaría. Las de
    /// los documentos confirmados lo conservan, igual que sus filas del libro.
    /// </para>
    /// </remarks>
    public partial class ElAjusteLlevaSuDivisa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "divisa",
                schema: "inventario",
                table: "ajustes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE inventario.ajustes AS a
                   SET divisa = COALESCE(
                       (SELECT min(l.coste_unitario_divisa)
                          FROM inventario.lineas_ajuste AS l
                         WHERE l.ajuste_id = a.id),
                       'EUR')
                """);

            migrationBuilder.Sql("ALTER TABLE inventario.ajustes ALTER COLUMN divisa SET NOT NULL");

            migrationBuilder.RenameColumn(
                name: "coste_unitario_cantidad",
                schema: "inventario",
                table: "lineas_ajuste",
                newName: "coste_unitario");

            migrationBuilder.Sql(
                "ALTER TABLE inventario.lineas_ajuste ALTER COLUMN coste_unitario DROP NOT NULL");

            migrationBuilder.Sql(
                """
                UPDATE inventario.lineas_ajuste AS l
                   SET coste_unitario = NULL
                  FROM inventario.ajustes AS a
                 WHERE a.id = l.ajuste_id
                   AND a.estado = 'Borrador'
                   AND l.cantidad_introducida < 0
                """);

            migrationBuilder.DropColumn(
                name: "coste_unitario_divisa",
                schema: "inventario",
                table: "lineas_ajuste");

            migrationBuilder.RenameColumn(
                name: "coste_unitario_cantidad",
                schema: "inventario",
                table: "movimiento_stock",
                newName: "coste_unitario");

            migrationBuilder.Sql(
                "ALTER TABLE inventario.movimiento_stock ALTER COLUMN coste_unitario DROP NOT NULL");

            migrationBuilder.RenameColumn(
                name: "coste_unitario_divisa",
                schema: "inventario",
                table: "movimiento_stock",
                newName: "divisa");
        }

        /// <inheritdoc />
        /// <remarks>
        /// <b>El libro no vuelve si ya tiene filas sin coste.</b> Devolverle el <c>NOT NULL</c>
        /// exigiría rellenarlas, y el libro no admite un <c>UPDATE</c>: la sentencia falla con el
        /// nombre de la columna, y la base se queda en esta migración. Las líneas sí se rellenan,
        /// a cero, porque un documento se puede corregir y el libro no.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "divisa",
                schema: "inventario",
                table: "movimiento_stock",
                newName: "coste_unitario_divisa");

            migrationBuilder.Sql(
                "ALTER TABLE inventario.movimiento_stock ALTER COLUMN coste_unitario SET NOT NULL");

            migrationBuilder.RenameColumn(
                name: "coste_unitario",
                schema: "inventario",
                table: "movimiento_stock",
                newName: "coste_unitario_cantidad");

            migrationBuilder.AddColumn<string>(
                name: "coste_unitario_divisa",
                schema: "inventario",
                table: "lineas_ajuste",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE inventario.lineas_ajuste AS l
                   SET coste_unitario_divisa = a.divisa,
                       coste_unitario = COALESCE(l.coste_unitario, 0)
                  FROM inventario.ajustes AS a
                 WHERE a.id = l.ajuste_id
                """);

            migrationBuilder.Sql(
                "ALTER TABLE inventario.lineas_ajuste ALTER COLUMN coste_unitario_divisa SET NOT NULL");

            migrationBuilder.Sql(
                "ALTER TABLE inventario.lineas_ajuste ALTER COLUMN coste_unitario SET NOT NULL");

            migrationBuilder.RenameColumn(
                name: "coste_unitario",
                schema: "inventario",
                table: "lineas_ajuste",
                newName: "coste_unitario_cantidad");

            migrationBuilder.DropColumn(
                name: "divisa",
                schema: "inventario",
                table: "ajustes");
        }
    }
}
