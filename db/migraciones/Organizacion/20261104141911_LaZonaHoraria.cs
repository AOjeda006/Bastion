using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Organizacion.Infrastructure.Migrations
{
    /// <summary>
    /// La zona horaria de la empresa y la del almacén, que deciden a qué día pertenece un instante
    /// (ADR-0079).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La de la empresa no admite nulo y nace con la peninsular</b>, que es la de todas las que
    /// ya estaban: hasta hoy el día era el UTC, y ninguna empresa había dicho otra cosa. El valor
    /// por omisión se queda en la columna, y no solo para rellenar: es el mismo que pone el dominio
    /// a la que no dice la suya.
    /// </para>
    /// <para>
    /// <b>La del almacén admite nulo, y el nulo significa algo</b>: el almacén hereda la de su
    /// empresa, en vivo. Por eso no se rellena con la de la empresa al migrar: copiarla convertiría
    /// la herencia en una zona propia que ya no seguiría a la empresa.
    /// </para>
    /// <para>
    /// <b>Texto con <c>CHECK</c>, y no un <c>ENUM</c> de PostgreSQL</b>, como el resto de
    /// enumerados: la lista es cerrada, pero ampliarla tiene que ser una migración corriente. La
    /// lista del <c>CHECK</c> sale de <c>ZonaHoraria.Admitidas</c>, no se teclea.
    /// </para>
    /// <para>
    /// Las dos columnas se añaden sin reescribir la tabla: un valor por omisión constante es barato
    /// desde PostgreSQL 11, y la anulable no lleva ninguno.
    /// </para>
    /// </remarks>
    public partial class LaZonaHoraria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "zona_horaria",
                schema: "organizacion",
                table: "empresas",
                type: "text",
                nullable: false,
                defaultValue: "Europe/Madrid");

            migrationBuilder.AddColumn<string>(
                name: "zona_horaria",
                schema: "organizacion",
                table: "almacenes",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_empresas_zona_horaria",
                schema: "organizacion",
                table: "empresas",
                sql: "zona_horaria IN ('Europe/Madrid', 'Atlantic/Canary', 'Africa/Ceuta')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_almacenes_zona_horaria",
                schema: "organizacion",
                table: "almacenes",
                sql: "zona_horaria IS NULL OR zona_horaria IN ('Europe/Madrid', 'Atlantic/Canary', 'Africa/Ceuta')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_empresas_zona_horaria",
                schema: "organizacion",
                table: "empresas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_almacenes_zona_horaria",
                schema: "organizacion",
                table: "almacenes");

            migrationBuilder.DropColumn(
                name: "zona_horaria",
                schema: "organizacion",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "zona_horaria",
                schema: "organizacion",
                table: "almacenes");
        }
    }
}
