using Bastion.BuildingBlocks.Domain.Bloqueos;

namespace Bastion.Organizacion.Application.Bloqueos;

/// <summary>Qué clase de recurso del módulo está bloqueado.</summary>
/// <remarks>
/// <b>Un enumerado y no el nombre del tipo por reflexión.</b> El nombre de una clase es un detalle
/// interno que se renombra en una refactorización, y aquí acaba publicado en el contrato de la API:
/// renombrar <c>Almacen</c> cambiaría lo que lee un cliente ya desplegado sin que nada fallara.
/// Escrito aquí, el nombre externo es una decisión y no una consecuencia.
/// </remarks>
public enum TipoDeRecursoBloqueado
{
    /// <summary>Una empresa de la instalación.</summary>
    Empresa,

    /// <summary>Un almacén.</summary>
    Almacen,

    /// <summary>Una ubicación dentro de un almacén.</summary>
    Ubicacion,
}

/// <summary>
/// Una fila bloqueada, con lo justo para reconocerla y para levantar su bloqueo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un modelo de lectura y no las tres entidades.</b> El listado une empresas, almacenes y
/// ubicaciones en una sola página, y devolver las entidades obligaría a un contrato con tres formas
/// —o a traerlas enteras para enseñar cuatro campos de cada una. Aquí se proyecta en la consulta:
/// lo que viaja desde PostgreSQL es exactamente lo que se publica.
/// </para>
/// <para>
/// <b>Propiedades con <c>init</c> y no parámetros posicionales, y es una imposición de EF Core.</b>
/// La consulta lo construye en un inicializador de objeto —<c>new RecursoBloqueado { ... }</c>—
/// porque un constructor con argumentos deja el árbol de expresión sin manera de resolver
/// «ordéname por <c>BloqueadoEn</c>»: EF Core sabe atravesar las asignaciones de un inicializador y
/// no sabe atravesar un constructor, así que con la forma posicional el <c>?sort=</c> compila y
/// revienta en ejecución. La forma se paga con un tipo más largo y se cobra en que el listado
/// ordena.
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
