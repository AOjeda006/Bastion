namespace Bastion.BuildingBlocks.Contracts.Maestros;

/// <summary>
/// Cómo se llama un maestro para quien lo enseña: su código, y su nombre o su descripción.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es lo que pinta la pantalla de un maestro de otro módulo</b>, el código solo o el código con
/// el nombre (ADR-0055 §12), y lo que la exportación escribe en dos columnas (ADR-0074). No es la
/// ficha: ni su estado, ni su versión, ni nada con lo que decidir. Para decidir están los estados
/// de cada puerto.
/// </para>
/// <para>
/// <b>Vive en el bloque común</b>, como <c>DireccionDto</c>: lo publican Organización, por cinco
/// maestros, y Catálogo, por el artículo, y un módulo no puede referenciar el <c>Contracts</c> de
/// otro para tomar prestado un tipo común. Con uno por dueño serían dos copias de la misma forma.
/// </para>
/// </remarks>
/// <param name="Codigo">El código del maestro, que es lo que lo identifica para una persona.</param>
/// <param name="Nombre">
/// El nombre, o la descripción, o <c>null</c> si el maestro no tiene: una ubicación puede no
/// llevarla.
/// </param>
public sealed record NombreDeMaestro(string Codigo, string? Nombre);
