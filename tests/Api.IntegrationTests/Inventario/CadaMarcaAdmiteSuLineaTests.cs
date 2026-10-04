using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Application.Transferencias;
using Bastion.Inventario.Application.Trazabilidad;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Qué línea admite cada marca de trazabilidad, entera: las tres marcas por las cuatro formas de
/// una línea (ADR-0048 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la regla que se aplica dos veces</b>: al abrir, como cortesía, y al confirmar, con la
/// marca leída bajo cerrojo. Hasta el ítem 2.9 solo tenía caso una de sus seis ramas de rechazo, la
/// del artículo por lote sin lote, y por el camino de la carrera: la mutación 90, que dejaba pasar
/// un lote en un artículo sin marca, salió verde en los dos carriles.
/// </para>
/// <para>
/// <b>Y desde el 2.11, en dos documentos</b>: el ajuste y la transferencia la comparten, y cada uno
/// convierte el rechazo en su error, con su prefijo. Cada fila se mira con los dos.
/// </para>
/// <para>
/// <b>En el carril rápido y en este ensamblado</b>, que es el que ve lo interno de la aplicación de
/// Inventario: es una función pura, y no necesita ni base ni contenedor.
/// </para>
/// <para>
/// <b>Lote y serie a la vez también está</b>, porque la forma lo deja pasar y lo rechaza la marca,
/// que es de una sola cosa. Lo que sobra se nombra antes que lo que falta.
/// </para>
/// </remarks>
public sealed class CadaMarcaAdmiteSuLineaTests
{
    private const string Lote = "L-01";
    private const string Serie = "SN-01";

    /// <summary>La marca, el lote y la serie de la línea, y lo que le falta o le sobra.</summary>
    public static TheoryData<MarcaDeTrazabilidad, string?, string?, string?> Casos { get; } = new()
    {
        { MarcaDeTrazabilidad.Ninguna, null, null, null },
        { MarcaDeTrazabilidad.Ninguna, Lote, null, "le sobra el lote." },
        { MarcaDeTrazabilidad.Ninguna, null, Serie, "le sobra el número de serie." },
        { MarcaDeTrazabilidad.Ninguna, Lote, Serie, "le sobra el lote." },
        { MarcaDeTrazabilidad.PorLote, null, null, "le falta el lote." },
        { MarcaDeTrazabilidad.PorLote, Lote, null, null },
        { MarcaDeTrazabilidad.PorLote, null, Serie, "le sobra el número de serie." },
        { MarcaDeTrazabilidad.PorLote, Lote, Serie, "le sobra el número de serie." },
        { MarcaDeTrazabilidad.PorNumeroSerie, null, null, "le falta el número de serie." },
        { MarcaDeTrazabilidad.PorNumeroSerie, Lote, null, "le sobra el lote." },
        { MarcaDeTrazabilidad.PorNumeroSerie, null, Serie, null },
        { MarcaDeTrazabilidad.PorNumeroSerie, Lote, Serie, "le sobra el lote." },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void Cada_marca_admite_su_linea_y_nombra_lo_que_le_falta_o_le_sobra_a_las_demas(
        MarcaDeTrazabilidad marca, string? lote, string? serie, string? queNoCasa)
    {
        var articuloId = Guid.NewGuid();

        LineaQueNoCasa? noCasa = LaTrazabilidadDeLasLineas.LoQueNoCasa(
            [new LineaConCodigos(3, articuloId, lote, serie)],
            new Dictionary<Guid, MarcaDeTrazabilidad> { [articuloId] = marca });

        if (queNoCasa is null)
        {
            noCasa.ShouldBeNull($"«{marca}» admite la línea con lote «{lote}» y serie «{serie}»");

            return;
        }

        LineaQueNoCasa rechazada = noCasa.ShouldNotBeNull(
            $"«{marca}» no admite la línea con lote «{lote}» y serie «{serie}»");

        rechazada.ArticuloId.ShouldBe(articuloId);
        rechazada.Marca.ShouldBe(marca);

        ErrorDeOperacion[] rechazos =
        [
            ErroresDeAjuste.TrazabilidadNoCasa(rechazada),
            ErroresDeTransferencia.TrazabilidadNoCasa(rechazada),
        ];

        rechazos.Select(rechazo => rechazo.Codigo)
            .ShouldBe(["ajuste-trazabilidad-no-casa", "transferencia-trazabilidad-no-casa"]);

        foreach (ErrorDeOperacion rechazo in rechazos)
        {
            rechazo.Tipo.ShouldBe(TipoDeError.Conflicto, "el cuerpo puede estar bien escrito: lo que falla es la ficha");
            rechazo.Mensaje.ShouldContain("La línea 3 ", Case.Sensitive, "nombra la línea por su número");
            rechazo.Mensaje.ShouldContain(queNoCasa);
        }
    }

    [Fact]
    public void La_tabla_cubre_cada_marca_con_las_cuatro_formas_de_una_linea()
    {
        var filas = Casos.Select(fila => (
            Marca: (MarcaDeTrazabilidad)fila[0],
            Forma: (fila[1] is not null, fila[2] is not null))).ToList();

        filas.Select(fila => fila.Marca).Distinct().Order().ShouldBe(Enum.GetValues<MarcaDeTrazabilidad>().Order());

        foreach (IGrouping<MarcaDeTrazabilidad, (MarcaDeTrazabilidad Marca, (bool, bool) Forma)> deUnaMarca
            in filas.GroupBy(fila => fila.Marca))
        {
            deUnaMarca.Select(fila => fila.Forma).Distinct().Count().ShouldBe(
                4, $"«{deUnaMarca.Key}»: sin lote ni serie, con lote, con serie y con los dos");
        }

        filas.Distinct().Count().ShouldBe(filas.Count, "cada combinación, una sola vez");
    }
}
