namespace Bastion.BuildingBlocks.Application.Importacion;

/// <summary>
/// La regla del apóstrofo de un campo de texto, en los dos sentidos: la exportación lo pone y la
/// importación lo quita, y lo que sale vuelve a entrar igual (ADR-0075).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué se pone</b>: un texto que empieza por <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, un
/// tabulador o un retorno, una hoja de cálculo lo lee como una fórmula, y con el apóstrofo delante,
/// como texto (decisión 7 del 1.11).
/// </para>
/// <para>
/// <b>Por qué el propio apóstrofo es el séptimo</b>: sin él, la vuelta no sería exacta. Un texto que
/// ya empieza por <c>'=</c> saldría igual que uno que empieza por <c>=</c>, y al quitar el apóstrofo
/// no se sabría cuál de los dos era. Con él, <c>'=Foo</c> sale <c>''=Foo</c>, y vuelve.
/// </para>
/// <para>
/// <b>Solo se quita uno, y solo delante de uno de los siete</b>, que es exactamente lo que se puso.
/// Un apóstrofo delante de otra cosa no lo puso Bastion, y se queda.
/// </para>
/// </remarks>
public static class ApostrofoDelDialecto
{
    /// <summary>El apóstrofo, que una hoja de cálculo lee como «lo que sigue es texto».</summary>
    public const char Apostrofo = '\'';

    private static readonly char[] s_disparadores = ['=', '+', '-', '@', '\t', '\r', Apostrofo];

    /// <summary>
    /// El texto como sale en el fichero: con un apóstrofo delante, si empieza por uno de los siete.
    /// </summary>
    /// <param name="texto">El texto, tal como lo guarda Bastion.</param>
    public static string Poner(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return EmpiezaPorUnDisparador(texto) ? Apostrofo + texto : texto;
    }

    /// <summary>
    /// El texto como lo guarda Bastion: sin el apóstrofo que le puso <see cref="Poner"/>.
    /// </summary>
    /// <param name="campo">El campo, tal como llegó en el fichero.</param>
    public static string Quitar(string campo)
    {
        ArgumentNullException.ThrowIfNull(campo);

        return campo.Length > 1 && campo[0] == Apostrofo && EmpiezaPorUnDisparador(campo.AsSpan(1))
            ? campo[1..]
            : campo;
    }

    private static bool EmpiezaPorUnDisparador(ReadOnlySpan<char> texto) =>
        texto.Length > 0 && Array.IndexOf(s_disparadores, texto[0]) >= 0;
}
