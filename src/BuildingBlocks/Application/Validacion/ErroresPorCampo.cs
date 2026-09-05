using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Validacion;

/// <summary>
/// Va juntando los campos que no cuadran para poder devolverlos TODOS de una vez.
/// </summary>
/// <remarks>
/// <para>
/// Devolver solo el primer fallo obliga a corregir, reenviar y descubrir el siguiente, y así
/// hasta que se acaba la paciencia. Un formulario se corrige entero o no se corrige (§9).
/// </para>
/// <para>
/// <b>Vive en el bloque común desde el ítem 1.5</b>, y no en la capa de aplicación de un módulo. El
/// motivo no es el ahorro de cuarenta líneas: es <c>Codigo</c>. <c>datos-no-validos</c> es UN
/// <c>type</c> del catálogo del ADR-0030, con UN texto escrito en el diccionario del frontal; con
/// una copia por módulo, la constante estaría declarada tantas veces como módulos, y el día que una
/// se tocara habría dos códigos distintos para el mismo hecho —uno de ellos sin texto— sin que
/// nada se pusiera rojo hasta que alguien viera la clave cruda en pantalla.
/// </para>
/// </remarks>
public sealed class ErroresPorCampo
{
    /// <summary>Código estable de los errores por campo del módulo.</summary>
    public const string Codigo = "datos-no-validos";

    private readonly Dictionary<string, List<string>> _campos = new(StringComparer.Ordinal);

    /// <summary>Indica si se ha recogido algún incumplimiento.</summary>
    public bool Hay => _campos.Count > 0;

    /// <summary>Apunta que un campo incumple algo.</summary>
    /// <param name="campo">Nombre del campo TAL COMO VIAJA en el cuerpo de la petición.</param>
    /// <param name="motivo">Qué le pasa, dicho para quien rellenó el formulario.</param>
    public void Agregar(string campo, string motivo)
    {
        if (!_campos.TryGetValue(campo, out List<string>? motivos))
        {
            motivos = [];
            _campos[campo] = motivos;
        }

        motivos.Add(motivo);
    }

    /// <summary>El error de operación que recoge todo lo apuntado.</summary>
    public ErrorDeOperacion AError() => ErrorDeOperacion.Validacion(
        Codigo,
        "Algunos campos no son válidos. Revise los indicados.",
        _campos.ToDictionary(par => par.Key, par => (IReadOnlyList<string>)par.Value, StringComparer.Ordinal));
}
