using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Ubicaciones;

/// <summary>
/// Lo que otros módulos pueden preguntar sobre las ubicaciones de un almacén.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4, igual que la del almacén: interfaz del <c>Contracts</c> del
/// dueño, resuelta en proceso.
/// </para>
/// <para>
/// <b>Recibe también el almacén, y no es comodidad.</b> Cada movimiento de existencias apunta a un
/// almacén <b>y</b> a una ubicación, y que la segunda esté dentro del primero es una condición que
/// quien pregunta <b>no puede comprobar por su cuenta</b>: los dos datos viven en
/// <c>organizacion</c> y ninguna consulta cruza esquemas (§5, regla 4). O lo contesta este puerto,
/// o no lo contesta nadie y el libro admite movimientos con la mercancía en una nave y el hueco en
/// otra.
/// </para>
/// <para>
/// <b>La ubicación hereda el estado de su almacén, y el suyo propio solo puede empeorarlo</b>
/// (ADR-0037, §4). Una estantería no sigue admitiendo mercancía porque nadie se acordara de
/// bloquearla cuando se cerró la nave que la contiene: el género tendría que entrar físicamente
/// por una puerta cerrada. Bloquear un almacén <b>no</b> toca sus ubicaciones, así que la fila de
/// una ubicación activa dentro de un almacén bloqueado existe de verdad y esta es la respuesta que
/// se le da.
/// </para>
/// </remarks>
public interface IConsultaDeUbicaciones
{
    /// <summary>En qué estado está esa ubicación de ese almacén.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="EstadoDeMaestro.NoExiste"/> cubre tres situaciones y las cubre a propósito: no
    /// hay ubicación con ese identificador; la hay, pero <b>cuelga de otro almacén</b>; o la hay,
    /// pero es de otra empresa (R8). La segunda es la respuesta correcta y no una simplificación:
    /// la pregunta es «qué hay en este almacén con este identificador», y ahí no hay nada.
    /// </para>
    /// <para>
    /// Que el almacén no pueda aportar <see cref="EstadoDeMaestro.NoExiste"/> por su cuenta es un
    /// hecho del esquema: hay clave ajena de <c>ubicaciones.almacen_id</c> con <c>Restrict</c>, así
    /// que una ubicación sin almacén no existe.
    /// </para>
    /// </remarks>
    /// <param name="almacenId">Almacén dentro del cual se pregunta.</param>
    /// <param name="ubicacionId">Identificador de la ubicación.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDeMaestro> EstadoDeAsync(
        Guid almacenId, Guid ubicacionId, CancellationToken cancelacion);

    /// <summary>El código y la descripción de cada ubicación, en una sola consulta.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nombra lo que enseña su ficha</b>, la de
    /// <c>GET /api/v1/organizacion/ubicaciones/{id}</c>, con sus mismos filtros (ADR-0074): una
    /// de otra empresa no vuelve, y una <b>bloqueada</b> tampoco. Una ubicación de un almacén
    /// bloqueado sí vuelve, porque su ficha la enseña: el filtro mira su propio bloqueo.
    /// </para>
    /// <para>
    /// <b>No recibe el almacén</b>, al revés que <see cref="EstadoDeAsync"/>: aquel decide si
    /// un movimiento puede apuntar a ese hueco de esa nave, y este solo dice cómo se llama. El
    /// nombre de una ubicación sin descripción es <c>null</c>.
    /// </para>
    /// <para>
    /// <b>No mira el permiso de quien pregunta</b>: lo mira quien pregunta, antes de preguntar, como
    /// la pantalla. Lo que no vuelve, se escribe con su identificador.
    /// </para>
    /// </remarks>
    /// <param name="ubicaciones">Las ubicaciones por las que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El código y el nombre de cada una que vuelve.</returns>
    Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombresDeAsync(
        IReadOnlyCollection<Guid> ubicaciones, CancellationToken cancelacion);
}
