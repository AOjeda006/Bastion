using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Identidad.Infrastructure.Migrations
{
    /// <summary>
    /// R16 y R14 en el esquema de Identidad: el <c>estado</c> del usuario pasa a las tres columnas
    /// del bloqueo compartido, y usuario y rol estrenan las marcas de tiempo que les faltaban.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Está escrita a mano por lo mismo que su hermana de Organización: lo generado tiraba
    /// <c>estado</c> antes de leerlo y creaba <c>bloqueado</c> a <c>false</c>, que sobre una tabla
    /// con filas es desbloquear a todo el mundo en silencio.
    /// </para>
    /// <para>
    /// <c>usuarios.creado_en</c> <b>no aparece aquí</b>: la tabla ya la tenía desde el 0.5, con el
    /// mismo nombre y el mismo tipo. Lo que ha cambiado es de dónde sale la propiedad —ahora la
    /// hereda de <c>EntidadBase</c> en vez de declararla el usuario—, y eso no es un cambio de
    /// esquema. Que la columna sobreviviera intacta es la señal de que el tipo base se extrajo de
    /// lo que ya había en vez de inventarse.
    /// </para>
    /// </remarks>
    public partial class BloqueoYMarcasDeTiempo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // --- 1. El bloqueo del usuario -------------------------------------------------
            // `bloqueado_en` ya se llamaba así y no se toca: es el instante del que cuelga el
            // plazo del art. 32.
            migrationBuilder.AddColumn<bool>(
                name: "bloqueado",
                schema: "identidad",
                table: "usuarios",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_del_bloqueo",
                schema: "identidad",
                table: "usuarios",
                type: "text",
                nullable: true);

            // Antes del 0.10 solo había un camino que bloqueaba un usuario: la supresión del
            // art. 32 pedida por el interesado. El motivo de las filas ya bloqueadas es ese, y no
            // hace falta adivinarlo.
            //
            // OJO con lo que NO entra aquí: `rechazado_hasta` es el rechazo temporal por intentos
            // fallidos y se queda donde está, en su propia columna. Fundirlo con este bloqueo
            // haría que fallar la contraseña cinco veces sacara al usuario de todas las consultas
            // ordinarias del sistema, y que se recuperara solo al cabo de unos minutos: R16 no se
            // levanta sola.
            migrationBuilder.Sql(
                """
                UPDATE identidad.usuarios
                SET bloqueado = (estado = 'Bloqueado'),
                    motivo_del_bloqueo = CASE WHEN estado = 'Bloqueado'
                                              THEN 'SupresionSolicitada' END,
                    bloqueado_en = CASE WHEN estado = 'Bloqueado'
                                        THEN COALESCE(bloqueado_en, now()) END
                """);

            migrationBuilder.AlterColumn<bool>(
                name: "bloqueado",
                schema: "identidad",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "estado",
                schema: "identidad",
                table: "usuarios");

            // --- 2. Las marcas de tiempo ---------------------------------------------------
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "modificado_en",
                schema: "identidad",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            // Aquí sí se sabe cuándo nació la fila, porque `creado_en` existe desde el 0.5. De un
            // usuario anterior al 0.10 no consta ninguna modificación, así que decir que se
            // modificó por última vez cuando se creó es exactamente lo que se puede afirmar; y es
            // como se ve cualquier fila recién dada de alta.
            migrationBuilder.Sql(
                """
                UPDATE identidad.usuarios
                SET modificado_en = creado_en
                WHERE modificado_en IS NULL
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "modificado_en",
                schema: "identidad",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "creado_en",
                schema: "identidad",
                table: "roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "modificado_en",
                schema: "identidad",
                table: "roles",
                type: "timestamp with time zone",
                nullable: true);

            // El rol sí empieza de cero: nunca tuvo fecha. Lo único cierto de una fila anterior
            // al 0.10 es que ya existía cuando esta migración corrió, y ese instante es la cota
            // superior más ajustada que se puede afirmar. `0001-01-01` —lo que ponía el
            // andamiaje— no dice «no se sabe», dice que el rol se creó en el año uno.
            migrationBuilder.Sql(
                """
                UPDATE identidad.roles
                SET creado_en = now(), modificado_en = now()
                WHERE creado_en IS NULL
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "creado_en",
                schema: "identidad",
                table: "roles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "modificado_en",
                schema: "identidad",
                table: "roles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "creado_en",
                schema: "identidad",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "modificado_en",
                schema: "identidad",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "modificado_en",
                schema: "identidad",
                table: "usuarios");

            // Deshacer no puede ser una forma de desbloquear en silencio: `estado` se rehace a
            // partir de `bloqueado` antes de que la columna desaparezca.
            migrationBuilder.AddColumn<string>(
                name: "estado",
                schema: "identidad",
                table: "usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE identidad.usuarios
                SET estado = CASE WHEN bloqueado THEN 'Bloqueado' ELSE 'Activo' END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                schema: "identidad",
                table: "usuarios",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "bloqueado",
                schema: "identidad",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "motivo_del_bloqueo",
                schema: "identidad",
                table: "usuarios");
        }
    }
}
