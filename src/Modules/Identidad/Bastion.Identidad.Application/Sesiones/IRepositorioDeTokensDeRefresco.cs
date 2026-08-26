using Bastion.Identidad.Domain.Sesiones;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>Acceso a los tokens de refresco emitidos.</summary>
public interface IRepositorioDeTokensDeRefresco
{
    /// <summary>La emisión cuyo resumen coincide, o nulo si no hay ninguna.</summary>
    /// <remarks>
    /// Se busca por el resumen porque es lo único que hay guardado: la fila no contiene el token.
    /// Devuelve también las <b>ya canjeadas</b>, y eso es esencial — encontrar una canjeada es
    /// justo la señal de reutilización que hay que detectar. Un repositorio que filtrara por
    /// «vigente» haría que un token robado y ya usado se viera igual que uno inventado, y con eso
    /// la detección desaparecería sin dejar rastro en ningún test.
    /// </remarks>
    /// <param name="hash">SHA-256 del token presentado, en hexadecimal.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<TokenDeRefresco?> ObtenerPorHashAsync(string hash, CancellationToken cancelacion);

    /// <summary>Todas las emisiones vivas de una familia.</summary>
    /// <param name="familiaId">Cadena de rotaciones.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlyList<TokenDeRefresco>> DeLaFamiliaAsync(Guid familiaId, CancellationToken cancelacion);

    /// <summary>Todas las emisiones vivas de un usuario.</summary>
    /// <param name="usuarioId">Usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlyList<TokenDeRefresco>> DelUsuarioAsync(Guid usuarioId, CancellationToken cancelacion);

    /// <summary>Apunta una emisión nueva.</summary>
    /// <param name="token">Emisión.</param>
    void Agregar(TokenDeRefresco token);
}
