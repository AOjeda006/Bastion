using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// La resolución de un precio: los tres fallos con nombre, la divisa pegada al precio, y —el caso
/// que de verdad vigila este fichero— <b>el número de viajes a la base</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La afirmación del número de viajes no es de color, es una cota.</b> Dice que resolver un
/// precio cuesta el mismo número de consultas con un artículo colgado de la raíz que con uno
/// colgado once niveles más abajo. Sin ella, la implementación natural —reusar el ascenso por
/// padres de <c>ElArbolSigueSiendoUnArbol</c>, que gasta una consulta por nivel— pasa todos los
/// demás casos de este fichero: devuelve el precio correcto, y ninguna de sus consultas es lenta
/// por separado. Lo que produce es un N+1 por construcción en el camino más caliente del módulo:
/// un documento de cuarenta líneas × hasta once niveles son cuatrocientas cuarenta consultas para
/// valorar un albarán, y nada en el registro señala a ninguna.
/// </para>
/// <para>
/// Por eso los dobles cuentan. <c>LlamadasDeAscendencia</c> y <c>LlamadasDeCandidatas</c> son la
/// prueba, y el caso se ejerce <b>dos veces con profundidades distintas</b> comparando los dos
/// recuentos entre sí: un número absoluto escrito a mano se podría actualizar al romperlo, y la
/// igualdad entre las dos profundidades no.
/// </para>
/// </remarks>
public sealed class ResolverPrecioTests
{
    private static readonly Guid s_empresa = Guid.NewGuid();
    private static readonly DateTimeOffset s_ahora = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly s_hoy = new(2026, 9, 11);

    /// <summary>EL CASO DE LA COTA: los viajes no crecen con la profundidad del artículo.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public async Task Resolver_cuesta_los_mismos_viajes_sea_cual_sea_la_profundidad(int niveles)
    {
        CategoriasEnMemoria categorias = new();
        IReadOnlyList<Guid> cadena = categorias.ConCadenaDe(niveles);
        Guid laDelArticulo = cadena[^1];

        (Tarifa tarifa, Articulo articulo, TarifasEnMemoria tarifas,
            LineasDeTarifaEnMemoria lineas, ArticulosEnMemoria articulos) =
            Escenario(laDelArticulo);

        // La línea la pone la categoría RAÍZ, que con once niveles está a diez saltos: así el
        // ascenso tiene que recorrer el árbol entero para encontrarla, que es el peor caso.
        lineas.Guardadas.Add(LineaTarifa.ParaCategoria(
            s_empresa, tarifa.Id, cadena[0], 0m, PrecioODescuento.DePrecio(9m), s_ahora));

        Resultado<PrecioResueltoDto> resuelto = await Resolver(
            tarifas, lineas, articulos, categorias).EjecutarAsync(
            "pvp", articulo.Id, 1m, null, CancellationToken.None);

        resuelto.EsCorrecto.ShouldBeTrue();
        resuelto.Valor.Precio.ShouldBe(9m);
        resuelto.Valor.NivelDeLaCategoria.ShouldBe(niveles - 1, "la raíz está a un salto por nivel");

        // LOS TRES CONTADORES, y los tres son constantes. `Llamadas` es el del ascenso por padres
        // de `EslabonAsync`: que esté a CERO es lo que dice que la precedencia no se resolvió
        // subiendo de uno en uno. Es la mutación 8.
        categorias.LlamadasDeAscendencia.ShouldBe(
            1,
            "la ascendencia se pide UNA vez por resolución, con toda la cadena dentro");

        categorias.Llamadas.ShouldBe(
            0,
            "la resolución ha ascendido por padres de uno en uno. Eso es una consulta por nivel: " +
            "con cuarenta líneas y once niveles, cuatrocientas cuarenta consultas para valorar " +
            "un documento, y ninguna lenta por separado");

        lineas.LlamadasDeCandidatas.ShouldBe(
            1,
            "las candidatas se piden UNA vez, con la ascendencia entera en un `= ANY`");
    }

    [Fact]
    public async Task Un_codigo_que_no_existe_y_una_tarifa_que_no_cubre_el_dia_son_DOS_errores()
    {
        // «No hay tarifa» y «hay tarifa pero su vigencia no cubre esta fecha» no se arreglan
        // igual: la primera se corrige escribiendo bien el código, la segunda abriendo el tramo
        // que falta. Dos `type` distintos, como la unidad retirada frente a la inexistente.
        CategoriasEnMemoria categorias = new();
        (Tarifa tarifa, Articulo articulo, TarifasEnMemoria tarifas,
            LineasDeTarifaEnMemoria lineas, ArticulosEnMemoria articulos) = Escenario(null);

        Resultado<PrecioResueltoDto> inventada = await Resolver(
            tarifas, lineas, articulos, categorias).EjecutarAsync(
            "NO-EXISTE", articulo.Id, 1m, null, CancellationToken.None);

        inventada.EsCorrecto.ShouldBeFalse();
        inventada.Error!.Codigo.ShouldBe("tarifa-no-encontrada");

        // El mismo código, un día en el que su único tramo no rige: la tarifa existe.
        Resultado<PrecioResueltoDto> fueraDeVigencia = await Resolver(
            tarifas, lineas, articulos, categorias).EjecutarAsync(
            tarifa.Codigo, articulo.Id, 1m, s_hoy.AddYears(-5), CancellationToken.None);

        fueraDeVigencia.EsCorrecto.ShouldBeFalse();
        fueraDeVigencia.Error!.Codigo.ShouldBe(
            "tarifa-no-vigente",
            "una tarifa que existe y no cubre el día contesta lo mismo que una que no existe. " +
            "Son dos arreglos distintos y el cliente no puede distinguirlos");
    }

    [Fact]
    public async Task Sin_linea_aplicable_hay_error_con_nombre_y_NUNCA_un_cero()
    {
        // La decisión 3 del ADR-0023, con otro sujeto: nunca un cero silencioso. Un precio cero
        // que nadie escribió entra en un documento, suma cero al total, y el descuadre aparece
        // semanas después sin autor.
        CategoriasEnMemoria categorias = new();
        (Tarifa tarifa, Articulo articulo, TarifasEnMemoria tarifas,
            LineasDeTarifaEnMemoria lineas, ArticulosEnMemoria articulos) = Escenario(null);

        Resultado<PrecioResueltoDto> resuelto = await Resolver(
            tarifas, lineas, articulos, categorias).EjecutarAsync(
            tarifa.Codigo, articulo.Id, 1m, null, CancellationToken.None);

        resuelto.EsCorrecto.ShouldBeFalse(
            "la tarifa rige y no dice nada de este artículo, y aun así ha devuelto un precio. Es " +
            "la mutación 3, y lo que devuelve es un cero que nadie escribió");

        resuelto.Error!.Codigo.ShouldBe("tarifa-sin-linea-aplicable");
    }

    [Fact]
    public async Task El_precio_resuelto_lleva_SIEMPRE_la_divisa_de_la_tarifa()
    {
        // Es lo que hace segura la decisión de aceptar una tarifa en una divisa distinta de la de
        // la empresa: quien recibe el precio recibe de qué moneda es, así que no puede sumarlo a
        // un total en otra por descuido.
        CategoriasEnMemoria categorias = new();
        (Tarifa tarifa, Articulo articulo, TarifasEnMemoria tarifas,
            LineasDeTarifaEnMemoria lineas, ArticulosEnMemoria articulos) = Escenario(null);

        lineas.Guardadas.Add(LineaTarifa.ParaArticulo(
            s_empresa, tarifa.Id, articulo.Id, 0m, PrecioODescuento.DePrecio(30m), s_ahora));

        Resultado<PrecioResueltoDto> resuelto = await Resolver(
            tarifas, lineas, articulos, categorias).EjecutarAsync(
            tarifa.Codigo, articulo.Id, 1m, null, CancellationToken.None);

        resuelto.EsCorrecto.ShouldBeTrue();
        resuelto.Valor.DivisaId.ShouldBe(tarifa.DivisaId);
        resuelto.Valor.Origen.ShouldBe(OrigenDelPrecio.Articulo);
        resuelto.Valor.NivelDeLaCategoria.ShouldBeNull("una línea de artículo no está a ningún salto");
    }

    [Fact]
    public async Task Un_articulo_sin_clasificar_no_es_un_caso_especial_es_la_lista_vacia()
    {
        // Sin categoría no hay ascendencia que pedir, así que ni siquiera se pregunta: la lista
        // vacía basta y lo único que puede ganar es una línea suya.
        CategoriasEnMemoria categorias = new();
        (Tarifa tarifa, Articulo articulo, TarifasEnMemoria tarifas,
            LineasDeTarifaEnMemoria lineas, ArticulosEnMemoria articulos) = Escenario(null);

        lineas.Guardadas.Add(LineaTarifa.ParaArticulo(
            s_empresa, tarifa.Id, articulo.Id, 0m, PrecioODescuento.DeDescuento(20m), s_ahora));

        Resultado<PrecioResueltoDto> resuelto = await Resolver(
            tarifas, lineas, articulos, categorias).EjecutarAsync(
            tarifa.Codigo, articulo.Id, 1m, null, CancellationToken.None);

        resuelto.EsCorrecto.ShouldBeTrue();
        resuelto.Valor.DescuentoPorcentaje.ShouldBe(20m);
        resuelto.Valor.Precio.ShouldBeNull();
        categorias.LlamadasDeAscendencia.ShouldBe(0);
    }

    private static (Tarifa Tarifa, Articulo Articulo, TarifasEnMemoria Tarifas,
        LineasDeTarifaEnMemoria Lineas, ArticulosEnMemoria Articulos) Escenario(Guid? categoriaId)
    {
        var tarifa = Tarifa.Crear(
            s_empresa, "PVP", "Precio de venta al público", Guid.NewGuid(),
            s_hoy.AddYears(-1), null, s_ahora);

        var articulo = Articulo.Crear(
            s_empresa, "ART-1", "Un artículo", TipoDeArticulo.Bien,
            Guid.NewGuid(), Guid.NewGuid(), categoriaId, s_ahora);

        TarifasEnMemoria tarifas = new();
        tarifas.Agregar(tarifa);

        ArticulosEnMemoria articulos = new();
        articulos.Agregar(articulo);

        return (tarifa, articulo, tarifas, new LineasDeTarifaEnMemoria(), articulos);
    }

    private static ResolverPrecio Resolver(
        TarifasEnMemoria tarifas,
        LineasDeTarifaEnMemoria lineas,
        ArticulosEnMemoria articulos,
        CategoriasEnMemoria categorias) =>
        new(new UsuarioDe(s_empresa), tarifas, lineas, articulos, categorias,
            new RelojParado(s_ahora));
}
