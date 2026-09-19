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
    /// <b>La clave ajena es <c>NO ACTION</c> y se aplaza al <c>COMMIT</c>, y eso lo decidió una
    /// medición, no un gusto.</b> Quien borra la fila hija es el ORM, con su testigo de
    /// concurrencia dentro, y de eso depende la carrera suprimir-contra-confirmar: si alguien
    /// numeró en medio, ese <c>DELETE</c> no casa con ninguna fila y EF Core lo cuenta como un
    /// choque de versión. Con <c>RESTRICT</c> nunca llegaba a contarlo: PostgreSQL comprueba
    /// <c>RESTRICT</c> <b>en el acto</b> —y no se puede aplazar, que es la diferencia entera entre
    /// <c>RESTRICT</c> y <c>NO ACTION</c>—, así que el <c>DELETE</c> de la serie, que viaja en el
    /// mismo lote, reventaba antes con un <c>23503</c> que el borde no sabe traducir. Medido:
    /// <c>DbUpdateException</c> donde tenía que salir <c>DbUpdateConcurrencyException</c>.
    /// </para>
    /// <para>
    /// <b>Lo que NO cambia es la prohibición</b>, y ahí está el precio exacto de la decisión: un
    /// contador huérfano sigue siendo imposible —<c>NO ACTION</c> lo prohíbe igual que
    /// <c>RESTRICT</c>— y sigue sin haber cascada ninguna en el motor, que es lo que el ADR-0007
    /// §8 prohíbe. Lo único que se mueve es <b>cuándo</b> se comprueba: al confirmar la
    /// transacción en vez de al ejecutar la sentencia. Que siga prohibido lo ejerce
    /// <c>ElCerrojoDeLaNumeracionTests.Borrar_una_serie_a_mano_sin_su_contador_sigue_siendo_imposible</c>,
    /// que borra la serie a mano y ve reventar el <c>COMMIT</c>.
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
                        onDelete: ReferentialAction.NoAction);
                });

            // APLAZADA AL `COMMIT`, para que el testigo del ORM hable primero. EF Core no sabe
            // declarar esto, así que va a mano y justo detrás de la tabla. El porqué entero, arriba.
            migrationBuilder.Sql(
                """
                ALTER TABLE organizacion.contadores_de_serie
                    ALTER CONSTRAINT fk_contadores_de_serie_series_serie_id
                    DEFERRABLE INITIALLY DEFERRED
                """);

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
