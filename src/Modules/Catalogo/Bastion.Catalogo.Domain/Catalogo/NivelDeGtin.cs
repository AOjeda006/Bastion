namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>En qué agrupación del artículo va impreso un GTIN (ADR-0051 §3).</summary>
/// <remarks>
/// <para>
/// <b>Se declara, no se deduce del primer dígito.</b> El indicador de un GTIN-14 lo asigna el dueño
/// de la marca y no es un código de nivel, y una caja puede llevar un GTIN-13.
/// </para>
/// <para>
/// Se guarda como texto, como los demás enumerados del sistema. <see cref="Palet"/> sin tilde en el
/// código, y «palé» en la pantalla.
/// </para>
/// </remarks>
public enum NivelDeGtin
{
    /// <summary>La unidad que se vende, la del precio. Lleva una unidad base.</summary>
    Base = 0,

    /// <summary>Una agrupación de unidades base, de dos en adelante.</summary>
    Caja = 1,

    /// <summary>La agrupación de transporte, también de dos unidades base en adelante.</summary>
    Palet = 2,
}
