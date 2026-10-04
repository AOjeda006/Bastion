using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;

namespace Bastion.Inventario.Application.Trazabilidad;

/// <summary>Una línea, con lo que hace falta para compararla con la marca de su artículo.</summary>
/// <param name="Numero">Su posición en el documento, desde uno.</param>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="CodigoDeLote">El lote, si lo lleva.</param>
/// <param name="NumeroDeSerie">El número de serie, si lo lleva.</param>
internal readonly record struct LineaConCodigos(
    int Numero, Guid ArticuloId, string? CodigoDeLote, string? NumeroDeSerie);

/// <summary>Una línea de una petición de alta, con lo que hace falta para mirar su forma.</summary>
/// <param name="Numero">Su posición en la petición, desde uno.</param>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="CantidadIntroducida">La cantidad, tal como se escribió.</param>
/// <param name="FactorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
/// <param name="CodigoDeLote">El lote, si lo lleva.</param>
/// <param name="NumeroDeSerie">El número de serie, si lo lleva.</param>
internal readonly record struct LineaConForma(
    int Numero,
    Guid ArticuloId,
    decimal CantidadIntroducida,
    decimal FactorAUnidadBase,
    string? CodigoDeLote,
    string? NumeroDeSerie);

/// <summary>La primera línea cuyos códigos no tienen la forma que piden, y qué le pasa.</summary>
/// <param name="Linea">La línea, desde uno.</param>
/// <param name="Falta">Qué forma no tiene.</param>
/// <param name="Serie">El número de serie ya normalizado, cuando lo que pasa es que se repite.</param>
internal readonly record struct LineaSinForma(int Linea, FaltaDeForma Falta, string? Serie);

/// <summary>La primera línea que no casa con la marca de su artículo, y qué le falta o le sobra.</summary>
/// <param name="Linea">La línea, desde uno.</param>
/// <param name="ArticuloId">Su artículo.</param>
/// <param name="Marca">La marca del artículo.</param>
/// <param name="Discrepancia">Qué le falta o le sobra.</param>
internal readonly record struct LineaQueNoCasa(
    int Linea, Guid ArticuloId, MarcaDeTrazabilidad Marca, DiscrepanciaDeTrazabilidad Discrepancia);

/// <summary>
/// Si las líneas casan con la marca de sus artículos, y si sus códigos tienen la forma que piden
/// (ADR-0048 §2, §3 y §4).
/// </summary>
/// <remarks>
/// <b>La regla es una y los documentos son dos</b>, el ajuste y la transferencia. Aquí se dice qué
/// línea falla y por qué, y cada documento lo convierte en su error, con su prefijo: el catálogo de
/// <c>type</c> solo lee códigos escritos en el mismo fichero que su error (ADR-0030), así que el
/// error no puede nacer aquí.
/// </remarks>
internal static class LaTrazabilidadDeLasLineas
{
    /// <summary>Las líneas de un ajuste.</summary>
    /// <param name="ajuste">El documento.</param>
    /// <returns>Cada línea con sus códigos, en su orden.</returns>
    internal static IReadOnlyList<LineaConCodigos> DelDocumento(Ajuste ajuste) =>
    [
        .. ajuste.Lineas.Select(linea => new LineaConCodigos(
            linea.Numero, linea.ArticuloId, linea.CodigoDeLote, linea.NumeroDeSerie)),
    ];

    /// <summary>Las líneas de una transferencia.</summary>
    /// <param name="transferencia">El documento.</param>
    /// <returns>Cada línea con sus códigos, en su orden.</returns>
    internal static IReadOnlyList<LineaConCodigos> DelDocumento(Transferencia transferencia) =>
    [
        .. transferencia.Lineas.Select(linea => new LineaConCodigos(
            linea.Numero, linea.ArticuloId, linea.CodigoDeLote, linea.NumeroDeSerie)),
    ];

    /// <summary>Las líneas de una petición de alta de ajuste, numeradas desde uno.</summary>
    /// <param name="lineas">Lo que llegó.</param>
    /// <returns>Cada línea con su forma, en su orden.</returns>
    internal static IReadOnlyList<LineaConForma> DeLaPeticion(IReadOnlyList<LineaDeAjusteDto> lineas) =>
    [
        .. lineas.Select((linea, indice) => new LineaConForma(
            indice + 1,
            linea.ArticuloId,
            linea.CantidadIntroducida,
            linea.FactorAUnidadBase,
            linea.CodigoDeLote,
            linea.NumeroDeSerie)),
    ];

    /// <summary>Las líneas de una petición de alta de transferencia, numeradas desde uno.</summary>
    /// <param name="lineas">Lo que llegó.</param>
    /// <returns>Cada línea con su forma, en su orden.</returns>
    internal static IReadOnlyList<LineaConForma> DeLaPeticion(IReadOnlyList<LineaDeTransferenciaDto> lineas) =>
    [
        .. lineas.Select((linea, indice) => new LineaConForma(
            indice + 1,
            linea.ArticuloId,
            linea.CantidadIntroducida,
            linea.FactorAUnidadBase,
            linea.CodigoDeLote,
            linea.NumeroDeSerie)),
    ];

    /// <summary>Lo que se compara con la marca, de las líneas de una petición.</summary>
    /// <param name="lineas">Las líneas, ya con su forma.</param>
    /// <returns>Cada línea con sus códigos, en su orden.</returns>
    internal static IEnumerable<LineaConCodigos> SusCodigos(IEnumerable<LineaConForma> lineas) =>
        lineas.Select(linea => new LineaConCodigos(
            linea.Numero, linea.ArticuloId, linea.CodigoDeLote, linea.NumeroDeSerie));

    /// <summary>
    /// La forma de los códigos de una petición de alta: lo que se puede decir sin preguntar a nadie.
    /// </summary>
    /// <remarks>
    /// <b>Son las mismas reglas que lanza el dominio</b>, dichas antes con su <c>type</c>: ahí son
    /// invariantes, y aquí, un cuerpo mal escrito que el borde contesta con un <c>400</c>
    /// (ADR-0004). Lote y serie a la vez no está aquí: lo rechaza la marca, que es de una sola cosa.
    /// </remarks>
    /// <param name="lineas">Lo que llegó, en su orden.</param>
    /// <returns>La primera línea que falla, por orden, o <see langword="null"/>.</returns>
    internal static LineaSinForma? LoQueNoTieneForma(IEnumerable<LineaConForma> lineas)
    {
        HashSet<CodigoDeUnArticulo> series = [];

        foreach (LineaConForma linea in lineas)
        {
            if (linea.CodigoDeLote is not null && CodigoGs1.Normalizar(linea.CodigoDeLote) is null)
            {
                return new LineaSinForma(linea.Numero, FaltaDeForma.LoteNoValido, null);
            }

            if (linea.NumeroDeSerie is null)
            {
                continue;
            }

            if (CodigoGs1.Normalizar(linea.NumeroDeSerie) is not { } serie)
            {
                return new LineaSinForma(linea.Numero, FaltaDeForma.NumeroDeSerieNoValido, null);
            }

            if (Math.Abs(MovimientoStock.EnUnidadBase(linea.CantidadIntroducida, linea.FactorAUnidadBase)) != 1m)
            {
                return new LineaSinForma(linea.Numero, FaltaDeForma.SerieNoUnitaria, null);
            }

            if (!series.Add(new CodigoDeUnArticulo(linea.ArticuloId, serie)))
            {
                return new LineaSinForma(linea.Numero, FaltaDeForma.SerieRepetida, serie);
            }
        }

        return null;
    }

    /// <summary>La primera línea que no casa con la marca de su artículo, o nada.</summary>
    /// <param name="lineas">Las líneas, en su orden.</param>
    /// <param name="marcas">La marca de cada artículo, leída por el puerto.</param>
    /// <returns>La línea y lo que le falta o le sobra, o <see langword="null"/> si todas casan.</returns>
    /// <exception cref="InvalidOperationException">
    /// Falta la marca de un artículo: los artículos no se borran, y el alta ya comprobó que existía.
    /// </exception>
    internal static LineaQueNoCasa? LoQueNoCasa(
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
                return new LineaQueNoCasa(linea.Numero, linea.ArticuloId, marca, queNoCasa);
            }
        }

        return null;
    }

    /// <summary>Lo que le falta o le sobra a la línea, dicho para el mensaje del error.</summary>
    /// <param name="discrepancia">Qué le falta o le sobra.</param>
    /// <returns>La frase, con su punto.</returns>
    internal static string EnPalabras(DiscrepanciaDeTrazabilidad discrepancia) => discrepancia switch
    {
        DiscrepanciaDeTrazabilidad.FaltaElLote => "le falta el lote.",
        DiscrepanciaDeTrazabilidad.FaltaElNumeroDeSerie => "le falta el número de serie.",
        DiscrepanciaDeTrazabilidad.SobraElLote => "le sobra el lote.",
        DiscrepanciaDeTrazabilidad.SobraElNumeroDeSerie => "le sobra el número de serie.",
        _ => throw new ArgumentOutOfRangeException(nameof(discrepancia), discrepancia, null),
    };
}

/// <summary>Qué forma no tienen los códigos de una línea.</summary>
internal enum FaltaDeForma
{
    /// <summary>El lote no es un código GS1.</summary>
    LoteNoValido = 1,

    /// <summary>El número de serie no es un código GS1.</summary>
    NumeroDeSerieNoValido = 2,

    /// <summary>La línea lleva número de serie y no mueve una unidad base.</summary>
    SerieNoUnitaria = 3,

    /// <summary>Otra línea del mismo artículo ya nombra ese número de serie.</summary>
    SerieRepetida = 4,
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
