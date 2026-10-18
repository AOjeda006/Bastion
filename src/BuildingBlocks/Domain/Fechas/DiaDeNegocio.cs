namespace Bastion.BuildingBlocks.Domain.Fechas;

/// <summary>
/// El día de negocio de un instante: el único sitio del backend que convierte un instante en el
/// día al que pertenece. Hoy es el día UTC.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un solo sitio.</b> «Hoy» decide qué se acepta y qué no: la fecha de operación que se
/// rechaza por futura, el tramo de impuesto que se propone, el precio que se resuelve, el saldo que
/// se contesta sin día y el mes al que tiene que llegar el corte. Hasta el 2.16 lo calculaban
/// dieciséis sitios, cada uno con su <c>DateOnly.FromDateTime</c>, en Catálogo, Inventario y
/// Terceros. El día que la regla cambie —la zona del almacén, antes de la fase 5, como decidió la
/// respuesta 2 del repaso de la fase 2—, cambia aquí y en ningún otro sitio.
/// </para>
/// <para>
/// <b>Y por qué el UTC, todavía.</b> Una fecha de negocio no lleva zona (R14), y la zona que la
/// decide es la del sitio donde pasa, no la de la empresa. Mientras el almacén no lleve la suya,
/// todo instante del sistema es UTC, y su día también. En España eso deja una o dos horas de
/// madrugada en las que aquí ya es mañana y en UTC todavía no: la nota de la zona, en
/// <c>docs/PLAN.md</c>, dice qué cuesta y qué zona regirá para lo que no tiene almacén.
/// </para>
/// <para>
/// <b>Lo vigila un test de arquitectura</b>, que busca en el código compilado de todo el backend
/// las llamadas a <c>DateOnly.FromDateTime</c> y solo admite las de esta clase. Su pareja es la
/// que afirma que aquí sí existe.
/// </para>
/// </remarks>
public static class DiaDeNegocio
{
    /// <summary>El día de negocio al que pertenece un instante.</summary>
    /// <param name="instante">El instante. Su desplazamiento no cuenta: se lee en UTC.</param>
    /// <returns>El día UTC del instante.</returns>
    public static DateOnly De(DateTimeOffset instante) =>
        DateOnly.FromDateTime(instante.UtcDateTime);

    /// <summary>El día de negocio de ahora, según el reloj.</summary>
    /// <param name="reloj">El reloj del sistema, o el de un test.</param>
    /// <returns>El día UTC del instante que da el reloj.</returns>
    public static DateOnly Hoy(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        return De(reloj.GetUtcNow());
    }
}
