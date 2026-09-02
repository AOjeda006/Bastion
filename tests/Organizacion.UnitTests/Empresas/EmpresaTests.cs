using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Organizacion.Domain.Empresas;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Empresas;

public sealed class EmpresaTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

    private static Direccion Fiscal() => Direccion.De(
        "Gran Vía", "31", "28013", "Madrid", "Madrid", "ES");

    private static Empresa Nueva() => Empresa.Crear(
        Nif.De("A58818501"), "Ferretería del Norte, S.L.", Fiscal(), "EUR", RegimenDeIva.General, s_momento);

    [Fact]
    public void Una_empresa_nace_activa()
    {
        Empresa empresa = Nueva();

        empresa.Bloqueo.EstaBloqueado.ShouldBeFalse();
        empresa.Bloqueo.Desde.ShouldBeNull();
        empresa.Bloqueo.Motivo.ShouldBeNull();
        empresa.Id.ShouldNotBe(Guid.Empty);

        // R14: las dos marcas, puestas por la fábrica y en el mismo instante.
        empresa.CreadoEn.ShouldBe(s_momento);
        empresa.ModificadoEn.ShouldBe(s_momento);
    }

    [Fact]
    public void La_divisa_base_se_normaliza_contra_el_catalogo_del_bloque_comun()
    {
        Empresa.Crear(Nif.De("A58818501"), "Norte", Fiscal(), " eur ", RegimenDeIva.General, s_momento)
            .DivisaBase.ShouldBe("EUR");
    }

    [Fact]
    public void Una_divisa_de_la_que_no_se_conoce_el_redondeo_no_puede_ser_la_base_de_una_empresa()
    {
        // Aceptarla dejaría a la empresa sin poder calcular una cuota: R6 exige saber con
        // cuántos decimales se redondea ANTES de emitir la primera factura, no después.
        //
        // El ejemplo era el yen hasta el 0.15, cuando entró en el catálogo con su caso dorado
        // (cero decimales). Ahora es el dinar kuwaití, que sigue fuera.
        Should.Throw<NotSupportedException>(() => Empresa.Crear(
            Nif.De("A58818501"), "Norte", Fiscal(), "KWD", RegimenDeIva.General, s_momento));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void La_razon_social_es_obligatoria(string razonSocial)
    {
        Should.Throw<ArgumentException>(() => Empresa.Crear(
            Nif.De("A58818501"), razonSocial, Fiscal(), "EUR", RegimenDeIva.General, s_momento));
    }

    [Fact]
    public void Bloquear_no_es_borrar_deja_la_empresa_con_su_estado_y_su_fecha()
    {
        // R16 / art. 32 LOPDGDD. Una empresa puede ser un empresario INDIVIDUAL, que es persona
        // física: su razón social es un nombre propio y su domicilio fiscal, un domicilio real.
        // Por eso el estado `Bloqueada` le alcanza igual que a un tercero.
        Empresa empresa = Nueva();
        DateTimeOffset momento = new(2026, 8, 26, 10, 30, 0, TimeSpan.FromHours(2));

        empresa.Bloquear(MotivoDeBloqueo.SupresionSolicitada, momento);

        empresa.Bloqueo.EstaBloqueado.ShouldBeTrue();
        empresa.Bloqueo.Desde.ShouldBe(momento);

        // El motivo se guarda con el bloqueo, y no es decoración: es lo que permite responder
        // «por qué está bloqueado esto» sin ir a buscarlo a la traza.
        empresa.Bloqueo.Motivo.ShouldBe(MotivoDeBloqueo.SupresionSolicitada);
    }

    [Fact]
    public void Bloquear_dos_veces_no_mueve_la_fecha_del_primer_bloqueo()
    {
        // La fecha de bloqueo es la que arranca el plazo de prescripción del art. 32: moverla
        // al re-bloquear alargaría la conservación de datos sin que nadie lo decidiera.
        Empresa empresa = Nueva();
        DateTimeOffset primero = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

        empresa.Bloquear(MotivoDeBloqueo.SupresionSolicitada, primero);
        empresa.Bloquear(MotivoDeBloqueo.SupresionSolicitada, primero.AddDays(30));

        empresa.Bloqueo.Desde.ShouldBe(primero);
    }

    [Fact]
    public void Desbloquear_devuelve_la_empresa_a_activa_y_borra_la_fecha()
    {
        Empresa empresa = Nueva();
        empresa.Bloquear(MotivoDeBloqueo.SupresionSolicitada, s_momento);

        empresa.Desbloquear();

        empresa.Bloqueo.EstaBloqueado.ShouldBeFalse();
        empresa.Bloqueo.Desde.ShouldBeNull();
        empresa.Bloqueo.Motivo.ShouldBeNull();
    }

    [Fact]
    public void Una_empresa_bloqueada_no_se_puede_modificar()
    {
        Empresa empresa = Nueva();
        empresa.Bloquear(MotivoDeBloqueo.SupresionSolicitada, s_momento);

        // El art. 32 impide el TRATAMIENTO de los datos bloqueados, no solo su visualización.
        // Modificarlos es tratarlos.
        Should.Throw<InvalidOperationException>(() =>
            empresa.Modificar("Otra razón social", Fiscal(), "EUR", RegimenDeIva.General));
    }

    [Fact]
    public void Modificar_cambia_lo_que_puede_cambiar_y_deja_quieto_el_NIF()
    {
        Empresa empresa = Nueva();
        var nueva = Direccion.De("Rúa Nova", "7", "15703", "Santiago", "A Coruña", "ES");

        empresa.Modificar("Ferretería del Sur, S.L.", nueva, "EUR", RegimenDeIva.RecargoDeEquivalencia);

        empresa.RazonSocial.ShouldBe("Ferretería del Sur, S.L.");
        empresa.DomicilioFiscal.ShouldBe(nueva);
        empresa.RegimenDeIva.ShouldBe(RegimenDeIva.RecargoDeEquivalencia);

        // El NIF identifica a la empresa ante la AEAT y aparece en cada factura emitida:
        // cambiarlo no es una modificación, es otra empresa.
        empresa.Nif.Valor.ShouldBe("A58818501");
    }
}
