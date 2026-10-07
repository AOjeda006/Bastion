using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Recuentos;
using Shouldly;
using static Bastion.Inventario.UnitTests.Recuentos.ElRecuentoDeLaPrueba;

namespace Bastion.Inventario.UnitTests.Recuentos;

/// <summary>
/// El ajuste que genera el recuento al confirmarse (ADR-0055 §8): solo las líneas que difieren, en
/// la unidad base, con la fecha de la confirmación, y con su doble flecha.
/// </summary>
public sealed class ElAjusteDelRecuentoTests
{
    [Fact]
    public void Lleva_solo_las_lineas_que_difieren_con_lo_contado_menos_el_teorico_en_la_unidad_base()
    {
        Recuento recuento = Abrir(
            Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas), Precargada(Estanteria, Taladros));
        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 10m);

        recuento.Contar(LaDe(recuento, Tornillos).Id, 12.5m, 10m);
        recuento.Contar(LaDe(recuento, Tuercas).Id, 10m, 10m);
        recuento.Contar(LaDe(recuento, Taladros).Id, 7m, 10m);

        Ajuste ajuste = recuento.AjusteDeLaDiferencia(teoricos, Confirmacion, Momento).ShouldNotBeNull();

        // El orden de los artículos lo decide su identificador, así que lo esperado sale del recuento.
        IEnumerable<(Guid, decimal)> esperadas = recuento.Lineas
            .Where(linea => linea.ArticuloId != Tuercas)
            .Select(linea => (linea.ArticuloId, linea.ArticuloId == Tornillos ? 2.5m : -3m));

        ajuste.Lineas.Select(linea => (linea.ArticuloId, linea.CantidadIntroducida)).ShouldBe(
            esperadas, "las tuercas cuadran, y una línea de cero no ajusta nada; el orden es el del recuento");

        ajuste.Lineas.ShouldAllBe(linea => linea.UnidadIntroducidaId == Unidades && linea.FactorAUnidadBase == 1m);
        ajuste.Lineas.ShouldAllBe(linea => linea.UbicacionId == Estanteria);
    }

    [Fact]
    public void Lleva_la_cabecera_del_recuento_y_la_fecha_de_la_confirmacion()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        recuento.Contar(recuento.Lineas[0].Id, 4m, 5m);

        Ajuste ajuste = recuento.AjusteDeLaDiferencia(TodasA(recuento, 5m), Confirmacion, Momento).ShouldNotBeNull();

        ajuste.RecuentoId.ShouldBe(recuento.Id, "la doble flecha: el ajuste apunta a su recuento (R13)");
        ajuste.EmpresaId.ShouldBe(recuento.EmpresaId);
        ajuste.AlmacenId.ShouldBe(recuento.AlmacenId);
        ajuste.SerieId.ShouldBe(recuento.SerieDelAjusteId, "numera en la serie de ajustes elegida en el alta");
        ajuste.Motivo.ShouldBe(recuento.Motivo);
        ajuste.Divisa.ShouldBe(recuento.Divisa);

        ajuste.FechaDeOperacion.ShouldBe(
            Confirmacion, "el teórico es el de la confirmación, así que la fecha también (ADR-0055 §1.5)");

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Borrador, "lo confirma el caso de uso, que es quien valora");
        ajuste.AnulaAId.ShouldBeNull();
    }

    [Fact]
    public void Si_todo_cuadra_no_hay_ajuste_y_el_recuento_se_confirma_igual()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas));

        Ajuste? ajuste = ContarYConfirmar(recuento, TodasA(recuento, 3m), _ => 3m);

        ajuste.ShouldBeNull();
        recuento.Estado.ShouldBe(EstadoDeRecuento.Confirmado);
        recuento.Lineas.ShouldAllBe(linea => linea.LineaDeAjusteId == null);
        recuento.MovioElLibro.ShouldBeFalse();
    }

    [Fact]
    public void Al_confirmar_cada_linea_que_difiere_apunta_a_la_del_ajuste_que_la_mueve()
    {
        Recuento recuento = Abrir(
            Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas), Precargada(OtraEstanteria, Tornillos));
        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 10m);

        Ajuste ajuste = ContarYConfirmar(
            recuento, teoricos, linea => linea.ArticuloId == Tuercas ? 10m : 11m).ShouldNotBeNull();

        foreach (LineaDeRecuento linea in recuento.Lineas)
        {
            if (linea.ArticuloId == Tuercas)
            {
                linea.LineaDeAjusteId.ShouldBeNull("cuadraba, así que ninguna línea del ajuste la mueve");
                continue;
            }

            LineaDeAjuste suya = ajuste.Lineas.Single(deAjuste => deAjuste.Id == linea.LineaDeAjusteId);
            suya.UbicacionId.ShouldBe(linea.UbicacionId);
            suya.ArticuloId.ShouldBe(linea.ArticuloId);
        }

        recuento.MovioElLibro.ShouldBeTrue();
    }

    [Fact]
    public void Una_clave_anadida_que_sube_lleva_su_coste_y_una_precargada_no()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        LineaDeRecuento anadida = recuento.AnadirLinea(
            ClaveDelRecuento.De(OtraEstanteria, Tuercas), Unidades, costeUnitario: 1.5m, Momento);
        LineaDeRecuento sinCoste = recuento.AnadirLinea(
            ClaveDelRecuento.De(OtraEstanteria, Taladros), Unidades, costeUnitario: null, Momento);

        recuento.Contar(LaDe(recuento, Tornillos).Id, 8m, 5m);
        recuento.Contar(anadida.Id, 4m, 0m);
        recuento.Contar(sinCoste.Id, 2m, 0m);

        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 0m);
        teoricos[LaDe(recuento, Tornillos).Id] = 5m;

        Ajuste ajuste = recuento.AjusteDeLaDiferencia(teoricos, Confirmacion, Momento).ShouldNotBeNull();

        ajuste.Lineas.Single(linea => linea.ArticuloId == Tornillos).CosteUnitario.ShouldBeNull(
            "una clave que el sistema ya tenía entra al precio medio de su clave (ADR-0055 §6)");
        ajuste.Lineas.Single(linea => linea.ArticuloId == Tuercas).CosteUnitario.ShouldBe(1.5m);
        ajuste.Lineas.Single(linea => linea.ArticuloId == Taladros).CosteUnitario.ShouldBeNull(
            "sin coste, la añadida también entra al precio medio, o la confirmación da el 422 que ya existe");
    }

    [Fact]
    public void Una_clave_anadida_que_baja_no_lleva_coste_porque_sale_al_precio_medio()
    {
        // Una clave que el sistema no tenía en la precarga, pero que su teórico dice que sí hay:
        // alguien la movió después de abrir.
        Recuento recuento = Abrir();
        LineaDeRecuento anadida = recuento.AnadirLinea(
            ClaveDelRecuento.De(Estanteria, Tuercas), Unidades, costeUnitario: 1.5m, Momento);
        recuento.Contar(anadida.Id, 1m, 3m);

        Ajuste ajuste = recuento.AjusteDeLaDiferencia(TodasA(recuento, 3m), Confirmacion, Momento).ShouldNotBeNull();

        ajuste.Lineas.ShouldHaveSingleItem().CosteUnitario.ShouldBeNull();
        ajuste.Lineas[0].CantidadIntroducida.ShouldBe(-2m);
    }

    [Fact]
    public void Los_lotes_y_las_series_viajan_a_la_linea_del_ajuste()
    {
        Recuento recuento = Abrir(
            Precargada(Estanteria, Tornillos, lote: "L-7"), Precargada(Estanteria, Taladros, serie: "SN-1"));
        recuento.Contar(LaDe(recuento, Tornillos).Id, 3m, 5m);
        recuento.Contar(LaDe(recuento, Taladros, "SN-1").Id, 0m, 1m);

        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 5m);
        teoricos[LaDe(recuento, Taladros, "SN-1").Id] = 1m;

        Ajuste ajuste = recuento.AjusteDeLaDiferencia(teoricos, Confirmacion, Momento).ShouldNotBeNull();

        ajuste.Lineas.Single(linea => linea.ArticuloId == Tornillos).CodigoDeLote.ShouldBe("L-7");
        LineaDeAjuste serie = ajuste.Lineas.Single(linea => linea.ArticuloId == Taladros);
        serie.NumeroDeSerie.ShouldBe("SN-1");
        serie.CantidadIntroducida.ShouldBe(-1m);
    }

    [Fact]
    public void Con_una_linea_sin_contar_no_hay_ajuste_que_valga()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas));
        recuento.Contar(LaDe(recuento, Tornillos).Id, 3m, 5m);

        Should.Throw<InvalidOperationException>(
            () => recuento.AjusteDeLaDiferencia(TodasA(recuento, 5m), Confirmacion, Momento));

        Should.Throw<InvalidOperationException>(
            () => recuento.Confirmar(31, Confirmacion, TodasA(recuento, 5m), null, Confirmado(recuento, null)));

        recuento.Estado.ShouldBe(EstadoDeRecuento.EnCurso, "la línea sin contar no es un cero (ADR-0055 §5)");
    }

    [Fact]
    public void Confirmar_con_un_ajuste_que_no_es_la_diferencia_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos), Precargada(Estanteria, Tuercas));
        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 5m);
        recuento.Contar(LaDe(recuento, Tornillos).Id, 3m, 5m);
        recuento.Contar(LaDe(recuento, Tuercas).Id, 5m, 5m);

        Ajuste bueno = recuento.AjusteDeLaDiferencia(teoricos, Confirmacion, Momento).ShouldNotBeNull();
        ConfirmarSuAjuste(bueno);

        // Otro teórico: el ajuste ya no es la diferencia de lo que se confirma.
        Dictionary<Guid, decimal> otros = TodasA(recuento, 5m);
        otros[LaDe(recuento, Tuercas).Id] = 4m;
        Should.Throw<ArgumentException>(
            () => recuento.Confirmar(31, Confirmacion, otros, bueno, Confirmado(recuento, bueno)));

        // Sin el ajuste, cuando hay diferencia.
        Should.Throw<ArgumentException>(
            () => recuento.Confirmar(31, Confirmacion, teoricos, null, Confirmado(recuento, null)));

        recuento.Estado.ShouldBe(EstadoDeRecuento.EnCurso);
        recuento.Lineas.ShouldAllBe(linea => linea.LineaDeAjusteId == null);
    }

    [Fact]
    public void Confirmar_con_el_ajuste_en_borrador_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 5m);
        recuento.Contar(recuento.Lineas[0].Id, 3m, 5m);

        Ajuste borrador = recuento.AjusteDeLaDiferencia(teoricos, Confirmacion, Momento).ShouldNotBeNull();

        Should.Throw<ArgumentException>(
            () => recuento.Confirmar(31, Confirmacion, teoricos, borrador, Confirmado(recuento, borrador)),
            "un recuento confirmado cuyo ajuste no movió el libro sería una diferencia sin escribir");
    }

    [Fact]
    public void Confirmar_con_el_ajuste_de_otro_recuento_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        Recuento otro = Abrir(Precargada(Estanteria, Tornillos));
        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 5m);
        recuento.Contar(recuento.Lineas[0].Id, 3m, 5m);
        otro.Contar(otro.Lineas[0].Id, 3m, 5m);

        Ajuste ajeno = otro.AjusteDeLaDiferencia(TodasA(otro, 5m), Confirmacion, Momento).ShouldNotBeNull();
        ConfirmarSuAjuste(ajeno);

        Should.Throw<ArgumentException>(
            () => recuento.Confirmar(31, Confirmacion, teoricos, ajeno, Confirmado(recuento, ajeno)));
    }

    [Fact]
    public void Confirmar_con_el_ajuste_de_otro_dia_no_se_puede()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        Dictionary<Guid, decimal> teoricos = TodasA(recuento, 5m);
        recuento.Contar(recuento.Lineas[0].Id, 3m, 5m);

        // Generado para el día de la apertura: la diferencia entraría en el libro un día antes de que
        // el recuento la diera por buena.
        Ajuste deOtroDia = recuento.AjusteDeLaDiferencia(teoricos, Apertura, Momento).ShouldNotBeNull();
        ConfirmarSuAjuste(deOtroDia);

        Should.Throw<ArgumentException>(
            () => recuento.Confirmar(31, Confirmacion, teoricos, deOtroDia, Confirmado(recuento, deOtroDia)));

        recuento.Estado.ShouldBe(EstadoDeRecuento.EnCurso);
        recuento.Lineas.ShouldAllBe(linea => linea.LineaDeAjusteId == null);
    }
}
