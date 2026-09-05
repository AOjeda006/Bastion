using Bastion.BuildingBlocks.Application.Direcciones;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Comun;

/// <summary>
/// Traducción entre las entidades del módulo y sus DTO.
/// </summary>
/// <remarks>
/// <para>
/// A mano y en un solo sitio, igual que en Organización: lo que sale de la API sale porque
/// alguien lo escribió (`patrones/repository-y-dto.md`). La dirección no está aquí porque su
/// traducción es del bloque común desde este mismo ítem —los dos tipos viven allí—, y una copia
/// por módulo se habría estrenado justo ahora.
/// </para>
/// <para>
/// Los enumerados salen como TEXTO (<c>ToString</c>): un ordinal es un contrato que se rompe solo
/// con reordenar el enumerado, sin que quien lo reordena vea que está rompiendo nada. Aquí eso
/// importa de más: el estado de verificación tiene hoy dos valores y va a tener un tercero cuando
/// exista la consulta al VIES.
/// </para>
/// </remarks>
internal static class Mapeos
{
    internal static IdentificacionFiscalDto ADto(this IdentificacionFiscal identificacion) => new(
        identificacion.Pais,
        identificacion.Numero,
        identificacion.Verificacion.ToString());

    internal static TerceroDto ADto(this Tercero tercero) => new(
        tercero.Id,
        tercero.EmpresaId,
        tercero.Identificacion.ADto(),
        tercero.RazonSocial,
        tercero.NombreComercial,
        tercero.DomicilioFiscal.ADto(),
        tercero.EsCliente,
        tercero.EsProveedor);
}
