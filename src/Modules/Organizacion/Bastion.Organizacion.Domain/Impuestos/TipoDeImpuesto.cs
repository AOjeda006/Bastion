namespace Bastion.Organizacion.Domain.Impuestos;

/// <summary>
/// Naturaleza de un impuesto, que es lo que decide en qué casilla del modelo acaba.
/// </summary>
/// <remarks>
/// Los tres del §7, y ni uno más por si acaso. Son excluyentes: una operación lleva IVA o lleva
/// IGIC —nunca los dos—, y la retención se calcula aparte y con su propio signo.
/// </remarks>
public enum TipoDeImpuesto
{
    /// <summary>Impuesto sobre el valor añadido: península y Baleares.</summary>
    Iva,

    /// <summary>Impuesto general indirecto canario: sustituye al IVA en Canarias.</summary>
    Igic,

    /// <summary>Retención a cuenta (IRPF, capital mobiliario). Resta del líquido a pagar.</summary>
    Retencion,
}
