using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Reservas;

/// <summary>
/// Lo que una salida le pide al disponible de su clave (ADR-0059 §9): pasa de él cuando saca más
/// que el físico menos lo reservado, y no más que el físico.
/// </summary>
/// <remarks>
/// <b>Lo que pasa del físico no es asunto de esta guarda</b>: lo contesta la del hueco, que es el
/// <c>422</c> <c>stock-insuficiente</c> de siempre. El físico es el de la clave: el hueco no llega
/// aquí, y lo que pasa con él lo cuenta <c>LaTransferenciaFrenteAlDisponibleTests</c>.
/// </remarks>
public sealed class ElDisponibleDeLaSalidaTests
{
    private static readonly ClaveDeValoracion s_clave = new(
        Guid.Parse("0197f000-0000-7000-8000-000000000d01"), Guid.Parse("0197f000-0000-7000-8000-000000000d02"));

    private static readonly ClaveDeValoracion s_otra = new(
        Guid.Parse("0197f000-0000-7000-8000-000000000d03"), Guid.Parse("0197f000-0000-7000-8000-000000000d02"));

    /// <summary>
    /// Con 10 de físico y 4 reservados, el disponible es 6: sacar 6 cabe, y una millonésima más ya
    /// no. Sacar el físico entero pasa del disponible, y una millonésima más ya es del hueco.
    /// </summary>
    /// <param name="sale">Lo que saca la salida.</param>
    /// <param name="pasa">Si pasa del disponible.</param>
    [Theory]
    [InlineData("6", false)]
    [InlineData("6.000001", true)]
    [InlineData("10", true)]
    [InlineData("10.000001", false)]
    public void Pasa_del_disponible_entre_el_disponible_y_el_fisico(string sale, bool pasa)
    {
        decimal cantidad = decimal.Parse(sale, System.Globalization.CultureInfo.InvariantCulture);

        SalidaPorEncimaDelDisponible? pasada = ElDisponibleDeLaSalida.LoQuePasa(
            [new LineaAValorar(s_clave, -cantidad)], Saldos((s_clave, 10m, 0m)), Reservado((s_clave, 4m)));

        pasada.ShouldBe(pasa ? new SalidaPorEncimaDelDisponible(s_clave, cantidad, 6m) : null);
    }

    /// <summary>Sin nada reservado, el disponible es el físico, y no hay nada que la guarda vea.</summary>
    [Fact]
    public void Sin_reservas_sacar_todo_el_fisico_no_pasa()
    {
        ElDisponibleDeLaSalida.LoQuePasa(
            [new LineaAValorar(s_clave, -10m)], Saldos((s_clave, 10m, 0m)), Reservado((s_clave, 0m)))
            .ShouldBeNull();
    }

    /// <summary>
    /// Las líneas de una misma clave se suman: dos de 4 caben cada una en un disponible de 6, y
    /// juntas no.
    /// </summary>
    [Fact]
    public void Las_lineas_de_una_clave_se_suman()
    {
        ElDisponibleDeLaSalida.LoQuePasa(
            [new LineaAValorar(s_clave, -4m), new LineaAValorar(s_clave, -4m)],
            Saldos((s_clave, 10m, 0m)),
            Reservado((s_clave, 4m)))
            .ShouldBe(new SalidaPorEncimaDelDisponible(s_clave, 8m, 6m));
    }

    /// <summary>
    /// Lo que vuela hacia la clave no es disponible (ADR-0059 §3): con 10 de físico, 50 en tránsito
    /// y 4 reservados, sacar 7 pasa.
    /// </summary>
    [Fact]
    public void Lo_que_vuela_no_cuenta_como_disponible()
    {
        ElDisponibleDeLaSalida.LoQuePasa(
            [new LineaAValorar(s_clave, -7m)], Saldos((s_clave, 10m, 50m)), Reservado((s_clave, 4m)))
            .ShouldBe(new SalidaPorEncimaDelDisponible(s_clave, 7m, 6m));
    }

    /// <summary>
    /// Cada clave mira su disponible: la primera cabe y la segunda no, y lo que contesta es la
    /// segunda, con sus cifras.
    /// </summary>
    [Fact]
    public void Cada_clave_mira_el_suyo_y_contesta_la_que_pasa()
    {
        ElDisponibleDeLaSalida.LoQuePasa(
            [new LineaAValorar(s_clave, -6m), new LineaAValorar(s_otra, -3m)],
            Saldos((s_clave, 10m, 0m), (s_otra, 5m, 0m)),
            Reservado((s_clave, 4m), (s_otra, 4m)))
            .ShouldBe(new SalidaPorEncimaDelDisponible(s_otra, 3m, 1m));
    }

    /// <summary>
    /// Lo que entra no da disponible a lo que sale: la guarda mira solo las líneas que bajan, y una
    /// entrada de la misma clave no las compensa.
    /// </summary>
    [Fact]
    public void Una_entrada_no_compensa_la_salida()
    {
        ElDisponibleDeLaSalida.LoQuePasa(
            [new LineaAValorar(s_clave, 5m), new LineaAValorar(s_clave, -7m)],
            Saldos((s_clave, 10m, 0m)),
            Reservado((s_clave, 4m)))
            .ShouldBe(new SalidaPorEncimaDelDisponible(s_clave, 7m, 6m));
    }

    private static Dictionary<ClaveDeValoracion, SaldoValorado> Saldos(
        params (ClaveDeValoracion Clave, decimal Fisico, decimal EnTransito)[] saldos) =>
        saldos.ToDictionary(
            saldo => saldo.Clave,
            saldo => new SaldoValorado(
                saldo.Fisico, Importe.De(saldo.Fisico * 5m, "EUR"), enTransito: saldo.EnTransito));

    private static Dictionary<ClaveDeValoracion, decimal> Reservado(
        params (ClaveDeValoracion Clave, decimal Reservado)[] reservado) =>
        reservado.ToDictionary(clave => clave.Clave, clave => clave.Reservado);
}
