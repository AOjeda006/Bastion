using Bastion.BuildingBlocks.Application;

namespace Bastion.Organizacion.Application;

/// <summary>
/// La unidad de trabajo <b>de este módulo</b>: confirma sobre el contexto de Organización y sobre
/// ningún otro.
/// </summary>
/// <remarks>
/// <para>
/// Existe porque el contenedor resuelve por tipo, y con un único <see cref="IUnidadTrabajo"/> para
/// todos los módulos la última inscripción gana: los casos de uso de Organización acabarían
/// confirmando sobre el contexto de Identidad. Y no daría error —<c>SaveChangesAsync</c> sobre un
/// contexto que no rastrea nada devuelve cero y calla—, así que el alta contestaría <c>201</c> y
/// la fila no existiría.
/// </para>
/// <para>
/// Con una interfaz por módulo, el contenedor no tiene nada que adivinar y equivocarse deja de ser
/// posible: pedir la de otro módulo no compila, porque un módulo no ve la capa de aplicación
/// ajena (§4).
/// </para>
/// </remarks>
public interface IUnidadTrabajoDeOrganizacion : IUnidadTrabajo
{
    /// <summary>
    /// Ejecuta el trabajo <b>dentro de una transacción</b>, abriéndola si no la hay ya.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Existe desde el ítem 2.6 y por el cerrojo del ejercicio.</b> Cerrar lo toma en exclusiva
    /// sobre la fila —para que ninguna confirmación en vuelo se cuele entre la comprobación del
    /// estado y el <c>COMMIT</c>—, y un cerrojo <b>solo dura hasta el final de su
    /// transacción</b>. Sin una que abarque la lectura y el guardado, el cerrojo se soltaría al
    /// acabar su propia consulta y el cierre parecería protegido sin estarlo, que es peor que no
    /// protegerlo. Mover y borrar el ejercicio lo toman igual desde el cierre del 2.6, porque
    /// deciden por lo mismo: por qué documentos hay dentro.
    /// </para>
    /// <para>
    /// <b>Por qué no la abre el filtro de idempotencia, que es quien la abre en todo lo demás.</b>
    /// Cerrar exige <c>If-Match</c>, y ninguna acción pide los dos mecanismos a la vez: hay un caso
    /// que lo prohíbe con dos motivos escritos, y el duro es que la transacción de la idempotencia
    /// va sin puntos de guardado, así que un choque de concurrencia la deja abortada y el 412 que
    /// tocaba sale convertido en 500. Abrirla aquí no toca esa regla ni ese mecanismo.
    /// </para>
    /// <para>
    /// <b>Y si ya hay una, no abre otra</b>: anidar transacciones en EF Core lanza, y quien la
    /// abrió es quien tiene que cerrarla. Así el mismo caso de uso vale llamado desde una acción
    /// que sí pase por el filtro.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Lo que devuelve el trabajo.</typeparam>
    /// <param name="trabajo">Lo que se ejecuta dentro.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Lo que haya devuelto el trabajo.</returns>
    Task<T> EnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> trabajo, CancellationToken cancelacion);
}
