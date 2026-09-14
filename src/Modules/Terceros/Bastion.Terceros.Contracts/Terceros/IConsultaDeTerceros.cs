namespace Bastion.Terceros.Contracts.Terceros;

/// <summary>En qué papel se le pregunta a un tercero.</summary>
/// <remarks>
/// Es un parámetro y no dos métodos porque el estado <b>depende del papel por el que se pregunta</b>:
/// el mismo tercero está disponible para comprarle y no para venderle. Con un método por papel, la
/// respuesta «no hace ese papel» necesitaría un valor distinto en cada enumerado, y serían dos
/// listas casi iguales que habría que mantener a la par.
/// </remarks>
public enum RolDeTercero
{
    /// <summary>Se le vende.</summary>
    Cliente = 0,

    /// <summary>Se le compra.</summary>
    Proveedor = 1,
}

/// <summary>
/// Qué se puede hacer hoy con un tercero al que otro módulo quiere apuntar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Son DOS preguntas y no una</b>, y por eso no es un <c>bool</c>: ¿hay en esta empresa una ficha
/// que se pueda tratar con ese identificador?, y ¿hace el papel por el que se pregunta? Un puerto
/// que solo contestara la primera dejaría colgar un suministro de un cliente que nunca ha vendido
/// nada.
/// </para>
/// <para>
/// <b>«Bloqueado» NO es un valor, y llegó a serlo.</b> Se diseñó con cuatro —<c>Bloqueado</c> entre
/// <c>NoExiste</c> y <c>NoHaceEseRol</c>— y la matriz de puerto × estado lo encontró inalcanzable
/// en el mismo ítem 1.10, antes de salir de la rama: la consulta pasa por el filtro de repositorio
/// del art. 32, la ficha bloqueada no llega al <c>switch</c>, y el puerto contestaba
/// <c>NoExiste</c>. Hacerlo alcanzable exigía abrir en el adaptador un ámbito de
/// <c>IAccesoALoBloqueado</c> con un motivo nuevo: mirar datos reservados en cada alta de proveedor
/// para producir una diferencia que quien pregunta junta con las demás, y que TIENE que juntar por
/// lo que dice <see cref="NoExiste"/>. La lista cerrada de motivos admite uno cuando un camino
/// necesita VER lo bloqueado, y este no lo necesita. Lo que sobraba era el valor.
/// </para>
/// <para>
/// <b>Y al quitarlo, el orden deja de ser una promesa.</b> Con cuatro valores, que de un bloqueado
/// no saliera qué papeles hace dependía de que el <c>switch</c> mirase el bloqueo antes que el
/// papel; ahora no sale nada porque la fila no llega, y no queda un orden que alguien pueda
/// invertir.
/// </para>
/// <para>
/// <b>Quien pregunta no publica la diferencia que queda.</b> Estos valores son para que la regla
/// decida, no para que la respuesta HTTP los cuente: el alta de un proveedor contesta lo mismo a
/// los dos que no autorizan, por el mismo motivo por el que <c>tercero-duplicado</c> es uno solo
/// para el activo y para el bloqueado (ítem 1.5).
/// </para>
/// <para>
/// <c>NoExiste</c> es el valor cero a propósito, como en <c>EstadoDeMaestro</c>: si alguien recibe
/// un <c>default</c> por un camino que no pasó por el puerto, lo que se encuentra es la respuesta
/// que no autoriza nada.
/// </para>
/// </remarks>
public enum EstadoDelTercero
{
    /// <summary>
    /// No hay en esta empresa ninguna ficha que se pueda tratar con ese identificador.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Caben aquí tres situaciones, y caben a propósito:</b> no hay ficha con ese identificador;
    /// la hay, pero en otra empresa de la instalación (filtro de inquilinato, R8); o la hay y está
    /// bloqueada (filtro del art. 32, R16). Desde fuera de Terceros las tres son lo mismo y tienen
    /// que serlo: distinguir la segunda diría que ese identificador es de alguien, y la tercera,
    /// que alguien tiene sus datos reservados.
    /// </para>
    /// <para>
    /// Lo que ya colgaba de una ficha que se bloquea se queda —reservar no es borrar—; lo que no
    /// se puede es colgarle nada nuevo.
    /// </para>
    /// </remarks>
    NoExiste = 0,

    /// <summary>Se puede tratar, pero no hace el papel por el que se pregunta.</summary>
    NoHaceEseRol = 1,

    /// <summary>Se puede tratar y hace ese papel.</summary>
    Disponible = 2,
}

/// <summary>
/// Lo que otros módulos pueden preguntar sobre los terceros.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>terceros.terceros</c> ni una llamada HTTP.
/// </para>
/// <para>
/// <b>Es la mitad de ida del primer cruce MUTUO del proyecto.</b> La otra es
/// <c>IConsultaDeTarifas</c>, en <c>Catalogo.Contracts</c>. Que los dos <c>Contracts</c> no se vean
/// entre sí es lo que impide que la mutua sea un ciclo: aquí no entra ni un tipo de Catálogo, y
/// allí no entra ni uno de Terceros. Por eso lo que cruza es <c>Guid</c> y primitivos.
/// </para>
/// <para>
/// <b>No publica nombre, ni identificación fiscal, ni domicilio</b>, y esa ausencia es la decisión:
/// un puerto que devolviera la ficha convertiría cualquier módulo en un lector de datos personales
/// sin permiso ni traza. Lo que hace falta para validar un identificador es un estado, y un estado
/// es lo que sale.
/// </para>
/// </remarks>
public interface IConsultaDeTerceros
{
    /// <summary>En qué estado está ese tercero para ese papel.</summary>
    /// <param name="terceroId">Identificador del tercero.</param>
    /// <param name="rol">Papel por el que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDelTercero> EstadoDeAsync(
        Guid terceroId, RolDeTercero rol, CancellationToken cancelacion);

    /// <summary>
    /// De esos identificadores, cuáles se pueden tratar hoy: ni inexistentes ni bloqueados.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es lo que hace posible el filtro del art. 32 en una LECTURA de otro módulo.</b> Un
    /// listado que enseñe filas apuntando a terceros no puede filtrar por el bloqueo con un
    /// <c>WHERE</c>: el bloqueo está en <c>terceros.terceros</c>, en otro esquema, y ninguna
    /// consulta cruza esquemas (§5, regla 4). La única vía es preguntar, y preguntar una vez por
    /// fila sería una consulta por fila; de ahí que reciba el conjunto.
    /// </para>
    /// <para>
    /// <b>Devuelve los que SÍ, y no los que no, y la diferencia es de seguridad.</b> Quien llama
    /// se queda con la intersección, así que un fallo que devolviera menos de la cuenta —o vacío—
    /// enseña de menos. Al revés —devolver los bloqueados y restarlos— el mismo fallo los
    /// enseñaría todos, que es el modo en que un filtro de protección de datos se cae sin ruido.
    /// </para>
    /// </remarks>
    /// <param name="terceroIds">Los identificadores por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlySet<Guid>> CualesSePuedenTratarAsync(
        IReadOnlyCollection<Guid> terceroIds, CancellationToken cancelacion);
}
