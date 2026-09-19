using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Inventario.Infrastructure.Migrations
{
    /// <summary>
    /// El ajuste gana la serie que lo numera y el correlativo que esa serie le dio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>`numero` admite nulos y `serie_id` no</b>, y la asimetría es la regla: la serie se elige
    /// al abrir el borrador y el número no existe hasta confirmarlo. Un `numero` a cero por
    /// omisión no se distinguiría de un correlativo en ninguna consulta.
    /// </para>
    /// <para>
    /// <b>El índice único es la mitad de la R5 que cabe en esta tabla.</b> «Correlativa» prohibe
    /// que dos documentos de la misma serie lleven el mismo número, y eso se comprueba con estas
    /// dos columnas; «sin huecos» no, porque un hueco es una fila que no existe. Va filtrado por
    /// `numero IS NOT NULL` en vez de confiar en que PostgreSQL considere distintos los nulos: ese
    /// es el comportamiento por omisión, se puede cambiar en la definición del índice, y una
    /// garantía que depende de un ajuste que no se ve al leer no es una garantía.
    /// </para>
    /// <para>
    /// <b>La columna obligatoria entra con el cero por omisión, y ese cero es el menos malo de los
    /// valores que había.</b> De un ajuste ya escrito no se puede deducir la serie, y las otras
    /// dos salidas son peores: elegir una fila de <c>organizacion.series</c> ataría documentos
    /// viejos a una numeración fiscal de verdad, y reventar la migración pararía el paso
    /// <c>migraciones</c> del compose y con él el arranque de la API. El cero no hace ninguna de
    /// las dos cosas y no se confunde con nada: ningún identificador de serie lo lleva —salen de
    /// <c>Guid.CreateVersion7()</c>—, de modo que la fila migrada queda legible y a la vez
    /// incapaz de tomar número, porque el <c>WHERE</c> de la sentencia que numera no encuentra
    /// ninguna serie suya. Y nacer así tampoco se puede: <c>Ajuste.Abrir</c> rechaza el
    /// <c>Guid</c> vacío.
    /// </para>
    /// <para>
    /// <b>Aquí esto no migra ninguna fila, y no es eso lo que decide el valor.</b>
    /// <c>inventario.ajustes</c> nació en la migración anterior y el módulo no tiene todavía
    /// ningún borde HTTP por el que llegue un alta, así que no hay instalación desplegada con
    /// ajustes dentro. Lo que decide es el único sitio donde sí se construye el estado viejo con
    /// las tablas pobladas —<c>LasMigracionesSobreTablasConFilasTests</c>—, que es el que ve lo
    /// que le pasa a una tabla que ya tiene filas cuando le llega el paso siguiente.
    /// </para>
    /// </remarks>
    public partial class ElAjusteLlevaSerieYNumero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<long>(
                name: "numero",
                schema: "inventario",
                table: "ajustes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "serie_id",
                schema: "inventario",
                table: "ajustes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_serie_id_numero",
                schema: "inventario",
                table: "ajustes",
                columns: new[] { "serie_id", "numero" },
                unique: true,
                filter: "numero IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "ix_ajustes_serie_id_numero",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.DropColumn(
                name: "numero",
                schema: "inventario",
                table: "ajustes");

            migrationBuilder.DropColumn(
                name: "serie_id",
                schema: "inventario",
                table: "ajustes");
        }
    }
}
