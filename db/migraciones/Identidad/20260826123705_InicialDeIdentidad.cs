using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Identidad.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialDeIdentidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identidad");

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    es_del_sistema = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    hash_de_contrasena = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    bloqueado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_acceso_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos_fallidos = table.Column<int>(type: "integer", nullable: false),
                    rechazado_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permisos_de_rol",
                schema: "identidad",
                columns: table => new
                {
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permiso = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permisos_de_rol", x => new { x.rol_id, x.permiso });
                    table.ForeignKey(
                        name: "fk_permisos_de_rol_roles_rol_id",
                        column: x => x.rol_id,
                        principalSchema: "identidad",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membresias",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_membresias", x => x.id);
                    table.ForeignKey(
                        name: "fk_membresias_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "identidad",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tokens_de_refresco",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_activa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    canjeado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sustituido_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revocado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokens_de_refresco", x => x.id);
                    table.ForeignKey(
                        name: "fk_tokens_de_refresco_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "identidad",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roles_de_membresia",
                schema: "identidad",
                columns: table => new
                {
                    membresia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles_de_membresia", x => new { x.membresia_id, x.rol_id });
                    table.ForeignKey(
                        name: "fk_roles_de_membresia_membresias_membresia_id",
                        column: x => x.membresia_id,
                        principalSchema: "identidad",
                        principalTable: "membresias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_membresias_usuario_id_empresa_id",
                schema: "identidad",
                table: "membresias",
                columns: new[] { "usuario_id", "empresa_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_codigo",
                schema: "identidad",
                table: "roles",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_de_refresco_familia_id",
                schema: "identidad",
                table: "tokens_de_refresco",
                column: "familia_id");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_de_refresco_hash",
                schema: "identidad",
                table: "tokens_de_refresco",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_de_refresco_usuario_id",
                schema: "identidad",
                table: "tokens_de_refresco",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_correo",
                schema: "identidad",
                table: "usuarios",
                column: "correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permisos_de_rol",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "roles_de_membresia",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "tokens_de_refresco",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "membresias",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "identidad");
        }
    }
}
