using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Las restricciones <c>CHECK</c> cuya violación <b>no</b> es un fallo del servidor sino una regla
/// de negocio que solo el motor puede guardar, cada una con el error que la cuenta.
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
/// <b>Cada entrada lleva su error y su motivo</b>, y <c>CadaRestriccionTraducidaSeJustificaTests</c>
/// compara esta lista con el modelo: una restricción declarada que no existe pone el carril rojo,
/// y así una traducción no sobrevive a la restricción que traducía.
/// </para>
/// </remarks>
public sealed class RestriccionesQueGuardanUnaRegla
{
    private readonly Dictionary<string, RestriccionDeclarada> _declaradas = new(StringComparer.Ordinal);

    /// <summary>Las restricciones declaradas, con su error y su motivo, por nombre.</summary>
    public IReadOnlyDictionary<string, RestriccionDeclarada> Declaradas => _declaradas;

    /// <summary>Declara que la violación de esta restricción es una regla de negocio.</summary>
    /// <param name="restriccion">El nombre de la restricción <b>tal como lo crea la migración</b>,
    /// que es lo único que trae la excepción del motor: <c>ConstraintName</c>.</param>
    /// <param name="error">El error con el que se contesta. Lo declara el módulo dueño de la
    /// restricción, porque qué significa que salte es una afirmación sobre su dominio.</param>
    /// <param name="motivo">Por qué la regla no se puede comprobar antes de escribir.</param>
    /// <returns>La misma lista, para encadenar.</returns>
    public RestriccionesQueGuardanUnaRegla Declarar(string restriccion, ErrorDeOperacion error, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(restriccion);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        _declaradas[restriccion] = new RestriccionDeclarada(error, motivo);

        return this;
    }
}

/// <summary>Una restricción declarada: el error que la cuenta y por qué solo la guarda el motor.</summary>
/// <param name="Error">El error con el que contesta el borde.</param>
/// <param name="Motivo">Por qué la regla no se puede comprobar antes de escribir.</param>
public sealed record RestriccionDeclarada(ErrorDeOperacion Error, string Motivo);
