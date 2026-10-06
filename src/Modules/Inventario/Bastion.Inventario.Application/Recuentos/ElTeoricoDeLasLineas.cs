using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>
/// El teórico de cada línea de un recuento, tal como se enseña: el de ahora mientras está en curso,
/// el que quedó al confirmarlo después, y ninguno si se descartó.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mientras está en curso, el teórico es el físico de la clave en este instante</b> (ADR-0055
/// §2), y una clave sin fila en las existencias tiene teórico cero. El tránsito se lee con él, de
/// la misma fila (§7).
/// </para>
/// <para>
/// <b>Uno confirmado no se vuelve a leer del libro</b>: lo que diga hoy ya no es lo que se contó
/// contra él. El que quedó al confirmar es lo contado menos lo que movió su línea del ajuste, que
/// va en la unidad base con factor uno, y lo contado si la línea cuadraba y no movió nada.
/// </para>
/// </remarks>
internal sealed class ElTeoricoDeLasLineas
{
    private readonly IReadOnlyDictionary<Guid, decimal> _teoricos;
    private readonly IReadOnlyDictionary<Guid, decimal>? _transitos;

    private ElTeoricoDeLasLineas(
        IReadOnlyDictionary<Guid, decimal> teoricos,
        IReadOnlyDictionary<Guid, decimal>? transitos,
        Guid? ajusteId)
    {
        _teoricos = teoricos;
        _transitos = transitos;
        AjusteId = ajusteId;
    }

    /// <summary>Si es el teórico de ahora, el de un recuento en curso.</summary>
    internal bool EsElDeAhora => _transitos is not null;

    /// <summary>El ajuste que movió la diferencia, si el recuento la movió.</summary>
    internal Guid? AjusteId { get; }

    /// <summary>
    /// El teórico de cada línea, de todas: lo que piden la huella y las cuentas del dominio. Solo
    /// existe mientras el recuento está en curso.
    /// </summary>
    internal IReadOnlyDictionary<Guid, decimal> DeAhora => EsElDeAhora
        ? _teoricos
        : throw new InvalidOperationException(
            "Solo un recuento en curso tiene teórico de ahora: el de uno cerrado es el que quedó al " +
            "confirmarlo, y no se compara con nada.");

    /// <summary>Lee el teórico que corresponde al estado del recuento.</summary>
    /// <param name="recuento">El recuento, con sus líneas.</param>
    /// <param name="recuentos">De dónde se leen las existencias y su ajuste.</param>
    /// <param name="articulos">
    /// Los artículos a los que se acota la lectura de existencias, o <see langword="null"/> para el
    /// almacén entero. Las líneas de otros artículos no tendrán teórico.
    /// </param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El teórico de cada línea.</returns>
    internal static async Task<ElTeoricoDeLasLineas> LeerAsync(
        Recuento recuento,
        IRepositorioDeRecuentos recuentos,
        IReadOnlyCollection<Guid>? articulos,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(recuento);
        ArgumentNullException.ThrowIfNull(recuentos);

        switch (recuento.Estado)
        {
            case EstadoDeRecuento.EnCurso:
                IReadOnlyList<ExistenciaDeUnaClave> existencias = await recuentos
                    .ExistenciasAsync(recuento.AlmacenId, articulos, cancelacion)
                    .ConfigureAwait(false);

                return Ahora(recuento, existencias, articulos);

            case EstadoDeRecuento.Confirmado or EstadoDeRecuento.Anulado:
                Ajuste? ajuste = recuento.MovioElLibro
                    ? await recuentos.AjusteDeAsync(recuento.Id, cancelacion).ConfigureAwait(false)
                    : null;

                return AlConfirmar(recuento, ajuste);

            case EstadoDeRecuento.Descartado:
                return new ElTeoricoDeLasLineas(new Dictionary<Guid, decimal>(), transitos: null, ajusteId: null);

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(recuento), recuento.Estado, "un estado del recuento que la ficha no sabe enseñar");
        }
    }

    /// <summary>El teórico de ahora, desde las existencias ya leídas.</summary>
    /// <param name="recuento">El recuento en curso.</param>
    /// <param name="existencias">Las existencias de su almacén.</param>
    /// <param name="articulos">
    /// Los artículos a los que se acotó la lectura, o <see langword="null"/> si se leyó entero.
    /// </param>
    /// <returns>El teórico y el tránsito de cada línea.</returns>
    internal static ElTeoricoDeLasLineas Ahora(
        Recuento recuento,
        IReadOnlyList<ExistenciaDeUnaClave> existencias,
        IReadOnlyCollection<Guid>? articulos = null)
    {
        ArgumentNullException.ThrowIfNull(recuento);
        ArgumentNullException.ThrowIfNull(existencias);

        var porClave = existencias.ToDictionary(existencia => existencia.Clave);

        Dictionary<Guid, decimal> teoricos = [];
        Dictionary<Guid, decimal> transitos = [];

        foreach (LineaDeRecuento linea in recuento.Lineas)
        {
            if (articulos is not null && !articulos.Contains(linea.ArticuloId))
            {
                continue;
            }

            ExistenciaDeUnaClave? existencia = porClave.GetValueOrDefault(linea.Clave);

            teoricos[linea.Id] = existencia?.Fisico ?? 0m;
            transitos[linea.Id] = existencia?.EnTransito ?? 0m;
        }

        return new ElTeoricoDeLasLineas(teoricos, transitos, ajusteId: null);
    }

    /// <summary>El teórico de una línea, o <see langword="null"/> si no tiene.</summary>
    /// <param name="linea">La línea.</param>
    /// <returns>El teórico.</returns>
    internal decimal? De(LineaDeRecuento linea)
    {
        ArgumentNullException.ThrowIfNull(linea);

        return _teoricos.TryGetValue(linea.Id, out decimal teorico) ? teorico : null;
    }

    /// <summary>Lo que vuela hacia la clave de una línea, o <see langword="null"/> si ya no está en curso.</summary>
    /// <param name="linea">La línea.</param>
    /// <returns>El tránsito.</returns>
    internal decimal? TransitoDe(LineaDeRecuento linea)
    {
        ArgumentNullException.ThrowIfNull(linea);

        return _transitos is not null && _transitos.TryGetValue(linea.Id, out decimal transito) ? transito : null;
    }

    private static ElTeoricoDeLasLineas AlConfirmar(Recuento recuento, Ajuste? ajuste)
    {
        Dictionary<Guid, decimal> movido = ajuste?.Lineas.ToDictionary(
            linea => linea.Id, linea => linea.CantidadIntroducida) ?? [];

        Dictionary<Guid, decimal> teoricos = [];

        foreach (LineaDeRecuento linea in recuento.Lineas)
        {
            // Uno confirmado tiene todas sus líneas contadas: no se confirma con una sin contar.
            decimal contado = linea.Contado ?? throw new InvalidOperationException(
                $"La línea {linea.Numero} del recuento {recuento.Id} está sin contar y el recuento no " +
                "está en curso: un recuento confirmado las tiene todas contadas.");

            teoricos[linea.Id] = linea.LineaDeAjusteId is { } lineaDeAjusteId
                ? contado - movido[lineaDeAjusteId]
                : contado;
        }

        return new ElTeoricoDeLasLineas(teoricos, transitos: null, ajuste?.Id);
    }
}
