namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// Por qué un documento no se puede valorar, y en qué clave (ADR-0046 §10).
/// </summary>
/// <param name="Motivo">Cuál de las dos cosas lo impide.</param>
/// <param name="Clave">La clave en la que se encontró.</param>
public sealed record ImpedimentoDeValoracion(MotivoDelImpedimento Motivo, ClaveDeValoracion Clave);

/// <summary>Las dos cosas que impiden valorar. Cada una tiene su código en el borde.</summary>
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
}
