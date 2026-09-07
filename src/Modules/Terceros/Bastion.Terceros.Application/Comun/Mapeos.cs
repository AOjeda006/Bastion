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

    internal static RegimenFiscalDto ADto(this RegimenFiscal regimen) => new(
        regimen.Territorio.ToString(),
        regimen.RecargoDeEquivalencia,
        regimen.CriterioDeCaja,
        regimen.SujetoARetencionIrpf);

    internal static ContactoDto ADto(this Contacto contacto) => new(
        contacto.Id,
        contacto.TerceroId,
        contacto.Nombre,
        contacto.Cargo,
        contacto.Correo?.Valor,
        contacto.Telefono);

    // El IBAN sale ENTERO, no por `ToString()`, que enmascara: quien tiene el permiso es quien va
    // a pagar por esa cuenta. Lo enmascarado es lo que va al registro. Ver `CuentaBancariaDto`.
    internal static CuentaBancariaDto ADto(this CuentaBancaria cuenta) => new(
        cuenta.Id,
        cuenta.TerceroId,
        cuenta.Iban.Valor,
        cuenta.Bic,
        cuenta.Alias,
        cuenta.EsPreferente);

    internal static CondicionPagoDto ADto(this CondicionPago condicion) => new(
        condicion.Id,
        condicion.TerceroId,
        condicion.Rol.ToString(),
        condicion.DiasDePlazo,
        condicion.DiaDePagoFijo,
        condicion.DescuentoPorProntoPago);

    internal static LimiteCreditoDto ALimiteDto(this Tercero tercero) => new(
        tercero.Id,
        tercero.LimiteCredito?.Cantidad,
        tercero.LimiteCredito?.Divisa);

    internal static TerceroDto ADto(this Tercero tercero) => new(
        tercero.Id,
        tercero.EmpresaId,
        tercero.Identificacion.ADto(),
        tercero.RazonSocial,
        tercero.NombreComercial,
        tercero.DomicilioFiscal.ADto(),
        tercero.EsCliente,
        tercero.EsProveedor,
        tercero.RegimenFiscal.ADto());
}
