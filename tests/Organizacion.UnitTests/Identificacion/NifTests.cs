using Bastion.BuildingBlocks.Domain.Identificacion;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Identificacion;

/// <summary>
/// El NIF de una empresa puede ser el de una persona jurídica (lo que se llamaba CIF) o el de
/// una persona física, porque un empresario individual tributa con su DNI o su NIE. Las tres
/// formas tienen carácter de control, y las tres se comprueban.
/// </summary>
/// <remarks>
/// <b>Ni un identificador fiscal real, y por eso todos tienen esta pinta.</b> Un NIF real es un
/// dato personal, y en una fixture no se borra nunca: queda en el fichero, en el artefacto de
/// resultados y en el registro de la CI. Los de aquí son números inventados —ceros y nueves— con
/// su carácter de control <b>calculado</b>, no ejemplos pegados de una página web: el «12345678Z»
/// de todos los manuales es un DNI que existe, y el CIF que había aquí antes era el de una empresa
/// de verdad. La batería exhaustiva, generada y recorrida en los dos sentidos, está en
/// <c>Bastion.Terceros.UnitTests.Identificacion.LaBateriaGeneradaTests</c>.
/// </remarks>
public sealed class NifTests
{
    [Theory]
    // Persona física con DNI: 8 dígitos + letra de "TRWAGMYFPDXBNJZSQVHLCKE"[n % 23].
    [InlineData("00000001R")]
    [InlineData("00000000T")]
    // Persona física con NIE: X/Y/Z valen 0/1/2 y luego es el cálculo del DNI.
    [InlineData("X0000001R")]
    [InlineData("Y0000001S")]
    [InlineData("Z0000001Y")]
    // Persona jurídica, control numérico (letras A, B, E, H).
    [InlineData("A99999997")]
    [InlineData("B99999997")]
    // Persona jurídica, control alfabético (letras K, P, Q, R, S, N, W).
    [InlineData("P9999999G")]
    [InlineData("Q9999999G")]
    public void Un_identificador_con_el_caracter_de_control_correcto_se_acepta(string valor)
    {
        Nif.Intentar(valor, out Nif? nif).ShouldBeTrue($"«{valor}» debería ser válido");
        nif!.Valor.ShouldBe(valor);
    }

    [Theory]
    [InlineData("00000001W")]   // DNI con la letra cambiada
    [InlineData("X0000001S")]   // NIE con la letra cambiada
    [InlineData("A99999998")]   // CIF con el dígito de control cambiado
    [InlineData("P9999999H")]   // CIF de control alfabético con la letra cambiada
    // Y los dos que NO son «la letra cambiada» sino «la clase cambiada»: el carácter de control
    // vale lo que tiene que valer, pero en la forma que esa inicial no admite. Es el fallo típico
    // de las implementaciones que aceptan las dos formas para todas las iniciales, y sin estos dos
    // casos una que lo hiciera pasaría toda esta batería:
    //   · A99999997 controla con el dígito 7; «G» es esa misma posición en JABCDEFGHI, y la
    //     inicial A es de control SIEMPRE numérico, así que A9999999G se rechaza.
    //   · P9999999G controla con la letra G; «7» es esa misma posición, y la inicial P es de
    //     control SIEMPRE alfabético, así que P99999997 se rechaza.
    [InlineData("A9999999G")]   // control numérico escrito como letra
    [InlineData("P99999997")]   // control alfabético escrito como dígito
    public void Un_identificador_con_el_caracter_de_control_equivocado_se_rechaza(string valor)
    {
        Nif.Intentar(valor, out Nif? nif).ShouldBeFalse($"«{valor}» no debería ser válido");
        nif.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("0000001R")]     // una posición de menos
    [InlineData("000000001R")]   // una posición de más
    [InlineData("0000-001R")]    // un carácter que no es ni letra ni dígito
    [InlineData("ZZZZZZZZZ")]    // nueve letras
    public void Lo_que_ni_siquiera_tiene_la_forma_de_un_NIF_se_rechaza(string? valor)
    {
        Nif.Intentar(valor, out Nif? nif).ShouldBeFalse();
        nif.ShouldBeNull();
    }

    [Fact]
    public void Se_normaliza_a_mayusculas_y_sin_espacios_ni_guiones()
    {
        Nif.Intentar(" 00000001-r ", out Nif? nif).ShouldBeTrue();
        nif!.Valor.ShouldBe("00000001R");
    }

    [Fact]
    public void El_NIF_ocupa_exactamente_nueve_posiciones_y_por_eso_su_columna_lleva_tope()
    {
        // Es el ejemplo de libro de cuándo `varchar(n)` está justificado frente a `text`:
        // nueve no es una estimación nuestra, es la longitud del identificador.
        Nif.Longitud.ShouldBe(9);
        Nif.De("00000001R").Valor.Length.ShouldBe(Nif.Longitud);
    }

    [Fact]
    public void De_lanza_cuando_el_valor_no_es_valido_porque_a_esas_alturas_ya_es_un_fallo_de_programa()
    {
        // `Intentar` es lo que usa la capa de aplicación para devolver un error POR CAMPO;
        // `De` es para cuando el valor ya viene comprobado (lectura de base de datos, por
        // ejemplo). Un `De` que falla es un error de programación, no de negocio (ADR-0004).
        Should.Throw<ArgumentException>(() => Nif.De("00000001W"));
    }

    [Fact]
    public void Dos_NIF_con_el_mismo_valor_son_el_mismo_NIF()
    {
        Nif.De("00000001R").ShouldBe(Nif.De("00000001r"));
    }
}
