using Bastion.Catalogo.Domain.Catalogo;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// El tipo de una tarifa y su divisa en ISO 4217, del ítem 3.3, en el agregado.
/// </summary>
/// <remarks>
/// <para>
/// <b>La divisa deja de ser el identificador de un maestro ajeno y pasa a ser su código.</b> Es lo
/// que la pone al lado de la divisa base de la empresa, que ya era un código: el precio de compra
/// solo se propone en la divisa base, y comparar un <c>uuid</c> con un código no se puede sin
/// preguntar a nadie. Que el código exista y no esté retirado lo pregunta el caso de uso al
/// puerto de divisas; lo que el agregado exige es la forma, la de <c>CatalogoDeDivisas</c>, para
/// que ningún camino que no pase por la validación deje una columna con cuatro letras.
/// </para>
/// <para>
/// <b>Y el tipo se elige al abrir y no se cambia.</b> No hay ningún método que lo toque, y eso es
/// lo que se afirma aquí: un tipo fuera del enumerado no llega al agregado.
/// </para>
/// </remarks>
public sealed class ElTipoYLaDivisaDeLaTarifaTests
{
    private static readonly Guid s_empresa = Guid.NewGuid();
    private static readonly DateTimeOffset s_ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly s_hoy = new(2026, 11, 2);

    [Theory]
    [InlineData(TipoDeTarifa.Venta)]
    [InlineData(TipoDeTarifa.Compra)]
    public void El_tipo_se_guarda_tal_cual_se_eligio(TipoDeTarifa tipo)
    {
        Tarifa tarifa = Abrir(tipo, "EUR");

        tarifa.Tipo.ShouldBe(tipo);
    }

    [Fact]
    public void Un_tipo_fuera_del_enumerado_no_abre_una_tarifa()
    {
        // Un entero convertido a la fuerza es lo que llega por un camino que no leyó el texto con
        // `TiposDeTarifa.Leer`. Sin esta guarda, la columna recibiría un «2» que la restricción
        // de la base rechazaría convertido en un 500.
        Should.Throw<ArgumentOutOfRangeException>(() => Abrir((TipoDeTarifa)2, "EUR"));
    }

    [Theory]
    [InlineData("usd", "USD")]
    [InlineData("  gbp ", "GBP")]
    [InlineData("EUR", "EUR")]
    public void La_divisa_se_guarda_con_su_codigo_en_mayusculas_y_sin_blancos(
        string escrita, string guardada)
    {
        Tarifa tarifa = Abrir(TipoDeTarifa.Venta, escrita);

        tarifa.Divisa.ShouldBe(
            guardada,
            "la divisa se compara con la base de la empresa, que se guarda normalizada: una «usd» " +
            "en minúsculas no sería nunca la misma");
    }

    [Theory]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("")]
    public void Una_divisa_sin_forma_de_codigo_ISO_no_abre_una_tarifa(string divisa)
    {
        Should.Throw<ArgumentException>(() => Abrir(TipoDeTarifa.Venta, divisa));
    }

    private static Tarifa Abrir(TipoDeTarifa tipo, string divisa) =>
        Tarifa.Crear(
            s_empresa, "PVP", "Precio de venta al público", tipo, divisa, s_hoy, null, s_ahora);
}
