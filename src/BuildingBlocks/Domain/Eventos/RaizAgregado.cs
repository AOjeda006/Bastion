using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.BuildingBlocks.Domain.Eventos;

/// <summary>
/// Raíz de agregado: la entidad que además de su estado lleva <b>lo que ha ocurrido con ella</b> y
/// todavía no se ha contado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué los eventos viajan en el agregado y no en un servicio de ámbito.</b> Un recolector
/// inyectado —«apunta este evento en la lista de la petición»— daría la misma atomicidad y perdería
/// la propiedad que de verdad importa: aquí un evento <b>no puede existir sin su escritura</b>. La
/// bandeja se llena leyendo el rastreador de cambios, así que solo llega a ella el evento de un
/// agregado que se está guardando. Un caso de uso que registre un evento y luego no guarde nada no
/// publica nada, y eso es correcto: no ha pasado nada que contar. Con una lista suelta, ese mismo
/// evento se colaría en el <c>SaveChanges</c> siguiente, que puede ser el de otra cosa.
/// </para>
/// <para>
/// <b>Solo la heredan los agregados que emiten.</b> No es una clase base universal ni pretende
/// serlo: no aporta identidad ni igualdad — eso lo declara cada entidad y duplicarlo sería un
/// molde que hay que rellenar. Heredarla significa exactamente una cosa: «de esta raíz salen
/// eventos».
/// </para>
/// <para>
/// <b>Desde el 0.10 sí aporta las dos marcas de tiempo</b>, porque hereda de
/// <see cref="EntidadBase"/>. No es una excepción a lo anterior: una raíz de agregado es, por
/// definición, un recurso que alguien lee y edita por su cuenta, y de eso trata
/// <see cref="EntidadBase"/>. Lo que sigue sin aportar es todo lo demás.
/// </para>
/// <para>
/// <b>EF Core no ve nada de esto.</b> <see cref="EventosPendientes"/> apunta a un tipo que el
/// modelo ignora explícitamente (<c>ConfiguracionDeLaBandeja</c>), así que no se convierte en una
/// navegación ni en una tabla. Sin esa línea, EF Core intentaría mapear
/// <see cref="EventoDeIntegracion"/> como entidad y pediría una clave primaria para él.
/// </para>
/// </remarks>
public abstract class RaizAgregado : EntidadBase
{
    private readonly List<EventoDeIntegracion> _eventos = [];

    /// <summary>Crea la raíz con sus marcas de tiempo puestas.</summary>
    /// <param name="momento">Ahora.</param>
    protected RaizAgregado(DateTimeOffset momento)
        : base(momento)
    {
    }

    /// <summary>Constructor de materialización para EF Core.</summary>
    protected RaizAgregado()
    {
    }

    /// <summary>Lo ocurrido con este agregado que todavía no está en la bandeja de salida.</summary>
    public IReadOnlyList<EventoDeIntegracion> EventosPendientes => _eventos;

    /// <summary>Apunta un hecho consumado de este agregado.</summary>
    /// <remarks>
    /// Es <c>public</c> y no <c>protected</c> porque quien construye el evento es la capa de
    /// aplicación: el tipo concreto vive en el <c>Contracts</c> del módulo y el dominio no lo ve
    /// (ver <see cref="EventoDeIntegracion"/>). La alternativa —que el dominio referenciara su
    /// propio <c>Contracts</c>— invertiría la única dependencia que el §12 declara pública.
    /// </remarks>
    /// <param name="evento">El hecho ocurrido.</param>
    public void Registrar(EventoDeIntegracion evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        _eventos.Add(evento);
    }

    /// <summary>Olvida los eventos ya volcados a la bandeja.</summary>
    /// <remarks>
    /// Lo llama el interceptor cuando el guardado <b>ya ha ido bien</b>, y no al volcar la lista:
    /// si la vaciara antes y el guardado reventara, el agregado seguiría vivo en el rastreador con
    /// la lista limpia y el evento se habría perdido sin que nada fallase.
    /// </remarks>
    public void OlvidarEventos() => _eventos.Clear();
}
