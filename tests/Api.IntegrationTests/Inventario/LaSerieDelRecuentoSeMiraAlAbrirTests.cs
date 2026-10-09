using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Application;
using Bastion.Inventario.Application.Recuentos;
using Bastion.Organizacion.Contracts.Series;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Qué serie admite el alta del recuento por lo que numera: de qué documentos es y si su ejercicio
/// ha terminado (ADR-0055 §1.3, desde el ítem 2.13).
/// </summary>
/// <remarks>
/// <para>
/// <b>La frontera es aritmética, y baja al carril rápido.</b> Que el ejercicio acabe hoy vale y que
/// acabara ayer no; y que empiece mañana también vale, porque el recuento se numera con la fecha de
/// su confirmación, que todavía no se sabe. Por la API, con el reloj de verdad, ninguna de las tres
/// cosas se puede fijar: el alta las ve con la fecha del día en que corre la CI.
/// </para>
/// <para>
/// <b>En este ensamblado</b>, que es el que ve lo interno de la aplicación de Inventario: es una
/// función pura, y no necesita ni base ni contenedor. Que el alta la llame lo prueba
/// <c>ElAltaDelRecuentoTests</c>, con las series de verdad.
/// </para>
/// </remarks>
public sealed class LaSerieDelRecuentoSeMiraAlAbrirTests
{
    private const string DeOtroDocumento = "recuento-serie-de-otro-documento";
    private const string DeUnEjercicioTerminado = "recuento-serie-de-un-ejercicio-terminado";

    private static readonly DateOnly s_hoy = new(2026, 10, 9);
    private static readonly Guid s_serieId = Guid.CreateVersion7();

    /// <summary>
    /// De qué documentos es la serie, su ejercicio en días desde hoy, y lo que contesta el alta.
    /// </summary>
    public static TheoryData<DocumentoQueNumera, int, int, string?> Casos { get; } = new()
    {
        { DocumentoQueNumera.Recuento, -100, 100, null },
        { DocumentoQueNumera.Recuento, -364, 0, null },
        { DocumentoQueNumera.Recuento, -365, -1, DeUnEjercicioTerminado },
        { DocumentoQueNumera.Recuento, 1, 365, null },
        { DocumentoQueNumera.Ajuste, -100, 100, DeOtroDocumento },
        { DocumentoQueNumera.Transferencia, -100, 100, DeOtroDocumento },
        { DocumentoQueNumera.Ajuste, -365, -1, DeOtroDocumento },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void La_serie_del_recuento_se_admite_por_su_documento_y_por_el_fin_de_su_ejercicio(
        DocumentoQueNumera deLaSerie, int desde, int hasta, string? codigo)
    {
        LoQueNumeraUnaSerie loQueNumera = new(
            SeriesDeInventario.De(deLaSerie), s_hoy.AddDays(desde), s_hoy.AddDays(hasta));

        Resultado resultado = LosMaestrosDelRecuento.LoQueNumeraLaSerie(
            loQueNumera, s_serieId, "del recuento", SeriesDeInventario.De(DocumentoQueNumera.Recuento), s_hoy);

        string fila = $"una serie de {deLaSerie} con su ejercicio del día {desde} al {hasta}";

        if (codigo is null)
        {
            resultado.EsCorrecto.ShouldBeTrue($"{fila}: {resultado.Error?.Mensaje}");

            return;
        }

        ErrorDeOperacion error = resultado.Error.ShouldNotBeNull(fila);

        error.Codigo.ShouldBe(codigo, fila);
        error.Tipo.ShouldBe(
            TipoDeError.Conflicto,
            "la misma clase que la sentencia que numera da al confirmar: la cortesía contesta lo " +
            "que contestaría la regla, solo que antes");
        error.Mensaje.ShouldContain("La serie del recuento, ", Case.Sensitive, "dice cuál de las dos es");
    }

    [Fact]
    public void Una_serie_que_dejo_de_verse_contesta_lo_que_una_que_no_existe()
    {
        Resultado resultado = LosMaestrosDelRecuento.LoQueNumeraLaSerie(
            null, s_serieId, "del ajuste", SeriesDeInventario.De(DocumentoQueNumera.Ajuste), s_hoy);

        resultado.Error.ShouldNotBeNull().Codigo.ShouldBe("recuento-serie-no-encontrada");
    }
}
