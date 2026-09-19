using System.Reflection;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Documentos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Ajustes;

/// <summary>
/// La máquina de estados del <c>Ajuste</c> (R1): dónde nace, por dónde se sale de cada estado, y
/// las dos transiciones que no existen.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que R1 quiere impedir es <c>documento.Estado = X</c>, y eso no lo impide una
/// convención</b>: el <c>set</c> de <c>Estado</c> es privado y vive en OTRO ensamblado, así que
/// la asignación no compila desde ningún sitio del proyecto. Una prueba no puede afirmar que algo
/// no compila —lo que no compila no llega a ejecutarse—, así que lo que se afirma aquí es
/// exactamente la condición que lo provoca, y se afirma sobre el ensamblado compilado:
/// <see cref="El_estado_no_se_asigna_se_transita"/>.
/// </para>
/// <para>
/// <b>Y cada transición imposible tiene su caso.</b> No son variantes de la misma: confirmar dos
/// veces y anular un borrador fallan por el mismo mecanismo, pero un cambio que dejara de
/// comprobar el estado de partida rompería las dos a la vez, y un cambio que solo tocara la tabla
/// de transiciones del ajuste rompería una. Por separado, el mensaje dice cuál.
/// </para>
/// </remarks>
public sealed class LaMaquinaDeEstadosDelAjusteTests
{
    /// <summary>Un correlativo cualquiera, de los que entrega el mecanismo de numeración.</summary>
    /// <remarks>
    /// <b>No es el 1 a propósito.</b> Una serie que ya ha numerado entrega el que le toque, y un
    /// caso escrito con el 1 saldría verde igual contra un <c>Confirmar</c> que ignorara el
    /// parámetro y contara las confirmaciones por su cuenta.
    /// </remarks>
    private const long NumeroQueDioLaSerie = 47;

    private static readonly DateTimeOffset s_momento =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Un ajuste nace en borrador y sin haber movido nada.</summary>
    /// <remarks>
    /// El estado inicial se afirma porque es la mitad que hace significar a todas las demás: si
    /// naciera confirmado, «añadir una línea a un confirmado lanza» seguiría verde y el documento
    /// no admitiría ni su primera línea.
    /// </remarks>
    [Fact]
    public void Un_ajuste_nace_en_borrador_y_sin_lineas()
    {
        Ajuste ajuste = UnAjuste();

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Borrador);
        ajuste.Lineas.ShouldBeEmpty();

        ajuste.Numero.ShouldBeNull(
            "un borrador no ha gastado ningún correlativo: si naciera con uno, tirarlo dejaría " +
            "el hueco que la R5 prohibe");

        ajuste.EventosPendientes.ShouldBeEmpty(
            "abrir un borrador no es un hecho que nadie de fuera necesite saber: lo que se cuenta " +
            "es la confirmación, que es la que mueve el libro");
    }

    /// <summary>Confirmar devuelve una fila del libro por línea y deja el documento confirmado.</summary>
    /// <remarks>
    /// <b>Las tres afirmaciones van juntas porque describen un solo hecho.</b> Que el estado se
    /// mueva sin devolver los movimientos sería una confirmación que no mueve el libro; que
    /// devuelva los movimientos sin mover el estado dejaría el documento confirmable otra vez, y
    /// el libro con las filas por duplicado. Y el evento es la tercera: <c>Transitar</c> no
    /// admite transitar sin él.
    /// </remarks>
    [Fact]
    public void Confirmar_mueve_el_estado_devuelve_una_fila_por_linea_y_cuenta_el_hecho()
    {
        Ajuste ajuste = UnAjuste();
        ConLinea(ajuste, cantidad: 4m, factor: 2m);
        ConLinea(ajuste, cantidad: -1m, factor: 1m);

        IReadOnlyList<MovimientoStock> movimientos = ajuste.Confirmar(NumeroQueDioLaSerie, Confirmado(ajuste), s_momento);

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Confirmado);

        ajuste.Numero.ShouldBe(
            NumeroQueDioLaSerie,
            "el documento se queda el número que le dieron, tal cual: componerlo, desplazarlo o " +
            "recontarlo aquí rompería la correspondencia con el contador de la serie");

        movimientos.Count.ShouldBe(
            2, "el libro recibe una fila por línea del documento: ni una menos, ni agrupadas");

        movimientos[0].CantidadEnUnidadBase.ShouldBe(8m);
        movimientos[1].CantidadEnUnidadBase.ShouldBe(
            -1m, "la cantidad va con signo y la salida se escribe en negativo, no en otra columna");

        movimientos.ShouldAllBe(
            movimiento => movimiento.DocumentoOrigenTipo == TipoDeDocumentoOrigen.Ajuste
                && movimiento.DocumentoOrigenId == ajuste.Id,
            "toda fila del libro dice de qué documento sale (R13), y ese documento es este");

        movimientos.ShouldAllBe(
            movimiento => movimiento.EmpresaId == ajuste.EmpresaId
                && movimiento.AlmacenId == ajuste.AlmacenId
                && movimiento.FechaDeOperacion == ajuste.FechaDeOperacion,
            "la empresa, el almacén y la fecha son del documento: una fila con otra fecha caería " +
            "en la partición de otro mes");

        ajuste.EventosPendientes.Count.ShouldBe(
            1, "una transición sin su evento deja a quien escucha creyendo lo contrario de lo que pasó");
    }

    /// <summary>Un ajuste sin líneas no se confirma, y esa es la vuelta de la R13.</summary>
    /// <remarks>
    /// <b>Se sostiene aquí, ANTES de que la fila exista</b>, y no con un barrido posterior que
    /// encontraría el daño hecho. El barrido existe igualmente
    /// —<c>LaDobleFlechaDelLibroTests</c>—, porque mañana puede haber otro camino que confirme:
    /// una importación, una migración de datos. Lo que este caso afirma es que <b>el camino de
    /// producción no lo produce</b>, que es lo que convierte aquel barrido en una red y no en la
    /// única defensa.
    /// </remarks>
    [Fact]
    public void Un_ajuste_sin_lineas_no_se_confirma()
    {
        Ajuste ajuste = UnAjuste();

        InvalidOperationException fallo = Should.Throw<InvalidOperationException>(
            () => ajuste.Confirmar(NumeroQueDioLaSerie, Confirmado(ajuste), s_momento));

        fallo.Message.ShouldContain("R13");

        ajuste.Estado.ShouldBe(
            EstadoDeAjuste.Borrador,
            "la comprobación va antes de la transición: un documento que se queda confirmado tras " +
            "un intento fallido es peor que el intento");
    }

    /// <summary>Confirmar dos veces no se puede: la transición sale de borrador.</summary>
    [Fact]
    public void Un_ajuste_ya_confirmado_no_se_vuelve_a_confirmar()
    {
        Ajuste ajuste = UnAjuste();
        ConLinea(ajuste, cantidad: 1m, factor: 1m);
        ajuste.Confirmar(NumeroQueDioLaSerie, Confirmado(ajuste), s_momento);

        InvalidOperationException fallo = Should.Throw<InvalidOperationException>(
            () => ajuste.Confirmar(NumeroQueDioLaSerie, Confirmado(ajuste), s_momento));

        // El mensaje dice de DÓNDE sale la transición, no solo que no se pueda: es lo que
        // necesita quien la intentó desde donde no tocaba.
        fallo.Message.ShouldContain(nameof(EstadoDeAjuste.Borrador));

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Confirmado);
    }

    /// <summary>El segundo intento no repinta el número del primero.</summary>
    /// <remarks>
    /// <b>Es el caso del de arriba visto por el otro lado, y no sobra.</b> Aquel afirma que el
    /// estado no se mueve; este, que el número tampoco —y es el que se pondría rojo si la
    /// asignación se colocara <i>antes</i> de <c>Transitar</c>, donde parece igual de válida—. Un
    /// documento que se quedara el número del reintento apuntaría a un correlativo que otro
    /// documento se llevó de verdad, con los dos enseñando el mismo.
    /// </remarks>
    [Fact]
    public void El_segundo_intento_de_confirmar_no_repinta_el_numero()
    {
        Ajuste ajuste = UnAjuste();
        ConLinea(ajuste, cantidad: 1m, factor: 1m);
        ajuste.Confirmar(NumeroQueDioLaSerie, Confirmado(ajuste), s_momento);

        Should.Throw<InvalidOperationException>(
            () => ajuste.Confirmar(NumeroQueDioLaSerie + 1, Confirmado(ajuste), s_momento));

        ajuste.Numero.ShouldBe(NumeroQueDioLaSerie);
    }

    /// <summary>Un número que no sale del mecanismo no se acepta.</summary>
    /// <remarks>
    /// <para>
    /// <b>El cero es el que importa</b>, y no porque alguien vaya a escribirlo: es el valor por
    /// omisión de un <c>long</c>. Un llamante que olvidara el parámetro —una sobrecarga nueva, un
    /// mapeo automático, un campo sin rellenar— confirmaría con cero, y el documento diría
    /// llevar un correlativo que ninguna serie entregó. Los correlativos empiezan en uno porque el
    /// contador nace a cero y la sentencia incrementa antes de leer.
    /// </para>
    /// <para>
    /// Y el estado se comprueba después: de los dos modos de fallar, el que deja el documento
    /// confirmado con un número inválido es el que no se puede deshacer.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Un_numero_que_no_sale_del_mecanismo_no_se_acepta(long numero)
    {
        Ajuste ajuste = UnAjuste();
        ConLinea(ajuste, cantidad: 1m, factor: 1m);

        Should.Throw<ArgumentOutOfRangeException>(
            () => ajuste.Confirmar(numero, Confirmado(ajuste), s_momento))
            .ParamName.ShouldBe("numero");

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Borrador);
        ajuste.Numero.ShouldBeNull();
    }

    /// <summary>Un confirmado no admite líneas nuevas: sus filas del libro ya están escritas.</summary>
    /// <remarks>
    /// Esta es la que el libro no puede defender solo. El disparador de solo añadido impide
    /// cambiar una fila escrita, pero una línea nueva en un documento ya confirmado no cambiaría
    /// ninguna: se quedaría <b>sin</b> fila, y el documento diría que movió una cosa más de la
    /// que movió.
    /// </remarks>
    [Fact]
    public void Un_ajuste_confirmado_no_admite_lineas_nuevas()
    {
        Ajuste ajuste = UnAjuste();
        ConLinea(ajuste, cantidad: 1m, factor: 1m);
        ajuste.Confirmar(NumeroQueDioLaSerie, Confirmado(ajuste), s_momento);

        Should.Throw<InvalidOperationException>(() => ConLinea(ajuste, cantidad: 2m, factor: 1m));

        ajuste.Lineas.Count.ShouldBe(1);
    }

    /// <summary>Anular sale de confirmado, no de borrador.</summary>
    /// <remarks>
    /// <b>Y las dos mitades en el mismo caso</b>, porque por separado ninguna afirma lo que dice:
    /// un <c>Anular</c> que lanzara siempre pasaría la primera, y uno que no comprobara nada
    /// pasaría la segunda. Lo que hay que ver es que la puerta está donde está — un borrador no se
    /// anula, se tira.
    /// </remarks>
    [Fact]
    public void Anular_sale_de_confirmado_y_no_de_borrador()
    {
        Ajuste borrador = UnAjuste();
        ConLinea(borrador, cantidad: 1m, factor: 1m);

        Should.Throw<InvalidOperationException>(() => borrador.Anular(Anulado(borrador)));
        borrador.Estado.ShouldBe(EstadoDeAjuste.Borrador);

        Ajuste confirmado = UnAjuste();
        ConLinea(confirmado, cantidad: 1m, factor: 1m);
        confirmado.Confirmar(NumeroQueDioLaSerie, Confirmado(confirmado), s_momento);

        confirmado.Anular(Anulado(confirmado));

        confirmado.Estado.ShouldBe(EstadoDeAjuste.Anulado);
        confirmado.EventosPendientes.Count.ShouldBe(
            2, "las dos transiciones han contado la suya: la confirmación y la anulación");
    }

    /// <summary>Ninguna transición ocurre sin el evento que la cuenta.</summary>
    /// <remarks>
    /// El evento está en la firma a propósito, y lo que se afirma aquí es que además se comprueba:
    /// una firma que admita el nulo es «acuérdate de pasarlo», que es la clase de cosa que sale
    /// verde el día que alguien escribe la segunda transición.
    /// </remarks>
    [Fact]
    public void Sin_evento_no_hay_transicion()
    {
        Ajuste ajuste = UnAjuste();
        ConLinea(ajuste, cantidad: 1m, factor: 1m);

        Should.Throw<ArgumentNullException>(
            () => ajuste.Confirmar(NumeroQueDioLaSerie, null!, s_momento));

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Borrador);
    }

    /// <summary>
    /// El estado no se asigna: su <c>set</c> es privado y vive en otro ensamblado, que es lo que
    /// hace que <c>documento.Estado = X</c> no compile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Esto se mira por reflexión y no con un comentario</b>, porque es la única forma de que
    /// la afirmación se ponga roja. «No compila» no se puede escribir como un caso: el código que
    /// no compila no llega a ejecutarse, y el fichero entero se quedaría sin construir. Lo que sí
    /// se puede afirmar son las <b>dos condiciones exactas</b> que lo provocan, y son las dos:
    /// que el <c>set</c> no sea público, y que esté declarado en otro ensamblado — porque un
    /// <c>internal</c> en el mismo ensamblado sí compilaría desde el propio módulo.
    /// </para>
    /// <para>
    /// <b>Y se afirma antes que el <c>set</c> exista</b> (ADR-0020): sin esa línea, una propiedad
    /// de solo lectura —sin <c>set</c> ninguno— daría el caso por bueno por no haber nada que
    /// mirar, y el día que alguien le pusiera uno público el caso seguiría verde.
    /// </para>
    /// <para>
    /// <b>El <c>set</c> hay que pedírselo a quien lo DECLARA</b>, no a <c>Ajuste</c>: la reflexión
    /// no asoma los miembros privados de una clase base a través de la derivada, así que
    /// <c>typeof(Ajuste).GetProperty(…).GetSetMethod(nonPublic: true)</c> devuelve <c>null</c> y
    /// diría «no hay <c>set</c>» sobre una propiedad que sí lo tiene. Eso mismo es la R1 vista
    /// desde fuera —el módulo no alcanza el asignador—, pero como afirmación no distingue un
    /// <c>set</c> inaccesible de la ausencia de <c>set</c>, que es justo lo que este caso separa.
    /// </para>
    /// </remarks>
    [Fact]
    public void El_estado_no_se_asigna_se_transita()
    {
        PropertyInfo vistaDesdeElAjuste = typeof(Ajuste).GetProperty(nameof(Ajuste.Estado))
            ?? throw new InvalidOperationException("`Ajuste` ya no tiene propiedad `Estado`.");

        Type declarante = vistaDesdeElAjuste.DeclaringType!;

        PropertyInfo estado = declarante.GetProperty(
            nameof(Ajuste.Estado),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"`{declarante.Name}` ya no declara `Estado`.");

        MethodInfo asignador = estado.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException(
                "`Estado` no tiene `set` ninguno. Este caso comprueba que el que hay es " +
                "inaccesible; sin `set`, no estaría comprobando nada (ADR-0020).");

        asignador.IsPublic.ShouldBeFalse(
            "con el `set` público, `ajuste.Estado = EstadoDeAjuste.Confirmado` compila y la R1 " +
            "pasa a ser una convención");

        declarante.Assembly.ShouldBe(
            typeof(DocumentoBase<>).Assembly,
            "el `set` tiene que vivir en el bloque común y no en el módulo: un `private` en la " +
            "propia clase valdría, pero un `internal` declarado aquí compilaría desde el resto " +
            "del módulo sin que nada lo notara");

        typeof(Ajuste).Assembly.ShouldNotBe(
            typeof(DocumentoBase<>).Assembly,
            "si el documento y su tipo base acabaran en el mismo ensamblado, la frase de arriba " +
            "dejaría de ser cierta sin que este caso se enterara");
    }

    private static Ajuste UnAjuste() => Ajuste.Abrir(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new DateOnly(2026, 3, 14),
        "Recuento de marzo",
        s_momento);

    private static void ConLinea(Ajuste ajuste, decimal cantidad, decimal factor) =>
        ajuste.AnadirLinea(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            cantidad,
            Guid.CreateVersion7(),
            factor,
            Importe.De(2.50m, "EUR"),
            s_momento);

    private static AjusteConfirmado Confirmado(Ajuste ajuste) => new(
        ajuste.Id, ajuste.EmpresaId, ajuste.AlmacenId, ajuste.FechaDeOperacion, ajuste.Lineas.Count);

    private static AjusteAnulado Anulado(Ajuste ajuste) => new(ajuste.Id, ajuste.EmpresaId);
}
