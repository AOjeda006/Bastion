using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Domain.Series;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Numeracion;

/// <summary>
/// Cada documento de Inventario dice en qué series numera, y lo que dice es un tipo de serie que
/// existe.
/// </summary>
/// <remarks>
/// <para>
/// <b>El nombre va escrito a mano porque Inventario no ve el enumerado.</b>
/// <c>TipoDeDocumento</c> es de <c>Organizacion.Domain</c>, y el ADR-0013 no deja que Inventario
/// lo referencie: el numerador del módulo escribe <c>"AjusteDeInventario"</c> como cadena. Una
/// cadena no se entera de que el valor cambie de nombre, y la sentencia no fallaría —dejaría de
/// casar con todas las series y cada confirmación contestaría «esta serie es de otro documento»—.
/// Desde aquí se ven los dos lados, y por eso la comparación vive aquí.
/// </para>
/// <para>
/// <b>Lo que se afirma sobre la columna está al lado</b>: que <c>tipo_de_documento</c> guarda el
/// nombre del valor, y no su número, lo dice
/// <see cref="LaSentenciaDeNumeracionNombraLaTablaDeVerdadTests"/>. Aquí solo el mapa.
/// </para>
/// </remarks>
public sealed class LosDocumentosDeInventarioNumeranEnSusSeriesTests
{
    [Fact]
    public void Un_ajuste_numera_en_las_series_de_ajustes_de_inventario() =>
        NumeradorDeSeriesDeInventario.SeriesDe(TipoDeDocumentoOrigen.Ajuste)
            .ShouldBe(nameof(TipoDeDocumento.AjusteDeInventario));

    [Fact]
    public void Cada_documento_del_modulo_numera_en_un_tipo_de_serie_que_existe()
    {
        // Se RECORRE el enumerado, no se enumera a mano: el documento que entre en el 2.11 o el
        // 2.12 sin su línea en el mapa pone esto rojo antes de llegar a ninguna base, con la
        // excepción que el mapa lanza a propósito.
        TipoDeDocumentoOrigen[] documentos = Enum.GetValues<TipoDeDocumentoOrigen>();

        // El barrido se afirma primero (ADR-0020): un enumerado vacío dejaría el bucle sin vueltas.
        documentos.ShouldNotBeEmpty();

        List<string> sinTipo = [.. documentos
            .Where(documento => !Enum.TryParse<TipoDeDocumento>(
                NumeradorDeSeriesDeInventario.SeriesDe(documento), ignoreCase: false, out _))
            .Select(documento => documento.ToString())];

        sinTipo.ShouldBeEmpty(
            "estos documentos de inventario numeran en un tipo de serie que Organización no " +
            "tiene: " + string.Join(", ", sinTipo));
    }

    [Fact]
    public void Un_documento_que_el_mapa_no_nombra_no_numera_en_ninguna_serie_por_defecto()
    {
        // SIN RAMA POR DEFECTO, y queda afirmado: un documento nuevo que numerara en las series de
        // ajustes «porque sí» sería el agujero que el ADR-0043 cierra, abierto desde dentro.
        const TipoDeDocumentoOrigen QueNoExiste = (TipoDeDocumentoOrigen)0;

        Enum.IsDefined(QueNoExiste).ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(
            () => NumeradorDeSeriesDeInventario.SeriesDe(QueNoExiste));
    }
}
