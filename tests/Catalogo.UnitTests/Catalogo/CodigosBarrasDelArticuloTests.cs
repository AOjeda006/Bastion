using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// Los casos de uso de los códigos de barras del artículo (ítem 2.10, ADR-0051): lo que deciden
/// ellos y no el dominio.
/// </summary>
/// <remarks>
/// <para>
/// <b>El dominio ya dice qué es un GTIN</b> (<c>ElGtinTests</c>) y qué unidades cuadran con qué
/// nivel (<c>ElCodigoDeBarrasTests</c>). Lo que se prueba aquí es lo que hace el caso de uso con
/// eso: que cada motivo llegue con SU <c>type</c>, que el nivel se lea por nombre y no por ordinal,
/// que la base sin unidades sea una y una caja sin unidades sea un <c>400</c>, que el duplicado se
/// vea en la forma normalizada, y el orden de las preguntas.
/// </para>
/// <para>
/// <b>Ninguno abre una conexión.</b> Lo que solo da PostgreSQL —la forma de catorce comparada por el
/// índice, el filtro de empresa, el <c>412</c> de la baja— lo ejerce
/// <c>ContratoDelCodigoDeBarrasTests</c>. El índice único y el <c>409</c> que el borde saca de su
/// nombre solo se alcanzan con dos altas a la vez, y los ejerce su último caso: una segunda alta que
/// llega después la para la comprobación previa, como aquí.
/// </para>
/// </remarks>
public sealed class CodigosBarrasDelArticuloTests
{
    private static readonly DateTimeOffset s_momento =
        new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid s_empresaId = Guid.CreateVersion7();

    /// <summary>
    /// Cada motivo del enumerado tiene su error, todos de validación y todos distintos.
    /// </summary>
    /// <remarks>
    /// Recorre el ENUMERADO y no una lista escrita: un motivo nuevo sin su rama lanza aquí, en el
    /// carril rápido, y no en la primera petición que lo traiga. Y que los códigos sean distintos es
    /// la mitad que importa: dos motivos con el mismo <c>type</c> son una pantalla que no puede decir
    /// cuál de los dos arreglar.
    /// </remarks>
    [Fact]
    public void Cada_motivo_tiene_su_type_y_todos_son_de_validacion()
    {
        List<ErrorDeOperacion> errores =
            [.. Enum.GetValues<MotivoDeRechazoDelGtin>().Select(ErroresDeGtin.DelMotivo)];

        errores.ShouldNotBeEmpty();
        errores.ShouldAllBe(error => error.Tipo == TipoDeError.Validacion);
        errores.ShouldAllBe(error => error.Codigo.StartsWith("gtin-", StringComparison.Ordinal));
        errores.Select(error => error.Codigo).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(errores.Count, "un type por motivo");
    }

    [Fact]
    public void Un_motivo_sin_su_rama_lanza_y_no_se_disfraza_de_otro()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ErroresDeGtin.DelMotivo((MotivoDeRechazoDelGtin)99));
    }

    /// <summary>Un GTIN que no sirve llega al alta con el <c>type</c> de su motivo.</summary>
    /// <remarks>
    /// Un ejemplo por motivo, sacado de los de <c>ElGtinTests</c>. El GTIN que falta entra por el
    /// largo, porque el contrato no le pone <c>[Required]</c> (ver <c>AgregarCodigoBarrasDto</c>).
    /// </remarks>
    [Theory]
    [InlineData("4006-381333931", "gtin-no-son-digitos")]
    [InlineData(null, "gtin-largo-no-admitido")]
    [InlineData("36000291452", "gtin-largo-no-admitido")]
    [InlineData("4006381333932", "gtin-digito-de-control")]
    [InlineData("2012345678903", "gtin-circulacion-restringida")]
    [InlineData("98412345678908", "gtin-medida-variable")]
    [InlineData("9801234567892", "gtin-cupon")]
    [InlineData("9511234567890", "gtin-sin-asignar")]
    public async Task Un_gtin_que_no_sirve_da_el_type_de_su_motivo(string? gtin, string codigo)
    {
        Articulo articulo = Alta();
        var codigos = new CodigosBarrasEnMemoria();
        var confirmaciones = new ConfirmacionesContadas();

        Resultado<CodigoBarrasDto> resultado = await Agregar(articulo, codigos, confirmaciones)
            .EjecutarAsync(articulo.Id, Peticion(gtin, "Base"), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe(codigo);
        resultado.Error.Tipo.ShouldBe(TipoDeError.Validacion);
        codigos.Guardados.ShouldBeEmpty();
        confirmaciones.Veces.ShouldBe(0);
    }

    /// <summary>
    /// El GTIN sale en catorce, la empresa del claim y, sin unidades, la base lleva una.
    /// </summary>
    [Fact]
    public async Task El_alta_de_la_base_guarda_el_gtin_en_catorce_y_una_unidad()
    {
        Articulo articulo = Alta();
        var codigos = new CodigosBarrasEnMemoria();
        var confirmaciones = new ConfirmacionesContadas();

        Resultado<CodigoBarrasDto> resultado = await Agregar(articulo, codigos, confirmaciones)
            .EjecutarAsync(articulo.Id, Peticion(" 4006381333931 ", "Base"), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.Gtin.ShouldBe("04006381333931", "se guarda y sale en su forma de catorce");
        resultado.Valor.Nivel.ShouldBe("Base");
        resultado.Valor.Unidades.ShouldBe(1, "sin unidades, la base lleva la suya");
        resultado.Valor.ArticuloId.ShouldBe(articulo.Id);
        resultado.Valor.EmpresaId.ShouldBe(s_empresaId, "la empresa sale del claim y no del cuerpo (R8)");
        codigos.Guardados.ShouldHaveSingleItem();
        confirmaciones.Veces.ShouldBe(1);
    }

    [Fact]
    public async Task Una_caja_guarda_las_unidades_que_dice()
    {
        Articulo articulo = Alta();

        Resultado<CodigoBarrasDto> resultado = await Agregar(articulo)
            .EjecutarAsync(articulo.Id, Peticion("10012345678902", "Caja", 12), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.Nivel.ShouldBe("Caja");
        resultado.Valor.Unidades.ShouldBe(12);
    }

    /// <summary>
    /// Las unidades que no cuadran son un <c>400</c>, y la caja sin unidades también: no hay un
    /// número que suponer.
    /// </summary>
    /// <remarks>
    /// Antes de construir el agregado, que LANZA con unas unidades que no cuadran: si este caso
    /// llegara a <c>CodigoBarras.Nuevo</c>, lo que saldría es una excepción, no un resultado.
    /// </remarks>
    [Theory]
    [InlineData("Base", 2)]
    [InlineData("Base", 0)]
    [InlineData("Caja", null)]
    [InlineData("Caja", 1)]
    [InlineData("Palet", null)]
    [InlineData("Palet", -5)]
    public async Task Las_unidades_que_no_cuadran_con_el_nivel_son_un_400(string nivel, int? unidades)
    {
        Articulo articulo = Alta();
        var codigos = new CodigosBarrasEnMemoria();

        Resultado<CodigoBarrasDto> resultado = await Agregar(articulo, codigos)
            .EjecutarAsync(articulo.Id, Peticion("4006381333931", nivel, unidades), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("codigo-barras-unidades-no-validas");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Validacion);
        codigos.Guardados.ShouldBeEmpty();
    }

    /// <summary>El nivel se lee por su NOMBRE, con su grafía; ni el ordinal ni otra caja.</summary>
    /// <remarks>
    /// <c>"2"</c> es el que importa: <c>Enum.TryParse</c> lo aceptaría como <c>Palet</c>, y un palé
    /// dado de alta por error con las unidades de una caja es un pedido de una caja que llega en
    /// palés.
    /// </remarks>
    [Theory]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("base")]
    [InlineData("CAJA")]
    [InlineData("Pallet")]
    [InlineData("")]
    public async Task Un_nivel_que_no_es_uno_de_los_tres_nombres_es_un_400(string nivel)
    {
        Articulo articulo = Alta();

        Resultado<CodigoBarrasDto> resultado = await Agregar(articulo)
            .EjecutarAsync(articulo.Id, Peticion("4006381333931", nivel, 2), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("codigo-barras-nivel-no-valido");
        resultado.Error.Mensaje.ShouldContain("Base, Caja, Palet");
    }

    /// <summary>
    /// El duplicado se ve en la forma normalizada: el GTIN-12 y su GTIN-13 con un cero delante son
    /// el mismo número.
    /// </summary>
    /// <remarks>
    /// Y el otro artículo da igual: un GTIN identifica UNA cosa en la empresa, no una por artículo
    /// (ADR-0051 §5). Comprobarlo con el mismo artículo dejaría pasar la versión que busca por
    /// <c>(articulo_id, gtin)</c>.
    /// </remarks>
    [Fact]
    public async Task El_gtin12_y_su_forma_de_13_chocan_aunque_sea_otro_articulo()
    {
        Articulo articulo = Alta();
        CodigosBarrasEnMemoria codigos = new CodigosBarrasEnMemoria().Con(CodigoBarras.Nuevo(
            s_empresaId,
            Guid.CreateVersion7(),
            Gtin.De("036000291452"),
            NivelDeGtin.Base,
            CodigoBarras.UnidadesDeLaBase,
            s_momento));
        var confirmaciones = new ConfirmacionesContadas();

        Resultado<CodigoBarrasDto> resultado = await Agregar(articulo, codigos, confirmaciones)
            .EjecutarAsync(articulo.Id, Peticion("0036000291452", "Base"), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("codigo-barras-duplicado");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Conflicto);
        codigos.Guardados.ShouldHaveSingleItem();
        confirmaciones.Veces.ShouldBe(0);
    }

    /// <summary>
    /// El duplicado que da la comprobación previa es el MISMO que da el índice: sin parámetros.
    /// </summary>
    /// <remarks>
    /// El borde traduce el nombre del índice con <c>ErroresDeCodigoBarras.Duplicado()</c>, que no
    /// sabe qué GTIN chocó. Si este mensaje llevara el número, las dos respuestas dirían por qué
    /// camino se llegó, y la pantalla tendría dos textos para un <c>type</c>.
    /// </remarks>
    [Fact]
    public async Task El_duplicado_previo_es_el_mismo_error_que_traduce_el_indice()
    {
        Articulo articulo = Alta();
        CodigosBarrasEnMemoria codigos = new CodigosBarrasEnMemoria().Con(Base(articulo.Id, "4006381333931"));

        Resultado<CodigoBarrasDto> resultado = await Agregar(articulo, codigos)
            .EjecutarAsync(articulo.Id, Peticion("4006381333931", "Base"), CancellationToken.None);

        ErrorDeOperacion delIndice = ErroresDeCodigoBarras.Duplicado();
        resultado.Error!.Codigo.ShouldBe(delIndice.Codigo);
        resultado.Error.Tipo.ShouldBe(delIndice.Tipo);
        resultado.Error.Mensaje.ShouldBe(delIndice.Mensaje);
        resultado.Error.Mensaje.ShouldNotContain("4006381333931");
    }

    /// <summary>
    /// El duplicado es lo último que se pregunta: un GTIN repetido con un nivel o unas unidades que
    /// no sirven recibe el <c>400</c> de lo que no sirve.
    /// </summary>
    /// <remarks>
    /// Lo que está mal en la petición se arregla en la petición; el duplicado se arregla en otro
    /// artículo. Si el <c>409</c> fuera antes, quien lo corrigiera volvería con el mismo nivel mal.
    /// </remarks>
    [Theory]
    [InlineData("2", null, "codigo-barras-nivel-no-valido")]
    [InlineData("Caja", null, "codigo-barras-unidades-no-validas")]
    [InlineData("Base", 3, "codigo-barras-unidades-no-validas")]
    public async Task El_duplicado_se_pregunta_despues_de_lo_que_trae_la_peticion(
        string nivel,
        int? unidades,
        string codigo)
    {
        Articulo articulo = Alta();
        CodigosBarrasEnMemoria codigos = new CodigosBarrasEnMemoria().Con(Base(articulo.Id, "4006381333931"));

        Resultado<CodigoBarrasDto> resultado = await Agregar(articulo, codigos)
            .EjecutarAsync(articulo.Id, Peticion("4006381333931", nivel, unidades), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe(codigo);
        codigos.BusquedasDeGtin.ShouldBe(0, "lo que no sirve no llega a preguntar por otras filas");
    }

    /// <summary>
    /// Un artículo lleva varios GTIN del mismo nivel: dos bases, un EAN-13 y un UPC-12, y dos cajas
    /// de unidades distintas.
    /// </summary>
    /// <remarks>
    /// La segunda decisión de la puerta del 2.10 (ADR-0051 §3). Una regla de «una base por
    /// artículo» dejaría al producto importado sin su código de origen.
    /// </remarks>
    [Fact]
    public async Task Un_articulo_lleva_varios_gtin_del_mismo_nivel()
    {
        Articulo articulo = Alta();
        var codigos = new CodigosBarrasEnMemoria();
        AgregarCodigoBarrasAlArticulo agregar = Agregar(articulo, codigos);
        (string Gtin, string Nivel, int? Unidades)[] altas =
        [
            ("4006381333931", "Base", null),
            ("036000291452", "Base", null),
            ("10036000291459", "Caja", 6),
            ("10012345678902", "Caja", 12),
        ];

        foreach ((string gtin, string nivel, int? unidades) in altas)
        {
            Resultado<CodigoBarrasDto> resultado = await agregar
                .EjecutarAsync(articulo.Id, Peticion(gtin, nivel, unidades), CancellationToken.None);

            resultado.EsCorrecto.ShouldBeTrue($"{gtin} como {nivel}: {resultado.Error?.Codigo}");
        }

        codigos.Guardados.Count.ShouldBe(4);
    }

    /// <summary>Con la empresa activa fuera de servicio no se pregunta nada más.</summary>
    /// <remarks>
    /// Con un artículo que no existe y un GTIN que no sirve: si la empresa se comprobara después de
    /// cualquiera de los dos, saldría su <c>404</c> o su <c>400</c>, y no el <c>409</c> que dice que
    /// lo que falla es la empresa.
    /// </remarks>
    [Fact]
    public async Task Con_la_empresa_inoperativa_no_se_llega_ni_al_articulo()
    {
        Articulo articulo = Alta();
        var codigos = new CodigosBarrasEnMemoria();
        var confirmaciones = new ConfirmacionesContadas();

        Resultado<CodigoBarrasDto> resultado = await new AgregarCodigoBarrasAlArticulo(
                new UsuarioDe(s_empresaId),
                new EmpresasQueContestan(activa: false),
                new ArticulosEnMemoria { Guardados = { articulo } },
                codigos,
                confirmaciones,
                new RelojParado(s_momento))
            .EjecutarAsync(Guid.CreateVersion7(), Peticion("ABC", "2"), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("empresa-activa-no-operativa");
        codigos.BusquedasDeGtin.ShouldBe(0);
        confirmaciones.Veces.ShouldBe(0);
    }

    /// <summary>El artículo que no existe es un <c>404</c>, y va antes que lo que trae la petición.</summary>
    /// <remarks>
    /// Con un GTIN que tampoco sirve: si la petición se leyera primero, saldría su <c>400</c>, y
    /// quien se equivocó de artículo se pondría a corregir un número que no era el problema.
    /// </remarks>
    [Fact]
    public async Task El_articulo_que_no_existe_es_un_404_antes_que_el_gtin()
    {
        var otro = Guid.CreateVersion7();

        Resultado<CodigoBarrasDto> resultado = await Agregar(Alta())
            .EjecutarAsync(otro, Peticion("ABC", "Base"), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-no-encontrado");
        resultado.Error.Tipo.ShouldBe(TipoDeError.NoEncontrado);
    }

    [Fact]
    public async Task El_listado_de_un_articulo_que_no_existe_es_un_404_y_no_una_lista_vacia()
    {
        Resultado<IReadOnlyList<CodigoBarrasDto>> resultado = await new ListarCodigosBarrasDelArticulo(
                new ArticulosEnMemoria(),
                new CodigosBarrasEnMemoria())
            .EjecutarAsync(Guid.CreateVersion7(), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-no-encontrado");
    }

    [Fact]
    public async Task El_listado_devuelve_solo_los_del_articulo()
    {
        Articulo articulo = Alta();
        CodigosBarrasEnMemoria codigos = new CodigosBarrasEnMemoria()
            .Con(Base(articulo.Id, "4006381333931"))
            .Con(Base(Guid.CreateVersion7(), "036000291452"));

        Resultado<IReadOnlyList<CodigoBarrasDto>> resultado = await new ListarCodigosBarrasDelArticulo(
                new ArticulosEnMemoria { Guardados = { articulo } },
                codigos)
            .EjecutarAsync(articulo.Id, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.ShouldHaveSingleItem().Gtin.ShouldBe("04006381333931");
    }

    /// <summary>
    /// La búsqueda normaliza lo que entra: el GTIN-12 encuentra el alta que se hizo en trece.
    /// </summary>
    [Fact]
    public async Task La_busqueda_encuentra_el_mismo_numero_escrito_de_otra_forma()
    {
        var articuloId = Guid.CreateVersion7();
        var buscar = new BuscarCodigoBarrasPorGtin(
            new CodigosBarrasEnMemoria().Con(Base(articuloId, "0036000291452")));

        Resultado<IReadOnlyList<CodigoBarrasDto>> resultado =
            await buscar.EjecutarAsync("036000291452", CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.ShouldHaveSingleItem().ArticuloId.ShouldBe(articuloId);
    }

    [Fact]
    public async Task La_busqueda_de_un_gtin_que_nadie_lleva_es_una_lista_vacia()
    {
        Resultado<IReadOnlyList<CodigoBarrasDto>> resultado = await new BuscarCodigoBarrasPorGtin(
                new CodigosBarrasEnMemoria())
            .EjecutarAsync("4006381333931", CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.ShouldBeEmpty();
    }

    /// <summary>
    /// Lo que no es un GTIN no se busca: da el <c>400</c> de su motivo, y no una lista vacía.
    /// </summary>
    /// <remarks>
    /// Una lista vacía diría «no lo lleva ningún artículo», y quien tecleó una cifra de más se iría
    /// a darlo de alta.
    /// </remarks>
    [Theory]
    [InlineData(null, "gtin-largo-no-admitido")]
    [InlineData("4006381333932", "gtin-digito-de-control")]
    [InlineData("9801234567892", "gtin-cupon")]
    public async Task La_busqueda_de_lo_que_no_es_un_gtin_es_un_400(string? gtin, string codigo)
    {
        var codigos = new CodigosBarrasEnMemoria();

        Resultado<IReadOnlyList<CodigoBarrasDto>> resultado =
            await new BuscarCodigoBarrasPorGtin(codigos).EjecutarAsync(gtin, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe(codigo);
        codigos.BusquedasDeGtin.ShouldBe(0, "lo que no es un GTIN no llega al repositorio");
    }

    [Fact]
    public async Task Quitar_borra_la_fila_y_confirma()
    {
        CodigoBarras codigo = Base(Guid.CreateVersion7(), "4006381333931");
        CodigosBarrasEnMemoria codigos = new CodigosBarrasEnMemoria().Con(codigo);
        var confirmaciones = new ConfirmacionesContadas();

        Resultado resultado = await new QuitarCodigoBarras(codigos, confirmaciones, new VersionesQueDanIgual())
            .EjecutarAsync(codigo.Id, new VersionDeRecurso(0), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        codigos.Eliminados.ShouldHaveSingleItem().ShouldBeSameAs(codigo);
        codigos.Guardados.ShouldBeEmpty();
        confirmaciones.Veces.ShouldBe(1);
    }

    [Fact]
    public async Task Quitar_lo_que_no_existe_es_un_404()
    {
        var confirmaciones = new ConfirmacionesContadas();

        Resultado resultado = await new QuitarCodigoBarras(
                new CodigosBarrasEnMemoria(),
                confirmaciones,
                new VersionesQueDanIgual())
            .EjecutarAsync(Guid.CreateVersion7(), new VersionDeRecurso(0), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("codigo-barras-no-encontrado");
        resultado.Error.Tipo.ShouldBe(TipoDeError.NoEncontrado);
        confirmaciones.Veces.ShouldBe(0);
    }

    private static AgregarCodigoBarrasAlArticulo Agregar(
        Articulo articulo,
        CodigosBarrasEnMemoria? codigos = null,
        ConfirmacionesContadas? confirmaciones = null) =>
        new(
            new UsuarioDe(s_empresaId),
            new EmpresasQueContestan(activa: true),
            new ArticulosEnMemoria { Guardados = { articulo } },
            codigos ?? new CodigosBarrasEnMemoria(),
            confirmaciones ?? new ConfirmacionesContadas(),
            new RelojParado(s_momento));

    private static AgregarCodigoBarrasDto Peticion(string? gtin, string nivel, int? unidades = null) =>
        new() { Gtin = gtin, Nivel = nivel, Unidades = unidades };

    private static CodigoBarras Base(Guid articuloId, string gtin) =>
        CodigoBarras.Nuevo(
            s_empresaId,
            articuloId,
            Gtin.De(gtin),
            NivelDeGtin.Base,
            CodigoBarras.UnidadesDeLaBase,
            s_momento);

    private static Articulo Alta() =>
        Articulo.Crear(
            s_empresaId,
            "ART-1",
            "Artículo de prueba",
            TipoDeArticulo.Bien,
            Trazabilidad.Ninguna,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            s_momento);
}
