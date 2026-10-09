using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>
/// El núcleo de la anulación de un ajuste, que comparten sus dos caminos (ADR-0055 §9): el público,
/// <see cref="AnularAjuste"/>, y el del recuento, que anula el ajuste de su diferencia.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las guardas no viven aquí</b>: cada camino pregunta lo suyo antes de construir el inverso. El
/// público rechaza el inverso de otro y el ajuste de un recuento, y el del recuento comprueba que el
/// ajuste es el suyo. Lo que hay aquí es lo que tiene que ser igual en los dos: el número, la
/// valoración, los lotes y las series, el libro y el original anulado.
/// </para>
/// <para>
/// <b>No guarda</b>: el <c>SaveChanges</c> es de quien llama, que todavía tiene que escribir lo suyo
/// en la misma transacción. El recuento pasa a anulado junto con su ajuste, o nada.
/// </para>
/// </remarks>
internal static class ElInversoQueCompensa
{
    /// <summary>
    /// Numera, valora y confirma el inverso, escribe su libro y deja el original anulado, sin guardar.
    /// </summary>
    /// <param name="ajustes">Dónde viven el documento, las valoraciones y el libro.</param>
    /// <param name="numerador">Quién entrega el correlativo, en esta misma transacción (R5).</param>
    /// <param name="valoracion">Quién valora el inverso contra los saldos bloqueados (ADR-0046 §10).</param>
    /// <param name="original">El ajuste que se anula, confirmado.</param>
    /// <param name="inverso">Su inverso, en borrador, con la fecha de hoy.</param>
    /// <param name="ahora">El instante de la anulación.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El inverso confirmado, o el motivo por el que no se puede.</returns>
    internal static async Task<Resultado<Ajuste>> CompensarAsync(
        IRepositorioDeAjustes ajustes,
        INumeradorDeSeriesDeInventario numerador,
        IValoracionDeExistencias valoracion,
        Ajuste original,
        Ajuste inverso,
        DateTimeOffset ahora,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ajustes);
        ArgumentNullException.ThrowIfNull(numerador);
        ArgumentNullException.ThrowIfNull(valoracion);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(inverso);

        // LA EXCEPCIÓN DE LA NUMERACIÓN, ESCRITA: el inverso lleva la fecha de hoy pero numera en la
        // serie de su original, y por eso la fecha que se le pasa al numerador es LA DEL ORIGINAL,
        // que es la que cae en el ejercicio de esa serie. Pasarle `hoy` obligaría a numerar en una
        // serie del ejercicio de hoy, y anular fallaría cada vez que esa serie no existiera —o
        // sea, siempre que el original fuera de otro año—, cuando la R2 promete que anular se puede
        // siempre. El inverso pertenece a su original, no a su fecha.
        //
        // Vale para los ajustes y NO es regla para todos. Una factura rectificativa exige serie
        // propia (Reglamento de facturación, RD 1619/2012, art. 6; se contrasta con la biblioteca
        // al abrir la fase 5), y quien la numere elegirá esa serie y le pasará su propia fecha.
        Resultado<long> numero = await numerador
            .TomarNumeroAsync(
                inverso.SerieId, DocumentoQueNumera.Ajuste, original.FechaDeOperacion, cancelacion)
            .ConfigureAwait(false);

        if (!numero.EsCorrecto)
        {
            return Resultado.Fallo<Ajuste>(numero.Error!);
        }

        // LA VALORACIÓN DEL INVERSO, en el mismo sitio que al confirmar (ADR-0046 §2). Cada línea
        // trae el valor de la del original como valor que compensa, así que el par suma cero
        // también en valor, salvo que otras salidas se hayan llevado parte de él entretanto
        // (ADR-0046 §6).
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos = await ajustes
            .BloquearLasValoracionesAsync(
                [.. inverso.LineasAValorar().Select(linea => linea.Clave).Distinct()], inverso.Divisa, cancelacion)
            .ConfigureAwait(false);

        // LOS LOTES Y LAS SERIES DEL ORIGINAL, que el inverso copia (ADR-0048 §5): resuelven a las
        // mismas filas, porque la clave es la misma. Y la marca no se lee: el original tiene
        // movimientos, así que su marca ya no puede cambiar.
        Resultado<IReadOnlyList<MovimientoStock>> movimientos = await ElAjusteQueSeConfirma
            .ConfirmarAsync(ajustes, valoracion, inverso, numero.Valor, saldos, ahora, cancelacion)
            .ConfigureAwait(false);

        if (!movimientos.EsCorrecto)
        {
            return Resultado.Fallo<Ajuste>(movimientos.Error!);
        }

        original.Anular(inverso, new AjusteAnulado(original.Id, original.EmpresaId));

        ajustes.Agregar(inverso);
        await ajustes.AnotarEnElLibroAsync(movimientos.Valor, cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(inverso);
    }
}
