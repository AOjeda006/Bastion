using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.UnitTests.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Transferencias;

/// <summary>
/// El inverso con el que se anula una transferencia: niega cada pata que el original escribió, y ni
/// él ni el original crean o destruyen valor (ADR-0053 §5 y §6).
/// </summary>
/// <remarks>
/// <para>
/// <b>De una enviada</b> vuelve al origen el valor exacto que salió, y el tránsito baja a cero. <b>De
/// una recibida</b> sale del destino lo que entró, como mucho lo que quede y todo lo que quede si
/// lo vacía, y el origen recibe eso: lo que salió del destino, no lo que salió del origen. Es la
/// tercera respuesta de la puerta (ADR-0054).
/// </para>
/// <para>
/// <b>La afirmación que decide es la del valor de la empresa</b>, que es el mismo antes y después de
/// anular. Que el origen recupere su cantidad no lo dice: con el tope, recupera la cantidad y no
/// todo el valor, y es lo correcto.
/// </para>
/// </remarks>
public sealed class ElInversoDeLaTransferenciaTests
{
    private const string Motivo = "Se mandó al almacén equivocado";

    private static readonly DateOnly s_diaDePartida = new(2026, 3, 2);

    private static readonly DateOnly s_diaDeEnvio = new(2026, 3, 14);

    private static readonly DateOnly s_diaDeLlegada = new(2026, 3, 16);

    private static readonly DateOnly s_diaDeLaSalidaDelDestino = new(2026, 3, 20);

    private static readonly DateOnly s_diaDeLaEntradaEnElDestino = new(2026, 3, 21);

    private static readonly DateOnly s_diaDeLaAnulacion = new(2026, 4, 2);

    /// <summary>El inverso copia cada línea con la cantidad negada, y el valor que viajó como el que compensa.</summary>
    [Fact]
    public void El_inverso_copia_las_lineas_negadas_con_el_valor_que_compensa()
    {
        ElLibroDeLaPrueba libro = UnLibroConUnOrigenDePrecioInexacto();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(1m, s_diaDeEnvio);
        libro.Enviar(original);

        Transferencia inverso = original.CrearInverso(s_diaDeLaAnulacion, Motivo, ElLibroDeLaPrueba.Momento);

        LineaDeTransferencia suya = original.Lineas.ShouldHaveSingleItem();
        LineaDeTransferencia mia = inverso.Lineas.ShouldHaveSingleItem();

        suya.Valor.ShouldBe(3.3333m, "sin valor en el original, no habría nada que compensar");
        mia.CantidadIntroducida.ShouldBe(-suya.CantidadIntroducida);
        mia.ValorQueCompensa.ShouldBe(-suya.Valor);
        mia.Valor.ShouldBeNull("lo que vuelve se sabe al valorar, no al construir");
        mia.UbicacionOrigenId.ShouldBe(suya.UbicacionOrigenId);
        mia.UbicacionDestinoId.ShouldBe(suya.UbicacionDestinoId);
        mia.ArticuloId.ShouldBe(suya.ArticuloId);
        mia.Numero.ShouldBe(suya.Numero, "el inverso se escribe en el orden del original");

        inverso.AlmacenOrigenId.ShouldBe(original.AlmacenOrigenId, "el mismo documento, negado: no se da la vuelta");
        inverso.AlmacenDestinoId.ShouldBe(original.AlmacenDestinoId);
    }

    /// <summary>
    /// De una enviada, vuelve al origen el valor exacto que salió, el tránsito baja a cero y el
    /// destino no se toca.
    /// </summary>
    /// <remarks>
    /// El origen tiene un precio medio de 3,333333. Revalorar la vuelta a ese precio —en vez de con el
    /// importe que salió— dejaría el origen en 9,9999 y no en 10.
    /// </remarks>
    [Fact]
    public void De_una_enviada_vuelve_al_origen_el_valor_exacto_y_el_transito_se_vacia()
    {
        ElLibroDeLaPrueba libro = UnLibroConUnOrigenDePrecioInexacto();
        decimal valorDeLaEmpresa = libro.ValorDeLaEmpresa();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(1m, s_diaDeEnvio);
        libro.Enviar(original);
        libro.EnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(1m, "sin nada en vuelo, vaciarlo no diría nada");

        (Transferencia inverso, LoQueMueveLaTransferencia movido) = libro.Anular(original, s_diaDeLaAnulacion);

        MovimientoStock vuelta = movido.Movimientos.ShouldHaveSingleItem("de una enviada solo hay una pata que negar");
        vuelta.AlmacenId.ShouldBe(ElLibroDeLaPrueba.Origen);
        vuelta.CantidadEnUnidadBase.ShouldBe(1m);
        vuelta.Valor.ShouldBe(Importe.De(3.3333m, ElLibroDeLaPrueba.Divisa));
        vuelta.FechaDeOperacion.ShouldBe(s_diaDeLaAnulacion);
        vuelta.DocumentoOrigenId.ShouldBe(inverso.Id);

        MovimientoEnTransito transito = movido.Transito.ShouldHaveSingleItem();
        transito.Cantidad.ShouldBe(-1m);
        transito.Valor.ShouldBe(Importe.De(-3.3333m, ElLibroDeLaPrueba.Divisa));

        libro.FisicoEn(ElLibroDeLaPrueba.Origen).ShouldBe(3m);
        libro.ValorEn(ElLibroDeLaPrueba.Origen).ShouldBe(10m);
        libro.EnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
        libro.ValorEnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
        libro.FisicoEn(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
        libro.ValorDeLaEmpresa().ShouldBe(valorDeLaEmpresa);

        inverso.Lineas.ShouldHaveSingleItem().Valor.ShouldBe(-3.3333m);
    }

    /// <summary>
    /// De una recibida, el inverso niega las dos patas, y si nada más se movió, cada clave vuelve a
    /// lo que tenía.
    /// </summary>
    [Fact]
    public void De_una_recibida_niega_las_dos_patas()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);
        libro.Ajustar(ElLibroDeLaPrueba.Destino, ElLibroDeLaPrueba.UbicacionDelDestino, 10m, 5m, s_diaDePartida);
        int filasDePartida = libro.Filas.Count;
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(original);
        libro.Recibir(original, s_diaDeLlegada);

        (_, LoQueMueveLaTransferencia movido) = libro.Anular(original, s_diaDeLaAnulacion);

        movido.Movimientos.Count.ShouldBe(2, "de una recibida hay dos patas que negar");
        movido.Transito.ShouldBeEmpty("ni la recibida ni su inverso dejan nada en vuelo");

        var porClave = libro.Filas
            .Skip(filasDePartida)
            .GroupBy(fila => (fila.AlmacenId, fila.UbicacionId))
            .Select(grupo => (
                Clave: grupo.Key,
                Cantidad: grupo.Sum(fila => fila.CantidadEnUnidadBase),
                Valor: grupo.Sum(fila => fila.Valor.Cantidad)))
            .ToList();

        porClave.Count.ShouldBe(2, "una clave en el origen y otra en el destino");
        porClave.ShouldAllBe(clave => clave.Cantidad == 0m && clave.Valor == 0m);

        libro.ValorEn(ElLibroDeLaPrueba.Origen).ShouldBe(20m);
        libro.ValorEn(ElLibroDeLaPrueba.Destino).ShouldBe(50m);
    }

    /// <summary>
    /// Al anular una recibida, el origen recibe lo que salió del destino, que el tope deja por debajo
    /// de lo que salió del origen; y la línea guarda las dos cifras.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Salen 5 unidades por 10 €. En el destino, después, un ajuste saca 3 a su precio medio, 6 €, y
    /// otro mete 3 a 0,50, 1,50 €: quedan 5 unidades por 5,50 €. El inverso quiere sacar 10 €, y como
    /// vacía la clave se lleva lo que hay, 5,50 €. El origen recibe 5,50 €, no 10.
    /// </para>
    /// <para>
    /// <b>Y la empresa vale lo mismo antes y después de anular.</b> Los 4,50 € que no vuelven no se
    /// pierden en la anulación: se perdieron en los dos ajustes del destino, que son los únicos que
    /// mueven el valor de la empresa.
    /// </para>
    /// </remarks>
    [Fact]
    public void De_una_recibida_el_origen_recibe_lo_que_salio_del_destino()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(5m, s_diaDeEnvio);
        libro.Enviar(original);
        libro.Recibir(original, s_diaDeLlegada);
        libro.Ajustar(
            ElLibroDeLaPrueba.Destino, ElLibroDeLaPrueba.UbicacionDelDestino, -3m, null, s_diaDeLaSalidaDelDestino);
        libro.Ajustar(
            ElLibroDeLaPrueba.Destino, ElLibroDeLaPrueba.UbicacionDelDestino, 3m, 0.5m, s_diaDeLaEntradaEnElDestino);
        libro.ValorEn(ElLibroDeLaPrueba.Destino).ShouldBe(5.5m, "la partida del destino tiene que ser la del caso");
        decimal valorDeLaEmpresa = libro.ValorDeLaEmpresa();

        (Transferencia inverso, LoQueMueveLaTransferencia movido) = libro.Anular(original, s_diaDeLaAnulacion);

        MovimientoStock salidaDelDestino = movido.Movimientos.Single(fila => fila.AlmacenId == ElLibroDeLaPrueba.Destino);
        MovimientoStock entradaEnElOrigen = movido.Movimientos.Single(fila => fila.AlmacenId == ElLibroDeLaPrueba.Origen);

        salidaDelDestino.Valor.Cantidad.ShouldBe(-5.5m, "el tope: como mucho lo que queda");
        entradaEnElOrigen.Valor.Cantidad.ShouldBe(5.5m, "el origen recibe lo que salió del destino");
        entradaEnElOrigen.CantidadEnUnidadBase.ShouldBe(5m);

        LineaDeTransferencia linea = inverso.Lineas.ShouldHaveSingleItem();
        linea.ValorQueCompensa.ShouldBe(-10m, "lo que quería compensar");
        linea.Valor.ShouldBe(-5.5m, "y lo que viajó de vuelta");

        libro.ValorDeLaEmpresa().ShouldBe(valorDeLaEmpresa, "anular no crea ni destruye valor");
        libro.FisicoEn(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
        libro.ValorEn(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
        libro.FisicoEn(ElLibroDeLaPrueba.Origen).ShouldBe(10m);
        libro.ValorEn(ElLibroDeLaPrueba.Origen).ShouldBe(15.5m);
    }

    /// <summary>
    /// Si el inverso vacía el destino, se lleva todo lo que queda, que puede valer más de lo que
    /// entró, y el origen recibe eso (ADR-0054).
    /// </summary>
    /// <remarks>
    /// <para>
    /// El origen tiene 5 unidades por 50 €, y el destino, 10 por 200 €. Salen las 5 del origen por
    /// 50 €, y al recibir el destino tiene 15 por 250 €. Un ajuste saca 10 a su precio medio,
    /// 16,666667, y el destino queda con 5 por 83,3333 €. El inverso vacía el destino y se lleva los
    /// 83,3333 €, no los 50 que entraron: sin cantidad no hay valor.
    /// </para>
    /// <para>
    /// <b>Y la empresa vale lo mismo antes y después de anular</b>, porque lo que sale del destino
    /// de más entra entero en el origen.
    /// </para>
    /// </remarks>
    [Fact]
    public void Si_el_inverso_vacia_el_destino_el_origen_recibe_mas_de_lo_que_salio()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 5m, 10m, s_diaDePartida);
        libro.Ajustar(ElLibroDeLaPrueba.Destino, ElLibroDeLaPrueba.UbicacionDelDestino, 10m, 20m, s_diaDePartida);
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(5m, s_diaDeEnvio);
        libro.Enviar(original);
        libro.Recibir(original, s_diaDeLlegada);
        libro.Ajustar(
            ElLibroDeLaPrueba.Destino, ElLibroDeLaPrueba.UbicacionDelDestino, -10m, null, s_diaDeLaSalidaDelDestino);
        libro.ValorEn(ElLibroDeLaPrueba.Destino).ShouldBe(83.3333m, "la partida del destino tiene que ser la del caso");
        libro.ValorEn(ElLibroDeLaPrueba.Origen).ShouldBe(0m, "el origen se vació al enviar");
        decimal valorDeLaEmpresa = libro.ValorDeLaEmpresa();

        (Transferencia inverso, LoQueMueveLaTransferencia movido) = libro.Anular(original, s_diaDeLaAnulacion);

        MovimientoStock salidaDelDestino = movido.Movimientos.Single(fila => fila.AlmacenId == ElLibroDeLaPrueba.Destino);
        MovimientoStock entradaEnElOrigen = movido.Movimientos.Single(fila => fila.AlmacenId == ElLibroDeLaPrueba.Origen);

        salidaDelDestino.Valor.Cantidad.ShouldBe(-83.3333m, "vaciar la clave se lleva todo lo que queda");
        entradaEnElOrigen.Valor.Cantidad.ShouldBe(83.3333m, "el origen recibe lo que salió del destino");

        LineaDeTransferencia linea = inverso.Lineas.ShouldHaveSingleItem();
        linea.ValorQueCompensa.ShouldBe(-50m, "lo que quería compensar");
        linea.Valor.ShouldBe(-83.3333m, "y lo que viajó de vuelta, que es más");

        libro.ValorDeLaEmpresa().ShouldBe(valorDeLaEmpresa, "anular no crea ni destruye valor");
        libro.FisicoEn(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
        libro.ValorEn(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
        libro.FisicoEn(ElLibroDeLaPrueba.Origen).ShouldBe(5m);
        libro.ValorEn(ElLibroDeLaPrueba.Origen).ShouldBe(83.3333m);
    }

    /// <summary>
    /// El inverso no devuelve al origen otra cosa que lo que viajó de vuelta: ni el valor original
    /// cuando el tope cortó, ni otro precio.
    /// </summary>
    [Fact]
    public void El_inverso_no_devuelve_al_origen_otro_valor_que_el_que_volvio()
    {
        ElLibroDeLaPrueba libro = UnLibroConUnOrigenDePrecioInexacto();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(1m, s_diaDeEnvio);
        libro.Enviar(original);
        Transferencia inverso = original.CrearInverso(s_diaDeLaAnulacion, Motivo, ElLibroDeLaPrueba.Momento);

        Should.Throw<ArgumentException>(() => inverso.ConfirmarComoInverso(
            original,
            999,
            ElLibroDeLaPrueba.LaRecepcionDe(inverso, s_diaDeLaAnulacion),
            valoracionEnElDestino: null,
            [UnaValoracion(3.3334m)],
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));

        inverso.Estado.ShouldBe(EstadoDeTransferencia.Borrador);
        inverso.Lineas.ShouldHaveSingleItem().Valor.ShouldBeNull();
    }

    /// <summary>La valoración del destino va si y solo si el original se recibió.</summary>
    /// <param name="recibirAntes">Si el original se recibe antes de anularlo.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void La_valoracion_del_destino_va_si_y_solo_si_el_original_se_recibio(bool recibirAntes)
    {
        ElLibroDeLaPrueba libro = UnLibroConUnOrigenDePrecioInexacto();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(1m, s_diaDeEnvio);
        libro.Enviar(original);

        if (recibirAntes)
        {
            libro.Recibir(original, s_diaDeLlegada);
        }

        Transferencia inverso = original.CrearInverso(s_diaDeLaAnulacion, Motivo, ElLibroDeLaPrueba.Momento);
        IReadOnlyList<LineaValorada>? alReves = recibirAntes ? null : [UnaValoracion(-3.3333m)];

        Should.Throw<ArgumentException>(() => inverso.ConfirmarComoInverso(
            original,
            999,
            ElLibroDeLaPrueba.LaRecepcionDe(inverso, s_diaDeLaAnulacion),
            alReves,
            [UnaValoracion(3.3333m)],
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));

        inverso.Estado.ShouldBe(EstadoDeTransferencia.Borrador);
    }

    /// <summary>Un inverso confirmado contra otro original que el suyo es un defecto.</summary>
    [Fact]
    public void Un_inverso_se_confirma_contra_su_original_y_no_contra_otro()
    {
        ElLibroDeLaPrueba libro = UnLibroConUnOrigenDePrecioInexacto();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(1m, s_diaDeEnvio);
        Transferencia otra = ElLibroDeLaPrueba.UnaTransferenciaDe(1m, s_diaDeEnvio);
        libro.Enviar(original);
        libro.Enviar(otra);
        Transferencia inverso = original.CrearInverso(s_diaDeLaAnulacion, Motivo, ElLibroDeLaPrueba.Momento);

        Should.Throw<InvalidOperationException>(() => inverso.ConfirmarComoInverso(
            otra,
            999,
            ElLibroDeLaPrueba.LaRecepcionDe(inverso, s_diaDeLaAnulacion),
            valoracionEnElDestino: null,
            LaValoracion.DeLasLineas(
                libro.Filas, inverso.LineasAValorarEnElOrigen(), ElLibroDeLaPrueba.Divisa, s_diaDeLaAnulacion),
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));
    }

    /// <summary>Un origen de tres unidades que valen 10: un precio medio que no es exacto.</summary>
    private static ElLibroDeLaPrueba UnLibroConUnOrigenDePrecioInexacto()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 1m, 1m, s_diaDePartida);
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 2m, 4.5m, s_diaDePartida);

        return libro;
    }

    private static LineaValorada UnaValoracion(decimal valor) => new(
        Importe.De(valor, ElLibroDeLaPrueba.Divisa), PrecioUnitario.De(3.333333m, ElLibroDeLaPrueba.Divisa));
}
