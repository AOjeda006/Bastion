namespace Bastion.BuildingBlocks.Domain.Fechas;

/// <summary>
/// El día de negocio de un instante en una zona: el único sitio del backend que convierte un
/// instante en el día al que pertenece, y un día en el instante en que empieza.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un solo sitio.</b> «Hoy» decide qué se acepta y qué no: la fecha de operación que se
/// rechaza por futura, el tramo de impuesto que se propone, el precio que se resuelve, el saldo que
/// se contesta sin día y el mes al que tiene que llegar el corte. Hasta el 2.16 lo calculaban
/// dieciséis sitios, cada uno con su <c>DateOnly.FromDateTime</c>, en Catálogo, Inventario y
/// Terceros. Hasta el 3.5 el día era el UTC, y cuando la regla cambió a la zona del almacén
/// (ADR-0079), cambió aquí y en ningún otro sitio.
/// </para>
/// <para>
/// <b>Siempre con su zona.</b> Una fecha de negocio no lleva zona (R14), y la zona que la decide es
/// la del sitio donde pasa: la del almacén, y la de la empresa para lo que no tiene almacén. Ningún
/// método de aquí da un día sin que se le diga en qué zona, así que quien lo pide tiene que saber
/// cuál le toca, y no hay un valor por omisión que lo decida por él. La zona se pide al puerto de
/// las zonas, y se lee al comprobar: cambiarla cambia lo que se acepta desde ese momento, y lo que
/// ya está fechado no se mueve.
/// </para>
/// <para>
/// <b>Lo que abarca varias zonas</b> no tiene un día, sino uno por zona, y dos maneras de elegir:
/// <see cref="ElMasAtrasado"/>, para lo que tiene que haber terminado en todas —un mes está cerrado
/// cuando ha terminado en todas sus zonas—, y <see cref="ElMasAdelantado"/>, para lo que no puede
/// llamar futuro a nada que ya sea hoy en alguna.
/// </para>
/// <para>
/// <b>Lo vigila un test de arquitectura</b>, que busca en el código compilado de todo el backend
/// las llamadas a <c>DateOnly.FromDateTime</c> y a las conversiones de <c>TimeZoneInfo</c>, y solo
/// las admite aquí. Su pareja es la que afirma que aquí sí están, y otro caso, que todo lo de aquí
/// que da un día recibe la zona.
/// </para>
/// </remarks>
public static class DiaDeNegocio
{
    /// <summary>El día al que pertenece un instante en una zona.</summary>
    /// <param name="instante">El instante. Su desplazamiento no cuenta: cuenta el de la zona.</param>
    /// <param name="zona">La zona del sitio donde pasa.</param>
    /// <returns>El día de la zona en ese instante.</returns>
    public static DateOnly De(DateTimeOffset instante, ZonaHoraria zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, zona.Reglas).DateTime);
    }

    /// <summary>El día de ahora en una zona, según el reloj.</summary>
    /// <param name="reloj">El reloj del sistema, o el de un test.</param>
    /// <param name="zona">La zona del sitio.</param>
    /// <returns>El día de la zona en el instante que da el reloj.</returns>
    public static DateOnly Hoy(TimeProvider reloj, ZonaHoraria zona)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        return De(reloj.GetUtcNow(), zona);
    }

    /// <summary>El instante en que empieza un día en una zona: su medianoche.</summary>
    /// <remarks>
    /// Un día no dura siempre veinticuatro horas: el del cambio al horario de verano dura veintitrés,
    /// y el de vuelta, veinticinco. Por eso el final de un día es el principio del siguiente, y no
    /// el principio más un día. En las tres zonas admitidas la hora cambia de madrugada y nunca a
    /// medianoche, así que la medianoche existe y es una sola; un caso lo afirma de 2000 a 2100.
    /// </remarks>
    /// <param name="dia">El día.</param>
    /// <param name="zona">La zona.</param>
    /// <returns>El instante, en UTC.</returns>
    public static DateTimeOffset Inicio(DateOnly dia, ZonaHoraria zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        var medianoche = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(medianoche, zona.Reglas), TimeSpan.Zero);
    }

    /// <summary>
    /// Si un día de una zona no acaba antes de que empiece otro día de otra: si se solapan, o si el
    /// primero va detrás.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la guarda de lo que no puede ir antes que otra cosa cuando cada una lleva la fecha de su
    /// sitio, como la recepción de una transferencia frente a su envío (ADR-0079). En una zona es
    /// comparar los dos días. Entre dos, el anterior en el calendario puede compartir una hora con
    /// el otro: el 31 de diciembre de Canarias y el 1 de enero de Madrid comparten la de las 23:00
    /// UTC.
    /// </para>
    /// <para>
    /// <b>Se compara sin sumar horas</b>: el principio del otro, visto desde la zona del primero, no
    /// cae después del primero. El final de un día de cambio de hora no es su principio más
    /// veinticuatro horas, y así no hace falta saber cuánto dura.
    /// </para>
    /// </remarks>
    /// <param name="dia">El día que no puede ir antes.</param>
    /// <param name="zona">Su zona.</param>
    /// <param name="otro">El otro día.</param>
    /// <param name="zonaDelOtro">La zona del otro.</param>
    /// <returns><c>true</c> si el día no acaba antes de que empiece el otro.</returns>
    public static bool NoAcabaAntesDe(DateOnly dia, ZonaHoraria zona, DateOnly otro, ZonaHoraria zonaDelOtro)
    {
        ArgumentNullException.ThrowIfNull(zona);
        ArgumentNullException.ThrowIfNull(zonaDelOtro);

        return De(Inicio(otro, zonaDelOtro), zona) <= dia;
    }

    /// <summary>El día más atrasado de un instante entre varias zonas.</summary>
    /// <remarks>
    /// Para lo que tiene que haber terminado en todas: el mes que puede cerrar el corte de una
    /// empresa es el que ha terminado en cada una de sus zonas.
    /// </remarks>
    /// <param name="instante">El instante.</param>
    /// <param name="zonas">Las zonas, al menos una.</param>
    /// <returns>El menor de los días.</returns>
    /// <exception cref="ArgumentException">No hay ninguna zona.</exception>
    public static DateOnly ElMasAtrasado(DateTimeOffset instante, IEnumerable<ZonaHoraria> zonas) =>
        Dias(instante, zonas).Min();

    /// <summary>El día más adelantado de un instante entre varias zonas.</summary>
    /// <remarks>
    /// Para lo que compara con «hoy» algo que pudo pasar en cualquiera de ellas: con el más
    /// adelantado, nada que ya sea hoy en alguna parece futuro.
    /// </remarks>
    /// <param name="instante">El instante.</param>
    /// <param name="zonas">Las zonas, al menos una.</param>
    /// <returns>El mayor de los días.</returns>
    /// <exception cref="ArgumentException">No hay ninguna zona.</exception>
    public static DateOnly ElMasAdelantado(DateTimeOffset instante, IEnumerable<ZonaHoraria> zonas) =>
        Dias(instante, zonas).Max();

    private static DateOnly[] Dias(DateTimeOffset instante, IEnumerable<ZonaHoraria> zonas)
    {
        ArgumentNullException.ThrowIfNull(zonas);

        DateOnly[] dias = [.. zonas.Select(zona => De(instante, zona))];

        return dias.Length > 0
            ? dias
            : throw new ArgumentException("Sin ninguna zona no hay día que elegir.", nameof(zonas));
    }
}
