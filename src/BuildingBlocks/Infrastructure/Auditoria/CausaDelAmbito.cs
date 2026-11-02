using System.Diagnostics;
using Bastion.BuildingBlocks.Application.Autorizacion;

namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>
/// Quién causó lo que se escribe en este ámbito, con qué correlación y en qué traza.
/// </summary>
/// <remarks>
/// <para>
/// La leen los dos interceptores que firman lo que se escribe: la traza de auditoría lleva el
/// usuario y la correlación, y cada fila de la bandeja, las tres. Así un evento sabe quién lo
/// causó sin que el caso de uso que lo registra tenga que copiar nada.
/// </para>
/// <para>
/// <b>En una petición</b>, el usuario es el de su token, la traza es la actividad en curso, y la
/// correlación es una por ámbito: todo lo que escribe una operación, en uno o en varios
/// <c>SaveChanges</c>, se junta por ella (3.4, hueco 3). Hasta el 3.4 era una por
/// <c>SaveChanges</c>, y la fila de la bandeja no habría podido llevar la de su traza.
/// </para>
/// <para>
/// <b>Detrás del despachador</b>, las tres son las de la fila del evento que se atiende: quien
/// causó el evento es quien causó su efecto. Las restaura el despachador con
/// <see cref="ConLaDelEvento"/>, que es <c>internal</c> por lo mismo que el ámbito de la empresa
/// del evento: ningún caso de uso puede firmar en nombre de otro.
/// </para>
/// <para>
/// Es <c>scoped</c>, la vida de los interceptores que la leen: una <i>singleton</i> daría la misma
/// correlación a todas las peticiones.
/// </para>
/// </remarks>
/// <param name="usuario">Quién pide la operación, cuando la pide una petición.</param>
public sealed class CausaDelAmbito(IUsuarioActual usuario)
{
    private static readonly AsyncLocal<Causa?> s_delEvento = new();

    /// <summary>Quién lo causó, o <c>null</c> si no fue nadie con sesión: la semilla, un trabajo de fondo.</summary>
    public Guid? UsuarioId => s_delEvento.Value is { } delEvento
        ? delEvento.UsuarioId
        : usuario.EstaAutenticado ? usuario.UsuarioId : null;

    /// <summary>La correlación de todo lo que escribe esta operación.</summary>
    public Guid CorrelacionId { get => s_delEvento.Value?.CorrelacionId ?? field; } = Guid.CreateVersion7();

    /// <summary>Las tres juntas, como las lleva una fila de la bandeja.</summary>
    public Causa Actual => new(UsuarioId, CorrelacionId, ContextoDeTraza);

    // El `traceparent` W3C de la actividad en curso, si la hay. No es del ámbito sino del flujo
    // asíncrono, como la actividad misma: por eso es estático, y solo sale dentro de la causa.
    private static string? ContextoDeTraza => s_delEvento.Value is { } delEvento
        ? delEvento.ContextoDeTraza
        : Activity.Current is { IdFormat: ActivityIdFormat.W3C } actividad ? actividad.Id : null;

    /// <summary>Abre el ámbito en el que la causa es la de un evento, para su manejador.</summary>
    /// <remarks>Al cerrarse, vuelve la de fuera.</remarks>
    /// <param name="causa">La causa de la fila de la bandeja.</param>
    /// <returns>El ámbito. Desecharlo lo cierra.</returns>
    internal static IDisposable ConLaDelEvento(Causa causa) => new AmbitoDelEvento(causa);

    private sealed class AmbitoDelEvento : IDisposable
    {
        private readonly Causa? _deFuera = s_delEvento.Value;
        private bool _cerrado;

        public AmbitoDelEvento(Causa causa) => s_delEvento.Value = causa;

        public void Dispose()
        {
            if (_cerrado)
            {
                return;
            }

            _cerrado = true;
            s_delEvento.Value = _deFuera;
        }
    }
}

/// <summary>Quién causó un cambio, con qué correlación y en qué traza.</summary>
/// <param name="UsuarioId">Quién, si fue alguien con sesión.</param>
/// <param name="CorrelacionId">La correlación de la operación que lo causó.</param>
/// <param name="ContextoDeTraza">El <c>traceparent</c> W3C de su actividad, si la había.</param>
public sealed record Causa(Guid? UsuarioId, Guid CorrelacionId, string? ContextoDeTraza);
