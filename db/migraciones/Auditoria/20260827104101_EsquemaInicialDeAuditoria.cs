using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Auditoria.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicialDeAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auditoria");

            migrationBuilder.CreateTable(
                name: "registros",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocurrido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sin_inquilino = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entidad = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entidad_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    cambio = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    valores = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registros", x => x.id);
                    table.CheckConstraint("ck_registros_empresa_o_motivo", "(empresa_id IS NULL) <> (sin_inquilino IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_registros_correlacion_id",
                schema: "auditoria",
                table: "registros",
                column: "correlacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_registros_entidad_entidad_id",
                schema: "auditoria",
                table: "registros",
                columns: new[] { "entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "ix_registros_ocurrido_en",
                schema: "auditoria",
                table: "registros",
                column: "ocurrido_en");

            // LO QUE HACE QUE «SOLO SE ANADE» SEA VERDAD.
            //
            // Un REVOKE no vale: los permisos los da y los quita el mismo duenno de la tabla, que
            // es el usuario con el que se conecta la aplicacion. Un permiso que el interesado
            // puede devolverse a si mismo es una frase, no una guarda. Esto lo rechaza el MOTOR,
            // para todo el mundo, venga por donde venga.
            //
            // Y no, esto no es logica de negocio en un disparador: es una restriccion de
            // integridad, de la misma familia que un CHECK. La diferencia con un CHECK es que
            // PostgreSQL no sabe expresar «esta fila no se puede cambiar» de otra manera. No
            // decide nada, no calcula nada y no depende de ningun dato: mira la OPERACION y la
            // rechaza. El dia que alguien lea esto con el antipatron en la mano —«nada de logica
            // en disparadores»— que lea tambien esta linea.
            //
            // Dos disparadores y no uno: los de fila no ven un TRUNCATE, que es precisamente la
            // orden que se usaria para vaciar la tabla de un golpe.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION auditoria.registros_son_de_solo_anadido() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION
                        'auditoria.registros es de solo anadido: % no esta permitido sobre esta tabla.',
                        TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER registros_sin_modificar_ni_borrar
                    BEFORE UPDATE OR DELETE ON auditoria.registros
                    FOR EACH ROW EXECUTE FUNCTION auditoria.registros_son_de_solo_anadido();
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER registros_sin_vaciar
                    BEFORE TRUNCATE ON auditoria.registros
                    FOR EACH STATEMENT EXECUTE FUNCTION auditoria.registros_son_de_solo_anadido();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registros",
                schema: "auditoria");

            // Los disparadores se van con la tabla; la funcion no, y una funcion huerfana haria
            // fallar el siguiente `Up` con un «ya existe».
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS auditoria.registros_son_de_solo_anadido();");
        }
    }
}
