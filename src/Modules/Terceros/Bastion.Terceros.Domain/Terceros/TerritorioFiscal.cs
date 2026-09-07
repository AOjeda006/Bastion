namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// Dónde tributa el tercero, que es lo que decide qué impuesto indirecto lleva la operación.
/// </summary>
/// <remarks>
/// <para>
/// Los cinco del §7.2, y ni uno más por si acaso. El plan maestro es explícito en que <b>no es un
/// dato de la dirección</b>: dos empresas con el mismo código postal pueden tributar distinto —una
/// establecida en Canarias y una sucursal de una peninsular—, y sobre todo, el territorio se
/// decide por la operación y no por la calle. Por eso vive en la ficha y no dentro de
/// <c>Direccion</c>.
/// </para>
/// <para>
/// <b>Aquí está el valor y no la regla.</b> Que Canarias lleve IGIC en vez de IVA, que Ceuta y
/// Melilla lleven IPSI, y que una entrega intracomunitaria con NIF-IVA válido esté exenta son
/// reglas de cálculo, y el cálculo es de la fase 3. Lo que este ítem fija es dónde se guarda el
/// dato, porque guardarlo después obligaría a migrar un maestro con fichas dentro y a adivinar el
/// territorio de cada una a partir de su código postal, que es justo lo que el §7.2 dice que no se
/// puede hacer.
/// </para>
/// </remarks>
public enum TerritorioFiscal
{
    /// <summary>Territorio de aplicación del IVA español.</summary>
    PeninsulaYBaleares,

    /// <summary>IGIC en vez de IVA.</summary>
    Canarias,

    /// <summary>IPSI en vez de IVA.</summary>
    CeutaYMelilla,

    /// <summary>Otro Estado miembro. La operación puede ser intracomunitaria.</summary>
    UnionEuropea,

    /// <summary>Fuera de la Unión. Exportación o importación.</summary>
    TercerosPaises,
}
