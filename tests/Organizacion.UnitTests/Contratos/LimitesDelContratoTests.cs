using Bastion.BuildingBlocks.Contracts.Direcciones;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Series;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Contratos;

/// <summary>
/// Los DTO llevan sus longitudes escritas como números porque <c>Contracts</c> no referencia al
/// dominio —es lo único que otro módulo ve, y arrastrar el dominio abriría la frontera por la
/// puerta de atrás—. Esa copia se queda desfasada sola: alguien sube un límite en la entidad y
/// el contrato sigue rechazando el valor nuevo con un 400 que nadie entiende.
/// </summary>
/// <remarks>
/// Este test es el que impide que se desfase. No prueba comportamiento: prueba que dos sitios que
/// no pueden referenciarse siguen diciendo lo mismo, y vive aquí porque este proyecto sí ve los
/// dos.
/// </remarks>
public sealed class LimitesDelContratoTests
{
    [Theory]
    [InlineData(nameof(DireccionDto.Calle), Direccion.LongitudMaximaDeCalle)]
    [InlineData(nameof(DireccionDto.Numero), Direccion.LongitudMaximaDeNumero)]
    [InlineData(nameof(DireccionDto.CodigoPostal), Direccion.LongitudMaximaDeCodigoPostal)]
    [InlineData(nameof(DireccionDto.Poblacion), Direccion.LongitudMaximaDePoblacion)]
    [InlineData(nameof(DireccionDto.Subdivision), Direccion.LongitudMaximaDeSubdivision)]
    [InlineData(nameof(DireccionDto.Pais), Direccion.LongitudDelPais)]
    public void El_contrato_de_direccion_repite_exactamente_los_limites_de_SEPA(string campo, int esperado)
    {
        LongitudMaximaDe<DireccionDto>(campo).ShouldBe(esperado);
    }

    [Fact]
    public void El_contrato_de_serie_repite_el_limite_de_codigo_del_dominio()
    {
        LongitudMaximaDe<CrearSerieDto>(nameof(CrearSerieDto.Codigo))
            .ShouldBe(Serie.LongitudMaximaDeCodigo);
    }

    [Fact]
    public void El_contrato_de_almacen_repite_el_limite_de_codigo_del_dominio()
    {
        LongitudMaximaDe<Contracts.Almacenes.CrearAlmacenDto>(
            nameof(Contracts.Almacenes.CrearAlmacenDto.Codigo))
            .ShouldBe(Almacen.LongitudMaximaDeCodigo);
    }

    [Fact]
    public void El_NIF_del_contrato_no_lleva_tope_de_longitud_porque_se_normaliza_antes()
    {
        // Un NIF se escribe con guiones y espacios («B-12345678», «12345678 Z») y se normaliza
        // al construirlo. Un StringLength(9) en el contrato rechazaría con un 400 de forma algo
        // que es perfectamente válido, antes de que nadie haya llegado a normalizarlo.
        Nif.Longitud.ShouldBe(9);

        LongitudMaximaDe<Contracts.Empresas.CrearEmpresaDto>(
            nameof(Contracts.Empresas.CrearEmpresaDto.Nif))
            .ShouldBeNull();
    }

    private static int? LongitudMaximaDe<T>(string propiedad)
    {
        PropertyInfo? encontrada = typeof(T).GetProperty(propiedad);
        encontrada.ShouldNotBeNull($"{typeof(T).Name} no tiene la propiedad {propiedad}.");

        return encontrada.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
    }
}
