using System.Collections.ObjectModel;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Domain.Autorizacion;

namespace Bastion.BuildingBlocks.Application.Exportacion;

/// <summary>
/// Cómo pide una exportación los nombres de los maestros de una página interna: una vez por
/// maestro, con los identificadores distintos de la página, y solo con el permiso de verlos
/// (ADR-0074).
/// </summary>
/// <remarks>
/// <para>
/// <b>El permiso se mira antes de preguntar</b> (ADR-0074 §3): el puerto del dueño no sabe quién
/// pregunta. Sin el permiso no se pregunta, y la fila sale con el identificador, como la pantalla,
/// que sin él no se lo pide a su dueño.
/// </para>
/// <para>
/// <b>Una página sin identificadores no pregunta</b> (ADR-0074 §5): el puerto va a la base también
/// con un lote vacío, y el que no tiene nada que preguntar es quien lo sabe.
/// </para>
/// </remarks>
public static class NombresDeMaestros
{
    private static readonly IReadOnlyDictionary<Guid, NombreDeMaestro> s_ninguno =
        ReadOnlyDictionary<Guid, NombreDeMaestro>.Empty;

    /// <summary>Los nombres de los maestros de una página, si quien exporta puede verlos.</summary>
    /// <param name="usuarioActual">Quien exporta.</param>
    /// <param name="permiso">El permiso de ver el maestro, el mismo que pide su ficha.</param>
    /// <param name="ids">Los identificadores de la página, con repetidos y nulos.</param>
    /// <param name="nombrar">El método por lotes del puerto del dueño.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Los nombres que han vuelto, o ninguno sin el permiso.</returns>
    public static Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> PedirAsync(
        IUsuarioActual usuarioActual,
        Permiso permiso,
        IEnumerable<Guid?> ids,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyDictionary<Guid, NombreDeMaestro>>> nombrar,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(usuarioActual);
        ArgumentNullException.ThrowIfNull(permiso);

        return usuarioActual.Tiene(permiso)
            ? PedirAsync(ids, nombrar, cancelacion)
            : Task.FromResult(s_ninguno);
    }

    /// <summary>
    /// Lo mismo, para un maestro cuyo permiso es el del propio listado, como la categoría padre en
    /// el de categorías: quien exporta ya lo tiene, o la exportación no habría llegado aquí.
    /// </summary>
    /// <param name="ids">Los identificadores de la página, con repetidos y nulos.</param>
    /// <param name="nombrar">El método por lotes que los nombra.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Los nombres que han vuelto.</returns>
    public static async Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> PedirAsync(
        IEnumerable<Guid?> ids,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyDictionary<Guid, NombreDeMaestro>>> nombrar,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(nombrar);

        Guid[] distintos = [.. ids.OfType<Guid>().Distinct()];

        return distintos.Length == 0
            ? s_ninguno
            : await nombrar(distintos, cancelacion).ConfigureAwait(false);
    }
}
