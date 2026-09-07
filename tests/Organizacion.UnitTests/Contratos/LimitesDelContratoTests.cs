using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Bastion.BuildingBlocks.Contracts.Direcciones;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Domain.Unidades;
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
        // Un NIF se escribe con guiones y espacios («B-9999999 7», «00000001 R») y se normaliza
        // al construirlo. Un StringLength(9) en el contrato rechazaría con un 400 de forma algo
        // que es perfectamente válido, antes de que nadie haya llegado a normalizarlo.
        Nif.Longitud.ShouldBe(9);

        LongitudMaximaDe<Contracts.Empresas.CrearEmpresaDto>(
            nameof(Contracts.Empresas.CrearEmpresaDto.Nif))
            .ShouldBeNull();
    }

    /// <summary>
    /// Todo <c>Factor</c> del contrato acota exactamente donde acota el dominio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Barrido y no lista.</b> Los cuatro literales —dos DTO por dos extremos— son la misma
    /// copia a mano de <see cref="ConversionUM.FactorMinimo"/> y
    /// <see cref="ConversionUM.FactorMaximo"/> que el resto de este fichero vigila para las
    /// longitudes, y el tercer DTO con un factor que aparezca entra aquí solo. Enumerarlos sería
    /// dejar fuera justo al que llegue después, que es como se queda vieja una lista escrita.
    /// </para>
    /// <para>
    /// <b>Y no es papeleo.</b> El rango del factor sostiene la decisión 2 del ADR-0023: la
    /// comprobación de la inversa es <i>total</i> —no tiene casos en los que callarse— porque la
    /// inversa de cualquier factor válido cae también dentro del rango. Un contrato que admitiera
    /// más de lo que el dominio admite pondría un 500 donde tocaba un 400; uno que admitiera menos
    /// rechazaría con un 400 lo que la entidad guarda sin problema. Las dos averías son mudas.
    /// </para>
    /// </remarks>
    [Fact]
    public void Todo_factor_del_contrato_acota_donde_acota_el_dominio()
    {
        IReadOnlyList<(string Donde, decimal Minimo, decimal Maximo)> factores = RangosDeFactor();

        factores.ShouldNotBeEmpty(
            "no se ha encontrado ni una propiedad `Factor` con `[Range]` en el contrato del " +
            "módulo. O los DTO han cambiado de forma, o se está barriendo el ensamblado " +
            "equivocado — y las dos cosas dejan esta comparación sin nada que comparar");

        foreach ((string donde, decimal minimo, decimal maximo) in factores)
        {
            minimo.ShouldBe(
                ConversionUM.FactorMinimo,
                $"{donde} deja pasar un factor que el dominio rechaza, o rechaza uno que acepta");

            maximo.ShouldBe(
                ConversionUM.FactorMaximo,
                $"{donde} no tiene el mismo techo que el dominio, y de ese techo cuelga que la " +
                "comprobación de la inversa no tenga casos en los que callarse");
        }
    }

    /// <summary>Los <c>[Range]</c> de todas las propiedades llamadas <c>Factor</c>.</summary>
    /// <remarks>
    /// Los límites se leen como los ESCRIBIÓ quien puso el atributo —son cadenas hasta que
    /// <c>RangeAttribute</c> las convierte— y se parsean en cultura invariante, que es la única en
    /// la que <c>"0.000001"</c> significa lo que parece. Comparar el texto directamente valdría
    /// hoy y fallaría con un <c>"1000000.0"</c> que dice exactamente lo mismo.
    /// </remarks>
    private static IReadOnlyList<(string Donde, decimal Minimo, decimal Maximo)> RangosDeFactor() =>
        [.. from tipo in typeof(EstadoDeMaestro).Assembly.GetTypes()
            from propiedad in tipo.GetProperties()
            where string.Equals(propiedad.Name, "Factor", StringComparison.Ordinal)
            let rango = propiedad.GetCustomAttribute<RangeAttribute>()
            where rango is not null
            select (
                $"{tipo.Name}.{propiedad.Name}",
                ADecimal(rango.Minimum),
                ADecimal(rango.Maximum))];

    private static decimal ADecimal(object limite) =>
        limite is decimal ya
            ? ya
            : decimal.Parse((string)limite, CultureInfo.InvariantCulture);

    private static int? LongitudMaximaDe<T>(string propiedad)
    {
        PropertyInfo? encontrada = typeof(T).GetProperty(propiedad);
        encontrada.ShouldNotBeNull($"{typeof(T).Name} no tiene la propiedad {propiedad}.");

        return encontrada.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
    }
}
