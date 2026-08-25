using Bastion.BuildingBlocks.Domain.Resultados;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Resultados;

public sealed class ResultadoTests
{
    [Fact]
    public void Correcto_NoLlevaError()
    {
        var resultado = Resultado.Correcto();

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Error.ShouldBeNull();
    }

    [Fact]
    public void Fallo_NoEsCorrectoYConservaElError()
    {
        var error = ErrorDeOperacion.ReglaDeNegocio(
            "stock-insuficiente", "No hay bastante stock disponible para servir la línea.");

        var resultado = Resultado.Fallo(error);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error.ShouldBe(error);
    }

    [Fact]
    public void Fallo_SinError_Lanza() =>
        Should.Throw<ArgumentNullException>(() => Resultado.Fallo(null!));

    [Fact]
    public void CorrectoConValor_DevuelveElValor()
    {
        var resultado = Resultado.Correcto(42);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.ShouldBe(42);
    }

    // Leer el valor de un resultado con error es un fallo de programación, no un desenlace de
    // negocio: quien lo hace se ha saltado la comprobación. Por eso lanza y no devuelve
    // `default`, que sería un cero o un null colándose río abajo sin que nadie se entere.
    [Fact]
    public void ValorDeUnResultadoConError_Lanza()
    {
        var resultado = Resultado.Fallo<int>(
            ErrorDeOperacion.NoEncontrado("articulo-no-encontrado", "No existe el artículo pedido."));

        Should.Throw<InvalidOperationException>(() => resultado.Valor);
    }
}

public sealed class ErrorDeOperacionTests
{
    // El código es ESTABLE y es lo que el borde va a mapear: acaba publicado como parte del
    // `type` del ProblemDetails, que es contrato. El mensaje puede reescribirse; el código, no.
    [Fact]
    public void ReglaDeNegocio_ConservaElCodigoYElTipo()
    {
        var error = ErrorDeOperacion.ReglaDeNegocio("stock-insuficiente", "Mensaje.");

        error.Codigo.ShouldBe("stock-insuficiente");
        error.Tipo.ShouldBe(TipoDeError.ReglaDeNegocio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_SinCodigo_Lanza(string codigo) =>
        Should.Throw<ArgumentException>(() => ErrorDeOperacion.Conflicto(codigo, "Mensaje."));

    // El código viaja dentro de un URI (`/errors/{codigo}`). Si admitiera espacios, mayúsculas
    // o acentos, el contrato publicado dependería de cómo lo escribiera cada quien.
    [Theory]
    [InlineData("Stock Insuficiente")]
    [InlineData("stock_insuficiente")]
    [InlineData("StockInsuficiente")]
    [InlineData("stock/insuficiente")]
    [InlineData("artículo-no-encontrado")]
    public void Crear_ConUnCodigoQueNoEsUnaRanuraEstable_Lanza(string codigo) =>
        Should.Throw<ArgumentException>(() => ErrorDeOperacion.Conflicto(codigo, "Mensaje."));

    [Fact]
    public void Crear_SinMensaje_Lanza() =>
        Should.Throw<ArgumentException>(() => ErrorDeOperacion.PermisoDenegado("sin-permiso", " "));

    [Theory]
    [InlineData(TipoDeError.Validacion)]
    [InlineData(TipoDeError.PermisoDenegado)]
    [InlineData(TipoDeError.NoEncontrado)]
    [InlineData(TipoDeError.Conflicto)]
    [InlineData(TipoDeError.ReglaDeNegocio)]
    public void CadaTipoDeError_TieneUnaFabricaPropia(TipoDeError tipo)
    {
        ErrorDeOperacion error = tipo switch
        {
            TipoDeError.Validacion => ErrorDeOperacion.Validacion("codigo", "Mensaje."),
            TipoDeError.PermisoDenegado => ErrorDeOperacion.PermisoDenegado("codigo", "Mensaje."),
            TipoDeError.NoEncontrado => ErrorDeOperacion.NoEncontrado("codigo", "Mensaje."),
            TipoDeError.Conflicto => ErrorDeOperacion.Conflicto("codigo", "Mensaje."),
            _ => ErrorDeOperacion.ReglaDeNegocio("codigo", "Mensaje."),
        };

        error.Tipo.ShouldBe(tipo);
    }
}
