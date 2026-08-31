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
public sealed partial class AccesoALoBloqueado(ILogger<AccesoALoBloqueado> registro)
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
        Anotar(registro, motivo);

        Ambito ambito = new(motivo, s_ambito.Value);
        s_ambito.Value = ambito;

        return ambito;
    }

    [LoggerMessage(
        EventId = 8200,
        Level = LogLevel.Information,
        Message = "Consulta que ve datos bloqueados, a propósito. Motivo: {Motivo}.")]
    private static partial void Anotar(ILogger registro, MotivoParaVerLoBloqueado motivo);

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
