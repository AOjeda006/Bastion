using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// Marca un caso como el que afirma que <b>este</b> puerto contesta <b>este</b> estado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la mitad descubrible de la matriz.</b> <see cref="EstadoDeMaestro"/> es una lista cerrada
/// y los puertos que la producen son un conjunto abierto; sin nada que los relacione, un valor del
/// enumerado puede quedarse sin ningún productor que lo alcance —que es lo que pasó con
/// <see cref="EstadoDeMaestro.SoloResuelveLoViejo"/>, inalcanzable para divisas y unidades desde
/// que el enumerado se escribió—. Un atributo sobre el caso que lo afirma convierte esa relación
/// en algo que se puede recorrer, y <c>LaMatrizDePuertoYEstadoTests</c> la recorre.
/// </para>
/// <para>
/// <b>Sobre el caso y no en una lista aparte</b>, a propósito. Una lista de «casillas cubiertas»
/// escrita a mano es una lista más que mantener, y ya se ha visto dos veces en este proyecto lo
/// que le pasa a una lista escrita a mano: se queda vieja en verde. Aquí la cobertura no se
/// declara: se marca donde está la aserción, así que borrar el caso borra el cubrimiento y la
/// matriz se pone roja por la casilla que se quedó sin nadie.
/// </para>
/// <para>
/// Lo que este atributo <b>no</b> promete es que la aserción de dentro sea la correcta —eso no lo
/// puede prometer ninguna marca—. Promete que la casilla tiene dueño, que el dueño existe, que es
/// un caso de verdad y que corre contra la base. Que además afirme lo que dice lo sostiene la
/// mutación que quita la retirada: los dos casos marcados se ponen rojos.
/// </para>
/// </remarks>
/// <param name="puerto">La interfaz del puerto, tal como la publica el <c>Contracts</c>.</param>
/// <param name="estado">El estado que este caso afirma que ese puerto contesta.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class CubreEstadoDeMaestroAttribute(Type puerto, EstadoDeMaestro estado) : Attribute
{
    /// <summary>La interfaz del puerto cuya respuesta se afirma.</summary>
    public Type Puerto { get; } = puerto;

    /// <summary>El estado que ese puerto contesta en este caso.</summary>
    public EstadoDeMaestro Estado { get; } = estado;
}
