using Bastion.BuildingBlocks.Domain.Bloqueos;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Bloqueos;

/// <summary>
/// Las respuestas escritas a las dos preguntas incómodas de una transición: qué pasa al bloquear
/// lo que ya está bloqueado, y al desbloquear lo que no lo está.
/// </summary>
/// <remarks>
/// Estaban contestadas tres veces —en <c>Empresa</c>, en <c>Almacen</c> y en <c>Usuario</c>— y no
/// necesariamente igual. Aquí se contestan una vez, y las tres entidades delegan.
/// </remarks>
public sealed class BloqueoTests
{
    private static readonly DateTimeOffset s_momento = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ninguno_es_lo_que_lleva_encima_una_ficha_que_nunca_se_bloqueo()
    {
        var bloqueo = Bloqueo.Ninguno();

        bloqueo.EstaBloqueado.ShouldBeFalse();
        bloqueo.Desde.ShouldBeNull();
        bloqueo.Motivo.ShouldBeNull();
    }

    [Fact]
    public void Ninguno_es_un_METODO_y_no_una_instancia_compartida()
    {
        // Parece un detalle y no lo es. Un `static readonly Bloqueo Ninguno` daría UNA instancia
        // para todas las fichas del sistema, y EF Core la vería como el valor de un tipo complejo
        // de cada una de ellas. Con un tipo complejo eso no rompe hoy —se copia al materializar—,
        // pero es exactamente la clase de estado compartido del que luego cuesta salir.
        Bloqueo.Ninguno().ShouldNotBeSameAs(Bloqueo.Ninguno());

        // Y aun así son IGUALES, porque es un `record`: comparar bloqueos compara lo que dicen,
        // no dónde están.
        Bloqueo.Ninguno().ShouldBe(Bloqueo.Ninguno());
    }

    [Fact]
    public void Bloquear_deja_el_motivo_y_el_instante()
    {
        Bloqueo bloqueo = Bloqueo.Ninguno().Bloquear(MotivoDeBloqueo.SupresionSolicitada, s_momento);

        bloqueo.EstaBloqueado.ShouldBeTrue();
        bloqueo.Desde.ShouldBe(s_momento);
        bloqueo.Motivo.ShouldBe(MotivoDeBloqueo.SupresionSolicitada);
    }

    [Fact]
    public void Bloquear_lo_ya_bloqueado_devuelve_el_bloqueo_de_antes_ENTERO()
    {
        // La respuesta escrita a la primera pregunta. No es un error —el `DELETE` de la API tiene
        // que ser idempotente, y repetirlo por un reintento de red no puede fallar— pero tampoco
        // es un cambio: de la fecha del PRIMER bloqueo cuelga el plazo de prescripción del art. 32
        // de la LOPDGDD, así que moverla alargaría la conservación de datos personales sin que
        // nadie lo hubiera decidido.
        //
        // Y el motivo tampoco se pisa: el segundo bloqueo puede venir con otro, y el que explica
        // por qué esos datos están reservados es el primero.
        Bloqueo primero = Bloqueo.Ninguno().Bloquear(MotivoDeBloqueo.SupresionSolicitada, s_momento);

        Bloqueo segundo = primero.Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento.AddDays(30));

        segundo.ShouldBeSameAs(primero);
    }

    [Fact]
    public void Desbloquear_lo_que_no_esta_bloqueado_no_es_un_error()
    {
        // La respuesta escrita a la segunda. Lanzar aquí obligaría a todo el que desbloquea a
        // preguntar antes si hace falta, y esa pregunta y la acción no son atómicas: entre una y
        // otra el estado puede cambiar. Devolver lo mismo hace que la operación sea idempotente,
        // que es lo que el `POST /desbloqueo` promete.
        var sinBloquear = Bloqueo.Ninguno();

        sinBloquear.Desbloquear().ShouldBeSameAs(sinBloquear);
    }

    [Fact]
    public void Desbloquear_borra_las_tres_cosas_y_no_solo_la_bandera()
    {
        // Dejar la fecha y el motivo puestos con la bandera en falso sería un cuarto estado que
        // nadie ha definido: «activa, pero estuvo bloqueada». Si algún día hace falta saber eso,
        // el sitio es la traza de auditoría, que ya guarda los dos cambios con quién y cuándo.
        Bloqueo bloqueado = Bloqueo.Ninguno().Bloquear(MotivoDeBloqueo.SupresionSolicitada, s_momento);

        Bloqueo desbloqueado = bloqueado.Desbloquear();

        desbloqueado.EstaBloqueado.ShouldBeFalse();
        desbloqueado.Desde.ShouldBeNull();
        desbloqueado.Motivo.ShouldBeNull();
    }

    [Fact]
    public void La_guarda_lanza_cuando_esta_bloqueado_y_dice_de_quien_habla_y_por_que()
    {
        Bloqueo bloqueado = Bloqueo.Ninguno().Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento);

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() =>
            bloqueado.ExigirQueNoEsteBloqueado("El almacén", "está retirado de la operativa"));

        // El sujeto lo pone quien llama, concordado, y por eso el mensaje se lee. Con un texto
        // armado aquí a partir del nombre del tipo saldría «Un almacén bloqueada».
        error.Message.ShouldBe("El almacén no admite cambios: está retirado de la operativa.");
    }

    [Fact]
    public void La_guarda_deja_pasar_lo_que_no_esta_bloqueado()
    {
        Should.NotThrow(() =>
            Bloqueo.Ninguno().ExigirQueNoEsteBloqueado("La empresa", "da igual, no está bloqueada"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void La_guarda_no_admite_un_sujeto_ni_un_motivo_en_blanco(string vacio)
    {
        // Un mensaje a medias —« no admite cambios: .»— es peor que no tenerlo: aparece en el
        // ProblemDetails que lee quien usa la API, y ahí no hay nadie que pueda ir al código a
        // averiguar de qué hablaba.
        Should.Throw<ArgumentException>(() =>
            Bloqueo.Ninguno().ExigirQueNoEsteBloqueado(vacio, "un motivo"));

        Should.Throw<ArgumentException>(() =>
            Bloqueo.Ninguno().ExigirQueNoEsteBloqueado("Un sujeto", vacio));
    }

    [Fact]
    public void Los_motivos_de_bloqueo_son_dos_y_estan_enumerados()
    {
        // Lista cerrada, como la de los motivos para saltarse el filtro. Un `string` dejaría
        // bloquear «por lo que sea», y el motivo es lo que permite responder años después por qué
        // esos datos siguen guardados: la supresión del art. 32 obliga a conservarlos, y el cese
        // de uso de un almacén obliga a conservar su valoración histórica. No son lo mismo y no
        // caducan igual.
        Enum.GetValues<MotivoDeBloqueo>().ShouldBe(
            [MotivoDeBloqueo.SupresionSolicitada, MotivoDeBloqueo.CeseDeUso]);
    }
}
