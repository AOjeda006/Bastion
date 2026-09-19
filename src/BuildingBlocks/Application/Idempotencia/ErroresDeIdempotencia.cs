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
/// <b>Los cuatro son del cliente</b> —dice algo que no cuadra con lo que ya dijo, pide un servicio
/// donde no se presta, o no lo pide donde es obligatorio—, así que ninguno es <c>5xx</c> y todos
/// llevan en el mensaje qué hacer.
/// </para>
/// <para>
/// <b>El último mira al revés que los otros tres</b>, y entró en el ítem 2.4: tres se quejan de una
/// cabecera que sobra o que no vale, y <see cref="Obligatoria"/> se queja de que falte. Es la
/// excepción a la doctrina del 0.9 —«la clave es una garantía que el cliente <i>pide</i>, no un
/// peaje que se le cobra»— y está acotada donde vale: las operaciones que gastan algo que no se
/// puede devolver.
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

    /// <summary>Código estable del <c>428</c> en una ruta que exige la cabecera.</summary>
    public const string CodigoDeObligatoria = "idempotencia-obligatoria";

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

    /// <summary>La ruta exige la cabecera y la petición no la trae.</summary>
    /// <remarks>
    /// <para>
    /// <b><c>428</c> y no <c>400</c></b>, por lo mismo que el <c>428</c> del <c>If-Match</c>: la
    /// petición está bien formada y lo que falta es una precondición. El cliente lo arregla solo
    /// —genera una clave y repite— y un <c>400</c> le mandaría a revisar un cuerpo impecable.
    /// </para>
    /// <para>
    /// <b>Lo que hay detrás no es celo, es que sin la cabecera el endpoint no puede cumplir lo que
    /// promete.</b> La transacción de estas operaciones tiene un solo dueño —el filtro de
    /// idempotencia, ADR-0014— y sin clave el filtro se aparta en su primera línea sin abrir
    /// ninguna. Una confirmación que numera se quedaría con el contador subido por su cuenta y el
    /// documento sin guardar: un hueco en la serie, que es lo que la R5 prohibe.
    /// </para>
    /// </remarks>
    /// <param name="metodo">Método de la petición, para que el mensaje diga cuál era.</param>
    /// <param name="ruta">Ruta de la petición.</param>
    public static ErrorDeOperacion Obligatoria(string metodo, string ruta) =>
        ErrorDeOperacion.FaltaLaPrecondicion(
            CodigoDeObligatoria,
            $"La operación {metodo} {ruta} exige la cabecera Idempotency-Key. Genere una clave —lo " +
            "habitual es un UUID— antes del primer intento y mande la misma en cada reintento: es " +
            "lo que hace que repetir la petición no repita su efecto.");
}
