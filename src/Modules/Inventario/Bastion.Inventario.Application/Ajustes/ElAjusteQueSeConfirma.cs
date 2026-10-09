using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>
/// El núcleo de la confirmación de un ajuste, que comparten sus tres caminos: el público,
/// <see cref="ConfirmarAjuste"/>; el inverso de una anulación, <see cref="ElInversoQueCompensa"/>; y
/// el ajuste de la diferencia de un recuento.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es lo que tiene que ser igual en los tres</b>: la valoración, los lotes y las series, el evento
/// y el documento confirmado. Un ajuste es el mismo documento venga de donde venga, y el libro no
/// admite corregir después lo que un camino escribió distinto (R2). El evento, sobre todo, es el que
/// llega al asiento, y el de un recuento es el de cualquier otro ajuste (ADR-0055 §1).
/// </para>
/// <para>
/// <b>Ni numera ni bloquea ni escribe el libro</b>, porque eso cada camino lo hace en su orden. El
/// número va antes que las valoraciones (ADR-0046 §2), y la fecha con la que se pide no es la misma
/// en el inverso. Las valoraciones que se bloquean no son las mismas en el recuento, que bloquea las
/// de todas sus claves. Y entre confirmar y escribir el libro, el inverso deja anulado su original, y
/// un documento nuevo se agrega: el libro guarda lo pendiente antes de mover la existencia
/// (ADR-0046 §4).
/// </para>
/// </remarks>
internal static class ElAjusteQueSeConfirma
{
    /// <summary>
    /// Valora el ajuste contra los saldos ya bloqueados, resuelve sus lotes y sus series y lo
    /// confirma, sin guardar.
    /// </summary>
    /// <param name="ajustes">Dónde se resuelven los lotes y las series.</param>
    /// <param name="valoracion">Quién valora contra los saldos bloqueados (ADR-0046 §10).</param>
    /// <param name="ajuste">El ajuste, en borrador.</param>
    /// <param name="numero">Su número, ya tomado en esta transacción (R5).</param>
    /// <param name="saldos">Los saldos de todas las claves que valora, bloqueados.</param>
    /// <param name="ahora">El instante de la confirmación.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Los movimientos que el libro tiene que escribir, o por qué no se valora.</returns>
    internal static async Task<Resultado<IReadOnlyList<MovimientoStock>>> ConfirmarAsync(
        IRepositorioDeAjustes ajustes,
        IValoracionDeExistencias valoracion,
        Ajuste ajuste,
        long numero,
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos,
        DateTimeOffset ahora,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ajustes);
        ArgumentNullException.ThrowIfNull(valoracion);
        ArgumentNullException.ThrowIfNull(ajuste);
        ArgumentNullException.ThrowIfNull(saldos);

        // EL IMPEDIMENTO SE PREGUNTA ANTES DE VALORAR, porque el dominio lanza y el borde necesita un
        // 422 con su código (ADR-0004). Si no se puede valorar, quien llama deshace la transacción
        // con el número y con las valoraciones que el cerrojo creó.
        IReadOnlyList<LineaAValorar> lineas = ajuste.LineasAValorar();

        if (valoracion.LoQueImpide(saldos, lineas, ajuste.Divisa, ajuste.FechaDeOperacion) is { } impedimento)
        {
            return Resultado.Fallo<IReadOnlyList<MovimientoStock>>(
                ErroresDeAjuste.NoSeValora(impedimento, ajuste.Divisa));
        }

        IReadOnlyList<LineaValorada> valoradas =
            valoracion.Valorar(saldos, lineas, ajuste.Divisa, ajuste.FechaDeOperacion);

        // LOS LOTES Y LAS SERIES, DESPUÉS DE LA VALORACIÓN Y ANTES DEL DOCUMENTO (ADR-0048 §4): la
        // existencia los necesita y la valoración no, porque el lote no entra en su clave.
        LotesYSeriesResueltos resueltos = await ajustes
            .ResolverLotesYSeriesAsync(ajuste.LotesQueNombra(), ajuste.SeriesQueNombra(), cancelacion)
            .ConfigureAwait(false);

        var confirmado = new AjusteConfirmado(
            ajuste.Id,
            ajuste.EmpresaId,
            ajuste.AlmacenId,
            ajuste.FechaDeOperacion,
            ajuste.Lineas.Count);

        return Resultado.Correcto(ajuste.Confirmar(numero, confirmado, valoradas, resueltos, ahora));
    }
}
