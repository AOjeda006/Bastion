using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Exportacion;
using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Application;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Application.Recuentos;
using Bastion.Inventario.Application.Saldos;
using Bastion.Inventario.Application.Transferencias;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Contracts.Saldos;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Organizacion.Contracts;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Contracts.Unidades;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Exportacion;

/// <summary>
/// Las exportaciones de Inventario: la página del listado tal cual, y cada maestro nombrado por su
/// dueño y solo con el permiso de verlo (ADR-0074 §3). Sin él, va su identificador, y no se
/// pregunta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada maestro tiene su propio nombrador</b>, con su prefijo, así que un maestro nombrado por
/// el puerto de otro sale con otro nombre y el caso lo ve. Y los permisos van de uno en uno, en la
/// teoría: un caso que los diera todos o ninguno no vería uno cambiado por el de otro maestro.
/// </para>
/// <para>
/// Lo que llega a la base —el filtro de la empresa, el orden y lo bloqueado— lo ven los de
/// integración. Aquí está lo que se decide en el caso de uso.
/// </para>
/// </remarks>
public sealed class LasExportacionesDeInventarioTests
{
    private static readonly Paginacion s_paginacion = new() { Pagina = 2, Tamanio = 2 };

    private static readonly DateOnly s_dia = new(2026, 10, 27);

    private readonly Nombrador _almacenes = new("AL");

    private readonly Nombrador _ubicaciones = new("UB");

    private readonly Nombrador _articulos = new("AR");

    private readonly Nombrador _unidades = new("UN");

    [Theory]
    [InlineData(PermisosDeOrganizacion.AlmacenVer)]
    [InlineData(PermisosDeOrganizacion.UbicacionVer)]
    [InlineData(PermisosDeCatalogo.ArticuloVer)]
    [InlineData(PermisosDeOrganizacion.UnidadMedidaVer)]
    public async Task Cada_maestro_se_nombra_solo_con_el_permiso_de_verlo(string concedido)
    {
        LosNombresDeLosMaestros nombres = Nombres(Usuario.Con(concedido));
        var id = Guid.NewGuid();

        (string Permiso, Nombrador Puerto, IReadOnlyDictionary<Guid, NombreDeMaestro> Nombres)[] pedidos =
        [
            (PermisosDeOrganizacion.AlmacenVer, _almacenes,
                await nombres.DeAlmacenesAsync([id], CancellationToken.None)),
            (PermisosDeOrganizacion.UbicacionVer, _ubicaciones,
                await nombres.DeUbicacionesAsync([id], CancellationToken.None)),
            (PermisosDeCatalogo.ArticuloVer, _articulos,
                await nombres.DeArticulosAsync([id], CancellationToken.None)),
            (PermisosDeOrganizacion.UnidadMedidaVer, _unidades,
                await nombres.DeUnidadesAsync([id], CancellationToken.None)),
        ];

        foreach ((string permiso, Nombrador puerto, IReadOnlyDictionary<Guid, NombreDeMaestro> devueltos) in pedidos)
        {
            bool puede = string.Equals(permiso, concedido, StringComparison.Ordinal);

            devueltos.ContainsKey(id).ShouldBe(puede, $"{permiso}, concedido {concedido}");
            puerto.Preguntas.Count.ShouldBe(puede ? 1 : 0, $"sin {permiso} no se pregunta a su dueño");
        }
    }

    [Fact]
    public async Task El_ajuste_sale_con_la_pagina_del_listado_y_su_almacen_nombrado()
    {
        AjusteResumenDto ajuste = new(Guid.NewGuid(), 7, Guid.NewGuid(), s_dia, "Confirmado", "Rotura");
        ListadoDeAjustes listado = new(new PaginaDe<AjusteResumenDto>([ajuste, ajuste], 2, 2, 9));
        var almacen = Guid.NewGuid();

        PaginaDe<AjusteExportado> pagina = await new ExportarAjustes(listado, Nombres(Usuario.ConTodos()))
            .EjecutarAsync(s_paginacion, "Confirmado", almacen, CancellationToken.None);

        listado.Pedidas.ShouldBe([(s_paginacion, "Confirmado", (Guid?)almacen)]);
        (pagina.Pagina, pagina.Tamanio, pagina.Total).ShouldBe((2, 2, 9L));
        pagina.Elementos.ShouldAllBe(fila => fila.Ajuste == ajuste);
        pagina.Elementos.ShouldAllBe(fila => fila.Almacen == _almacenes.Citado(ajuste.AlmacenId));
        _almacenes.Preguntas.ShouldHaveSingleItem().ShouldBe([ajuste.AlmacenId], "una vez por página, sin repetidos");
    }

    [Fact]
    public async Task El_recuento_sale_con_la_pagina_del_listado_y_su_almacen_nombrado()
    {
        RecuentoResumenDto recuento = new(Guid.NewGuid(), 3, Guid.NewGuid(), s_dia, null, "EnCurso", "Anual");
        ListadoDeRecuentos listado = new(new PaginaDe<RecuentoResumenDto>([recuento], 2, 2, 3));
        var almacen = Guid.NewGuid();

        PaginaDe<RecuentoExportado> pagina = await new ExportarRecuentos(listado, Nombres(Usuario.ConTodos()))
            .EjecutarAsync(s_paginacion, "EnCurso", almacen, CancellationToken.None);

        listado.Pedidas.ShouldBe([(s_paginacion, "EnCurso", (Guid?)almacen)]);
        (pagina.Pagina, pagina.Tamanio, pagina.Total).ShouldBe((2, 2, 3L));
        pagina.Elementos.ShouldHaveSingleItem()
            .ShouldBe(new RecuentoExportado(recuento, _almacenes.Citado(recuento.AlmacenId)));
    }

    [Fact]
    public async Task La_linea_de_recuento_sale_con_sus_tres_maestros_nombrados_cada_uno_por_su_dueno()
    {
        LineaDeRecuentoDto linea = LineaDeRecuento();
        var recuento = Guid.NewGuid();
        ListadoDeLineasDeRecuento listado = new(
            Resultado.Correcto(new PaginaDe<LineaDeRecuentoDto>([linea], 2, 2, 3)));

        Resultado<PaginaDe<LineaDeRecuentoExportada>> resultado =
            await new ExportarLineasDeRecuento(listado, Nombres(Usuario.ConTodos()))
                .EjecutarAsync(s_paginacion, recuento, "Diferencias", CancellationToken.None);

        listado.Pedidas.ShouldBe([(s_paginacion, recuento, "Diferencias")]);
        resultado.EsCorrecto.ShouldBeTrue();
        (resultado.Valor.Pagina, resultado.Valor.Tamanio, resultado.Valor.Total).ShouldBe((2, 2, 3L));
        resultado.Valor.Elementos.ShouldHaveSingleItem().ShouldBe(new LineaDeRecuentoExportada(
            linea,
            _ubicaciones.Citado(linea.UbicacionId),
            _articulos.Citado(linea.ArticuloId),
            _unidades.Citado(linea.UnidadBaseId)));
    }

    [Fact]
    public async Task Las_lineas_de_un_recuento_que_no_existe_son_el_error_del_listado()
    {
        var noEsta = ErrorDeOperacion.NoEncontrado("recuento-no-encontrado", "No está.");
        ListadoDeLineasDeRecuento listado = new(Resultado.Fallo<PaginaDe<LineaDeRecuentoDto>>(noEsta));

        Resultado<PaginaDe<LineaDeRecuentoExportada>> resultado =
            await new ExportarLineasDeRecuento(listado, Nombres(Usuario.ConTodos()))
                .EjecutarAsync(s_paginacion, Guid.NewGuid(), null, CancellationToken.None);

        resultado.Error.ShouldBe(noEsta);
        PreguntasATodos().ShouldBe(0, "sin página no se pregunta a nadie");
    }

    [Fact]
    public async Task La_transferencia_nombra_sus_dos_almacenes_en_una_sola_pregunta()
    {
        TransferenciaResumenDto transferencia = new(
            Guid.NewGuid(), 4, Guid.NewGuid(), Guid.NewGuid(), s_dia, null, "Enviada", null);
        ListadoDeTransferencias listado = new(new PaginaDe<TransferenciaResumenDto>([transferencia], 2, 2, 3));
        var origen = Guid.NewGuid();
        var destino = Guid.NewGuid();

        PaginaDe<TransferenciaExportada> pagina =
            await new ExportarTransferencias(listado, Nombres(Usuario.ConTodos()))
                .EjecutarAsync(s_paginacion, "Enviada", origen, destino, CancellationToken.None);

        listado.Pedidas.ShouldBe([(s_paginacion, "Enviada", (Guid?)origen, (Guid?)destino)]);
        (pagina.Pagina, pagina.Tamanio, pagina.Total).ShouldBe((2, 2, 3L));
        pagina.Elementos.ShouldHaveSingleItem().ShouldBe(new TransferenciaExportada(
            transferencia,
            _almacenes.Citado(transferencia.AlmacenOrigenId),
            _almacenes.Citado(transferencia.AlmacenDestinoId)));
        _almacenes.Preguntas.ShouldHaveSingleItem()
            .ShouldBe([transferencia.AlmacenOrigenId, transferencia.AlmacenDestinoId], ignoreOrder: true);
    }

    [Fact]
    public async Task Las_lineas_de_la_transferencia_salen_por_tramos_de_su_ficha_con_su_divisa()
    {
        LineaDeFichaDeTransferenciaDto[] lineas = [.. Enumerable.Range(1, 5).Select(LineaDeTransferencia)];
        var id = Guid.NewGuid();
        FichaDeLaTransferencia ficha = new(Resultado.Correcto(new ConVersion<FichaDeTransferenciaDto>(
            Ficha(id, lineas), new VersionDeRecurso(1))));

        Resultado<PaginaDe<LineaDeTransferenciaExportada>> resultado =
            await new ExportarLineasDeTransferencia(ficha, Nombres(Usuario.ConTodos()))
                .EjecutarAsync(id, s_paginacion, CancellationToken.None);

        ficha.Pedidas.ShouldBe([id]);
        resultado.EsCorrecto.ShouldBeTrue();
        (resultado.Valor.Pagina, resultado.Valor.Tamanio, resultado.Valor.Total)
            .ShouldBe((2, 2, 5L), "el total es la longitud de la lista entera");
        resultado.Valor.Elementos.Select(fila => fila.Linea.Numero).ShouldBe([3, 4], "la segunda página de dos");

        LineaDeTransferenciaExportada tercera = resultado.Valor.Elementos[0];

        tercera.ShouldBe(new LineaDeTransferenciaExportada(
            lineas[2],
            "EUR",
            _ubicaciones.Citado(lineas[2].UbicacionOrigenId),
            _ubicaciones.Citado(lineas[2].UbicacionDestinoId),
            _articulos.Citado(lineas[2].ArticuloId),
            _unidades.Citado(lineas[2].UnidadIntroducidaId)));
        _ubicaciones.Preguntas.ShouldHaveSingleItem().Length
            .ShouldBe(4, "el origen y el destino del tramo, en una sola pregunta");
    }

    [Fact]
    public async Task Las_lineas_de_una_transferencia_que_no_existe_son_el_error_de_la_ficha()
    {
        var noEsta = ErrorDeOperacion.NoEncontrado("transferencia-no-encontrada", "No está.");
        FichaDeLaTransferencia ficha = new(Resultado.Fallo<ConVersion<FichaDeTransferenciaDto>>(noEsta));

        Resultado<PaginaDe<LineaDeTransferenciaExportada>> resultado =
            await new ExportarLineasDeTransferencia(ficha, Nombres(Usuario.ConTodos()))
                .EjecutarAsync(Guid.NewGuid(), s_paginacion, CancellationToken.None);

        resultado.Error.ShouldBe(noEsta);
        PreguntasATodos().ShouldBe(0, "sin ficha no se pregunta a nadie");
    }

    [Fact]
    public async Task El_saldo_sale_con_el_dia_que_contesta_la_consulta_y_sus_dos_maestros_nombrados()
    {
        SaldoDto saldo = new(Guid.NewGuid(), Guid.NewGuid(), 4m, 10m, 1m, 2.5m, "EUR");
        ConsultaDeSaldos consulta = new(Resultado.Correcto(
            new SaldosAUnaFechaDto(s_dia, new PaginaDe<SaldoDto>([saldo], 2, 2, 3))));
        var almacen = Guid.NewGuid();
        var articulo = Guid.NewGuid();

        Resultado<PaginaDe<SaldoExportado>> resultado =
            await new ExportarSaldos(consulta, Nombres(Usuario.ConTodos()), new Reloj(s_dia))
                .EjecutarAsync(s_dia, almacen, articulo, s_paginacion, CancellationToken.None);

        consulta.Pedidas.ShouldBe([((DateOnly?)s_dia, (Guid?)almacen, (Guid?)articulo, s_paginacion)]);
        resultado.EsCorrecto.ShouldBeTrue();
        (resultado.Valor.Pagina, resultado.Valor.Tamanio, resultado.Valor.Total).ShouldBe((2, 2, 3L));
        resultado.Valor.Elementos.ShouldHaveSingleItem().ShouldBe(new SaldoExportado(
            s_dia, saldo, _articulos.Citado(saldo.ArticuloId), _almacenes.Citado(saldo.AlmacenId)));
    }

    [Fact]
    public async Task Los_saldos_a_una_fecha_futura_son_el_error_de_la_consulta()
    {
        var futura = ErrorDeOperacion.Validacion("saldo-con-fecha-futura", "Es futura.");
        ConsultaDeSaldos consulta = new(Resultado.Fallo<SaldosAUnaFechaDto>(futura));

        Resultado<PaginaDe<SaldoExportado>> resultado =
            await new ExportarSaldos(consulta, Nombres(Usuario.ConTodos()), new Reloj(s_dia))
                .EjecutarAsync(s_dia.AddDays(1), null, null, s_paginacion, CancellationToken.None);

        resultado.Error.ShouldBe(futura);
        PreguntasATodos().ShouldBe(0, "sin página no se pregunta a nadie");
    }

    [Fact]
    public void El_dia_de_la_exportacion_es_el_pedido_y_sin_pedir_ninguno_hoy_en_utc()
    {
        // Las 23:30 en UTC son ya mañana en Madrid: el día es el de UTC, como en la consulta.
        Reloj reloj = new(new DateTimeOffset(2026, 10, 27, 23, 30, 0, TimeSpan.Zero));
        ExportarSaldos exportar = new(new ConsultaDeSaldos(null!), Nombres(Usuario.ConTodos()), reloj);

        exportar.DiaDe(null).ShouldBe(new DateOnly(2026, 10, 27));
        exportar.DiaDe(new DateOnly(2025, 1, 31)).ShouldBe(new DateOnly(2025, 1, 31));
    }

    private static LineaDeRecuentoDto LineaDeRecuento() => new(
        Guid.NewGuid(),
        1,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "L-1",
        null,
        Guid.NewGuid(),
        "Teorico",
        2.5m,
        3m,
        4m,
        4m,
        0m,
        -1m,
        false,
        null);

    private static LineaDeFichaDeTransferenciaDto LineaDeTransferencia(int numero) => new(
        numero, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2m, Guid.NewGuid(), 1m, 2m, null, null, 5m);

    private static FichaDeTransferenciaDto Ficha(Guid id, IReadOnlyList<LineaDeFichaDeTransferenciaDto> lineas) => new(
        id, Guid.NewGuid(), 4, Guid.NewGuid(), Guid.NewGuid(), s_dia, null, "Enviada", "EUR", null, null, lineas);

    private LosNombresDeLosMaestros Nombres(IUsuarioActual usuario) =>
        new(_almacenes, _ubicaciones, _articulos, _unidades, usuario);

    private int PreguntasATodos() =>
        _almacenes.Preguntas.Count + _ubicaciones.Preguntas.Count + _articulos.Preguntas.Count
        + _unidades.Preguntas.Count;

    /// <summary>Un usuario con los permisos que se le den, y ninguno más.</summary>
    private sealed class Usuario(IReadOnlySet<Permiso> concedidos) : IUsuarioActual
    {
        public bool EstaAutenticado => true;

        public Guid UsuarioId { get; } = Guid.NewGuid();

        public Guid EmpresaId { get; } = Guid.NewGuid();

        public static Usuario Con(params string[] permisos) => new(new HashSet<Permiso>(permisos.Select(Permiso.De)));

        public static Usuario ConTodos() => Con(
            PermisosDeOrganizacion.AlmacenVer,
            PermisosDeOrganizacion.UbicacionVer,
            PermisosDeCatalogo.ArticuloVer,
            PermisosDeOrganizacion.UnidadMedidaVer);

        public bool Tiene(Permiso permiso) => concedidos.Contains(permiso);
    }

    /// <summary>
    /// El puerto de un maestro que nombra todo lo que se le pregunta, con su prefijo, y apunta cada
    /// pregunta. Sirve de los cuatro puertos, porque los cuatro nombran con la misma firma; cada
    /// maestro tiene el suyo, con otro prefijo.
    /// </summary>
    private sealed class Nombrador(string prefijo)
        : IConsultaDeAlmacenes, IConsultaDeUbicaciones, IConsultaDeArticulos, IConsultaDeUnidadesDeMedida
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

        public Task<EstadoDeMaestro> EstadoDeAsync(Guid almacenId, Guid ubicacionId, CancellationToken cancelacion) =>
            throw new NotSupportedException("La exportación no pregunta por el estado.");

        public Task<AptitudParaMoverExistencias> AptitudDeAsync(Guid articuloId, CancellationToken cancelacion) =>
            throw new NotSupportedException("La exportación no pregunta por la aptitud.");

        public Task<IReadOnlyDictionary<Guid, Guid>> UnidadesBaseDeAsync(
            IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion) =>
            throw new NotSupportedException("La exportación no pregunta por la unidad base.");

        private NombreDeMaestro Nombre(Guid id) => new($"{prefijo}-{id:N}"[..9], $"{prefijo} {id:N}");
    }

    /// <summary>Un reloj parado en un instante.</summary>
    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public Reloj(DateOnly dia)
            : this(new DateTimeOffset(dia.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero))
        {
        }

        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class ListadoDeAjustes(PaginaDe<AjusteResumenDto> pagina) : IListarAjustes
    {
        internal List<(Paginacion, string?, Guid?)> Pedidas { get; } = [];

        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<PaginaDe<AjusteResumenDto>> EjecutarAsync(
            Paginacion paginacion, string? estado, Guid? almacenId, CancellationToken cancelacion)
        {
            Pedidas.Add((paginacion, estado, almacenId));

            return Task.FromResult(pagina);
        }
    }

    private sealed class ListadoDeRecuentos(PaginaDe<RecuentoResumenDto> pagina) : IListarRecuentos
    {
        internal List<(Paginacion, string?, Guid?)> Pedidas { get; } = [];

        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<PaginaDe<RecuentoResumenDto>> EjecutarAsync(
            Paginacion paginacion, string? estado, Guid? almacenId, CancellationToken cancelacion)
        {
            Pedidas.Add((paginacion, estado, almacenId));

            return Task.FromResult(pagina);
        }
    }

    private sealed class ListadoDeLineasDeRecuento(Resultado<PaginaDe<LineaDeRecuentoDto>> resultado)
        : IListarLineasDeRecuento
    {
        internal List<(Paginacion, Guid, string?)> Pedidas { get; } = [];

        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<Resultado<PaginaDe<LineaDeRecuentoDto>>> EjecutarAsync(
            Paginacion paginacion, Guid recuentoId, string? solo, CancellationToken cancelacion)
        {
            Pedidas.Add((paginacion, recuentoId, solo));

            return Task.FromResult(resultado);
        }
    }

    private sealed class ListadoDeTransferencias(PaginaDe<TransferenciaResumenDto> pagina) : IListarTransferencias
    {
        internal List<(Paginacion, string?, Guid?, Guid?)> Pedidas { get; } = [];

        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<PaginaDe<TransferenciaResumenDto>> EjecutarAsync(
            Paginacion paginacion, string? estado, Guid? origenId, Guid? destinoId, CancellationToken cancelacion)
        {
            Pedidas.Add((paginacion, estado, origenId, destinoId));

            return Task.FromResult(pagina);
        }
    }

    private sealed class FichaDeLaTransferencia(Resultado<ConVersion<FichaDeTransferenciaDto>> resultado)
        : IObtenerTransferencia
    {
        internal List<Guid> Pedidas { get; } = [];

        public Task<Resultado<ConVersion<FichaDeTransferenciaDto>>> EjecutarAsync(
            Guid id, CancellationToken cancelacion)
        {
            Pedidas.Add(id);

            return Task.FromResult(resultado);
        }
    }

    private sealed class ConsultaDeSaldos(Resultado<SaldosAUnaFechaDto> resultado) : IConsultarSaldos
    {
        internal List<(DateOnly?, Guid?, Guid?, Paginacion)> Pedidas { get; } = [];

        public IReadOnlySet<string> CamposOrdenables { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<Resultado<SaldosAUnaFechaDto>> EjecutarAsync(
            DateOnly? fecha, Guid? almacenId, Guid? articuloId, Paginacion paginacion, CancellationToken cancelacion)
        {
            Pedidas.Add((fecha, almacenId, articuloId, paginacion));

            return Task.FromResult(resultado);
        }
    }
}
