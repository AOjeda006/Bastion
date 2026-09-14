namespace Bastion.Api.IntegrationTests.Cruces;

/// <summary>
/// Marca un caso como el que afirma que <b>este</b> puerto contesta <b>este</b> estado, para
/// cualquier puerto de estado de cualquier <c>Contracts</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el <c>CubreEstadoDeMaestro</c> de Organización con el enumerado sin fijar</b>, y existe
/// porque aquel no podía ver los puertos del ítem 1.10. Su matriz descubre puertos solo en el
/// ensamblado de <c>EstadoDeMaestro</c> y solo si devuelven ese tipo: <c>IConsultaDeTerceros</c> y
/// <c>IConsultaDeTarifas</c> devuelven cada uno su propio enumerado, viven en otros dos
/// <c>Contracts</c>, y habrían entrado sin que ninguna casilla suya se echara en falta.
/// </para>
/// <para>
/// <b>El estado va como <c>object</c></b> porque un atributo no admite parámetros genéricos, y
/// cada puerto contesta un enumerado distinto. Que el valor sea de verdad del enumerado del puerto
/// que se nombra al lado no lo garantiza el compilador: lo comprueba
/// <c>LaMatrizDeLosPuertosDeEstadoTests</c>, y una marca con el valor de otro enumerado se lee allí
/// como lo que es, una casilla que no existe.
/// </para>
/// <para>
/// <b>Vive en este ensamblado y no en uno común</b> porque este es el único proyecto de pruebas
/// que ve la infraestructura de todos los módulos, así que es el único donde puede estar el caso
/// que afirma el estado de un puerto de cualquiera de ellos contra la base.
/// </para>
/// </remarks>
/// <param name="puerto">La interfaz del puerto, tal como la publica su <c>Contracts</c>.</param>
/// <param name="estado">El valor del enumerado de ese puerto que este caso afirma.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class CubreEstadoDelPuertoAttribute(Type puerto, object estado) : Attribute
{
    /// <summary>La interfaz del puerto cuya respuesta se afirma.</summary>
    public Type Puerto { get; } = puerto;

    /// <summary>El estado que ese puerto contesta en este caso.</summary>
    public object Estado { get; } = estado;
}
