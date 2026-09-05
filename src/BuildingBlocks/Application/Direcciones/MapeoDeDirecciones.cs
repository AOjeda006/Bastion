using Bastion.BuildingBlocks.Contracts.Direcciones;
using Bastion.BuildingBlocks.Domain.Direcciones;

namespace Bastion.BuildingBlocks.Application.Direcciones;

/// <summary>
/// Traducción entre la dirección del dominio y la del contrato, en un solo sitio.
/// </summary>
/// <remarks>
/// <para>
/// <b>Está en el bloque común desde el ítem 1.5, y por la misma razón que <c>DireccionDto</c>
/// llegó a <c>Contracts</c></b> (ADR-0029): el tipo de origen vive en <c>BuildingBlocks.Domain</c>
/// y el de destino en <c>BuildingBlocks.Contracts</c>, así que la traducción entre los dos no es
/// de ningún módulo. Con una copia por módulo, Terceros habría estrenado la segunda —seis campos,
/// idénticos— y el día que R17 crezca con un séptimo campo habría que acordarse de las dos.
/// </para>
/// <para>
/// Lo demás del criterio de mapeo no cambia: a mano y escrito, no por convención. Un mapeador
/// automático ahorra estas líneas y a cambio hace que añadir una propiedad al DTO la rellene sola
/// desde una entidad que quizá no debía exponerla (`patrones/repository-y-dto.md`).
/// </para>
/// </remarks>
public static class MapeoDeDirecciones
{
    /// <summary>La dirección tal como sale de la API.</summary>
    /// <param name="direccion">La dirección del dominio.</param>
    public static DireccionDto ADto(this Direccion direccion)
    {
        ArgumentNullException.ThrowIfNull(direccion);

        return new DireccionDto
        {
            Calle = direccion.Calle,
            Numero = direccion.Numero,
            CodigoPostal = direccion.CodigoPostal,
            Poblacion = direccion.Poblacion,
            Subdivision = direccion.Subdivision,
            Pais = direccion.Pais,
        };
    }

    /// <summary>
    /// Construye la dirección del dominio a partir de la del contrato.
    /// </summary>
    /// <remarks>
    /// Puede lanzar, y es correcto que lo haga: la forma —obligatoriedad, longitudes y las dos
    /// letras del país— ya la ha comprobado el borde con sus anotaciones antes de llegar aquí. Si
    /// aun así saltara una guarda, no sería un dato malo del usuario sino una validación que falta
    /// en el contrato, y eso es un fallo de programación (ADR-0004).
    /// </remarks>
    /// <param name="dto">La dirección del contrato, ya validada por el borde.</param>
    public static Direccion ADireccion(this DireccionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return Direccion.De(
            dto.Calle, dto.Numero, dto.CodigoPostal, dto.Poblacion, dto.Subdivision, dto.Pais);
    }
}
