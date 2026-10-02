namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>Por qué un texto no es el GTIN de un artículo (ADR-0051 §1 y §4).</summary>
/// <remarks>
/// Los tres primeros son de forma: el texto no es un GTIN. Los cuatro últimos son de prefijo: tiene
/// forma de GTIN, con su control bien, y no es el de un artículo. Cada uno tiene su <c>type</c> en
/// la API, para que la pantalla diga cuál.
/// </remarks>
public enum MotivoDeRechazoDelGtin
{
    /// <summary>Lleva algo que no es una cifra ASCII, también por dentro.</summary>
    NoSonDigitos = 0,

    /// <summary>No tiene 8, 12, 13 ni 14 cifras.</summary>
    LargoNoAdmitido = 1,

    /// <summary>La última cifra no es la que sale de las demás (§7.9.1).</summary>
    DigitoDeControl = 2,

    /// <summary>Un número de circulación restringida: fuera de su ámbito no es único (§2.1.11).</summary>
    CirculacionRestringida = 3,

    /// <summary>Un GTIN-14 con indicador 9, al que le falta la medida (§2.1.10).</summary>
    MedidaVariable = 4,

    /// <summary>Un cupón o un vale de devolución, que no son artículos (§2.6.3 y §2.6.4).</summary>
    Cupon = 5,

    /// <summary>Un prefijo reservado para el futuro, o que se dio para algo que no es un GTIN.</summary>
    SinAsignar = 6,
}
