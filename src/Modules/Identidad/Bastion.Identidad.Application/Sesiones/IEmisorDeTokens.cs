namespace Bastion.Identidad.Application.Sesiones;

/// <summary>
/// Emite los dos tokens de una sesión: el de acceso, que se firma, y el de refresco, que es
/// aleatorio.
/// </summary>
/// <remarks>
/// <para>
/// Son dos cosas distintas a propósito. El de <b>acceso</b> es un JWT firmado que lleva dentro
/// quién es, en qué empresa opera y qué permisos tiene, y se valida sin tocar la base de datos:
/// por eso dura poco —quince minutos (§11)—, porque revocarlo antes de tiempo no se puede. El de
/// <b>refresco</b> no lleva nada dentro: es aleatoriedad, y todo lo que significa está en su fila,
/// que sí se puede revocar.
/// </para>
/// <para>
/// La clave de firma sale de la variable de entorno <c>JWT_SIGNING_KEY</c>. Ni un secreto en
/// fichero ni en prosa; si falta, la aplicación <b>no arranca</b>.
/// </para>
/// </remarks>
public interface IEmisorDeTokens
{
    /// <summary>Emite el token de acceso.</summary>
    /// <param name="usuarioId">Quién.</param>
    /// <param name="nombre">Su nombre, para que la interfaz no necesite otra consulta.</param>
    /// <param name="empresaId">Empresa activa (R8).</param>
    /// <param name="permisos">Permisos en esa empresa.</param>
    TokenDeAcceso EmitirAcceso(
        Guid usuarioId,
        string nombre,
        Guid empresaId,
        IReadOnlyList<string> permisos);

    /// <summary>
    /// Genera un token de refresco: lo que se entrega y el resumen que se guarda.
    /// </summary>
    /// <remarks>
    /// Devuelve las dos mitades juntas porque solo aquí existen a la vez. Quien lo llama entrega
    /// una y guarda la otra; no hay ningún sitio donde el token entregado se pueda recuperar.
    /// </remarks>
    RefrescoGenerado GenerarRefresco();

    /// <summary>
    /// Calcula el resumen de un token de refresco presentado, para poder buscar su fila.
    /// </summary>
    /// <remarks>
    /// Es el mismo cálculo que hace <see cref="GenerarRefresco"/>, y por eso está aquí y no
    /// repetido en el caso de uso: si la renovación hiciera su propio SHA-256, el día que el
    /// resumen cambiara de forma —o de codificación, que basta con pasar de minúsculas a
    /// mayúsculas— la búsqueda dejaría de encontrar nada y todas las renovaciones fallarían con
    /// el error genérico, que es justo el que no explica nada.
    /// </remarks>
    /// <param name="valor">El token tal como lo presenta el cliente.</param>
    string HashearRefresco(string valor);

    /// <summary>Cuánto vale un token de refresco.</summary>
    TimeSpan DuracionDelRefresco { get; }
}

/// <summary>Un token de acceso recién emitido.</summary>
/// <param name="Valor">El JWT.</param>
/// <param name="ExpiraEn">Cuándo deja de valer.</param>
public readonly record struct TokenDeAcceso(string Valor, DateTimeOffset ExpiraEn);

/// <summary>Las dos mitades de un token de refresco.</summary>
/// <param name="Valor">Lo que se entrega al cliente, en la cookie. No se guarda.</param>
/// <param name="Hash">SHA-256 del valor, en hexadecimal. Es lo que se guarda.</param>
public readonly record struct RefrescoGenerado(string Valor, string Hash);
