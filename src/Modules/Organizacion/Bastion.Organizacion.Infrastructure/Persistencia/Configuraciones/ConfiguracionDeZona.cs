using Bastion.BuildingBlocks.Domain.Fechas;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Configuraciones;

/// <summary>Cómo se guarda una zona horaria: su identificador IANA, y solo uno de los admitidos.</summary>
/// <remarks>
/// Compartida por la empresa y el almacén, que la guardan igual. La lista del <c>CHECK</c> sale de
/// <see cref="ZonaHoraria.Admitidas"/> y no se teclea: si la lista cambia, el modelo cambia, y la
/// comprobación de migraciones pendientes lo dice antes que nadie.
/// </remarks>
internal static class ConfiguracionDeZona
{
    /// <summary>El nombre de la columna en las dos tablas, que es el que pone la convención.</summary>
    internal const string Columna = "zona_horaria";

    /// <summary>La columna se lee y se escribe por el identificador.</summary>
    /// <param name="zona">La propiedad.</param>
    internal static void Mapear(PropertyBuilder<ZonaHoraria> zona) =>
        zona.HasConversion(valor => valor.Iana, iana => ZonaHoraria.De(iana));

    /// <summary>La columna es una de las admitidas, o nula donde se admite nula.</summary>
    /// <returns>La condición del <c>CHECK</c>.</returns>
    internal static string Admitidas() =>
        Columna + " IN ('" + string.Join("', '", ZonaHoraria.Admitidas.Select(zona => zona.Iana)) + "')";
}
