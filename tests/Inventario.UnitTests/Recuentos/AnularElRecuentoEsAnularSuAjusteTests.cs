using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Recuentos;
using Shouldly;
using static Bastion.Inventario.UnitTests.Recuentos.ElRecuentoDeLaPrueba;

namespace Bastion.Inventario.UnitTests.Recuentos;

/// <summary>
/// Anular un recuento es anular su ajuste, y el ajuste de un recuento no se anula por separado
/// (ADR-0055 §9). El caso de uso público lo contesta con un <c>409</c>; aquí, el dominio lanza,
/// como invariante.
/// </summary>
public sealed class AnularElRecuentoEsAnularSuAjusteTests
{
    private static readonly DateOnly s_anulacion = new(2026, 10, 9);

    [Fact]
    public void El_ajuste_de_un_recuento_no_da_su_inverso_por_el_camino_de_cualquier_ajuste()
    {
        (_, Ajuste ajuste) = UnRecuentoConfirmadoConAjuste();

        Should.Throw<InvalidOperationException>(
            () => ajuste.CrearInverso(s_anulacion, "Me equivoqué", Momento),
            "su recuento seguiría confirmado con un ajuste anulado: la doble flecha quedaría mintiendo");
    }

    [Fact]
    public void El_recuento_da_el_inverso_de_su_ajuste_y_se_anula_con_el()
    {
        (Recuento recuento, Ajuste ajuste) = UnRecuentoConfirmadoConAjuste();

        Ajuste inverso = recuento.CrearInversoDeSuAjuste(ajuste, s_anulacion, "Se contó dos veces", Momento);

        inverso.AnulaAId.ShouldBe(ajuste.Id);
        inverso.RecuentoId.ShouldBeNull(
            "la flecha del recuento es de su ajuste, y es única: el inverso apunta al ajuste, no al recuento");
        inverso.FechaDeOperacion.ShouldBe(s_anulacion);
        inverso.Lineas.ShouldHaveSingleItem().CantidadIntroducida.ShouldBe(2m);

        ConfirmarSuAjuste(inverso, numero: 10);
        ajuste.Anular(inverso, new AjusteAnulado(ajuste.Id, ajuste.EmpresaId));
        recuento.Anular("Se contó dos veces", ajuste, Anulado(recuento));

        recuento.Estado.ShouldBe(EstadoDeRecuento.Anulado);
        ajuste.Estado.ShouldBe(EstadoDeAjuste.Anulado);
    }

    [Fact]
    public void Un_recuento_no_da_el_inverso_de_un_ajuste_que_no_es_suyo()
    {
        (Recuento recuento, _) = UnRecuentoConfirmadoConAjuste();
        (_, Ajuste ajeno) = UnRecuentoConfirmadoConAjuste();

        Should.Throw<InvalidOperationException>(
            () => recuento.CrearInversoDeSuAjuste(ajeno, s_anulacion, "Se contó dos veces", Momento));
    }

    [Fact]
    public void Un_recuento_en_curso_no_da_ningun_inverso()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        recuento.Contar(recuento.Lineas[0].Id, 3m, 5m);
        Ajuste borrador = recuento.AjusteDeLaDiferencia(TodasA(recuento, 5m), Confirmacion, Momento).ShouldNotBeNull();

        Should.Throw<InvalidOperationException>(
            () => recuento.CrearInversoDeSuAjuste(borrador, s_anulacion, "Me equivoqué", Momento));
    }

    [Fact]
    public void Un_recuento_que_movio_el_libro_no_se_anula_con_su_ajuste_sin_anular()
    {
        (Recuento recuento, Ajuste ajuste) = UnRecuentoConfirmadoConAjuste();

        Should.Throw<ArgumentException>(
            () => recuento.Anular("Se contó dos veces", ajuste, Anulado(recuento)),
            "el ajuste sigue confirmado: anular el recuento dejaría su diferencia en el libro");

        Should.Throw<ArgumentException>(
            () => recuento.Anular("Se contó dos veces", null, Anulado(recuento)),
            "y sin el ajuste, nadie habría compensado lo que movió");

        recuento.Estado.ShouldBe(EstadoDeRecuento.Confirmado);
    }

    [Fact]
    public void Un_recuento_que_no_movio_el_libro_no_se_anula_con_un_ajuste()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        _ = ContarYConfirmar(recuento, TodasA(recuento, 5m), _ => 5m);
        (_, Ajuste ajeno) = UnRecuentoConfirmadoConAjuste();

        Should.Throw<ArgumentException>(() => recuento.Anular("Se contó dos veces", ajeno, Anulado(recuento)));
    }

    [Fact]
    public void Un_inverso_sigue_sin_anularse_tampoco_por_el_camino_del_recuento()
    {
        (Recuento recuento, Ajuste ajuste) = UnRecuentoConfirmadoConAjuste();
        Ajuste inverso = recuento.CrearInversoDeSuAjuste(ajuste, s_anulacion, "Se contó dos veces", Momento);
        ConfirmarSuAjuste(inverso, numero: 10);

        Should.Throw<InvalidOperationException>(() => inverso.CrearInverso(s_anulacion, "Otra vez", Momento));
        Should.Throw<InvalidOperationException>(
            () => recuento.CrearInversoDeSuAjuste(inverso, s_anulacion, "Otra vez", Momento));
    }

    private static (Recuento Recuento, Ajuste Ajuste) UnRecuentoConfirmadoConAjuste()
    {
        Recuento recuento = Abrir(Precargada(Estanteria, Tornillos));
        Ajuste ajuste = ContarYConfirmar(recuento, TodasA(recuento, 5m), _ => 3m).ShouldNotBeNull();

        return (recuento, ajuste);
    }
}
