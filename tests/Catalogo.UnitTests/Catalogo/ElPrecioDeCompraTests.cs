using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// El precio de compra que Catálogo propone a otro módulo: cada motivo para no proponer, el orden
/// en que se miran, y el tramo del que sale el precio.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí se mira la traducción, y contra la base, el estado.</b> Cada motivo sale de una
/// comparación —el tipo, la vigencia, la divisa, la línea—, y esas comparaciones se ponen rojas
/// en segundos con los dobles. Que cada valor del enumerado lo produzca la base lo afirman las
/// marcas de <c>ElPrecioDeCompraContraLaBaseTests</c>, en el carril de integración.
/// </para>
/// <para>
/// <b>El orden es un caso aparte, con varios motivos a la vez.</b> Con uno solo por caso, un puerto
/// que mirase el artículo antes que el tipo pasaría todos: el orden solo se ve cuando dos motivos
/// se dan juntos y gana el que va primero.
/// </para>
/// </remarks>
public sealed class ElPrecioDeCompraTests
{
    private static readonly Guid s_empresa = Guid.NewGuid();
    private static readonly DateTimeOffset s_ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly s_hoy = new(2026, 11, 2);
    private static readonly DateOnly s_desde = new(2026, 1, 1);

    /// <summary>Una tarifa de compra en la divisa base propone el precio de su línea.</summary>
    [Fact]
    public async Task Una_tarifa_de_compra_en_la_divisa_base_propone_el_precio_de_su_linea()
    {
        Escena escena = new(TipoDeTarifa.Compra, "EUR");
        escena.ConLinea(escena.Tramo, 6m);

        PrecioPropuesto propuesto = await escena.PreguntarAsync(escena.Tramo.Id, 3m, s_hoy);

        propuesto.Propuesta.ShouldBe(PropuestaDePrecio.Propone);
        propuesto.Precio.ShouldNotBeNull();
        propuesto.Precio.Precio.ShouldBe(6m);
        propuesto.Precio.DescuentoPorcentaje.ShouldBeNull();
        propuesto.Precio.Divisa.ShouldBe("EUR");
        propuesto.Precio.TarifaId.ShouldBe(escena.Tramo.Id);
        propuesto.Precio.ArticuloId.ShouldBe(escena.Articulo.Id);
        propuesto.Precio.Cantidad.ShouldBe(3m);
        propuesto.Precio.Fecha.ShouldBe(s_hoy);
        propuesto.Precio.Origen.ShouldBe(OrigenDelPrecio.Articulo);

        escena.Empresas.Preguntadas.ShouldBe(
            [s_empresa],
            "la divisa base se ha preguntado de otra empresa que la del tramo, o no se ha preguntado");
    }

    /// <summary>
    /// Una línea de la categoría del artículo también propone, y dice que viene de la categoría.
    /// </summary>
    /// <remarks>
    /// El puerto resuelve con la misma precedencia que el precio por HTTP, y la comparte con él: si
    /// le pasara al helper el artículo sin su ascendencia, la línea de la categoría no llegaría.
    /// </remarks>
    [Fact]
    public async Task Una_linea_de_la_categoria_del_articulo_tambien_propone()
    {
        var categoria = Guid.NewGuid();
        Escena escena = new(TipoDeTarifa.Compra, "EUR", categoriaDelArticulo: categoria);
        escena.Categorias.Con(categoria, null, "RAIZ");
        escena.Lineas.Agregar(LineaTarifa.ParaCategoria(
            s_empresa, escena.Tramo.Id, categoria, 0m, PrecioODescuento.DeDescuento(15m), s_ahora));

        PrecioPropuesto propuesto = await escena.PreguntarAsync(escena.Tramo.Id, 1m, s_hoy);

        propuesto.Propuesta.ShouldBe(PropuestaDePrecio.Propone);
        propuesto.Precio!.DescuentoPorcentaje.ShouldBe(15m);
        propuesto.Precio.Origen.ShouldBe(OrigenDelPrecio.Categoria);
        propuesto.Precio.OrigenId.ShouldBe(categoria);
    }

    /// <summary>Una tarifa de venta contesta <c>EsDeVenta</c>, aunque tenga línea y rija.</summary>
    [Fact]
    public async Task Una_tarifa_de_venta_no_propone_aunque_tenga_linea_y_rija()
    {
        Escena escena = new(TipoDeTarifa.Venta, "EUR");
        escena.ConLinea(escena.Tramo, 10m);

        PrecioPropuesto propuesto = await escena.PreguntarAsync(escena.Tramo.Id, 1m, s_hoy);

        propuesto.Propuesta.ShouldBe(
            PropuestaDePrecio.EsDeVenta,
            "el precio de venta se ha propuesto para comprar: un cliente que también es proveedor " +
            "recibiría en su pedido lo que se le cobra");
        propuesto.Precio.ShouldBeNull();
    }

    /// <summary>Un tramo que no existe en esta empresa no propone.</summary>
    [Fact]
    public async Task Una_tarifa_que_no_existe_no_propone()
    {
        Escena escena = new(TipoDeTarifa.Compra, "EUR");
        escena.ConLinea(escena.Tramo, 6m);

        PrecioPropuesto propuesto = await escena.PreguntarAsync(Guid.NewGuid(), 1m, s_hoy);

        propuesto.Propuesta.ShouldBe(PropuestaDePrecio.NoExisteLaTarifa);
        propuesto.Precio.ShouldBeNull();
    }

    /// <summary>Si ningún tramo del código rige ese día, no propone.</summary>
    [Fact]
    public async Task Si_ningun_tramo_de_su_codigo_rige_ese_dia_no_propone()
    {
        Escena escena = new(TipoDeTarifa.Compra, "EUR");
        escena.ConLinea(escena.Tramo, 6m);

        PrecioPropuesto propuesto = await escena.PreguntarAsync(
            escena.Tramo.Id, 1m, s_desde.AddDays(-1));

        propuesto.Propuesta.ShouldBe(PropuestaDePrecio.NoRigeEseDia);
        propuesto.Precio.ShouldBeNull();
    }

    /// <summary>
    /// El precio sale del tramo de su código que rige ese día, aunque el tercero lleve otro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El tercero guarda el tramo que se le asignó</b>, y ese tramo caduca. Un puerto que
    /// resolviera con el asignado propondría su precio para siempre, o dejaría de proponer el día
    /// que caducase aunque el código tuviera sucesor.
    /// </para>
    /// <para>
    /// La contraria, en el mismo caso: el día que el asignado rige, el precio es el suyo. Sin ella,
    /// un puerto que buscara siempre el último tramo pasaría.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Resuelve_con_el_tramo_de_su_codigo_que_rige_ese_dia_y_no_con_el_asignado()
    {
        DateOnly finDelPrimero = new(2026, 6, 30);
        Escena escena = new(TipoDeTarifa.Compra, "EUR", hasta: finDelPrimero);
        escena.ConLinea(escena.Tramo, 5m);

        var sucesor = Tarifa.Crear(
            s_empresa, escena.Tramo.Codigo, "El sucesor", TipoDeTarifa.Compra, "EUR",
            finDelPrimero.AddDays(1), null, s_ahora);
        escena.Tarifas.Agregar(sucesor);
        escena.ConLinea(sucesor, 4m);

        PrecioPropuesto despues = await escena.PreguntarAsync(escena.Tramo.Id, 1m, s_hoy);

        despues.Propuesta.ShouldBe(PropuestaDePrecio.Propone);
        despues.Precio!.TarifaId.ShouldBe(
            sucesor.Id,
            "el precio ha salido del tramo asignado, que ya no rige ese día");
        despues.Precio.Precio.ShouldBe(4m);

        PrecioPropuesto antes = await escena.PreguntarAsync(
            escena.Tramo.Id, 1m, finDelPrimero);

        antes.Precio!.TarifaId.ShouldBe(escena.Tramo.Id, "el día que el asignado rige, es el suyo");
        antes.Precio.Precio.ShouldBe(5m);
    }

    /// <summary>
    /// La divisa que se compara es la del tramo que rige, y no la del asignado.
    /// </summary>
    /// <remarks>
    /// Los tramos de un código comparten el tipo, y la base lo impone (ADR-0076), pero no la
    /// divisa. Un asignado en euros con un sucesor en dólares no propone el día del sucesor, aunque
    /// los dos tengan línea, y el día del asignado propone el suyo.
    /// </remarks>
    [Fact]
    public async Task La_divisa_que_se_compara_es_la_del_tramo_que_rige_y_no_la_del_asignado()
    {
        DateOnly finDelPrimero = new(2026, 6, 30);
        Escena escena = new(TipoDeTarifa.Compra, "EUR", hasta: finDelPrimero);
        escena.ConLinea(escena.Tramo, 5m);

        var enDolares = Tarifa.Crear(
            s_empresa, escena.Tramo.Codigo, "En dólares", TipoDeTarifa.Compra, "USD",
            finDelPrimero.AddDays(1), null, s_ahora);
        escena.Tarifas.Agregar(enDolares);
        escena.ConLinea(enDolares, 4m);

        PrecioPropuesto despues = await escena.PreguntarAsync(escena.Tramo.Id, 1m, s_hoy);

        despues.Propuesta.ShouldBe(
            PropuestaDePrecio.NoEstaEnLaDivisaBase,
            "se ha comparado la divisa del asignado, que ese día ya no rige");

        (await escena.PreguntarAsync(escena.Tramo.Id, 1m, finDelPrimero)).Propuesta
            .ShouldBe(PropuestaDePrecio.Propone);
    }

    /// <summary>
    /// Un tramo en otra divisa no propone, y en una empresa sin divisa base tampoco.
    /// </summary>
    /// <remarks>
    /// La divisa se compara por su código ISO 4217, que es como la guardan las dos desde el 3.3
    /// (ADR-0076). La empresa sin divisa base es la bloqueada: el puerto de Organización contesta
    /// nulo, y nulo no es la divisa de ningún tramo.
    /// </remarks>
    [Theory]
    [InlineData("USD", "EUR")]
    [InlineData("EUR", "USD")]
    [InlineData("EUR", null)]
    public async Task Un_tramo_fuera_de_la_divisa_base_no_propone(string divisaDelTramo, string? divisaBase)
    {
        Escena escena = new(TipoDeTarifa.Compra, divisaDelTramo, divisaBase: divisaBase);
        escena.ConLinea(escena.Tramo, 6m);

        PrecioPropuesto propuesto = await escena.PreguntarAsync(escena.Tramo.Id, 1m, s_hoy);

        propuesto.Propuesta.ShouldBe(
            PropuestaDePrecio.NoEstaEnLaDivisaBase,
            $"un precio en {divisaDelTramo} se ha propuesto para una empresa en " +
            $"{divisaBase ?? "ninguna"}, sin tipo de cambio");
        propuesto.Precio.ShouldBeNull();
    }

    /// <summary>Un artículo que no existe no propone.</summary>
    [Fact]
    public async Task Un_articulo_que_no_existe_no_propone()
    {
        Escena escena = new(TipoDeTarifa.Compra, "EUR");
        escena.ConLinea(escena.Tramo, 6m);

        PrecioPropuesto propuesto = await escena.PreguntarAsync(
            escena.Tramo.Id, 1m, s_hoy, articuloId: Guid.NewGuid());

        propuesto.Propuesta.ShouldBe(PropuestaDePrecio.NoExisteElArticulo);
        propuesto.Precio.ShouldBeNull();
    }

    /// <summary>
    /// Una tarifa que rige y no dice nada del artículo no propone, y no es un cero.
    /// </summary>
    [Fact]
    public async Task Sin_linea_para_el_articulo_no_propone_y_no_es_un_cero()
    {
        Escena escena = new(TipoDeTarifa.Compra, "EUR");

        PrecioPropuesto propuesto = await escena.PreguntarAsync(escena.Tramo.Id, 1m, s_hoy);

        propuesto.Propuesta.ShouldBe(PropuestaDePrecio.SinLineaParaElArticulo);
        propuesto.Precio.ShouldBeNull("sin línea, el precio no es cero: es que no hay");
    }

    /// <summary>
    /// Con varios motivos a la vez, gana el que va primero: de la tarifa hacia la línea.
    /// </summary>
    /// <remarks>
    /// Cada fila junta dos motivos, y el que se espera es el primero de los dos. Están todas las
    /// parejas de comprobaciones consecutivas, así que intercambiar dos vecinas pone roja al menos
    /// una fila. La última es la del artículo frente a la línea: sin línea en ninguna fila, un
    /// puerto que buscara las candidatas antes de leer el artículo contestaría que no hay línea.
    /// </remarks>
    [Theory]
    [InlineData(TipoDeTarifa.Venta, false, "EUR", true, PropuestaDePrecio.EsDeVenta)]
    [InlineData(TipoDeTarifa.Venta, true, "USD", true, PropuestaDePrecio.EsDeVenta)]
    [InlineData(TipoDeTarifa.Venta, true, "EUR", false, PropuestaDePrecio.EsDeVenta)]
    [InlineData(TipoDeTarifa.Compra, false, "USD", true, PropuestaDePrecio.NoRigeEseDia)]
    [InlineData(TipoDeTarifa.Compra, false, "EUR", false, PropuestaDePrecio.NoRigeEseDia)]
    [InlineData(TipoDeTarifa.Compra, true, "USD", false, PropuestaDePrecio.NoEstaEnLaDivisaBase)]
    [InlineData(TipoDeTarifa.Compra, true, "EUR", false, PropuestaDePrecio.NoExisteElArticulo)]
    public async Task Con_varios_motivos_gana_el_que_va_primero(
        TipoDeTarifa tipo,
        bool rige,
        string divisaDelTramo,
        bool existeElArticulo,
        PropuestaDePrecio esperada)
    {
        Escena escena = new(tipo, divisaDelTramo);

        // Sin línea en ninguna fila: el último motivo está siempre puesto, y no gana nunca.
        PrecioPropuesto propuesto = await escena.PreguntarAsync(
            escena.Tramo.Id,
            1m,
            rige ? s_hoy : s_desde.AddDays(-1),
            articuloId: existeElArticulo ? escena.Articulo.Id : Guid.NewGuid());

        propuesto.Propuesta.ShouldBe(esperada);
    }

    /// <summary>La cantidad tiene que ser mayor que cero, y si no lo es, lanza.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Una_cantidad_que_no_es_positiva_lanza(int cantidad)
    {
        Escena escena = new(TipoDeTarifa.Compra, "EUR");
        escena.ConLinea(escena.Tramo, 6m);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => escena.PreguntarAsync(escena.Tramo.Id, cantidad, s_hoy));
    }

    /// <summary>Sin precio solo se construye un motivo, y con precio solo <c>Propone</c>.</summary>
    [Fact]
    public void La_propuesta_no_se_construye_con_un_precio_y_un_motivo_a_la_vez()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => PrecioPropuesto.Sin(PropuestaDePrecio.Propone));
        Should.Throw<ArgumentOutOfRangeException>(() => PrecioPropuesto.Sin((PropuestaDePrecio)99));
        Should.Throw<ArgumentNullException>(() => PrecioPropuesto.Con(null!));

        PrecioPropuesto.Sin(PropuestaDePrecio.EsDeVenta).Precio.ShouldBeNull();
    }

    /// <summary>Un tramo, un artículo y la empresa que contesta su divisa base.</summary>
    private sealed class Escena
    {
        internal Escena(
            TipoDeTarifa tipo,
            string divisaDelTramo,
            DateOnly? hasta = null,
            Guid? categoriaDelArticulo = null,
            string? divisaBase = "EUR")
        {
            Tramo = Tarifa.Crear(
                s_empresa, "PROV", "Tarifa del proveedor", tipo, divisaDelTramo, s_desde, hasta,
                s_ahora);
            Tarifas.Agregar(Tramo);

            Articulo = Articulo.Crear(
                s_empresa, "ART-1", "Un artículo", TipoDeArticulo.Bien, Trazabilidad.Ninguna,
                Guid.NewGuid(), Guid.NewGuid(), categoriaDelArticulo, s_ahora);
            Articulos.Agregar(Articulo);

            Empresas = new EmpresaEnDivisa(divisaBase);
        }

        internal Tarifa Tramo { get; }

        internal Articulo Articulo { get; }

        internal TarifasEnMemoria Tarifas { get; } = new();

        internal LineasDeTarifaEnMemoria Lineas { get; } = new();

        internal ArticulosEnMemoria Articulos { get; } = new();

        internal CategoriasEnMemoria Categorias { get; } = new();

        internal EmpresaEnDivisa Empresas { get; }

        internal void ConLinea(Tarifa tramo, decimal precio) =>
            Lineas.Agregar(LineaTarifa.ParaArticulo(
                s_empresa, tramo.Id, Articulo.Id, 0m, PrecioODescuento.DePrecio(precio), s_ahora));

        internal Task<PrecioPropuesto> PreguntarAsync(
            Guid tarifaId,
            decimal cantidad,
            DateOnly fecha,
            Guid? articuloId = null) =>
            new ConsultaDePrecios(Tarifas, Lineas, Articulos, Categorias, Empresas)
                .DeCompraAsync(
                    tarifaId, articuloId ?? Articulo.Id, cantidad, fecha, CancellationToken.None);
    }
}
