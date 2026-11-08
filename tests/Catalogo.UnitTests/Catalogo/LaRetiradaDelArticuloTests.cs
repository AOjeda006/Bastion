using Bastion.BuildingBlocks.Domain.Retiradas;
using Bastion.Catalogo.Domain.Catalogo;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// El artículo se retira como los maestros de instalación: deja de ofrecerse para lo nuevo y sigue
/// resolviendo lo viejo (ADR-0023, ampliado por el ADR-0082).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que aquí se prueba es la ficha, no la frontera.</b> Qué rechaza un artículo retirado lo
/// deciden los consumidores del puerto, y lo prueban ellos. Aquí, que la ficha nace ofrecida, que
/// las dos transiciones existen y son idempotentes, y que retirar no toca nada más: una retirada
/// que cambiara la descripción o la unidad base dejaría de resolver lo viejo, que es la mitad de lo
/// que significa.
/// </para>
/// </remarks>
public sealed class LaRetiradaDelArticuloTests
{
    private static readonly Guid s_empresa = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_unidad = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid s_impuesto = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DateTimeOffset s_momento =
        new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void El_articulo_es_retirable()
    {
        typeof(IRetirable).IsAssignableFrom(typeof(Articulo)).ShouldBeTrue();
    }

    [Fact]
    public void Un_articulo_nuevo_nace_ofreciendose_para_lo_nuevo()
    {
        Nuevo().EstaRetirada.ShouldBeFalse();
    }

    [Fact]
    public void Retirar_lo_retira_y_reincorporar_lo_devuelve()
    {
        Articulo articulo = Nuevo();

        articulo.Retirar();
        articulo.EstaRetirada.ShouldBeTrue();

        articulo.Reincorporar();
        articulo.EstaRetirada.ShouldBeFalse();
    }

    [Fact]
    public void Las_dos_transiciones_son_idempotentes()
    {
        Articulo articulo = Nuevo();

        articulo.Reincorporar();
        articulo.EstaRetirada.ShouldBeFalse();

        articulo.Retirar();
        articulo.Retirar();
        articulo.EstaRetirada.ShouldBeTrue();
    }

    [Fact]
    public void Retirar_no_toca_nada_mas_de_la_ficha()
    {
        Articulo articulo = Nuevo();
        var categoria = Guid.Parse("44444444-4444-4444-4444-444444444444");
        articulo.Modificar(
            "Tornillo de 8", TipoDeArticulo.Bien, Trazabilidad.PorLote, s_impuesto, categoria);

        articulo.Retirar();

        articulo.Codigo.ShouldBe("TORN-8");
        articulo.Descripcion.ShouldBe("Tornillo de 8");
        articulo.Tipo.ShouldBe(TipoDeArticulo.Bien);
        articulo.Trazabilidad.ShouldBe(Trazabilidad.PorLote);
        articulo.UnidadBaseId.ShouldBe(s_unidad);
        articulo.ImpuestoPorDefectoId.ShouldBe(s_impuesto);
        articulo.CategoriaId.ShouldBe(categoria);
    }

    [Fact]
    public void Un_articulo_retirado_se_sigue_pudiendo_modificar_y_sigue_retirado()
    {
        Articulo articulo = Nuevo();
        articulo.Retirar();

        articulo.Modificar(
            "Tornillo de 8, descatalogado", TipoDeArticulo.Bien, Trazabilidad.Ninguna, s_impuesto, null);

        articulo.Descripcion.ShouldBe("Tornillo de 8, descatalogado");
        articulo.EstaRetirada.ShouldBeTrue();
    }

    private static Articulo Nuevo() => Articulo.Crear(
        s_empresa,
        "torn-8",
        "Tornillo",
        TipoDeArticulo.Bien,
        Trazabilidad.Ninguna,
        s_unidad,
        s_impuesto,
        null,
        s_momento);
}
