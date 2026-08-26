using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Organizacion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicialDeOrganizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organizacion");

            migrationBuilder.CreateTable(
                name: "empresas",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nif = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    razon_social = table.Column<string>(type: "text", nullable: false),
                    domicilio_fiscal_calle = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    domicilio_fiscal_numero = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    domicilio_fiscal_codigo_postal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    domicilio_fiscal_poblacion = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    domicilio_fiscal_subdivision = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    domicilio_fiscal_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    divisa_base = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    regimen_de_iva = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    bloqueada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empresas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "almacenes",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    direccion_calle = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    direccion_numero = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    direccion_codigo_postal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    direccion_poblacion = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    direccion_subdivision = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    direccion_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    bloqueado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_almacenes", x => x.id);
                    table.ForeignKey(
                        name: "fk_almacenes_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "organizacion",
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ejercicios",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    fecha_de_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_de_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ejercicios", x => x.id);
                    table.ForeignKey(
                        name: "fk_ejercicios_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "organizacion",
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "series",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejercicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_de_documento = table.Column<string>(type: "text", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    formato = table.Column<string>(type: "text", nullable: false),
                    contador = table.Column<long>(type: "bigint", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series", x => x.id);
                    table.ForeignKey(
                        name: "fk_series_ejercicios_ejercicio_id",
                        column: x => x.ejercicio_id,
                        principalSchema: "organizacion",
                        principalTable: "ejercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_series_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "organizacion",
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_almacenes_empresa_id_codigo",
                schema: "organizacion",
                table: "almacenes",
                columns: new[] { "empresa_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ejercicios_empresa_id_anio",
                schema: "organizacion",
                table: "ejercicios",
                columns: new[] { "empresa_id", "anio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empresas_nif",
                schema: "organizacion",
                table: "empresas",
                column: "nif",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_series_ejercicio_id",
                schema: "organizacion",
                table: "series",
                column: "ejercicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_series_empresa_id_ejercicio_id_codigo",
                schema: "organizacion",
                table: "series",
                columns: new[] { "empresa_id", "ejercicio_id", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "almacenes",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "series",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "ejercicios",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "empresas",
                schema: "organizacion");
        }
    }
}
