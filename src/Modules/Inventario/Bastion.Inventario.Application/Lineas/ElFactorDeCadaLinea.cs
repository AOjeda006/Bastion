using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Unidades;

namespace Bastion.Inventario.Application.Lineas;

/// <summary>Lo que una línea dice para resolver su factor: su artículo y la unidad en que se escribió.</summary>
/// <param name="ArticuloId">El artículo, cuya unidad base es el destino del factor.</param>
/// <param name="UnidadIntroducidaId">La unidad en que se escribió la cantidad.</param>
internal readonly record struct ArticuloYUnidad(Guid ArticuloId, Guid UnidadIntroducidaId);

/// <summary>El factor de cada línea a la unidad base de su artículo (ADR-0065 §2, ADR-0068 §2).</summary>
/// <remarks>
/// <para>
/// <b>Dos consultas como mucho, sea cual sea el número de líneas</b>: las unidades base de todos
/// los artículos, y las conversiones de todos los pares distintos. Una línea en la unidad base no
/// pregunta nada, porque una conversión de una unidad a sí misma no puede existir.
/// </para>
/// <para>
/// <b>Contesta la primera línea que falla, por su orden</b>, le falte la conversión o la tenga
/// retirada: el número que sale es el de la línea que hay que corregir primero.
/// </para>
/// <para>
/// <b>Es de los dos documentos que escriben en el libro desde una pantalla</b>, el ajuste y la
/// transferencia. Cada uno pone sus dos errores, porque su <c>type</c> dice qué documento falló, y
/// el código tiene que ir literal en la llamada que lo fabrica (ADR-0030).
/// </para>
/// </remarks>
internal static class ElFactorDeCadaLinea
{
    /// <summary>Resuelve el factor de cada línea, en su orden.</summary>
    /// <param name="articulos">Puerto de Catálogo, para la unidad base de cada artículo.</param>
    /// <param name="conversiones">Puerto de Organización, para el par exacto.</param>
    /// <param name="lineas">Las líneas de la petición, con sus maestros ya comprobados.</param>
    /// <param name="sinConversion">El error del documento para un par que no está.</param>
    /// <param name="conversionRetirada">El error del documento para un par retirado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El factor de cada línea, en su orden, o el motivo por el que una no lo tiene.</returns>
    /// <exception cref="InvalidOperationException">
    /// Falta la unidad base de un artículo: los artículos no se borran, y el alta ya comprobó que
    /// existía.
    /// </exception>
    internal static async Task<Resultado<IReadOnlyList<decimal>>> ResolverAsync(
        IConsultaDeArticulos articulos,
        IConsultaDeConversiones conversiones,
        IReadOnlyList<ArticuloYUnidad> lineas,
        Func<int, ParDeUnidades, ErrorDeOperacion> sinConversion,
        Func<int, ParDeUnidades, ErrorDeOperacion> conversionRetirada,
        CancellationToken cancelacion)
    {
        IReadOnlyDictionary<Guid, Guid> unidadesBase = await articulos
            .UnidadesBaseDeAsync([.. lineas.Select(linea => linea.ArticuloId).Distinct()], cancelacion)
            .ConfigureAwait(false);

        ParDeUnidades[] pares =
        [
            .. lineas.Select((linea, indice) => new ParDeUnidades(
                linea.UnidadIntroducidaId,
                unidadesBase.TryGetValue(linea.ArticuloId, out Guid unidadBaseId)
                    ? unidadBaseId
                    : throw new InvalidOperationException(
                        $"El artículo {linea.ArticuloId} de la línea {indice + 1} no tiene unidad base: el " +
                        "alta comprobó que existía y un artículo no se borra, así que o el puerto no lo " +
                        "leyó o no es de esta empresa.")))
        ];

        ParDeUnidades[] porPreguntar = [.. pares.Where(par => par.OrigenId != par.DestinoId).Distinct()];

        IReadOnlyDictionary<ParDeUnidades, ConversionResuelta> resueltas = porPreguntar.Length == 0
            ? new Dictionary<ParDeUnidades, ConversionResuelta>()
            : await conversiones.DeLosParesAsync(porPreguntar, cancelacion).ConfigureAwait(false);

        decimal[] factores = new decimal[pares.Length];

        for (int indice = 0; indice < pares.Length; indice++)
        {
            ParDeUnidades par = pares[indice];

            if (par.OrigenId == par.DestinoId)
            {
                factores[indice] = 1m;
            }
            else if (!resueltas.TryGetValue(par, out ConversionResuelta? conversion))
            {
                return Resultado.Fallo<IReadOnlyList<decimal>>(sinConversion(indice + 1, par));
            }
            else if (conversion.EstaRetirada)
            {
                return Resultado.Fallo<IReadOnlyList<decimal>>(conversionRetirada(indice + 1, par));
            }
            else
            {
                factores[indice] = conversion.Factor;
            }
        }

        return Resultado.Correcto<IReadOnlyList<decimal>>(factores);
    }

    /// <summary>La primera línea que no cumple la regla, por su orden.</summary>
    /// <typeparam name="TLinea">La línea de la petición de cada documento.</typeparam>
    /// <param name="lineas">Las líneas de la petición.</param>
    /// <param name="cabe">La regla, con la línea y su índice desde cero.</param>
    /// <returns>Su número, desde uno, o <c>null</c> si todas la cumplen.</returns>
    internal static int? LaPrimeraQueNoCabe<TLinea>(IReadOnlyList<TLinea> lineas, Func<TLinea, int, bool> cabe)
    {
        for (int indice = 0; indice < lineas.Count; indice++)
        {
            if (!cabe(lineas[indice], indice))
            {
                return indice + 1;
            }
        }

        return null;
    }
}
