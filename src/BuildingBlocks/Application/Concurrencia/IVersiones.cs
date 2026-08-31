namespace Bastion.BuildingBlocks.Application.Concurrencia;

/// <summary>
/// El puerto por el que un caso de uso lee la versión de lo que ha cargado y exige que siga
/// siendo esa al guardar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ningún caso de uso pide ESTE tipo</b>, por el mismo motivo que con <c>IUnidadTrabajo</c>:
/// el contenedor resuelve por tipo y la última inscripción gana. Cada módulo declara el suyo
/// —<c>IVersionesDeOrganizacion</c>, <c>IVersionesDeIdentidad</c>— y es ese el que se inyecta,
/// porque la versión sale del contexto que rastrea la entidad y de ningún otro.
/// </para>
/// <para>
/// <b>Por qué no lo comprueba el caso de uso.</b> Podría leer la versión, compararla con la que
/// trae el cliente y decidir; y entre la comparación y el guardado cabe otra escritura, así que
/// la comprobación diría que sí justo cuando ya no. Lo que hace <see cref="Exigir"/> es meter la
/// versión en el <c>WHERE</c> del <c>UPDATE</c>: la comparación y la escritura pasan a ser la
/// misma sentencia y no queda hueco donde colarse.
/// </para>
/// </remarks>
public interface IVersiones
{
    /// <summary>La versión con la que se cargó una entidad.</summary>
    /// <param name="entidad">La entidad, tal como la devolvió el repositorio.</param>
    /// <returns>Su versión, la que se publica como <c>ETag</c>.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Si el contexto no rastrea la entidad. Leer el testigo de una entidad traída con
    /// <c>AsNoTracking()</c> devuelve cero sin avisar, y un cero NO puede acabar en un
    /// <c>ETag</c>.
    /// </exception>
    VersionDeRecurso De(object entidad);

    /// <summary>
    /// Exige que la entidad siga en esa versión cuando se guarde; si no, el guardado falla y el
    /// borde responde <c>412</c>.
    /// </summary>
    /// <param name="entidad">La entidad cargada que se va a modificar.</param>
    /// <param name="version">La versión que el cliente dice tener.</param>
    void Exigir(object entidad, VersionDeRecurso version);
}
