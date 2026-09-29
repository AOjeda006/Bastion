using Bastion.BuildingBlocks.Application;

namespace Bastion.Catalogo.Application;

/// <summary>
/// La unidad de trabajo <b>de este módulo</b>: confirma sobre el contexto de Catálogo y sobre
/// ningún otro.
/// </summary>
/// <remarks>
/// Una interfaz por módulo por lo mismo que en los otros tres: el contenedor resuelve por tipo y,
/// con un único <see cref="IUnidadTrabajo"/> para todos, la última inscripción gana. El fallo no
/// sería ruidoso —<c>SaveChangesAsync</c> sobre un contexto que no rastrea nada devuelve cero y
/// calla—, así que el alta contestaría <c>201</c> y la fila no existiría.
/// </remarks>
public interface IUnidadTrabajoDeCatalogo : IUnidadTrabajo
{
    /// <summary>
    /// Ejecuta el trabajo <b>dentro de una transacción</b>, abriéndola si no la hay ya.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Existe desde el ítem 2.9 y por el cerrojo del artículo</b>, igual que la de Organización
    /// existe por el del ejercicio. Cambiar la marca de trazabilidad bloquea la fila del artículo
    /// antes de preguntar a Inventario si tiene movimientos (ADR-0048 §4), y un cerrojo <b>solo
    /// dura hasta el final de su transacción</b>. Sin una que abarque el cerrojo, la pregunta y el
    /// guardado, se soltaría al acabar su propia consulta y la marca parecería protegida sin
    /// estarlo.
    /// </para>
    /// <para>
    /// <b>La abre el caso de uso y no el filtro de idempotencia</b>, por lo mismo que cerrar el
    /// ejercicio: modificar un artículo exige <c>If-Match</c>, y ninguna acción pide los dos
    /// mecanismos a la vez.
    /// </para>
    /// <para>
    /// <b>Y si ya hay una, no abre otra</b>: anidar transacciones en EF Core lanza, y quien la
    /// abrió es quien tiene que cerrarla.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Lo que devuelve el trabajo.</typeparam>
    /// <param name="trabajo">Lo que se ejecuta dentro.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Lo que haya devuelto el trabajo.</returns>
    Task<T> EnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> trabajo, CancellationToken cancelacion);
}
