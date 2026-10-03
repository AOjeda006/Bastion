using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Transferencias;

/// <summary>
/// El valor que sale del origen es el que entra en el destino, y mientras vuela lo cuenta el
/// tránsito (ADR-0053 §1, §2 y §6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que decide es el cuadre</b>: lo que falta en el origen es lo que vuela, en cantidad y en
/// valor, y el valor de la empresa —el de las claves más el que vuela— no se mueve ni al enviar ni
/// al recibir. Un error que valorara la entrada al precio medio del destino dejaría verdes «se
/// recibió» y «el destino tiene la cantidad», y crearía valor por el camino.
/// </para>
/// <para>
/// <b>Los precios se escogen para que se note</b>: el destino tiene su propio precio medio, distinto
/// del del origen, y el origen tiene uno que no es exacto, para que vaciarlo con la cantidad por el
/// precio deje un resto.
/// </para>
/// </remarks>
public sealed class ElValorViajaConLaLineaTests
{
    private static readonly DateOnly s_diaDePartida = new(2026, 3, 2);

    private static readonly DateOnly s_diaDeEnvio = new(2026, 3, 14);

    private static readonly DateOnly s_diaDeLlegada = new(2026, 3, 16);

    /// <summary>
    /// Enviada y no recibida: el origen descontado, el destino sin sumar y el tránsito cuadrando la
    /// diferencia, en cantidad y en valor.
    /// </summary>
    [Fact]
    public void Mientras_vuela_el_transito_cuadra_lo_que_falta_en_el_origen()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);
        decimal valorDeLaEmpresa = libro.ValorDeLaEmpresa();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);

        LoQueMueveLaTransferencia movido = libro.Enviar(transferencia);

        movido.Movimientos.ShouldHaveSingleItem("al enviar solo se escribe la salida del origen");
        movido.Transito.ShouldHaveSingleItem();

        libro.FisicoEn(ElLibroDeLaPrueba.Origen).ShouldBe(6m);
        libro.ValorEn(ElLibroDeLaPrueba.Origen).ShouldBe(12m);
        libro.FisicoEn(ElLibroDeLaPrueba.Destino).ShouldBe(0m, "lo que vuela no ha llegado");
        libro.ValorEn(ElLibroDeLaPrueba.Destino).ShouldBe(0m);

        libro.EnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(10m - 6m);
        libro.ValorEnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(20m - 12m);
        libro.ValorDeLaEmpresa().ShouldBe(valorDeLaEmpresa, "enviar no crea ni destruye valor");

        transferencia.Lineas.ShouldHaveSingleItem().Valor.ShouldBe(8m, "la línea guarda lo que salió");
    }

    /// <summary>
    /// Al recibir, el destino suma el valor que salió del origen, y su precio medio mezcla los dos.
    /// </summary>
    /// <remarks>
    /// Con el precio medio del destino, 5, la entrada valdría 20 y no 8, y la empresa ganaría 12 € por
    /// mover cuatro unidades de sitio.
    /// </remarks>
    [Fact]
    public void Al_recibir_el_destino_suma_lo_que_salio_del_origen_y_su_precio_medio_mezcla()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);
        libro.Ajustar(ElLibroDeLaPrueba.Destino, ElLibroDeLaPrueba.UbicacionDelDestino, 10m, 5m, s_diaDePartida);
        decimal valorDeLaEmpresa = libro.ValorDeLaEmpresa();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(transferencia);

        LoQueMueveLaTransferencia movido = libro.Recibir(transferencia, s_diaDeLlegada);

        MovimientoStock entrada = movido.Movimientos.ShouldHaveSingleItem("al recibir solo se escribe la entrada");
        entrada.AlmacenId.ShouldBe(ElLibroDeLaPrueba.Destino);
        entrada.UbicacionId.ShouldBe(ElLibroDeLaPrueba.UbicacionDelDestino);
        entrada.FechaDeOperacion.ShouldBe(s_diaDeLlegada, "la entrada va con la fecha de llegada");
        entrada.Valor.ShouldBe(Importe.De(8m, ElLibroDeLaPrueba.Divisa));
        entrada.PrecioMedio.ShouldBe(PrecioUnitario.De(58m / 14m, ElLibroDeLaPrueba.Divisa));

        libro.FisicoEn(ElLibroDeLaPrueba.Destino).ShouldBe(14m);
        libro.ValorEn(ElLibroDeLaPrueba.Destino).ShouldBe(58m);
        libro.EnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(0m, "lo que llegó ya no vuela");
        libro.ValorEnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
        libro.ValorDeLaEmpresa().ShouldBe(valorDeLaEmpresa, "recibir no crea ni destruye valor");
    }

    /// <summary>Vaciar el origen se lleva todo su valor, no la cantidad por el precio medio.</summary>
    /// <remarks>
    /// Tres unidades que valen 10 tienen un precio medio de 3,333333. Tres por ese precio son
    /// 9,999999, y el céntimo que faltara se quedaría en un origen sin una sola unidad.
    /// </remarks>
    [Fact]
    public void Vaciar_el_origen_se_lleva_todo_su_valor()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 1m, 1m, s_diaDePartida);
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 2m, 4.5m, s_diaDePartida);
        libro.ValorEn(ElLibroDeLaPrueba.Origen).ShouldBe(10m, "la partida tiene que ser la del caso");
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(3m, s_diaDeEnvio);

        libro.Enviar(transferencia);

        transferencia.Lineas.ShouldHaveSingleItem().Valor.ShouldBe(10m);
        libro.FisicoEn(ElLibroDeLaPrueba.Origen).ShouldBe(0m);
        libro.ValorEn(ElLibroDeLaPrueba.Origen).ShouldBe(0m, "sin unidades no queda valor");
        libro.ValorEnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(10m);
    }

    /// <summary>El tránsito se cuenta en unidad base, como el libro.</summary>
    [Fact]
    public void El_transito_se_cuenta_en_unidad_base()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 24m, 1m, s_diaDePartida);
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(2m, s_diaDeEnvio, factor: 6m);

        libro.Enviar(transferencia);

        libro.EnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(12m, "dos cajas de seis son doce unidades");
        libro.FisicoEn(ElLibroDeLaPrueba.Origen).ShouldBe(12m);

        libro.Recibir(transferencia, s_diaDeLlegada);

        libro.FisicoEn(ElLibroDeLaPrueba.Destino).ShouldBe(12m);
        libro.EnTransitoHacia(ElLibroDeLaPrueba.Destino).ShouldBe(0m);
    }

    /// <summary>
    /// Las dos filas de una línea suman cero en cantidad y en valor, y cada una va a su almacén y a
    /// su ubicación.
    /// </summary>
    [Fact]
    public void Las_dos_patas_de_una_linea_suman_cero()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);

        MovimientoStock salida = libro.Enviar(transferencia).Movimientos.ShouldHaveSingleItem();
        MovimientoStock entrada = libro.Recibir(transferencia, s_diaDeLlegada).Movimientos.ShouldHaveSingleItem();

        salida.AlmacenId.ShouldBe(ElLibroDeLaPrueba.Origen);
        salida.UbicacionId.ShouldBe(ElLibroDeLaPrueba.UbicacionDelOrigen);
        salida.FechaDeOperacion.ShouldBe(s_diaDeEnvio);
        salida.CantidadEnUnidadBase.ShouldBe(-4m);

        entrada.AlmacenId.ShouldBe(ElLibroDeLaPrueba.Destino);
        entrada.CantidadEnUnidadBase.ShouldBe(4m);

        (salida.CantidadEnUnidadBase + entrada.CantidadEnUnidadBase).ShouldBe(0m);
        (salida.Valor.Cantidad + entrada.Valor.Cantidad).ShouldBe(0m);
        salida.Valor.Cantidad.ShouldNotBe(0m, "un par de ceros también suma cero");

        foreach (MovimientoStock fila in new[] { salida, entrada })
        {
            fila.DocumentoOrigenTipo.ShouldBe(TipoDeDocumentoOrigen.Transferencia);
            fila.DocumentoOrigenId.ShouldBe(transferencia.Id, "las dos patas apuntan a su documento (R13)");
        }
    }

    /// <summary>La recepción no acepta una valoración que meta otro valor que el que viajó.</summary>
    [Fact]
    public void La_recepcion_no_acepta_otro_valor_que_el_que_viajo()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(transferencia);

        Should.Throw<ArgumentException>(() => transferencia.Recibir(
            s_diaDeLlegada,
            ElLibroDeLaPrueba.LaRecepcionDe(transferencia, s_diaDeLlegada),
            [new LineaValorada(Importe.De(20m, ElLibroDeLaPrueba.Divisa), PrecioUnitario.De(5m, ElLibroDeLaPrueba.Divisa))],
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));

        transferencia.Estado.ShouldBe(EstadoDeTransferencia.Enviada);
    }
}
