using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Idempotencia;

/// <summary>
/// Lo que puede salir mal al presentar una <c>Idempotency-Key</c>, con sus códigos, que son
/// contrato publicado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ninguno de estos es un choque de concurrencia</b>, y por eso viven aparte de
/// <c>ErroresDeConcurrencia</c>. La cabecera <c>If-Match</c> protege de que dos personas pisen el
/// mismo recurso; la <c>Idempotency-Key</c> protege de que <b>una</b> repita su propia petición.
/// Son dos mecanismos distintos que el ítem 0.9 junta en un criterio, no una cosa con dos nombres.
/// </para>
/// <para>
/// <b>Los tres son del cliente</b> —dice algo que no cuadra con lo que ya dijo, o pide un servicio
/// donde no se presta—, así que ninguno es <c>5xx</c> y todos llevan en el mensaje qué hacer.
/// </para>
/// </remarks>
public static class ErroresDeIdempotencia
{
    /// <summary>Código estable del <c>409</c> por reusar una clave con otro cuerpo.</summary>
    public const string CodigoDeCuerpoDistinto = "idempotencia-cuerpo-distinto";

    /// <summary>Código estable del <c>400</c> en una ruta que no admite la cabecera.</summary>
    public const string CodigoDeNoAdmitida = "idempotencia-no-admitida";

    /// <summary>Código estable del <c>400</c> por una clave ilegible.</summary>
    public const string CodigoDeClaveNoValida = "idempotencia-clave-no-valida";

    /// <summary>Código estable del <c>400</c> cuando no hay con qué formar la identidad.</summary>
    public const string CodigoDeSinEmpresaActiva = "idempotencia-sin-empresa-activa";

    /// <summary>La misma clave, otro cuerpo.</summary>
    /// <remarks>
    /// <b>409 y no 400</b>: la petición está bien formada; lo que falla es que contradice a una
    /// anterior. Devolver la respuesta guardada sería peor que cualquier error, porque el cliente
    /// leería el desenlace de una operación <b>que no es la que acaba de pedir</b>.
    /// </remarks>
    public static ErrorDeOperacion CuerpoDistinto() => ErrorDeOperacion.Conflicto(
        CodigoDeCuerpoDistinto,
        "Esta Idempotency-Key ya se usó para una petición con otro contenido. Use una clave nueva " +
        "si la operación es otra, o repita exactamente la anterior si lo que quiere es reintentarla.");

    /// <summary>La ruta no admite la cabecera.</summary>
    /// <remarks>
    /// <b>Se responde y no se ignora.</b> Tragarse la cabecera dejaría al cliente creyendo que su
    /// reintento es seguro cuando no lo es, que es exactamente la situación que el mecanismo viene
    /// a evitar. Más vale un error visible que una garantía imaginaria.
    /// </remarks>
    /// <param name="metodo">Método de la petición, para que el mensaje diga cuál era.</param>
    /// <param name="ruta">Ruta de la petición.</param>
    public static ErrorDeOperacion NoAdmitida(string metodo, string ruta) => ErrorDeOperacion.Validacion(
        CodigoDeNoAdmitida,
        $"La operación {metodo} {ruta} no admite la cabecera Idempotency-Key. Quítela: repetirla " +
        "no está protegido por este mecanismo, y dejar la cabecera puesta haría creer que sí.");

    /// <summary>La cabecera viene, pero no sirve como clave.</summary>
    /// <param name="maximo">Longitud máxima admitida, para que el mensaje la diga.</param>
    public static ErrorDeOperacion ClaveNoValida(int maximo) => ErrorDeOperacion.Validacion(
        CodigoDeClaveNoValida,
        $"La Idempotency-Key no es válida: tiene que traer texto y no pasar de {maximo} caracteres. " +
        "Lo habitual es un UUID generado por el cliente antes del primer intento.");

    /// <summary>No hay empresa activa en el <i>claim</i>, así que no hay identidad que formar.</summary>
    /// <remarks>
    /// La identidad de una clave es la tupla entera —empresa, usuario, método, ruta y clave—. Sin
    /// empresa faltaría un miembro, y las dos salidas son peores que este error: inventarse un
    /// <c>Guid.Empty</c> mete un valor falso en una columna de verdad, y dejar la empresa fuera de
    /// la clave haría que dos inquilinos con la misma clave se pisaran la respuesta.
    /// </remarks>
    public static ErrorDeOperacion SinEmpresaActiva() => ErrorDeOperacion.Validacion(
        CodigoDeSinEmpresaActiva,
        "No se puede aplicar la Idempotency-Key sin una empresa activa en la sesión. Entre en una " +
        "empresa y repita la petición.");
}
