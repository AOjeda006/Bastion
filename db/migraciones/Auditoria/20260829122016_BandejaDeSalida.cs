using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Auditoria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BandejaDeSalida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bandeja_de_salida",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocurrido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sin_inquilino = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    nombre = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    cuerpo = table.Column<string>(type: "jsonb", nullable: false),
                    estado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    publicado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    ultimo_error = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bandeja_de_salida", x => x.id);
                    table.CheckConstraint("ck_bandeja_empresa_o_motivo", "(empresa_id IS NULL) <> (sin_inquilino IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "eventos_procesados",
                schema: "auditoria",
                columns: table => new
                {
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumidor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    procesado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_procesados", x => new { x.evento_id, x.consumidor });
                });

            migrationBuilder.CreateIndex(
                name: "ix_bandeja_de_salida_estado_id",
                schema: "auditoria",
                table: "bandeja_de_salida",
                columns: new[] { "estado", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_bandeja_evento_id",
                schema: "auditoria",
                table: "bandeja_de_salida",
                column: "evento_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bandeja_de_salida",
                schema: "auditoria");

            migrationBuilder.DropTable(
                name: "eventos_procesados",
                schema: "auditoria");
        }
    }
}
