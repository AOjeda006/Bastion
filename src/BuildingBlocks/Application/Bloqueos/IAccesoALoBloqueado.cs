namespace Bastion.BuildingBlocks.Application.Bloqueos;

/// <summary>
/// La puerta declarada por la que se ve lo bloqueado (R16), y el único sitio del que sale.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bloquear no es esconder en la pantalla.</b> El artículo 32 de la LOPDGDD obliga a impedir el
/// tratamiento de los datos bloqueados <b>incluida su visualización</b>, así que el filtro es de
/// repositorio y no de interfaz: un registro bloqueado no aparece en una consulta ordinaria porque
/// la consulta no lo trae, no porque la pantalla lo oculte.
/// </para>
/// <para>
/// <b>Y sin embargo hay que poder llegar a él.</b> Ese mismo artículo reserva el acceso a jueces,
/// Fiscalía y Administraciones competentes durante el plazo de prescripción; y, más cerca, para
/// <b>levantar</b> un bloqueo hay que poder leer lo que está bloqueado. La forma de resolverlo es
/// la misma que el 0.6 eligió para el inquilinato y por las mismas razones: un ámbito
/// <b>explícito</b>, con un motivo de una lista cerrada
/// (<see cref="MotivoParaVerLoBloqueado"/>) y anotado en el registro.
/// </para>
/// <para>
/// <b>Lo que NO es: un <c>IgnoreQueryFilters</c>.</b> Está prohibido desde el 0.6 y sigue
/// estándolo. Un <c>IgnoreQueryFilters</c> suelto donde hiciera falta apagaría de paso el filtro
/// de empresa —son el mismo mecanismo—, no dejaría rastro de quién decidió mirar, y convertiría
/// «se ve lo bloqueado aquí, por esto» en una decisión que se toma línea a línea. Un camino nuevo
/// que quiera ver lo bloqueado y no esté en la lista pone <c>ElFiltroNoSeSaltaPorAhiTests</c> en
/// rojo.
/// </para>
/// </remarks>
public interface IAccesoALoBloqueado
{
    /// <summary>
    /// Si <b>esta</b> consulta puede ver lo bloqueado, porque hay un ámbito abierto a propósito.
    /// </summary>
    /// <remarks>
    /// Es un <b>miembro de instancia que se lee en cada consulta</b>, por lo mismo que
    /// <c>EmpresaDelFiltro</c>: el modelo de EF Core se cachea, y un valor copiado al construir el
    /// contexto se quedaría con el del primero que lo construyó.
    /// </remarks>
    bool Abierto { get; }

    /// <summary>El motivo del ámbito abierto ahora mismo, o <c>null</c> si no hay ninguno.</summary>
    MotivoParaVerLoBloqueado? MotivoDelAmbito { get; }

    /// <summary>Abre un ámbito en el que las consultas <b>sí</b> ven lo bloqueado.</summary>
    /// <remarks>
    /// El ámbito vale para el flujo asíncrono en curso y se cierra al desechar el resultado.
    /// Anidar dos está permitido y el de dentro manda; al cerrarse, se recupera el de fuera.
    /// </remarks>
    /// <param name="motivo">Por qué esta operación mira lo bloqueado. Va al registro.</param>
    /// <returns>El ámbito. Desecharlo lo cierra.</returns>
    IDisposable ViendoLoBloqueado(MotivoParaVerLoBloqueado motivo);
}

/// <summary>
/// Los motivos por los que una operación puede ver datos bloqueados.
/// </summary>
/// <remarks>
/// Lista cerrada, igual que <c>MotivoSinInquilino</c>: añadir un motivo obliga a tocar este
/// enumerado, que es un cambio que se ve en la revisión. Un <c>string</c> dejaría abrir la puerta
/// con «temporal».
/// </remarks>
public enum MotivoParaVerLoBloqueado
{
    /// <summary>
    /// Levantar un bloqueo. Es el único camino ordinario que necesita ver lo bloqueado, y la
    /// razón es de lógica: para desbloquear algo hay que poder leerlo primero, y lo que se va a
    /// leer está —por definición— bloqueado.
    /// </summary>
    /// <remarks>
    /// <b>Este motivo NO es el del artículo 32</b>, y la diferencia se ve en quién lo abre: lo
    /// abren los cuatro desbloqueos, y lo abren por una necesidad mecánica —leer la fila que van a
    /// escribir— y no por un derecho de acceso. Quien desbloquea no está consultando datos
    /// reservados: está levantando la reserva.
    /// </remarks>
    AdministracionDelBloqueo,

    /// <summary>
    /// La vía de acceso que el artículo 32 de la LOPDGDD reserva a jueces, Fiscalía y
    /// Administraciones competentes durante el plazo de prescripción: separada de la consulta
    /// ordinaria, nominativa y trazada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hasta el ítem 1.4 este valor no existía, y su ausencia estaba escrita aquí</b> con estas
    /// palabras: «no existe todavía el camino que lo serviría. Cuando exista traerá el suyo, y
    /// traerlo antes sería una rama que nadie recorre y que nadie prueba». El camino existe desde el
    /// 1.4 —el listado de lo bloqueado, con su permiso propio— así que el valor entra ahora y no
    /// antes, que es justo lo que aquella frase pedía.
    /// </para>
    /// <para>
    /// <b>Va aparte de <see cref="AdministracionDelBloqueo"/> y no reutiliza el suyo</b>, aunque
    /// abrir el ámbito haga lo mismo en los dos casos. Lo que se anota en el registro es el motivo,
    /// y el registro es la traza que el artículo 32 exige: con un solo valor, la consulta de un juez
    /// y el clic de un administrador que desbloquea un almacén dejan la misma línea, y la pregunta
    /// «quién ha mirado datos reservados y cuándo» deja de tener respuesta. Son dos hechos
    /// jurídicamente distintos y tienen que ser dos líneas distintas.
    /// </para>
    /// </remarks>
    AccesoReservadoDelArticulo32,

    /// <summary>
    /// Comprobar que un identificador fiscal no lo tiene ya nadie en esa empresa, antes de dar de
    /// alta a un tercero. Mira lo bloqueado porque la unicidad lo abarca.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Entra en el ítem 1.5 porque es cuando aparece el primer camino que lo necesita</b>, que
    /// es la regla de esta lista desde que se escribió. Y lo necesita por una decisión tomada a
    /// propósito: la unicidad de (empresa, identificador) <b>abarca también las fichas
    /// bloqueadas</b>, así que el alta tiene que poder ver una ficha bloqueada para saber que
    /// choca con ella. Con una unicidad parcial no haría falta este motivo —y a cambio el
    /// desbloqueo tendría que resolver una colisión que hoy no puede existir—.
    /// </para>
    /// <para>
    /// <b>No reutiliza <see cref="AdministracionDelBloqueo"/></b> por lo mismo que ese no reutiliza
    /// el del artículo 32: lo que el registro anota es el motivo, y con un solo valor «alguien ha
    /// dado de alta un cliente» y «alguien ha levantado una baja» dejarían la misma línea. Y son
    /// hechos de distinto peso: por esta puerta se mira mucho —cada alta— y no se escribe nada
    /// sobre lo bloqueado; por la otra se mira poco y se levanta una reserva.
    /// </para>
    /// <para>
    /// <b>Lo que se ve por aquí no sale por la respuesta</b>, y eso no lo garantiza este
    /// enumerado: lo garantiza la forma del puerto. <c>ExisteLaIdentificacionAsync</c> devuelve un
    /// booleano, así que el caso de uso que abre este ámbito no llega a saber si lo que estorba
    /// estaba activo o bloqueado. El ámbito sirve para que la pregunta sea correcta, no para
    /// traerse los datos reservados a la capa de arriba.
    /// </para>
    /// </remarks>
    ComprobacionDeUnicidadDeIdentificador,
}
