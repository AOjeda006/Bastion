using System.Globalization;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// Las exportaciones de Catálogo: la página del listado tal cual, y cada maestro nombrado solo con
/// el permiso de verlo (ADR-0074 §3). Sin él, va su identificador y no se pregunta a su dueño.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los permisos van de uno en uno</b>, en la teoría del artículo: un caso que los diera todos o
/// ninguno no vería un permiso cambiado por el de otro maestro, que es el error que se escribe
/// copiando una línea.
/// </para>
/// <para>
/// Lo que llega a la base —el filtro de la empresa en cada puerto y que la página sea la del
/// listado con su orden— lo ven los de integración. Aquí está lo que se decide en el caso de uso.
/// </para>
/// </remarks>
public sealed class LasExportacionesDeCatalogoTests
{
    private static readonly Guid s_empresa = Guid.NewGuid();

    private static readonly DateTimeOffset s_ahora = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static readonly Paginacion s_paginacion = new() { Pagina = 3, Tamanio = 2 };

    private readonly Nombrador _unidades = new("U");

    private readonly Nombrador _impuestos = new("I");

    private readonly Nombrador _divisas = new("D");

    private readonly Nombrador _articulos = new("A");

    private readonly CategoriasEnMemoria _categorias = new();

    [Fact]
    public async Task El_articulo_sale_con_la_pagina_del_listado_y_sus_tres_maestros_nombrados()
    {
        Categoria categoria = NuevaCategoria("=FRUTA", "Fruta", null);
        ArticuloDto conCategoria = Articulo(categoria.Id);
        ArticuloDto sinCategoria = Articulo(null) with { UnidadBaseId = conCategoria.UnidadBaseId };
        ListadoDeArticulos listado = new(new PaginaDe<ArticuloDto>([conCategoria, sinCategoria], 3, 2, 41));
        var acotada = Guid.NewGuid();

        PaginaDe<ArticuloExportado> pagina =
            await Exportar(listado, Usuario.Con(Todos)).EjecutarAsync(s_paginacion, acotada, CancellationToken.None);

        listado.Pedidas.ShouldHaveSingleItem().ShouldBe((s_paginacion, (Guid?)acotada));
        (pagina.Pagina, pagina.Tamanio, pagina.Total).ShouldBe((3, 2, 41L));
        pagina.Elementos.Select(fila => fila.Articulo).ShouldBe([conCategoria, sinCategoria]);

        ArticuloExportado primera = pagina.Elementos[0];
        primera.UnidadBase.ShouldBe(_unidades.Citado(conCategoria.UnidadBaseId));
        primera.ImpuestoPorDefecto.ShouldBe(_impuestos.Citado(conCategoria.ImpuestoPorDefectoId));
        primera.Categoria.ShouldBe(new MaestroCitado(categoria.Id, new NombreDeMaestro("=FRUTA", "Fruta")));
        pagina.Elementos[1].Categoria.ShouldBeNull("el artículo sin categoría no apunta a ninguna");

        _unidades.Preguntas.ShouldHaveSingleItem().ShouldBe(
            [conCategoria.UnidadBaseId], "una pregunta por página, con la unidad repetida una vez");
        _impuestos.Preguntas.ShouldHaveSingleItem().Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(PermisosDeOrganizacion.UnidadMedidaVer)]
    [InlineData(PermisosDeOrganizacion.ImpuestoVer)]
    [InlineData(PermisosDeCatalogo.CategoriaVer)]
    public async Task Cada_maestro_del_articulo_se_nombra_solo_con_el_permiso_de_verlo(string concedido)
    {
        Categoria categoria = NuevaCategoria("FRUTA", "Fruta", null);
        ArticuloDto articulo = Articulo(categoria.Id);
        ListadoDeArticulos listado = new(new PaginaDe<ArticuloDto>([articulo], 1, 200, 1));

        ArticuloExportado fila = (await Exportar(listado, Usuario.Con(concedido))
            .EjecutarAsync(s_paginacion, null, CancellationToken.None)).Elementos.ShouldHaveSingleItem();

        Nombrado(fila.UnidadBase, articulo.UnidadBaseId).ShouldBe(concedido == PermisosDeOrganizacion.UnidadMedidaVer);
        Nombrado(fila.ImpuestoPorDefecto, articulo.ImpuestoPorDefectoId)
            .ShouldBe(concedido == PermisosDeOrganizacion.ImpuestoVer);
        Nombrado(fila.Categoria, categoria.Id).ShouldBe(concedido == PermisosDeCatalogo.CategoriaVer);

        _unidades.Preguntas.Count.ShouldBe(concedido == PermisosDeOrganizacion.UnidadMedidaVer ? 1 : 0);
        _impuestos.Preguntas.Count.ShouldBe(concedido == PermisosDeOrganizacion.ImpuestoVer ? 1 : 0);
    }

    [Fact]
    public async Task La_categoria_nombra_a_su_madre_sin_mirar_permisos_y_la_raiz_no_tiene()
    {
        Categoria madre = NuevaCategoria("RAIZ", "Raíz", null);
        Categoria hija = NuevaCategoria("HOJA", "Hoja", madre.Id);
        ListadoDeCategorias listado = new(new PaginaDe<CategoriaDto>([Dto(madre), Dto(hija)], 1, 200, 2));

        PaginaDe<CategoriaExportada> pagina =
            await new ExportarCategorias(listado, _categorias).EjecutarAsync(s_paginacion, CancellationToken.None);

        listado.Pedidas.ShouldHaveSingleItem().ShouldBe(s_paginacion);
        pagina.Elementos[0].Padre.ShouldBeNull();
        pagina.Elementos[1].Padre.ShouldBe(new MaestroCitado(madre.Id, new NombreDeMaestro("RAIZ", "Raíz")));
        (pagina.Pagina, pagina.Tamanio, pagina.Total).ShouldBe((1, 200, 2L));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task La_tarifa_nombra_su_divisa_solo_con_el_permiso_de_verla(bool puedeVerla)
    {
        TarifaDto tarifa = new(
            Guid.NewGuid(), s_empresa, "PVP", "Venta", Guid.NewGuid(), new DateOnly(2026, 1, 1), null);
        ListadoDeTarifas listado = new(new PaginaDe<TarifaDto>([tarifa], 1, 200, 1));
        IUsuarioActual usuario =
            Usuario.Con(puedeVerla ? PermisosDeOrganizacion.DivisaVer : PermisosDeCatalogo.TarifaVer);

        TarifaExportada fila = (await new ExportarTarifas(listado, _divisas, usuario)
            .EjecutarAsync(s_paginacion, "PVP", CancellationToken.None)).Elementos.ShouldHaveSingleItem();

        listado.Pedidas.ShouldHaveSingleItem().ShouldBe((s_paginacion, "PVP"));
        fila.Tarifa.ShouldBe(tarifa);
        Nombrado(fila.Divisa, tarifa.DivisaId).ShouldBe(puedeVerla);
        _divisas.Preguntas.Count.ShouldBe(puedeVerla ? 1 : 0);
    }

    [Fact]
    public async Task Los_codigos_de_barras_salen_por_tramos_de_la_lista_entera_con_su_longitud_de_total()
    {
        var articuloId = Guid.NewGuid();
        CodigoBarrasDto[] codigos = [.. Enumerable.Range(1, 5).Select(numero => Codigo(articuloId, numero))];
        ListadoDeCodigos listado = new(Resultado.Correcto<IReadOnlyList<CodigoBarrasDto>>(codigos));
        ExportarCodigosBarrasDelArticulo exportar = new(listado, _articulos);

        PaginaDe<CodigoBarrasExportado> segunda = (await exportar.EjecutarAsync(
            articuloId, new Paginacion { Pagina = 2, Tamanio = 2 }, CancellationToken.None)).Valor;
        PaginaDe<CodigoBarrasExportado> tercera = (await exportar.EjecutarAsync(
            articuloId, new Paginacion { Pagina = 3, Tamanio = 2 }, CancellationToken.None)).Valor;

        segunda.Elementos.Select(fila => fila.CodigoBarras).ShouldBe([codigos[2], codigos[3]]);
        tercera.Elementos.Select(fila => fila.CodigoBarras).ShouldBe([codigos[4]]);
        (segunda.Pagina, segunda.Tamanio, segunda.Total).ShouldBe((2, 2, 5L));
        tercera.Total.ShouldBe(5L);
        segunda.Elementos.ShouldAllBe(fila => fila.Articulo == _articulos.Citado(articuloId));
        _articulos.Preguntas.Select(pregunta => pregunta.Single())
            .ShouldBe([articuloId, articuloId], "una pregunta por tramo, sin permiso que mirar");
        exportar.CamposOrdenables.ShouldBeEmpty("como el listado, que no lee ?sort=");
    }

    [Fact]
    public async Task Los_codigos_de_barras_de_un_articulo_que_no_existe_son_el_error_del_listado()
    {
        var error = ErrorDeOperacion.NoEncontrado("articulo-no-encontrado", "No existe.");
        ListadoDeCodigos listado = new(Resultado.Fallo<IReadOnlyList<CodigoBarrasDto>>(error));

        Resultado<PaginaDe<CodigoBarrasExportado>> resultado = await new ExportarCodigosBarrasDelArticulo(
            listado, _articulos).EjecutarAsync(Guid.NewGuid(), s_paginacion, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error.ShouldBe(error);
        _articulos.Preguntas.ShouldBeEmpty();
    }

    private static string[] Todos =>
    [
        PermisosDeOrganizacion.UnidadMedidaVer,
        PermisosDeOrganizacion.ImpuestoVer,
        PermisosDeCatalogo.CategoriaVer,
    ];

    private static bool Nombrado(MaestroCitado? citado, Guid id)
    {
        citado.ShouldNotBeNull();
        citado.Id.ShouldBe(id, "el identificador va siempre, se haya nombrado o no");

        return citado.Nombre is not null;
    }

    private static ArticuloDto Articulo(Guid? categoriaId) => new(
        Guid.NewGuid(),
        s_empresa,
        "ART-" + Guid.NewGuid().ToString("N")[..6],
        "Manzana",
        "Mercaderia",
        "Ninguna",
        Guid.NewGuid(),
        Guid.NewGuid(),
        categoriaId);

    private static CategoriaDto Dto(Categoria categoria) =>
        new(categoria.Id, categoria.EmpresaId, categoria.Codigo, categoria.Nombre, categoria.PadreId);

    private static CodigoBarrasDto Codigo(Guid articuloId, int numero) =>
        new(
            Guid.NewGuid(),
            s_empresa,
            articuloId,
            "84" + numero.ToString("D11", CultureInfo.InvariantCulture),
            "Unidad",
            numero);

    private Categoria NuevaCategoria(string codigo, string nombre, Guid? padreId)
    {
        var categoria = Categoria.Crear(s_empresa, codigo, nombre, padreId, s_ahora);
        _categorias.Agregar(categoria);

        return categoria;
    }

    private ExportarArticulos Exportar(ListadoDeArticulos listado, IUsuarioActual usuario) =>
        new(listado, _categorias, _unidades, _impuestos, usuario);

    /// <summary>Un usuario con los permisos que se le den, y ninguno más.</summary>
    private sealed class Usuario(IReadOnlySet<Permiso> concedidos) : IUsuarioActual
    {
        public bool EstaAutenticado => true;

        public Guid UsuarioId { get; } = Guid.NewGuid();

        public Guid EmpresaId => s_empresa;

        public static Usuario Con(params string[] permisos) => new(new HashSet<Permiso>(permisos.Select(Permiso.De)));

        public bool Tiene(Permiso permiso) => concedidos.Contains(permiso);
    }

    /// <summary>
    /// El puerto de un maestro que nombra todo lo que se le pregunta, con su prefijo, y apunta cada
    /// pregunta. Sirve de los cuatro puertos, porque los cuatro nombran con la misma firma.
    /// </summary>
    private sealed class Nombrador(string prefijo)
        : IConsultaDeUnidadesDeMedida, IConsultaDeImpuestos, IConsultaDeDivisas, IConsultaDeArticulos
    {
        internal List<Guid[]> Preguntas { get; } = [];

        internal MaestroCitado Citado(Guid id) => new(id, Nombre(id));

        public Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombresDeAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken cancelacion)
        {
            Preguntas.Add([.. ids]);

            return Task.FromResult<IReadOnlyDictionary<Guid, NombreDeMaestro>>(ids.ToDictionary(id => id, Nombre));
        }

        public Task<EstadoDeMaestro> EstadoDeAsync(Guid id, CancellationToken cancelacion) =>
            throw new NotSupportedException("La exportación no pregunta por el estado.");

        public Task<EstadoDeMaestro> EstadoDeAsync(Guid id, DateOnly devengo, CancellationToken cancelacion) =>
            throw new NotSupportedException("La exportación no pregunta por el estado.");

        public Task<AptitudParaMoverExistencias> AptitudDeAsync(Guid articuloId, CancellationToken cancelacion) =>
            throw new NotSupportedException("La exportación no pregunta por la aptitud.");

        public Task<IReadOnlyDictionary<Guid, Guid>> UnidadesBaseDeAsync(
            IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion) =>
            throw new NotSupportedException("La exportación no pregunta por la unidad base.");

        private NombreDeMaestro Nombre(Guid id) => new($"{prefijo}-{id:N}"[..8], $"{prefijo} {id:N}");
    }

    private sealed class ListadoDeArticulos(PaginaDe<ArticuloDto> pagina) : IListarArticulos
    {
        internal List<(Paginacion, Guid?)> Pedidas { get; } = [];

        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<PaginaDe<ArticuloDto>> EjecutarAsync(
            Paginacion paginacion, Guid? categoriaId, CancellationToken cancelacion)
        {
            Pedidas.Add((paginacion, categoriaId));

            return Task.FromResult(pagina);
        }
    }

    private sealed class ListadoDeCategorias(PaginaDe<CategoriaDto> pagina) : IListarCategorias
    {
        internal List<Paginacion> Pedidas { get; } = [];

        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<PaginaDe<CategoriaDto>> EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion)
        {
            Pedidas.Add(paginacion);

            return Task.FromResult(pagina);
        }
    }

    private sealed class ListadoDeTarifas(PaginaDe<TarifaDto> pagina) : IListarTarifas
    {
        internal List<(Paginacion, string?)> Pedidas { get; } = [];

        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<PaginaDe<TarifaDto>> EjecutarAsync(
            Paginacion paginacion, string? codigo, CancellationToken cancelacion)
        {
            Pedidas.Add((paginacion, codigo));

            return Task.FromResult(pagina);
        }
    }

    private sealed class ListadoDeCodigos(Resultado<IReadOnlyList<CodigoBarrasDto>> resultado)
        : IListarCodigosBarrasDelArticulo
    {
        public Task<Resultado<IReadOnlyList<CodigoBarrasDto>>> EjecutarAsync(
            Guid articuloId, CancellationToken cancelacion) => Task.FromResult(resultado);
    }
}
