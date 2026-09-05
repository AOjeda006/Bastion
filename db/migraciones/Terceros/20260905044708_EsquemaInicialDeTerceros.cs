using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Terceros.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicialDeTerceros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "terceros");

            migrationBuilder.CreateTable(
                name: "terceros",
                schema: "terceros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    razon_social = table.Column<string>(type: "text", nullable: false),
                    nombre_comercial = table.Column<string>(type: "text", nullable: true),
                    es_cliente = table.Column<bool>(type: "boolean", nullable: false),
                    es_proveedor = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    bloqueado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_del_bloqueo = table.Column<string>(type: "text", nullable: true),
                    domicilio_fiscal_calle = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    domicilio_fiscal_codigo_postal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    domicilio_fiscal_numero = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    domicilio_fiscal_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    domicilio_fiscal_poblacion = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    domicilio_fiscal_subdivision = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    identificacion_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identificacion_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    identificacion_verificacion = table.Column<string>(type: "text", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terceros", x => x.id);
                });

            // ESCRITO A MANO, Y ESO ES LA DECISIÓN DEL ÍTEM, NO UN APAÑO.
            //
            // A mano porque EF Core 10 no sabe indexar los miembros de un tipo complejo: el
            // selector responde «no es una expresión de acceso a miembro válida» y los nombres
            // responden «la propiedad "Identificacion.Pais" no se puede añadir al tipo "Tercero"».
            // Degradar la identificación a tipo poseído la habría hecho indexable y se ha
            // descartado: un poseído lleva identidad sintetizada y se sigue como una entidad
            // (ADR-0016). Antes falsear el modelo que el mapeo.
            //
            // Y lo que hay que mirar dos veces es lo que esta llamada NO tiene: no tiene `filter`.
            // La unicidad abarca también a los terceros bloqueados. Con un índice parcial sobre los
            // activos, bloquear una ficha liberaría su identificador, otra podría ocuparlo, y al
            // desbloquear la primera habría dos filas con la misma llave: una colisión a resolver a
            // mano, en el peor momento y con datos personales por medio. El precio de abarcarlo
            // todo es que el alta contra un tercero bloqueado choca — que es justo el conflicto que
            // el ítem exige que NO revele contra cuál chocó.
            //
            // Como el modelo no conoce este índice, `has-pending-model-changes` no puede echarlo en
            // falta si alguien lo borra. Quien lo vigila es el test de esquema, que lo busca en la
            // base y afirma además que su predicado parcial está vacío.
            //
            // El país va DENTRO de la llave: el mismo número identifica a personas distintas en
            // países distintos, y sin él chocarían dos terceros que no tienen nada que ver.
            migrationBuilder.CreateIndex(
                name: "ix_terceros_empresa_id_identificacion_pais_numero",
                schema: "terceros",
                table: "terceros",
                columns: new[] { "empresa_id", "identificacion_pais", "identificacion_numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terceros",
                schema: "terceros");
        }
    }
}
