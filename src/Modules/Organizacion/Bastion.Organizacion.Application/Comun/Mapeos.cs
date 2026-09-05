using Bastion.BuildingBlocks.Application.Direcciones;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Divisas;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Impuestos;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Domain.Ubicaciones;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Comun;

/// <summary>
/// Traducción entre las entidades del módulo y sus DTO.
/// </summary>
/// <remarks>
/// <para>
/// A mano y en un solo sitio. Un mapeador por convención ahorra estas líneas y a cambio hace que
/// añadir una propiedad al DTO la rellene sola desde una entidad que quizá no debía exponerla;
/// aquí, lo que sale de la API sale porque alguien lo escribió (`patrones/repository-y-dto.md`).
/// De paso, los mapeadores automáticos de referencia pasaron a licencia comercial en 2025.
/// </para>
/// <para>
/// Los enumerados salen como TEXTO (<c>ToString</c>): un ordinal es un contrato que se rompe solo
/// con reordenar el enumerado, sin que quien lo reordena vea que está rompiendo nada.
/// </para>
/// </remarks>
internal static class Mapeos
{
    internal static EmpresaDto ADto(this Empresa empresa) => new(
        empresa.Id,
        empresa.Nif.Valor,
        empresa.RazonSocial,
        empresa.DomicilioFiscal.ADto(),
        empresa.DivisaBase,
        empresa.RegimenDeIva.ToString());

    internal static EjercicioDto ADto(this Ejercicio ejercicio) => new(
        ejercicio.Id,
        ejercicio.EmpresaId,
        ejercicio.Anio,
        ejercicio.FechaDeInicio,
        ejercicio.FechaDeFin,
        ejercicio.Estado.ToString());

    internal static SerieDto ADto(this Serie serie) => new(
        serie.Id,
        serie.EmpresaId,
        serie.EjercicioId,
        serie.TipoDeDocumento.ToString(),
        serie.Codigo,
        serie.Formato,
        serie.Contador,
        serie.Estado.ToString());

    internal static AlmacenDto ADto(this Almacen almacen) => new(
        almacen.Id,
        almacen.EmpresaId,
        almacen.Codigo,
        almacen.Nombre,
        almacen.Direccion?.ADto(),
        almacen.Tipo.ToString());

    internal static ImpuestoDto ADto(this Impuesto impuesto) => new(
        impuesto.Id,
        impuesto.Codigo,
        impuesto.Nombre,
        impuesto.Tipo.ToString(),
        impuesto.Porcentaje,
        impuesto.VigenteDesde,
        impuesto.VigenteHasta,
        impuesto.CuentaRepercutido,
        impuesto.CuentaSoportado);

    // `Decimales` sale del catálogo del código, no de una columna: es una propiedad calculada de
    // la entidad. Que aquí se escriba igual que las demás es justo lo que se quería —quien lee el
    // DTO no tiene por qué saber de dónde viene cada campo—, y que no se pueda guardar lo
    // comprueba el test de esquema, que exige que la columna NO exista.
    internal static DivisaDto ADto(this Divisa divisa) => new(
        divisa.Id,
        divisa.Codigo,
        divisa.Nombre,
        divisa.Decimales);

    internal static TipoCambioDto ADto(this TipoCambio cambio) => new(
        cambio.Id,
        cambio.DivisaOrigenId,
        cambio.DivisaDestinoId,
        cambio.Fecha,
        cambio.Tasa);

    internal static UnidadMedidaDto ADto(this UnidadMedida unidad) => new(
        unidad.Id,
        unidad.Codigo,
        unidad.Nombre,
        unidad.Decimales);

    internal static ConversionUmDto ADto(this ConversionUM conversion) => new(
        conversion.Id,
        conversion.UnidadOrigenId,
        conversion.UnidadDestinoId,
        conversion.Factor);

    internal static UbicacionDto ADto(this Ubicacion ubicacion) => new(
        ubicacion.Id,
        ubicacion.EmpresaId,
        ubicacion.AlmacenId,
        ubicacion.Codigo,
        ubicacion.Pasillo,
        ubicacion.Estante,
        ubicacion.Hueco,
        ubicacion.Descripcion);
}
