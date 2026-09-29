using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Comun;

/// <summary>
/// Lee la trazabilidad que llega por la API y la convierte en la del dominio.
/// </summary>
/// <remarks>
/// Por lo mismo que <see cref="TiposDeArticulo"/>, y con su misma comparación: solo por los
/// nombres, en ordinal. <c>Enum.TryParse</c> dejaría entrar un <c>"1"</c> como <c>PorLote</c>.
/// </remarks>
internal static class Trazabilidades
{
    /// <summary>Los nombres admitidos, en el orden en que se cuentan al rechazar.</summary>
    internal static string Admitidas => string.Join(", ", Enum.GetNames<Trazabilidad>());

    /// <summary>
    /// Convierte el texto en una <see cref="Trazabilidad"/>, o devuelve nulo si no es ninguna.
    /// </summary>
    /// <param name="texto">La trazabilidad tal como llegó en el cuerpo.</param>
    internal static Trazabilidad? Leer(string? texto) =>
        Array.Exists(
            Enum.GetNames<Trazabilidad>(),
            nombre => string.Equals(nombre, texto, StringComparison.Ordinal))
            ? Enum.Parse<Trazabilidad>(texto!)
            : null;

    /// <summary>
    /// Lee la trazabilidad y la comprueba contra el tipo, que es lo que el dominio exige y la API
    /// tiene que contestar con su código en vez de con un <c>500</c>.
    /// </summary>
    /// <param name="tipo">El tipo que lleva la misma petición, ya leído.</param>
    /// <param name="texto">La trazabilidad tal como llegó en el cuerpo.</param>
    internal static Resultado<Trazabilidad> ParaElTipo(TipoDeArticulo tipo, string? texto)
    {
        if (Leer(texto) is not { } trazabilidad)
        {
            return Resultado.Fallo<Trazabilidad>(ErroresDeArticulo.TrazabilidadNoValida(Admitidas));
        }

        return tipo == TipoDeArticulo.Servicio && trazabilidad != Trazabilidad.Ninguna
            ? Resultado.Fallo<Trazabilidad>(ErroresDeArticulo.ServicioConTrazabilidad())
            : Resultado.Correcto(trazabilidad);
    }
}
