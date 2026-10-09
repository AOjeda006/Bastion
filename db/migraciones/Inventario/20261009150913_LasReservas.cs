using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// Las reservas y sus consumos, y lo reservado sale de la fila de existencias (ADR-0059 §3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No hay nada que trasladar</b>: <c>reservado</c> valía cero en todas las filas, porque
    /// nada lo escribía, y <c>disponible</c> lo calculaba el motor a partir de él. Lo reservado se
    /// suma ahora al leer, de las reservas activas y vigentes. Antes no había reservas, así que las
    /// dos tablas nacen vacías.
    /// </para>
    /// <para>
    /// <b>La bajada devuelve las dos columnas</b>, con <c>reservado</c> a cero y el disponible
    /// calculado otra vez por el motor. Lo que no devuelve es lo reservado de las reservas que se
    /// pierden con su tabla: es la bajada de una versión que no las conocía.
    /// </para>
    /// </remarks>
    public partial class LasReservas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "disponible",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.DropColumn(
                name: "reservado",
                schema: "inventario",
                table: "existencias");

            migrationBuilder.CreateTable(
                name: "reservas",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen_linea = table.Column<int>(type: "integer", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unidad_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caduca_el = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    causa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    liberada_el = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservas", x => x.id);
                    table.CheckConstraint("ck_reservas_caducidad_en_su_fecha", "causa IS DISTINCT FROM 'Caducidad' OR COALESCE(liberada_el = caduca_el, FALSE)");
                    table.CheckConstraint("ck_reservas_cantidad_positiva", "cantidad > 0");
                    table.CheckConstraint("ck_reservas_liberada_con_causa_y_fecha", "(estado = 'Liberada') = (causa IS NOT NULL) AND (estado = 'Liberada') = (liberada_el IS NOT NULL)");
                    table.CheckConstraint("ck_reservas_motivo_solo_a_mano", "(motivo IS NOT NULL) = COALESCE(causa = 'AMano', FALSE)");
                    table.CheckConstraint("ck_reservas_origen_linea_desde_uno", "origen_linea > 0");
                });

            migrationBuilder.CreateTable(
                name: "consumos_de_reserva",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reserva_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_de_operacion = table.Column<DateOnly>(type: "date", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumos_de_reserva", x => x.id);
                    table.CheckConstraint("ck_consumos_de_reserva_cantidad_positiva", "cantidad > 0");
                    table.ForeignKey(
                        name: "fk_consumos_de_reserva_reservas_reserva_id",
                        column: x => x.reserva_id,
                        principalSchema: "inventario",
                        principalTable: "reservas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consumos_de_reserva_documento_tipo_documento_id",
                schema: "inventario",
                table: "consumos_de_reserva",
                columns: new[] { "documento_tipo", "documento_id" });

            migrationBuilder.CreateIndex(
                name: "ix_consumos_de_reserva_uno_por_documento",
                schema: "inventario",
                table: "consumos_de_reserva",
                columns: new[] { "reserva_id", "documento_tipo", "documento_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reservas_activas_por_clave",
                schema: "inventario",
                table: "reservas",
                columns: new[] { "empresa_id", "articulo_id", "almacen_id" },
                filter: "estado = 'Activa'");

            migrationBuilder.CreateIndex(
                name: "ix_reservas_una_por_origen",
                schema: "inventario",
                table: "reservas",
                columns: new[] { "empresa_id", "origen_tipo", "origen_id", "origen_linea" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumos_de_reserva",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "reservas",
                schema: "inventario");

            migrationBuilder.AddColumn<decimal>(
                name: "reservado",
                schema: "inventario",
                table: "existencias",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "disponible",
                schema: "inventario",
                table: "existencias",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                computedColumnSql: "fisico - reservado",
                stored: true);
        }
    }
}
