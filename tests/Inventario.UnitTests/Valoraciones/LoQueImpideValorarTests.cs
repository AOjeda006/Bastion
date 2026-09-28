using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Valoraciones;

/// <summary>
/// Lo que impide valorar, que el caso de uso pregunta antes, y lo que es un defecto de quien llama
/// (ADR-0046 §10).
/// </summary>
/// <remarks>
/// <para>
/// <b>Los impedimentos se preguntan y los defectos lanzan.</b> Un impedimento es algo que el usuario
/// puede arreglar —darle coste a la entrada, o esperar al tipo de cambio— y el borde lo contesta con
/// su código. Un defecto es algo que el borde ya rechazó o que la infraestructura tenía que haber
/// hecho, y si llega aquí, llega por un camino roto.
/// </para>
/// <para>
/// <b>Cada impedimento se comprueba por los dos métodos</b>: <c>LoQueImpide</c> lo dice sin lanzar, y
/// <c>Valorar</c> lanza si se le llama igualmente. Si solo se mirara uno, los dos podrían dejar de
/// decir lo mismo.
/// </para>
/// </remarks>
public sealed class LoQueImpideValorarTests
{
    private static readonly ClaveDeValoracion s_clave = new(
        Guid.Parse("0197f000-0000-7000-8000-000000000b01"), Guid.Parse("0197f000-0000-7000-8000-000000000b02"));

    private static readonly ElPrecioMedioPonderado s_valoracion = new();

    /// <summary>Una entrada sin coste en una clave vacía no tiene precio medio que tomar.</summary>
    [Fact]
    public void Una_entrada_sin_coste_en_una_clave_vacia_no_se_puede_valorar()
    {
        Dictionary<ClaveDeValoracion, SaldoValorado> saldos = new() { [s_clave] = SaldoValorado.Vacio("EUR") };
        LineaAValorar[] lineas = [new(s_clave, 5m)];

        s_valoracion.LoQueImpide(saldos, lineas, "EUR").ShouldBe(
            new ImpedimentoDeValoracion(MotivoDelImpedimento.EntradaSinCosteNiPrecioMedio, s_clave));

        Should.Throw<InvalidOperationException>(() => s_valoracion.Valorar(saldos, lineas, "EUR"));
    }

    /// <summary>
    /// Y la misma línea detrás de una entrada con coste de la misma clave sí se valora: toma el
    /// precio que dejó la otra.
    /// </summary>
    [Fact]
    public void Una_entrada_sin_coste_detras_de_una_con_coste_del_mismo_documento_se_valora()
    {
        Dictionary<ClaveDeValoracion, SaldoValorado> saldos = new() { [s_clave] = SaldoValorado.Vacio("EUR") };
        LineaAValorar[] lineas = [new(s_clave, 10m, Coste(2m, 10m)), new(s_clave, 5m)];

        s_valoracion.LoQueImpide(saldos, lineas, "EUR").ShouldBeNull();

        s_valoracion.Valorar(saldos, lineas, "EUR")[1].ShouldBe(
            new LineaValorada(Importe.De(10m, "EUR"), PrecioUnitario.De(2m, "EUR")));
    }

    /// <summary>Unas existencias valoradas en otra divisa no se mezclan con el documento.</summary>
    [Fact]
    public void Una_clave_con_existencias_en_otra_divisa_no_se_valora()
    {
        Dictionary<ClaveDeValoracion, SaldoValorado> saldos = new()
        {
            [s_clave] = new SaldoValorado(10m, Importe.De(20m, "USD")),
        };
        LineaAValorar[] lineas = [new(s_clave, 5m, Coste(2m, 5m))];

        s_valoracion.LoQueImpide(saldos, lineas, "EUR").ShouldBe(
            new ImpedimentoDeValoracion(MotivoDelImpedimento.ValoracionEnOtraDivisa, s_clave));

        Should.Throw<InvalidOperationException>(() => s_valoracion.Valorar(saldos, lineas, "EUR"));
    }

    /// <summary>Y la misma clave, vacía, empieza de nuevo en la divisa del documento.</summary>
    /// <remarks>
    /// Es el contraste del caso de arriba: lo que impide es sumar, y sin nada que sumar no hay nada
    /// que convertir.
    /// </remarks>
    [Fact]
    public void Una_clave_vacia_en_otra_divisa_empieza_de_nuevo_en_la_del_documento()
    {
        Dictionary<ClaveDeValoracion, SaldoValorado> saldos = new() { [s_clave] = SaldoValorado.Vacio("USD") };
        LineaAValorar[] lineas = [new(s_clave, 5m, Coste(2m, 5m))];

        s_valoracion.LoQueImpide(saldos, lineas, "EUR").ShouldBeNull();

        s_valoracion.Valorar(saldos, lineas, "EUR").ShouldHaveSingleItem().ShouldBe(
            new LineaValorada(Importe.De(10m, "EUR"), PrecioUnitario.De(2m, "EUR")));
    }

    /// <summary>Una clave sin su saldo bloqueado es un defecto de quien llama, no un saldo nuevo.</summary>
    [Fact]
    public void Una_clave_sin_su_saldo_bloqueado_no_se_valora_desde_cero()
    {
        LineaAValorar[] lineas = [new(s_clave, 5m, Coste(2m, 5m))];

        Should.Throw<ArgumentException>(() => s_valoracion.LoQueImpide(
            new Dictionary<ClaveDeValoracion, SaldoValorado>(), lineas, "EUR"));
    }

    /// <summary>Los importes de una línea van en la divisa del documento.</summary>
    [Fact]
    public void Un_coste_en_otra_divisa_que_la_del_documento_es_un_defecto()
    {
        Dictionary<ClaveDeValoracion, SaldoValorado> saldos = new() { [s_clave] = SaldoValorado.Vacio("EUR") };

        Should.Throw<ArgumentException>(() => s_valoracion.LoQueImpide(
            saldos, [new(s_clave, 5m, new CosteDeEntrada(PrecioUnitario.De(2m, "USD"), 5m))], "EUR"));
    }

    /// <summary>Lo que el borde rechaza con <c>ajuste-coste-no-valido</c> no llega a construirse.</summary>
    [Fact]
    public void Una_linea_que_baja_no_lleva_coste_y_ninguna_lo_lleva_negativo()
    {
        Should.Throw<ArgumentException>(() => new LineaAValorar(s_clave, -5m, Coste(2m, 5m)));
        Should.Throw<ArgumentOutOfRangeException>(() => Coste(-2m, 5m));
        Should.Throw<ArgumentOutOfRangeException>(() => Coste(2m, 0m));
        Should.Throw<ArgumentOutOfRangeException>(() => new LineaAValorar(s_clave, 0m));

        Coste(0m, 5m).Valor.ShouldBe(Importe.Cero("EUR"), "una muestra o un regalo entran a cero a propósito");
    }

    /// <summary>El valor que compensa sustituye al coste, y va con el signo de la cantidad.</summary>
    [Fact]
    public void El_valor_que_compensa_no_va_con_coste_ni_con_el_signo_cambiado()
    {
        Should.Throw<ArgumentException>(() => new LineaAValorar(
            s_clave, 5m, Coste(2m, 5m), Importe.De(10m, "EUR")));
        Should.Throw<ArgumentException>(() => new LineaAValorar(s_clave, -5m, valorQueCompensa: Importe.De(10m, "EUR")));

        new LineaAValorar(s_clave, -5m, valorQueCompensa: Importe.Cero("EUR")).ValorQueCompensa
            .ShouldBe(Importe.Cero("EUR"), "el inverso de una línea de antes del 2.8 compensa cero");
    }

    /// <summary>Un saldo cumple las tres restricciones de la tabla, o no es un saldo.</summary>
    [Fact]
    public void Un_saldo_no_baja_de_cero_y_sin_cantidad_no_tiene_valor()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SaldoValorado(-1m, Importe.Cero("EUR")));
        Should.Throw<ArgumentOutOfRangeException>(() => new SaldoValorado(1m, Importe.De(-1m, "EUR")));
        Should.Throw<ArgumentException>(() => new SaldoValorado(0m, Importe.De(1m, "EUR")));

        new SaldoValorado(1m, Importe.Cero("EUR")).PrecioMedio.ShouldBe(PrecioUnitario.De(0m, "EUR"));
        SaldoValorado.Vacio("EUR").PrecioMedio.ShouldBeNull();
    }

    private static CosteDeEntrada Coste(decimal porUnidad, decimal unidades) =>
        new(PrecioUnitario.De(porUnidad, "EUR"), unidades);
}
