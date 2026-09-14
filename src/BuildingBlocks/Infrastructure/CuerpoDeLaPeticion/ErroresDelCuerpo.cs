using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Infrastructure.CuerpoDeLaPeticion;

/// <summary>Lo que puede salir mal al leer el cuerpo de una petición, con su código publicado.</summary>
/// <remarks>
/// Vive en <c>Infrastructure</c> y no con los errores de los casos de uso porque ningún caso de uso
/// lo puede emitir: cuando uno recibe su entrada, el cuerpo ya se ha leído y el tope ya se ha
/// impuesto. Quien lo emite es el borde.
/// </remarks>
public static class ErroresDelCuerpo
{
    /// <summary>Código estable del <c>413</c> por un cuerpo mayor que el tope de la acción.</summary>
    public const string CodigoDeDemasiadoGrande = "cuerpo-demasiado-grande";

    /// <summary>El cuerpo supera el tope que declara la acción.</summary>
    /// <param name="tope">El tope, en bytes, para que el mensaje lo diga.</param>
    public static ErrorDeOperacion DemasiadoGrande(long tope) => ErrorDeOperacion.DemasiadoGrande(
        CodigoDeDemasiadoGrande,
        $"El contenido supera los {tope} bytes que admite esta operación. Pártalo en varias " +
        "peticiones más pequeñas y mande cada una por separado.");
}
