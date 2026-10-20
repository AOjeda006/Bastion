using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Transferencias;

/// <summary>
/// El descarte del borrador (ADR-0068 §5), como el del ajuste: una marca de tiempo, y no un estado,
/// que solo se pone a un borrador, una sola vez, y que lo deja sin camino hacia el libro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no es una transición.</b> Un borrador de transferencia no numeró, no movió nada y no
/// bloqueó nada: se borra, y el borrado lógico es para los borradores y los maestros. El estado se
/// queda en <see cref="EstadoDeTransferencia.Borrador"/>, sin motivo y sin evento. Lo que lo esconde
/// es el filtro con nombre del contexto, y eso se prueba contra la base.
/// </para>
/// <para>
/// <b>Y el dominio se defiende solo, aunque el filtro lo esconda.</b> Una descartada que llegara a
/// <see cref="Transferencia.Enviar"/> por un camino que se saltara el filtro sacaría mercancía del
/// origen desde un documento que nadie ve. Así que enviar y añadir una línea lanzan contra ella, y
/// esos dos casos son la mitad que el filtro no cubre. Recibir y anular ya no llegan: piden una
/// enviada, y una descartada es siempre un borrador.
/// </para>
/// </remarks>
public sealed class ElDescarteDeLaTransferenciaTests
{
    private static readonly DateOnly s_diaDePartida = new(2026, 3, 2);

    private static readonly DateOnly s_diaDeEnvio = new(2026, 3, 14);

    private static readonly DateOnly s_diaDeLlegada = new(2026, 3, 16);

    /// <summary>Otro instante que el de la apertura, para que la marca no pueda salir de él.</summary>
    private static readonly DateTimeOffset s_alDescartar = new(2026, 3, 15, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Descartar_un_borrador_lo_marca_con_su_instante_y_lo_deja_en_borrador_sin_contarlo()
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);

        // La pareja de la marca: si naciera puesta, la de abajo saldría verde sin descartar nada.
        transferencia.DescartadoEn.ShouldBeNull("un borrador recién abierto está vivo");

        transferencia.Descartar(s_alDescartar);

        transferencia.DescartadoEn.ShouldBe(s_alDescartar, "la marca es el instante que se le da, no otro");

        transferencia.Estado.ShouldBe(
            EstadoDeTransferencia.Borrador,
            "descartar no es una transición: es un borrado lógico del borrador (ADR-0068 §5)");

        transferencia.Numero.ShouldBeNull("un borrador tirado no gasta correlativo, y por eso no deja hueco");

        transferencia.EventosPendientes.ShouldBeEmpty(
            "tirar un borrador no es un hecho que nadie de fuera necesite: no movió nada");
    }

    [Theory]
    [InlineData(false, EstadoDeTransferencia.Enviada)]
    [InlineData(true, EstadoDeTransferencia.Recibida)]
    public void Una_enviada_o_una_recibida_no_se_descarta_porque_ya_escribio_el_libro(
        bool recibirla, EstadoDeTransferencia estado)
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(transferencia);

        if (recibirla)
        {
            libro.Recibir(transferencia, s_diaDeLlegada);
        }

        transferencia.Estado.ShouldBe(estado);

        InvalidOperationException fallo = Should.Throw<InvalidOperationException>(
            () => transferencia.Descartar(s_alDescartar));

        fallo.Message.ShouldContain(estado.ToString());
        fallo.Message.ShouldContain("R2");

        transferencia.DescartadoEn.ShouldBeNull("la guarda va antes de la marca");
    }

    [Fact]
    public void Descartar_dos_veces_lanza_y_no_mueve_la_marca()
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        transferencia.Descartar(s_alDescartar);

        Should.Throw<InvalidOperationException>(() => transferencia.Descartar(s_alDescartar.AddHours(1)))
            .Message.ShouldContain("ya se descartó");

        transferencia.DescartadoEn.ShouldBe(
            s_alDescartar, "la segunda vez no repinta el instante: la auditoría diría otra hora");
    }

    [Fact]
    public void Una_descartada_no_se_envia()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        transferencia.Descartar(s_alDescartar);

        // Con la valoración de una línea de 4 a 2, la que el libro de la prueba daría: sin la marca,
        // este envío saldría bien, y lo que lo para es el descarte.
        Should.Throw<InvalidOperationException>(() => transferencia.Enviar(
                101,
                ElLibroDeLaPrueba.ElEnvioDe(transferencia),
                [UnaValoracion(8m)],
                LotesYSeriesResueltos.Ninguno,
                ElLibroDeLaPrueba.Momento))
            .Message.ShouldContain("descartado");

        transferencia.Estado.ShouldBe(EstadoDeTransferencia.Borrador);
        transferencia.Numero.ShouldBeNull("la guarda va antes de la transición y del número");
        libro.FisicoEn(ElLibroDeLaPrueba.Origen).ShouldBe(10m, "no salió nada del origen");
    }

    [Fact]
    public void Una_descartada_no_admite_lineas()
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        transferencia.Descartar(s_alDescartar);

        Should.Throw<InvalidOperationException>(() => transferencia.AnadirLinea(
                ElLibroDeLaPrueba.UbicacionDelOrigen,
                ElLibroDeLaPrueba.UbicacionDelDestino,
                ElLibroDeLaPrueba.Articulo,
                1m,
                ElLibroDeLaPrueba.Unidad,
                1m,
                ElLibroDeLaPrueba.Momento))
            .Message.ShouldContain("descartado");

        transferencia.Lineas.Count.ShouldBe(1, "la línea que se intentó no entra");
    }

    private static ElLibroDeLaPrueba UnLibroConExistenciasEnElOrigen()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);

        return libro;
    }

    private static LineaValorada UnaValoracion(decimal valor) => new(
        Importe.De(valor, ElLibroDeLaPrueba.Divisa), PrecioUnitario.De(2m, ElLibroDeLaPrueba.Divisa));
}
