namespace Bastion.BuildingBlocks.Infrastructure.Idempotencia;

/// <summary>
/// Declara que una acción admite la cabecera <c>Idempotency-Key</c> (R10).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una lista blanca, y se lee en la propia acción.</b> Lo que decide si una petición puede
/// repetirse sin repetir su efecto no es el método HTTP ni la ruta: es si guardar su respuesta
/// tiene sentido y es seguro. Escrito en un fichero de configuración lejos de la acción, el día que
/// alguien añada un endpoint nadie irá a mirarlo; escrito aquí, se ve al lado del
/// <c>[ExigePermiso]</c>, y el barrido de <c>Api.FunctionalTests</c> obliga a que toda acción que
/// cambia estado esté clasificada —con este atributo o en la lista de exentas con su motivo—.
/// </para>
/// <para>
/// <b>Sin este atributo, mandar la cabecera es un <c>400</c>, no un silencio.</b> Un cliente que la
/// manda cree que su reintento es seguro. Ignorarla le dejaría esa creencia sin nada detrás, que es
/// peor que no ofrecer el mecanismo: creería estar protegido justo mientras duplica un alta.
/// </para>
/// <para>
/// <b>Qué NO se marca, y por qué no es pereza.</b> Las escrituras que exigen <c>If-Match</c> ya
/// están protegidas del reintento por otra vía: el segundo intento lleva una versión que ya no es
/// la actual y se lo lleva un <c>412</c>. Las que son idempotentes por naturaleza —conceder una
/// pertenencia que ya está concedida, fijar una contraseña— dan el mismo resultado repetidas. Y las
/// de sesión no se marcan porque su respuesta lleva credenciales dentro, y esta tabla guarda
/// respuestas.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AdmiteIdempotenciaAttribute : Attribute;
