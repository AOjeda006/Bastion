using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Series;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Codigos;

/// <summary>
/// Series y almacenes normalizan su código al crearse, y sobre el código normalizado hay un índice
/// único. Quien comprueba «¿existe ya?» ANTES de insertar —la capa de aplicación— tiene que
/// preguntar por la misma forma que se va a guardar; si preguntara por lo que escribió el usuario,
/// «fac» pasaría el filtro, chocaría contra el índice y saldría como un 500 en vez de como un 409
/// con explicación.
/// </summary>
/// <remarks>
/// De ahí que la normalización sea pública: no es un detalle interno, es una regla que otra capa
/// necesita aplicar para preguntar bien. Lo que sigue siendo interno es la validación completa
/// —longitud incluida—, que solo tiene sentido al construir la entidad.
/// </remarks>
public sealed class NormalizacionDeCodigosTests
{
    private static readonly Guid s_empresa = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000001");
    private static readonly Guid s_ejercicio = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000002");

    [Theory]
    [InlineData("fac", "FAC")]
    [InlineData("  fac  ", "FAC")]
    [InlineData("FAC", "FAC")]
    [InlineData("Fac2026", "FAC2026")]
    public void El_codigo_de_serie_se_normaliza_igual_se_pregunte_o_se_cree(string escrito, string esperado)
    {
        Serie.NormalizarCodigo(escrito).ShouldBe(esperado);

        var creada = Serie.Crear(
            s_empresa, s_ejercicio, TipoDeDocumento.FacturaEmitida, escrito, "{serie}-{numero:0000}");

        // Las dos puertas dan lo mismo: es lo único que hace que la comprobación previa sirva.
        creada.Codigo.ShouldBe(esperado);
        creada.Codigo.ShouldBe(Serie.NormalizarCodigo(escrito));
    }

    [Theory]
    [InlineData("central", "CENTRAL")]
    [InlineData("  central  ", "CENTRAL")]
    [InlineData("CENTRAL", "CENTRAL")]
    public void El_codigo_de_almacen_se_normaliza_igual_se_pregunte_o_se_cree(string escrito, string esperado)
    {
        Almacen.NormalizarCodigo(escrito).ShouldBe(esperado);

        var creado = Almacen.Crear(
            s_empresa,
            escrito,
            "Almacén central",
            Direccion.De("Gran Vía", "31", "28013", "Madrid", "Madrid", "ES"),
            TipoDeAlmacen.Fisico);

        creado.Codigo.ShouldBe(esperado);
        creado.Codigo.ShouldBe(Almacen.NormalizarCodigo(escrito));
    }

    [Fact]
    public void Normalizar_no_valida_la_longitud_porque_no_es_su_trabajo()
    {
        // Preguntar «¿existe ya este código?» con uno demasiado largo tiene respuesta —no existe—
        // y no debería reventar. Quien rechaza el código largo es la creación de la entidad, que
        // es donde el límite significa algo.
        string largo = new('A', Serie.LongitudMaximaDeCodigo + 5);

        Should.NotThrow(() => Serie.NormalizarCodigo(largo)).ShouldBe(largo);

        Should.Throw<ArgumentException>(() => Serie.Crear(
            s_empresa, s_ejercicio, TipoDeDocumento.FacturaEmitida, largo, "{numero}"));
    }
}
