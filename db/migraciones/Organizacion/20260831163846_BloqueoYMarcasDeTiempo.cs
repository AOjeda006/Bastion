using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Organizacion.Infrastructure.Migrations
{
    /// <summary>
    /// R16 y R14 en el esquema: el estado de bloqueo pasa de un enumerado por tabla a las tres
    /// columnas del tipo complejo compartido, y las cuatro tablas estrenan sus dos marcas de tiempo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Está escrita a mano y no como salió del andamiaje.</b> Lo generado empezaba tirando
    /// <c>estado</c> y creando <c>bloqueado</c> a <c>false</c>: sobre una tabla vacía da igual, y
    /// sobre una con filas desbloquea todo lo que estuviera bloqueado sin que nada se queje. El dato
    /// que se pierde ahí es justamente el que el artículo 32 de la LOPDGDD obliga a conservar.
    /// </para>
    /// <para>
    /// <b>Las columnas obligatorias nacen admitiendo nulo y se cierran después.</b> Un
    /// <c>NOT NULL</c> directo necesita un <c>DEFAULT</c> para las filas que ya existen, y ese
    /// <c>DEFAULT</c> se queda en la tabla aunque el modelo no lo declare: una diferencia entre el
    /// esquema y el modelo que ningún barrido ve, y encima una forma nueva de valor generado por el
    /// servidor en un modelo donde lo único que lo genera son los seis testigos de concurrencia
    /// (ADR-0015). Añadir, rellenar y cerrar deja la tabla exactamente como la describe el modelo.
    /// </para>
    /// </remarks>
    public partial class BloqueoYMarcasDeTiempo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // --- 1. El bloqueo -------------------------------------------------------------
            // `bloqueada_en` se RENOMBRA en vez de crearse y tirarse: es el instante del que
            // cuelga el plazo de prescripción del art. 32, y renombrar lo conserva. El almacén ya
            // la llamaba `bloqueado_en`, así que las tres tablas acaban con una sola grafía.
            migrationBuilder.RenameColumn(
                name: "bloqueada_en",
                schema: "organizacion",
                table: "empresas",
                newName: "bloqueado_en");

            migrationBuilder.AddColumn<bool>(
                name: "bloqueado",
                schema: "organizacion",
                table: "empresas",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_del_bloqueo",
                schema: "organizacion",
                table: "empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "bloqueado",
                schema: "organizacion",
                table: "almacenes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_del_bloqueo",
                schema: "organizacion",
                table: "almacenes",
                type: "text",
                nullable: true);

            // El motivo de las filas ya bloqueadas no se inventa: antes del 0.10 solo había un
            // camino que bloqueaba una empresa —la supresión del art. 32— y uno que bloqueaba un
            // almacén —el cese de uso, para no romper la valoración histórica—. Cada tabla hereda
            // el suyo.
            //
            // Y `bloqueado_en` se completa si faltara: una fila bloqueada sin fecha dejaría el
            // plazo del art. 32 sin punto de partida. Arrancarlo hoy es el lado que protege el
            // dato más tiempo, que es el error que se puede cometer aquí.
            migrationBuilder.Sql(
                """
                UPDATE organizacion.empresas
                SET bloqueado = (estado = 'Bloqueada'),
                    motivo_del_bloqueo = CASE WHEN estado = 'Bloqueada'
                                              THEN 'SupresionSolicitada' END,
                    bloqueado_en = CASE WHEN estado = 'Bloqueada'
                                        THEN COALESCE(bloqueado_en, now()) END
                """);

            migrationBuilder.Sql(
                """
                UPDATE organizacion.almacenes
                SET bloqueado = (estado = 'Bloqueado'),
                    motivo_del_bloqueo = CASE WHEN estado = 'Bloqueado'
                                              THEN 'CeseDeUso' END,
                    bloqueado_en = CASE WHEN estado = 'Bloqueado'
                                        THEN COALESCE(bloqueado_en, now()) END
                """);

            migrationBuilder.AlterColumn<bool>(
                name: "bloqueado",
                schema: "organizacion",
                table: "empresas",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "bloqueado",
                schema: "organizacion",
                table: "almacenes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            // Ya no queda nada que leer de la columna vieja. `ejercicios.estado` y `series.estado`
            // NO se tocan: abierto/cerrado y activa/cerrada son estados de negocio, no son R16.
            migrationBuilder.DropColumn(
                name: "estado",
                schema: "organizacion",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "estado",
                schema: "organizacion",
                table: "almacenes");

            // --- 2. Las marcas de tiempo ---------------------------------------------------
            foreach (string tabla in new[] { "empresas", "ejercicios", "series", "almacenes" })
            {
                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "creado_en",
                    schema: "organizacion",
                    table: tabla,
                    type: "timestamp with time zone",
                    nullable: true);

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "modificado_en",
                    schema: "organizacion",
                    table: tabla,
                    type: "timestamp with time zone",
                    nullable: true);

                // Cuándo se creó una fila anterior al 0.10 no lo sabe nadie: la columna no
                // existía. Lo único cierto es que ya existía cuando esta migración corrió, así
                // que ese instante es la cota superior más ajustada que se puede afirmar.
                //
                // La alternativa del andamiaje era `0001-01-01`, que no es «no se sabe» sino una
                // fecha: cualquier informe de antigüedad diría que la empresa se dio de alta hace
                // dos mil años. Todas las filas viejas comparten aquí el mismo instante al
                // milisegundo, y esa coincidencia es precisamente la señal de que el valor está
                // derivado y no observado.
                //
                // `modificado_en` arranca igual a `creado_en`: es como se ve una fila que no se
                // ha tocado desde que nació, que es todo lo que se puede decir de ella.
                migrationBuilder.Sql(
                    $"""
                    UPDATE organizacion.{tabla}
                    SET creado_en = now(), modificado_en = now()
                    WHERE creado_en IS NULL
                    """);

                migrationBuilder.AlterColumn<DateTimeOffset>(
                    name: "creado_en",
                    schema: "organizacion",
                    table: tabla,
                    type: "timestamp with time zone",
                    nullable: false,
                    oldClrType: typeof(DateTimeOffset),
                    oldType: "timestamp with time zone",
                    oldNullable: true);

                migrationBuilder.AlterColumn<DateTimeOffset>(
                    name: "modificado_en",
                    schema: "organizacion",
                    table: tabla,
                    type: "timestamp with time zone",
                    nullable: false,
                    oldClrType: typeof(DateTimeOffset),
                    oldType: "timestamp with time zone",
                    oldNullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            foreach (string tabla in new[] { "empresas", "ejercicios", "series", "almacenes" })
            {
                migrationBuilder.DropColumn(name: "creado_en", schema: "organizacion", table: tabla);
                migrationBuilder.DropColumn(name: "modificado_en", schema: "organizacion", table: tabla);
            }

            // La vuelta atrás rehace `estado` a partir de `bloqueado` por el mismo motivo por el
            // que la ida lo hizo al revés: deshacer una migración no puede ser una forma de
            // desbloquear en silencio lo que estaba bloqueado.
            migrationBuilder.AddColumn<string>(
                name: "estado",
                schema: "organizacion",
                table: "empresas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado",
                schema: "organizacion",
                table: "almacenes",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE organizacion.empresas
                SET estado = CASE WHEN bloqueado THEN 'Bloqueada' ELSE 'Activa' END
                """);

            migrationBuilder.Sql(
                """
                UPDATE organizacion.almacenes
                SET estado = CASE WHEN bloqueado THEN 'Bloqueado' ELSE 'Activo' END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                schema: "organizacion",
                table: "empresas",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "estado",
                schema: "organizacion",
                table: "almacenes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "bloqueado",
                schema: "organizacion",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "motivo_del_bloqueo",
                schema: "organizacion",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "bloqueado",
                schema: "organizacion",
                table: "almacenes");

            migrationBuilder.DropColumn(
                name: "motivo_del_bloqueo",
                schema: "organizacion",
                table: "almacenes");

            migrationBuilder.RenameColumn(
                name: "bloqueado_en",
                schema: "organizacion",
                table: "empresas",
                newName: "bloqueada_en");
        }
    }
}
