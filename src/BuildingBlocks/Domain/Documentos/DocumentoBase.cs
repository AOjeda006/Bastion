using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.BuildingBlocks.Domain.Documentos;

/// <summary>
/// Lo que comparten los documentos que cambian de estado: que el estado <b>solo</b> se mueve por
/// una transición, y que ninguna transición ocurre sin su evento (R1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que R1 quiere que no se pueda escribir es <c>documento.Estado = X</c>.</b> Aquí eso no lo
/// impide una convención: <see cref="Estado"/> tiene el <c>set</c> privado y vive en este
/// ensamblado, así que la asignación desde fuera <b>no compila</b>. El único camino es
/// <see cref="Transitar"/>, que es <c>protected</c> — lo llaman los métodos con nombre del
/// documento concreto (<c>Confirmar</c>, <c>Anular</c>), que son los que saben qué precondiciones
/// tiene cada paso.
/// </para>
/// <para>
/// <b>Los estados NO se comparten, y por eso esto es genérico.</b> El ajuste es
/// <c>Borrador → Confirmado → Anulado</c>; el recuento tiene <c>EnCurso</c> porque contar lleva
/// tiempo; la transferencia es <c>Enviada → Recibida</c> porque el stock en tránsito existe
/// mientras vuela. Una máquina compartida con tabla de transiciones por tipo obligaría a todo
/// documento a arrastrar estados que no puede alcanzar, y degradaría «esta transición no existe»
/// —que es un error de compilación— a «no está permitida para este tipo», que es una
/// configuración y se pone mal en silencio.
/// </para>
/// <para>
/// <b>Y este tipo no sabe qué es un movimiento, ni va a saberlo.</b> No es una buena intención:
/// <c>ElTipoBaseDeDocumentoNoSabeQueEsUnMovimientoTests</c> lo comprueba sobre el ensamblado
/// compilado. El bloque común lo ven los siete módulos; el día que aquí apareciera una referencia
/// al libro de existencias, la factura y el asiento contable la heredarían sin pedirla.
/// </para>
/// <para>
/// <b>El evento va en la firma de la transición</b>, y no es una comodidad. Un documento que
/// cambia de estado sin contarlo deja a quien escucha creyendo lo contrario de lo que pasó, y
/// «acordarse de llamar a <c>Registrar</c> después» es exactamente la clase de cosa que sale verde
/// el día que alguien escribe la segunda transición. Quien construye el evento sigue siendo la
/// capa de aplicación —el tipo concreto vive en el <c>Contracts</c> del módulo y el dominio no lo
/// ve, igual que en <see cref="RaizAgregado.Registrar"/>—; lo que cambia es que ahora no hay forma
/// de transitar sin traerlo.
/// </para>
/// </remarks>
/// <typeparam name="TEstado">El enumerado de estados de ESTE documento, que no comparte con nadie.</typeparam>
public abstract class DocumentoBase<TEstado> : RaizAgregado
    where TEstado : struct, Enum
{
    /// <summary>Crea el documento en su estado inicial.</summary>
    /// <param name="estadoInicial">El estado con el que nace.</param>
    /// <param name="momento">Ahora.</param>
    protected DocumentoBase(TEstado estadoInicial, DateTimeOffset momento)
        : base(momento) => Estado = estadoInicial;

    /// <summary>Constructor de materialización para EF Core.</summary>
    protected DocumentoBase()
    {
    }

    /// <summary>En qué punto de su vida está el documento.</summary>
    public TEstado Estado { get; private set; }

    /// <summary>El único camino por el que un documento cambia de estado.</summary>
    /// <remarks>
    /// El estado de partida se pide <b>explícitamente</b> en vez de comprobarse dentro de cada
    /// método concreto: así la precondición está escrita en la llamada, y una transición que se
    /// intente desde donde no toca falla con los dos estados en el mensaje. Es una invariante del
    /// dominio, así que es una excepción y no un <c>Resultado</c> (ADR-0004): quien llama no puede
    /// «manejar» que el documento estuviera en otro sitio, tenía que haberlo mirado antes.
    /// </remarks>
    /// <param name="desde">El estado en el que el documento tiene que estar.</param>
    /// <param name="hasta">El estado al que pasa.</param>
    /// <param name="evento">Lo que se cuenta de esta transición. Obligatorio.</param>
    protected void Transitar(TEstado desde, TEstado hasta, EventoDeIntegracion evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        if (!EqualityComparer<TEstado>.Default.Equals(Estado, desde))
        {
            throw new InvalidOperationException(
                $"Un documento en estado «{Estado}» no puede pasar a «{hasta}»: esa transición " +
                $"sale de «{desde}» (R1).");
        }

        Estado = hasta;
        Registrar(evento);
    }
}
