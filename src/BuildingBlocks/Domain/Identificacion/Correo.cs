using System.Diagnostics.CodeAnalysis;

namespace Bastion.BuildingBlocks.Domain.Identificacion;

/// <summary>
/// Una dirección de correo electrónico, normalizada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que comprueba y lo que no.</b> Comprueba la forma: una arroba, parte local no vacía,
/// dominio con al menos un punto y sin puntos en los extremos, sin espacios, y el límite de 254
/// posiciones de la RFC 5321 —que es el que acaba siendo la longitud de la columna, y una columna
/// corta trunca en silencio—. <b>No</b> comprueba que el buzón exista: eso solo lo demuestra
/// mandarle un mensaje, y es cosa del módulo de Notificaciones, no de un objeto de valor. Una
/// expresión regular que persiga la RFC 5322 entera rechaza direcciones válidas y acepta basura;
/// la línea está donde el fallo se nota.
/// </para>
/// <para>
/// <b>La normalización no es cosmética: decide identidad.</b> El correo identifica al usuario, así
/// que si <c>Ana@ejemplo.es</c> y <c>ana@ejemplo.es</c> no se normalizan a lo mismo, son dos
/// cuentas distintas y el índice único no lo impide. Se recorta y se pasa a minúsculas —el dominio
/// no distingue mayúsculas por norma, y la parte local técnicamente sí, pero ningún proveedor real
/// lo aprovecha y tratarlas como distintas solo produce cuentas duplicadas de la misma persona—.
/// </para>
/// <para>
/// Vive en el bloque común, como <see cref="Nif"/>: lo van a necesitar Identidad, Terceros,
/// RRHH y Notificaciones.
/// </para>
/// </remarks>
public sealed record Correo
{
    /// <summary>Longitud máxima de la RFC 5321. No es una estimación.</summary>
    public const int Longitud = 254;

    private Correo(string valor) => Valor = valor;

    /// <summary>La dirección normalizada: recortada y en minúsculas.</summary>
    public string Valor { get; }

    /// <summary>Construye el correo, o lanza si no tiene forma de dirección.</summary>
    /// <param name="valor">Dirección, tal como se haya escrito.</param>
    public static Correo De(string valor)
    {
        if (!Intentar(valor, out Correo? correo))
        {
            throw new ArgumentException($"«{valor}» no tiene forma de dirección de correo.", nameof(valor));
        }

        return correo;
    }

    /// <summary>Intenta construir el correo sin lanzar.</summary>
    /// <param name="valor">Dirección, tal como se haya escrito.</param>
    /// <param name="correo">El correo normalizado, si el texto era válido.</param>
    public static bool Intentar(string? valor, [NotNullWhen(true)] out Correo? correo)
    {
        correo = null;

        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        string normalizado = valor.Trim().ToLowerInvariant();

        if (normalizado.Length > Longitud || normalizado.AsSpan().ContainsAny(' ', '\t'))
        {
            return false;
        }

        string[] partes = normalizado.Split('@');

        if (partes.Length != 2 || partes[0].Length == 0)
        {
            return false;
        }

        string dominio = partes[1];

        if (dominio.Length == 0 || !dominio.Contains('.', StringComparison.Ordinal)
            || dominio.StartsWith('.') || dominio.EndsWith('.'))
        {
            return false;
        }

        correo = new Correo(normalizado);
        return true;
    }

    /// <summary>La dirección, para cuando hace falta como texto.</summary>
    public override string ToString() => Valor;
}
