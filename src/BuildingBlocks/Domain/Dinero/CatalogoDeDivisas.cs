namespace Bastion.BuildingBlocks.Domain.Dinero;

// La divisa se guarda como texto ISO 4217 y no como enumerado: el catálogo lo mantiene la ISO,
// no nosotros, y un enumerado obligaría a recompilar para admitir una divisa más.
/// <summary>
/// Catálogo de divisas: qué es un código válido y con cuántos decimales se redondea cada una.
/// </summary>
/// <remarks>
/// <para>
/// Pasó de <c>internal</c> a <c>public</c> en el ítem 0.4: la divisa base de una empresa se
/// valida contra este mismo catálogo. Duplicar la comprobación en el módulo habría sido tener
/// dos listas de divisas que se separan en cuanto entre la segunda.
/// </para>
/// <para>
/// <b>Se llamaba <c>Divisas</c> hasta el 0.15</b>, y el nombre tuvo que cambiar cuando Organización
/// estrenó su maestro <c>Divisa</c>: la carpeta de un agregado se llama por su plural en todo el
/// proyecto —<c>Almacenes</c>, <c>Empresas</c>, <c>Series</c>—, así que el espacio de nombres
/// <c>Bastion.Organizacion.Domain.Divisas</c> tapaba a esta clase para todo el módulo y
/// <c>Divisas.Normalizar(...)</c> dejaba de compilar (CS0118). Renombrar la clase, y no la carpeta,
/// es lo que deja la convención intacta; y de paso el nombre dice lo que es: el catálogo, no las
/// divisas con las que se opera, que son las filas de la tabla.
/// </para>
/// </remarks>
public static class CatalogoDeDivisas
{
    // Unidad mínima (decimales de redondeo fiscal) POR divisa. Deliberadamente NO hay valor por
    // omisión: suponer dos decimales acertaría con el dólar y fallaría en silencio con el yen
    // (cero decimales) o el dinar (tres). Cada divisa entra aquí con su caso dorado.
    //
    // Hasta el 0.15 solo estaba el euro, «que es lo único que Bastion factura», y era coherente
    // mientras no hubiera nada más. Con el maestro de divisas y los tipos de cambio del §7, no lo
    // era: `Divisa.Crear` exige que el redondeo se conozca —para que la tabla y este catálogo no
    // puedan separarse—, así que con una sola divisa conocida la tabla no admitiría ninguna otra
    // fila y `TipoCambio`, que relaciona DOS, no podría tener ni una. Un maestro cuyo conjunto
    // solo puede estar vacío no es un maestro.
    //
    // Las cinco son las que una pyme española se encuentra de verdad. El yen está a propósito:
    // es el contraejemplo que impide que alguien «simplifique» esto a una constante 2.
    // Y sigue habiendo divisas fuera —el dinar kuwaití redondea a TRES— para que la puerta que
    // rechaza lo desconocido siga teniendo algo que rechazar.
    private static readonly Dictionary<string, int> s_unidadMinima = new(StringComparer.Ordinal)
    {
        ["EUR"] = 2,
        ["USD"] = 2,
        ["GBP"] = 2,
        ["CHF"] = 2,
        ["JPY"] = 0,
    };

    /// <summary>Normaliza el código y comprueba que tiene forma ISO 4217.</summary>
    public static string Normalizar(string divisa)
    {
        ArgumentNullException.ThrowIfNull(divisa);

        return ConForma(divisa) ?? throw new ArgumentException(
            $"La divisa {divisa} no es un código ISO 4217 (tres letras, como EUR).", nameof(divisa));
    }

    /// <summary>Indica si se conoce el redondeo fiscal de la divisa, sin lanzar por nada.</summary>
    /// <remarks>
    /// La puerta que PREGUNTA, frente a <see cref="UnidadMinima"/>, que EXIGE. El borde necesita
    /// preguntar para poder responder «divisaBase: no se conoce el redondeo» en el campo que toca;
    /// dentro, en cambio, una divisa que no se sabe redondear es un fallo de programación y por eso
    /// <see cref="UnidadMinima"/> lanza. Las dos leen el mismo catálogo (ver ADR-0004).
    /// </remarks>
    public static bool EsConocida(string divisa)
    {
        ArgumentNullException.ThrowIfNull(divisa);

        string? normalizada = ConForma(divisa);

        return normalizada is not null && s_unidadMinima.ContainsKey(normalizada);
    }

    // Un solo sitio decide qué es «forma de divisa». Si normalizar y preguntar tuvieran cada uno
    // su copia, una acabaría admitiendo lo que la otra rechaza.
    private static string? ConForma(string divisa)
    {
        string normalizada = divisa.Trim().ToUpperInvariant();

        return normalizada.Length == 3 && normalizada.All(char.IsAsciiLetterUpper) ? normalizada : null;
    }

    /// <summary>Decimales de redondeo fiscal de la divisa; lanza si no se conoce.</summary>
    public static int UnidadMinima(string divisa) =>
        s_unidadMinima.TryGetValue(divisa, out int decimales)
            ? decimales
            : throw new NotSupportedException(
                $"No se conoce la unidad mínima de {divisa}. Añádela en CatalogoDeDivisas con su caso dorado " +
                "antes de redondear importes en esa divisa.");
}
