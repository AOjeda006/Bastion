using Bastion.BuildingBlocks.Domain.Identificacion;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Identificacion;

/// <summary>
/// El NIF de una empresa puede ser el de una persona jurídica (lo que se llamaba CIF) o el de
/// una persona física, porque un empresario individual tributa con su DNI o su NIE. Las tres
/// formas tienen carácter de control, y las tres se comprueban.
/// </summary>
public sealed class NifTests
{
    [Theory]
    // Persona física con DNI: 8 dígitos + letra de "TRWAGMYFPDXBNJZSQVHLCKE"[n % 23].
    [InlineData("12345678Z")]
    [InlineData("00000000T")]
    // Persona física con NIE: X/Y/Z valen 0/1/2 y luego es el cálculo del DNI.
    [InlineData("X1234567L")]
    [InlineData("Y1234567X")]
    [InlineData("Z1234567R")]
    // Persona jurídica, control numérico (letras A, B, E, H).
    [InlineData("A58818501")]
    [InlineData("B12345674")]
    // Persona jurídica, control alfabético (letras K, P, Q, R, S, N, W).
    [InlineData("P1234567D")]
    [InlineData("Q1234567D")]
    public void Un_identificador_con_el_caracter_de_control_correcto_se_acepta(string valor)
    {
        Nif.Intentar(valor, out Nif? nif).ShouldBeTrue($"«{valor}» debería ser válido");
        nif!.Valor.ShouldBe(valor);
    }

    [Theory]
    [InlineData("12345678A")]   // DNI con la letra cambiada
    [InlineData("X1234567M")]   // NIE con la letra cambiada
    [InlineData("A58818502")]   // CIF con el dígito de control cambiado
    [InlineData("P1234567E")]   // CIF de control alfabético con la letra cambiada
    public void Un_identificador_con_el_caracter_de_control_equivocado_se_rechaza(string valor)
    {
        Nif.Intentar(valor, out Nif? nif).ShouldBeFalse($"«{valor}» no debería ser válido");
        nif.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("1234567Z")]     // una posición de menos
    [InlineData("123456789Z")]   // una posición de más
    [InlineData("1234-678Z")]    // un carácter que no es ni letra ni dígito
    [InlineData("ZZZZZZZZZ")]    // nueve letras
    public void Lo_que_ni_siquiera_tiene_la_forma_de_un_NIF_se_rechaza(string? valor)
    {
        Nif.Intentar(valor, out Nif? nif).ShouldBeFalse();
        nif.ShouldBeNull();
    }

    [Fact]
    public void Se_normaliza_a_mayusculas_y_sin_espacios_ni_guiones()
    {
        Nif.Intentar(" 12345678-z ", out Nif? nif).ShouldBeTrue();
        nif!.Valor.ShouldBe("12345678Z");
    }

    [Fact]
    public void El_NIF_ocupa_exactamente_nueve_posiciones_y_por_eso_su_columna_lleva_tope()
    {
        // Es el ejemplo de libro de cuándo `varchar(n)` está justificado frente a `text`:
        // nueve no es una estimación nuestra, es la longitud del identificador.
        Nif.Longitud.ShouldBe(9);
        Nif.De("12345678Z").Valor.Length.ShouldBe(Nif.Longitud);
    }

    [Fact]
    public void De_lanza_cuando_el_valor_no_es_valido_porque_a_esas_alturas_ya_es_un_fallo_de_programa()
    {
        // `Intentar` es lo que usa la capa de aplicación para devolver un error POR CAMPO;
        // `De` es para cuando el valor ya viene comprobado (lectura de base de datos, por
        // ejemplo). Un `De` que falla es un error de programación, no de negocio (ADR-0004).
        Should.Throw<ArgumentException>(() => Nif.De("12345678A"));
    }

    [Fact]
    public void Dos_NIF_con_el_mismo_valor_son_el_mismo_NIF()
    {
        Nif.De("12345678Z").ShouldBe(Nif.De("12345678z"));
    }
}
