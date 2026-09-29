using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Bastion.Organizacion.Contracts.Comun;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// La marca de trazabilidad del artículo: qué admite, quién la rechaza, y en qué orden se cambia.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que se prueba aquí no es solo un resultado, es un orden</b> (ADR-0048 §4). La marca no
/// cambia en cuanto hay un movimiento, y quien lo sabe es Inventario. Si el caso de uso preguntara
/// antes de bloquear la fila, o fuera de la transacción, la respuesta caducaría antes del
/// <c>COMMIT</c>. Por eso los dobles apuntan en una misma <see cref="Bitacora"/>, y los casos
/// comparan la lista entera.
/// </para>
/// <para>
/// Que el cerrojo de verdad haga esperar a una confirmación de Inventario no se puede ver aquí: lo
/// prueba la carrera contra PostgreSQL.
/// </para>
/// </remarks>
public sealed class LaMarcaDeTrazabilidadTests
{
    private static readonly Guid s_empresa = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_unidad = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid s_impuesto = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset s_momento = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------------------ el dominio

    [Fact]
    public void El_enumerado_tiene_exactamente_las_tres_marcas()
    {
        // Una cuarta marca —lote y serie a la vez— es el disparador que el ADR-0048 §1 deja
        // escrito, y cambia el CHECK, la traducción y el libro. Aquí se ve al compilar la suite.
        Enum.GetNames<Trazabilidad>().ShouldBe(
            ["Ninguna", "PorLote", "PorNumeroSerie"],
            Case.Sensitive,
            "la marca es excluyente y tiene tres valores. Uno nuevo hay que decidirlo en el ADR");
    }

    [Fact]
    public void Un_articulo_nace_con_la_marca_que_se_le_da()
    {
        Articulo articulo = Alta(TipoDeArticulo.Bien, Trazabilidad.PorNumeroSerie);

        articulo.Trazabilidad.ShouldBe(Trazabilidad.PorNumeroSerie);
    }

    [Fact]
    public void Un_servicio_no_nace_ni_pasa_a_llevar_lote_o_serie()
    {
        Should.Throw<ArgumentException>(() => Alta(TipoDeArticulo.Servicio, Trazabilidad.PorLote));

        Articulo servicio = Alta(TipoDeArticulo.Servicio, Trazabilidad.Ninguna);

        Should.Throw<ArgumentException>(() => servicio.Modificar(
            "Montaje", TipoDeArticulo.Servicio, Trazabilidad.PorNumeroSerie, s_impuesto, null));

        servicio.Trazabilidad.ShouldBe(
            Trazabilidad.Ninguna, "el rechazo no puede dejar la marca a medio cambiar");
    }

    [Fact]
    public void Una_marca_fuera_del_enumerado_lanza_en_vez_de_guardarse()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => Alta(TipoDeArticulo.Bien, (Trazabilidad)99));
    }

    // -------------------------------------------------------------------------------- el alta

    [Fact]
    public async Task Sin_decir_la_marca_el_alta_es_sin_trazabilidad()
    {
        // Opcional al crear y obligatoria al modificar: el PUT sustituye la ficha entera, y una
        // marca que se omite ahí no puede significar «quítala».
        ArticulosEnMemoria articulos = new();

        Resultado<ArticuloDto> resultado = await CrearAsync(
            articulos, new CrearArticuloDto
            {
                Codigo = "TORN-8",
                Descripcion = "Tornillo de 8",
                Tipo = nameof(TipoDeArticulo.Bien),
                UnidadBaseId = s_unidad,
                ImpuestoPorDefectoId = s_impuesto,
            });

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.Trazabilidad.ShouldBe(nameof(Trazabilidad.Ninguna));
        articulos.Guardados.ShouldHaveSingleItem().Trazabilidad.ShouldBe(Trazabilidad.Ninguna);
    }

    [Theory]
    [InlineData("porLote")]
    [InlineData("1")]
    [InlineData("")]
    public async Task El_alta_rechaza_lo_que_no_es_una_marca_escrita_como_tal(string marca)
    {
        // Ordinal y por nombre, como el tipo: «1» sería `PorLote` para `Enum.TryParse`.
        ArticulosEnMemoria articulos = new();

        Resultado<ArticuloDto> resultado = await CrearAsync(
            articulos, Alta(nameof(TipoDeArticulo.Bien), marca));

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-trazabilidad-no-valida");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Validacion);
        articulos.Guardados.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_alta_de_un_servicio_con_lote_se_rechaza_con_su_propio_codigo()
    {
        ArticulosEnMemoria articulos = new();

        Resultado<ArticuloDto> resultado = await CrearAsync(
            articulos, Alta(nameof(TipoDeArticulo.Servicio), nameof(Trazabilidad.PorLote)));

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-servicio-con-trazabilidad");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Validacion);
        articulos.Guardados.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------ la modificación

    [Fact]
    public async Task Sin_movimientos_la_marca_cambia_y_se_pregunta_con_la_fila_ya_bloqueada()
    {
        Escena escena = new(tieneMovimientos: false);

        Resultado<ArticuloDto> resultado = await escena.ModificarAsync(
            nameof(TipoDeArticulo.Bien), nameof(Trazabilidad.PorLote));

        resultado.EsCorrecto.ShouldBeTrue();
        escena.Articulo.Trazabilidad.ShouldBe(Trazabilidad.PorLote);
        escena.Movimientos.Preguntados.ShouldBe([escena.Articulo.Id]);
        escena.Bitacora.Pasos.ShouldBe(
            ["abrir", "bloquear", "leer", "preguntar", "confirmar", "cerrar"],
            "el cerrojo va dentro de la transacción y antes de leer, y la pregunta a Inventario " +
            "después del cerrojo: si no, la respuesta caduca antes del COMMIT (ADR-0048 §4)");
    }

    [Fact]
    public async Task Con_movimientos_la_marca_no_cambia_y_se_contesta_con_un_conflicto()
    {
        Escena escena = new(tieneMovimientos: true);

        Resultado<ArticuloDto> resultado = await escena.ModificarAsync(
            nameof(TipoDeArticulo.Bien), nameof(Trazabilidad.PorNumeroSerie));

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-trazabilidad-con-movimientos");
        resultado.Error.Tipo.ShouldBe(
            TipoDeError.Conflicto,
            "la petición está bien escrita: lo que choca es el estado del libro");
        escena.Articulo.Trazabilidad.ShouldBe(Trazabilidad.Ninguna);
        escena.Articulo.Descripcion.ShouldBe(
            "Tornillo de 8", "el rechazo es de la ficha entera, no solo de la marca");
        escena.Confirmaciones.Veces.ShouldBe(0);
        escena.Bitacora.Pasos.ShouldBe(["abrir", "bloquear", "leer", "preguntar", "cerrar"]);
    }

    [Fact]
    public async Task Si_la_marca_no_cambia_no_se_pregunta_aunque_haya_movimientos()
    {
        // Lo mismo que con el impuesto y la categoría: preguntar por lo que no cambia convertiría
        // tener movimientos en un congelador para la descripción.
        Escena escena = new(tieneMovimientos: true);

        Resultado<ArticuloDto> resultado = await escena.ModificarAsync(
            nameof(TipoDeArticulo.Bien), nameof(Trazabilidad.Ninguna));

        resultado.EsCorrecto.ShouldBeTrue();
        escena.Articulo.Descripcion.ShouldBe("Tornillo de 8 mm");
        escena.Movimientos.Preguntados.ShouldBeEmpty();
        escena.Bitacora.Pasos.ShouldBe(
            ["abrir", "bloquear", "leer", "confirmar", "cerrar"],
            "el cerrojo se toma igual: el UPDATE tomaría el mismo, y así no hay dos caminos");
    }

    [Fact]
    public async Task Lo_que_no_se_puede_bloquear_no_existe_y_ni_se_lee()
    {
        Escena escena = new(tieneMovimientos: false, existe: false);

        Resultado<ArticuloDto> resultado = await escena.ModificarAsync(
            nameof(TipoDeArticulo.Bien), nameof(Trazabilidad.PorLote));

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-no-encontrado");
        escena.Bitacora.Pasos.ShouldBe(["abrir", "bloquear", "cerrar"]);
    }

    [Fact]
    public async Task Al_modificar_una_marca_vacia_se_rechaza_sin_preguntar()
    {
        Escena escena = new(tieneMovimientos: false);

        Resultado<ArticuloDto> resultado = await escena.ModificarAsync(
            nameof(TipoDeArticulo.Bien), "");

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-trazabilidad-no-valida");
        escena.Movimientos.Preguntados.ShouldBeEmpty();
        escena.Confirmaciones.Veces.ShouldBe(0);
    }

    [Fact]
    public async Task Pasar_a_servicio_con_la_marca_puesta_se_rechaza_antes_de_preguntar()
    {
        Escena escena = new(tieneMovimientos: false, marca: Trazabilidad.PorLote);

        Resultado<ArticuloDto> resultado = await escena.ModificarAsync(
            nameof(TipoDeArticulo.Servicio), nameof(Trazabilidad.PorLote));

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("articulo-servicio-con-trazabilidad");
        escena.Articulo.Tipo.ShouldBe(TipoDeArticulo.Bien);
        escena.Movimientos.Preguntados.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------------- el arnés

    private static Articulo Alta(TipoDeArticulo tipo, Trazabilidad marca) =>
        Articulo.Crear(
            s_empresa, "TORN-8", "Tornillo de 8", tipo, marca,
            s_unidad, s_impuesto, null, s_momento);

    private static CrearArticuloDto Alta(string tipo, string marca) => new()
    {
        Codigo = "TORN-8",
        Descripcion = "Tornillo de 8",
        Tipo = tipo,
        Trazabilidad = marca,
        UnidadBaseId = s_unidad,
        ImpuestoPorDefectoId = s_impuesto,
    };

    private static Task<Resultado<ArticuloDto>> CrearAsync(
        ArticulosEnMemoria articulos, CrearArticuloDto peticion)
    {
        var crear = new CrearArticulo(
            new UsuarioDe(s_empresa),
            articulos,
            new CategoriasEnMemoria(),
            new EmpresasQueContestan(activa: true),
            new UnidadesEn(EstadoDeMaestro.SeOfreceParaLoNuevo),
            new ImpuestosEn(EstadoDeMaestro.SeOfreceParaLoNuevo),
            new ConfirmacionesContadas(),
            new RelojParado(s_momento));

        return crear.EjecutarAsync(peticion, CancellationToken.None);
    }

    /// <summary>Un artículo guardado y todos los dobles de la modificación, con una bitácora.</summary>
    private sealed class Escena
    {
        internal Escena(bool tieneMovimientos, bool existe = true, Trazabilidad marca = Trazabilidad.Ninguna)
        {
            Articulo = Alta(TipoDeArticulo.Bien, marca);
            Articulos = new ArticulosEnMemoria { Bitacora = Bitacora };
            Articulos.Guardados.Add(Articulo);
            Confirmaciones = new ConfirmacionesContadas(Bitacora);
            Cerrojo = new CerrojoApuntado(Bitacora, existe);
            Movimientos = new MovimientosQueContestan(tieneMovimientos, Bitacora);
        }

        internal Bitacora Bitacora { get; } = new();

        internal Articulo Articulo { get; }

        internal ArticulosEnMemoria Articulos { get; }

        internal ConfirmacionesContadas Confirmaciones { get; }

        internal CerrojoApuntado Cerrojo { get; }

        internal MovimientosQueContestan Movimientos { get; }

        internal Task<Resultado<ArticuloDto>> ModificarAsync(string tipo, string marca)
        {
            var modificar = new ModificarArticulo(
                Articulos,
                new CategoriasEnMemoria(),
                new ImpuestosEn(EstadoDeMaestro.SeOfreceParaLoNuevo),
                Cerrojo,
                Movimientos,
                Confirmaciones,
                new VersionesQueDanIgual(),
                new RelojParado(s_momento));

            return modificar.EjecutarAsync(
                Articulo.Id,
                new VersionDeRecurso(1),
                new ModificarArticuloDto
                {
                    Descripcion = "Tornillo de 8 mm",
                    Tipo = tipo,
                    Trazabilidad = marca,
                    ImpuestoPorDefectoId = s_impuesto,
                    CategoriaId = null,
                },
                CancellationToken.None);
        }
    }
}
