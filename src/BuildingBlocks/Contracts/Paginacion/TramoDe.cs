namespace Bastion.BuildingBlocks.Contracts.Paginacion;

/// <summary>
/// Un tramo de resultados de una <b>búsqueda</b>, con por dónde seguir.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué esto y <see cref="PaginaDe{T}"/> son dos tipos y no uno.</b> Paginar y buscar son
/// dos formas de recorrer, y sus respuestas no se parecen: una página tiene número y total, un
/// tramo tiene cursor y no tiene ninguno de los dos. Meterlo todo en un tipo daría un contrato
/// con la mitad de los campos vacíos en cada respuesta —<c>Total = 0</c> en una búsqueda,
/// <c>CursorSiguiente = null</c> en un listado—, y un cliente no puede distinguir «vacío porque
/// esta forma no lo usa» de «vacío porque no hay». Un tipo que miente en cada respuesta es peor
/// que dos tipos con nombres que dicen lo que son. Decidido en el ítem 1.3.
/// </para>
/// <para>
/// <b>Sin total, y no por pereza.</b> Contar un conjunto filtrado cuesta un recorrido entero de
/// la tabla en cada tramo, que es justo lo que un cursor viene a evitar. Un listado ordinario sí
/// lo lleva porque su total es el de la tabla y sale barato (§9, «con el total cuando es barato
/// calcularlo»).
/// </para>
/// <para>
/// <b>El cursor es opaco y solo lleva POSICIÓN.</b> Ni el criterio de búsqueda ni ningún dato del
/// que buscó: el criterio lo reenvía el cliente en el cuerpo del siguiente <c>POST</c>. Si el
/// cursor llevara el criterio dentro, un NIF acabaría en una cadena que se copia, se comparte y
/// se escribe en un registro de acceso — que es exactamente lo que el ADR-0025 saca de la URL,
/// entrando por la puerta de al lado.
/// </para>
/// </remarks>
/// <typeparam name="T">Lo que hay en el tramo.</typeparam>
/// <param name="Elementos">Los de este tramo, en el orden del recorrido.</param>
/// <param name="Tamanio">Cuántos elementos se pidieron.</param>
/// <param name="CursorSiguiente">Con qué pedir el tramo siguiente, o nulo si no hay más.</param>
public sealed record TramoDe<T>(IReadOnlyList<T> Elementos, int Tamanio, string? CursorSiguiente);
