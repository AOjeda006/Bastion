using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Auditoria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClavesDeIdempotencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claves_de_idempotencia",
                schema: "auditoria",
                columns: table => new
                {
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metodo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ruta = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    clave = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    huella = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    codigo_de_estado = table.Column<int>(type: "integer", nullable: true),
                    cuerpo = table.Column<string>(type: "text", nullable: true),
                    tipo_de_contenido = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    etiqueta = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ubicacion = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claves_de_idempotencia", x => new { x.empresa_id, x.usuario_id, x.metodo, x.ruta, x.clave });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claves_de_idempotencia",
                schema: "auditoria");
        }
    }
}
