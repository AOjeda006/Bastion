using Bastion.BuildingBlocks.Application;

namespace Bastion.Inventario.Application;

/// <summary>La unidad de trabajo del módulo Inventario.</summary>
/// <remarks>
/// Es propia del módulo, como en los demás: un <c>IUnidadTrabajo</c> compartido haría que
/// confirmar un ajuste guardara de paso lo que otro módulo tuviera a medias en la misma petición.
/// </remarks>
public interface IUnidadTrabajoDeInventario : IUnidadTrabajo
{
    /// <summary>
    /// Ejecuta el trabajo <b>dentro de una transacción</b>, abriéndola si no la hay ya.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Existe desde el ítem 2.12 y por el cerrojo del recuento</b>, como la de Catálogo existe
    /// por el del artículo. Toda escritura en una línea bloquea antes la fila del recuento, mira que
    /// siga en curso y la toca (ADR-0055 §4), y un cerrojo <b>solo dura hasta el final de su
    /// transacción</b>. Sin una que abarque el cerrojo, la lectura y el guardado, se soltaría al
    /// acabar su propia consulta, y dos personas que cuentan a la vez se pisarían la versión de la
    /// cabecera.
    /// </para>
    /// <para>
    /// <b>La abre el caso de uso y no el filtro de idempotencia</b>: contar y quitar exigen
    /// <c>If-Match</c> y no admiten la clave, así que el filtro no abre nada. Añadir sí la admite, y
    /// con ella la transacción es la del filtro.
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
