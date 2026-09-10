using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Contrato;

/// <summary>
/// El contrato del módulo de Catálogo visto desde fuera: lo que se manda, lo que vuelve y con qué
/// código.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí es donde la retirada del ADR-0023 deja de ser una columna y pasa a significar algo.</b>
/// El ítem 1.7 dejó los tres valores de <c>EstadoDeMaestro</c> contestables por los puertos, pero
/// hasta el 1.8 nadie los había ramificado en un camino de negocio: Catálogo es el consumidor para
/// el que se construyeron. Los tres estados se ejercen aquí <b>contra PostgreSQL y por HTTP</b>,
/// porque quien decide cuál de los tres contesta cada puerto es una consulta traducida a SQL —la
/// vigencia de un tramo son dos comparaciones de fecha— y no una rama en C#. Esa mitad tiene
/// además su gemela sin contenedor en <c>Catalogo.UnitTests</c>, que es donde se recorren las
/// combinaciones en milisegundos; ninguna de las dos sustituye a la otra.
/// </para>
/// <para>
/// <b>Ni la unidad ni el impuesto son claves ajenas</b> (§5, regla 4): viven en el esquema de
/// Organización y aquí se guardan como <c>uuid</c> desnudos. Lo único que impide que acabe ahí un
/// identificador inventado son los puertos, y por eso lo que estos casos comprueban de verdad es
/// que las llamadas existen — si alguien las quitara, la base de datos <b>no diría nada</b>.
/// </para>
/// <para>
/// <b>Los maestros que estos casos crean son de instalación (R8) y se comparten entre empresas.</b>
/// Cada caso se lleva la suya para las fichas, pero las unidades y los tramos que da de alta los ve
/// todo el mundo, así que sus códigos llevan el número del caso: dos tests que retiraran «UD» se
/// pisarían, y el rojo del segundo se leería como un fallo de la API.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ContratoDeCatalogoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Articulos = "/api/v1/catalogo/articulos";
    private const string Categorias = "/api/v1/catalogo/categorias";
    private const string Unidades = "/api/v1/organizacion/unidades-de-medida";
    private const string Impuestos = "/api/v1/organizacion/impuestos";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    [Fact]
    public async Task Crear_un_articulo_devuelve_201_con_Location_que_lleva_al_recurso()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(170);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 170);

        using HttpResponseMessage creacion = await cliente.PostAsJsonAsync(
            Articulos, Alta("TOR-8", unidad, impuesto));

        creacion.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(creacion));
        creacion.Headers.Location.ShouldNotBeNull();

        using HttpResponseMessage seguido = await cliente.GetAsync(creacion.Headers.Location);
        seguido.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(seguido));

        ArticuloDto articulo = (await seguido.Content.ReadFromJsonAsync<ArticuloDto>())!;

        articulo.Codigo.ShouldBe("TOR-8", "el código se normaliza a mayúsculas");
        articulo.UnidadBaseId.ShouldBe(unidad);
        articulo.ImpuestoPorDefectoId.ShouldBe(impuesto);
    }

    /// <summary>
    /// <c>SoloResuelveLoViejo</c>, con sus <b>dos</b> mitades: el alta se rechaza, y el artículo que
    /// ya usaba esa unidad la sigue resolviendo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La segunda mitad es la que distingue una retirada de un borrado</b>, y es la que se cae
    /// sola. Es facilísimo escribir la comprobación de la unidad en un sitio por el que pasen las
    /// dos operaciones —el alta y la modificación—, y el resultado es que retirar una unidad
    /// convierte en intocables todos los artículos que la usan: nadie podría ni corregirles una
    /// falta de ortografía en la descripción. Eso no es retirar, es congelar.
    /// </para>
    /// <para>
    /// Las dos mitades van en el mismo caso a propósito. Separadas, un alta que rechazara todo
    /// pasaría la primera y una modificación que no comprobara nada pasaría la segunda; lo que hay
    /// que ver es la <b>misma</b> unidad diciendo que no a lo nuevo y que sí a lo viejo.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Una_unidad_RETIRADA_no_vale_para_un_alta_y_el_articulo_que_ya_la_usa_sigue_resolviendola()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(171);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 171);

        ArticuloDto antiguo = await CrearAsync(cliente, Alta("VIEJO", unidad, impuesto));

        using (HttpResponseMessage retirada = await cliente.AccionarAsync(
            $"{Unidades}/{unidad}", $"{Unidades}/{unidad}/retirada", HttpMethod.Post))
        {
            retirada.StatusCode.ShouldBe(
                HttpStatusCode.NoContent, await Escenario.Detalle(retirada));
        }

        // Primera mitad: para lo NUEVO, no.
        using HttpResponseMessage rechazo = await cliente.PostAsJsonAsync(
            Articulos, Alta("NUEVO", unidad, impuesto));

        rechazo.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(rechazo));
        (await Problema(rechazo)).GetProperty("type").GetString()
            .ShouldBe("/errors/articulo-unidad-retirada");

        // Segunda mitad: lo VIEJO sigue en pie, y sigue diciendo en qué se mide.
        using HttpResponseMessage lectura = await cliente.GetAsync($"{Articulos}/{antiguo.Id}");

        lectura.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(lectura));
        (await lectura.Content.ReadFromJsonAsync<ArticuloDto>())!.UnidadBaseId.ShouldBe(unidad);

        // Y se puede corregir. Esta es la aserción que se pone roja el día que alguien «unifique»
        // la comprobación de la unidad entre el alta y la modificación.
        using HttpResponseMessage correccion = await cliente.ModificarAsync(
            $"{Articulos}/{antiguo.Id}",
            new ModificarArticuloDto
            {
                Descripcion = "Descripción corregida",
                Tipo = "Bien",
                ImpuestoPorDefectoId = impuesto,
                CategoriaId = null,
            });

        correccion.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "un artículo que ya usa una unidad retirada tiene que poder seguir corrigiéndose: " +
            "retirar la unidad no es congelar lo que la usa. " + await Escenario.Detalle(correccion));

        (await correccion.Content.ReadFromJsonAsync<ArticuloDto>())!.UnidadBaseId.ShouldBe(
            unidad, "y la sigue teniendo: la modificación no puede cambiarla ni queriendo");
    }

    /// <summary>
    /// <c>NoExiste</c>, y su <c>type</c> es <b>distinto</b> del de la unidad retirada.
    /// </summary>
    /// <remarks>
    /// No es cortesía: son dos arreglos distintos. Quien teclea un identificador que no existe
    /// tiene que corregir el identificador; quien apunta a uno retirado tiene que elegir otra
    /// unidad que sí se ofrezca. Con un solo <c>type</c>, el frontal escribiría un texto que sirve
    /// para uno de los dos y despista en el otro (ADR-0030).
    /// </remarks>
    [Fact]
    public async Task Una_unidad_que_no_existe_es_400_y_su_type_es_DISTINGUIBLE_del_de_la_retirada()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(172);
        (_, Guid impuesto) = await MaestrosAsync(cliente, 172);

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Articulos, Alta("FANTASMA", Guid.NewGuid(), impuesto));

        // 400 y no 409: no hay ningún conflicto con el estado del sistema, hay un dato mal escrito.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));

        (await Problema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/articulo-unidad-no-encontrada");
    }

    /// <summary>Un tramo que ya no rige no se propone para un artículo nuevo.</summary>
    /// <remarks>
    /// «Retirado» no vale como palabra: un tramo de impuesto no se retira, deja de regir. El 1 de
    /// septiembre de 2012 el IVA general pasó del 18 % al 21 %, y las facturas anteriores siguen
    /// llevando el 18 % para siempre — pero ningún artículo nuevo puede proponerlo.
    /// </remarks>
    [Fact]
    public async Task Un_tramo_de_impuesto_que_ya_no_rige_no_se_propone_para_un_articulo_nuevo()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(173);
        (Guid unidad, _) = await MaestrosAsync(cliente, 173);

        Guid caducado = await CrearImpuestoAsync(
            cliente,
            "CAT173VIEJO",
            new DateOnly(2010, 1, 1),
            new DateOnly(2012, 8, 31));

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Articulos, Alta("AL18", unidad, caducado));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(respuesta));
        (await Problema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/articulo-impuesto-no-vigente");
    }

    /// <summary>
    /// Al modificar, el impuesto solo se vuelve a preguntar si <b>cambia</b>.
    /// </summary>
    /// <remarks>
    /// Es la misma decisión que la segunda mitad de la unidad retirada, y hace falta comprobarla
    /// aparte porque el impuesto <b>sí</b> se puede cambiar y la unidad no. Revalidar un impuesto
    /// que nadie ha tocado dejaría sin poder corregirse a todo artículo cuyo tramo haya expirado
    /// — que con el tiempo son todos.
    /// </remarks>
    [Fact]
    public async Task Modificar_sin_tocar_el_impuesto_no_lo_vuelve_a_preguntar_pero_cambiarlo_a_uno_caducado_SI_se_rechaza()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(174);
        (Guid unidad, Guid vigente) = await MaestrosAsync(cliente, 174);

        Guid caducado = await CrearImpuestoAsync(
            cliente,
            "CAT174VIEJO",
            new DateOnly(2010, 1, 1),
            new DateOnly(2012, 8, 31));

        ArticuloDto articulo = await CrearAsync(cliente, Alta("MOD", unidad, vigente));

        // Y AHORA SE CIERRA EL TRAMO DEL ARTÍCULO, que es lo que le da dientes a lo de abajo. Con
        // el tramo todavía rigiendo, revalidar lo que nadie ha tocado y no revalidarlo se ven
        // exactamente igual desde fuera: el caso saldría verde el día que alguien unificara las
        // dos ramas. Derogado el tramo que el artículo ya tenía, el «sin tocarlo» de la línea
        // siguiente es literalmente el congelador que esta decisión existe para evitar.
        var ayer = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        using (HttpResponseMessage cierre = await cliente.EnviarConVersionAsync(
            HttpMethod.Post,
            $"{Impuestos}/{vigente}/cierre",
            await cliente.EtiquetaDeAsync($"{Impuestos}/{vigente}"),
            JsonContent.Create(new CerrarImpuestoDto { UltimoDia = ayer })))
        {
            cierre.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cierre));
        }

        using (HttpResponseMessage sinTocarlo = await cliente.ModificarAsync(
            $"{Articulos}/{articulo.Id}",
            Cambio("Otra descripción", vigente)))
        {
            sinTocarlo.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                "el tramo del artículo ya no rige, y aun así la ficha tiene que poder corregirse: " +
                "revalidar el maestro que nadie ha tocado convierte la derogación en un " +
                "congelador. " + await Escenario.Detalle(sinTocarlo));
        }

        using HttpResponseMessage cambiandolo = await cliente.ModificarAsync(
            $"{Articulos}/{articulo.Id}",
            Cambio("Otra descripción más", caducado));

        cambiandolo.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "cambiar el impuesto a uno que ya no rige SÍ tiene que preguntar al puerto. " +
            await Escenario.Detalle(cambiandolo));

        (await Problema(cambiandolo)).GetProperty("type").GetString()
            .ShouldBe("/errors/articulo-impuesto-no-vigente");
    }

    [Fact]
    public async Task El_codigo_del_articulo_se_normaliza_y_el_duplicado_en_minusculas_es_409()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(175);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 175);

        ArticuloDto primero = await CrearAsync(cliente, Alta("tor-10", unidad, impuesto));

        primero.Codigo.ShouldBe("TOR-10");

        using HttpResponseMessage segundo = await cliente.PostAsJsonAsync(
            Articulos, Alta("TOR-10", unidad, impuesto));

        // Sin la normalización, la base aceptaría las dos filas y el maestro tendría dos artículos
        // que quien los lee no distingue.
        segundo.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(segundo));

        JsonElement problema = await Problema(segundo);
        problema.GetProperty("type").GetString().ShouldBe("/errors/articulo-duplicado");

        // Y el código va DENTRO del mensaje, al revés que en el conflicto de un tercero: allí las
        // dos respuestas se igualan para no delatar una baja del art. 32; aquí no hay nada que
        // esconder y el código es lo único que hace falta para saber qué corregir.
        problema.GetProperty("detail").GetString()!.ShouldContain("TOR-10");
    }

    [Fact]
    public async Task Modificar_devuelve_el_recurso_entero_y_no_hay_por_donde_tocar_el_codigo_ni_la_unidad()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(176);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 176);

        ArticuloDto articulo = await CrearAsync(cliente, Alta("FIJO", unidad, impuesto));

        using HttpResponseMessage sinVersion = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Articulos}/{articulo.Id}",
            etiqueta: null,
            JsonContent.Create(Cambio("Da igual", impuesto)));

        sinVersion.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired);

        using HttpResponseMessage conVersion = await cliente.ModificarAsync(
            $"{Articulos}/{articulo.Id}", Cambio("Descripción nueva", impuesto));

        conVersion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(conVersion));

        ArticuloDto modificado = (await conVersion.Content.ReadFromJsonAsync<ArticuloDto>())!;

        modificado.Descripcion.ShouldBe("Descripción nueva");

        // Ni el código ni la unidad están en `ModificarArticuloDto`, así que no hay manera de
        // intentarlo. El código ya está impreso en albaranes y etiquetas fuera del sistema; la
        // unidad es peor todavía, porque cambiarla reinterpretaría cada existencia ya registrada
        // sin que nadie hubiera movido mercancía.
        modificado.Codigo.ShouldBe(articulo.Codigo);
        modificado.UnidadBaseId.ShouldBe(articulo.UnidadBaseId);
    }

    /// <summary>El caso degenerado del ciclo, en las dos puertas: una categoría no es su madre.</summary>
    [Fact]
    public async Task Una_categoria_no_puede_colgar_de_si_misma_ni_al_crearla_ni_al_moverla()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(177);

        // Al crearla no hace falta comprobarlo desde fuera: el identificador todavía no existe, así
        // que ponerse a sí misma de madre es imposible por construcción. Lo que sí se puede es
        // MOVERSE debajo de una misma, y eso es un ciclo de longitud uno.
        CategoriaDto suya = await CrearCategoriaAsync(cliente, "SOLA", "Sola");

        using HttpResponseMessage respuesta = await cliente.ModificarAsync(
            $"{Categorias}/{suya.Id}",
            new ModificarCategoriaDto { Nombre = "Sola", PadreId = suya.Id });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(respuesta));
        (await Problema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/categoria-ciclo");

        // Y la base tampoco lo dejaría: hay un CHECK que compara `padre_id` con `id` en la misma
        // fila. Son dos defensas y ninguna sobra — el CHECK solo ve UNA fila, así que el ciclo de
        // dos eslabones del caso siguiente se le escapa entero.
    }

    /// <summary>El ciclo de verdad: mover una rama debajo de su propia descendencia.</summary>
    /// <remarks>
    /// <b>Este es el hueco que deja comprobar los ciclos solo en el alta.</b> Una categoría que
    /// nace no puede cerrar un ciclo —todavía no tiene descendencia—, así que la comprobación del
    /// alta sale verde siempre y no protege de nada. El ciclo se cierra moviendo, y mover es
    /// modificar.
    /// </remarks>
    [Fact]
    public async Task Mover_una_categoria_debajo_de_su_propia_descendencia_es_409_con_type_categoria_ciclo()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(178);

        CategoriaDto ferreteria = await CrearCategoriaAsync(cliente, "FERR", "Ferretería");
        CategoriaDto tornillos = await CrearCategoriaAsync(
            cliente, "TORN", "Tornillería", ferreteria.Id);

        using HttpResponseMessage respuesta = await cliente.ModificarAsync(
            $"{Categorias}/{ferreteria.Id}",
            new ModificarCategoriaDto { Nombre = "Ferretería", PadreId = tornillos.Id });

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "ni el CHECK de la fila ni la clave ajena pueden ver esto: FERR existe y TORN existe. " +
            await Escenario.Detalle(respuesta));

        JsonElement problema = await Problema(respuesta);
        problema.GetProperty("type").GetString().ShouldBe("/errors/categoria-ciclo");

        // El mensaje dice POR DÓNDE vuelve la cadena. Un «hay un ciclo» a secas obliga a quien lo
        // lee a reconstruirla a mano sobre un árbol que puede tener cientos de ramas.
        problema.GetProperty("detail").GetString()!.ShouldContain("TORN");
    }

    /// <summary>La cota del ascenso, ejercida contra el repositorio de verdad.</summary>
    /// <remarks>
    /// La cota tiene su caso en <c>Catalogo.UnitTests</c>, contra un doble en memoria. Aquí se
    /// ejerce contra PostgreSQL porque el recorrido lo hace el repositorio consulta a consulta, y
    /// lo que se comprueba es que la cota está <b>en el camino real</b>: sin ella, cada nivel de
    /// más es una consulta de más por petición, y sobre datos cíclicos —que se pueden meter con un
    /// <c>UPDATE</c> a mano o una importación— sería una petición que no termina nunca.
    /// </remarks>
    [Fact]
    public async Task Colgar_una_categoria_por_debajo_del_nivel_maximo_es_409_y_lo_dice()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(179);

        Guid? padre = null;

        // El árbol entero que la cota admite: la raíz, en el nivel cero, y `ProfundidadMaxima`
        // niveles por debajo de ella. Son once categorías, no diez, y esa diferencia es justo la
        // frontera — por eso el bucle llega hasta la cota incluida y no hasta una menos.
        for (int nivel = 0; nivel <= Categoria.ProfundidadMaxima; nivel++)
        {
            CategoriaDto una = await CrearCategoriaAsync(
                cliente,
                "N" + nivel.ToString(CultureInfo.InvariantCulture),
                "Nivel " + nivel.ToString(CultureInfo.InvariantCulture),
                padre);

            padre = una.Id;
        }

        // Los que caben se han creado sin protestar —esa es la mitad que se olvida: una cota que
        // rechazara uno de menos también daría 409 abajo, y este caso saldría verde igual—, y el
        // siguiente no cabe. La cota se lee del dominio y no se copia: escrita aquí como un 10, el
        // día que se moviera allí este caso seguiría verde comprobando otra cosa.
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Categorias,
            new CrearCategoriaDto
            {
                Codigo = "SOBRA",
                Nombre = "Uno más de los que caben",
                PadreId = padre,
            });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(respuesta));
        (await Problema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/categoria-demasiado-profunda");
    }

    [Fact]
    public async Task Una_categoria_padre_que_no_existe_es_400_y_no_un_409_de_ciclo()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(180);

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Categorias,
            new CrearCategoriaDto
            {
                Codigo = "HUERFANA",
                Nombre = "Huérfana",
                PadreId = Guid.NewGuid(),
            });

        // Validación y no conflicto: hay que corregir el identificador, no elegir otro sitio del
        // árbol. Y vale igual para «no existe» que para «es de otra empresa», porque la consulta
        // lleva puesto el filtro de inquilinato y una categoría prestada no se ve.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));
        (await Problema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/categoria-padre-no-encontrado");
    }

    [Fact]
    public async Task El_listado_de_articulos_viene_paginado_con_su_total_y_filtra_por_categoria()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(181);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 181);

        CategoriaDto herramientas = await CrearCategoriaAsync(cliente, "HERR", "Herramientas");

        await CrearAsync(cliente, Alta("MAR-1", unidad, impuesto, "Martillo", herramientas.Id));
        await CrearAsync(cliente, Alta("DES-1", unidad, impuesto, "Destornillador", herramientas.Id));
        await CrearAsync(cliente, Alta("CLA-1", unidad, impuesto, "Caja de clavos"));

        // La empresa es nueva, así que estos tres son todos los que hay: el total es una afirmación
        // sobre lo que este caso ha creado y no sobre lo que haya dejado otro.
        PaginaDe<ArticuloDto> todos = await ListarAsync(cliente, $"{Articulos}?page=1&size=50");

        todos.Total.ShouldBe(3);

        PaginaDe<ArticuloDto> deLaCategoria = await ListarAsync(
            cliente, $"{Articulos}?page=1&size=50&categoria={herramientas.Id}");

        deLaCategoria.Total.ShouldBe(2);
        deLaCategoria.Elementos.ShouldAllBe(uno => uno.CategoriaId == herramientas.Id);

        // El filtro de texto mira código y descripción, que es lo que se lee en una pantalla.
        PaginaDe<ArticuloDto> buscados = await ListarAsync(
            cliente, $"{Articulos}?page=1&size=50&q=clavos");

        buscados.Total.ShouldBe(1);
        buscados.Elementos[0].Codigo.ShouldBe("CLA-1");
    }

    [Fact]
    public async Task Un_articulo_que_no_existe_es_404_con_ProblemDetails()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(182);

        using HttpResponseMessage respuesta =
            await cliente.GetAsync($"{Articulos}/{Guid.NewGuid()}");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Problema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/articulo-no-encontrado");
    }

    [Fact]
    public async Task El_tipo_del_articulo_viaja_como_TEXTO_y_no_como_numero()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(183);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 183);

        using HttpResponseMessage creacion = await cliente.PostAsJsonAsync(
            Articulos, Alta("SERV", unidad, impuesto) with { Tipo = "Servicio" });

        creacion.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(creacion));

        // Un ordinal es un contrato que se rompe solo con reordenar el enumerado, y quien lo
        // reordena no ve que está rompiendo a nadie.
        (await creacion.Content.ReadAsStringAsync()).ShouldContain("\"tipo\":\"Servicio\"");

        using HttpResponseMessage inventado = await cliente.PostAsJsonAsync(
            Articulos, Alta("RARO", unidad, impuesto) with { Tipo = "Intangible" });

        inventado.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Problema(inventado)).GetProperty("type").GetString()
            .ShouldBe("/errors/articulo-tipo-no-valido");
    }

    // ------------------------------------------------------------------------------- ayudantes

    private static CrearArticuloDto Alta(
        string codigo,
        Guid unidad,
        Guid impuesto,
        string descripcion = "Artículo de prueba",
        Guid? categoria = null) => new()
        {
            Codigo = codigo,
            Descripcion = descripcion,
            Tipo = "Bien",
            UnidadBaseId = unidad,
            ImpuestoPorDefectoId = impuesto,
            CategoriaId = categoria,
        };

    private static ModificarArticuloDto Cambio(string descripcion, Guid impuesto) => new()
    {
        Descripcion = descripcion,
        Tipo = "Bien",
        ImpuestoPorDefectoId = impuesto,
        CategoriaId = null,
    };

    private async Task<HttpClient> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));
        _clientes.Add(cliente);

        return cliente;
    }

    /// <summary>Una unidad y un tramo vigente, propios de este caso, listos para usarse.</summary>
    /// <remarks>
    /// Los dos llevan el número del caso en el código porque son maestros de instalación: los ve
    /// toda la base compartida. El tramo empieza el 1 de enero de 2000 y no tiene fin, así que
    /// rige hoy sea cual sea el día en que corra la CI — una fecha «de este año» escrita a mano se
    /// caduca sola y pone rojo un test que no ha cambiado.
    /// </remarks>
    private static async Task<(Guid Unidad, Guid Impuesto)> MaestrosAsync(
        HttpClient cliente,
        int semilla)
    {
        string sufijo = semilla.ToString(CultureInfo.InvariantCulture);

        using HttpResponseMessage unidad = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto { Codigo = "U" + sufijo, Nombre = "Unidad " + sufijo, Decimales = 0 });

        unidad.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(unidad));

        UnidadMedidaDto medida = (await unidad.Content.ReadFromJsonAsync<UnidadMedidaDto>())!;

        Guid impuesto = await CrearImpuestoAsync(
            cliente, "CAT" + sufijo, new DateOnly(2000, 1, 1), null);

        return (medida.Id, impuesto);
    }

    private static async Task<Guid> CrearImpuestoAsync(
        HttpClient cliente,
        string codigo,
        DateOnly desde,
        DateOnly? hasta)
    {
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Impuestos,
            new CrearImpuestoDto
            {
                Codigo = codigo,
                Nombre = "Tramo " + codigo,
                Tipo = "Iva",
                Porcentaje = 21m,
                VigenteDesde = desde,
                VigenteHasta = hasta,
            });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<ImpuestoDto>())!.Id;
    }

    private static async Task<ArticuloDto> CrearAsync(HttpClient cliente, CrearArticuloDto alta)
    {
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Articulos, alta);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<ArticuloDto>())!;
    }

    private static async Task<CategoriaDto> CrearCategoriaAsync(
        HttpClient cliente,
        string codigo,
        string nombre,
        Guid? padre = null)
    {
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Categorias,
            new CrearCategoriaDto { Codigo = codigo, Nombre = nombre, PadreId = padre });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<CategoriaDto>())!;
    }

    private static async Task<PaginaDe<ArticuloDto>> ListarAsync(HttpClient cliente, string ruta)
    {
        using HttpResponseMessage respuesta = await cliente.GetAsync(ruta);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<PaginaDe<ArticuloDto>>())!;
    }

    private static async Task<JsonElement> Problema(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
    }
}
