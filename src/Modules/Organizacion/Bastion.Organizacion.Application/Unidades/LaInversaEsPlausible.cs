using System.Globalization;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>
/// Si la fila inversa está declarada, su factor tiene que ser una inversa plausible del directo
/// (ADR-0023, decisión 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>El hueco que cierra.</b> Los dos sentidos de una conversión son dos filas independientes —a
/// propósito: el inverso de 12 no cabe en seis decimales, así que la vuelta se declara con su
/// propio redondeo pensado—. Lo que faltaba era el límite: se podía declarar <c>caja→ud = 12</c> y
/// <c>ud→caja = 0,5</c>, y las dos pasaban todas las comprobaciones que había, porque cada factor
/// por separado es positivo y cae en el rango. Un inventario valorado con esas dos filas cuadra
/// por un lado y descuadra por el otro, <b>sin un solo error</b>.
/// </para>
/// <para>
/// <b>La tolerancia no está elegida: la impone la escala.</b> Cada factor se guarda redondeado a
/// seis decimales, así que arrastra hasta media unidad del último decimal —<c>5·10⁻⁷</c>— de
/// error; propagado al producto, el margen es <c>5·10⁻⁷·(f + g)</c> y ni uno más, y sale de los
/// decimales del factor en vez de estar escrito. Un 1 % o un
/// 0,1 % serían números elegidos por comodidad, y cualquier número elegido por comodidad admite
/// discrepancias reales o rechaza redondeos legítimos.
/// </para>
/// <para>
/// <b>Vive en la capa de aplicación y no en el dominio.</b> La regla relaciona <b>dos instancias
/// distintas</b> del agregado, y la R12 dice una transacción, un agregado: un invariante de
/// dominio que necesitara cargar otro agregado sería justo la grieta que la R12 cierra. Tampoco es
/// una restricción de la base, que tendría que mirar otra fila y por tanto ser un disparador, con
/// el coste y la invisibilidad que tienen. El fallo es un error de negocio con nombre, no una
/// excepción (ADR-0004).
/// </para>
/// <para>
/// <b>Se comprueba en el alta y en la modificación</b>, y lo segundo no es un extra. Si se da de
/// alta <c>A→B</c>, luego <c>B→A</c>, y después alguien <b>modifica</b> la primera, el par vuelve a
/// poder contradecirse: una comprobación que solo mirase el alta deja pasar exactamente el camino
/// por el que se rompe, y lo deja pasar en verde.
/// </para>
/// <para>
/// <b>Y la fila retirada cuenta.</b> Decisión 2 del ítem 1.7: «sigue resolviendo» significa que
/// sigue siendo verdad. Si retirar una fila la sacara de esta comprobación, retirar el sentido
/// incómodo sería la manera de declarar cualquier número en el otro — una salida que la propia
/// regla ofrece no es una salida, es un agujero con permiso.
/// </para>
/// </remarks>
internal static class LaInversaEsPlausible
{
    /// <summary>
    /// Error que arrastra cada factor por estar redondeado: media unidad de su último decimal.
    /// </summary>
    /// <remarks>
    /// <b>Se calcula, no se escribe.</b> Con seis decimales sale <c>5·10⁻⁷</c>, que es el número
    /// que cita el ADR; escribirlo como literal dejaría dos sitios que tienen que decir lo mismo
    /// —este y <see cref="ConversionUM.DecimalesDelFactor"/>— sin nada que los ate. El día que la
    /// escala cambiara, el literal seguiría compilando, seguiría en verde, y la tolerancia pasaría
    /// a ser un número elegido por nadie: demasiado ancha admitiría discrepancias reales,
    /// demasiado estrecha rechazaría redondeos legítimos. <c>new decimal(5, 0, 0, false, escala)</c>
    /// es <c>5</c> con la coma corrida esa escala, exacto y sin coma flotante por el medio.
    /// </remarks>
    public static readonly decimal MargenPorFactor =
        new(5, 0, 0, isNegative: false, (byte)(ConversionUM.DecimalesDelFactor + 1));

    /// <summary>Código del error con el que se rechaza una inversa que no lo es.</summary>
    public const string CodigoDeInversaImplausible = "conversion-um-inversa-implausible";

    /// <summary>
    /// Comprueba el par contra la inversa declarada, si la hay.
    /// </summary>
    /// <param name="inversa">La fila del sentido contrario, o nula si no está declarada.</param>
    /// <param name="factor">El factor que se quiere guardar en este sentido.</param>
    internal static Resultado Comprobar(ConversionUM? inversa, decimal factor)
    {
        // Sin fila inversa no hay nada que contradecir: el ADR concede la libertad de declarar un
        // solo sentido, y lo que acota es la relación entre los dos cuando existen los dos.
        if (inversa is null)
        {
            return Resultado.Correcto();
        }

        return Casan(factor, inversa.Factor)
            ? Resultado.Correcto()
            : Resultado.Fallo(ErrorDeOperacion.Conflicto(
                CodigoDeInversaImplausible,
                $"El sentido contrario está declarado con factor " +
                $"{Texto(inversa.Factor)}, y {Texto(factor)} no es su inversa: " +
                $"{Texto(factor)} × {Texto(inversa.Factor)} tendría que dar 1 con un margen de " +
                $"{Texto(Margen(factor, inversa.Factor))}, y da " +
                $"{Texto(factor * inversa.Factor)}. La libertad que da declarar los dos sentidos " +
                "por separado es la de elegir el redondeo, no la de declarar otro número: con los " +
                "dos así, el mismo inventario cuadra por un lado y descuadra por el otro."));
    }

    /// <summary>
    /// La desigualdad del ADR: <c>|f · g − 1| ≤ 5·10⁻⁷·(f + g)</c>, con <c>f</c> y <c>g</c> ya
    /// redondeados a seis decimales.
    /// </summary>
    /// <remarks>
    /// <b>Es <c>≤</c> y no <c>&lt;</c></b>: el margen es el error máximo que la escala puede
    /// producir, así que un par que se separe <b>exactamente</b> lo que la escala explica es un
    /// redondeo legítimo, no una discrepancia. Con <c>&lt;</c>, el caso frontera —el que la escala
    /// produce de verdad— se rechazaría, que es donde una desigualdad se equivoca de signo.
    /// </remarks>
    /// <param name="factor">El factor directo.</param>
    /// <param name="inverso">El factor del sentido contrario.</param>
    internal static bool Casan(decimal factor, decimal inverso) =>
        Math.Abs((factor * inverso) - 1m) <= Margen(factor, inverso);

    private static decimal Margen(decimal factor, decimal inverso) =>
        MargenPorFactor * (factor + inverso);

    // Cultura invariante: el mismo despliegue tiene que escribir el mismo número en un servidor en
    // castellano y en uno en inglés. El texto lo lee una persona, pero el `type` del ProblemDetails
    // es lo que ramifica un cliente, y ese no depende de esto.
    private static string Texto(decimal numero) =>
        numero.ToString(CultureInfo.InvariantCulture);
}
