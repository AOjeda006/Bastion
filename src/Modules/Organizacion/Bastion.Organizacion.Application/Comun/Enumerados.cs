namespace Bastion.Organizacion.Application.Comun;

/// <summary>
/// Lee un enumerado del dominio a partir del texto que llegó en el cuerpo de la petición.
/// </summary>
/// <remarks>
/// Los enumerados viajan como texto, así que en algún punto hay que traducirlos, y ese punto es
/// la capa de aplicación: es la primera que ve a la vez el contrato y el dominio.
/// </remarks>
internal static class Enumerados
{
    /// <summary>Intenta leer el valor; devuelve <see langword="false"/> si el texto no es uno.</summary>
    /// <typeparam name="T">El enumerado que se espera.</typeparam>
    /// <param name="valor">Texto recibido.</param>
    /// <param name="leido">El valor leído, si se pudo.</param>
    internal static bool Intentar<T>(string? valor, out T leido)
        where T : struct, Enum
    {
        // `IsDefined` ADEMÁS de `TryParse`, y no es redundante: TryParse acepta también el número
        // en texto —"7" se convierte sin rechistar en un valor que no existe— y acepta
        // combinaciones separadas por comas. Sin esta segunda comprobación entrarían al dominio
        // valores que ningún `switch` cubre.
        return Enum.TryParse(valor, ignoreCase: true, out leido) && Enum.IsDefined(leido);
    }

    /// <summary>Los nombres admitidos, para poder decírselos a quien se equivocó.</summary>
    /// <typeparam name="T">El enumerado del que se listan los valores.</typeparam>
    internal static string Admitidos<T>()
        where T : struct, Enum =>
        string.Join(", ", Enum.GetNames<T>());
}
