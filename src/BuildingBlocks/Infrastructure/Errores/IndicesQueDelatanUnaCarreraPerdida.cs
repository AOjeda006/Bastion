namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// Los índices únicos cuya violación <b>no</b> es un fallo del servidor sino una carrera perdida,
/// y que por eso se contestan con el mismo <c>412</c> que da el testigo de concurrencia.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una lista cerrada y corta a propósito, y lo normal es NO estar en ella.</b> El sistema
/// tiene medio centenar de índices únicos y ninguno de los demás se traduce: una violación de
/// unicidad es, casi siempre, un defecto —dos altas con el mismo NIF, un código repetido— y un
/// <c>500</c> es la respuesta honrada, porque el cliente no puede hacer nada útil con ella. Meter
/// aquí un índice es afirmar algo mucho más fuerte: que <b>el único desenlace posible</b> de esa
/// violación es que otra petición legítima llegó antes, y que quien la pierde debe releer el
/// recurso exactamente igual que si hubiera chocado contra el testigo.
/// </para>
/// <para>
/// <b>Por qué no basta con el testigo, que es la pregunta que esto levanta.</b> Cuando una
/// operación escribe DOS filas —una nueva y un cambio en la de al lado— el testigo protege la
/// segunda y no la primera, y cuál de las dos llega antes a la base lo decide el ORM, no el caso
/// de uso. El índice es la garantía que no depende de ese orden ni de que todo camino futuro se
/// acuerde de tocar la fila con testigo; la traducción es lo que impide que esa garantía se pague
/// con una respuesta peor.
/// </para>
/// <para>
/// <b>Cada entrada lleva su motivo escrito</b>, y <c>CadaIndiceTraducidoSeJustificaTests</c> compara
/// esta lista con el modelo <b>en los dos sentidos</b>: un índice declarado que no existe —o que
/// dejó de ser único— pone el carril rojo, y así una traducción no sobrevive al índice que traducía.
/// </para>
/// </remarks>
public sealed class IndicesQueDelatanUnaCarreraPerdida
{
    private readonly Dictionary<string, string> _motivos = new(StringComparer.Ordinal);

    /// <summary>Los índices declarados, con su motivo, por nombre de índice.</summary>
    public IReadOnlyDictionary<string, string> Motivos => _motivos;

    /// <summary>Declara que la violación de este índice es una carrera perdida y no un defecto.</summary>
    /// <param name="indice">El nombre del índice <b>tal como lo crea la migración</b>.</param>
    /// <param name="motivo">Por qué su único desenlace posible es que otro llegó antes.</param>
    /// <returns>La misma lista, para encadenar.</returns>
    /// <remarks>
    /// El nombre es el del índice en PostgreSQL y no el de la propiedad, porque es lo único que
    /// trae la excepción del motor: <c>ConstraintName</c>. Si alguien renombra el índice en una
    /// migración y no toca esta línea, la traducción deja de aplicarse en silencio y la carrera
    /// vuelve a salir como <c>500</c> — de eso responde el barrido que compara las dos listas.
    /// </remarks>
    public IndicesQueDelatanUnaCarreraPerdida Declarar(string indice, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indice);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        _motivos[indice] = motivo;

        return this;
    }
}
