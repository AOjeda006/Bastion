using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

internal static class Mapeos
{
    /// <summary>
    /// La ficha, con las dos cuentas del teórico de ahora y la huella si el recuento sigue en curso.
    /// </summary>
    /// <param name="recuento">El recuento, con sus líneas.</param>
    /// <param name="teorico">El teórico de todas sus líneas.</param>
    /// <returns>La ficha.</returns>
    internal static RecuentoDto ADto(this Recuento recuento, ElTeoricoDeLasLineas teorico)
    {
        bool enCurso = teorico.EsElDeAhora;

        return new RecuentoDto(
            recuento.Id,
            recuento.SerieId,
            recuento.SerieDelAjusteId,
            recuento.Numero,
            recuento.AlmacenId,
            recuento.FechaDeApertura,
            recuento.FechaDeConfirmacion,
            recuento.Estado.ToString(),
            recuento.Motivo,
            recuento.Divisa,
            recuento.MotivoDelDescarte,
            recuento.MotivoDeLaAnulacion,
            teorico.AjusteId,
            recuento.Lineas.Count,
            recuento.LineasSinContar().Count,
            enCurso ? recuento.LineasConElTeoricoCambiado(teorico.DeAhora).Count : null,
            enCurso ? recuento.Lineas.Count(linea => teorico.TransitoDe(linea) > 0m) : null,
            enCurso ? recuento.HuellaDelTeorico(teorico.DeAhora) : null);
    }

    internal static RecuentoResumenDto AResumen(this Recuento recuento) => new(
        recuento.Id,
        recuento.Numero,
        recuento.AlmacenId,
        recuento.FechaDeApertura,
        recuento.FechaDeConfirmacion,
        recuento.Estado.ToString(),
        recuento.Motivo);

    /// <summary>Una línea, con su teórico y la diferencia que movería el ajuste.</summary>
    /// <param name="linea">La línea.</param>
    /// <param name="teorico">El teórico de las líneas de su recuento.</param>
    /// <returns>La línea, como se enseña.</returns>
    internal static LineaDeRecuentoDto ADto(this LineaDeRecuento linea, ElTeoricoDeLasLineas teorico)
    {
        decimal? elTeorico = teorico.De(linea);

        return new LineaDeRecuentoDto(
            linea.Id,
            linea.Numero,
            linea.UbicacionId,
            linea.ArticuloId,
            linea.CodigoDeLote,
            linea.NumeroDeSerie,
            linea.UnidadBaseId,
            linea.Origen.ToString(),
            linea.CosteUnitario,
            linea.Contado,
            linea.TeoricoAlContar,
            elTeorico,
            teorico.TransitoDe(linea),
            linea.Contado - elTeorico,
            TieneElTeoricoCambiado(linea, teorico),
            linea.LineaDeAjusteId);
    }

    /// <summary>
    /// Si el teórico de ahora ya no es el de cuando se contó. Solo puede pasar en curso: el de un
    /// recuento cerrado no se vuelve a leer.
    /// </summary>
    /// <param name="linea">La línea.</param>
    /// <param name="teorico">El teórico de las líneas de su recuento.</param>
    /// <returns>Si ha cambiado.</returns>
    internal static bool TieneElTeoricoCambiado(LineaDeRecuento linea, ElTeoricoDeLasLineas teorico) =>
        teorico.EsElDeAhora
        && linea.TeoricoAlContar is { } alContar
        && teorico.De(linea) is { } ahora
        && alContar != ahora;
}
