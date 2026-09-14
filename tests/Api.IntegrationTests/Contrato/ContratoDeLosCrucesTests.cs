using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Cruces;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Contrato;

/// <summary>
/// Los dos cruces mutuos del ítem 1.10 por la API y contra PostgreSQL: el proveedor de un artículo,
/// que Catálogo le pregunta a Terceros, y la tarifa de un tercero, que Terceros le pregunta a
/// Catálogo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada petición de aquí atraviesa un cruce, y esa es la comprobación de que está compuesto.</b>
/// Que el adaptador de un puerto esté registrado no se mira en el contenedor: se mira pidiendo algo
/// que solo se puede contestar preguntando al otro módulo. Si el registro falta, lo que falla es
/// una petición de verdad, igual que fallaría en producción.
/// </para>
/// <para>
/// <b>La respuesta del art. 32 al cruce nuevo está aquí escrita como casos</b>, no como una frase:
/// qué enseña el listado de proveedores cuando uno se bloquea, qué contesta el alta a un bloqueado
/// —lo mismo que a uno inventado—, y qué contesta cuando ese bloqueado ya suministraba el artículo
/// —lo mismo otra vez, y no un 409—.
/// </para>
/// <para>
/// <b>Del lado de Terceros el puerto no se marca aquí</b>: el alta de un proveedor junta los
/// estados que no autorizan en un solo 400, así que desde la API no se ve cuál contestó. Esas
/// casillas se afirman en <c>ElPuertoDeTercerosContraLaBaseTests</c>. <b>Del lado de las tarifas,
/// sí</b>: sus dos noes salen por dos <c>type</c> distintos y el 200 es el tercero, así que cada
/// respuesta nombra exactamente un estado del puerto.
/// </para>
/// <para>
/// <b>Las semillas son el bloque 200-214</b>: 200-205 en <c>ElPuertoDeTercerosContraLaBaseTests</c>
/// y 206-214 aquí. Los terceros van del 32 000 001 al 32 000 009 allí y del 32 000 010 en adelante
/// aquí. El NIF de una empresa es único en toda la instalación, y el de un tercero dentro de su
/// empresa; <c>ContratoDeTarifasTests</c> tiene el 190-193 y <c>ContratoDeCatalogoTests</c> el
/// 170-183, y un choque de semillas no sale por el que llega segundo sino por el otro.
/// </para>
/// <para>
/// <b>La divisa de las tarifas es el euro</b>, por lo mismo que en <c>ContratoDeTarifasTests</c>:
/// es la única de las cinco del catálogo de redondeos que ningún fichero retira.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ContratoDeLosCrucesTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Articulos = "/api/v1/catalogo/articulos";
    private const string Tarifas = "/api/v1/catalogo/tarifas";
    private const string Terceros = "/api/v1/terceros/terceros";
    private const string Divisas = "/api/v1/organizacion/divisas";
    private const string Unidades = "/api/v1/organizacion/unidades-de-medida";
    private const string Impuestos = "/api/v1/organizacion/impuestos";

    private const string TerceroNoValido = "/errors/articulo-proveedor-tercero-no-valido";

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
    /// Un proveedor de esta empresa se añade a un artículo con 201, su <c>Location</c> lleva al
    /// suministro, y el listado del artículo lo trae.
    /// </summary>
    [Fact]
    public async Task Un_proveedor_de_aqui_se_anade_con_201_y_el_listado_lo_trae()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(206);
        ArticuloDto articulo = await ArticuloAsync(cliente, 206);
        TerceroDto proveedor = await TerceroAsync(cliente, 32_000_010, esCliente: false, esProveedor: true);

        using HttpResponseMessage alta = await AnadirAsync(cliente, articulo.Id, proveedor.Id, "REF-206");

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        ArticuloProveedorDto suministro = (await alta.Content.ReadFromJsonAsync<ArticuloProveedorDto>())!;

        suministro.ArticuloId.ShouldBe(articulo.Id);
        suministro.TerceroId.ShouldBe(proveedor.Id);
        suministro.ReferenciaDelProveedor.ShouldBe("REF-206");

        alta.Headers.Location.ShouldNotBeNull();

        using HttpResponseMessage porSuLocation = await cliente.GetAsync(alta.Headers.Location);

        porSuLocation.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(porSuLocation));
        (await porSuLocation.Content.ReadFromJsonAsync<ArticuloProveedorDto>())!.Id.ShouldBe(suministro.Id);

        (await ListadoAsync(cliente, articulo.Id)).Select(uno => uno.Id).ShouldBe([suministro.Id]);
    }

    /// <summary>
    /// Uno inventado, uno de otra empresa, uno bloqueado y uno que solo es cliente contestan el
    /// MISMO 400, cuerpo a cuerpo, y ninguno deja una fila.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los cuatro cuerpos se comparan enteros</b>, con el identificador de traza sustituido y las
    /// claves ordenadas, como en <c>ElConflictoQueNoRevelaTests</c>: un mensaje distinto, un
    /// parámetro de más o un campo que alguien añada mañana a uno solo de los cuatro separaría lo
    /// que no se puede separar.
    /// </para>
    /// <para>
    /// <b>Y la contraria cierra el caso</b>: el mismo artículo acepta después a un proveedor de
    /// verdad. Sin ella, un artículo roto que rechazara a todo el mundo pasaría las cuatro.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Inventado_ajeno_bloqueado_y_solo_cliente_contestan_el_MISMO_400_y_no_dejan_fila()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(207);
        HttpClient enLaAjena = await EnUnaEmpresaNuevaAsync(208);
        ArticuloDto articulo = await ArticuloAsync(cliente, 207);

        TerceroDto deLaOtra = await TerceroAsync(enLaAjena, 32_000_011, esCliente: false, esProveedor: true);
        TerceroDto bloqueado = await TerceroAsync(cliente, 32_000_012, esCliente: false, esProveedor: true);
        TerceroDto soloCliente = await TerceroAsync(cliente, 32_000_013, esCliente: true, esProveedor: false);
        TerceroDto proveedor = await TerceroAsync(cliente, 32_000_014, esCliente: false, esProveedor: true);

        await BloquearAsync(cliente, bloqueado.Id);

        Dictionary<string, string> cuerpos = new(StringComparer.Ordinal);

        foreach ((string quien, Guid terceroId) in (IEnumerable<(string, Guid)>)
            [
                ("inventado", Guid.CreateVersion7()),
                ("de otra empresa", deLaOtra.Id),
                ("bloqueado", bloqueado.Id),
                ("solo cliente", soloCliente.Id),
            ])
        {
            using HttpResponseMessage alta = await AnadirAsync(cliente, articulo.Id, terceroId, null);

            alta.StatusCode.ShouldBe(
                HttpStatusCode.BadRequest,
                $"el {quien} no contesta 400. {await Escenario.Detalle(alta)}");

            (await TypeDe(alta)).ShouldBe(TerceroNoValido);

            cuerpos[quien] = await Comparable(alta);
        }

        cuerpos.Values.Distinct(StringComparer.Ordinal).Count().ShouldBe(
            1,
            "los cuatro que no se pueden añadir no contestan exactamente lo mismo. Quien tenga el " +
            "permiso de añadir proveedores puede recorrer identificadores y separar los que no " +
            "existen de los que existen y están reservados (art. 32 LOPDGDD, R16), o saber que un " +
            "identificador es de otra empresa (R8):\n" +
            string.Join("\n", cuerpos.Select(par => $"  {par.Key}: {par.Value}")));

        (await ListadoAsync(cliente, articulo.Id)).ShouldBeEmpty("un alta rechazada ha dejado fila");

        using HttpResponseMessage deVerdad = await AnadirAsync(cliente, articulo.Id, proveedor.Id, null);

        deVerdad.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            "el artículo rechaza también a un proveedor de verdad, así que los cuatro 400 de arriba " +
            $"no dicen nada del puerto. {await Escenario.Detalle(deVerdad)}");
    }

    /// <summary>
    /// El listado deja de enseñar al proveedor que se bloquea, la fila sigue, y al desbloquearlo
    /// vuelve la MISMA.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La respuesta del art. 32 para la lectura del cruce.</b> Catálogo no sabe qué terceros
    /// están bloqueados ni puede saberlo —el bloqueo está en otro esquema—, así que lo que esconde
    /// al bloqueado es la pregunta por el conjunto a <c>IConsultaDeTerceros</c>. El suministro no se
    /// borra: reservar no es borrar, y al desbloquear el tercero vuelve a salir con su identificador
    /// de siempre.
    /// </para>
    /// <para>
    /// <b>El otro proveedor del mismo artículo sigue en el listado todo el rato</b>, que es la
    /// contraria: un listado roto que se vaciara entero al bloquear a uno pasaría el paso del medio.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_listado_esconde_al_proveedor_bloqueado_y_desbloquearlo_devuelve_la_MISMA_fila()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(209);
        ArticuloDto articulo = await ArticuloAsync(cliente, 209);
        TerceroDto queSeBloquea = await TerceroAsync(cliente, 32_000_015, esCliente: false, esProveedor: true);
        TerceroDto queSigue = await TerceroAsync(cliente, 32_000_016, esCliente: true, esProveedor: true);

        Guid suministroQueSeEsconde = await AnadidoAsync(cliente, articulo.Id, queSeBloquea.Id);
        Guid suministroQueSigue = await AnadidoAsync(cliente, articulo.Id, queSigue.Id);

        (await IdsDelListadoAsync(cliente, articulo.Id))
            .ShouldBe(Ordenados(suministroQueSeEsconde, suministroQueSigue));

        await BloquearAsync(cliente, queSeBloquea.Id);

        (await IdsDelListadoAsync(cliente, articulo.Id)).ShouldBe(
            [suministroQueSigue],
            "el listado de proveedores de un artículo enseña a un tercero bloqueado. Es la " +
            "manera de leer, desde Catálogo, quién tiene sus datos reservados (art. 32 LOPDGDD)");

        await DesbloquearAsync(cliente, queSeBloquea.Id);

        (await IdsDelListadoAsync(cliente, articulo.Id)).ShouldBe(
            Ordenados(suministroQueSeEsconde, suministroQueSigue),
            "al desbloquear, el suministro no vuelve con su identificador de siempre: o el bloqueo " +
            "lo borró, o lo que vuelve es una copia");
    }

    /// <summary>
    /// Volver a añadir a un bloqueado que ya suministraba el artículo es el MISMO 400 que uno
    /// inventado, y no un 409; con el tercero disponible, el duplicado sí es 409.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Este caso es el que movió el orden del alta.</b> El listado ya no enseña ese suministro;
    /// si el alta contestara «ya está», la diferencia entre lo que el listado calla y lo que el alta
    /// dice contaría exactamente quién está bloqueado. Por eso el estado se pregunta antes que el
    /// duplicado.
    /// </para>
    /// <para>
    /// <b>El 409 va antes y después, con el tercero disponible</b>, que es la contraria: un alta que
    /// contestara siempre 400 al segundo intento pasaría el paso del medio sin que el orden tuviera
    /// nada que ver.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Volver_a_anadir_a_un_bloqueado_que_ya_suministraba_es_el_MISMO_400_y_no_un_409()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(210);
        ArticuloDto articulo = await ArticuloAsync(cliente, 210);
        TerceroDto tercero = await TerceroAsync(cliente, 32_000_017, esCliente: false, esProveedor: true);

        await AnadidoAsync(cliente, articulo.Id, tercero.Id);

        using (HttpResponseMessage duplicado = await AnadirAsync(cliente, articulo.Id, tercero.Id, null))
        {
            duplicado.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(duplicado));
        }

        await BloquearAsync(cliente, tercero.Id);

        using HttpResponseMessage delBloqueado = await AnadirAsync(cliente, articulo.Id, tercero.Id, null);
        using HttpResponseMessage delInventado = await AnadirAsync(cliente, articulo.Id, Guid.CreateVersion7(), null);

        delBloqueado.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest,
            "volver a añadir a un bloqueado que ya suministraba el artículo no contesta como uno " +
            "inventado. Si es un 409, el alta dice «ya está» de un suministro que el listado " +
            $"esconde, y eso es decir que está bloqueado. {await Escenario.Detalle(delBloqueado)}");

        (await Comparable(delBloqueado)).ShouldBe(await Comparable(delInventado));

        await DesbloquearAsync(cliente, tercero.Id);

        using HttpResponseMessage otraVez = await AnadirAsync(cliente, articulo.Id, tercero.Id, null);

        otraVez.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(otraVez));
    }

    /// <summary>
    /// Una tarifa que rige hoy se asigna a un tercero con 200, el GET la lee, y con nulo se quita.
    /// </summary>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDeTarifas), EstadoDeLaTarifa.RigeEnEsaFecha)]
    public async Task Una_tarifa_que_rige_hoy_se_asigna_se_lee_y_con_nulo_se_quita()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(211);
        TerceroDto tercero = await TerceroAsync(cliente, 32_000_018, esCliente: true, esProveedor: false);
        TarifaDto vigente = await TarifaAsync(cliente, "VIG", s_desde, null);

        using (HttpResponseMessage asignar = await AsignarTarifaAsync(cliente, tercero.Id, vigente.Id))
        {
            asignar.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(asignar));
            (await asignar.Content.ReadFromJsonAsync<TarifaAsignadaDto>())!.TarifaId.ShouldBe(vigente.Id);
        }

        (await TarifaAsignadaAsync(cliente, tercero.Id)).ShouldBe(new TarifaAsignadaDto(tercero.Id, vigente.Id));

        using (HttpResponseMessage quitar = await AsignarTarifaAsync(cliente, tercero.Id, null))
        {
            quitar.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(quitar));
        }

        (await TarifaAsignadaAsync(cliente, tercero.Id)).ShouldBe(new TarifaAsignadaDto(tercero.Id, null));
    }

    /// <summary>
    /// Una tarifa que ya no rige y una que todavía no rige son 409
    /// <c>tercero-tarifa-no-vigente</c>, y la ficha se queda con la que tenía.
    /// </summary>
    /// <remarks>
    /// Los dos extremos de la vigencia, porque el puerto los junta en un solo valor y un adaptador
    /// que mirase uno solo de los dos lados de la fecha pasaría con un único caso. Y la ficha tiene
    /// una tarifa vigente antes de intentarlo: sin ella, «no ha cambiado» sería comprobar que un
    /// nulo sigue siendo nulo.
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDeTarifas), EstadoDeLaTarifa.SoloResuelveLoViejo)]
    public async Task Una_tarifa_que_ya_no_rige_y_una_que_aun_no_son_409_y_la_ficha_sigue_con_la_suya()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(212);
        TerceroDto tercero = await TerceroAsync(cliente, 32_000_019, esCliente: true, esProveedor: false);

        TarifaDto vigente = await TarifaAsync(cliente, "VIG", s_desde, null);
        TarifaDto acabada = await TarifaAsync(cliente, "VIEJA", s_desde, new DateOnly(2001, 12, 31));
        TarifaDto futura = await TarifaAsync(cliente, "FUTURA", new DateOnly(2999, 1, 1), null);

        using (HttpResponseMessage asignar = await AsignarTarifaAsync(cliente, tercero.Id, vigente.Id))
        {
            asignar.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(asignar));
        }

        foreach ((string cual, TarifaDto tarifa) in (IEnumerable<(string, TarifaDto)>)
            [("la que ya no rige", acabada), ("la que todavía no rige", futura)])
        {
            using HttpResponseMessage intento = await AsignarTarifaAsync(cliente, tercero.Id, tarifa.Id);

            intento.StatusCode.ShouldBe(
                HttpStatusCode.Conflict,
                $"{cual} se ha asignado a un tercero, que la propondría en cada venta nueva. " +
                await Escenario.Detalle(intento));

            (await TypeDe(intento)).ShouldBe("/errors/tercero-tarifa-no-vigente");
        }

        (await TarifaAsignadaAsync(cliente, tercero.Id)).TarifaId.ShouldBe(
            vigente.Id, "un intento rechazado ha tocado la tarifa de la ficha");
    }

    /// <summary>
    /// Una tarifa inventada y una de OTRA empresa son 400 <c>tercero-tarifa-no-encontrada</c>, y la
    /// de la otra empresa se asigna sin problema en la suya.
    /// </summary>
    /// <remarks>
    /// La contraria es la R8 dicha al revés: la misma tarifa, preguntada desde su empresa, rige. Sin
    /// ese paso, un puerto que contestara <c>NoExiste</c> a todo pasaría los dos primeros.
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDeTarifas), EstadoDeLaTarifa.NoExiste)]
    public async Task Una_tarifa_inventada_y_una_de_otra_empresa_son_400_y_la_ajena_vale_en_la_suya()
    {
        HttpClient cliente = await EnUnaEmpresaNuevaAsync(213);
        HttpClient enLaAjena = await EnUnaEmpresaNuevaAsync(214);

        TerceroDto tercero = await TerceroAsync(cliente, 32_000_020, esCliente: true, esProveedor: false);
        TerceroDto terceroDeLaOtra = await TerceroAsync(enLaAjena, 32_000_021, esCliente: true, esProveedor: false);
        TarifaDto deLaOtra = await TarifaAsync(enLaAjena, "AJENA", s_desde, null);

        foreach ((string cual, Guid tarifaId) in (IEnumerable<(string, Guid)>)
            [("inventada", Guid.CreateVersion7()), ("de otra empresa", deLaOtra.Id)])
        {
            using HttpResponseMessage intento = await AsignarTarifaAsync(cliente, tercero.Id, tarifaId);

            intento.StatusCode.ShouldBe(
                HttpStatusCode.BadRequest,
                $"la tarifa {cual} no contesta 400. {await Escenario.Detalle(intento)}");

            (await TypeDe(intento)).ShouldBe(
                "/errors/tercero-tarifa-no-encontrada",
                cual == "de otra empresa"
                    ? "una tarifa de otra empresa se ve desde esta: el tercero quedaría asignado a " +
                      "la lista de precios de otra empresa de la instalación (R8)"
                    : null);
        }

        (await TarifaAsignadaAsync(cliente, tercero.Id)).TarifaId.ShouldBeNull();

        using HttpResponseMessage enSuEmpresa = await AsignarTarifaAsync(enLaAjena, terceroDeLaOtra.Id, deLaOtra.Id);

        enSuEmpresa.StatusCode.ShouldBe(
            HttpStatusCode.OK, $"en su propia empresa la tarifa sí rige. {await Escenario.Detalle(enSuEmpresa)}");
    }

    private async Task<HttpClient> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));
        _clientes.Add(cliente);

        return cliente;
    }

    /// <summary>Un artículo con su unidad y su tramo de impuesto, propios de este caso.</summary>
    /// <remarks>
    /// La unidad y el tramo llevan el número del caso porque son maestros de instalación: los ve
    /// toda la base compartida. El prefijo del tramo es <c>CRU</c> para no cruzarse con el
    /// <c>TAR</c> de <c>ContratoDeTarifasTests</c>.
    /// </remarks>
    private static async Task<ArticuloDto> ArticuloAsync(HttpClient cliente, int semilla)
    {
        string sufijo = semilla.ToString(CultureInfo.InvariantCulture);

        using HttpResponseMessage unidad = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto { Codigo = "U" + sufijo, Nombre = "Unidad " + sufijo, Decimales = 0 });

        unidad.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(unidad));

        using HttpResponseMessage impuesto = await cliente.PostAsJsonAsync(
            Impuestos,
            new CrearImpuestoDto
            {
                Codigo = "CRU" + sufijo,
                Nombre = "Tramo CRU" + sufijo,
                Tipo = "Iva",
                Porcentaje = 21m,
                VigenteDesde = s_desde,
                VigenteHasta = null,
            });

        impuesto.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(impuesto));

        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Articulos,
            new CrearArticuloDto
            {
                Codigo = "ART-" + sufijo,
                Descripcion = "Artículo " + sufijo,
                Tipo = "Bien",
                UnidadBaseId = (await unidad.Content.ReadFromJsonAsync<UnidadMedidaDto>())!.Id,
                ImpuestoPorDefectoId = (await impuesto.Content.ReadFromJsonAsync<ImpuestoDto>())!.Id,
                CategoriaId = null,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ArticuloDto>())!;
    }

    private static async Task<TerceroDto> TerceroAsync(
        HttpClient cliente,
        int numero,
        bool esCliente,
        bool esProveedor)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Terceros,
            new CrearTerceroDto
            {
                Identificacion = new IdentificacionDeAltaDto
                {
                    Pais = "ES",
                    Numero = Escenario.NifInventado(numero),
                },
                RazonSocial = "Tercero de prueba",
                DomicilioFiscal = Escenario.Domicilio(),
                EsCliente = esCliente,
                EsProveedor = esProveedor,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<TerceroDto>())!;
    }

    private static async Task BloquearAsync(HttpClient cliente, Guid terceroId)
    {
        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Terceros}/{terceroId}");

        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));
    }

    private static async Task DesbloquearAsync(HttpClient cliente, Guid terceroId)
    {
        // Sin If-Match: un bloqueado no se puede leer, así que no hay versión que citar.
        using HttpResponseMessage desbloqueo = await cliente.EnviarConVersionAsync(
            HttpMethod.Post, $"{Terceros}/{terceroId}/desbloqueo", etiqueta: null);

        desbloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(desbloqueo));
    }

    private static Task<HttpResponseMessage> AnadirAsync(
        HttpClient cliente,
        Guid articuloId,
        Guid terceroId,
        string? referencia) =>
        cliente.PostAsJsonAsync(
            $"{Articulos}/{articuloId}/proveedores",
            new AgregarProveedorDto { TerceroId = terceroId, ReferenciaDelProveedor = referencia });

    private static async Task<Guid> AnadidoAsync(HttpClient cliente, Guid articuloId, Guid terceroId)
    {
        using HttpResponseMessage alta = await AnadirAsync(cliente, articuloId, terceroId, null);

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ArticuloProveedorDto>())!.Id;
    }

    private static async Task<IReadOnlyList<ArticuloProveedorDto>> ListadoAsync(
        HttpClient cliente,
        Guid articuloId)
    {
        using HttpResponseMessage listado = await cliente.GetAsync($"{Articulos}/{articuloId}/proveedores");

        listado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(listado));

        return (await listado.Content.ReadFromJsonAsync<List<ArticuloProveedorDto>>())!;
    }

    private static async Task<Guid[]> IdsDelListadoAsync(HttpClient cliente, Guid articuloId) =>
        Ordenados([.. (await ListadoAsync(cliente, articuloId)).Select(uno => uno.Id)]);

    private static Guid[] Ordenados(params Guid[] ids) => [.. ids.Order()];

    private static async Task<TarifaDto> TarifaAsync(
        HttpClient cliente,
        string codigo,
        DateOnly desde,
        DateOnly? hasta)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Tarifas,
            new CrearTarifaDto
            {
                Codigo = codigo,
                Nombre = "Tarifa " + codigo,
                DivisaId = await EuroAsync(cliente),
                VigenteDesde = desde,
                VigenteHasta = hasta,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<TarifaDto>())!;
    }

    /// <summary>El euro, dándolo de alta si todavía no está.</summary>
    private static async Task<Guid> EuroAsync(HttpClient cliente)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Divisas, new CrearDivisaDto { Codigo = "EUR", Nombre = "Euro" });

        if (alta.StatusCode == HttpStatusCode.Created)
        {
            return (await alta.Content.ReadFromJsonAsync<DivisaDto>())!.Id;
        }

        alta.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "si no se ha creado, lo único esperable es que ya estuviera. " + await Escenario.Detalle(alta));

        for (int pagina = 1; ; pagina++)
        {
            using HttpResponseMessage listado = await cliente.GetAsync($"{Divisas}?page={pagina}&size=50");

            listado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(listado));

            PaginaDe<DivisaDto> trozo = (await listado.Content.ReadFromJsonAsync<PaginaDe<DivisaDto>>())!;
            DivisaDto? euro = trozo.Elementos.FirstOrDefault(divisa => divisa.Codigo == "EUR");

            if (euro is not null)
            {
                return euro.Id;
            }

            if (trozo.Elementos.Count == 0 || pagina * 50 >= trozo.Total)
            {
                throw new InvalidOperationException("El euro ni se ha creado ni está en el listado.");
            }
        }
    }

    /// <summary>Asigna la tarifa citando la versión de la FICHA, que es la que se escribe.</summary>
    private static async Task<HttpResponseMessage> AsignarTarifaAsync(
        HttpClient cliente,
        Guid terceroId,
        Guid? tarifaId)
    {
        string etiqueta = await cliente.EtiquetaDeAsync($"{Terceros}/{terceroId}");

        return await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Terceros}/{terceroId}/tarifa-asignada",
            etiqueta,
            JsonContent.Create(new AsignarTarifaDto { TarifaId = tarifaId }));
    }

    private static async Task<TarifaAsignadaDto> TarifaAsignadaAsync(HttpClient cliente, Guid terceroId)
    {
        using HttpResponseMessage lectura = await cliente.GetAsync($"{Terceros}/{terceroId}/tarifa-asignada");

        lectura.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(lectura));

        return (await lectura.Content.ReadFromJsonAsync<TarifaAsignadaDto>())!;
    }

    private static async Task<string?> TypeDe(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync())
            .RootElement.GetProperty("type").GetString();
    }

    // El cuerpo del ProblemDetails con el identificador de traza sustituido, que es lo único que
    // puede diferir entre dos peticiones sin significar nada, y con las claves ordenadas. El resto
    // entra tal cual, incluido cualquier campo que alguien añada mañana (ElConflictoQueNoRevelaTests).
    private static async Task<string> Comparable(HttpResponseMessage respuesta)
    {
        JsonObject problema = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!.AsObject();

        if (problema.ContainsKey("traceId"))
        {
            problema["traceId"] = "«el de cada petición»";
        }

        SortedDictionary<string, string> ordenado = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonNode?> campo in problema)
        {
            ordenado[campo.Key] = campo.Value?.ToJsonString() ?? "null";
        }

        return JsonSerializer.Serialize(ordenado);
    }
}
