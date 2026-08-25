using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Bastion.Api.FunctionalTests.Errores;

/// <summary>
/// Sumidero de Serilog que guarda lo que la API escribe, ya RENDERIZADO con el mismo
/// formateador que usa el host real.
/// </summary>
/// <remarks>
/// Renderizar y no quedarse con el <c>LogEvent</c> es deliberado: lo que hay que comprobar es
/// la línea que acaba en el fichero de registro, con su campo <c>@tr</c>, no una propiedad
/// intermedia que podría escribirse de otra forma.
/// </remarks>
public sealed class RegistroCapturado : ILogEventSink
{
    private static readonly CompactJsonFormatter s_formateador = new();

    private readonly Lock _cerrojo = new();
    private readonly List<string> _lineas = [];

    public void Emit(LogEvent logEvent)
    {
        using StringWriter escritor = new();
        s_formateador.Format(logEvent, escritor);

        lock (_cerrojo)
        {
            _lineas.Add(escritor.ToString());
        }
    }

    public IReadOnlyList<string> Lineas()
    {
        lock (_cerrojo)
        {
            return [.. _lineas];
        }
    }
}
