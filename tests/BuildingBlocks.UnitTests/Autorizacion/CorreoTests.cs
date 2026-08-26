using Bastion.BuildingBlocks.Domain.Identificacion;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Autorizacion;

public sealed class CorreoTests
{
    [Theory]
    [InlineData("ana@ejemplo.es")]
    [InlineData("ana.lopez@ejemplo.co.uk")]
    [InlineData("ana+facturas@ejemplo.es")]
    [InlineData("a@b.cd")]
    public void De_ConUnCorreoRazonable_LoAcepta(string texto) =>
        Correo.De(texto).Valor.ShouldBe(texto);

    // El correo identifica al usuario, así que su normalización decide si dos altas son la misma
    // persona. Se recorta y se pasa a minúsculas ANTES de comparar; si no, `Ana@ejemplo.es` y
    // `ana@ejemplo.es` serían dos cuentas y el índice único no lo impediría.
    [Theory]
    [InlineData("  ana@ejemplo.es  ", "ana@ejemplo.es")]
    [InlineData("Ana.Lopez@Ejemplo.ES", "ana.lopez@ejemplo.es")]
    public void De_Normaliza_RecortandoYEnMinusculas(string entrada, string esperado) =>
        Correo.De(entrada).Valor.ShouldBe(esperado);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]                    // sin arroba
    [InlineData("ana@")]                   // sin dominio
    [InlineData("@ejemplo.es")]            // sin parte local
    [InlineData("ana@ejemplo")]            // dominio sin punto
    [InlineData("ana@@ejemplo.es")]        // dos arrobas
    [InlineData("an a@ejemplo.es")]        // espacio
    [InlineData("ana@ejemplo.es.")]        // dominio acabado en punto
    [InlineData("ana@.ejemplo.es")]        // dominio empezado en punto
    public void De_ConCualquierOtraCosa_Lanza(string texto) =>
        Should.Throw<ArgumentException>(() => Correo.De(texto));

    [Fact]
    public void De_ConMasDeDoscientosCincuentaYCuatro_Lanza()
    {
        // El límite de la RFC 5321 para el camino de retorno. Se comprueba porque es el que
        // acaba siendo la longitud de la columna, y una columna corta trunca en silencio.
        string largo = new string('a', 250) + "@ejemplo.es";

        Should.Throw<ArgumentException>(() => Correo.De(largo));
    }

    [Fact]
    public void Intentar_NoLanza_YEsLaPuertaDelBorde()
    {
        Correo.Intentar("ana", out Correo? malo).ShouldBeFalse();
        malo.ShouldBeNull();

        Correo.Intentar("  ANA@ejemplo.es ", out Correo? bueno).ShouldBeTrue();
        bueno!.Valor.ShouldBe("ana@ejemplo.es");
    }

    [Fact]
    public void DosCorreosIguales_SonElMismo() =>
        Correo.De("ana@ejemplo.es").ShouldBe(Correo.De("ANA@EJEMPLO.ES"));
}
