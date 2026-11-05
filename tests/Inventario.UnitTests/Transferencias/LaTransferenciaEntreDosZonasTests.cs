using System.Globalization;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Transferencias;

/// <summary>
/// Una transferencia entre dos almacenes con su zona cada uno (ADR-0079): cada fecha va en la de su
/// almacén, y las guardas de la recepción y del inverso comparan instantes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no basta comparar los dos días.</b> La península va una hora por delante de Canarias:
/// lo que sale de Madrid a las 00:30 del 26 llega a Canarias a las 23:45 del 25. La recepción lleva
/// un día anterior al del envío, y es correcta. Lo que se compara es si el día de la recepción acaba
/// antes de que empiece el del envío.
/// </para>
/// <para>
/// <b>Los días de cambio de hora van a propósito</b>: el 29 de marzo de 2026 dura veintitrés horas, y
/// el 25 de octubre, veinticinco. Una guarda que sumara veinticuatro horas a un principio fallaría
/// en ellos.
/// </para>
/// </remarks>
public sealed class LaTransferenciaEntreDosZonasTests
{
    private const string Motivo = "Se mandó al almacén equivocado";

    private static readonly DateOnly s_diaDePartida = new(2026, 3, 2);

    private static readonly ZonasDeLaTransferencia s_deMadridACanarias = new(ZonaHoraria.Madrid, ZonaHoraria.Canarias);

    /// <summary>
    /// La recepción se admite si su día no acaba antes de que empiece el del envío, aunque lleve un
    /// número menor; y en una misma zona, o en dos con la misma hora, es comparar los días.
    /// </summary>
    /// <param name="zonaDelOrigen">La zona del almacén que envía.</param>
    /// <param name="envio">El día del envío, en esa zona.</param>
    /// <param name="zonaDelDestino">La del que recibe.</param>
    /// <param name="recepcion">El día de la recepción, en la suya.</param>
    /// <param name="admitida">Si se admite.</param>
    [Theory]
    [InlineData("Europe/Madrid", "2026-03-29", "Europe/Madrid", "2026-03-28", false)]
    [InlineData("Europe/Madrid", "2026-03-29", "Europe/Madrid", "2026-03-29", true)]

    // El 29 de Madrid empieza a las 23:00 UTC del 28, que en Canarias todavía es 28.
    [InlineData("Europe/Madrid", "2026-03-29", "Atlantic/Canary", "2026-03-28", true)]
    [InlineData("Europe/Madrid", "2026-03-29", "Atlantic/Canary", "2026-03-27", false)]

    // El 29 de Canarias empieza a las 00:00 UTC, que en Madrid ya es la 01:00 del 29.
    [InlineData("Atlantic/Canary", "2026-03-29", "Europe/Madrid", "2026-03-28", false)]
    [InlineData("Atlantic/Canary", "2026-03-29", "Europe/Madrid", "2026-03-29", true)]

    // El 26 de Madrid empieza a las 23:00 UTC del 25, la hora que el 25 tiene de más.
    [InlineData("Europe/Madrid", "2026-10-26", "Atlantic/Canary", "2026-10-25", true)]
    [InlineData("Europe/Madrid", "2026-10-26", "Atlantic/Canary", "2026-10-24", false)]
    [InlineData("Atlantic/Canary", "2026-10-25", "Europe/Madrid", "2026-10-24", false)]
    [InlineData("Europe/Madrid", "2026-10-26", "Africa/Ceuta", "2026-10-25", false)]
    public void La_recepcion_se_admite_si_su_dia_no_acaba_antes_de_que_empiece_el_del_envio(
        string zonaDelOrigen, string envio, string zonaDelDestino, string recepcion, bool admitida)
    {
        ZonasDeLaTransferencia zonas = new(ZonaHoraria.De(zonaDelOrigen), ZonaHoraria.De(zonaDelDestino));
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, Dia(envio));
        libro.Enviar(transferencia);

        transferencia.AdmiteLaRecepcionEn(Dia(recepcion), zonas).ShouldBe(admitida);

        if (admitida)
        {
            libro.Recibir(transferencia, Dia(recepcion), zonas);

            transferencia.FechaDeRecepcion.ShouldBe(Dia(recepcion), "cada fecha es la de su almacén");
        }
        else
        {
            Should.Throw<ArgumentOutOfRangeException>(() => libro.Recibir(transferencia, Dia(recepcion), zonas));

            transferencia.Estado.ShouldBe(EstadoDeTransferencia.Enviada);
        }
    }

    /// <summary>
    /// El inverso nace con sus dos fechas, cada una hoy en su almacén: a las 23:30 UTC del 25 de
    /// octubre de 2026 es 26 en Madrid y 25 en Canarias, y cada pata del libro lleva la suya.
    /// </summary>
    /// <param name="recibirAntes">Si se recibe antes de anularla.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void El_inverso_lleva_cada_fecha_en_la_zona_de_su_almacen(bool recibirAntes)
    {
        DateTimeOffset ahora = new(2026, 10, 25, 23, 30, 0, TimeSpan.Zero);
        DateOnly enMadrid = DiaDeNegocio.De(ahora, ZonaHoraria.Madrid);
        DateOnly enCanarias = DiaDeNegocio.De(ahora, ZonaHoraria.Canarias);

        enMadrid.ShouldBe(new DateOnly(2026, 10, 26));
        enCanarias.ShouldBe(new DateOnly(2026, 10, 25), "si fueran el mismo día, el caso no vería nada");

        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, enMadrid);
        libro.Enviar(original);

        if (recibirAntes)
        {
            libro.Recibir(original, enCanarias, s_deMadridACanarias);
        }

        (Transferencia inverso, LoQueMueveLaTransferencia movido) =
            libro.Anular(original, enMadrid, enCanarias, s_deMadridACanarias);

        inverso.FechaDeEnvio.ShouldBe(enMadrid, "la del origen, en la zona del origen");
        inverso.FechaDeRecepcion.ShouldBe(enCanarias, "la del destino, en la suya: un día menos y el mismo instante");

        MovimientoStock[] enElOrigen = [.. movido.Movimientos.Where(fila => fila.AlmacenId == ElLibroDeLaPrueba.Origen)];
        MovimientoStock[] enElDestino =
            [.. movido.Movimientos.Where(fila => fila.AlmacenId == ElLibroDeLaPrueba.Destino)];

        enElOrigen.ShouldHaveSingleItem().FechaDeOperacion.ShouldBe(enMadrid);
        enElDestino.Length.ShouldBe(recibirAntes ? 1 : 0, "solo una recibida tiene entrada en el destino que negar");
        enElDestino.ShouldAllBe(fila => fila.FechaDeOperacion == enCanarias);
    }

    /// <summary>
    /// Ninguna fecha del inverso acaba antes de que empiece ninguna del original, ni su recepción
    /// antes que su envío. Lo único que lo rompe con fechas de hoy es la hora de un cambio de zona.
    /// </summary>
    /// <remarks>
    /// El original sale de Madrid el 26 de octubre de 2026. Las dos últimas filas son esa hora: el
    /// destino recibió el 26 en Madrid y después pasó a Canarias, donde a las 23:30 UTC del 25 todavía
    /// es 25. A medianoche de Canarias, sí.
    /// </remarks>
    /// <param name="zonaAlRecibir">La zona del destino cuando recibió el original, o nada si no llegó.</param>
    /// <param name="recepcion">El día en que lo recibió, en esa zona.</param>
    /// <param name="zonaDelDestino">La zona del destino al anular.</param>
    /// <param name="enElOrigen">El día del inverso en el origen.</param>
    /// <param name="enElDestino">El día del inverso en el destino.</param>
    /// <param name="admitido">Si se admite.</param>
    [Theory]
    [InlineData(null, null, "Atlantic/Canary", "2026-10-26", "2026-10-25", true)]
    [InlineData(null, null, "Atlantic/Canary", "2026-10-26", "2026-10-24", false)]
    [InlineData("Atlantic/Canary", "2026-10-25", "Atlantic/Canary", "2026-10-26", "2026-10-25", true)]
    [InlineData("Atlantic/Canary", "2026-10-25", "Atlantic/Canary", "2026-10-25", "2026-10-25", false)]
    [InlineData("Atlantic/Canary", "2026-10-25", "Atlantic/Canary", "2026-10-27", "2026-10-25", false)]
    [InlineData("Europe/Madrid", "2026-10-26", "Atlantic/Canary", "2026-10-26", "2026-10-25", false)]
    [InlineData("Europe/Madrid", "2026-10-26", "Atlantic/Canary", "2026-10-26", "2026-10-26", true)]
    public void Ninguna_fecha_del_inverso_acaba_antes_de_que_empiece_ninguna_del_original(
        string? zonaAlRecibir,
        string? recepcion,
        string zonaDelDestino,
        string enElOrigen,
        string enElDestino,
        bool admitido)
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, new DateOnly(2026, 10, 26));
        libro.Enviar(original);

        if (recepcion is not null)
        {
            libro.Recibir(original, Dia(recepcion), new(ZonaHoraria.Madrid, ZonaHoraria.De(zonaAlRecibir!)));
        }

        ZonasDeLaTransferencia ahora = new(ZonaHoraria.Madrid, ZonaHoraria.De(zonaDelDestino));
        EstadoDeTransferencia antes = original.Estado;

        original.AdmiteUnInversoEn(Dia(enElOrigen), Dia(enElDestino), ahora).ShouldBe(admitido);

        if (admitido)
        {
            Transferencia inverso = original.CrearInverso(
                Dia(enElOrigen), Dia(enElDestino), ahora, Motivo, ElLibroDeLaPrueba.Momento);

            inverso.FechaDeEnvio.ShouldBe(Dia(enElOrigen));
            inverso.FechaDeRecepcion.ShouldBe(Dia(enElDestino));
        }
        else
        {
            Should.Throw<ArgumentOutOfRangeException>(() => original.CrearInverso(
                Dia(enElOrigen), Dia(enElDestino), ahora, Motivo, ElLibroDeLaPrueba.Momento));
        }

        original.Estado.ShouldBe(antes, "construir el inverso no anula nada");
    }

    /// <summary>
    /// La holgura de la base: en ningún par de zonas admitidas, de 2000 a 2100, se admite una
    /// recepción más de <see cref="Transferencia.DiasQueLaRecepcionPuedeIrPorDetras"/> días por
    /// detrás de su envío. Y con esos días, sí, todos, de la península a Canarias.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es lo que deja pasar el <c>CHECK</c> de la base</b>, que compara dos días y no sabe de
    /// zonas: con menos holgura, una recepción correcta sería un <c>500</c>; con más, la base dejaría
    /// de parar lo que nunca puede pasar.
    /// </para>
    /// <para>
    /// <b>La pareja es el diccionario</b>: dice en qué pares se usa la holgura, y cuántos días. Si la
    /// guarda comparara días sin zonas, saldría vacío.
    /// </para>
    /// </remarks>
    [Fact]
    public void Una_recepcion_admitida_va_como_mucho_un_dia_por_detras_de_su_envio()
    {
        int holgura = Transferencia.DiasQueLaRecepcionPuedeIrPorDetras;
        DateOnly ultimo = new(2100, 12, 31);
        List<string> fuera = [];
        List<string> conHolgura = [];

        foreach (ZonaHoraria delOrigen in ZonaHoraria.Admitidas)
        {
            foreach (ZonaHoraria delDestino in ZonaHoraria.Admitidas)
            {
                ZonasDeLaTransferencia zonas = new(delOrigen, delDestino);
                int dias = 0;

                for (DateOnly envio = new(2000, 1, 1); envio <= ultimo; envio = envio.AddDays(1))
                {
                    if (zonas.LaRecepcionNoVaAntesDelEnvio(envio.AddDays(-holgura - 1), envio))
                    {
                        fuera.Add($"{delOrigen} → {delDestino}, {envio:yyyy-MM-dd}");
                    }

                    if (zonas.LaRecepcionNoVaAntesDelEnvio(envio.AddDays(-holgura), envio))
                    {
                        dias++;
                    }
                }

                if (dias > 0)
                {
                    conHolgura.Add($"{delOrigen} → {delDestino}: {dias}");
                }
            }
        }

        fuera.ShouldBeEmpty();
        conHolgura.ShouldBe(
            ["Europe/Madrid → Atlantic/Canary: 36890", "Africa/Ceuta → Atlantic/Canary: 36890"],
            ignoreOrder: true);
    }

    /// <summary>
    /// El primer día que admite la recepción, el que publica la ficha para el formulario, es el
    /// primero que admite la guarda: él entra, y el anterior, no. Por los nueve pares, de 2000 a 2100.
    /// </summary>
    /// <remarks>
    /// La pareja son los dos sentidos entre Madrid y Canarias en un día de cada cambio de hora: hacia
    /// Canarias, el primero es el día anterior al del envío, y hacia Madrid, el mismo. Sin ella, un
    /// cálculo que devolviera siempre el día del envío solo fallaría en el barrido por un lado.
    /// </remarks>
    [Fact]
    public void La_primera_recepcion_es_el_primer_dia_que_admite_la_guarda()
    {
        DateOnly ultimo = new(2100, 12, 31);
        List<string> malas = [];

        foreach (ZonaHoraria delOrigen in ZonaHoraria.Admitidas)
        {
            foreach (ZonaHoraria delDestino in ZonaHoraria.Admitidas)
            {
                ZonasDeLaTransferencia zonas = new(delOrigen, delDestino);

                for (DateOnly envio = new(2000, 1, 1); envio <= ultimo; envio = envio.AddDays(1))
                {
                    DateOnly primera = zonas.LaPrimeraRecepcionDe(envio);

                    if (!zonas.LaRecepcionNoVaAntesDelEnvio(primera, envio)
                        || zonas.LaRecepcionNoVaAntesDelEnvio(primera.AddDays(-1), envio))
                    {
                        malas.Add($"{delOrigen} → {delDestino}, {envio:yyyy-MM-dd}: {primera:yyyy-MM-dd}");
                    }
                }
            }
        }

        malas.ShouldBeEmpty();
        s_deMadridACanarias.LaPrimeraRecepcionDe(Dia("2026-03-29")).ShouldBe(Dia("2026-03-28"));
        s_deMadridACanarias.LaPrimeraRecepcionDe(Dia("2026-10-26")).ShouldBe(Dia("2026-10-25"));
        new ZonasDeLaTransferencia(ZonaHoraria.Canarias, ZonaHoraria.Madrid)
            .LaPrimeraRecepcionDe(Dia("2026-03-29")).ShouldBe(Dia("2026-03-29"));
    }

    private static ElLibroDeLaPrueba UnLibroConExistenciasEnElOrigen()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);

        return libro;
    }

    private static DateOnly Dia(string iso) => DateOnly.Parse(iso, CultureInfo.InvariantCulture);
}
