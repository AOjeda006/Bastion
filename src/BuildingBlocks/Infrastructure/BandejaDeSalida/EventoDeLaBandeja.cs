using Bastion.BuildingBlocks.Application.Multiempresa;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// Una fila de la bandeja de salida: un evento escrito en la <b>misma transacción</b> que el cambio
/// que lo provocó, esperando a que el trabajo de fondo lo publique (R12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe la tabla.</b> Sin ella hay que elegir entre dos incorrecciones: publicar antes
/// de confirmar —y contar un hecho que puede no llegar a ocurrir— o publicar después —y perder el
/// hecho si el proceso se cae en medio—. La bandeja convierte «guardar y publicar» en una sola
/// escritura, que es lo único que una base de datos sabe hacer de forma atómica. Lo que queda al
/// otro lado es un problema distinto y resoluble: entregar al menos una vez y no duplicar el efecto.
/// </para>
/// <para>
/// <b>Sin empresa no significa sin explicación</b>, exactamente igual que en la traza de auditoría:
/// hay emisores legítimos sin inquilino —la semilla de arranque, mañana— y cuando no hay empresa,
/// <see cref="SinInquilino"/> dice cuál es el motivo. Uno de los dos y solo uno: lo comprueba el
/// constructor y lo vuelve a comprobar una restricción de la propia tabla.
/// </para>
/// <para>
/// <b><see cref="Id"/> ordena.</b> Es un identificador de versión 7, o sea, monótono por el instante
/// de creación: leer la bandeja por <c>id</c> ascendente es leerla en el orden en que se escribió,
/// sin necesidad de un contador que serialice las escrituras. Con un identificador aleatorio haría
/// falta ordenar por <see cref="OcurridoEn"/>, y dos eventos del mismo <c>SaveChanges</c> comparten
/// instante.
/// </para>
/// </remarks>
public sealed class EventoDeLaBandeja
{
    /// <summary>Cuántos intentos fallidos seguidos aparcan un evento.</summary>
    /// <remarks>
    /// Cinco, no uno y no cien. Uno confundiría un corte de red de un segundo con un evento
    /// imposible; cien serían ocho minutos de vueltas antes de que nadie se entere, y el registro
    /// con cien excepciones iguales dentro.
    /// </remarks>
    public const int IntentosAntesDeAparcar = 5;

    /// <summary>Longitud máxima del texto del último error.</summary>
    internal const int MaximoDelError = 1024;

    // Constructor para EF Core. Las propiedades se rellenan por reflexión al materializar.
    private EventoDeLaBandeja()
    {
        Nombre = string.Empty;
        Cuerpo = string.Empty;
    }

    private EventoDeLaBandeja(
        Guid eventoId,
        DateTimeOffset ocurridoEn,
        Guid? empresaId,
        MotivoSinInquilino? sinInquilino,
        string nombre,
        string cuerpo)
    {
        Id = Guid.CreateVersion7();
        EventoId = eventoId;
        OcurridoEn = ocurridoEn;
        EmpresaId = empresaId;
        SinInquilino = sinInquilino;
        Nombre = nombre;
        Cuerpo = cuerpo;
        Estado = EstadoDelEnvio.Pendiente;
    }

    /// <summary>Identificador de la fila. Es de versión 7, así que además ordena la cola.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Identificador del evento, el que viaja dentro del propio evento y no cambia al reentregarlo.
    /// </summary>
    /// <remarks>
    /// Es la mitad de la clave de deduplicación del consumidor. Se guarda además en su propia
    /// columna, con índice único, y no solo dentro del cuerpo: es lo que impide que el mismo hecho
    /// entre dos veces en la cola, y lo que permite buscarlo sin abrir el JSON.
    /// </remarks>
    public Guid EventoId { get; private set; }

    /// <summary>Instante en que ocurrió el hecho, con zona (§3).</summary>
    public DateTimeOffset OcurridoEn { get; private set; }

    /// <summary>Empresa desde la que se emitió, o <c>null</c> si no había ninguna.</summary>
    public Guid? EmpresaId { get; private set; }

    /// <summary>Por qué no hay empresa, cuando no la hay.</summary>
    public MotivoSinInquilino? SinInquilino { get; private set; }

    /// <summary>
    /// Nombre del evento en el cable: <c>modulo.hecho-ocurrido</c>.
    /// </summary>
    /// <remarks>
    /// Es una cadena declarada a mano en el catálogo, no el nombre del tipo de C#. Con el nombre
    /// del tipo, renombrar una clase dejaría en la cola filas que ya nadie sabe deserializar, y el
    /// síntoma llegaría en el despliegue siguiente.
    /// </remarks>
    public string Nombre { get; private set; }

    /// <summary>El evento serializado, como <c>jsonb</c>.</summary>
    public string Cuerpo { get; private set; }

    /// <summary>Pendiente, publicado o aparcado.</summary>
    public EstadoDelEnvio Estado { get; private set; }

    /// <summary>Cuándo se terminó de publicar, si se publicó.</summary>
    public DateTimeOffset? PublicadoEn { get; private set; }

    /// <summary>Cuántas veces se ha intentado publicar y ha fallado.</summary>
    public int Intentos { get; private set; }

    /// <summary>Qué dijo el último fallo. Es lo que se lee cuando algo queda aparcado.</summary>
    public string? UltimoError { get; private set; }

    /// <summary>Arma una fila de la bandeja, comprobando lo que la tabla también comprobará.</summary>
    /// <param name="eventoId">Identificador del evento.</param>
    /// <param name="ocurridoEn">Instante del hecho.</param>
    /// <param name="empresaId">Empresa desde la que se emite, si la hay.</param>
    /// <param name="sinInquilino">Motivo, si no la hay.</param>
    /// <param name="nombre">Nombre del evento en el cable.</param>
    /// <param name="cuerpo">El evento serializado.</param>
    /// <returns>La fila, en estado pendiente.</returns>
    /// <exception cref="InvalidOperationException">
    /// Si lleva empresa y motivo a la vez, o ninguno de los dos.
    /// </exception>
    public static EventoDeLaBandeja De(
        Guid eventoId,
        DateTimeOffset ocurridoEn,
        Guid? empresaId,
        MotivoSinInquilino? sinInquilino,
        string nombre,
        string cuerpo)
    {
        if (empresaId.HasValue == sinInquilino.HasValue)
        {
            throw new InvalidOperationException(
                "Un evento de la bandeja lleva empresa, o lleva el motivo por el que no la lleva, " +
                "pero nunca las dos cosas ni ninguna de las dos.");
        }

        return new EventoDeLaBandeja(eventoId, ocurridoEn, empresaId, sinInquilino, nombre, cuerpo);
    }

    /// <summary>Da el evento por publicado.</summary>
    /// <remarks>
    /// Se llama <b>después</b> de que todos sus manejadores hayan terminado, no antes: ver la
    /// decisión de entrega en <c>PublicadorDeLaBandeja</c>.
    /// </remarks>
    /// <param name="ahora">Instante en que se terminó de publicar.</param>
    public void DarPorPublicado(DateTimeOffset ahora)
    {
        Estado = EstadoDelEnvio.Publicado;
        PublicadoEn = ahora;
        UltimoError = null;
    }

    /// <summary>Apunta que este intento ha fallado, y aparca el evento si ya van demasiados.</summary>
    /// <param name="error">Qué ha fallado.</param>
    /// <returns><c>true</c> si este fallo lo ha aparcado.</returns>
    public bool AnotarFallo(string error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Intentos++;

        // Recortado a lo que cabe en la columna. Un mensaje de excepción con su traza entera puede
        // ocupar kilobytes, y lo que hace falta para saber qué pasa está en la primera línea.
        UltimoError = error.Length > MaximoDelError ? error[..MaximoDelError] : error;

        if (Intentos < IntentosAntesDeAparcar)
        {
            return false;
        }

        Estado = EstadoDelEnvio.Aparcado;

        return true;
    }
}
