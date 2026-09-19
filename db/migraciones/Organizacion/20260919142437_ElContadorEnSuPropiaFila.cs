using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Organizacion.Infrastructure.Migrations
{
    /// <summary>
    /// El contador de una serie sale de la fila de `series` a su propia tabla (ADR-0039).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El orden de las tres órdenes es la migración entera.</b> Lo que EF Core genera solo es
    /// «tira la columna, crea la tabla», en ese orden y sin copiar nada: contra una base recién
    /// creada da igual, y contra una que ya tiene series dentro pierde el contador de cada una.
    /// Aquí se crea la tabla, se copia lo que hay y solo entonces se tira la columna.
    /// </para>
    /// <para>
    /// <b>La clave ajena se fija a <c>RESTRICT</c> a mano</b>, y no es adorno. El modelo declara la
    /// relación como cascada <i>del lado del cliente</i>, que en la base se traduce a «sin acción»:
    /// quien borra la fila hija es el ORM, con su testigo de concurrencia dentro, y de eso depende
    /// la carrera suprimir-contra-confirmar. <c>RESTRICT</c> añade lo que falta por debajo: un
    /// <c>DELETE</c> a mano sobre <c>series</c> no puede dejar un contador huérfano, y una cascada
    /// en el motor —lo que el ADR-0007 §8 prohíbe— tampoco puede colarse por aquí.
    /// </para>
    /// </remarks>
    public partial class ElContadorEnSuPropiaFila : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "contadores_de_serie",
                schema: "organizacion",
                columns: table => new
                {
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ultimo_numero = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contadores_de_serie", x => x.serie_id);
                    table.ForeignKey(
                        name: "fk_contadores_de_serie_series_serie_id",
                        column: x => x.serie_id,
                        principalSchema: "organizacion",
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // UNA FILA POR SERIE, con lo que la serie llevara contado. Es la mudanza: sin esto, la
            // siguiente lectura de cualquier serie ya numerada diría que va por cero, y
            // `SePuedeSuprimir` dejaría borrar una serie con documentos emitidos detrás.
            migrationBuilder.Sql(
                """
                INSERT INTO organizacion.contadores_de_serie (serie_id, ultimo_numero)
                SELECT id, contador FROM organizacion.series
                """);

            migrationBuilder.DropColumn(
                name: "contador",
                schema: "organizacion",
                table: "series");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<long>(
                name: "contador",
                schema: "organizacion",
                table: "series",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // SIMÉTRICA, y por el mismo motivo: deshacer la migración sin devolver los números
            // dejaría todas las series en cero, que es la misma pérdida al revés.
            migrationBuilder.Sql(
                """
                UPDATE organizacion.series AS s
                SET contador = c.ultimo_numero
                FROM organizacion.contadores_de_serie AS c
                WHERE c.serie_id = s.id
                """);

            migrationBuilder.DropTable(
                name: "contadores_de_serie",
                schema: "organizacion");
        }
    }
}
