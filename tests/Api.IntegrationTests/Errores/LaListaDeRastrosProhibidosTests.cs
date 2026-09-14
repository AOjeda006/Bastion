using Shouldly;

namespace Bastion.Api.IntegrationTests.Errores;

/// <summary>
/// La regla sobre la lista de rastros prohibidos, que es arnés y por eso necesita la suya.
/// </summary>
/// <remarks>
/// Sin <c>Category=Integracion</c>: mira una lista de cadenas y no abre ninguna conexión, así que
/// corre en el carril rápido, que es donde se nota antes que alguien ha vuelto a escribir un
/// rastro que el azar puede fabricar.
/// </remarks>
public sealed class LaListaDeRastrosProhibidosTests
{
    [Fact]
    public void Ningun_rastro_prohibido_cabe_en_un_identificador_aleatorio()
    {
        string[] fabricables = RastrosProhibidos.Todos.Where(CabeEnUnIdentificadorAleatorio).ToArray();

        fabricables.ShouldBeEmpty(
            "estos rastros los puede fabricar un traceId o un GUID, así que tiran un dado en cada "
            + "respuesta: " + string.Join(", ", fabricables));
    }

    [Fact]
    public void La_regla_de_la_lista_puede_dispararse()
    {
        // Sin este canario, la regla de arriba no distingue una lista limpia de un predicado que
        // no caza nada. El rastro que tumbó la batería del 1.11 tiene que caer, y su arreglo no.
        CabeEnUnIdentificadorAleatorio("5432").ShouldBeTrue();
        CabeEnUnIdentificadorAleatorio(":5432").ShouldBeFalse();
    }

    // Un traceId son treinta y dos cifras hexadecimales y un GUID lleva además guiones; las dos
    // cosas salen en respuestas buenas. Un rastro hecho solo de esos caracteres aparece ahí por
    // azar tarde o temprano, y entonces el rojo no dice nada de una fuga.
    private static bool CabeEnUnIdentificadorAleatorio(string rastro) =>
        rastro.All(caracter => Uri.IsHexDigit(caracter) || caracter == '-');
}
