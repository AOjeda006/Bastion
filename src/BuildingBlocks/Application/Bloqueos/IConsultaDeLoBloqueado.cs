using Bastion.BuildingBlocks.Domain.Bloqueos;

namespace Bastion.BuildingBlocks.Application.Bloqueos;

/// <summary>Qué clase de recurso está bloqueado.</summary>
/// <remarks>
/// <para>
/// <b>Un enumerado y no el nombre del tipo por reflexión.</b> El nombre de una clase es un detalle
/// interno que se renombra en una refactorización, y aquí acaba publicado en el contrato de la API:
/// renombrar <c>Almacen</c> cambiaría lo que lee un cliente ya desplegado sin que nada fallara.
/// Escrito aquí, el nombre externo es una decisión y no una consecuencia.
/// </para>
/// <para>
/// <b>Vive en el bloque común desde el ítem 1.6, y ese traslado es el arreglo.</b> Estaba en
/// <c>Organizacion.Application</c>, y una obligación transversal declarada dentro de un módulo no
/// alcanza a los demás por construcción: nacieron faltando <c>Usuario</c> —desde el 1.4— y
/// <c>Tercero</c> —desde el 1.5—, que son justamente los dos casos más nítidos del artículo,
/// porque un usuario es una persona física. Lo comprueba
/// <c>LoBloqueadoSeVeEnteroTests</c>, comparando esta lista entera contra los agregados que
/// implementan <see cref="IBloqueable"/>, en las dos direcciones.
/// </para>
/// </remarks>
public enum TipoDeRecursoBloqueado
{
    /// <summary>Un almacén.</summary>
    Almacen,

    /// <summary>Una empresa de la instalación.</summary>
    Empresa,

    /// <summary>Un tercero: cliente, proveedor o las dos cosas.</summary>
    Tercero,

    /// <summary>Una ubicación dentro de un almacén.</summary>
    Ubicacion,

    /// <summary>Una cuenta de usuario. Es una persona física.</summary>
    Usuario,
}

/// <summary>
/// Una fila bloqueada, con lo justo para reconocerla y para levantar su bloqueo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un modelo de lectura y no las entidades.</b> El listado une agregados de tres módulos en
/// una sola página, y devolver las entidades obligaría a un contrato con cinco formas —o a traerlas
/// enteras para enseñar cuatro campos de cada una. Cada módulo lo proyecta en SU consulta: lo que
/// cruza el puerto es exactamente lo que se publica.
/// </para>
/// <para>
/// <b>Propiedades con <c>init</c> y no parámetros posicionales, y es una imposición de EF Core.</b>
/// Las consultas lo construyen en un inicializador de objeto porque un constructor con argumentos
/// deja el árbol de expresión sin manera de resolver «ordéname por <c>BloqueadoEn</c>»: EF Core
/// sabe atravesar las asignaciones de un inicializador y no sabe atravesar un constructor, así que
/// con la forma posicional el <c>?sort=</c> compila y revienta en ejecución.
/// </para>
/// <para>
/// <b>Lo que NO trae es tan deliberado como lo que trae.</b> No hay testigo de concurrencia
/// —ninguna lectura de lo bloqueado emite versión, y de eso dependen cuatro exenciones de
/// <c>If-Match</c> (ADR-0017, ADR-0027)— y no hay más datos del recurso que los necesarios para
/// reconocerlo: el art. 32 reserva estos datos, y enseñar de más por comodidad es tratarlos.
/// </para>
/// </remarks>
public sealed record RecursoBloqueado
{
    /// <summary>Identificador del recurso. Es lo que pide su desbloqueo.</summary>
    public Guid Id { get; init; }

    /// <summary>Qué clase de recurso es.</summary>
    public TipoDeRecursoBloqueado Tipo { get; init; }

    /// <summary>Su código, o nulo si su tipo no tiene.</summary>
    public string? Codigo { get; init; }

    /// <summary>Con qué nombre se le reconoce.</summary>
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Cuándo se bloqueó.</summary>
    public DateTimeOffset BloqueadoEn { get; init; }

    /// <summary>Por qué se bloqueó.</summary>
    public MotivoDeBloqueo Motivo { get; init; }
}

/// <summary>Qué se le pide a cada módulo para poder componer una página del art. 32.</summary>
/// <param name="Filtro">El <c>?q=</c>, o nulo. Cada módulo lo aplica en SU base.</param>
/// <param name="Campo">Por qué campo se ordena, ya validado contra <see cref="ComposicionDeLoBloqueado.CamposOrdenables"/>.</param>
/// <param name="Descendente">Si el orden va del mayor al menor.</param>
/// <param name="Cuantos">
/// Cuántas filas como mucho. Es <c>salto + tamaño</c>: la cota de una fusión k-vías, explicada en
/// <see cref="ComposicionDeLoBloqueado"/>.
/// </param>
public sealed record CriterioDeLoBloqueado(string? Filtro, string Campo, bool Descendente, int Cuantos);

/// <summary>Lo que un módulo contesta: sus primeras filas en ese orden, y cuántas tiene en total.</summary>
/// <param name="Primeros">Las <c>Cuantos</c> primeras según el criterio, ya ordenadas.</param>
/// <param name="Total">Cuántas filas suyas casan con el filtro. La suma de los totales es el total.</param>
public sealed record LoBloqueadoDeUnModulo(IReadOnlyList<RecursoBloqueado> Primeros, long Total);

/// <summary>
/// Lo que un módulo que bloquea tiene que saber contestar: <b>qué tengo bloqueado</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el patrón de puertos del ítem 1.2 con la flecha al revés.</b> Allí un módulo dueño publica
/// una lectura y varios preguntan (<c>IConsultaDeImpuestos</c>); aquí hay <b>varios dueños y un
/// consumidor</b>, así que la interfaz no puede vivir en el <c>Contracts</c> de ninguno de ellos
/// —el consumidor tendría que conocerlos a todos, y añadir un módulo obligaría a tocar el
/// listado—. Vive en el bloque común, que es donde ya vive <see cref="IBloqueable"/>, y el
/// consumidor resuelve <c>IEnumerable&lt;IConsultaDeLoBloqueado&gt;</c>: un módulo nuevo entra
/// registrando su implementación y sin que nadie edite el listado.
/// </para>
/// <para>
/// <b>Por qué no basta con añadir dos valores al enumerado.</b> El repositorio anterior unía en SQL
/// tres tablas del esquema <c>organizacion</c>. Alcanzar a <c>identidad.usuarios</c> y a
/// <c>terceros.terceros</c> por ahí exigiría un <c>JOIN</c> entre esquemas, que es exactamente la
/// frontera que el monolito modular no cruza. El puerto es la única vía que queda, y es la que el
/// §4 ya tenía escrita.
/// </para>
/// <para>
/// <b>No abre el ámbito de lo bloqueado, y esa ausencia es la decisión.</b> Lo abre el listado, una
/// vez, con su motivo y su permiso; el ámbito es transversal —lo leen los cuatro contextos— así que
/// una sola apertura vale para todos los módulos. Un puerto que se destapara a sí mismo convertiría
/// una apertura declarada en cinco puertas.
/// </para>
/// </remarks>
public interface IConsultaDeLoBloqueado
{
    /// <summary>Sus primeras filas bloqueadas según el criterio, y cuántas tiene en total.</summary>
    /// <param name="criterio">Filtro, orden y cuántas filas como mucho.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<LoBloqueadoDeUnModulo> PrimerosAsync(
        CriterioDeLoBloqueado criterio, CancellationToken cancelacion);
}
