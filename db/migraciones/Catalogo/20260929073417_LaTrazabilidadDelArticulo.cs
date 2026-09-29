using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Catalogo.Infrastructure.Migrations
{
    /// <summary>
    /// La marca de trazabilidad del artículo: sin nada, por lote o por número de serie (ADR-0048
    /// §1).
    /// </summary>
    /// <remarks>
    /// <b>La columna entra nula, se rellena y se cierra</b>, como la divisa del ajuste. Lo que
    /// propone EF Core es añadirla <c>NOT NULL</c> con la cadena vacía por omisión, y la cadena
    /// vacía no es ninguna de las tres marcas: el <c>CHECK</c> que llega detrás rechazaría la
    /// primera fila que ya estuviera. Todo artículo existente recibe <c>Ninguna</c>, que es lo que
    /// hacía hasta hoy: su libro no guarda ni lote ni serie.
    /// </remarks>
    public partial class LaTrazabilidadDelArticulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trazabilidad",
                schema: "catalogo",
                table: "articulos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("UPDATE catalogo.articulos SET trazabilidad = 'Ninguna'");

            migrationBuilder.Sql(
                "ALTER TABLE catalogo.articulos ALTER COLUMN trazabilidad SET NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_articulos_servicio_sin_trazabilidad",
                schema: "catalogo",
                table: "articulos",
                sql: "tipo <> 'Servicio' OR trazabilidad = 'Ninguna'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_articulos_trazabilidad",
                schema: "catalogo",
                table: "articulos",
                sql: "trazabilidad IN ('Ninguna', 'PorLote', 'PorNumeroSerie')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_articulos_servicio_sin_trazabilidad",
                schema: "catalogo",
                table: "articulos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_articulos_trazabilidad",
                schema: "catalogo",
                table: "articulos");

            migrationBuilder.DropColumn(
                name: "trazabilidad",
                schema: "catalogo",
                table: "articulos");
        }
    }
}
