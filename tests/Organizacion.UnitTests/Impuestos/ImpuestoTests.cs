using Bastion.Organizacion.Domain.Impuestos;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Impuestos;

public sealed class ImpuestoTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly s_desde = new(2012, 9, 1);

    private static Impuesto General(DateOnly? hasta = null) => Impuesto.Crear(
        "IVA-GENERAL", "IVA general", TipoDeImpuesto.Iva, 21m, s_desde, hasta, "477000", "472000", s_momento);

    [Fact]
    public void Un_impuesto_nace_vigente_y_sin_fecha_de_fin()
    {
        Impuesto impuesto = General();

        impuesto.Porcentaje.ShouldBe(21m);
        impuesto.Tipo.ShouldBe(TipoDeImpuesto.Iva);
        impuesto.VigenteDesde.ShouldBe(s_desde);
        impuesto.VigenteHasta.ShouldBeNull();

        // R14: las dos marcas puestas y en el mismo instante.
        impuesto.CreadoEn.ShouldBe(s_momento);
        impuesto.ModificadoEn.ShouldBe(s_momento);
    }

    [Theory]
    [InlineData(" iva-general ", "IVA-GENERAL")]
    [InlineData("iva21", "IVA21")]
    public void El_codigo_se_normaliza_a_mayusculas(string escrito, string guardado)
    {
        Impuesto.Crear(
                escrito, "IVA general", TipoDeImpuesto.Iva, 21m, s_desde, null, null, null, s_momento)
            .Codigo.ShouldBe(guardado);

        // Y la puerta que PREGUNTA da exactamente la misma forma: quien comprueba si el código ya
        // existe antes de insertar tiene que buscar por lo que se guarda, no por lo que se tecleó.
        Impuesto.NormalizarCodigo(escrito).ShouldBe(guardado);
    }

    [Fact]
    public void El_tipo_del_dia_es_el_que_regia_ese_dia_y_no_el_ultimo()
    {
        // El caso real: el general del IVA pasó del 18 % al 21 % el 1 de septiembre de 2012. Una
        // factura de agosto de ese año lleva el 18 para siempre.
        var anterior = Impuesto.Crear(
            "IVA-GENERAL",
            "IVA general",
            TipoDeImpuesto.Iva,
            18m,
            new DateOnly(2010, 7, 1),
            new DateOnly(2012, 8, 31),
            null,
            null,
            s_momento);

        Impuesto vigente = General();

        anterior.RigeEl(new DateOnly(2012, 8, 15)).ShouldBeTrue();
        vigente.RigeEl(new DateOnly(2012, 8, 15)).ShouldBeFalse();

        anterior.RigeEl(new DateOnly(2012, 9, 1)).ShouldBeFalse();
        vigente.RigeEl(new DateOnly(2012, 9, 1)).ShouldBeTrue();
    }

    [Fact]
    public void Los_bordes_del_tramo_entran_dentro()
    {
        // Un tramo cerrado por los dos lados: el primer día y el último SE APLICAN. Dejarlos fuera
        // por un `<` de más deja dos días al año sin impuesto y nadie lo mira hasta la liquidación.
        Impuesto impuesto = General(new DateOnly(2026, 12, 31));

        impuesto.RigeEl(s_desde).ShouldBeTrue();
        impuesto.RigeEl(new DateOnly(2026, 12, 31)).ShouldBeTrue();
        impuesto.RigeEl(s_desde.AddDays(-1)).ShouldBeFalse();
        impuesto.RigeEl(new DateOnly(2027, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void Modificar_no_toca_ni_el_porcentaje_ni_el_tipo_ni_el_tramo()
    {
        // La garantía es que no HAY forma de cambiarlos: `Modificar` no los recibe. Esto lo deja
        // escrito como comportamiento y no solo como firma, porque una firma se amplía sin querer.
        Impuesto impuesto = General();

        impuesto.Modificar("IVA general (21 %)", "477001", null);

        impuesto.Nombre.ShouldBe("IVA general (21 %)");
        impuesto.CuentaRepercutido.ShouldBe("477001");
        impuesto.CuentaSoportado.ShouldBeNull();
        impuesto.Porcentaje.ShouldBe(21m);
        impuesto.Tipo.ShouldBe(TipoDeImpuesto.Iva);
        impuesto.VigenteDesde.ShouldBe(s_desde);
        impuesto.VigenteHasta.ShouldBeNull();
    }

    [Fact]
    public void Cerrar_un_tramo_es_como_se_sustituye_un_tipo_por_otro()
    {
        Impuesto impuesto = General();

        impuesto.Cerrar(new DateOnly(2026, 12, 31));

        impuesto.VigenteHasta.ShouldBe(new DateOnly(2026, 12, 31));
        impuesto.RigeEl(new DateOnly(2027, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void Un_tramo_no_puede_cerrarse_antes_de_abrirse()
    {
        Should.Throw<ArgumentException>(() => General().Cerrar(s_desde.AddDays(-1)));

        Should.Throw<ArgumentException>(() => Impuesto.Crear(
            "IVA-GENERAL",
            "IVA general",
            TipoDeImpuesto.Iva,
            21m,
            s_desde,
            s_desde.AddDays(-1),
            null,
            null,
            s_momento));
    }

    [Fact]
    public void Un_tramo_de_un_solo_dia_es_legitimo()
    {
        // El borde de la comprobación anterior: `hasta == desde` no es un error, es un impuesto
        // que rigió exactamente un día. Un `>` en vez de un `>=` lo prohibiría sin motivo.
        Impuesto impuesto = General(s_desde);

        impuesto.RigeEl(s_desde).ShouldBeTrue();
        impuesto.RigeEl(s_desde.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void El_cero_por_ciento_es_un_tipo_y_no_una_ausencia_de_tipo()
    {
        // Una entrega intracomunitaria exenta lleva un impuesto al 0 %, y eso NO es lo mismo que
        // «esta línea no lleva impuesto»: en el modelo 303 va en su casilla, con su base.
        var exento = Impuesto.Crear(
            "IVA-0", "Exento", TipoDeImpuesto.Iva, 0m, s_desde, null, null, null, s_momento);

        exento.Porcentaje.ShouldBe(0m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-15)]
    [InlineData(101)]
    public void Un_porcentaje_fuera_de_rango_no_se_guarda(decimal porcentaje)
    {
        // El negativo tiene su propia trampa: una retención resta POR SER retención, no por
        // llevar el signo puesto. Guardar el 15 % como -15 lo restaría dos veces.
        Should.Throw<ArgumentOutOfRangeException>(() => Impuesto.Crear(
            "RET", "Retención", TipoDeImpuesto.Retencion, porcentaje, s_desde, null, null, null, s_momento));
    }

    [Theory]
    [InlineData("477.000")]
    [InlineData("477A")]
    [InlineData("4770000000")]
    public void Una_cuenta_que_no_es_del_PGC_no_se_guarda(string cuenta)
    {
        Should.Throw<ArgumentException>(() => Impuesto.Crear(
            "IVA-GENERAL", "IVA general", TipoDeImpuesto.Iva, 21m, s_desde, null, cuenta, null, s_momento));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_cuenta_contable_se_guarda_nulo_y_no_cadena_vacia(string? cuenta)
    {
        // Dejar conviviendo el nulo y la cadena vacía daría dos formas de decir «todavía no hay
        // cuenta», y las consultas de Contabilidad tendrían que preguntar por las dos para siempre.
        var impuesto = Impuesto.Crear(
            "IVA-GENERAL", "IVA general", TipoDeImpuesto.Iva, 21m, s_desde, null, cuenta, cuenta, s_momento);

        impuesto.CuentaRepercutido.ShouldBeNull();
        impuesto.CuentaSoportado.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_codigo_y_el_nombre_son_obligatorios(string vacio)
    {
        Should.Throw<ArgumentException>(() => Impuesto.Crear(
            vacio, "IVA general", TipoDeImpuesto.Iva, 21m, s_desde, null, null, null, s_momento));

        Should.Throw<ArgumentException>(() => Impuesto.Crear(
            "IVA-GENERAL", vacio, TipoDeImpuesto.Iva, 21m, s_desde, null, null, null, s_momento));
    }
}
