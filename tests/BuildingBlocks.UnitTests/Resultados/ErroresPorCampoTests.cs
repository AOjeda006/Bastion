using Bastion.BuildingBlocks.Domain.Resultados;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Resultados;

/// <summary>
/// El §9 pide «errores por campo en validación». El ADR-0004 ya dejó dicho dónde entrarían
/// cuando llegasen: como extensión <c>errors</c> del MISMO <c>ProblemDetails</c>, no como un
/// formato de error aparte. Esto es esa llegada.
/// </summary>
public sealed class ErroresPorCampoTests
{
    [Fact]
    public void Un_error_de_validacion_normal_no_lleva_campos()
    {
        // Que la lista esté vacía y no sea nula importa: el borde recorre `Campos` sin
        // comprobar nada, y un nulo aquí sería una excepción en la ruta de error, que es
        // el peor sitio posible para tener una.
        var error = ErrorDeOperacion.Validacion("peticion-mal-formada", "Revise el cuerpo.");

        error.Campos.ShouldBeEmpty();
    }

    [Fact]
    public void Un_error_por_campo_dice_que_campo_y_por_que()
    {
        var error = ErrorDeOperacion.Validacion(
            "datos-no-validos",
            "Revise los campos indicados.",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["nif"] = ["El NIF no tiene un carácter de control válido."],
                ["divisaBase"] = ["No se conoce el redondeo de esa divisa."],
            });

        error.Tipo.ShouldBe(TipoDeError.Validacion);
        error.Campos.Count.ShouldBe(2);
        error.Campos["nif"].ShouldHaveSingleItem();
        error.Campos["divisaBase"][0].ShouldBe("No se conoce el redondeo de esa divisa.");
    }

    [Fact]
    public void Un_campo_puede_incumplir_varias_reglas_a_la_vez()
    {
        // Devolver solo el primer fallo obliga al usuario a corregir, reenviar y descubrir el
        // siguiente. Un formulario se corrige entero de una vez o no se corrige.
        var error = ErrorDeOperacion.Validacion(
            "datos-no-validos",
            "Revise los campos indicados.",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["codigo"] = ["Es obligatorio.", "No puede pasar de 20 caracteres."],
            });

        error.Campos["codigo"].Count.ShouldBe(2);
    }

    [Fact]
    public void Los_campos_de_un_error_no_se_pueden_cambiar_despues()
    {
        // El error viaja desde el caso de uso hasta el borde. Si el diccionario que se le pasó
        // siguiera siendo el mismo objeto, quien lo construyó podría seguir tocándolo y lo que
        // se publica dejaría de ser lo que se decidió.
        var mutable = new Dictionary<string, IReadOnlyList<string>>
        {
            ["nif"] = ["No es válido."],
        };

        var error = ErrorDeOperacion.Validacion("datos-no-validos", "Revise.", mutable);
        mutable["razonSocial"] = ["Colada después."];

        error.Campos.Count.ShouldBe(1);
        error.Campos.ContainsKey("razonSocial").ShouldBeFalse();
    }

    [Fact]
    public void Solo_la_validacion_admite_campos()
    {
        // Un 404 o un 409 no son de un campo: son del recurso entero. Ofrecer la sobrecarga
        // para todos invitaría a inventarse nombres de campo donde no los hay.
        var conflicto = ErrorDeOperacion.Conflicto("serie-ya-numerada", "Ya ha numerado.");

        conflicto.Campos.ShouldBeEmpty();
    }
}
