using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bastion.Organizacion.Infrastructure.Migrations
{
    /// <summary>
    /// Impide que dos ejercicios de la misma empresa se pisen en el calendario.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Qué faltaba.</b> La tabla ya tenía un único sobre <c>(empresa_id, anio)</c> —«un año, un
    /// ejercicio, por empresa»— y eso mira la <b>etiqueta</b>, no las fechas. Con él puesto,
    /// «2026 = enero a diciembre» y «2027 = julio de 2026 a junio de 2027» conviven sin error, y
    /// cualquier fecha del segundo semestre de 2026 cae en <b>dos</b> ejercicios. La frase que
    /// sostiene la R9 es «el ejercicio lo decide la fecha del movimiento», y una fecha que cae en
    /// dos ejercicios deja esa frase sin respuesta: el cierre no sabe qué congela, la numeración
    /// no sabe de qué serie tira y la autoliquidación no sabe en qué periodo entra.
    /// </para>
    /// <para>
    /// <b>Por qué un <c>EXCLUDE USING gist</c> y no una comprobación en el código.</b> La
    /// comprobación en el caso de uso existe —contesta un 409 con el motivo escrito en vez de un
    /// 500—, pero no puede ser la única: dos peticiones simultáneas preguntan las dos antes de que
    /// ninguna escriba, las dos se van satisfechas y el solape entra. Sólo el motor ve las dos a la
    /// vez. Es el mismo reparto, y el mismo operador, que en <c>tarifas_sin_tramos_solapados</c>.
    /// </para>
    /// <para>
    /// <b>El rango va cerrado por los dos lados</b> —<c>'[]'</c>—, que es lo que dice el dominio:
    /// <c>Comprende</c> incluye el primer día y el último. Con el <c>'[)'</c> por omisión, dos
    /// ejercicios que compartieran el 31 de diciembre pasarían por aquí. Y es <c>date</c>, no
    /// <c>timestamptz</c>: un ejercicio contable no tiene huso (R14), empieza el mismo día en
    /// Madrid y en Canarias.
    /// </para>
    /// <para>
    /// <b><c>empresa_id WITH =</c> va DENTRO de la restricción</b> (R8): sin esa columna, el
    /// ejercicio de una empresa impediría el de otra, y un inquilino podría bloquear a otro sin
    /// saber siquiera que existe.
    /// </para>
    /// <para>
    /// <b>Y si alguna base tuviera ya un solape, esto FALLA CERRADO</b>, que es lo que de verdad lo
    /// sostiene. No se deja al despliegue descubrirlo: <c>ALTER TABLE … ADD CONSTRAINT EXCLUDE</c>
    /// comprueba las filas existentes, y con un par solapado aborta con <c>23P01</c>, la migración
    /// entera se deshace, el historial no anota nada y el migrador sale con error. En el
    /// <c>compose</c> eso significa que la API <b>no arranca</b>, porque depende del migrador con
    /// <c>service_completed_successfully</c>. Un despliegue parado con los datos intactos es el
    /// desenlace bueno; el malo sería entrar con dos ejercicios pisándose y descubrirlo al cerrar
    /// el año.
    /// </para>
    /// <para>
    /// <b>Que esto aguante sobre una tabla con filas no lo ejerce ningún barrido</b>, y se dice
    /// para que nadie lo dé por hecho. <c>LasMigracionesSobreTablasConFilasTests</c> aplica cada
    /// migración sobre datos inventados, pero inventa <b>una</b> fila por tabla, y una fila no se
    /// solapa consigo misma: lo que ese barrido comprueba aquí es que la restricción se crea sobre
    /// una tabla que no está vacía, no que rechace lo que tiene que rechazar. Quien comprueba eso
    /// es el caso de integración que planta el par solapado contra PostgreSQL de verdad.
    /// </para>
    /// <para>
    /// <b>Quién se lo encuentre no fuerza la restricción</b>: busca los pares con
    /// <c>SELECT a.id, b.id FROM organizacion.ejercicios a JOIN organizacion.ejercicios b ON
    /// a.empresa_id = b.empresa_id AND a.id &lt; b.id AND daterange(a.fecha_de_inicio,
    /// a.fecha_de_fin, '[]') &amp;&amp; daterange(b.fecha_de_inicio, b.fecha_de_fin, '[]')</c> y
    /// decide qué intervalo se recorta. Cuál de los dos cede es una decisión contable —depende de
    /// dónde estén los asientos—, no de esquema, y por eso no la toma una migración.
    /// </para>
    /// </remarks>
    public partial class UnaFechaCaeEnUnSoloEjercicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // `btree_gist` hace falta porque el operador `=` sobre `uuid` no es GiST de serie.
            // Este módulo ya la crea en el 0.15 y las migraciones de un mismo módulo se aplican en
            // orden, así que aquí está garantizada; se repite porque `IF NOT EXISTS` es gratis y
            // porque así este fichero no depende de que alguien lea el otro para entenderlo.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(
                """
                ALTER TABLE organizacion.ejercicios
                    ADD CONSTRAINT ejercicios_sin_intervalos_solapados
                    EXCLUDE USING gist (
                        empresa_id WITH =,
                        daterange(fecha_de_inicio, fecha_de_fin, '[]') WITH &&
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Aquí sí hay que soltarla a mano: a diferencia de las tarifas, la restricción no se va
            // con su tabla porque la tabla no nace en esta migración.
            //
            // La extensión `btree_gist` se queda a propósito: es de la base, no de esta migración,
            // y el 0.15 se apoya en ella. Soltarla aquí rompería una restricción ajena por revertir
            // ésta.
            migrationBuilder.Sql(
                """
                ALTER TABLE organizacion.ejercicios
                    DROP CONSTRAINT ejercicios_sin_intervalos_solapados;
                """);
        }
    }
}
