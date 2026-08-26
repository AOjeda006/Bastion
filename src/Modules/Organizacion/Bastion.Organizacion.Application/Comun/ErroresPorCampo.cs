using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Organizacion.Application.Comun;

/// <summary>
/// Va juntando los campos que no cuadran para poder devolverlos TODOS de una vez.
/// </summary>
/// <remarks>
/// Devolver solo el primer fallo obliga a corregir, reenviar y descubrir el siguiente, y así
/// hasta que se acaba la paciencia. Un formulario se corrige entero o no se corrige (§9).
/// </remarks>
internal sealed class ErroresPorCampo
{
    /// <summary>Código estable de los errores por campo del módulo.</summary>
    internal const string Codigo = "datos-no-validos";

    private readonly Dictionary<string, List<string>> _campos = new(StringComparer.Ordinal);

    /// <summary>Indica si se ha recogido algún incumplimiento.</summary>
    internal bool Hay => _campos.Count > 0;

    /// <summary>Apunta que un campo incumple algo.</summary>
    /// <param name="campo">Nombre del campo TAL COMO VIAJA en el cuerpo de la petición.</param>
    /// <param name="motivo">Qué le pasa, dicho para quien rellenó el formulario.</param>
    internal void Agregar(string campo, string motivo)
    {
        if (!_campos.TryGetValue(campo, out List<string>? motivos))
        {
            motivos = [];
            _campos[campo] = motivos;
        }

        motivos.Add(motivo);
    }

    /// <summary>El error de operación que recoge todo lo apuntado.</summary>
    internal ErrorDeOperacion AError() => ErrorDeOperacion.Validacion(
        Codigo,
        "Algunos campos no son válidos. Revise los indicados.",
        _campos.ToDictionary(par => par.Key, par => (IReadOnlyList<string>)par.Value, StringComparer.Ordinal));
}
