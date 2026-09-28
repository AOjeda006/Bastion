namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// Lo que se valora junto: un artículo en un almacén (ADR-0046 §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ni la ubicación ni el lote.</b> Cambiar una caja de estantería no cambia su valor, y un precio
/// medio por lote no es un medio: es el coste de cada lote, que es lo que haría FIFO.
/// </para>
/// <para>
/// <b>La empresa no va en la clave</b> porque una valoración se hace dentro de un documento, y un
/// documento es de una sola. La tabla sí la lleva, y la sentencia que bloquea filtra por ella.
/// </para>
/// <para>
/// <b>Es un valor de cálculo y no una referencia guardada.</b> Por eso es una estructura: los dos
/// identificadores ajenos que se guardan son los de la fila de la valoración, y esa entidad los
/// declara con su puerto como cualquier otra (R7).
/// </para>
/// </remarks>
/// <param name="ArticuloId">El artículo que se valora.</param>
/// <param name="AlmacenId">El almacén en el que está.</param>
public readonly record struct ClaveDeValoracion(Guid ArticuloId, Guid AlmacenId);
