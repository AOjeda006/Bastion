using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Ajustes;

/// <summary>
/// El contra-documento con el que se anula un ajuste (R2): qué copia, qué estrena, qué suma con
/// el original, y las cuatro formas de pedir una anulación que no lo es.
/// </summary>
/// <remarks>
/// <para>
/// <b>La afirmación que decide es que el par suma cero</b>, y no que la cantidad salga negada. Un
/// error de signo —negar la introducida y no la base, copiarlas sin negar, negar dos veces— deja
/// en verde «hay un inverso», «está confirmado», «lleva número» y «la flecha va en los dos
/// sentidos», y mueve el almacén al revés de lo que el documento dice.
/// <see cref="El_par_suma_cero_en_unidad_base"/> es el que se pone rojo, y por eso es el blanco
/// de la mutación del ítem: quitar la negación de <c>CrearInverso</c>.
/// </para>
/// <para>
/// <b>Aquí se suma lo que el agregado devuelve, y en el carril de integración se suma el libro ya
/// escrito.</b> No son el mismo caso repetido: este ve el cálculo —incluido el paso a unidad
/// base, que no está en la línea sino en <see cref="MovimientoStock"/>— sin encender nada, y el
/// otro ve lo que quedó en las filas, que es lo que leerá el stock. Un cambio que perdiera el
/// signo entre el agregado y la fila solo lo caza el segundo.
/// </para>
/// </remarks>
public sealed class ElInversoQueAnulaTests
{
    /// <summary>Un correlativo de los que da la serie. No es el uno, por el mismo motivo.</summary>
    private const long NumeroDelOriginal = 47;

    /// <summary>El siguiente: el inverso gasta número propio, de la misma serie.</summary>
    private const long NumeroDelInverso = 48;

    private static readonly DateTimeOffset s_momento =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly s_diaDelOriginal = new(2026, 3, 14);

    private static readonly DateOnly s_diaDeLaAnulacion = new(2026, 4, 2);

    /// <summary>El inverso copia cada línea con la cantidad cambiada de signo y el mismo coste.</summary>
    /// <remarks>
    /// <para>
    /// <b>El coste se copia y no se recalcula</b>, y eso se afirma línea a línea. El inverso
    /// compensa lo que el original escribió, no lo que costaría hoy: con un coste nuevo, el par
    /// sumaría cero en unidades y distinto de cero en valor, y anular movería el valor del
    /// almacén sin mover una sola unidad.
    /// </para>
    /// <para>
    /// <b>Y se afirma que hay líneas que mirar antes de mirarlas</b> (ADR-0020): sobre un inverso
    /// sin líneas, un bucle que compara no compara nada y el caso sale verde solo.
    /// </para>
    /// </remarks>
    [Fact]
    public void El_inverso_copia_las_lineas_con_la_cantidad_negada()
    {
        (Ajuste original, _) = UnAjusteConfirmadoDeDosLineas();

        Ajuste inverso = original.CrearInverso(s_diaDeLaAnulacion, "Me equivoqué", s_momento);

        inverso.Lineas.Count.ShouldBe(
            2, "el inverso tiene una línea por línea del original, ni una más ni una menos");

        foreach ((LineaDeAjuste suya, LineaDeAjuste mia) in original.Lineas.Zip(inverso.Lineas))
        {
            mia.CantidadIntroducida.ShouldBe(-suya.CantidadIntroducida);
            mia.CosteUnitario.ShouldBe(suya.CosteUnitario);
            mia.ArticuloId.ShouldBe(suya.ArticuloId);
            mia.UbicacionId.ShouldBe(suya.UbicacionId);
            mia.UnidadIntroducidaId.ShouldBe(suya.UnidadIntroducidaId);
            mia.FactorAUnidadBase.ShouldBe(suya.FactorAUnidadBase);
        }
    }

    /// <summary>El inverso hereda dónde y en qué serie, y estrena fecha, número y estado.</summary>
    /// <remarks>
    /// <b>La serie es la misma que la del original y eso es una decisión, no una omisión</b>: un
    /// tipo de documento propio para las anulaciones obligaría a cada empresa a abrir una serie
    /// más antes de poder anular, y dejaría la anulación fuera de la numeración del documento que
    /// compensa. Con la misma serie, un ejercicio cerrado impide anular con el mismo error con el
    /// que impide confirmar, que es la respuesta correcta y no un efecto secundario.
    /// </remarks>
    [Fact]
    public void El_inverso_hereda_la_serie_y_el_almacen_y_estrena_la_fecha()
    {
        (Ajuste original, _) = UnAjusteConfirmadoDeDosLineas();

        Ajuste inverso = original.CrearInverso(s_diaDeLaAnulacion, "Me equivoqué", s_momento);

        inverso.SerieId.ShouldBe(original.SerieId);
        inverso.AlmacenId.ShouldBe(original.AlmacenId);
        inverso.EmpresaId.ShouldBe(original.EmpresaId);

        inverso.FechaDeOperacion.ShouldBe(s_diaDeLaAnulacion);
        inverso.FechaDeOperacion.ShouldNotBe(
            original.FechaDeOperacion,
            "el inverso NO hereda la fecha: con la del original se asentaría en su mismo periodo " +
            "y anular un documento de un ejercicio cerrado sería escribir dentro de él");

        inverso.Id.ShouldNotBe(original.Id);
        inverso.AnulaAId.ShouldBe(original.Id);
        inverso.Numero.ShouldBeNull("el número lo da la serie al confirmar, no al construir");
        inverso.Estado.ShouldBe(EstadoDeAjuste.Borrador);
    }

    /// <summary>
    /// Las filas del libro del original y las del inverso suman cero por artículo, almacén y
    /// ubicación.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Esta es la afirmación del ítem, y se hace en unidad base a propósito.</b> La línea
    /// guarda la cantidad tal como se escribió; la que mueve el stock es la que calcula
    /// <see cref="MovimientoStock.EnUnidadBase"/> con el factor. Sumar las introducidas saldría
    /// verde con un factor perdido por el camino, y el almacén habría quedado movido.
    /// </para>
    /// <para>
    /// <b>Se agrupa por artículo, almacén y ubicación</b> y no en un solo montón: un total general
    /// a cero admite que una ubicación quede de más y otra de menos, que es exactamente el daño
    /// que hace mover la mercancía de sitio al anular.
    /// </para>
    /// <para>
    /// <b>Y se afirma que hay grupos antes de exigirles el cero</b> (ADR-0020). Sobre cero grupos,
    /// «todos suman cero» es verdad y no dice nada; el caso saldría verde con un inverso sin
    /// líneas, que es justo la avería que tiene que cazar. Por lo mismo se exige que lo que el
    /// original movió no fuera ya cero: un par de ceros también suma cero.
    /// </para>
    /// </remarks>
    [Fact]
    public void El_par_suma_cero_en_unidad_base()
    {
        (Ajuste original, IReadOnlyList<MovimientoStock> delOriginal) =
            UnAjusteConfirmadoDeDosLineas();

        Ajuste inverso = original.CrearInverso(s_diaDeLaAnulacion, "Me equivoqué", s_momento);
        IReadOnlyList<MovimientoStock> delInverso =
            inverso.Confirmar(NumeroDelInverso, Confirmado(inverso), s_momento);

        var grupos = delOriginal.Concat(delInverso)
            .GroupBy(movimiento => new
            {
                movimiento.ArticuloId,
                movimiento.AlmacenId,
                movimiento.UbicacionId,
            })
            .Select(grupo => new
            {
                grupo.Key,
                Suma = grupo.Sum(movimiento => movimiento.CantidadEnUnidadBase),
                Filas = grupo.Count(),
            })
            .ToList();

        grupos.Count.ShouldBe(
            2,
            "el par ha movido dos artículos: con cero grupos, «todos suman cero» sería verdad " +
            "sin haber sumado nada (ADR-0020)");

        delOriginal.ShouldAllBe(
            movimiento => movimiento.CantidadEnUnidadBase != 0m,
            "un original que no moviera nada haría que el par sumara cero por no haber nada");

        foreach (var grupo in grupos)
        {
            grupo.Filas.ShouldBe(
                2, "cada artículo tiene su fila del original y su fila del inverso");

            grupo.Suma.ShouldBe(
                0m,
                $"el par deja el artículo «{grupo.Key.ArticuloId}» donde estaba, y en esta " +
                $"ubicación ha quedado movido en {grupo.Suma}");
        }
    }

    /// <summary>Construir el inverso no anula nada: el original sigue confirmado.</summary>
    /// <remarks>
    /// <b>Esta costura es la que el barrido de la doble flecha vigila</b>, y por eso conviene
    /// verla escrita aquí: entre crear el inverso y dar el original por anulado hay dos pasos, y
    /// un inverso confirmado cuyo original siga <c>Confirmado</c> es un ajuste compensado que
    /// cualquier consulta de documentos sigue contando como vivo.
    /// </remarks>
    [Fact]
    public void Crear_el_inverso_no_anula_el_original()
    {
        (Ajuste original, _) = UnAjusteConfirmadoDeDosLineas();

        Ajuste inverso = original.CrearInverso(s_diaDeLaAnulacion, "Me equivoqué", s_momento);
        inverso.Confirmar(NumeroDelInverso, Confirmado(inverso), s_momento);

        original.Estado.ShouldBe(EstadoDeAjuste.Confirmado);
        original.EventosPendientes.Count.ShouldBe(
            1, "solo ha contado su confirmación: nadie ha anulado nada todavía");
    }

    /// <summary>Un ajuste ya anulado no da un segundo inverso.</summary>
    /// <remarks>
    /// <b>Esto es la mitad en memoria de «anular dos veces no crea dos inversos»</b>, y no la
    /// sustituye: la otra mitad —dos peticiones a la vez contra la misma fila— no se puede
    /// afirmar aquí, porque lo que las separa es el testigo de concurrencia de la base. Esa vive
    /// en el carril de integración, con dos transacciones de verdad.
    /// </remarks>
    [Fact]
    public void Un_ajuste_anulado_no_da_un_segundo_inverso()
    {
        (Ajuste original, _) = UnAjusteConfirmadoDeDosLineas();

        Ajuste inverso = original.CrearInverso(s_diaDeLaAnulacion, "Me equivoqué", s_momento);
        inverso.Confirmar(NumeroDelInverso, Confirmado(inverso), s_momento);
        original.Anular(inverso, Anulado(original));

        Should.Throw<InvalidOperationException>(
            () => original.CrearInverso(s_diaDeLaAnulacion, "Otra vez", s_momento));

        original.Estado.ShouldBe(EstadoDeAjuste.Anulado);
    }

    /// <summary>Anular con el inverso de otro documento no cuela.</summary>
    /// <remarks>
    /// <b>Sin esta guarda el par quedaría cruzado</b>: dos originales anulados y un solo inverso.
    /// El barrido de la doble flecha lo vería después, pero el daño ya estaría escrito en un
    /// libro de solo añadido, y deshacerlo pide otro documento más.
    /// </remarks>
    [Fact]
    public void Anular_rechaza_el_inverso_de_otro_ajuste()
    {
        (Ajuste mio, _) = UnAjusteConfirmadoDeDosLineas();
        (Ajuste ajeno, _) = UnAjusteConfirmadoDeDosLineas();

        Ajuste inversoDelAjeno = ajeno.CrearInverso(s_diaDeLaAnulacion, "Me equivoqué", s_momento);
        inversoDelAjeno.Confirmar(NumeroDelInverso, Confirmado(inversoDelAjeno), s_momento);

        Should.Throw<InvalidOperationException>(() => mio.Anular(inversoDelAjeno, Anulado(mio)));

        mio.Estado.ShouldBe(EstadoDeAjuste.Confirmado);
    }

    /// <summary>Anular contra un inverso que todavía no ha movido el libro no cuela.</summary>
    /// <remarks>
    /// <b>El orden es la regla</b>: primero existe el documento que compensa y después el
    /// original se da por anulado. Al revés habría un instante —y, si algo falla por medio, un
    /// estado permanente— con un ajuste anulado y su efecto en el libro sin compensar.
    /// </remarks>
    [Fact]
    public void Anular_rechaza_un_inverso_en_borrador()
    {
        (Ajuste original, _) = UnAjusteConfirmadoDeDosLineas();

        Ajuste inverso = original.CrearInverso(s_diaDeLaAnulacion, "Me equivoqué", s_momento);

        Should.Throw<InvalidOperationException>(() => original.Anular(inverso, Anulado(original)));

        original.Estado.ShouldBe(EstadoDeAjuste.Confirmado);
        inverso.Estado.ShouldBe(EstadoDeAjuste.Borrador);
    }

    /// <summary>Sin inverso no hay anulación, y el nulo lo dice al entrar.</summary>
    /// <remarks>
    /// El inverso está en la firma a propósito: una anulación sin contra-documento es la media
    /// regla que la R2 no admite. Admitir el nulo la convertiría en «acuérdate de crearlo».
    /// </remarks>
    [Fact]
    public void Sin_inverso_no_hay_anulacion()
    {
        (Ajuste original, _) = UnAjusteConfirmadoDeDosLineas();

        Should.Throw<ArgumentNullException>(() => original.Anular(null!, Anulado(original)));

        original.Estado.ShouldBe(EstadoDeAjuste.Confirmado);
    }

    /// <summary>
    /// Un ajuste confirmado de dos líneas, con su factor distinto de uno y con las dos cantidades
    /// de signo contrario, y las filas del libro que dejó.
    /// </summary>
    /// <remarks>
    /// <b>El factor no es uno y las cantidades no son las dos positivas</b>, y las dos cosas
    /// hacen falta. Con factor uno, la cantidad introducida y la de unidad base coinciden y un
    /// cálculo que se saltara el factor saldría verde; con las dos entradas del mismo signo, un
    /// inverso que en vez de negar pusiera el valor absoluto en negativo también saldría verde.
    /// </remarks>
    private static (Ajuste Ajuste, IReadOnlyList<MovimientoStock> Movimientos)
        UnAjusteConfirmadoDeDosLineas()
    {
        var ajuste = Ajuste.Abrir(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            s_diaDelOriginal,
            "Recuento de marzo",
            s_momento);

        ConLinea(ajuste, cantidad: 3m, factor: 12m);
        ConLinea(ajuste, cantidad: -2.5m, factor: 1m);

        return (ajuste, ajuste.Confirmar(NumeroDelOriginal, Confirmado(ajuste), s_momento));
    }

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
