using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Microsoft.Extensions.Logging;

namespace Bastion.BuildingBlocks.Infrastructure.Bloqueos;

/// <summary>
/// Lleva la cuenta de los ámbitos abiertos para ver lo bloqueado (R16).
/// </summary>
/// <remarks>
/// <para>
/// Es el gemelo de <c>InquilinoActual</c> por el lado del ámbito, y por las mismas razones: el
/// ámbito va en un <see cref="AsyncLocal{T}"/> para sobrevivir a los <c>await</c> del camino que
/// lo abre y <b>no</b> filtrarse a las peticiones vecinas que el host atiende a la vez.
/// </para>
/// <para>
/// Lo que no comparte con él es la mitad del claim: aquí no hay nada que leer de la petición. Ver
/// lo bloqueado no depende de quién eres, depende de que alguien lo haya pedido a propósito y haya
/// dicho por qué. Cerrado por omisión y sin excepciones por identidad.
/// </para>
/// </remarks>
/// <param name="registro">Dónde se anota la apertura de cada ámbito.</param>
/// <param name="usuario">Quién está pidiendo la operación, para que la traza lo diga.</param>
public sealed partial class AccesoALoBloqueado(
    ILogger<AccesoALoBloqueado> registro,
    IUsuarioActual usuario)
    : IAccesoALoBloqueado
{
    private static readonly AsyncLocal<Ambito?> s_ambito = new();

    /// <inheritdoc/>
    public bool Abierto => s_ambito.Value is not null;

    /// <inheritdoc/>
    public MotivoParaVerLoBloqueado? MotivoDelAmbito => s_ambito.Value?.Motivo;

    /// <inheritdoc/>
    public IDisposable ViendoLoBloqueado(MotivoParaVerLoBloqueado motivo)
    {
        // Se anota al abrirlo y no al cerrarlo: si lo de dentro revienta, el registro ya dice bajo
        // qué ámbito estaba corriendo. Y en este caso además importa por sí mismo: mirar un dato
        // reservado por el art. 32 es un hecho que conviene tener escrito.
        //
        // CON QUIÉN, y no solo con qué motivo. El art. 32 no reserva el acceso «a la
        // administración»: lo reserva a personas concretas, y una traza que dice que alguien miró
        // sin decir quién no es una traza, es un contador. Hasta el ítem 1.4 aquí solo iba el
        // motivo, que bastaba mientras el único camino era desbloquear —la escritura siguiente
        // dejaba su propia fila de auditoría con el usuario dentro—. La consulta del art. 32 no
        // escribe nada: si no lo dice esta línea, no lo dice nadie.
        //
        // Anulable a propósito y sin lanzar: el ámbito también se abre fuera de una petición
        // —arranque, trabajos de fondo—, y ahí no hay usuario. Un `Guid.Empty` de relleno pasaría
        // por un usuario de verdad en cuanto alguien leyera el registro.
        Anotar(registro, motivo, usuario.EstaAutenticado ? usuario.UsuarioId : null);

        Ambito ambito = new(motivo, s_ambito.Value);
        s_ambito.Value = ambito;

        return ambito;
    }

    [LoggerMessage(
        EventId = 8200,
        Level = LogLevel.Information,
        Message = "Consulta que ve datos bloqueados, a propósito. Motivo: {Motivo}. Usuario: {UsuarioId}.")]
    private static partial void Anotar(
        ILogger registro, MotivoParaVerLoBloqueado motivo, Guid? usuarioId);

    // Al cerrarse recupera el de fuera en vez de dejar el campo en nulo, por lo mismo que el
    // ámbito sin inquilino: anidar dos es normal, y con un nulo a pelo el de fuera se cerraría
    // también, en silencio y a mitad.
    private sealed class Ambito(MotivoParaVerLoBloqueado motivo, Ambito? anterior) : IDisposable
    {
        private bool _cerrado;

        public MotivoParaVerLoBloqueado Motivo => motivo;

        public void Dispose()
        {
            if (_cerrado)
            {
                return;
            }

            _cerrado = true;
            s_ambito.Value = anterior;
        }
    }
}
