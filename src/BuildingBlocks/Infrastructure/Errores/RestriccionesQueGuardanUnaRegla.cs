using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Las restricciones cuya violación <b>no</b> es un fallo del servidor sino una regla de negocio
/// que solo el motor puede guardar, cada una con su clase y con el error que la cuenta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una lista cerrada y corta a propósito, como la de los índices.</b> Casi toda restricción
/// del sistema guarda un invariante que el dominio ya comprueba antes de escribir, y si salta es un
/// defecto: un <c>500</c> es la respuesta honrada. Meter aquí una es afirmar algo más fuerte: que la
/// regla <b>no se puede</b> comprobar antes, porque entre leer y escribir cabe otra transacción, y
/// que la única guarda que dos peticiones simultáneas no cruzan a la vez es la del motor.
/// </para>
/// <para>
/// <b>Admite restricciones <c>CHECK</c> y, desde el 2.9, índices únicos</b> (ADR-0048 §3). Que un
/// número de serie no esté en dos sitios lo guarda un índice único parcial, porque un <c>CHECK</c>
/// de fila no ve las demás filas, y su violación es un <c>23505</c> y no un <c>23514</c>. Cada
/// entrada dice de qué clase es, y el manejador exige las dos cosas: el nombre y el código que le
/// corresponde. Un nombre no puede estar a la vez aquí y en
/// <see cref="IndicesQueDelatanUnaCarreraPerdida"/>, que traduce el mismo <c>23505</c> a otra cosa.
/// </para>
/// <para>
/// <b>Cada entrada lleva su error y su motivo</b>, y <c>CadaRestriccionTraducidaSeJustificaTests</c>
/// compara esta lista con el modelo: una restricción declarada que no existe, o que existe con otra
/// clase, pone el carril rojo, y así una traducción no sobrevive a la restricción que traducía. El
/// mismo fichero comprueba que ningún nombre esté en las dos listas.
/// </para>
/// </remarks>
public sealed class RestriccionesQueGuardanUnaRegla
{
    private readonly Dictionary<string, RestriccionDeclarada> _declaradas = new(StringComparer.Ordinal);

    /// <summary>Las restricciones declaradas, con su clase, su error y su motivo, por nombre.</summary>
    public IReadOnlyDictionary<string, RestriccionDeclarada> Declaradas => _declaradas;

    /// <summary>Declara que la violación de esta restricción es una regla de negocio.</summary>
    /// <param name="restriccion">El nombre de la restricción o del índice <b>tal como lo crea la
    /// migración</b>, que es lo único que trae la excepción del motor: <c>ConstraintName</c>.</param>
    /// <param name="clase">Si es un <c>CHECK</c> o un índice único, que es lo que dice con qué código
    /// llega su violación.</param>
    /// <param name="error">El error con el que se contesta. Lo declara el módulo dueño de la
    /// restricción, porque qué significa que salte es una afirmación sobre su dominio.</param>
    /// <param name="motivo">Por qué la regla no se puede comprobar antes de escribir.</param>
    /// <returns>La misma lista, para encadenar.</returns>
    public RestriccionesQueGuardanUnaRegla Declarar(
        string restriccion, ClaseDeRestriccion clase, ErrorDeOperacion error, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(restriccion);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        if (!Enum.IsDefined(clase))
        {
            throw new ArgumentOutOfRangeException(nameof(clase), clase, "Clase de restricción desconocida.");
        }

        _declaradas[restriccion] = new RestriccionDeclarada(clase, error, motivo);

        return this;
    }
}

/// <summary>De qué clase es una restricción, que es lo que decide con qué código llega su violación.</summary>
/// <remarks>
/// <b>No tiene valor cero, y es a propósito</b>: una declaración que olvidara la clase no pasaría por
/// una de las dos, y <see cref="RestriccionesQueGuardanUnaRegla.Declarar"/> la rechaza.
/// </remarks>
public enum ClaseDeRestriccion
{
    /// <summary>Una restricción <c>CHECK</c>: su violación es un <c>23514 check_violation</c>.</summary>
    Comprobacion = 1,

    /// <summary>Un índice único: su violación es un <c>23505 unique_violation</c>.</summary>
    Unicidad = 2,
}

/// <summary>
/// Una restricción declarada: su clase, el error que la cuenta y por qué solo la guarda el motor.
/// </summary>
/// <param name="Clase">Si es un <c>CHECK</c> o un índice único.</param>
/// <param name="Error">El error con el que contesta el borde.</param>
/// <param name="Motivo">Por qué la regla no se puede comprobar antes de escribir.</param>
public sealed record RestriccionDeclarada(ClaseDeRestriccion Clase, ErrorDeOperacion Error, string Motivo);
