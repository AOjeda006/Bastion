namespace Bastion.Inventario.Domain.Valoraciones;

/// <summary>
/// El método de valoración: recibe lo que un documento quiere mover y devuelve, por línea, su valor
/// y el precio que congela (ADR-0046 §10).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la costura que el plan maestro pide para FIFO</b>, y FIFO no se implementa.
/// <see cref="ElPrecioMedioPonderado"/> valora a partir de los saldos bloqueados. Un FIFO lo haría a
/// partir de sus capas, con su propia lectura bloqueada, y la tabla de capas no se escribe hasta
/// que alguien lo pida.
/// </para>
/// <para>
/// <b>Dos métodos y no uno, por el ADR-0004</b>: el dominio lanza y nunca devuelve un
/// <c>Resultado</c>. El caso de uso pregunta primero con <see cref="LoQueImpide"/> y contesta el
/// <c>422</c> con su código, como ya hace con el estado del documento antes de transitar.
/// <see cref="Valorar"/> lanza si se le llama igualmente.
/// </para>
/// <para>
/// <b>Los saldos son los de todas las claves del documento</b>, leídos con la fila ya bloqueada.
/// Una clave sin saldo es un defecto de quien llama: no se bloqueó, y valorar contra un cero
/// supuesto es la ventana que dos entradas simultáneas se saltan juntas.
/// </para>
/// </remarks>
public interface IValoracionDeExistencias
{
    /// <summary>Dice, sin lanzar, si hay una línea que no se puede valorar y por qué.</summary>
    /// <param name="saldos">El saldo bloqueado de cada clave del documento.</param>
    /// <param name="lineas">Las líneas del documento, en su orden.</param>
    /// <param name="divisa">La divisa del documento.</param>
    /// <returns>El primer impedimento, o <see langword="null"/> si todo se puede valorar.</returns>
    ImpedimentoDeValoracion? LoQueImpide(
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos,
        IReadOnlyList<LineaAValorar> lineas,
        string divisa);

    /// <summary>Valora cada línea contra los saldos y contra las líneas anteriores.</summary>
    /// <param name="saldos">El saldo bloqueado de cada clave del documento.</param>
    /// <param name="lineas">Las líneas del documento, en su orden.</param>
    /// <param name="divisa">La divisa del documento.</param>
    /// <returns>Una valoración por línea, en el orden de las líneas.</returns>
    /// <exception cref="InvalidOperationException">Si <see cref="LoQueImpide"/> habría dicho algo.</exception>
    IReadOnlyList<LineaValorada> Valorar(
        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos,
        IReadOnlyList<LineaAValorar> lineas,
        string divisa);
}
