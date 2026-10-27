using Bastion.BuildingBlocks.Contracts.Maestros;

namespace Bastion.BuildingBlocks.Application.Exportacion;

/// <summary>
/// Un maestro al que apunta una fila exportada: su identificador, y cómo se llama, si quien exporta
/// puede verlo y su ficha lo enseña (ADR-0074).
/// </summary>
/// <remarks>
/// <b>Lleva el identificador aunque se haya nombrado</b>, porque es lo que se escribe cuando no:
/// sin el permiso de ver el maestro, o con un maestro que su ficha ya no enseña, como un almacén
/// bloqueado, el fichero enseña lo mismo que la pantalla, que es el identificador.
/// </remarks>
/// <param name="Id">El identificador al que apunta la fila.</param>
/// <param name="Nombre">El código y el nombre, o nulo si no se ha nombrado.</param>
public sealed record MaestroCitado(Guid Id, NombreDeMaestro? Nombre)
{
    /// <summary>El maestro de una fila, con su nombre si está entre los pedidos.</summary>
    /// <param name="id">El identificador que lleva la fila.</param>
    /// <param name="nombres">Los nombres que han vuelto para la página.</param>
    public static MaestroCitado De(Guid id, IReadOnlyDictionary<Guid, NombreDeMaestro> nombres)
    {
        ArgumentNullException.ThrowIfNull(nombres);

        return new MaestroCitado(id, nombres.GetValueOrDefault(id));
    }

    /// <summary>Lo mismo, para un maestro opcional: sin identificador, no hay maestro.</summary>
    /// <param name="id">El identificador que lleva la fila, o nulo si no apunta a ninguno.</param>
    /// <param name="nombres">Los nombres que han vuelto para la página.</param>
    public static MaestroCitado? De(Guid? id, IReadOnlyDictionary<Guid, NombreDeMaestro> nombres) =>
        id is { } valor ? De(valor, nombres) : null;
}
