using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Terceros.Domain.Terceros;
using Bastion.Terceros.UnitTests.Identificacion;
using Shouldly;

namespace Bastion.Terceros.UnitTests.Terceros;

/// <summary>
/// El identificador de un tercero son tres campos: país, número y cuánto se sabe de que ese
/// número sea el que dice ser.
/// </summary>
public sealed class IdentificacionFiscalTests
{
    // Generado, no pegado: la letra sale del resto entre 23. Ver `IdentificadoresInventados`.
    private static readonly Nif s_nifInventado =
        Nif.De(IdentificadoresInventados.Dni(12_345_678).Valido);

    [Fact]
    public void La_espanola_nace_verificada_porque_su_letra_se_ha_comprobado()
    {
        var identificacion = IdentificacionFiscal.Espanola(s_nifInventado);

        identificacion.Pais.ShouldBe("ES");
        identificacion.EsEspanola.ShouldBeTrue();
        identificacion.Verificacion.ShouldBe(EstadoDeVerificacion.VerificadoPorAlgoritmo);
    }

    /// <summary>
    /// Lo que no se puede validar se marca como no validado; no se da por bueno.
    /// </summary>
    /// <remarks>
    /// Es la mitad del criterio del ítem que se pierde si el estado fuera un parámetro: quien da
    /// el alta pondría «verificado» porque le consta, y el maestro quedaría con la mitad de las
    /// fichas pareciendo comprobadas sin serlo. No hay forma de pedir otra cosa, y por eso este
    /// test no tiene un gemelo que intente lo contrario: no compilaría.
    /// </remarks>
    [Fact]
    public void La_extranjera_nace_sin_verificar_y_no_hay_manera_de_pedir_otra_cosa()
    {
        var identificacion = IdentificacionFiscal.Extranjera("pt", "000 111 222");

        identificacion.Pais.ShouldBe("PT");
        identificacion.Numero.ShouldBe("000111222");
        identificacion.EsEspanola.ShouldBeFalse();
        identificacion.Verificacion.ShouldBe(EstadoDeVerificacion.NoVerificado);
    }

    /// <summary>
    /// «ES» no entra por la puerta de lo extranjero, que es la que no valida.
    /// </summary>
    /// <remarks>
    /// Sin esto, el criterio «NIF, NIE o CIF validados de verdad» se cae sin que nada se ponga
    /// rojo: bastaría con declarar español un identificador con la letra mal diciendo que es
    /// extranjero. Es la única puerta por la que se podía hacer.
    /// </remarks>
    [Theory]
    [InlineData("ES")]
    [InlineData("es")]
    [InlineData(" Es ")]
    public void Espana_no_entra_por_la_puerta_de_lo_que_no_se_valida(string pais)
    {
        Should.Throw<ArgumentException>(
            () => IdentificacionFiscal.Extranjera(pais, s_nifInventado.Valor));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("E")]
    [InlineData("ESP")]
    [InlineData("E5")]
    public void El_pais_que_no_es_un_ISO_3166_de_dos_letras_no_se_normaliza(string? pais)
    {
        IdentificacionFiscal.PaisNormalizado(pais).ShouldBeNull();
    }

    [Theory]
    [InlineData(" fr ", "FR")]
    [InlineData("de", "DE")]
    public void El_pais_se_guarda_en_mayusculas_y_sin_espacios(string escrito, string guardado)
    {
        IdentificacionFiscal.PaisNormalizado(escrito).ShouldBe(guardado);
    }

    /// <summary>
    /// El número se normaliza igual en los dos sitios donde se usa: al guardar y al preguntar si
    /// ya existe.
    /// </summary>
    /// <remarks>
    /// Es la razón por la que <c>NumeroNormalizado</c> es pública. Sobre esta forma hay un índice
    /// único; quien pregunte por lo que escribió el usuario dejaría pasar «fr 123 456», chocaría
    /// contra el índice y devolvería un 500 en vez del conflicto que es.
    /// </remarks>
    [Theory]
    [InlineData(" fr 123-456 ", "FR123456")]
    [InlineData("de.123.456", "DE123456")]
    [InlineData(" gb 12ab34 ", "GB12AB34")]
    public void El_numero_se_guarda_sin_ruido_de_teclado_y_en_mayusculas(
        string escrito,
        string guardado)
    {
        IdentificacionFiscal.NumeroNormalizado(escrito).ShouldBe(guardado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("123456789012345678901")]   // veintiuna posiciones: una de más
    public void Lo_que_no_deja_nada_utilizable_no_se_normaliza(string? numero)
    {
        IdentificacionFiscal.NumeroNormalizado(numero).ShouldBeNull();
    }

    /// <summary>
    /// Dos identificaciones con los mismos tres campos son la misma, que es lo que un tipo
    /// complejo necesita para compararse miembro a miembro.
    /// </summary>
    [Fact]
    public void Dos_identificaciones_iguales_son_la_misma()
    {
        IdentificacionFiscal.Extranjera("FR", "123456")
            .ShouldBe(IdentificacionFiscal.Extranjera("fr", "12-34-56"));
    }

    [Fact]
    public void La_espanola_y_la_extranjera_con_el_mismo_numero_no_son_la_misma()
    {
        // El país es parte de la identidad, no un adorno: sin él, dos terceros que no tienen nada
        // que ver chocarían contra el índice único y el segundo recibiría un conflicto
        // incomprensible.
        IdentificacionFiscal.Espanola(s_nifInventado)
            .ShouldNotBe(IdentificacionFiscal.Extranjera("PT", s_nifInventado.Valor));
    }
}
