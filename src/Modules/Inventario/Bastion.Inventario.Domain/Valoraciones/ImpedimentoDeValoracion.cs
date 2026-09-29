namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// Por qué un documento no se puede valorar, y en qué clave (ADR-0046 §10).
/// </summary>
/// <param name="Motivo">Cuál de las tres cosas lo impide.</param>
/// <param name="Clave">La clave en la que se encontró.</param>
public sealed record ImpedimentoDeValoracion(MotivoDelImpedimento Motivo, ClaveDeValoracion Clave)
{
    /// <summary>
    /// El último movimiento de la clave, cuando el motivo es
    /// <see cref="MotivoDelImpedimento.FechaAnteriorAlUltimoMovimiento"/>: la fecha que el
    /// documento tiene que alcanzar. Con los demás motivos no se dice.
    /// </summary>
    public DateOnly? UltimaFecha { get; init; }
}

/// <summary>Las tres cosas que impiden valorar. Cada una tiene su código en el borde.</summary>
public enum MotivoDelImpedimento
{
    /// <summary>
    /// Una entrada sin coste en una clave sin cantidad: no hay precio medio que tomar. Se arregla
    /// dándole coste. Es <c>ajuste-entrada-sin-coste-ni-precio-medio</c>.
    /// </summary>
    EntradaSinCosteNiPrecioMedio = 1,

    /// <summary>
    /// La clave tiene existencias valoradas en una divisa que no es la del documento, y sumarlas
    /// exige un tipo de cambio con fecha, que llega en la fase 6. Es
    /// <c>ajuste-valoracion-en-otra-divisa</c>.
    /// </summary>
    ValoracionEnOtraDivisa = 2,

    /// <summary>
    /// El documento lleva una fecha anterior al último movimiento de la clave, y sumar hasta los
    /// días de en medio daría un estado que la clave no tuvo nunca (ADR-0047). Se arregla con esa
    /// fecha o una posterior. Es <c>ajuste-fecha-anterior-al-ultimo-movimiento</c>.
    /// </summary>
    FechaAnteriorAlUltimoMovimiento = 3,
}
