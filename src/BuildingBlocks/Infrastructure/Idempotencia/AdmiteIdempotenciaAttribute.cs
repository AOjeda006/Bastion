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
/// <para>
/// <b>Y desde el ítem 2.4 tiene un segundo grado: <see cref="Obligatoria"/>.</b> Admitirla es lo
/// normal; exigirla es la excepción, y hay que justificarla en la propia acción. El criterio no es
/// «esto es importante» —todo lo es—, sino que <b>sin la cabecera la acción no puede cumplir lo
/// que promete</b>: el filtro se aparta sin abrir transacción, y una operación cuya atomicidad
/// depende de esa transacción se quedaría a medias sin que nadie lo note.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AdmiteIdempotenciaAttribute : Attribute
{
    /// <summary>Si la acción <b>exige</b> la cabecera, y sin ella contesta <c>428</c>.</summary>
    /// <remarks>
    /// Por omisión es <c>false</c>, que es la doctrina del ítem 0.9: la clave es una garantía que el
    /// cliente <i>pide</i>, no un peaje que se le cobra. Ponerla a <c>true</c> invierte eso para una
    /// acción concreta y se acota donde vale: operaciones que gastan algo que no se puede devolver
    /// —un número de serie—, donde quedarse a medias no es un reintento perdido sino un hueco.
    /// </remarks>
    public bool Obligatoria { get; init; }
}
