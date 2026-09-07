using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Terceros.Domain.Terceros;
using Bastion.Terceros.UnitTests.Identificacion;
using Shouldly;

namespace Bastion.Terceros.UnitTests.Terceros;

/// <summary>
/// Las invariantes de lo que cuelga de la ficha: las de conjunto —una preferente, una condición
/// por papel, ningún IBAN repetido— y el tope legal del plazo de pago.
/// </summary>
/// <remarks>
/// Las de conjunto se prueban aquí y no en cada entidad porque el sitio donde se sostienen es el
/// agregado: una <c>CuentaBancaria</c> suelta no sabe si hay otra preferente, y no tiene por qué
/// saberlo.
/// </remarks>
public sealed class LoQueCuelgaDelTerceroTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    private static readonly Direccion s_domicilio = Direccion.De(
        calle: "Calle de la Prueba",
        numero: "1",
        codigoPostal: "28001",
        poblacion: "Madrid",
        subdivision: "Madrid",
        pais: "ES");

    [Fact]
    public void La_primera_cuenta_es_la_preferente_aunque_nadie_lo_pida()
    {
        Tercero tercero = Alta();

        CuentaBancaria cuenta = tercero.AgregarCuentaBancaria(
            Iban.De(IbanesInventados.Valido("ES").Valor), bic: null, alias: null, esPreferente: false,
            s_momento);

        // Una ficha con cuentas y sin ninguna preferente obliga a elegir a quien vaya a pagar, y
        // elegiría por el orden en que la base devuelva las filas.
        cuenta.EsPreferente.ShouldBeTrue(
            "la primera cuenta de una ficha tiene que quedar preferente por sí sola");
    }

    [Fact]
    public void Marcar_una_preferente_baja_a_la_que_lo_era()
    {
        Tercero tercero = Alta();
        CuentaBancaria primera = Cuenta(tercero, "ES");
        CuentaBancaria segunda = Cuenta(tercero, "PT", preferente: true);

        primera.EsPreferente.ShouldBeFalse("la anterior preferente tiene que bajar sola");
        segunda.EsPreferente.ShouldBeTrue();
        tercero.CuentasBancarias.Count(cuenta => cuenta.EsPreferente).ShouldBe(1);
    }

    [Fact]
    public void Elegir_preferente_entre_las_que_ya_estan_deja_una_sola()
    {
        Tercero tercero = Alta();
        CuentaBancaria primera = Cuenta(tercero, "ES");
        Cuenta(tercero, "PT", preferente: true);
        Cuenta(tercero, "FR", preferente: true);

        tercero.MarcarCuentaPreferente(primera.Id);

        tercero.CuentasBancarias.Count(cuenta => cuenta.EsPreferente).ShouldBe(1);
        primera.EsPreferente.ShouldBeTrue();
    }

    [Fact]
    public void Si_se_va_la_preferente_otra_toma_el_relevo()
    {
        Tercero tercero = Alta();
        CuentaBancaria primera = Cuenta(tercero, "ES");
        Cuenta(tercero, "PT", preferente: true);

        tercero.QuitarCuentaBancaria(
            tercero.CuentasBancarias.First(cuenta => cuenta.EsPreferente).Id);

        primera.EsPreferente.ShouldBeTrue(
            "quedarse con cuentas y sin preferente vuelve a obligar a elegir a quien pague");
    }

    [Fact]
    public void La_ultima_cuenta_se_puede_quitar_y_no_queda_ninguna_preferente()
    {
        Tercero tercero = Alta();
        CuentaBancaria unica = Cuenta(tercero, "ES");

        tercero.QuitarCuentaBancaria(unica.Id);

        // Sin cuentas no hay preferente que valga, y eso NO es el estado ambiguo: no hay entre qué
        // elegir. Ambiguo era tener varias y ninguna marcada.
        tercero.CuentasBancarias.ShouldBeEmpty();
    }

    [Fact]
    public void El_mismo_IBAN_dos_veces_en_la_misma_ficha_no_entra()
    {
        Tercero tercero = Alta();
        string iban = IbanesInventados.Valido("ES").Valor;

        tercero.AgregarCuentaBancaria(
            Iban.De(iban), bic: null, alias: "La de siempre", esPreferente: true, s_momento);

        Should.Throw<InvalidOperationException>(() => tercero.AgregarCuentaBancaria(
            Iban.De(iban), bic: null, alias: "La otra", esPreferente: false, s_momento));
    }

    /// <summary>
    /// El mismo IBAN escrito con los espacios de imprenta es el mismo IBAN.
    /// </summary>
    /// <remarks>
    /// Sin esta comprobación, la de arriba podría pasar por la razón equivocada: bastaría con que
    /// el duplicado se buscara comparando la cadena tal como llegó, y entonces «ES91 2100…» y
    /// «ES912100…» convivirían como dos cuentas distintas — y el índice único de la base, que ve
    /// el valor ya normalizado, reventaría al guardar.
    /// </remarks>
    [Fact]
    public void El_mismo_IBAN_con_espacios_tampoco_entra_dos_veces()
    {
        Tercero tercero = Alta();
        string iban = IbanesInventados.Valido("ES").Valor;
        string conEspacios = string.Join(' ', Trozos(iban));

        tercero.AgregarCuentaBancaria(
            Iban.De(iban), bic: null, alias: null, esPreferente: true, s_momento);

        Should.Throw<InvalidOperationException>(() => tercero.AgregarCuentaBancaria(
            Iban.De(conEspacios), bic: null, alias: null, esPreferente: false, s_momento));
    }

    [Fact]
    public void Una_condicion_por_papel_y_la_segunda_cambia_la_primera()
    {
        Tercero tercero = Alta();

        CondicionPago comoCliente = tercero.FijarCondicionPago(
            RolDeCondicionPago.Cliente, 30, diaDePagoFijo: null,
            descuentoPorProntoPago: null, s_momento);

        CondicionPago otraVez = tercero.FijarCondicionPago(
            RolDeCondicionPago.Cliente, 45, diaDePagoFijo: 15,
            descuentoPorProntoPago: 2m, s_momento);

        otraVez.Id.ShouldBe(comoCliente.Id, "fijar la del mismo papel cambia la que ya había");
        tercero.CondicionesPago.Count.ShouldBe(1);
        otraVez.DiasDePlazo.ShouldBe(45);
    }

    [Fact]
    public void Cliente_y_proveedor_llevan_cada_uno_la_suya()
    {
        Tercero tercero = Alta(esCliente: true, esProveedor: true);

        tercero.FijarCondicionPago(RolDeCondicionPago.Cliente, 30, null, null, s_momento);
        tercero.FijarCondicionPago(RolDeCondicionPago.Proveedor, 60, null, null, s_momento);

        // Lo que se concede cobrando no es lo que se acepta pagando (§7.2).
        tercero.CondicionesPago.Count.ShouldBe(2);
    }

    /// <summary>
    /// Sesenta días es el tope, y sesenta y uno ya no entra.
    /// </summary>
    /// <remarks>
    /// Los dos lados del borde en el mismo test, a propósito: sin el caso de sesenta, un tope mal
    /// escrito —un <c>&gt;=</c> donde va un <c>&gt;</c>— rechazaría el plazo legal más largo y
    /// este test seguiría verde.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(60)]
    public void El_plazo_legal_entra(int dias)
    {
        Tercero tercero = Alta();

        tercero.FijarCondicionPago(RolDeCondicionPago.Cliente, dias, null, null, s_momento)
            .DiasDePlazo.ShouldBe(dias);
    }

    [Theory]
    [InlineData(61)]
    [InlineData(90)]
    [InlineData(120)]
    public void Un_plazo_mayor_que_el_legal_no_entra_aunque_las_partes_lo_acuerden(int dias)
    {
        Tercero tercero = Alta();

        // El art. 4 de la Ley 3/2004 dice que el máximo NO ES AMPLIABLE POR ACUERDO ENTRE LAS
        // PARTES: noventa días no es una condición peor, es una cláusula nula. Por eso el tope
        // está en el dominio y no en la pantalla: aquí también se llega desde una importación de
        // CSV, desde una migración y desde un test.
        Should.Throw<ArgumentOutOfRangeException>(() =>
            tercero.FijarCondicionPago(RolDeCondicionPago.Cliente, dias, null, null, s_momento));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public void Un_plazo_negativo_tampoco(int dias)
    {
        Tercero tercero = Alta();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            tercero.FijarCondicionPago(RolDeCondicionPago.Cliente, dias, null, null, s_momento));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(31)]
    public void El_dia_de_pago_fijo_es_un_dia_que_existe_todos_los_meses(int dia)
    {
        Tercero tercero = Alta();

        // Un 30 no existe en febrero y un 31 falta en cuatro meses. Admitirlos obligaría a decidir
        // en el cálculo si se adelanta o se atrasa, y esa decisión cambia quién paga la demora.
        Should.Throw<ArgumentOutOfRangeException>(() =>
            tercero.FijarCondicionPago(RolDeCondicionPago.Cliente, 30, dia, null, s_momento));
    }

    [Fact]
    public void El_limite_de_credito_lleva_su_divisa_y_se_puede_retirar()
    {
        Tercero tercero = Alta();

        tercero.FijarLimiteDeCredito(Importe.De(5_000m, "EUR"));
        tercero.LimiteCredito.ShouldNotBeNull();
        tercero.LimiteCredito!.Divisa.ShouldBe("EUR");

        // Retirar el límite es dejarlo en NADA, que no es lo mismo que dejarlo en cero: cero es
        // «no se le fía ni un euro» y nada es «no se le controla el crédito».
        tercero.FijarLimiteDeCredito(null);
        tercero.LimiteCredito.ShouldBeNull();
    }

    [Fact]
    public void Un_limite_de_credito_negativo_no_es_un_limite()
    {
        Tercero tercero = Alta();

        Should.Throw<ArgumentOutOfRangeException>(
            () => tercero.FijarLimiteDeCredito(Importe.De(-1m, "EUR")));
    }

    /// <summary>
    /// Nada de lo que cuelga se puede tocar con la ficha bloqueada.
    /// </summary>
    /// <remarks>
    /// <b>Las seis operaciones, y no una de muestra.</b> El art. 32 impide el TRATAMIENTO de los
    /// datos reservados, y colgarle un contacto o cambiarle la cuenta es tratarlos igual que
    /// cambiarle la razón social. Una sola de las seis comprobada dejaría las otras cinco abiertas
    /// con este test en verde, que es exactamente el agujero que este ítem vino a cerrar en otro
    /// sitio.
    /// </remarks>
    [Fact]
    public void Con_la_ficha_bloqueada_no_se_toca_nada_de_lo_que_cuelga()
    {
        Tercero tercero = Alta();
        CuentaBancaria cuenta = Cuenta(tercero, "ES");
        Contacto contacto = tercero.AgregarContacto(
            "Quien Sea", cargo: null, correo: null, telefono: null, s_momento);

        tercero.Bloquear(MotivoDeBloqueo.SupresionSolicitada, s_momento);

        Should.Throw<InvalidOperationException>(() => tercero.AgregarContacto(
            "Otro", null, null, null, s_momento));
        Should.Throw<InvalidOperationException>(() => tercero.QuitarContacto(contacto.Id));
        Should.Throw<InvalidOperationException>(() => tercero.AgregarCuentaBancaria(
            Iban.De(IbanesInventados.Valido("PT").Valor), null, null, false, s_momento));
        Should.Throw<InvalidOperationException>(
            () => tercero.MarcarCuentaPreferente(cuenta.Id));
        Should.Throw<InvalidOperationException>(() => tercero.QuitarCuentaBancaria(cuenta.Id));
        Should.Throw<InvalidOperationException>(() => tercero.FijarCondicionPago(
            RolDeCondicionPago.Cliente, 30, null, null, s_momento));
        Should.Throw<InvalidOperationException>(
            () => tercero.FijarLimiteDeCredito(Importe.De(1m, "EUR")));
    }

    [Fact]
    public void Un_contacto_se_cuelga_y_se_quita()
    {
        Tercero tercero = Alta();

        Contacto contacto = tercero.AgregarContacto(
            "Nombre Apellido",
            cargo: "Compras",
            correo: Correo.De("compras@ejemplo.invalid"),
            telefono: "+34 900 000 000",
            s_momento);

        tercero.Contactos.ShouldHaveSingleItem();
        contacto.TerceroId.ShouldBe(tercero.Id);

        tercero.QuitarContacto(contacto.Id);
        tercero.Contactos.ShouldBeEmpty();
    }

    [Fact]
    public void El_regimen_fiscal_comun_es_el_del_territorio_de_aplicacion_del_IVA()
    {
        var comun = RegimenFiscal.Comun();

        comun.Territorio.ShouldBe(TerritorioFiscal.PeninsulaYBaleares);
        comun.RecargoDeEquivalencia.ShouldBeFalse();
        comun.CriterioDeCaja.ShouldBeFalse();
        comun.SujetoARetencionIrpf.ShouldBeFalse();
    }

    [Fact]
    public void Un_territorio_que_no_esta_en_la_lista_cerrada_no_se_guarda()
    {
        // El §7.2 fija cinco. El CHECK de la migración dice lo mismo en el motor, y este dice lo
        // mismo en el dominio: el valor que entre por un `Enum` casteado desde un entero tampoco
        // cuela.
        Should.Throw<ArgumentOutOfRangeException>(
            () => RegimenFiscal.De((TerritorioFiscal)99, false, false, false));
    }

    [Fact]
    public void El_regimen_fiscal_viaja_con_la_ficha_y_se_puede_cambiar()
    {
        Tercero tercero = Alta();

        tercero.RegimenFiscal.Territorio.ShouldBe(TerritorioFiscal.PeninsulaYBaleares);

        tercero.Modificar(
            "Razón Social",
            nombreComercial: null,
            s_domicilio,
            esCliente: true,
            esProveedor: false,
            RegimenFiscal.De(TerritorioFiscal.Canarias, false, false, true));

        tercero.RegimenFiscal.Territorio.ShouldBe(TerritorioFiscal.Canarias);
        tercero.RegimenFiscal.SujetoARetencionIrpf.ShouldBeTrue();
    }

    private static CuentaBancaria Cuenta(Tercero tercero, string pais, bool preferente = false) =>
        tercero.AgregarCuentaBancaria(
            Iban.De(IbanesInventados.Valido(pais).Valor),
            bic: null,
            alias: pais,
            esPreferente: preferente,
            s_momento);

    private static IEnumerable<string> Trozos(string iban)
    {
        for (int desde = 0; desde < iban.Length; desde += 4)
        {
            yield return iban.Substring(desde, Math.Min(4, iban.Length - desde));
        }
    }

    private static Tercero Alta(bool esCliente = true, bool esProveedor = false) =>
        Tercero.Crear(
            Guid.CreateVersion7(),
            IdentificacionFiscal.Espanola(Nif.De(
                IdentificadoresInventados.PersonaJuridica('B', 1_234_567, comoLetra: false).Valido)),
            "Razón Social",
            nombreComercial: null,
            s_domicilio,
            esCliente,
            esProveedor,
            RegimenFiscal.Comun(),
            s_momento);
}
