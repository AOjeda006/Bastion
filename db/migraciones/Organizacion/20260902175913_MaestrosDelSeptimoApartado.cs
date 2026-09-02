using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Organizacion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MaestrosDelSeptimoApartado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "divisas",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_divisas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "impuestos",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    porcentaje = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    vigente_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    vigente_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    cuenta_repercutido = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    cuenta_soportado = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_impuestos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ubicaciones",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pasillo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    estante = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    hueco = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    bloqueado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_del_bloqueo = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ubicaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_ubicaciones_almacenes_almacen_id",
                        column: x => x.almacen_id,
                        principalSchema: "organizacion",
                        principalTable: "almacenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ubicaciones_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "organizacion",
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unidades_de_medida",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    decimales = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidades_de_medida", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipos_de_cambio",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    divisa_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    divisa_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    tasa = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_de_cambio", x => x.id);
                    table.ForeignKey(
                        name: "fk_tipos_de_cambio_divisas_divisa_destino_id",
                        column: x => x.divisa_destino_id,
                        principalSchema: "organizacion",
                        principalTable: "divisas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tipos_de_cambio_divisas_divisa_origen_id",
                        column: x => x.divisa_origen_id,
                        principalSchema: "organizacion",
                        principalTable: "divisas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversiones_de_unidades",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversiones_de_unidades", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversiones_de_unidades_unidades_de_medida_unidad_destino_",
                        column: x => x.unidad_destino_id,
                        principalSchema: "organizacion",
                        principalTable: "unidades_de_medida",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conversiones_de_unidades_unidades_de_medida_unidad_origen_id",
                        column: x => x.unidad_origen_id,
                        principalSchema: "organizacion",
                        principalTable: "unidades_de_medida",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversiones_de_unidades_unidad_destino_id",
                schema: "organizacion",
                table: "conversiones_de_unidades",
                column: "unidad_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversiones_de_unidades_unidad_origen_id_unidad_destino_id",
                schema: "organizacion",
                table: "conversiones_de_unidades",
                columns: new[] { "unidad_origen_id", "unidad_destino_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_divisas_codigo",
                schema: "organizacion",
                table: "divisas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_impuestos_codigo_vigente_desde",
                schema: "organizacion",
                table: "impuestos",
                columns: new[] { "codigo", "vigente_desde" });

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_cambio_divisa_destino_id",
                schema: "organizacion",
                table: "tipos_de_cambio",
                column: "divisa_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_cambio_divisa_origen_id_divisa_destino_id_fecha",
                schema: "organizacion",
                table: "tipos_de_cambio",
                columns: new[] { "divisa_origen_id", "divisa_destino_id", "fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ubicaciones_almacen_id_codigo",
                schema: "organizacion",
                table: "ubicaciones",
                columns: new[] { "almacen_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ubicaciones_empresa_id",
                schema: "organizacion",
                table: "ubicaciones",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_unidades_de_medida_codigo",
                schema: "organizacion",
                table: "unidades_de_medida",
                column: "codigo",
                unique: true);

            // ---------------------------------------------------------------------------------
            // Lo que EF Core no sabe escribir, y que es la regla más importante de esta tabla.
            //
            // El código de un impuesto SE REPITE a propósito: una fila por tramo de vigencia, que
            // es lo que permite que una factura de agosto de 2012 siga llevando el 18 % después
            // de que el general subiera al 21. Lo que no puede pasar es que dos tramos del mismo
            // código se pisen: «el impuesto vigente el día D» devolvería dos filas y la consulta
            // elegiría una según el orden del plan de ejecución. El síntoma no sería un error:
            // sería una cuota distinta de un día para otro sin que nadie hubiera tocado nada.
            //
            // Un índice único no puede expresarlo —lo que no debe repetirse es un RANGO, no un
            // valor—, así que lo expresa un `EXCLUDE`: dos filas cuyo código coincida (`=`) y
            // cuyos tramos se solapen (`&&`) no pueden convivir. El rango se construye con
            // `daterange(desde, hasta, '[]')` —cerrado por los dos lados, igual que `RigeEl`—, y
            // el tramo abierto sale gratis: un extremo NULO en el constructor de un rango
            // significa «sin límite por ese lado», así que el impuesto todavía vigente se solapa
            // con cualquiera posterior, que es exactamente lo que hay que impedir.
            //
            // `btree_gist` hace falta porque el operador `=` sobre texto no es GiST de serie;
            // viene con PostgreSQL y no es una extensión de terceros. `IF NOT EXISTS` porque otro
            // módulo podría haberla creado ya en la misma base.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(
                """
                ALTER TABLE organizacion.impuestos
                    ADD CONSTRAINT impuestos_sin_tramos_solapados
                    EXCLUDE USING gist (
                        codigo WITH =,
                        daterange(vigente_desde, vigente_hasta, '[]') WITH &&
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La restricción de exclusión se va con su tabla, así que no hay que soltarla aparte.
            // La extensión `btree_gist` SÍ se queda a propósito: es de la base, no de este módulo, y
            // otro podría estar apoyándose en ella. Soltarla aquí rompería a un tercero por revertir
            // una migración ajena.
            migrationBuilder.DropTable(
                name: "conversiones_de_unidades",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "impuestos",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "tipos_de_cambio",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "ubicaciones",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "unidades_de_medida",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "divisas",
                schema: "organizacion");
        }
    }
}
