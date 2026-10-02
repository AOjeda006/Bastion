using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El número de serie de la existencia y del libro se llama <c>numero_de_serie_id</c>, y no
    /// <c>serie_id</c>, que en Inventario es también la serie de numeración (ADR-0050 §1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Se renombra y no se recrea</b> (ADR-0050 §2). Lo que EF Core propone es renombrar las
    /// columnas, pero borrar y crear otra vez las dos claves ajenas, el índice parcial y el
    /// <c>CHECK</c>. Crear una clave ajena o un <c>CHECK</c> recorre la tabla para validarla, y en
    /// el libro eso es leerlo entero. Renombrar solo toca el catálogo: PostgreSQL guarda las
    /// expresiones ya analizadas, apuntando a la columna y no a su nombre.
    /// </para>
    /// <para>
    /// <b>Las particiones del libro se renombran una a una.</b> Medido en PostgreSQL 17.6: la clave
    /// ajena renombrada en el padre conserva el nombre viejo en las particiones que ya existen, y
    /// el índice de cada partición, el que le puso el motor con la columna vieja. Las particiones
    /// que nacen después ya traen los nombres nuevos.
    /// </para>
    /// </remarks>
    public partial class ElNumeroDeSerieSeLlamaAsi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "serie_id",
                schema: "inventario",
                table: "movimiento_stock",
                newName: "numero_de_serie_id");

            migrationBuilder.RenameIndex(
                name: "ix_movimiento_stock_serie_id",
                schema: "inventario",
                table: "movimiento_stock",
                newName: "ix_movimiento_stock_numero_de_serie_id");

            migrationBuilder.RenameColumn(
                name: "serie_id",
                schema: "inventario",
                table: "existencias",
                newName: "numero_de_serie_id");

            migrationBuilder.RenameIndex(
                name: "ix_existencias_serie_id",
                schema: "inventario",
                table: "existencias",
                newName: "ix_existencias_numero_de_serie_id");

            migrationBuilder.RenameIndex(
                name: "ix_existencias_serie_en_un_sitio",
                schema: "inventario",
                table: "existencias",
                newName: "ix_existencias_numero_de_serie_en_un_sitio");

            migrationBuilder.Sql(
                """
                ALTER TABLE inventario.existencias
                    RENAME CONSTRAINT ck_existencias_serie_como_mucho_una
                    TO ck_existencias_numero_de_serie_como_mucho_una
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE inventario.existencias
                    RENAME CONSTRAINT fk_existencias_numeros_de_serie_serie_id
                    TO fk_existencias_numeros_de_serie_numero_de_serie_id
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE inventario.movimiento_stock
                    RENAME CONSTRAINT fk_movimiento_stock_numeros_de_serie_serie_id
                    TO fk_movimiento_stock_numeros_de_serie_numero_de_serie_id
                """);

            migrationBuilder.Sql(
                LasParticionesSeLlamanComoElPadre(
                    "fk_movimiento_stock_numeros_de_serie_numero_de_serie_id",
                    "ix_movimiento_stock_numero_de_serie_id",
                    "numero_de_serie_id"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE inventario.movimiento_stock
                    RENAME CONSTRAINT fk_movimiento_stock_numeros_de_serie_numero_de_serie_id
                    TO fk_movimiento_stock_numeros_de_serie_serie_id
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE inventario.existencias
                    RENAME CONSTRAINT fk_existencias_numeros_de_serie_numero_de_serie_id
                    TO fk_existencias_numeros_de_serie_serie_id
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE inventario.existencias
                    RENAME CONSTRAINT ck_existencias_numero_de_serie_como_mucho_una
                    TO ck_existencias_serie_como_mucho_una
                """);

            migrationBuilder.RenameIndex(
                name: "ix_existencias_numero_de_serie_en_un_sitio",
                schema: "inventario",
                table: "existencias",
                newName: "ix_existencias_serie_en_un_sitio");

            migrationBuilder.RenameIndex(
                name: "ix_existencias_numero_de_serie_id",
                schema: "inventario",
                table: "existencias",
                newName: "ix_existencias_serie_id");

            migrationBuilder.RenameColumn(
                name: "numero_de_serie_id",
                schema: "inventario",
                table: "existencias",
                newName: "serie_id");

            migrationBuilder.RenameIndex(
                name: "ix_movimiento_stock_numero_de_serie_id",
                schema: "inventario",
                table: "movimiento_stock",
                newName: "ix_movimiento_stock_serie_id");

            migrationBuilder.RenameColumn(
                name: "numero_de_serie_id",
                schema: "inventario",
                table: "movimiento_stock",
                newName: "serie_id");

            migrationBuilder.Sql(
                LasParticionesSeLlamanComoElPadre(
                    "fk_movimiento_stock_numeros_de_serie_serie_id",
                    "ix_movimiento_stock_serie_id",
                    "serie_id"));
        }

        /// <summary>
        /// Pone a cada partición del libro la clave ajena con el nombre de la del padre, y el índice
        /// con el nombre que el motor le daría hoy a una partición nueva:
        /// <c>&lt;partición&gt;_&lt;columna&gt;_idx</c>.
        /// </summary>
        /// <remarks>
        /// Se busca por parentesco, no por nombre: la clave ajena de cada partición es la que tiene
        /// por padre la del libro, y su índice, el que cuelga del índice del libro. Así vale para
        /// las dos direcciones y para las particiones que nacieron entre una y otra.
        /// </remarks>
        private static string LasParticionesSeLlamanComoElPadre(
            string claveAjena, string indice, string columna) =>
            $"""
            DO $$
            DECLARE
                hija record;
            BEGIN
                FOR hija IN
                    SELECT clon.conrelid::regclass AS particion,
                           clon.conname AS nombre,
                           padre.conname AS nuevo
                      FROM pg_catalog.pg_constraint AS padre
                      JOIN pg_catalog.pg_constraint AS clon ON clon.conparentid = padre.oid
                     WHERE padre.conrelid = 'inventario.movimiento_stock'::regclass
                       AND padre.conname = '{claveAjena}'
                       AND clon.conname <> padre.conname
                LOOP
                    EXECUTE format('ALTER TABLE %s RENAME CONSTRAINT %I TO %I',
                                   hija.particion, hija.nombre, hija.nuevo);
                END LOOP;

                FOR hija IN
                    SELECT indice.relname AS nombre,
                           particion.relname || '_{columna}_idx' AS nuevo
                      FROM pg_catalog.pg_inherits AS herencia
                      JOIN pg_catalog.pg_class AS indice ON indice.oid = herencia.inhrelid
                      JOIN pg_catalog.pg_index AS definicion ON definicion.indexrelid = indice.oid
                      JOIN pg_catalog.pg_class AS particion ON particion.oid = definicion.indrelid
                     WHERE herencia.inhparent = 'inventario.{indice}'::regclass
                       AND indice.relname <> particion.relname || '_{columna}_idx'
                LOOP
                    EXECUTE format('ALTER INDEX inventario.%I RENAME TO %I', hija.nombre, hija.nuevo);
                END LOOP;
            END
            $$;
            """;
    }
}
