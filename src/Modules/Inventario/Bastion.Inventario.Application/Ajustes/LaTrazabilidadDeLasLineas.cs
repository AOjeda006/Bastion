using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Una línea, con lo que hace falta para compararla con la marca de su artículo.</summary>
/// <param name="Numero">Su posición en el documento, desde uno.</param>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="CodigoDeLote">El lote, si lo lleva.</param>
/// <param name="NumeroDeSerie">El número de serie, si lo lleva.</param>
internal readonly record struct LineaConCodigos(
    int Numero, Guid ArticuloId, string? CodigoDeLote, string? NumeroDeSerie);

/// <summary>
/// Si las líneas casan con la marca de sus artículos, y si sus códigos tienen la forma que piden
/// (ADR-0048 §2, §3 y §4).
/// </summary>
internal static class LaTrazabilidadDeLasLineas
{
    /// <summary>Las líneas de un borrador.</summary>
    /// <param name="ajuste">El documento.</param>
    /// <returns>Cada línea con sus códigos, en su orden.</returns>
    internal static IReadOnlyList<LineaConCodigos> DelDocumento(Ajuste ajuste) =>
    [
        .. ajuste.Lineas.Select(linea => new LineaConCodigos(
            linea.Numero, linea.ArticuloId, linea.CodigoDeLote, linea.NumeroDeSerie)),
    ];

    /// <summary>Las líneas de una petición de alta, numeradas desde uno.</summary>
    /// <param name="lineas">Lo que llegó.</param>
    /// <returns>Cada línea con sus códigos, en su orden.</returns>
    internal static IReadOnlyList<LineaConCodigos> DeLaPeticion(IReadOnlyList<LineaDeAjusteDto> lineas) =>
    [
        .. lineas.Select((linea, indice) => new LineaConCodigos(
            indice + 1, linea.ArticuloId, linea.CodigoDeLote, linea.NumeroDeSerie)),
    ];

    /// <summary>
    /// La forma de los códigos de una petición de alta: lo que se puede decir sin preguntar a nadie.
    /// </summary>
    /// <remarks>
    /// <b>Son las mismas reglas que lanza el dominio</b>, dichas antes con su <c>type</c>: ahí son
    /// invariantes, y aquí, un cuerpo mal escrito que el borde contesta con un <c>400</c>
    /// (ADR-0004). Lote y serie a la vez no está aquí: lo rechaza la marca, que es de una sola cosa.
    /// </remarks>
    /// <param name="lineas">Lo que llegó.</param>
    /// <returns>El primer error, por orden de línea, o <see langword="null"/>.</returns>
    internal static ErrorDeOperacion? LoQueNoTieneForma(IReadOnlyList<LineaDeAjusteDto> lineas)
    {
        HashSet<CodigoDeUnArticulo> series = [];

        for (int indice = 0; indice < lineas.Count; indice++)
        {
            LineaDeAjusteDto linea = lineas[indice];
            int numero = indice + 1;

            if (linea.CodigoDeLote is not null && CodigoGs1.Normalizar(linea.CodigoDeLote) is null)
            {
                return ErroresDeAjuste.LoteNoValido(numero);
            }

            if (linea.NumeroDeSerie is null)
            {
                continue;
            }

            if (CodigoGs1.Normalizar(linea.NumeroDeSerie) is not { } serie)
            {
                return ErroresDeAjuste.NumeroDeSerieNoValido(numero);
            }

            if (Math.Abs(MovimientoStock.EnUnidadBase(linea.CantidadIntroducida, linea.FactorAUnidadBase)) != 1m)
            {
                return ErroresDeAjuste.SerieNoUnitaria(numero);
            }

            if (!series.Add(new CodigoDeUnArticulo(linea.ArticuloId, serie)))
            {
                return ErroresDeAjuste.SerieRepetida(numero, serie);
            }
        }

        return null;
    }

    /// <summary>La primera línea que no casa con la marca de su artículo, o nada.</summary>
    /// <param name="lineas">Las líneas, en su orden.</param>
    /// <param name="marcas">La marca de cada artículo, leída por el puerto.</param>
    /// <returns>El <c>409</c> que lo dice, o <see langword="null"/> si todas casan.</returns>
    /// <exception cref="InvalidOperationException">
    /// Falta la marca de un artículo: los artículos no se borran, y el alta ya comprobó que existía.
    /// </exception>
    internal static ErrorDeOperacion? LoQueNoCasa(
        IEnumerable<LineaConCodigos> lineas, IReadOnlyDictionary<Guid, MarcaDeTrazabilidad> marcas)
    {
        foreach (LineaConCodigos linea in lineas)
        {
            if (!marcas.TryGetValue(linea.ArticuloId, out MarcaDeTrazabilidad marca))
            {
                throw new InvalidOperationException(
                    $"El artículo {linea.ArticuloId} de la línea {linea.Numero} no tiene marca: el alta " +
                    "comprobó que existía y un artículo no se borra, así que o el puerto no lo leyó o " +
                    "no es de esta empresa.");
            }

            bool lote = linea.CodigoDeLote is not null;
            bool serie = linea.NumeroDeSerie is not null;

            DiscrepanciaDeTrazabilidad? discrepancia = marca switch
            {
                MarcaDeTrazabilidad.Ninguna when lote => DiscrepanciaDeTrazabilidad.SobraElLote,
                MarcaDeTrazabilidad.Ninguna when serie => DiscrepanciaDeTrazabilidad.SobraElNumeroDeSerie,
                MarcaDeTrazabilidad.Ninguna => null,
                MarcaDeTrazabilidad.PorLote when serie => DiscrepanciaDeTrazabilidad.SobraElNumeroDeSerie,
                MarcaDeTrazabilidad.PorLote when !lote => DiscrepanciaDeTrazabilidad.FaltaElLote,
                MarcaDeTrazabilidad.PorLote => null,
                MarcaDeTrazabilidad.PorNumeroSerie when lote => DiscrepanciaDeTrazabilidad.SobraElLote,
                MarcaDeTrazabilidad.PorNumeroSerie when !serie => DiscrepanciaDeTrazabilidad.FaltaElNumeroDeSerie,
                MarcaDeTrazabilidad.PorNumeroSerie => null,
                _ => throw new InvalidOperationException(
                    $"El artículo {linea.ArticuloId} llega con la marca «{marca}», que no es ninguna " +
                    "de las tres. Un valor nuevo es una decisión sobre qué líneas admite, y se escribe " +
                    "aquí."),
            };

            if (discrepancia is { } queNoCasa)
            {
                return ErroresDeAjuste.TrazabilidadNoCasa(linea.Numero, linea.ArticuloId, marca, queNoCasa);
            }
        }

        return null;
    }
}

/// <summary>Qué le falta o le sobra a una línea para casar con la marca de su artículo.</summary>
internal enum DiscrepanciaDeTrazabilidad
{
    /// <summary>El artículo va por lote y la línea no lo dice.</summary>
    FaltaElLote = 1,

    /// <summary>El artículo va por número de serie y la línea no lo dice.</summary>
    FaltaElNumeroDeSerie = 2,

    /// <summary>La línea lleva lote y el artículo no va por lote.</summary>
    SobraElLote = 3,

    /// <summary>La línea lleva número de serie y el artículo no va por serie.</summary>
    SobraElNumeroDeSerie = 4,
}
