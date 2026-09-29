using Bastion.BuildingBlocks.Domain.Dinero;

namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// El precio medio ponderado perpetuo, por artículo y almacén (ADR-0046 §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada fila, con <c>p</c> el precio medio de antes y <c>V</c> el valor de antes:</b>
/// </para>
/// <list type="bullet">
/// <item>una entrada con coste <c>c</c> suma <c>c.Por(q)</c>, y congela el precio de después;</item>
/// <item>una entrada sin coste suma <c>p.Por(q)</c>, y sin <c>p</c> no se puede valorar;</item>
/// <item>una salida resta <c>min(p.Por(q), V)</c>, o <c>V</c> entero si vacía la clave, y congela
/// <c>p</c>;</item>
/// <item>el inverso de una salida suma su valor exacto, y el de una entrada resta
/// <c>min(v, V)</c>, o <c>V</c> entero si vacía la clave.</item>
/// </list>
/// <para>
/// <b>El redondeo es el de la R6, en un solo punto</b>: <see cref="PrecioUnitario.Por"/>. Una
/// salida resta exactamente el importe redondeado de su fila, y lo que queda es lo que había menos
/// lo que la fila dice. Por eso una salida puede mover el precio medio que se deduce después en la
/// sexta decimal, aunque se valore con el de antes.
/// </para>
/// <para>
/// <b>Vaciar la clave se lleva todo el valor</b>, aunque el redondeo diga otra cosa. Si no, quedaría
/// valor sin unidades que lo sostengan, que es lo que la tabla prohíbe. Y el tope <c>V</c> solo lo
/// toca el redondeo: un precio medio redondeado hacia arriba por una cantidad que casi vacía la
/// clave puede pasar del valor por una diezmilésima.
/// </para>
/// <para>
/// <b>La cantidad puede quedar negativa aquí, y no es asunto de este cálculo.</b> El que rechaza el
/// negativo es el motor, con el <c>CHECK</c> de la existencia, que es la única guarda que dos
/// salidas simultáneas no cruzan a la vez (ADR-0046 §4). Una salida que no cabe se valora como si
/// vaciara la clave, y el documento no llega a escribirse.
/// </para>
/// <para>
/// <b>La fecha, antes que la divisa</b>, clave por clave: un documento con una fecha anterior al
/// último movimiento de una de sus claves no se valora (ADR-0047). Así, dentro de cada clave, el
/// orden de confirmación y el de la fecha coinciden, y la suma del libro hasta un día cualquiera es
/// un estado que la clave tuvo de verdad.
/// </para>
/// </remarks>
public sealed class ElPrecioMedioPonderado : IValoracionDeExistencias
{
    /// <inheritdoc/>
    public ImpedimentoDeValoracion? LoQueImpide(
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos,
        IReadOnlyList<LineaAValorar> lineas,
        string divisa,
        DateOnly fechaDeOperacion) =>
        Recorrer(saldos, lineas, divisa, fechaDeOperacion).Impedimento;

    /// <inheritdoc/>
    public IReadOnlyList<LineaValorada> Valorar(
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos,
        IReadOnlyList<LineaAValorar> lineas,
        string divisa,
        DateOnly fechaDeOperacion)
    {
        (LineaValorada[]? valoradas, ImpedimentoDeValoracion? impedimento) =
            Recorrer(saldos, lineas, divisa, fechaDeOperacion);

        return impedimento is null
            ? valoradas!
            : throw new InvalidOperationException(
                $"El documento no se puede valorar ({impedimento.Motivo}, en el artículo " +
                $"{impedimento.Clave.ArticuloId} del almacén {impedimento.Clave.AlmacenId}). El caso " +
                "de uso tenía que haberlo preguntado antes con LoQueImpide.");
    }

    /// <summary>
    /// Primero lo que sube y después lo que baja, cada grupo en el orden de sus líneas.
    /// </summary>
    /// <remarks>
    /// Si no, un documento con una salida y una entrada del mismo artículo pasaría a mitad de camino
    /// por un saldo negativo que no ha existido, y valoraría contra él. El libro y la existencia
    /// suman igual en cualquier orden; la valoración no, y por eso se fija este.
    /// </remarks>
    private static IEnumerable<int> EnOrdenDeValoracion(IReadOnlyList<LineaAValorar> lineas) =>
        Enumerable.Range(0, lineas.Count).OrderBy(indice => lineas[indice].Cantidad > 0m ? 0 : 1);

    private static (LineaValorada[]? Valoradas, ImpedimentoDeValoracion? Impedimento) Recorrer(
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos,
        IReadOnlyList<LineaAValorar> lineas,
        string divisa,
        DateOnly fechaDeOperacion)
    {
        ArgumentNullException.ThrowIfNull(saldos);
        ArgumentNullException.ThrowIfNull(lineas);

        string laDelDocumento = CatalogoDeDivisas.Normalizar(divisa);
        Dictionary<ClaveDeValoracion, (decimal Cantidad, Importe Valor)> vivos = [];

        foreach (LineaAValorar linea in lineas)
        {
            ArgumentNullException.ThrowIfNull(linea, nameof(lineas));

            if ((linea.Coste is { } coste && coste.Divisa != laDelDocumento)
                || (linea.ValorQueCompensa is { } compensa && compensa.Divisa != laDelDocumento))
            {
                throw new ArgumentException(
                    "Los importes de una línea van en la divisa del documento: la línea no tiene " +
                    "divisa propia (ADR-0046 §7).",
                    nameof(lineas));
            }

            if (vivos.ContainsKey(linea.Clave))
            {
                continue;
            }

            if (!saldos.TryGetValue(linea.Clave, out SaldoValorado? saldo))
            {
                throw new ArgumentException(
                    $"El artículo {linea.Clave.ArticuloId} del almacén {linea.Clave.AlmacenId} " +
                    "llega sin su saldo bloqueado: valorarlo desde cero sería valorar sin cerrojo.",
                    nameof(saldos));
            }

            // NINGUNA FECHA ANTERIOR AL ÚLTIMO MOVIMIENTO DE LA CLAVE (ADR-0047), y es lo primero:
            // un documento atrasado no se confirma en esa fecha, pase lo que pase con la divisa. La
            // misma fecha vale; dos documentos del mismo día sobre la misma clave son lo normal.
            if (saldo.UltimaFecha is { } ultima && fechaDeOperacion < ultima)
            {
                return (null, new ImpedimentoDeValoracion(
                    MotivoDelImpedimento.FechaAnteriorAlUltimoMovimiento, linea.Clave)
                { UltimaFecha = ultima });
            }

            // UNA CLAVE VACÍA EN OTRA DIVISA EMPIEZA DE NUEVO en la del documento: no hay nada que
            // convertir. Con cantidad o con valor, sumarlos pediría un tipo de cambio con fecha.
            if (saldo.Valor.Divisa == laDelDocumento)
            {
                vivos[linea.Clave] = (saldo.Cantidad, saldo.Valor);
            }
            else if (saldo.Cantidad == 0m)
            {
                vivos[linea.Clave] = (0m, Importe.Cero(laDelDocumento));
            }
            else
            {
                return (null, new ImpedimentoDeValoracion(MotivoDelImpedimento.ValoracionEnOtraDivisa, linea.Clave));
            }
        }

        var valoradas = new LineaValorada[lineas.Count];

        foreach (int indice in EnOrdenDeValoracion(lineas))
        {
            LineaAValorar linea = lineas[indice];
            (decimal cantidad, Importe valor) = vivos[linea.Clave];

            PrecioUnitario? antes = cantidad > 0m
                ? PrecioUnitario.De(valor.Cantidad / cantidad, laDelDocumento)
                : null;

            decimal despues = cantidad + linea.Cantidad;
            LineaValorada valorada;

            if (linea.Cantidad > 0m)
            {
                Importe? suma = linea.ValorQueCompensa ?? linea.Coste?.Por(linea.Cantidad) ?? antes?.Por(linea.Cantidad);

                if (suma is null)
                {
                    return (null, new ImpedimentoDeValoracion(
                        MotivoDelImpedimento.EntradaSinCosteNiPrecioMedio, linea.Clave));
                }

                Importe queda = valor + suma;

                valorada = new LineaValorada(suma, PrecioUnitario.De(queda.Cantidad / despues, laDelDocumento));
            }
            else
            {
                PrecioUnitario congelado = antes ?? PrecioUnitario.De(0m, laDelDocumento);

                Importe loQueDice = linea.ValorQueCompensa is { } compensa
                    ? Importe.De(-compensa.Cantidad, laDelDocumento)
                    : congelado.Por(-linea.Cantidad);

                Importe resta = despues <= 0m || loQueDice.Cantidad > valor.Cantidad ? valor : loQueDice;

                valorada = new LineaValorada(Importe.De(-resta.Cantidad, laDelDocumento), congelado);
            }

            valoradas[indice] = valorada;
            vivos[linea.Clave] = (despues, valor + valorada.Valor);
        }

        return (valoradas, null);
    }
}
