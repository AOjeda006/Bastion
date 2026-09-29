namespace Bastion.Inventario.Domain.LotesYSeries;

/// <summary>
/// La forma de un código de lote o de número de serie: la de la etiqueta GS1 que lo lleva
/// (ADR-0048 §2).
/// </summary>
/// <remarks>
/// <para>
/// <b>De 1 a 20 caracteres del conjunto 82</b>, que es lo que admiten el AI 10 (lote) y el AI 21
/// (serie). Un código que no cabe en la etiqueta no se puede leer con el escáner, y el día que el
/// almacén lea en vez de teclear, los que se hubieran escrito a mano no casarían con nada.
/// </para>
/// <para>
/// <b>Se recortan los extremos y nada más.</b> Un espacio a los lados es un descuido al teclear; uno
/// dentro no es del conjunto, así que se rechaza en vez de quitarse. <b>La caja se conserva</b>:
/// GS1 la distingue, y <c>a1</c> y <c>A1</c> son dos lotes del proveedor. Juntarlos mezclaría su
/// trazabilidad, que es justo lo que se guarda para no mezclar.
/// </para>
/// </remarks>
public static class CodigoGs1
{
    /// <summary>El largo máximo de un código, el de los AI 10 y 21.</summary>
    public const int LargoMaximo = 20;

    /// <summary>
    /// Los caracteres del conjunto 82, uno a uno: el ASCII imprimible menos los doce que GS1 deja
    /// fuera y el espacio.
    /// </summary>
    private const string Conjunto =
        "!\"%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";

    private static readonly System.Buffers.SearchValues<char> s_conjunto =
        System.Buffers.SearchValues.Create(Conjunto);

    /// <summary>El código recortado, o <see langword="null"/> si no es un código GS1.</summary>
    /// <param name="codigo">Lo que se escribió.</param>
    /// <returns>El código sin los espacios de los extremos, o <see langword="null"/>.</returns>
    public static string? Normalizar(string? codigo)
    {
        string recortado = (codigo ?? string.Empty).Trim();

        return recortado.Length is >= 1 and <= LargoMaximo
            && !recortado.AsSpan().ContainsAnyExcept(s_conjunto)
            ? recortado
            : null;
    }
}
