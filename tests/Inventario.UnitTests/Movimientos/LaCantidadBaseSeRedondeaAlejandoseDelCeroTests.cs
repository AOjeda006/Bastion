using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.Movimientos;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Movimientos;

/// <summary>
/// La regla que une las tres cantidades de una fila del libro: la base es la introducida por el
/// factor, redondeada a seis decimales <b>alejándose del cero</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta es la línea que compone el saldo.</b> No es una guarda que rechace entradas malas: es
/// la que decide qué número se suma. El stock de una ubicación es la suma de
/// <see cref="MovimientoStock.CantidadEnUnidadBase"/> de sus filas, así que un redondeo distinto
/// no da un error, da <b>otro inventario</b>, y lo da en silencio y acumulándose fila a fila.
/// </para>
/// <para>
/// <b>Y por eso los casos son fronteras y no muestras.</b> <c>2 × 3 = 6</c> sale verde con el
/// redondeo del banquero, con el de alejarse del cero, con truncamiento y con cualquier escala de
/// cuatro decimales en adelante: no distingue ninguna de las decisiones que esta línea toma. Lo
/// que las distingue es el punto medio exacto —<c>0,0000125</c>, que es <c>0,000013</c> alejándose
/// del cero y <c>0,000012</c> con el del banquero, que es el de .NET por omisión— y el primer
/// valor que se redondea a cero.
/// </para>
/// <para>
/// <b>El motor sostiene la misma igualdad con un <c>CHECK</c></b>, y eso no hace estos casos
/// redundantes: el <c>CHECK</c> compara dos columnas que ya han llegado escritas, así que solo
/// caza a quien escriba una fila sin pasar por aquí. Quién calcula el número que se suma es esta
/// función, y estas son las únicas comprobaciones que la miran calcular.
/// </para>
/// </remarks>
public sealed class LaCantidadBaseSeRedondeaAlejandoseDelCeroTests
{
    /// <summary>El punto medio exacto sube, que es lo que distingue este redondeo del de .NET.</summary>
    /// <remarks>
    /// <b>La segunda afirmación es la que convierte esto en una prueba.</b> Sin ella, «da
    /// 0,000013» es un número que alguien copió de una ejecución; con ella queda escrito que el
    /// redondeo por omisión de .NET —el del banquero, el que sale si alguien borra el
    /// <c>MidpointRounding.AwayFromZero</c> de la fábrica— daba <b>otro</b> resultado para esta
    /// misma entrada.
    /// </remarks>
    [Fact]
    public void El_punto_medio_exacto_se_aleja_del_cero_y_no_va_al_par()
    {
        MovimientoStock.EnUnidadBase(0.0000125m, 1m).ShouldBe(0.000013m);

        decimal.Round(0.0000125m, MovimientoStock.DecimalesDeCantidad).ShouldBe(
            0.000012m,
            "si el redondeo por omisión de .NET diera ya 0,000013, el caso de arriba estaría " +
            "saliendo verde sin que `AwayFromZero` pintara nada, y borrarlo no rompería nada");
    }

    /// <summary>Y el punto medio negativo baja: «alejarse del cero» es simétrico.</summary>
    /// <remarks>
    /// Una salida de almacén es una cantidad negativa, así que la mitad negativa de esta regla no
    /// es un caso raro: es la mitad de las filas del libro. Con <c>MidpointRounding.ToPositive</c>
    /// o con un <c>Math.Abs</c> de más, el caso de arriba seguiría verde y las salidas se
    /// redondearían hacia dentro, que es justo la dirección que infla el inventario.
    /// </remarks>
    [Fact]
    public void El_punto_medio_negativo_se_aleja_del_cero_hacia_abajo() =>
        MovimientoStock.EnUnidadBase(-0.0000125m, 1m).ShouldBe(-0.000013m);

    /// <summary>La regla es un producto, y el factor entra por los dos lados.</summary>
    /// <remarks>
    /// El punto medio se compone aquí con el factor y no con la cantidad, que es como llega de
    /// verdad: nadie teclea <c>0,0000125</c>, lo que hay es una cantidad corriente y una conversión
    /// con decimales. Un <c>/</c> donde hay un <c>*</c> —o los dos argumentos cambiados en una
    /// llamada— sale rojo aquí y no en los casos de arriba, donde el factor es uno.
    /// </remarks>
    [Fact]
    public void La_base_es_la_introducida_POR_el_factor_y_no_al_reves()
    {
        MovimientoStock.EnUnidadBase(2.5m, 0.000005m).ShouldBe(0.000013m);

        MovimientoStock.EnUnidadBase(12m, 0.5m).ShouldBe(
            6m, "media docena de docenas son seis: con una división saldrían veinticuatro");
    }

    /// <summary>
    /// Una fila cuya cantidad base se redondea a cero no se escribe; la contigua que sí llega a
    /// algo, sí.
    /// </summary>
    /// <remarks>
    /// <b>Las dos mitades en el mismo caso, y por la frontera.</b> Una guarda que rechazara
    /// siempre pasaría la primera; una que no mirase la cantidad <i>base</i> —solo la
    /// introducida, que en las dos es distinta de cero— pasaría la segunda. Lo que hay que ver es
    /// dónde está exactamente el corte, y está entre dos valores que se diferencian en la última
    /// cifra que el libro sabe escribir: <c>0,0000004</c> se va a cero y <c>0,0000005</c> es el
    /// punto medio que sube a <c>0,000001</c>.
    /// </remarks>
    [Fact]
    public void Una_cantidad_que_se_redondea_a_cero_no_se_escribe_y_la_contigua_si()
    {
        ArgumentOutOfRangeException fallo = Should.Throw<ArgumentOutOfRangeException>(
            () => UnaFilaDe(0.0000004m, 1m));

        fallo.ParamName.ShouldBe("cantidadIntroducida");
        // El mensaje tiene que hablar de la cantidad BASE y no de la introducida: quien escribió
        // 0,0000004 no ve por qué «cero» si no ha escrito un cero.
        fallo.Message.ShouldContain("sumaría nada");

        UnaFilaDe(0.0000005m, 1m).CantidadEnUnidadBase.ShouldBe(0.000001m);
    }

    /// <summary>Una fila que no mueve nada no se escribe.</summary>
    /// <remarks>
    /// Va aparte del caso anterior porque es otra comprobación y falla por otro motivo: aquí la
    /// cantidad introducida <b>es</b> cero, y el rechazo tiene que ocurrir antes de que el factor
    /// importe. Con un factor cualquiera, la cantidad base también sería cero, así que las dos
    /// guardas taparían este caso la una a la otra — y quitar la primera dejaría el mensaje
    /// hablando de un redondeo que no ha pasado.
    /// </remarks>
    [Fact]
    public void Una_fila_que_no_mueve_nada_no_se_escribe() =>
        Should.Throw<ArgumentOutOfRangeException>(() => UnaFilaDe(0m, 3m))
            .ParamName.ShouldBe("cantidadIntroducida");

    /// <summary>Un factor que no es positivo no se admite: ni cero, ni negativo.</summary>
    /// <remarks>
    /// <b>Los dos, porque fallan por cosas distintas.</b> El cero llevaría la base a cero —lo
    /// pillaría la otra guarda, con el mensaje equivocado—; el negativo <b>no</b>: daría una base
    /// con el signo cambiado, una entrada convertida en salida, y ninguna otra comprobación de
    /// este fichero lo vería. Un <c>&lt; 0</c> donde hay un <c>&lt;= 0</c> deja pasar el primero;
    /// un <c>== 0</c>, el segundo.
    /// </remarks>
    [Fact]
    public void Un_factor_que_no_es_positivo_no_se_admite()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => UnaFilaDe(5m, 0m))
            .ParamName.ShouldBe("factorAUnidadBase");

        Should.Throw<ArgumentOutOfRangeException>(() => UnaFilaDe(5m, -2m))
            .ParamName.ShouldBe("factorAUnidadBase");
    }

    /// <summary>El signo viaja de la cantidad introducida a la base, y la fila guarda las tres.</summary>
    /// <remarks>
    /// <para>
    /// <b>El signo es lo que hace que el libro sea un libro</b>: no hay columna «tipo de
    /// movimiento» ni tabla de salidas, así que una salida de cuatro cajas es exactamente una fila
    /// con <c>-4</c>. Un <c>Math.Abs</c> en la conversión no rompería ninguna suma —seguiría
    /// cuadrando consigo misma— y convertiría todas las salidas en entradas.
    /// </para>
    /// <para>
    /// Y las otras dos cantidades se guardan tal cual se introdujeron: la fila tiene que seguir
    /// explicándose sola dentro de dos años, cuando la conversión que hoy vale 2 valga otra cosa.
    /// </para>
    /// </remarks>
    [Fact]
    public void El_signo_viaja_a_la_base_y_la_fila_guarda_lo_que_se_introdujo()
    {
        MovimientoStock salida = UnaFilaDe(-4m, 2m);

        salida.CantidadEnUnidadBase.ShouldBe(-8m);
        salida.CantidadIntroducida.ShouldBe(-4m);
        salida.FactorAUnidadBase.ShouldBe(2m);
    }

    /// <summary>Toda fila del libro dice de qué documento sale (R13).</summary>
    /// <remarks>
    /// Los dos campos van juntos porque la flecha de la R13 es el par: un identificador sin su
    /// tipo apunta a cinco tablas a la vez, y un tipo sin identificador no apunta a nada. No hay
    /// clave ajena que pueda expresar esto —el origen es un par, y son cinco documentos—, así que
    /// lo que hay es esta fábrica, que los exige a los dos.
    /// </remarks>
    [Fact]
    public void Toda_fila_dice_de_que_documento_sale()
    {
        var documento = Guid.CreateVersion7();

        var fila = MovimientoStock.Registrar(
            Guid.CreateVersion7(),
            new DateOnly(2026, 3, 14),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1m,
            Guid.CreateVersion7(),
            1m,
            Importe.De(2.50m, "EUR"),
            TipoDeDocumentoOrigen.Ajuste,
            documento,
            DateTimeOffset.UnixEpoch);

        fila.DocumentoOrigenTipo.ShouldBe(TipoDeDocumentoOrigen.Ajuste);
        fila.DocumentoOrigenId.ShouldBe(documento);
    }

    private static MovimientoStock UnaFilaDe(decimal cantidadIntroducida, decimal factorAUnidadBase) =>
        MovimientoStock.Registrar(
            Guid.CreateVersion7(),
            new DateOnly(2026, 3, 14),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            cantidadIntroducida,
            Guid.CreateVersion7(),
            factorAUnidadBase,
            Importe.De(2.50m, "EUR"),
            TipoDeDocumentoOrigen.Ajuste,
            Guid.CreateVersion7(),
            DateTimeOffset.UnixEpoch);
}
