using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.UnitTests.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Ajustes;

/// <summary>
/// El descarte del borrador (ADR-0065 §5): una marca de tiempo, y no un estado, que solo se pone a
/// un borrador, una sola vez, y que lo deja sin camino hacia el libro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no es una transición.</b> El recuento descartado es un estado porque bloqueó un almacén
/// mientras estuvo en curso, y su listado lo enseña con su motivo. Un borrador de ajuste no numeró,
/// no movió nada y no bloqueó nada: se borra, y el borrado lógico es para los borradores y los
/// maestros. Así que el estado se queda en <see cref="EstadoDeAjuste.Borrador"/>, no hay motivo y no
/// hay evento. Lo que lo esconde es el filtro con nombre del contexto, y eso se prueba contra la base.
/// </para>
/// <para>
/// <b>Y el dominio se defiende solo, aunque el filtro lo esconda.</b> Un descartado que llegara a
/// <see cref="Ajuste.Confirmar"/> por un camino que se saltara el filtro escribiría el libro desde un
/// documento que nadie ve. Así que confirmar y añadir una línea lanzan contra él, y esos dos casos
/// son la mitad que el filtro no cubre.
/// </para>
/// </remarks>
public sealed class ElDescarteDelAjusteTests
{
    private static readonly DateTimeOffset s_alAbrir = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Otro instante, para que la marca no pueda salir del de la apertura.</summary>
    private static readonly DateTimeOffset s_alDescartar = new(2026, 10, 19, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Descartar_un_borrador_lo_marca_con_su_instante_y_lo_deja_en_borrador_sin_contarlo()
    {
        Ajuste ajuste = UnBorradorConUnaLinea();

        // La pareja de la marca: si naciera puesta, la de abajo saldría verde sin descartar nada.
        ajuste.DescartadoEn.ShouldBeNull("un borrador recién abierto está vivo");

        ajuste.Descartar(s_alDescartar);

        ajuste.DescartadoEn.ShouldBe(s_alDescartar, "la marca es el instante que se le da, no otro");

        ajuste.Estado.ShouldBe(
            EstadoDeAjuste.Borrador,
            "descartar no es una transición: es un borrado lógico del borrador (ADR-0065 §5)");

        ajuste.Numero.ShouldBeNull("un borrador tirado no gasta correlativo, y por eso no deja hueco");

        ajuste.EventosPendientes.ShouldBeEmpty(
            "tirar un borrador no es un hecho que nadie de fuera necesite: no movió nada");
    }

    [Fact]
    public void Un_confirmado_no_se_descarta_porque_ya_escribio_el_libro()
    {
        Ajuste ajuste = UnBorradorConUnaLinea();
        Confirmar(ajuste);

        InvalidOperationException fallo = Should.Throw<InvalidOperationException>(
            () => ajuste.Descartar(s_alDescartar));

        fallo.Message.ShouldContain(nameof(EstadoDeAjuste.Confirmado));
        fallo.Message.ShouldContain("R2");

        ajuste.DescartadoEn.ShouldBeNull("la guarda va antes de la marca");
    }

    [Fact]
    public void Descartar_dos_veces_lanza_y_no_mueve_la_marca()
    {
        Ajuste ajuste = UnBorradorConUnaLinea();
        ajuste.Descartar(s_alDescartar);

        Should.Throw<InvalidOperationException>(() => ajuste.Descartar(s_alDescartar.AddHours(1)))
            .Message.ShouldContain("ya se descartó");

        ajuste.DescartadoEn.ShouldBe(
            s_alDescartar, "la segunda vez no repinta el instante: la auditoría diría otra hora");
    }

    [Fact]
    public void Un_descartado_no_se_confirma()
    {
        Ajuste ajuste = UnBorradorConUnaLinea();
        ajuste.Descartar(s_alDescartar);

        Should.Throw<InvalidOperationException>(() => Confirmar(ajuste))
            .Message.ShouldContain("descartado");

        ajuste.Estado.ShouldBe(EstadoDeAjuste.Borrador);
        ajuste.Numero.ShouldBeNull("la guarda va antes de la transición y del número");
    }

    [Fact]
    public void Un_descartado_no_admite_lineas()
    {
        Ajuste ajuste = UnBorradorConUnaLinea();
        ajuste.Descartar(s_alDescartar);

        Should.Throw<InvalidOperationException>(() => ConLinea(ajuste))
            .Message.ShouldContain("descartado");

        ajuste.Lineas.Count.ShouldBe(1, "la línea que se intentó no entra");
    }

    private static Ajuste UnBorradorConUnaLinea()
    {
        var ajuste = Ajuste.Abrir(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 10, 18),
            "Rotura en el muelle",
            "EUR",
            s_alAbrir);

        ConLinea(ajuste);

        return ajuste;
    }

    private static void ConLinea(Ajuste ajuste) =>
        ajuste.AnadirLinea(
            Guid.CreateVersion7(), Guid.CreateVersion7(), 4m, Guid.CreateVersion7(), 1m, 2.50m, s_alAbrir);

    private static void Confirmar(Ajuste ajuste) =>
        ajuste.Confirmar(
            12,
            new AjusteConfirmado(
                ajuste.Id, ajuste.EmpresaId, ajuste.AlmacenId, ajuste.FechaDeOperacion, ajuste.Lineas.Count),
            LaValoracion.DesdeCero(ajuste),
            LotesYSeriesResueltos.Ninguno,
            s_alAbrir);
}
