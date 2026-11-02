using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Compras.Infrastructure.Migrations
{
    /// <summary>
    /// El esquema `compras`, vacío: el primer documento del módulo es el pedido del 3.7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El `EnsureSchema` está escrito a mano.</b> El andamiaje salió con los dos métodos vacíos,
    /// porque EF Core solo pide el esquema cuando crea una tabla en él, y este contexto no tiene
    /// ninguna propia: las cuatro que mapea son de Auditoría y no se migran desde aquí. Sin esta
    /// línea el esquema existiría igual, porque lo crea el historial al aplicar la migración, pero
    /// sería un efecto de dónde vive el historial y no algo que diga una migración.
    /// </para>
    /// <para>
    /// <b>El `Down` no lo borra.</b> El historial de este contexto vive dentro del esquema, y EF
    /// Core no lo quita al deshacer, así que un `DROP SCHEMA` sin <c>CASCADE</c> fallaría siempre;
    /// y con él se llevaría el historial, que es lo que dice qué hay aplicado. Las demás
    /// migraciones iniciales del sistema tampoco borran su esquema.
    /// </para>
    /// </remarks>
    /// <inheritdoc />
    public partial class EsquemaInicialDeCompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compras");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
