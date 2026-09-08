using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Bastion.Organizacion.Contracts.Comun;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// Los tres valores de <see cref="EstadoDeMaestro"/> significando tres cosas distintas en un
/// camino de negocio, que es el único sitio donde pueden significar algo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Catálogo es el consumidor para el que se construyó la retirada.</b> El ADR-0023 la decidió
/// en el ítem 1.7 y la dejó sin un solo consumidor: los cuatro maestros de instalación se podían
/// retirar y nadie preguntaba por su estado, así que el enumerado tenía tres valores y dos de
/// ellos no cambiaban el desenlace de nada. Un enumerado cuyos valores no se ramifican en ninguna
/// parte es una lista de palabras.
/// </para>
/// <para>
/// <b>Y son dos mitades, no una.</b> «Se retira» no quiere decir «desaparece»: quiere decir que no
/// se ofrece <i>para lo nuevo</i> y que sigue resolviendo <i>lo viejo</i>. Probar solo la primera
/// mitad dejaría pasar la implementación que las confunde —rechazar también al que ya la usa—, que
/// es peor que no tener retirada: convierte retirar una unidad en congelar todas las fichas que la
/// usan.
/// </para>
/// <para>
/// Los dos rechazos, además, tienen que ser <b>distinguibles</b>. «No existe» es un identificador
/// mal escrito y se arregla corrigiéndolo; «está retirada» es un identificador correcto de algo
/// que ya no se ofrece, y se arregla eligiendo otra cosa. Un solo <c>type</c> para los dos dejaría
/// al frontal sin poder decir cuál de las dos cosas ha pasado (ADR-0030).
/// </para>
/// </remarks>
public sealed class LaCasillaDeLaRetiradaTests
{
    private static readonly Guid s_empresa = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_unidad = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid s_impuesto = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DateTimeOffset s_momento =
        new(2026, 9, 8, 10, 30, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ la unidad, tres casillas

    [Fact]
    public async Task Con_la_unidad_ofreciendose_para_lo_nuevo_el_alta_pasa()
    {
        ArticulosEnMemoria articulos = new();
        UnidadesEn unidades = new(EstadoDeMaestro.SeOfreceParaLoNuevo);

        Resultado<ArticuloDto> resultado = await AltaAsync(articulos, unidades: unidades);

        resultado.EsCorrecto.ShouldBeTrue();
        articulos.Guardados.Count.ShouldBe(1);
        articulos.Guardados[0].UnidadBaseId.ShouldBe(s_unidad);

        // Que haya pasado no basta: tiene que haber pasado HABIENDO PREGUNTADO. Un alta que no
        // llamara al puerto también saldría correcta, y este caso solo la distingue así.
        unidades.Preguntadas.ShouldBe([s_unidad]);
    }

    [Fact]
    public async Task Con_la_unidad_solo_resolviendo_lo_viejo_el_alta_se_rechaza()
    {
        ArticulosEnMemoria articulos = new();

        Resultado<ArticuloDto> resultado = await AltaAsync(
            articulos, unidades: new UnidadesEn(EstadoDeMaestro.SoloResuelveLoViejo));

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-unidad-retirada");

        // Conflicto y no validación: el identificador que llegó es correcto y apunta a algo que
        // existe. Lo que falla no es cómo está escrita la petición, es el estado del sistema.
        resultado.Error.Tipo.ShouldBe(TipoDeError.Conflicto);

        articulos.Guardados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Con_la_unidad_inexistente_el_alta_se_rechaza_con_OTRO_error()
    {
        Resultado<ArticuloDto> retirada = await AltaAsync(
            new ArticulosEnMemoria(), unidades: new UnidadesEn(EstadoDeMaestro.SoloResuelveLoViejo));

        Resultado<ArticuloDto> inexistente = await AltaAsync(
            new ArticulosEnMemoria(), unidades: new UnidadesEn(EstadoDeMaestro.NoExiste));

        inexistente.EsCorrecto.ShouldBeFalse();
        inexistente.Error!.Codigo.ShouldBe("articulo-unidad-no-encontrada");
        inexistente.Error.Tipo.ShouldBe(TipoDeError.Validacion);

        // La afirmación que de verdad importa de este caso, y por eso se comparan los dos: que
        // sean DISTINGUIBLES. Con el mismo código, el frontal no podría decirle a quien da el alta
        // si tiene que corregir el identificador o elegir otra unidad (ADR-0030).
        inexistente.Error.Codigo.ShouldNotBe(retirada.Error!.Codigo);
        inexistente.Error.Tipo.ShouldNotBe(retirada.Error.Tipo);
    }

    // ----------------------------------------------------------------- el impuesto, tres casillas

    [Fact]
    public async Task Con_el_tramo_vigente_el_alta_pasa_y_se_pregunta_por_el_dia_de_hoy()
    {
        ImpuestosEn impuestos = new(EstadoDeMaestro.SeOfreceParaLoNuevo);

        Resultado<ArticuloDto> resultado = await AltaAsync(
            new ArticulosEnMemoria(), impuestos: impuestos);

        resultado.EsCorrecto.ShouldBeTrue();

        // El devengo con el que se pregunta es el día del alta, en UTC. Que se pregunte por una
        // FECHA y no solo por un identificador es lo que separa a este puerto del de unidades: un
        // tramo no está retirado o no, está vigente o no EN UN DÍA.
        impuestos.Preguntados.ShouldBe([(s_impuesto, new DateOnly(2026, 9, 8))]);
    }

    [Fact]
    public async Task Con_el_tramo_fuera_de_vigencia_el_alta_se_rechaza()
    {
        Resultado<ArticuloDto> resultado = await AltaAsync(
            new ArticulosEnMemoria(), impuestos: new ImpuestosEn(EstadoDeMaestro.SoloResuelveLoViejo));

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-impuesto-no-vigente");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Conflicto);

        // El mensaje dice la fecha con la que se preguntó, que es lo que hace que quien lo lee
        // pueda entender por qué le han dicho que no.
        resultado.Error.Mensaje.ShouldContain("2026-09-08");
    }

    [Fact]
    public async Task Con_el_tramo_inexistente_el_alta_se_rechaza_con_OTRO_error()
    {
        Resultado<ArticuloDto> resultado = await AltaAsync(
            new ArticulosEnMemoria(), impuestos: new ImpuestosEn(EstadoDeMaestro.NoExiste));

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-impuesto-no-encontrado");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Validacion);
    }

    // --------------------------------------------------------------------------- la otra mitad

    [Fact]
    public async Task El_articulo_que_YA_usa_una_unidad_retirada_se_sigue_pudiendo_corregir()
    {
        // La segunda mitad de «SoloResuelveLoViejo», y la que se olvida. La unidad de este
        // artículo se retiró AYER; su descripción tenía una errata y hay que corregirla hoy.
        ArticulosEnMemoria articulos = new();
        var articulo = Articulo.Crear(
            s_empresa, "TORN-8", "Tornillo de 8", TipoDeArticulo.Bien,
            s_unidad, s_impuesto, null, s_momento);
        articulos.Guardados.Add(articulo);

        ConfirmacionesContadas confirmaciones = new();

        // El puerto de unidades contesta que está retirada, y da igual: no se le pregunta. La
        // unidad base no está en el DTO de modificación y no se puede cambiar.
        UnidadesEn unidades = new(EstadoDeMaestro.SoloResuelveLoViejo);

        var modificar = new ModificarArticulo(
            articulos,
            new CategoriasEnMemoria(),
            new ImpuestosEn(EstadoDeMaestro.SeOfreceParaLoNuevo),
            confirmaciones,
            new VersionesQueDanIgual(),
            new RelojParado(s_momento));

        Resultado<ArticuloDto> resultado = await modificar.EjecutarAsync(
            articulo.Id,
            new BuildingBlocks.Application.Concurrencia.VersionDeRecurso(1),
            new ModificarArticuloDto
            {
                Descripcion = "Tornillo de 8 mm",
                Tipo = nameof(TipoDeArticulo.Bien),
                ImpuestoPorDefectoId = s_impuesto,
                CategoriaId = null,
            },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue(
            "retirar una unidad no puede congelar las fichas que ya la usaban: eso no es " +
            "retirarla, es borrarla a medias");

        articulo.Descripcion.ShouldBe("Tornillo de 8 mm");
        articulo.UnidadBaseId.ShouldBe(s_unidad, "la unidad base no se cambia, ni siquiera a otra");
        confirmaciones.Veces.ShouldBe(1);
        unidades.Preguntadas.ShouldBeEmpty("no hay por qué preguntar por lo que no se puede cambiar");
    }

    [Fact]
    public async Task El_impuesto_que_no_se_toca_no_se_vuelve_a_preguntar()
    {
        // El mismo argumento, un paso más fino: el impuesto SÍ se puede cambiar, así que la única
        // manera de no congelar la ficha es preguntar solo cuando cambia. Con el tramo ya
        // derogado, corregir la descripción tiene que seguir siendo posible.
        ArticulosEnMemoria articulos = new();
        var articulo = Articulo.Crear(
            s_empresa, "TORN-8", "Tornillo de 8", TipoDeArticulo.Bien,
            s_unidad, s_impuesto, null, s_momento);
        articulos.Guardados.Add(articulo);

        ImpuestosEn impuestos = new(EstadoDeMaestro.SoloResuelveLoViejo);

        var modificar = new ModificarArticulo(
            articulos,
            new CategoriasEnMemoria(),
            impuestos,
            new ConfirmacionesContadas(),
            new VersionesQueDanIgual(),
            new RelojParado(s_momento));

        Resultado<ArticuloDto> resultado = await modificar.EjecutarAsync(
            articulo.Id,
            new BuildingBlocks.Application.Concurrencia.VersionDeRecurso(1),
            new ModificarArticuloDto
            {
                Descripcion = "Tornillo de 8 mm",
                Tipo = nameof(TipoDeArticulo.Bien),
                ImpuestoPorDefectoId = s_impuesto,
                CategoriaId = null,
            },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        impuestos.Preguntados.ShouldBeEmpty(
            "el impuesto no ha cambiado, así que no se revalida. Revalidarlo convertiría la " +
            "derogación de un tramo en un congelador para toda ficha que lo propusiera");
    }

    [Fact]
    public async Task Pero_cambiar_el_impuesto_a_uno_derogado_SI_se_rechaza()
    {
        // La contrapartida, y sin ella el caso de arriba sería «no se valida nunca». Lo que se
        // deja pasar es no tocarlo; elegir uno derogado a propósito es otra cosa.
        ArticulosEnMemoria articulos = new();
        var articulo = Articulo.Crear(
            s_empresa, "TORN-8", "Tornillo de 8", TipoDeArticulo.Bien,
            s_unidad, s_impuesto, null, s_momento);
        articulos.Guardados.Add(articulo);

        var otroImpuesto = Guid.Parse("44444444-4444-4444-4444-444444444444");
        ImpuestosEn impuestos = new(EstadoDeMaestro.SoloResuelveLoViejo);

        var modificar = new ModificarArticulo(
            articulos,
            new CategoriasEnMemoria(),
            impuestos,
            new ConfirmacionesContadas(),
            new VersionesQueDanIgual(),
            new RelojParado(s_momento));

        Resultado<ArticuloDto> resultado = await modificar.EjecutarAsync(
            articulo.Id,
            new BuildingBlocks.Application.Concurrencia.VersionDeRecurso(1),
            new ModificarArticuloDto
            {
                Descripcion = "Tornillo de 8",
                Tipo = nameof(TipoDeArticulo.Bien),
                ImpuestoPorDefectoId = otroImpuesto,
                CategoriaId = null,
            },
            CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-impuesto-no-vigente");
        impuestos.Preguntados.ShouldBe([(otroImpuesto, new DateOnly(2026, 9, 8))]);
    }

    // ------------------------------------------------------------------------------- el arnés

    [Fact]
    public void El_enumerado_tiene_exactamente_los_tres_valores_que_esta_casilla_traduce()
    {
        // Si Organización añadiera un cuarto estado, los casos de arriba seguirían verdes —cada
        // uno prueba el suyo— y el valor nuevo caería en la rama `default`, que lanza. Es un
        // desenlace correcto, pero solo se descubriría en producción. Aquí se descubre al
        // compilar la suite.
        // `Case.Sensitive` explícito porque, para secuencias de texto, Shouldly resuelve a la
        // sobrecarga que lleva la sensibilidad a mayúsculas en el tercer sitio. Y sensible es lo
        // que hace falta: `noexiste` no es el valor del enumerado.
        Enum.GetNames<EstadoDeMaestro>().Order(StringComparer.Ordinal).ShouldBe(
            ["NoExiste", "SeOfreceParaLoNuevo", "SoloResuelveLoViejo"],
            Case.Sensitive,
            "los estados que este módulo sabe traducir son tres. Uno nuevo cae en la rama que " +
            "lanza, así que hay que decidir qué significa antes de que llegue por HTTP");
    }

    [Fact]
    public async Task Un_estado_que_esta_casilla_no_conoce_lanza_en_vez_de_dejar_pasar()
    {
        // La rama `default`, ejercida. Es la diferencia entre «no lo sé» y «adelante»: un
        // enumerado con un valor fuera de rango que cayera en el caso correcto daría de alta el
        // artículo contra una unidad de la que no se sabe nada.
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => AltaAsync(
            new ArticulosEnMemoria(), unidades: new UnidadesEn((EstadoDeMaestro)99)));
    }

    private static Task<Resultado<ArticuloDto>> AltaAsync(
        ArticulosEnMemoria articulos,
        UnidadesEn? unidades = null,
        ImpuestosEn? impuestos = null)
    {
        var crear = new CrearArticulo(
            new UsuarioDe(s_empresa),
            articulos,
            new CategoriasEnMemoria(),
            new EmpresasQueContestan(activa: true),
            unidades ?? new UnidadesEn(EstadoDeMaestro.SeOfreceParaLoNuevo),
            impuestos ?? new ImpuestosEn(EstadoDeMaestro.SeOfreceParaLoNuevo),
            new ConfirmacionesContadas(),
            new RelojParado(s_momento));

        return crear.EjecutarAsync(
            new CrearArticuloDto
            {
                Codigo = "TORN-8",
                Descripcion = "Tornillo de 8",
                Tipo = nameof(TipoDeArticulo.Bien),
                UnidadBaseId = s_unidad,
                ImpuestoPorDefectoId = s_impuesto,
                CategoriaId = null,
            },
            CancellationToken.None);
    }
}
