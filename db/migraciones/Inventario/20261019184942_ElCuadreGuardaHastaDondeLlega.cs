using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ElCuadreGuardaHastaDondeLlega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_instantaneas_mensuales_empresa_id",
                schema: "inventario",
                table: "instantaneas_mensuales");

            migrationBuilder.AddColumn<DateOnly>(
                name: "cuadrado_hasta",
                schema: "inventario",
                table: "cortes_de_la_instantanea",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_instantaneas_mensuales_empresa_id_mes",
                schema: "inventario",
                table: "instantaneas_mensuales",
                columns: new[] { "empresa_id", "mes" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_cortes_de_la_instantanea_cuadrado_hasta_es_primer_dia",
                schema: "inventario",
                table: "cortes_de_la_instantanea",
                sql: "extract(day from cuadrado_hasta) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cortes_de_la_instantanea_cuadrado_hasta_no_pasa_del_corte",
                schema: "inventario",
                table: "cortes_de_la_instantanea",
                sql: "cuadrado_hasta <= hasta_el_mes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_instantaneas_mensuales_empresa_id_mes",
                schema: "inventario",
                table: "instantaneas_mensuales");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cortes_de_la_instantanea_cuadrado_hasta_es_primer_dia",
                schema: "inventario",
                table: "cortes_de_la_instantanea");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cortes_de_la_instantanea_cuadrado_hasta_no_pasa_del_corte",
                schema: "inventario",
                table: "cortes_de_la_instantanea");

            migrationBuilder.DropColumn(
                name: "cuadrado_hasta",
                schema: "inventario",
                table: "cortes_de_la_instantanea");

            migrationBuilder.CreateIndex(
                name: "ix_instantaneas_mensuales_empresa_id",
                schema: "inventario",
                table: "instantaneas_mensuales",
                column: "empresa_id");
        }
    }
}
