using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Series;

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
    internal static DireccionDto ADto(this Direccion direccion) => new()
    {
        Calle = direccion.Calle,
        Numero = direccion.Numero,
        CodigoPostal = direccion.CodigoPostal,
        Poblacion = direccion.Poblacion,
        Subdivision = direccion.Subdivision,
        Pais = direccion.Pais,
    };

    /// <summary>
    /// Construye la dirección del dominio a partir de la del contrato.
    /// </summary>
    /// <remarks>
    /// Puede lanzar, y es correcto que lo haga: la forma —obligatoriedad, longitudes y las dos
    /// letras del país— ya la ha comprobado el borde con sus anotaciones antes de llegar aquí.
    /// Si aun así saltara una guarda, no sería un dato malo del usuario sino una validación que
    /// falta en el contrato, y eso es un fallo de programación (ADR-0004).
    /// </remarks>
    internal static Direccion ADireccion(this DireccionDto dto) => Direccion.De(
        dto.Calle, dto.Numero, dto.CodigoPostal, dto.Poblacion, dto.Subdivision, dto.Pais);

    internal static EmpresaDto ADto(this Empresa empresa) => new(
        empresa.Id,
        empresa.Nif.Valor,
        empresa.RazonSocial,
        empresa.DomicilioFiscal.ADto(),
        empresa.DivisaBase,
        empresa.RegimenDeIva.ToString(),
        empresa.Estado.ToString(),
        empresa.BloqueadaEn);

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
        almacen.Tipo.ToString(),
        almacen.Estado.ToString(),
        almacen.BloqueadoEn);
}
