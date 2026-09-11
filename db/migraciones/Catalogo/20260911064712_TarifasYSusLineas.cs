using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Catalogo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TarifasYSusLineas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tarifas",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    divisa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vigente_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    vigente_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tarifas", x => x.id);
                    table.CheckConstraint("ck_tarifas_vigencia_no_invertida", "vigente_hasta IS NULL OR vigente_hasta >= vigente_desde");
                });

            migrationBuilder.CreateTable(
                name: "lineas_tarifa",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarifa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    articulo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cantidad_desde = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    descuento_porcentaje = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    precio = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lineas_tarifa", x => x.id);
                    table.CheckConstraint("ck_lineas_tarifa_articulo_o_categoria", "(articulo_id IS NOT NULL AND categoria_id IS NULL) OR (articulo_id IS NULL AND categoria_id IS NOT NULL)");
                    table.CheckConstraint("ck_lineas_tarifa_cantidad_desde_no_negativa", "cantidad_desde >= 0");
                    table.CheckConstraint("ck_lineas_tarifa_descuento_en_rango", "descuento_porcentaje IS NULL OR (descuento_porcentaje >= 0 AND descuento_porcentaje <= 100)");
                    table.CheckConstraint("ck_lineas_tarifa_precio_no_negativo", "precio IS NULL OR precio >= 0");
                    table.CheckConstraint("ck_lineas_tarifa_precio_o_descuento", "(precio IS NOT NULL AND descuento_porcentaje IS NULL) OR (precio IS NULL AND descuento_porcentaje IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_lineas_tarifa_articulos_articulo_id",
                        column: x => x.articulo_id,
                        principalSchema: "catalogo",
                        principalTable: "articulos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lineas_tarifa_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalSchema: "catalogo",
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lineas_tarifa_tarifas_tarifa_id",
                        column: x => x.tarifa_id,
                        principalSchema: "catalogo",
                        principalTable: "tarifas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lineas_tarifa_articulo_id",
                schema: "catalogo",
                table: "lineas_tarifa",
                column: "articulo_id");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_tarifa_categoria_id",
                schema: "catalogo",
                table: "lineas_tarifa",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_tarifa_empresa_id_tarifa_id",
                schema: "catalogo",
                table: "lineas_tarifa",
                columns: new[] { "empresa_id", "tarifa_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lineas_tarifa_tarifa_id_articulo_id_cantidad_desde",
                schema: "catalogo",
                table: "lineas_tarifa",
                columns: new[] { "tarifa_id", "articulo_id", "cantidad_desde" },
                unique: true,
                filter: "articulo_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lineas_tarifa_tarifa_id_categoria_id_cantidad_desde",
                schema: "catalogo",
                table: "lineas_tarifa",
                columns: new[] { "tarifa_id", "categoria_id", "cantidad_desde" },
                unique: true,
                filter: "categoria_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tarifas_empresa_id_codigo_vigente_desde",
                schema: "catalogo",
                table: "tarifas",
                columns: new[] { "empresa_id", "codigo", "vigente_desde" });

            // ---------------------------------------------------------------------------------
            // Lo que EF Core no sabe escribir, y que es la regla más importante de esta tabla.
            //
            // El código de una tarifa SE REPITE a propósito: una fila por tramo de vigencia, que
            // es lo que permite que un albarán de marzo siga valorado con los precios de marzo
            // después de que se haya hecho la revisión de abril. Lo que no puede pasar es que dos
            // tramos del mismo código se pisen: «la tarifa PVP del día D» devolvería dos filas y
            // la consulta elegiría una según el orden del plan de ejecución. El síntoma no sería
            // un error: sería un precio distinto de un día para otro sin que nadie hubiera tocado
            // nada.
            //
            // Un índice único no puede expresarlo —lo que no debe repetirse es un RANGO, no un
            // valor—, así que lo expresa un `EXCLUDE`: dos filas de la misma empresa cuyo código
            // coincida (`=`) y cuyos tramos se solapen (`&&`) no pueden convivir.
            //
            // LA EMPRESA VA DENTRO DE LA RESTRICCIÓN, y no es decoración: sin `empresa_id WITH =`,
            // la tarifa PVP de una ferretería impediría que la imprenta de al lado abriera la
            // suya, y el rechazo hablaría de una fila que quien lo recibe no puede ni ver (R8).
            //
            // El rango se construye con `daterange(vigente_desde, vigente_hasta, '[]')` —CERRADO
            // POR LOS DOS LADOS, igual que `Tarifa.RigeEl` y que `HaySolapeAsync`—. Las tres
            // convenciones son la misma a propósito: con `[)` aquí y `[]` allí, el día en que un
            // tramo acaba valdría una cosa al resolver un precio y otra al comprobar el solape, y
            // dos tramos consecutivos que compartieran ese día pasarían la comprobación de la
            // aplicación para chocar contra la base convertidos en un 500. Y el tramo abierto sale
            // gratis: un extremo NULO en el constructor de un rango significa «sin límite por ese
            // lado», así que la tarifa todavía vigente se solapa con cualquiera posterior — que es
            // exactamente lo que hay que impedir.
            //
            // Es `date` y no `timestamptz` porque la vigencia es una fecha de negocio (R14). Un
            // `tstzrange` haría que el día de la frontera dependiera del huso de quien pregunta.
            //
            // `btree_gist` hace falta porque los operadores `=` sobre `uuid` y sobre texto no son
            // GiST de serie; viene con PostgreSQL y no es una extensión de terceros. `IF NOT
            // EXISTS` porque otro módulo puede haberla creado ya en la misma base —Organización lo
            // hace en el 0.15—, y porque el orden en que se aplican las migraciones de los módulos
            // no está garantizado: una base recién creada que solo tuviera Catálogo tiene que
            // funcionar igual, así que esta línea no puede darla por puesta.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(
                """
                ALTER TABLE catalogo.tarifas
                    ADD CONSTRAINT tarifas_sin_tramos_solapados
                    EXCLUDE USING gist (
                        empresa_id WITH =,
                        codigo WITH =,
                        daterange(vigente_desde, vigente_hasta, '[]') WITH &&
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La restricción de exclusión se va con su tabla, así que no hay que soltarla aparte.
            // La extensión `btree_gist` SÍ se queda a propósito: es de la base, no de este módulo,
            // y Organización se apoya en ella desde el 0.15. Soltarla aquí rompería a un tercero
            // por revertir una migración ajena.
            migrationBuilder.DropTable(
                name: "lineas_tarifa",
                schema: "catalogo");

            migrationBuilder.DropTable(
                name: "tarifas",
                schema: "catalogo");
        }
    }
}
