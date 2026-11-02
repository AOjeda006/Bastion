using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Infrastructure.Persistencia;
using Bastion.Terceros.Infrastructure.Persistencia.Repositorios;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Cruces;

/// <summary>
/// El precio de compra contra PostgreSQL: qué tarifa de compra lleva un proveedor, según
/// <c>IConsultaDeTerceros</c>, y qué precio propone esa tarifa, según <c>IConsultaDePrecios</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué los puertos y no la API.</b> Los dos se estrenan en el 3.3 sin consumidor: el pedido
/// de compra que los usa es del 3.7, así que hoy no hay petición cuya respuesta dependa de ellos.
/// Lo que decide cada motivo es una consulta con el filtro de la empresa, y eso solo se ve
/// preguntándole a la consulta, como en <c>ElPuertoDeTercerosContraLaBaseTests</c>.
/// </para>
/// <para>
/// <b>Lo de arriba se da de alta por la API y se pregunta por los adaptadores.</b> El precio se
/// compone a mano con los repositorios de verdad de Catálogo y la consulta de empresas de
/// Organización, cada uno con su contexto abierto como el de una petición de esa empresa: el
/// filtro de la empresa puesto y el acceso a lo bloqueado cerrado.
/// </para>
/// <para>
/// <b>Semillas del bloque 960-969</b>, de las que este fichero usa del 960 al 967; los terceros van
/// del 32 000 026 al 32 000 031, después de los de <c>ContratoDeLosCrucesTests</c>. La unidad y el
/// tramo de impuesto llevan el prefijo <c>PDC</c>, que no usa nadie más, porque son maestros de
/// instalación. La divisa de las tarifas es el euro, por lo mismo que allí.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElPrecioDeCompraContraLaBaseTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Articulos = "/api/v1/catalogo/articulos";
    private const string Tarifas = "/api/v1/catalogo/tarifas";
    private const string Terceros = "/api/v1/terceros/terceros";
    private const string Divisas = "/api/v1/organizacion/divisas";
    private const string Empresas = "/api/v1/organizacion/empresas";
    private const string Unidades = "/api/v1/organizacion/unidades-de-medida";
    private const string Impuestos = "/api/v1/organizacion/impuestos";

    private const string DeVenta = "tarifa-de-venta";
    private const string DeCompra = "tarifa-de-compra";

    private static readonly DateOnly s_desde = new(2000, 1, 1);
    private static readonly DateOnly s_dia = new(2026, 11, 2);

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
    /// Un tercero que es cliente y proveedor recibe su precio de compra, y no el de venta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es la frase del criterio del 3.3, dicha con los dos puertos.</b> La ficha lleva las dos
    /// tarifas, y la de venta cobra más que la de compra paga: si el precio de compra saliera de la
    /// de venta, el pedido al proveedor llevaría el diez que se le cobra como cliente.
    /// </para>
    /// <para>
    /// <b>La contraria va en el mismo caso</b>: la tarifa de venta, preguntada como si fuera de
    /// compra, no propone. Sin ella, un puerto que propusiera cualquier tarifa pasaría con el seis.
    /// </para>
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDePrecios), PropuestaDePrecio.Propone)]
    [CubreEstadoDelPuerto(typeof(IConsultaDePrecios), PropuestaDePrecio.EsDeVenta)]
    public async Task Un_cliente_que_tambien_es_proveedor_recibe_su_precio_de_compra_y_no_el_de_venta()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(960);
        Guid articulo = await ArticuloAsync(cliente, "960");

        TarifaDto venta = await TarifaAsync(cliente, "PVP", s_desde, null, "Venta");
        await LineaAsync(cliente, venta.Id, articulo, 10m);

        TarifaDto compra = await TarifaAsync(cliente, "PRV", s_desde, null, "Compra");
        await LineaAsync(cliente, compra.Id, articulo, 6m);

        TerceroDto tercero = await TerceroAsync(cliente, 32_000_026, esCliente: true, esProveedor: true);
        await AsignarAsync(cliente, tercero.Id, venta.Id, DeVenta);
        await AsignarAsync(cliente, tercero.Id, compra.Id, DeCompra);

        Guid? suTarifa = await TarifaDeCompraAsync(empresa.Id, tercero.Id);

        suTarifa.ShouldBe(compra.Id, "el puerto de Terceros no contesta la tarifa del hueco de compra");

        PrecioPropuesto propuesto = await PrecioAsync(empresa.Id, suTarifa!.Value, articulo, s_dia);

        propuesto.Propuesta.ShouldBe(PropuestaDePrecio.Propone);
        propuesto.Precio.ShouldNotBeNull();
        propuesto.Precio.Precio.ShouldBe(
            6m,
            "al proveedor se le ha propuesto otro precio que el de su tarifa de compra");
        propuesto.Precio.TarifaId.ShouldBe(compra.Id);
        propuesto.Precio.Divisa.ShouldBe("EUR");

        PrecioPropuesto deLaDeVenta = await PrecioAsync(empresa.Id, venta.Id, articulo, s_dia);

        deLaDeVenta.Propuesta.ShouldBe(
            PropuestaDePrecio.EsDeVenta,
            "la tarifa de venta ha propuesto un precio de compra");
        deLaDeVenta.Precio.ShouldBeNull();
    }

    /// <summary>
    /// El precio sale del tramo del código que rige ese día, y antes del primero no rige ninguno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El tercero guarda el tramo que se le asignó, y aquí es el viejo.</b> Preguntado por un
    /// día del nuevo, el precio y el tramo que salen son los del nuevo; preguntado por el último
    /// día del viejo, los del viejo, que es la frontera cerrada de <c>Tarifa.RigeEl</c>.
    /// </para>
    /// <para>
    /// <b>Y el día de antes del primer tramo no propone</b>, aunque el asignado exista y tenga
    /// línea. Lo que ahí falta es la vigencia, y no la tarifa: por eso el motivo es otro que el de
    /// una tarifa inventada.
    /// </para>
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDePrecios), PropuestaDePrecio.NoRigeEseDia)]
    public async Task El_precio_sale_del_tramo_que_rige_ese_dia_y_antes_del_primero_no_rige_ninguno()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(961);
        Guid articulo = await ArticuloAsync(cliente, "961");
        DateOnly finDelViejo = new(2001, 12, 31);

        TarifaDto viejo = await TarifaAsync(cliente, "PRV", s_desde, finDelViejo, "Compra");
        await LineaAsync(cliente, viejo.Id, articulo, 5m);

        TarifaDto nuevo = await TarifaAsync(cliente, "PRV", finDelViejo.AddDays(1), null, "Compra");
        await LineaAsync(cliente, nuevo.Id, articulo, 4m);

        PrecioPropuesto hoy = await PrecioAsync(empresa.Id, viejo.Id, articulo, s_dia);

        hoy.Propuesta.ShouldBe(PropuestaDePrecio.Propone);
        hoy.Precio!.TarifaId.ShouldBe(nuevo.Id, "el precio ha salido de un tramo que ya no rige");
        hoy.Precio.Precio.ShouldBe(4m);

        PrecioPropuesto suUltimoDia = await PrecioAsync(empresa.Id, viejo.Id, articulo, finDelViejo);

        suUltimoDia.Precio!.TarifaId.ShouldBe(viejo.Id, "el último día del tramo viejo todavía rige");
        suUltimoDia.Precio.Precio.ShouldBe(5m);

        PrecioPropuesto antes = await PrecioAsync(empresa.Id, viejo.Id, articulo, s_desde.AddDays(-1));

        antes.Propuesta.ShouldBe(PropuestaDePrecio.NoRigeEseDia);
        antes.Precio.ShouldBeNull();
    }

    /// <summary>
    /// En una empresa en dólares, una tarifa de compra en euros no propone, aunque su precio se
    /// resuelva.
    /// </summary>
    /// <remarks>
    /// <b>La contraria es el precio por HTTP</b>, sobre la misma tarifa, el mismo artículo y el
    /// mismo día: sale un 200 con el seis en euros. Así el motivo es la divisa y no una línea que
    /// faltara. La comparación es entre dos códigos ISO 4217, el de la tarifa y el de la empresa,
    /// que desde el 3.3 se guardan igual (ADR-0076).
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDePrecios), PropuestaDePrecio.NoEstaEnLaDivisaBase)]
    public async Task En_una_empresa_en_dolares_una_tarifa_en_euros_no_propone_aunque_su_precio_se_resuelva()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(
            Escenario.NuevaEmpresa(Escenario.NifInventado(962)) with { DivisaBase = "USD" });

        Guid articulo = await ArticuloAsync(cliente, "962");

        TarifaDto compra = await TarifaAsync(cliente, "PRV", s_desde, null, "Compra");
        await LineaAsync(cliente, compra.Id, articulo, 6m);

        PrecioPropuesto propuesto = await PrecioAsync(empresa.Id, compra.Id, articulo, s_dia);

        propuesto.Propuesta.ShouldBe(
            PropuestaDePrecio.NoEstaEnLaDivisaBase,
            "un precio en euros se ha propuesto para un pedido en dólares, sin tipo de cambio");
        propuesto.Precio.ShouldBeNull();

        string dia = s_dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using HttpResponseMessage porHttp = await cliente.GetAsync(
            $"{Tarifas}/{compra.Codigo}/precio?articulo={articulo}&cantidad=1&fecha={dia}");

        porHttp.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(porHttp));

        PrecioResueltoDto resuelto = (await porHttp.Content.ReadFromJsonAsync<PrecioResueltoDto>())!;

        resuelto.Precio.ShouldBe(6m);
        resuelto.Divisa.ShouldBe("EUR");
    }

    /// <summary>
    /// Una tarifa inventada y una de otra empresa no existen, y la de la otra propone en la suya.
    /// </summary>
    /// <remarks>
    /// La contraria es la de siempre: sin la última pregunta, un puerto que contestara
    /// <c>NoExisteLaTarifa</c> a todo pasaría las dos primeras. Y es la R8 dicha como un motivo: un
    /// proveedor de una empresa no recibe el precio de la tarifa de otra.
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDePrecios), PropuestaDePrecio.NoExisteLaTarifa)]
    public async Task Una_tarifa_inventada_y_una_de_otra_empresa_no_existen_y_la_ajena_propone_en_la_suya()
    {
        (HttpClient _, EmpresaDto propia) = await EnUnaEmpresaNuevaAsync(963);
        (HttpClient enLaAjena, EmpresaDto ajena) = await EnUnaEmpresaNuevaAsync(964);

        Guid articulo = await ArticuloAsync(enLaAjena, "964");
        TarifaDto deLaOtra = await TarifaAsync(enLaAjena, "PRV", s_desde, null, "Compra");
        await LineaAsync(enLaAjena, deLaOtra.Id, articulo, 6m);

        (await PrecioAsync(propia.Id, Guid.CreateVersion7(), articulo, s_dia)).Propuesta
            .ShouldBe(PropuestaDePrecio.NoExisteLaTarifa);

        (await PrecioAsync(propia.Id, deLaOtra.Id, articulo, s_dia)).Propuesta.ShouldBe(
            PropuestaDePrecio.NoExisteLaTarifa,
            "la tarifa de otra empresa de la instalación se ve desde esta, y su precio se le " +
            "propondría a un proveedor de aquí");

        (await PrecioAsync(ajena.Id, deLaOtra.Id, articulo, s_dia)).Propuesta.ShouldBe(
            PropuestaDePrecio.Propone,
            "en su propia empresa sí propone");
    }

    /// <summary>
    /// El artículo decide entre proponer, no tener línea y no existir.
    /// </summary>
    /// <remarks>
    /// Tres preguntas a la misma tarifa, que rige y está en la divisa base: lo único que cambia es
    /// el artículo. La que propone es la contraria de las otras dos, porque sin ella un puerto que
    /// no propusiera nunca pasaría el caso.
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDePrecios), PropuestaDePrecio.SinLineaParaElArticulo)]
    [CubreEstadoDelPuerto(typeof(IConsultaDePrecios), PropuestaDePrecio.NoExisteElArticulo)]
    public async Task El_articulo_decide_entre_proponer_no_tener_linea_y_no_existir()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(965);
        Guid conLinea = await ArticuloAsync(cliente, "965A");
        Guid sinLinea = await ArticuloAsync(cliente, "965B");

        TarifaDto compra = await TarifaAsync(cliente, "PRV", s_desde, null, "Compra");
        await LineaAsync(cliente, compra.Id, conLinea, 6m);

        (await PrecioAsync(empresa.Id, compra.Id, conLinea, s_dia)).Propuesta
            .ShouldBe(PropuestaDePrecio.Propone);

        PrecioPropuesto delQueNoTiene = await PrecioAsync(empresa.Id, compra.Id, sinLinea, s_dia);

        delQueNoTiene.Propuesta.ShouldBe(PropuestaDePrecio.SinLineaParaElArticulo);
        delQueNoTiene.Precio.ShouldBeNull("sin línea, el precio no es cero: es que no hay");

        (await PrecioAsync(empresa.Id, compra.Id, Guid.CreateVersion7(), s_dia)).Propuesta
            .ShouldBe(PropuestaDePrecio.NoExisteElArticulo);
    }

    /// <summary>El host de verdad registra el puerto del precio de compra.</summary>
    /// <remarks>
    /// El puerto no tiene consumidor hasta el pedido de compra del 3.7, y los casos de este fichero
    /// lo componen a mano. Sin esta pregunta al contenedor, quitar su registro no lo vería nadie:
    /// el primero en notarlo sería el pedido, al arrancar.
    /// </remarks>
    [Fact]
    public void El_host_de_verdad_registra_el_puerto_del_precio_de_compra()
    {
        using IServiceScope ambito = _api.Services.CreateScope();

        ambito.ServiceProvider.GetRequiredService<IConsultaDePrecios>().ShouldBeOfType<ConsultaDePrecios>();
    }

    /// <summary>
    /// La tarifa de compra la contesta el proveedor que la lleva, y nadie más.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Seis fichas, una por cada manera de contestar nulo y la que no</b>: el proveedor que la
    /// lleva, uno que no lleva ninguna, uno que dejó de ser proveedor, uno bloqueado, uno de otra
    /// empresa y uno inventado.
    /// </para>
    /// <para>
    /// <b>Dos contrarias, porque dos de esos nulos tienen otra explicación.</b> El que dejó de ser
    /// proveedor conserva la tarifa en su hueco —lo dice la lectura de la ficha—, así que su nulo
    /// es el del papel y no el de una tarifa que se hubiera borrado. Y el bloqueado, desbloqueado,
    /// vuelve a contestarla: lo que la escondía era el bloqueo.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_tarifa_de_compra_la_contesta_el_proveedor_que_la_lleva_y_nadie_mas()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(966);
        (HttpClient enLaAjena, EmpresaDto ajena) = await EnUnaEmpresaNuevaAsync(967);

        TarifaDto compra = await TarifaAsync(cliente, "PRV", s_desde, null, "Compra");

        TerceroDto conTarifa = await TerceroAsync(cliente, 32_000_027, esCliente: false, esProveedor: true);
        await AsignarAsync(cliente, conTarifa.Id, compra.Id, DeCompra);

        TerceroDto sinTarifa = await TerceroAsync(cliente, 32_000_028, esCliente: false, esProveedor: true);

        TerceroDto exProveedor = await TerceroAsync(cliente, 32_000_029, esCliente: true, esProveedor: true);
        await AsignarAsync(cliente, exProveedor.Id, compra.Id, DeCompra);
        await DejarDeSerProveedorAsync(cliente, exProveedor.Id);

        TerceroDto bloqueado = await TerceroAsync(cliente, 32_000_030, esCliente: false, esProveedor: true);
        await AsignarAsync(cliente, bloqueado.Id, compra.Id, DeCompra);
        await BloquearAsync(cliente, bloqueado.Id);

        TarifaDto suya = await TarifaAsync(enLaAjena, "PRV", s_desde, null, "Compra");
        TerceroDto deLaOtra = await TerceroAsync(enLaAjena, 32_000_031, esCliente: false, esProveedor: true);
        await AsignarAsync(enLaAjena, deLaOtra.Id, suya.Id, DeCompra);

        (await TarifaDeCompraAsync(empresa.Id, conTarifa.Id)).ShouldBe(compra.Id);
        (await TarifaDeCompraAsync(empresa.Id, sinTarifa.Id)).ShouldBeNull();
        (await TarifaDeCompraAsync(empresa.Id, Guid.CreateVersion7())).ShouldBeNull();

        (await TarifaDeCompraAsync(empresa.Id, exProveedor.Id)).ShouldBeNull(
            "quien dejó de ser proveedor ha propuesto su tarifa de compra");

        using (HttpResponseMessage suHueco = await cliente.GetAsync($"{Terceros}/{exProveedor.Id}/{DeCompra}"))
        {
            suHueco.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(suHueco));
            (await suHueco.Content.ReadFromJsonAsync<TarifaAsignadaDto>())!.TarifaId.ShouldBe(
                compra.Id,
                "la ficha ha perdido la tarifa al dejar de ser proveedor, así que el nulo de " +
                "arriba no dice si el puerto mira el papel");
        }

        (await TarifaDeCompraAsync(empresa.Id, bloqueado.Id)).ShouldBeNull(
            "desde fuera de Terceros se ve la tarifa de una ficha bloqueada");

        (await TarifaDeCompraAsync(empresa.Id, deLaOtra.Id)).ShouldBeNull(
            "se ve la tarifa de un proveedor de otra empresa de la instalación");

        (await TarifaDeCompraAsync(ajena.Id, deLaOtra.Id)).ShouldBe(suya.Id, "en su empresa sí");

        await DesbloquearAsync(cliente, bloqueado.Id);

        (await TarifaDeCompraAsync(empresa.Id, bloqueado.Id)).ShouldBe(
            compra.Id,
            "desbloqueado, la tarifa sigue sin verse: lo que la escondía no era el bloqueo");
    }

    private async Task<PrecioPropuesto> PrecioAsync(
        Guid empresaId,
        Guid tarifaId,
        Guid articuloId,
        DateOnly dia)
    {
        await using CatalogoDbContext catalogo = postgres.AbrirCatalogo(empresaId);
        await using OrganizacionDbContext organizacion = postgres.AbrirOrganizacion(empresaId);

        ConsultaDePrecios puerto = new(
            new RepositorioDeTarifas(catalogo),
            new RepositorioDeLineasDeTarifa(catalogo),
            new RepositorioDeArticulos(catalogo),
            new RepositorioDeCategorias(catalogo),
            new ConsultaDeEmpresas(organizacion));

        return await puerto.DeCompraAsync(tarifaId, articuloId, 1m, dia, CancellationToken.None);
    }

    private async Task<Guid?> TarifaDeCompraAsync(Guid empresaId, Guid proveedorId)
    {
        await using TercerosDbContext contexto = postgres.AbrirTerceros(empresaId);

        return await new ConsultaDeTerceros(contexto)
            .TarifaDeCompraDeAsync(proveedorId, CancellationToken.None);
    }

    private Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla) =>
        EnUnaEmpresaNuevaAsync(Escenario.NuevaEmpresa(Escenario.NifInventado(semilla)));

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(CrearEmpresaDto alta)
    {
        (HttpClient cliente, SesionDto sesion) = await _api.AbrirComoAdministradorAsync();
        _clientes.Add(cliente);

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Empresas, alta);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        EmpresaDto empresa = (await respuesta.Content.ReadFromJsonAsync<EmpresaDto>())!;

        await Escenario.EntrarEnAsync(cliente, sesion.UsuarioId, empresa.Id);

        return (cliente, empresa);
    }

    /// <summary>Un artículo con su unidad y su tramo de impuesto, propios de este caso.</summary>
    private static async Task<Guid> ArticuloAsync(HttpClient cliente, string sufijo)
    {
        using HttpResponseMessage unidad = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto { Codigo = "PDC" + sufijo, Nombre = "Unidad " + sufijo, Decimales = 0 });

        unidad.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(unidad));

        using HttpResponseMessage impuesto = await cliente.PostAsJsonAsync(
            Impuestos,
            new CrearImpuestoDto
            {
                Codigo = "PDC" + sufijo,
                Nombre = "Tramo PDC" + sufijo,
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
                Codigo = "PDC-" + sufijo,
                Descripcion = "Artículo " + sufijo,
                Tipo = "Bien",
                UnidadBaseId = (await unidad.Content.ReadFromJsonAsync<UnidadMedidaDto>())!.Id,
                ImpuestoPorDefectoId = (await impuesto.Content.ReadFromJsonAsync<ImpuestoDto>())!.Id,
                CategoriaId = null,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ArticuloDto>())!.Id;
    }

    private static async Task<TarifaDto> TarifaAsync(
        HttpClient cliente,
        string codigo,
        DateOnly desde,
        DateOnly? hasta,
        string tipo)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Tarifas,
            new CrearTarifaDto
            {
                Codigo = codigo,
                Nombre = "Tarifa " + codigo,
                Tipo = tipo,
                Divisa = await EuroAsync(cliente),
                VigenteDesde = desde,
                VigenteHasta = hasta,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<TarifaDto>())!;
    }

    /// <summary>El código del euro, dándolo de alta si todavía no está.</summary>
    private static async Task<string> EuroAsync(HttpClient cliente)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Divisas, new CrearDivisaDto { Codigo = "EUR", Nombre = "Euro" });

        if (alta.StatusCode != HttpStatusCode.Created)
        {
            alta.StatusCode.ShouldBe(
                HttpStatusCode.Conflict,
                "si no se ha creado, lo único esperable es que ya estuviera. " + await Escenario.Detalle(alta));
        }

        return "EUR";
    }

    private static async Task LineaAsync(HttpClient cliente, Guid tarifaId, Guid articuloId, decimal precio)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            $"{Tarifas}/{tarifaId}/lineas",
            new CrearLineaTarifaDto { ArticuloId = articuloId, CantidadDesde = 0m, Precio = precio });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
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

    /// <summary>
    /// Asigna la tarifa de un hueco citando la versión de la ficha, que es la que se escribe.
    /// </summary>
    private static async Task AsignarAsync(HttpClient cliente, Guid terceroId, Guid tarifaId, string hueco)
    {
        string etiqueta = await cliente.EtiquetaDeAsync($"{Terceros}/{terceroId}");

        using HttpResponseMessage asignada = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Terceros}/{terceroId}/{hueco}",
            etiqueta,
            JsonContent.Create(new AsignarTarifaDto { TarifaId = tarifaId }));

        asignada.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(asignada));
    }

    private static async Task DejarDeSerProveedorAsync(HttpClient cliente, Guid terceroId)
    {
        using HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Terceros}/{terceroId}",
            new ModificarTerceroDto
            {
                RazonSocial = "Tercero de prueba",
                DomicilioFiscal = Escenario.Domicilio(),
                EsCliente = true,
                EsProveedor = false,
            });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
    }

    private static async Task BloquearAsync(HttpClient cliente, Guid terceroId)
    {
        using HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{Terceros}/{terceroId}");

        bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));
    }

    private static async Task DesbloquearAsync(HttpClient cliente, Guid terceroId)
    {
        // Sin If-Match: un bloqueado no se puede leer, así que no hay versión que citar (ADR-0027).
        using HttpResponseMessage desbloqueo = await cliente.EnviarConVersionAsync(
            HttpMethod.Post, $"{Terceros}/{terceroId}/desbloqueo", etiqueta: null);

        desbloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(desbloqueo));
    }
}
