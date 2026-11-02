using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Pruebas.Comun;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Contrato;

/// <summary>
/// Las tarifas contra PostgreSQL: la restricción de exclusión, la precedencia resuelta con SQL de
/// verdad y los errores con nombre del camino de resolución.
/// </summary>
/// <remarks>
/// <para>
/// <b>La mitad que este carril guarda y el rápido no puede.</b> La precedencia se decide en C#
/// —<c>ElAntepasadoMasCercanoGana</c>, con sus casos en milisegundos—, pero lo que le llega para
/// decidir sale de dos consultas traducidas a SQL: la ascendencia de la categoría y las candidatas
/// de la tarifa. Un ascenso que trajera de más, o un <c>= ANY</c> que se dejara fuera la raíz,
/// dejan verde el carril rápido entero y devuelven otro precio. Aquí es donde se ven.
/// </para>
/// <para>
/// <b>Y la restricción de exclusión no tiene otro sitio donde ejercerse.</b> No es una rama de C#
/// que se pueda simular: es DDL, y o está en la base o no está. El caso que la ejerce escribe
/// <b>sin pasar por el caso de uso</b>, a propósito: la comprobación previa de
/// <c>HaySolapeAsync</c> cubre a un cliente educado, y lo que hay que ver es qué pasa con el que
/// no lo es —o con dos peticiones simultáneas, que es el caso real y el que ninguna comprobación
/// previa puede cubrir—.
/// </para>
/// <para>
/// <b>Las divisas son maestros de instalación</b> (R8) y se ven desde toda la base compartida,
/// pero su código es ISO 4217 y no admite un sufijo con el número del caso. Por eso
/// <c>DivisaAsync</c> da de alta o reutiliza: dos ficheros que eligieran el mismo código
/// chocarían, y el 409 del segundo se leería como un fallo de la API.
/// </para>
/// <para>
/// <b>Las semillas de este fichero son el bloque 190-194, y el bloque importa.</b> La semilla
/// escribe el NIF de la empresa que el caso da de alta, y el NIF es único en toda la instalación:
/// dos ficheros que compartieran una semilla se pisarían. El choque no sale por el que llega
/// segundo, sale por EL OTRO —quien recibe el 409 es el que corriera después—, así que se lee como
/// un fallo ajeno. <c>ContratoDeCatalogoTests</c> tiene el 170-183.
/// </para>
/// <para>
/// <b>Y la elegida es el euro, que es la única de las cinco del catálogo que nadie retira.</b> El
/// catálogo de redondeos solo conoce EUR, USD, GBP, CHF y JPY —una divisa sin caso dorado se
/// rechaza al darla de alta, a propósito—, y <c>LaRetiradaNoEsUnBloqueoTests</c> retira las otras
/// cuatro y las deja retiradas. Cualquiera de ellas aquí haría que estos casos salieran rojos o
/// verdes según el orden en que el ejecutor hubiera corrido los ficheros de la colección.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ContratoDeTarifasTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Tarifas = "/api/v1/catalogo/tarifas";
    private const string Articulos = "/api/v1/catalogo/articulos";
    private const string Categorias = "/api/v1/catalogo/categorias";
    private const string Divisas = "/api/v1/organizacion/divisas";
    private const string Unidades = "/api/v1/organizacion/unidades-de-medida";
    private const string Impuestos = "/api/v1/organizacion/impuestos";

    private static readonly DateOnly s_desde = new(2000, 1, 1);

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

    /// <summary>
    /// <c>btree_gist</c> está puesta, la puso una <b>migración</b>, y la imagen en la que está es
    /// la que despliega el compose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las tres mitades juntas, porque por separado ninguna dice nada.</b> Que la extensión
    /// esté en esta base no sirve si esta base no es la que se despliega: una extensión creada a
    /// mano en el PostgreSQL de desarrollo deja verde todo lo de aquí y revienta el despliegue el
    /// día del estreno, con un mensaje —<c>data type uuid has no default operator class for access
    /// method gist</c>— que no menciona ni la tarifa ni el solape.
    /// </para>
    /// <para>
    /// Y se comprueba <b>por el efecto</b>: este contenedor no ha corrido nada más que las
    /// migraciones, así que la extensión que hay en él la puso una migración. La comparación con
    /// el compose se hace leyendo el fichero y no copiando la etiqueta: una constante repetida a
    /// mano en dos sitios es exactamente lo que se queda atrás cuando uno de los dos sube.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_extension_btree_gist_la_puso_la_MIGRACION_en_la_imagen_del_compose()
    {
        PostgresConTodosLosModulos.Imagen.ShouldBe(
            ImagenDelCompose(),
            "el contenedor contra el que se prueba y el que levanta `deploy/docker-compose.yml` " +
            "tienen que ser el MISMO. Con versiones distintas, esta suite responde por una base " +
            "que nadie despliega");

        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using (NpgsqlCommand extension = new(
            "SELECT count(*) FROM pg_extension WHERE extname = 'btree_gist';", conexion))
        {
            object? cuantas = await extension.ExecuteScalarAsync();

            Convert.ToInt64(cuantas, CultureInfo.InvariantCulture).ShouldBe(
                1L,
                "la extensión no está. Este contenedor solo ha corrido migraciones, así que si " +
                "falta es que falta en la migración — y donde se notaría es en el despliegue");
        }

        // Y el efecto de tenerla: la restricción existe y es de EXCLUSIÓN (`x`). Sin `btree_gist`
        // la migración no habría podido crearla, porque `=` sobre `uuid` no es GiST de serie.
        await using NpgsqlCommand restriccion = new(
            "SELECT contype FROM pg_constraint WHERE conname = 'tarifas_sin_tramos_solapados';",
            conexion);

        object? tipo = await restriccion.ExecuteScalarAsync();

        tipo.ShouldNotBeNull("la restricción de exclusión de las tarifas no está en la base");
        ((char)tipo!).ShouldBe(
            'x',
            "la restricción existe pero no es de exclusión. Un índice único no puede expresar " +
            "que lo que no se repite es un RANGO: dejaría convivir dos tramos con fechas " +
            "distintas que se pisan");
    }

    /// <summary>
    /// Dos tramos del mismo código que se pisan los rechaza <b>la base</b>, no la comprobación
    /// previa.
    /// </summary>
    /// <remarks>
    /// Se escribe con SQL desnudo a propósito: por el caso de uso, quien contesta es
    /// <c>HaySolapeAsync</c>, y esa comprobación no cubre dos peticiones a la vez. Lo que este
    /// caso afirma es que por debajo hay una restricción que no depende de que nadie se acuerde de
    /// preguntar.
    /// </remarks>
    [Fact]
    public async Task Dos_tramos_del_mismo_codigo_que_se_pisan_los_rechaza_la_BASE()
    {
        var empresa = Guid.NewGuid();

        await InsertarTarifaAsync(empresa, "PVP", new DateOnly(2026, 1, 1), null);

        PostgresException choque = await Should.ThrowAsync<PostgresException>(
            () => InsertarTarifaAsync(empresa, "PVP", new DateOnly(2027, 6, 1), null));

        choque.SqlState.ShouldBe(
            PostgresErrorCodes.ExclusionViolation,
            "la base ha dejado entrar dos tramos del mismo código que se solapan. «La tarifa PVP " +
            "del día D» pasa a devolver dos filas, y cuál gana lo decide el plan de ejecución: " +
            "el síntoma no es un error, es un precio distinto de un día para otro sin que nadie " +
            "haya tocado nada");

        choque.ConstraintName.ShouldBe("tarifas_sin_tramos_solapados");

        // La otra empresa NO estorba, y es la mitad que se cae si alguien quita `empresa_id WITH =`
        // de la restricción: la tarifa PVP de una ferretería impediría a la imprenta de al lado
        // abrir la suya, con un rechazo que habla de una fila que no puede ni ver (R8).
        await InsertarTarifaAsync(Guid.NewGuid(), "PVP", new DateOnly(2026, 1, 1), null);
    }

    /// <summary>
    /// Dos tramos del mismo código y de distinto tipo los rechaza <b>la base</b>, aunque no se
    /// pisen.
    /// </summary>
    /// <remarks>
    /// El gemelo del solape, con el tipo (ADR-0076): <c>tarifas_un_tipo_por_codigo</c> excluye dos
    /// filas de la misma empresa y el mismo código cuyo tipo sea distinto. Las fechas van seguidas
    /// y sin pisarse a propósito, para que lo que salte sea esta restricción y no la del solape.
    /// Por el caso de uso, quien contesta es <c>TipoDelCodigoAsync</c>, y esa pregunta no cubre dos
    /// altas a la vez.
    /// </remarks>
    [Fact]
    public async Task Dos_tramos_del_mismo_codigo_y_distinto_tipo_los_rechaza_la_BASE()
    {
        var empresa = Guid.NewGuid();
        DateOnly frontera = new(2026, 12, 31);

        await InsertarTarifaAsync(empresa, "PRV", new DateOnly(2026, 1, 1), frontera, "Compra");

        PostgresException choque = await Should.ThrowAsync<PostgresException>(
            () => InsertarTarifaAsync(empresa, "PRV", frontera.AddDays(1), null, "Venta"));

        choque.SqlState.ShouldBe(
            PostgresErrorCodes.ExclusionViolation,
            "la base ha dejado entrar un tramo de venta en un código cuyos tramos son de compra. " +
            "Según el día, la tarifa PRV le daría a un cliente los precios de un proveedor");

        choque.ConstraintName.ShouldBe("tarifas_un_tipo_por_codigo");

        // Las parejas. El tramo siguiente del mismo tipo entra: sin esta, una restricción que
        // rechazara cualquier segundo tramo del código pasaría la aserción de arriba y subir
        // precios sería imposible. Y el otro tipo entra con otro código, y en otra empresa con el
        // mismo, que es la mitad que se cae si alguien quita `empresa_id WITH =` (R8).
        await InsertarTarifaAsync(empresa, "PRV", frontera.AddDays(1), null, "Compra");
        await InsertarTarifaAsync(empresa, "PVP", new DateOnly(2026, 1, 1), null, "Venta");
        await InsertarTarifaAsync(Guid.NewGuid(), "PRV", new DateOnly(2026, 1, 1), null, "Venta");
    }

    /// <summary>Un tipo que no es ni de venta ni de compra lo rechaza la base.</summary>
    /// <remarks>
    /// La restricción se escribe con los nombres del enumerado, y en mayúsculas y minúsculas como
    /// están: «venta» no es un tipo, igual que no lo es para el caso de uso.
    /// </remarks>
    [Fact]
    public async Task Un_tipo_que_no_es_venta_ni_compra_lo_rechaza_la_BASE()
    {
        PostgresException choque = await Should.ThrowAsync<PostgresException>(
            () => InsertarTarifaAsync(Guid.NewGuid(), "OTRO", new DateOnly(2026, 1, 1), null, "venta"));

        choque.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        choque.ConstraintName.ShouldBe("ck_tarifas_tipo");
    }

    /// <summary>El día en que un tramo <b>acaba</b> todavía cuenta para el solape.</summary>
    /// <remarks>
    /// La convención <c>'[]'</c> —cerrada por los dos lados— escrita como un caso. Con <c>'[)'</c>
    /// aquí, estos dos tramos convivirían y el día de la frontera tendría dos tarifas; y como
    /// <c>Tarifa.RigeEl</c> sí incluye ese día, la resolución devolvería una de las dos según el
    /// plan. El solape que se cuela es peor que el que se rechaza.
    /// </remarks>
    [Fact]
    public async Task El_dia_en_que_un_tramo_ACABA_todavia_cuenta_para_el_solape()
    {
        var empresa = Guid.NewGuid();
        DateOnly frontera = new(2026, 12, 31);

        await InsertarTarifaAsync(empresa, "MAY", new DateOnly(2026, 1, 1), frontera);

        PostgresException choque = await Should.ThrowAsync<PostgresException>(
            () => InsertarTarifaAsync(empresa, "MAY", frontera, null));

        choque.SqlState.ShouldBe(
            PostgresErrorCodes.ExclusionViolation,
            "el último día de un tramo y el primero del siguiente son el mismo y han entrado los " +
            "dos. El rango de la restricción no está cerrado por arriba, y entonces la base y " +
            "`Tarifa.RigeEl` no dicen lo mismo de ese día");

        // Y el día siguiente SÍ entra: sin esta mitad, una restricción que rechazara todo pasaría
        // la aserción de arriba y nadie podría encadenar dos tramos consecutivos.
        await InsertarTarifaAsync(empresa, "MAY", frontera.AddDays(1), null);
    }

    /// <summary>
    /// «No hay tarifa» y «hay tarifa y su vigencia no cubre este día» son <b>dos</b> <c>type</c>.
    /// </summary>
    /// <remarks>
    /// Se arreglan distinto —la primera escribiendo bien el código, la segunda abriendo el tramo
    /// que falta—, así que quien recibe la respuesta tiene que poder distinguirlas sin leer la
    /// prosa. Sus códigos HTTP también difieren: un código que no existe es un 404, y una fecha
    /// que ningún tramo cubre es un 400, porque la tarifa está ahí delante de quien pregunta.
    /// </remarks>
    [Fact]
    public async Task Una_tarifa_que_no_existe_y_una_que_no_cubre_el_dia_son_DOS_type_distintos()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(190);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 190);
        ArticuloDto articulo = await CrearArticuloAsync(cliente, "ART-190", unidad, impuesto, null);

        TarifaDto tarifa = await CrearTarifaAsync(
            cliente,
            "T190",
            await DivisaAsync(cliente, "EUR", "Euro"),
            s_desde,
            new DateOnly(2001, 12, 31));

        using HttpResponseMessage inventada = await cliente.GetAsync(
            $"{Tarifas}/NO-EXISTE/precio?articulo={articulo.Id}&cantidad=1");

        inventada.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(inventada));
        (await Problema(inventada)).GetProperty("type").GetString()
            .ShouldBe("/errors/tarifa-no-encontrada");

        using HttpResponseMessage fueraDeVigencia = await cliente.GetAsync(
            $"{Tarifas}/{tarifa.Codigo}/precio?articulo={articulo.Id}&cantidad=1&fecha=2026-09-11");

        fueraDeVigencia.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, await Escenario.Detalle(fueraDeVigencia));

        (await Problema(fueraDeVigencia)).GetProperty("type").GetString().ShouldBe(
            "/errors/tarifa-no-vigente",
            "una tarifa que existe y no cubre el día contesta lo mismo que una que no existe. El " +
            "descuido normal —una tarifa caducada sin sucesora— saldría como «esa tarifa no " +
            "existe» delante de quien la tiene en pantalla");
    }

    /// <summary>
    /// El tipo y la divisa por HTTP: la divisa se guarda con su código en mayúsculas, y cada
    /// rechazo del alta tiene su <c>type</c>.
    /// </summary>
    /// <remarks>
    /// Los <c>type</c> son el contrato del frontal (ADR-0030), y los tres se arreglan distinto: un
    /// tipo que no es ninguno se corrige eligiendo uno, un código cuyos tramos son del otro tipo se
    /// corrige con otro código, y una divisa que no existe, escribiéndola bien.
    /// </remarks>
    [Fact]
    public async Task La_divisa_se_guarda_con_su_codigo_y_cada_rechazo_del_tipo_tiene_su_type()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(194);
        await DivisaAsync(cliente, "EUR", "Euro");

        using (HttpResponseMessage alta = await PedirTarifaAsync(
                   cliente, "T194", "Compra", "eur", s_desde, new DateOnly(2001, 12, 31)))
        {
            alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

            TarifaDto guardada = (await alta.Content.ReadFromJsonAsync<TarifaDto>())!;

            (guardada.Tipo, guardada.Divisa).ShouldBe(("Compra", "EUR"));
            (await cliente.GetFromJsonAsync<TarifaDto>($"{Tarifas}/{guardada.Id}"))!
                .ShouldBe(guardada, "lo que se lee de la base es lo que contestó el alta");
        }

        await RechazadaAsync(
            cliente, "T194", "Venta", "EUR", HttpStatusCode.Conflict, "tarifa-tipo-distinto-del-codigo");
        await RechazadaAsync(
            cliente, "T194B", "Alquiler", "EUR", HttpStatusCode.BadRequest, "tarifa-tipo-no-valido");
        await RechazadaAsync(
            cliente, "T194C", "Venta", "XYZ", HttpStatusCode.BadRequest, "tarifa-divisa-no-encontrada");

        static async Task RechazadaAsync(
            HttpClient cliente,
            string codigo,
            string tipo,
            string divisa,
            HttpStatusCode esperado,
            string type)
        {
            using HttpResponseMessage intento = await PedirTarifaAsync(
                cliente, codigo, tipo, divisa, new DateOnly(2002, 1, 1), null);

            intento.StatusCode.ShouldBe(esperado, await Escenario.Detalle(intento));
            (await Problema(intento)).GetProperty("type").GetString().ShouldBe("/errors/" + type);
        }
    }

    /// <summary>Sin línea aplicable hay un error con nombre, y <b>nunca</b> un precio cero.</summary>
    /// <remarks>
    /// La decisión 3 del ADR-0023 con otro sujeto, ejercida por HTTP. El cero no da error al
    /// resolverlo: entra en una línea de documento, suma cero al total, y el descuadre aparece
    /// semanas después sin autor.
    /// </remarks>
    [Fact]
    public async Task Sin_linea_aplicable_hay_error_con_nombre_y_NUNCA_un_precio_cero()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(191);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 191);
        ArticuloDto articulo = await CrearArticuloAsync(cliente, "ART-191", unidad, impuesto, null);

        TarifaDto tarifa = await CrearTarifaAsync(
            cliente, "T191", await DivisaAsync(cliente, "EUR", "Euro"), s_desde, null);

        using HttpResponseMessage resolucion = await cliente.GetAsync(
            $"{Tarifas}/{tarifa.Codigo}/precio?articulo={articulo.Id}&cantidad=1");

        resolucion.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest,
            "la tarifa rige y no dice nada de este artículo, y aun así ha contestado un precio. " +
            await Escenario.Detalle(resolucion));

        (await Problema(resolucion)).GetProperty("type").GetString()
            .ShouldBe("/errors/tarifa-sin-linea-aplicable");
    }

    /// <summary>
    /// Gana el antepasado <b>más cercano</b>, y una categoría más honda de otra rama no compite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Aquí «más cercana» y «más profunda» dejan de parecer lo mismo.</b> El artículo cuelga de
    /// una categoría a profundidad 2. Hay línea en su madre —a un salto—, línea en la raíz —a dos—
    /// y línea en una categoría a profundidad 3 que cuelga por el otro lado del árbol. Gana la
    /// madre; la honda no compite siquiera, porque no es antepasada suya.
    /// </para>
    /// <para>
    /// Contra PostgreSQL y no solo en memoria porque quien decide qué llega a la regla es la
    /// consulta de ascendencia. Un ascenso que trajera de más pone esta aserción roja y deja verde
    /// el carril rápido entero.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Gana_el_antepasado_MAS_CERCANO_y_la_mas_honda_de_otra_rama_no_compite()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(192);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 192);

        CategoriaDto raiz = await CrearCategoriaAsync(cliente, "R192", "Raíz", null);
        CategoriaDto madre = await CrearCategoriaAsync(cliente, "M192", "Madre", raiz.Id);
        CategoriaDto suya = await CrearCategoriaAsync(cliente, "S192", "Suya", madre.Id);

        // La otra rama, MÁS HONDA que la del artículo y sin parentesco con él.
        CategoriaDto otra = await CrearCategoriaAsync(cliente, "O192", "Otra rama", raiz.Id);
        CategoriaDto honda = await CrearCategoriaAsync(cliente, "H192", "Honda", otra.Id);
        CategoriaDto masHonda = await CrearCategoriaAsync(cliente, "P192", "Más honda", honda.Id);

        ArticuloDto articulo = await CrearArticuloAsync(
            cliente, "ART-192", unidad, impuesto, suya.Id);

        TarifaDto tarifa = await CrearTarifaAsync(
            cliente, "T192", await DivisaAsync(cliente, "EUR", "Euro"), s_desde, null);

        await CrearLineaAsync(
            cliente, tarifa.Id, new CrearLineaTarifaDto
            {
                CategoriaId = madre.Id,
                CantidadDesde = 0m,
                Precio = 12m,
            });

        await CrearLineaAsync(
            cliente, tarifa.Id, new CrearLineaTarifaDto
            {
                CategoriaId = raiz.Id,
                CantidadDesde = 0m,
                Precio = 30m,
            });

        await CrearLineaAsync(
            cliente, tarifa.Id, new CrearLineaTarifaDto
            {
                CategoriaId = masHonda.Id,
                CantidadDesde = 0m,
                Precio = 1m,
            });

        PrecioResueltoDto resuelto = await ResolverAsync(cliente, tarifa.Codigo, articulo.Id, 1m);

        resuelto.Precio.ShouldBe(
            12m,
            "no ha ganado la madre. Con 30 gana la raíz —el antepasado más LEJANO—; con 1 gana " +
            "una categoría más honda que no es antepasada del artículo y que no tenía que " +
            "competir siquiera");

        resuelto.Origen.ShouldBe("Categoria");
        resuelto.OrigenId.ShouldBe(madre.Id);
        resuelto.NivelDeLaCategoria.ShouldBe(1, "la madre está a UN salto del artículo");
        resuelto.Divisa.ShouldBe(tarifa.Divisa, "el precio viaja siempre con su divisa");
        resuelto.Divisa.ShouldBe("EUR");
    }

    /// <summary>
    /// La línea del artículo le gana a la de su categoría, y el tramo se elige <b>después</b>.
    /// </summary>
    /// <remarks>
    /// Las dos mitades en el mismo caso. La primera es la precedencia. La segunda es el orden en
    /// que se desempata: primero quién pone el precio, y solo entre las líneas de ese destino,
    /// cuál tramo. Al revés —la <c>CantidadDesde</c> más alta de todas las candidatas— una línea
    /// de categoría con un tramo alto le ganaría a la del propio artículo. Y de paso la frontera:
    /// pedir exactamente 100 aplica el tramo de 100.
    /// </remarks>
    [Fact]
    public async Task La_linea_del_articulo_gana_y_el_tramo_se_elige_DESPUES_con_la_frontera_arriba()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(193);
        (Guid unidad, Guid impuesto) = await MaestrosAsync(cliente, 193);

        CategoriaDto categoria = await CrearCategoriaAsync(cliente, "C193", "Categoría", null);
        ArticuloDto articulo = await CrearArticuloAsync(
            cliente, "ART-193", unidad, impuesto, categoria.Id);

        TarifaDto tarifa = await CrearTarifaAsync(
            cliente, "T193", await DivisaAsync(cliente, "EUR", "Euro"), s_desde, null);

        // La categoría, con un tramo ALTO que en un desempate por cantidad ganaría a las dos del
        // artículo.
        await CrearLineaAsync(
            cliente, tarifa.Id, new CrearLineaTarifaDto
            {
                CategoriaId = categoria.Id,
                CantidadDesde = 0m,
                Precio = 50m,
            });

        await CrearLineaAsync(
            cliente, tarifa.Id, new CrearLineaTarifaDto
            {
                CategoriaId = categoria.Id,
                CantidadDesde = 500m,
                Precio = 45m,
            });

        await CrearLineaAsync(
            cliente, tarifa.Id, new CrearLineaTarifaDto
            {
                ArticuloId = articulo.Id,
                CantidadDesde = 0m,
                Precio = 10m,
            });

        await CrearLineaAsync(
            cliente, tarifa.Id, new CrearLineaTarifaDto
            {
                ArticuloId = articulo.Id,
                CantidadDesde = 100m,
                Precio = 8m,
            });

        PrecioResueltoDto unaUnidad = await ResolverAsync(cliente, tarifa.Codigo, articulo.Id, 1m);

        unaUnidad.Precio.ShouldBe(
            10m,
            "ha ganado la línea de la categoría. Un precio pactado para una referencia concreta " +
            "es lo más específico que existe y no lo desbanca ningún tramo");

        unaUnidad.Origen.ShouldBe("Articulo");
        unaUnidad.NivelDeLaCategoria.ShouldBeNull("una línea de artículo no está a ningún salto");

        PrecioResueltoDto justoCien = await ResolverAsync(
            cliente, tarifa.Codigo, articulo.Id, 100m);

        justoCien.Precio.ShouldBe(
            8m,
            "pedir exactamente 100 no ha aplicado el tramo de 100. La frontera cae hacia ARRIBA: " +
            "es lo que significa una tabla que dice «a partir de 100», y nadie lo vería hasta " +
            "que alguien pidiera justo esa cantidad");

        justoCien.CantidadDesde.ShouldBe(100m);

        PrecioResueltoDto casiCien = await ResolverAsync(
            cliente, tarifa.Codigo, articulo.Id, 99.999999m);

        casiCien.Precio.ShouldBe(
            10m, "un paso por debajo de la frontera sigue en el tramo de abajo");
    }

    // ------------------------------------------------------------------ andamiaje

    /// <summary>La etiqueta de la imagen de PostgreSQL que declara el compose del despliegue.</summary>
    /// <remarks>
    /// Se lee del fichero en vez de copiar la etiqueta, por lo que explica el caso que la usa.
    /// </remarks>
    private static string ImagenDelCompose()
    {
        string compose = Path.Combine(RaizDelRepositorio.Ruta(), "deploy", "docker-compose.yml");
        File.Exists(compose).ShouldBeTrue($"no está {compose}");

        string[] lineas = File.ReadAllLines(compose);
        int postgres = Array.FindIndex(lineas, linea => linea.Trim() == "postgres:");

        postgres.ShouldBeGreaterThan(-1, "el compose ya no declara un servicio `postgres`");

        string? imagen = lineas
            .Skip(postgres)
            .Take(6)
            .Select(linea => linea.Trim())
            .FirstOrDefault(linea => linea.StartsWith("image:", StringComparison.Ordinal));

        imagen.ShouldNotBeNull("el servicio `postgres` del compose ya no declara imagen");

        return imagen!["image:".Length..].Trim();
    }

    /// <summary>Mete una tarifa con SQL desnudo, sin pasar por el caso de uso ni por EF.</summary>
    /// <remarks>
    /// Es lo que hace que el caso hable de la BASE y no de la comprobación previa: si el rechazo
    /// llegara del caso de uso, seguiría verde con la restricción borrada.
    /// </remarks>
    private async Task InsertarTarifaAsync(
        Guid empresaId,
        string codigo,
        DateOnly desde,
        DateOnly? hasta,
        string tipo = "Venta")
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand insercion = new(
            """
            INSERT INTO catalogo.tarifas
                (id, empresa_id, codigo, nombre, tipo, divisa, vigente_desde, vigente_hasta,
                 creado_en, modificado_en)
            VALUES (@id, @empresa, @codigo, @nombre, @tipo, 'EUR', @desde, @hasta, now(), now());
            """,
            conexion);

        insercion.Parameters.AddWithValue("id", Guid.NewGuid());
        insercion.Parameters.AddWithValue("empresa", empresaId);
        insercion.Parameters.AddWithValue("codigo", codigo);
        insercion.Parameters.AddWithValue("nombre", "Tramo " + codigo);
        insercion.Parameters.AddWithValue("tipo", tipo);
        insercion.Parameters.AddWithValue("desde", desde);
        insercion.Parameters.AddWithValue("hasta", (object?)hasta ?? DBNull.Value);

        await insercion.ExecuteNonQueryAsync();
    }

    private async Task<HttpClient> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));
        _clientes.Add(cliente);

        return cliente;
    }

    /// <summary>Una unidad y un tramo de impuesto propios de este caso, listos para usarse.</summary>
    /// <remarks>
    /// Los dos llevan el número del caso porque son maestros de instalación: los ve toda la base
    /// compartida. El tramo empieza en 2000 y no acaba, así que rige el día en que corra la CI.
    /// </remarks>
    private static async Task<(Guid Unidad, Guid Impuesto)> MaestrosAsync(
        HttpClient cliente,
        int semilla)
    {
        string sufijo = semilla.ToString(CultureInfo.InvariantCulture);

        using HttpResponseMessage unidad = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto
            {
                Codigo = "U" + sufijo,
                Nombre = "Unidad " + sufijo,
                Decimales = 0,
            });

        unidad.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(unidad));

        using HttpResponseMessage impuesto = await cliente.PostAsJsonAsync(
            Impuestos,
            new CrearImpuestoDto
            {
                Codigo = "TAR" + sufijo,
                Nombre = "Tramo TAR" + sufijo,
                Tipo = "Iva",
                Porcentaje = 21m,
                VigenteDesde = s_desde,
                VigenteHasta = null,
            });

        impuesto.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(impuesto));

        return (
            (await unidad.Content.ReadFromJsonAsync<UnidadMedidaDto>())!.Id,
            (await impuesto.Content.ReadFromJsonAsync<ImpuestoDto>())!.Id);
    }

    /// <summary>El código de la divisa, dándola de alta si todavía no está.</summary>
    /// <remarks>
    /// Desde el 3.3 la tarifa la guarda por su código (ADR-0076), así que basta con que esté: el
    /// <c>409</c> de un alta repetida dice eso mismo, y no hace falta buscar su identificador.
    /// </remarks>
    private static async Task<string> DivisaAsync(HttpClient cliente, string codigo, string nombre)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Divisas, new CrearDivisaDto { Codigo = codigo, Nombre = nombre });

        if (alta.StatusCode != HttpStatusCode.Created)
        {
            alta.StatusCode.ShouldBe(
                HttpStatusCode.Conflict,
                "si no se ha creado, lo único esperable es que ya estuviera. " +
                await Escenario.Detalle(alta));
        }

        return codigo;
    }

    private static async Task<TarifaDto> CrearTarifaAsync(
        HttpClient cliente,
        string codigo,
        string divisa,
        DateOnly desde,
        DateOnly? hasta)
    {
        using HttpResponseMessage alta = await PedirTarifaAsync(
            cliente, codigo, "Venta", divisa, desde, hasta);

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<TarifaDto>())!;
    }

    private static Task<HttpResponseMessage> PedirTarifaAsync(
        HttpClient cliente,
        string codigo,
        string tipo,
        string divisa,
        DateOnly desde,
        DateOnly? hasta) =>
        cliente.PostAsJsonAsync(
            Tarifas,
            new CrearTarifaDto
            {
                Codigo = codigo,
                Nombre = "Tarifa " + codigo,
                Tipo = tipo,
                Divisa = divisa,
                VigenteDesde = desde,
                VigenteHasta = hasta,
            });

    private static async Task CrearLineaAsync(
        HttpClient cliente,
        Guid tarifaId,
        CrearLineaTarifaDto linea)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            $"{Tarifas}/{tarifaId}/lineas", linea);

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
    }

    private static async Task<ArticuloDto> CrearArticuloAsync(
        HttpClient cliente,
        string codigo,
        Guid unidad,
        Guid impuesto,
        Guid? categoria)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Articulos,
            new CrearArticuloDto
            {
                Codigo = codigo,
                Descripcion = "Artículo " + codigo,
                Tipo = "Bien",
                UnidadBaseId = unidad,
                ImpuestoPorDefectoId = impuesto,
                CategoriaId = categoria,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ArticuloDto>())!;
    }

    private static async Task<CategoriaDto> CrearCategoriaAsync(
        HttpClient cliente,
        string codigo,
        string nombre,
        Guid? padre)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Categorias,
            new CrearCategoriaDto { Codigo = codigo, Nombre = nombre, PadreId = padre });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<CategoriaDto>())!;
    }

    private static async Task<PrecioResueltoDto> ResolverAsync(
        HttpClient cliente,
        string codigo,
        Guid articuloId,
        decimal cantidad)
    {
        string pedida = cantidad.ToString(CultureInfo.InvariantCulture);

        using HttpResponseMessage respuesta = await cliente.GetAsync(
            $"{Tarifas}/{codigo}/precio?articulo={articuloId}&cantidad={pedida}");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<PrecioResueltoDto>())!;
    }

    private static async Task<JsonElement> Problema(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
    }
}
