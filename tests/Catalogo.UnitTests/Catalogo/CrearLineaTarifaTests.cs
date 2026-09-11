using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// Las dos exclusividades de una línea —un destino, un precio o un descuento— y las dos mitades de
/// «ni hueco ni solape» en los tramos de cantidad.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada exclusividad son DOS negativos, no uno.</b> Los dos puestos y ninguno puesto están
/// prohibidos igual, y el segundo es el que se olvida: una línea sin precio ni descuento no falla
/// al guardarla, casa con el artículo, gana la precedencia y devuelve el importe que nadie
/// escribió. Por eso las dos condiciones del caso de uso son igualdades entre los dos «hay», que
/// es una forma que no se puede dejar a medias.
/// </para>
/// <para>
/// <b>Y el hueco es peor que el solape.</b> Un solape devuelve un precio ambiguo; un hueco
/// devuelve «sin tarifa aplicable» para una cantidad que está en medio de una tabla con precios
/// escritos, y el usuario ve un error donde hay precio. Aquí se cierra por construcción: el primer
/// tramo de cada destino tiene que empezar en cero, la cantidad no se puede modificar después, y
/// no hay manera de borrar una línea. El solape lo cierran dos índices únicos parciales de la
/// base, que esta capa no puede ejercer, y por eso aquí solo está la mitad que sí.
/// </para>
/// </remarks>
public sealed class CrearLineaTarifaTests
{
    private static readonly Guid s_empresa = Guid.NewGuid();
    private static readonly DateTimeOffset s_ahora = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly s_hoy = new(2026, 9, 11);

    [Fact]
    public async Task Con_articulo_Y_categoria_se_rechaza()
    {
        Escena escena = new();

        Resultado<LineaTarifaDto> resultado = await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CategoriaId = escena.Categoria,
            CantidadDesde = 0m,
            Precio = 10m,
        });

        Fallo(resultado, "tarifa-linea-articulo-o-categoria");
    }

    [Fact]
    public async Task Sin_articulo_NI_categoria_tambien()
    {
        // El negativo que se olvida, por el lado del destino: una línea sin destino no le pone
        // precio a nada, pero ocupa un tramo y participa de los índices.
        Escena escena = new();

        Resultado<LineaTarifaDto> resultado = await escena.Crear(new CrearLineaTarifaDto
        {
            CantidadDesde = 0m,
            Precio = 10m,
        });

        Fallo(resultado, "tarifa-linea-articulo-o-categoria");
    }

    [Fact]
    public async Task Con_precio_Y_descuento_se_rechaza()
    {
        // Es la mutación 4. Con los dos puestos, el orden en el que se aplicarían no lo dice
        // nadie, y lo que sale depende de quién lea la fila.
        Escena escena = new();

        Resultado<LineaTarifaDto> resultado = await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CantidadDesde = 0m,
            Precio = 10m,
            DescuentoPorcentaje = 5m,
        });

        Fallo(resultado, "tarifa-linea-precio-o-descuento");
    }

    [Fact]
    public async Task Sin_precio_NI_descuento_tambien_y_ESTE_es_el_que_se_olvida()
    {
        // Es la mutación 5, y la que produce el precio cero por la puerta de atrás: la línea entra,
        // casa con el artículo, gana la precedencia y devuelve un importe que nadie escribió.
        Escena escena = new();

        Resultado<LineaTarifaDto> resultado = await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CantidadDesde = 0m,
        });

        Fallo(
            resultado,
            "tarifa-linea-precio-o-descuento",
            "ha entrado una línea sin precio y sin descuento. No da error al guardarla ni al " +
            "leerla: da un cero al facturar, y el descuadre aparece semanas después sin autor");
    }

    [Fact]
    public async Task El_primer_tramo_de_un_destino_tiene_que_empezar_en_cero()
    {
        // LA MITAD QUE CIERRA EL HUECO. Sin esto, una tabla que empezara en 10 no diría nada de
        // una cantidad de 3, y quien pidiera tres unidades vería «sin tarifa aplicable» con la
        // tabla delante y el precio escrito.
        Escena escena = new();

        Resultado<LineaTarifaDto> resultado = await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CantidadDesde = 10m,
            Precio = 8m,
        });

        Fallo(resultado, "tarifa-linea-primer-tramo-sin-cero");
    }

    [Fact]
    public async Task Con_el_primer_tramo_en_cero_los_siguientes_pueden_empezar_donde_quieran()
    {
        Escena escena = new();

        Resultado<LineaTarifaDto> primero = await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CantidadDesde = 0m,
            Precio = 10m,
        });

        primero.EsCorrecto.ShouldBeTrue();

        Resultado<LineaTarifaDto> segundo = await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CantidadDesde = 100m,
            Precio = 8m,
        });

        segundo.EsCorrecto.ShouldBeTrue();
        escena.Lineas.Guardadas.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Dos_tramos_del_mismo_destino_no_pueden_empezar_en_la_misma_cantidad()
    {
        // La mitad del solape que esta capa sí puede contestar con un error con nombre. La que de
        // verdad lo impide —dos peticiones a la vez— son los dos índices únicos parciales.
        Escena escena = new();

        await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CantidadDesde = 0m,
            Precio = 10m,
        });

        Resultado<LineaTarifaDto> repetido = await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CantidadDesde = 0m,
            Precio = 7m,
        });

        Fallo(repetido, "tarifa-linea-tramo-duplicado");
    }

    [Fact]
    public async Task El_primer_tramo_se_cuenta_por_destino_y_no_por_tarifa()
    {
        // Que el artículo ya tenga su tramo cero no le sirve a la categoría: cada destino tiene su
        // propia escala, y si esto se contara por tarifa la segunda tabla podría empezar por
        // arriba y abrir exactamente el hueco que la regla existe para cerrar.
        Escena escena = new();

        await escena.Crear(new CrearLineaTarifaDto
        {
            ArticuloId = escena.Articulo.Id,
            CantidadDesde = 0m,
            Precio = 10m,
        });

        Resultado<LineaTarifaDto> deLaCategoria = await escena.Crear(new CrearLineaTarifaDto
        {
            CategoriaId = escena.Categoria,
            CantidadDesde = 50m,
            Precio = 9m,
        });

        Fallo(deLaCategoria, "tarifa-linea-primer-tramo-sin-cero");
    }

    [Fact]
    public async Task Una_linea_de_una_tarifa_que_no_existe_no_entra()
    {
        Escena escena = new();

        Resultado<LineaTarifaDto> resultado = await escena.CrearEn(
            Guid.NewGuid(),
            new CrearLineaTarifaDto
            {
                ArticuloId = escena.Articulo.Id,
                CantidadDesde = 0m,
                Precio = 10m,
            });

        Fallo(resultado, "tarifa-no-encontrada");
    }

    private static void Fallo(
        Resultado<LineaTarifaDto> resultado,
        string codigo,
        string? porque = null)
    {
        resultado.EsCorrecto.ShouldBeFalse(porque ?? $"se esperaba «{codigo}»");
        resultado.Error!.Codigo.ShouldBe(codigo);
    }

    /// <summary>Una tarifa abierta, un artículo y una categoría, con sus dobles ya atados.</summary>
    private sealed class Escena
    {
        internal Escena()
        {
            Categoria = Guid.NewGuid();

            Articulo = Articulo.Crear(
                s_empresa, "ART-1", "Un artículo", TipoDeArticulo.Bien,
                Guid.NewGuid(), Guid.NewGuid(), Categoria, s_ahora);

            Tarifa = Tarifa.Crear(
                s_empresa, "PVP", "Precio de venta al público", Guid.NewGuid(),
                s_hoy.AddYears(-1), null, s_ahora);

            Articulos.Agregar(Articulo);
            Tarifas.Agregar(Tarifa);
            Categorias.Con(Categoria, null, "RAIZ");
            Categorias.Agregar(Domain.Catalogo.Categoria.Crear(
                s_empresa, "RAIZ", "Raíz", null, s_ahora));
        }

        internal Articulo Articulo { get; }

        internal Guid Categoria { get; }

        internal Tarifa Tarifa { get; }

        internal TarifasEnMemoria Tarifas { get; } = new();

        internal LineasDeTarifaEnMemoria Lineas { get; } = new();

        internal ArticulosEnMemoria Articulos { get; } = new();

        internal CategoriasEnMemoria Categorias { get; } = new();

        internal Task<Resultado<LineaTarifaDto>> Crear(CrearLineaTarifaDto peticion) =>
            CrearEn(Tarifa.Id, peticion);

        internal Task<Resultado<LineaTarifaDto>> CrearEn(
            Guid tarifaId,
            CrearLineaTarifaDto peticion) =>
            new CrearLineaTarifa(
                new UsuarioDe(s_empresa),
                Tarifas,
                Lineas,
                Articulos,
                Categorias,
                new EmpresasQueContestan(activa: true),
                new ConfirmacionesContadas(),
                new RelojParado(s_ahora))
            .EjecutarAsync(tarifaId, peticion, CancellationToken.None);
    }
}
