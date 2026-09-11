using Bastion.Catalogo.Domain.Catalogo;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// Precio o descuento, y los <b>dos</b> negativos: ni los dos puestos, ni ninguno.
/// </summary>
/// <remarks>
/// <para>
/// <b>El segundo negativo es el que se olvida, y es el que produce el precio cero por la puerta de
/// atrás.</b> Que no se puedan poner los dos es evidente en cuanto se escribe el tipo —el orden en
/// que se aplicarían no lo dice nadie— y por eso casi siempre está. Que no se pueda dejar ninguno
/// parece inofensivo: una fila «sin precio todavía». No lo es: esa fila casa con el artículo, gana
/// la precedencia y devuelve un importe que nadie escribió, y el descuadre aparece semanas después
/// sin autor.
/// </para>
/// <para>
/// La comprobación está <b>dos veces</b> a propósito, y no sobra ninguna: aquí lanza, porque
/// llegar con el par mal formado es un programa mal escrito, y en <c>CrearLineaTarifa</c> contesta
/// un error de negocio con nombre, porque quien manda el par es un cliente HTTP. Es la puerta
/// doble del ADR-0004. Y hay una tercera, que ésta no puede ver: el CHECK de
/// <c>lineas_tarifa</c>, que es quien lo impide en filas que no pasaron por este constructor.
/// </para>
/// </remarks>
public sealed class PrecioODescuentoTests
{
    [Fact]
    public void Con_los_dos_puestos_no_se_construye()
    {
        ArgumentException error = Should.Throw<ArgumentException>(
            () => PrecioODescuento.De(10m, 5m));

        error.Message.ShouldContain("no los dos");
    }

    [Fact]
    public void Sin_ninguno_de_los_dos_tampoco()
    {
        // EL NEGATIVO QUE SE OLVIDA. Es la mutación 5: aceptar esto deja entrar una línea sin
        // precio ni descuento, y lo que sale de ella no es un error, es un cero.
        ArgumentException error = Should.Throw<ArgumentException>(
            () => PrecioODescuento.De(null, null));

        error.Message.ShouldNotBeNullOrWhiteSpace(
            "una línea sin precio y sin descuento se ha construido. No falla al guardarla ni al " +
            "leerla: falla al facturar, con un importe cero que nadie escribió");
    }

    [Fact]
    public void Un_precio_a_secas_deja_el_descuento_nulo_y_al_reves()
    {
        var dePrecio = PrecioODescuento.De(12.5m, null);
        dePrecio.Precio.ShouldBe(12.5m);
        dePrecio.DescuentoPorcentaje.ShouldBeNull();

        var deDescuento = PrecioODescuento.De(null, 15m);
        deDescuento.Precio.ShouldBeNull();
        deDescuento.DescuentoPorcentaje.ShouldBe(15m);
    }

    [Fact]
    public void El_precio_cero_SI_vale_porque_alguien_lo_escribio()
    {
        // Lo prohibido no es el cero: es el cero que nadie escribió. Una muestra comercial se
        // factura a cero a propósito, con su línea en el documento y su firma detrás.
        PrecioODescuento.DePrecio(0m).Precio.ShouldBe(0m);
        PrecioODescuento.DeDescuento(0m).DescuentoPorcentaje.ShouldBe(0m);
    }

    [Fact]
    public void Un_precio_negativo_no()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => PrecioODescuento.DePrecio(-0.000001m));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Un_descuento_fuera_del_cero_al_cien_tampoco(decimal porcentaje)
    {
        // Por encima de cien el importe cambiaría de signo y un negativo es un recargo: ninguno de
        // los dos se declara por aquí.
        Should.Throw<ArgumentOutOfRangeException>(() => PrecioODescuento.DeDescuento(porcentaje));
    }

    [Fact]
    public void El_precio_se_redondea_a_seis_decimales_y_el_descuento_a_dos()
    {
        // Las dos escalas son las de sus columnas, y se redondea AQUÍ y no al guardar: si lo
        // hiciera la base, el objeto en memoria y la fila dirían cosas distintas durante toda la
        // transacción que lo creó.
        PrecioODescuento.DePrecio(1.23456749m).Precio.ShouldBe(1.234567m);
        PrecioODescuento.DeDescuento(12.345m).DescuentoPorcentaje.ShouldBe(12.35m);
    }
}
