namespace Bastion.BuildingBlocks.Domain.Resultados;

/// <summary>
/// Desenlace de una operación de negocio que no devuelve valor: correcto, o fallido con un
/// <see cref="ErrorDeOperacion"/>.
/// </summary>
/// <remarks>
/// Cruza UNA sola costura: de la capa de Aplicación hacia el borde. No es un mecanismo general
/// de propagación de errores y no debe usarse dentro del dominio, donde una invariante rota es
/// una excepción. La frontera exacta, con ejemplos, está en el ADR-0004.
/// </remarks>
public sealed record Resultado
{
    private static readonly Resultado s_correcto = new((ErrorDeOperacion?)null);

    private Resultado(ErrorDeOperacion? error) => Error = error;

    /// <summary>Error, o <see langword="null"/> si la operación salió bien.</summary>
    public ErrorDeOperacion? Error { get; }

    /// <summary>Indica si la operación salió bien.</summary>
    public bool EsCorrecto => Error is null;

    /// <summary>Desenlace correcto.</summary>
    public static Resultado Correcto() => s_correcto;

    /// <summary>Desenlace fallido.</summary>
    public static Resultado Fallo(ErrorDeOperacion error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Resultado(error);
    }

    /// <summary>Desenlace correcto, con su valor.</summary>
    /// <typeparam name="T">Lo que devuelve la operación.</typeparam>
    public static Resultado<T> Correcto<T>(T valor) => new(valor, null);

    /// <summary>Desenlace fallido de una operación que devuelve valor.</summary>
    /// <typeparam name="T">Lo que devolvería la operación si saliera bien.</typeparam>
    public static Resultado<T> Fallo<T>(ErrorDeOperacion error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Resultado<T>(default!, error);
    }
}

/// <summary>
/// Desenlace de una operación de negocio que devuelve un valor: correcto con el valor, o
/// fallido con un <see cref="ErrorDeOperacion"/>.
/// </summary>
/// <remarks>
/// Sus fábricas viven en <see cref="Resultado"/>, que no es genérico, para que el que llama no
/// tenga que escribir el argumento de tipo: <c>Resultado.Correcto(articulo)</c> lo infiere.
/// </remarks>
/// <typeparam name="T">Lo que devuelve la operación cuando sale bien.</typeparam>
public sealed record Resultado<T>
{
    internal Resultado(T valor, ErrorDeOperacion? error) => (ValorSinComprobar, Error) = (valor, error);

    // El valor tal cual, incluso cuando el resultado es fallido y entonces no significa nada.
    // Privado a propósito: fuera solo se llega a él por `Valor`, que comprueba antes.
    private T ValorSinComprobar { get; }

    /// <summary>Error, o <see langword="null"/> si la operación salió bien.</summary>
    public ErrorDeOperacion? Error { get; }

    /// <summary>Indica si la operación salió bien.</summary>
    public bool EsCorrecto => Error is null;

    /// <summary>Valor devuelto por la operación.</summary>
    /// <remarks>
    /// Leerlo en un resultado fallido LANZA, y no devuelve <c>default</c>: quien lo hace se ha
    /// saltado la comprobación, que es un fallo de programación. Devolver un cero o un
    /// <see langword="null"/> lo dejaría correr río abajo hasta reventar lejos de la causa.
    /// </remarks>
    /// <exception cref="InvalidOperationException">El resultado es fallido.</exception>
    public T Valor => EsCorrecto
        ? ValorSinComprobar
        : throw new InvalidOperationException(
            $"Este resultado es fallido ({Error!.Codigo}): compruebe EsCorrecto antes de leer Valor.");
}
