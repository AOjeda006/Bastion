using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Ningún documento con una fecha anterior al último movimiento de alguna de sus claves, contra la
/// base de verdad (ADR-0047).
/// </summary>
/// <remarks>
/// <para>
/// <b>Las fechas son del año pasado, del 10, el 15 y el 20 de junio</b>, con su ejercicio y su
/// serie. Con fechas de este año, los casos dependerían del día en que se ejecutan: el 20 de junio
/// sería futuro hasta ese día.
/// </para>
/// <para>
/// <b>Las confirmaciones van por el caso de uso</b>, con su cerrojo y su transacción, salvo en el
/// caso de la guarda de la sentencia, que tiene que saltarse el dominio y va por el repositorio,
/// como <see cref="ElLibro"/>. El de la migración construye su propia base, sin la API.
/// </para>
/// <para>
/// <b>Semillas: el fichero entero es del 520 al 539.</b> Las empresas, del 520 al 524; los
/// maestros de instalación, del 530 al 535. Este carril comparte la base entre todos sus ficheros,
/// así que una semilla repetida no falla aquí: falla en el fichero de otro que la pedía primero.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class NingunaFechaAnteriorAlUltimoMovimientoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string ElCodigo = "ajuste-fecha-anterior-al-ultimo-movimiento";

    /// <summary>La migración anterior a la de la fecha, que es donde el caso de la migración se para.</summary>
    private const string LaDeAntes = "20260928201715_ElOrdenDeLasLineas";

    /// <summary>La migración de la fecha.</summary>
    private const string LaDeLaFecha = "20260929020453_LaUltimaFechaDeLaValoracion";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    /// <summary>
    /// El contraejemplo del ADR-0047: el día 10 entran 10 a 10 €, el día 20 entran 10 a 100 €, y una
    /// salida de 10 fechada el día 15 es un <c>422</c> que no escribe nada.
    /// </summary>
    /// <remarks>
    /// Sin la regla, la salida se valoraría a 55 y restaría 550 €, y el libro sumado hasta el día 15
    /// diría 0 unidades y −450 €. Nada de eso llega a la base: ni la fila del libro, ni la existencia,
    /// ni la valoración, que sigue con la fecha del 20.
    /// </remarks>
    [Fact]
    public async Task El_contraejemplo_con_la_salida_del_dia_15_es_422_y_no_escribe_nada()
    {
        ElCaso caso = await UnCasoAsync(520, "V47-A", 530);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, Dia(10), new Linea(0, 10m, 10m));
        await ConfirmarAsync(modulo, caso, Dia(20), new Linea(0, 10m, 100m));

        Guid laSalida = await AbrirAsync(modulo, caso, caso.Serie.Id, Dia(15), new Linea(0, -10m, null));

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(laSalida);

        confirmacion.Error.ShouldNotBeNull("una salida del 15 sobre una clave que se movió el 20 no se confirma");
        confirmacion.Error.Codigo.ShouldBe(ElCodigo);
        confirmacion.Error.Tipo.ShouldBe(TipoDeError.ReglaDeNegocio);
        confirmacion.Error.Mensaje.ShouldContain(Dia(20).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        await using InventarioDbContext contexto = postgres.AbrirInventario(caso.EmpresaId);

        (await contexto.Movimientos.CountAsync()).ShouldBe(2, "el rechazo no escribe el libro");
        (await contexto.Existencias.SumAsync(fila => fila.Fisico)).ShouldBe(20m, "ni la existencia");
        (await contexto.Valoraciones.AsNoTracking().SingleAsync()).Saldo.ShouldBe(
            new SaldoValorado(20m, Importe.De(1100m, "EUR"), Dia(20)), "ni la valoración, ni su fecha");
        (await contexto.Ajustes.SingleAsync(ajuste => ajuste.Id == laSalida)).Estado.ShouldBe(EstadoDeAjuste.Borrador);

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>La misma fecha que el último movimiento se confirma: la regla es «anterior».</summary>
    [Fact]
    public async Task La_misma_fecha_que_el_ultimo_movimiento_se_confirma()
    {
        ElCaso caso = await UnCasoAsync(521, "V47-B", 531);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, Dia(10), new Linea(0, 10m, 10m));
        await ConfirmarAsync(modulo, caso, Dia(20), new Linea(0, 10m, 100m));
        Guid laSalida = await ConfirmarAsync(modulo, caso, Dia(20), new Linea(0, -10m, null));

        MovimientoStock fila = (await FilasDeAsync(caso.EmpresaId, laSalida)).ShouldHaveSingleItem();

        fila.Valor.ShouldBe(Importe.De(-550m, "EUR"), "10 a 55, el precio medio del día 20");

        (await LasValoracionesAsync(caso.EmpresaId)).ShouldHaveSingleItem().Saldo
            .ShouldBe(new SaldoValorado(10m, Importe.De(550m, "EUR"), Dia(20)));

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>
    /// Otra clave con fecha atrasada se confirma, y el mismo artículo en otro almacén también: la
    /// fecha es de cada valoración.
    /// </summary>
    [Fact]
    public async Task Otra_clave_y_el_mismo_articulo_en_otro_almacen_admiten_una_fecha_atrasada()
    {
        ElCaso caso = await UnCasoAsync(522, "V47-C", 532, 533);

        AlmacenDto otroAlmacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(caso.Cliente, "V47-C2");
        UbicacionDto otraUbicacion =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(caso.Cliente, otroAlmacen.Id, "V47-C2-01");
        ElCaso enElOtro = caso with { AlmacenId = otroAlmacen.Id, UbicacionId = otraUbicacion.Id };

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, Dia(20), new Linea(0, 10m, 10m));
        await ConfirmarAsync(modulo, caso, Dia(15), new Linea(1, 4m, 5m));
        await ConfirmarAsync(modulo, enElOtro, Dia(15), new Linea(0, 3m, 7m));

        await using InventarioDbContext contexto = postgres.AbrirInventario(caso.EmpresaId);

        Dictionary<(Guid, Guid), DateOnly?> fechas = await contexto.Valoraciones
            .AsNoTracking()
            .ToDictionaryAsync(fila => (fila.ArticuloId, fila.AlmacenId), fila => fila.UltimaFecha);

        fechas.ShouldBe(
            new Dictionary<(Guid, Guid), DateOnly?>
            {
                [(caso.Articulos[0].ArticuloId, caso.AlmacenId)] = Dia(20),
                [(caso.Articulos[1].ArticuloId, caso.AlmacenId)] = Dia(15),
                [(caso.Articulos[0].ArticuloId, otroAlmacen.Id)] = Dia(15),
            },
            ignoreOrder: true);

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 3);
    }

    /// <summary>
    /// Dos transacciones de verdad con las fechas cruzadas: la del día 20 tiene el cerrojo, y la del
    /// día 15 espera, lee la fecha que la otra dejó y recibe el <c>422</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el caso por el que la fecha se mira contra la fila bloqueada</b> (ADR-0047 §2). Leída
    /// antes del cerrojo, la del 15 vería la clave con su último movimiento el día 10, y pasaría.
    /// </para>
    /// <para>
    /// <b>La primera se para con la valoración bloqueada y nada escrito</b>, como en la carrera del
    /// precio medio, y por lo mismo. Y la clave ya existe, con su movimiento del día 10: con una
    /// clave nueva, las dos chocarían en el índice único al crearla, y el caso saldría verde con la
    /// mitad del mecanismo.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Con_las_fechas_cruzadas_la_del_dia_15_espera_a_la_del_20_y_recibe_el_422()
    {
        ElCaso caso = await UnCasoAsync(523, "V47-D", 534);

        // DOS SERIES, como en las otras carreras: con una sola, las dos se pararían antes en el
        // contador, y la segunda llegaría a la valoración con la primera ya confirmada.
        SerieDto otra = await LosMaestrosPorLaApi.CrearSerieEnAsync(caso.Cliente, caso.EjercicioId, "V47-D2");

        await using ElModuloDeInventario unos = new(postgres, caso.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(unos, caso, Dia(10), new Linea(0, 10m, 10m));

        Guid delVeinte = await AbrirAsync(unos, caso, caso.Serie.Id, Dia(20), new Linea(0, 10m, 100m));
        Guid delQuince = await AbrirAsync(otros, caso, otra.Id, Dia(15), new Linea(0, -10m, null));

        await using (IDbContextTransaction enVuelo = await unos.AbrirTransaccionAsync())
        {
            (await unos.BloquearLasValoracionesAsync(
                    [new ClaveDeValoracion(caso.Articulos[0].ArticuloId, caso.AlmacenId)], "EUR"))
                .ShouldHaveSingleItem().Value
                .ShouldBe(new SaldoValorado(10m, Importe.De(100m, "EUR"), Dia(10)));

            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(delQuince);

            await EsperarAQueLaFreneAsync(unos.ProcesoDeLaBase, laOtra);

            Resultado<AjusteDto> confirmacion = await unos.ConfirmarSinAbrirTransaccionAsync(delVeinte);

            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            await enVuelo.CommitAsync();

            Resultado<AjusteDto> segunda = await laOtra.WaitAsync(TimeSpan.FromSeconds(30));

            segunda.Error.ShouldNotBeNull(
                "la del 15 ha confirmado: ha leído la fecha de la clave sin esperar a la del 20");
            segunda.Error.Codigo.ShouldBe(ElCodigo);
        }

        (await FilasDeAsync(caso.EmpresaId, delQuince)).ShouldBeEmpty("la del 15 no escribe el libro");

        (await LasValoracionesAsync(caso.EmpresaId)).ShouldHaveSingleItem().Saldo
            .ShouldBe(new SaldoValorado(20m, Importe.De(1100m, "EUR"), Dia(20)));

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>
    /// Quien se salte el dominio se estrella en la sentencia que suma: la fila no va hacia atrás, y
    /// es un defecto, no un <c>422</c>.
    /// </summary>
    /// <remarks>
    /// El documento se confirma por el repositorio, con el saldo bloqueado pero sin su fecha, que es
    /// lo que haría un caso de uso que se olvidara de preguntar. La existencia se mueve —su sentencia
    /// va antes—, la valoración no, y el recuento lo denuncia. Nada queda escrito, porque la
    /// transacción se deshace.
    /// </remarks>
    [Fact]
    public async Task La_sentencia_que_suma_no_mueve_la_fecha_hacia_atras_aunque_se_salte_el_dominio()
    {
        ElCaso caso = await UnCasoAsync(524, "V47-E", 535);

        await using (ElModuloDeInventario modulo = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(modulo, caso, Dia(20), new Linea(0, 10m, 10m));
        }

        // UNA SERIE INVENTADA, como en LaValoracionDelAjusteTests: el número 1 de la del caso ya lo
        // tiene el ajuste del día 20, y el índice único de la numeración saltaría antes que la guarda.
        DateTimeOffset momento = DateTimeOffset.UtcNow;
        var ajuste = Ajuste.Abrir(
            caso.EmpresaId, Guid.CreateVersion7(), caso.AlmacenId, Dia(15), "Saltándose el dominio", "EUR", momento);

        ajuste.AnadirLinea(
            caso.UbicacionId, caso.Articulos[0].ArticuloId, -5m, caso.Articulos[0].UnidadId, 1m, null, momento);

        await using (InventarioDbContext contexto = postgres.AbrirInventario(caso.EmpresaId))
        await using (IDbContextTransaction transaccion = await contexto.Database.BeginTransactionAsync())
        {
            RepositorioDeAjustes repositorio = new(contexto, new InquilinoFijo(caso.EmpresaId));
            IReadOnlyList<LineaAValorar> lineas = ajuste.LineasAValorar();

            IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> bloqueados =
                await repositorio.BloquearLasValoracionesAsync(
                    [.. lineas.Select(linea => linea.Clave).Distinct()], "EUR", CancellationToken.None);

            bloqueados.ShouldHaveSingleItem().Value.UltimaFecha.ShouldBe(
                Dia(20), "el dominio lo habría visto: el saldo bloqueado trae la fecha");

            // EL DOMINIO, SALTADO: el saldo llega sin su fecha, y la valoración no tiene nada que
            // objetar.
            var sinFecha = bloqueados.ToDictionary(
                par => par.Key, par => new SaldoValorado(par.Value.Cantidad, par.Value.Valor));

            IReadOnlyList<MovimientoStock> movimientos = ajuste.Confirmar(
                1,
                new AjusteConfirmado(ajuste.Id, caso.EmpresaId, caso.AlmacenId, Dia(15), 1),
                new ElPrecioMedioPonderado().Valorar(sinFecha, lineas, "EUR", Dia(15)),
                LotesYSeriesResueltos.Ninguno,
                momento);

            repositorio.Agregar(ajuste);

            InvalidOperationException defecto = await Should.ThrowAsync<InvalidOperationException>(
                () => repositorio.AnotarEnElLibroAsync(movimientos, CancellationToken.None));

            defecto.Message.ShouldContain("se movió después de la fecha del documento");

            await transaccion.RollbackAsync();
        }

        (await LasValoracionesAsync(caso.EmpresaId)).ShouldHaveSingleItem().Saldo
            .ShouldBe(new SaldoValorado(10m, Importe.De(100m, "EUR"), Dia(20)));

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>
    /// La migración rellena la fecha con el máximo del libro de cada clave, y deja nula la de una
    /// valoración sin filas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>En una base propia, migrada hasta la de antes</b>, con el libro y las valoraciones escritos
    /// a mano y sin orden de fecha: la del 20 entra antes que la del 15, así que un relleno que tomara
    /// la última fila escrita en vez de la fecha más alta saldría rojo.
    /// </para>
    /// <para>
    /// <b>La clave es la empresa, el artículo y el almacén</b>, y el caso tiene una fila de cada
    /// variación: el mismo artículo en otro almacén, y la misma clave en otra empresa con una fecha
    /// más alta que no puede colarse.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_migracion_rellena_la_fecha_con_el_maximo_del_libro_de_cada_clave()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);

        var empresa = Guid.CreateVersion7();
        var otraEmpresa = Guid.CreateVersion7();
        var articulo = Guid.CreateVersion7();
        var sinFilas = Guid.CreateVersion7();
        var almacen = Guid.CreateVersion7();
        var otroAlmacen = Guid.CreateVersion7();

        await MigrarHastaAsync(cadena, LaDeAntes);

        await using (NpgsqlConnection conexion = new(cadena))
        {
            await conexion.OpenAsync();

            await EscribirEnElLibroAsync(conexion, empresa, articulo, almacen, Dia(10));
            await EscribirEnElLibroAsync(conexion, empresa, articulo, almacen, Dia(20));
            await EscribirEnElLibroAsync(conexion, empresa, articulo, almacen, Dia(15));
            await EscribirEnElLibroAsync(conexion, empresa, articulo, otroAlmacen, Dia(5));
            await EscribirEnElLibroAsync(conexion, otraEmpresa, articulo, almacen, Dia(25));

            foreach ((Guid deEmpresa, Guid deArticulo, Guid deAlmacen) in new[]
            {
                (empresa, articulo, almacen),
                (empresa, articulo, otroAlmacen),
                (empresa, sinFilas, almacen),
                (otraEmpresa, articulo, almacen),
            })
            {
                await using NpgsqlCommand valoracion = new(
                    "INSERT INTO inventario.valoraciones (empresa_id, articulo_id, almacen_id, cantidad, valor, divisa) "
                    + "VALUES (@empresa, @articulo, @almacen, 0, 0, 'EUR')",
                    conexion);

                valoracion.Parameters.AddWithValue("empresa", deEmpresa);
                valoracion.Parameters.AddWithValue("articulo", deArticulo);
                valoracion.Parameters.AddWithValue("almacen", deAlmacen);
                await valoracion.ExecuteNonQueryAsync();
            }
        }

        await MigrarHastaAsync(cadena, LaDeLaFecha);

        Dictionary<(Guid, Guid, Guid), DateOnly?> fechas = [];

        await using (NpgsqlConnection conexion = new(cadena))
        {
            await conexion.OpenAsync();

            await using NpgsqlCommand lectura = new(
                "SELECT empresa_id, articulo_id, almacen_id, ultima_fecha FROM inventario.valoraciones",
                conexion);
            await using NpgsqlDataReader lector = await lectura.ExecuteReaderAsync();

            while (await lector.ReadAsync())
            {
                fechas.Add(
                    (lector.GetGuid(0), lector.GetGuid(1), lector.GetGuid(2)),
                    lector.IsDBNull(3) ? null : lector.GetFieldValue<DateOnly>(3));
            }
        }

        fechas.ShouldBe(
            new Dictionary<(Guid, Guid, Guid), DateOnly?>
            {
                [(empresa, articulo, almacen)] = Dia(20),
                [(empresa, articulo, otroAlmacen)] = Dia(5),
                [(empresa, sinFilas, almacen)] = null,
                [(otraEmpresa, articulo, almacen)] = Dia(25),
            },
            ignoreOrder: true);
    }

    /// <summary>El año de los casos: el pasado, que tiene todos sus días detrás de hoy.</summary>
    private static int AnioPasado => DateTime.UtcNow.Year - 1;

    /// <summary>Un día de junio del año pasado.</summary>
    private static DateOnly Dia(int dia) => new(AnioPasado, 6, dia);

    /// <summary>Aplica las migraciones de Inventario hasta la que se diga, en una base propia.</summary>
    private static async Task MigrarHastaAsync(string cadena, string migracion)
    {
        DbContextOptionsBuilder<InventarioDbContext> opciones = new();
        InventarioDbContext.Configurar(opciones, cadena);

        await using InventarioDbContext contexto = new(opciones.Options, new InquilinoFijo(null), new AccesoCerrado());

        await contexto.GetService<IMigrator>().MigrateAsync(migracion);
    }

    /// <summary>Una fila del libro escrita a mano, con lo mínimo que la tabla exige.</summary>
    private static async Task EscribirEnElLibroAsync(
        NpgsqlConnection conexion, Guid empresaId, Guid articuloId, Guid almacenId, DateOnly fecha)
    {
        await using NpgsqlCommand fila = new(
            """
            INSERT INTO inventario.movimiento_stock
                (id, fecha_de_operacion, empresa_id, articulo_id, almacen_id, ubicacion_id,
                 unidad_introducida_id, cantidad_introducida, factor_a_unidad_base,
                 cantidad_en_unidad_base, divisa, documento_origen_tipo, documento_origen_id,
                 precio_medio, valor, creado_en, modificado_en)
            VALUES
                (gen_random_uuid(), @fecha, @empresa, @articulo, @almacen, gen_random_uuid(),
                 gen_random_uuid(), 1, 1, 1, 'EUR', 'Ajuste', gen_random_uuid(),
                 0, 0, now(), now())
            """,
            conexion);

        fila.Parameters.AddWithValue("fecha", fecha);
        fila.Parameters.AddWithValue("empresa", empresaId);
        fila.Parameters.AddWithValue("articulo", articuloId);
        fila.Parameters.AddWithValue("almacen", almacenId);
        await fila.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Una empresa en euros con un almacén de una ubicación, sus artículos, el ejercicio del año
    /// pasado y una serie en él.
    /// </summary>
    private async Task<ElCaso> UnCasoAsync(int semilla, string codigo, params int[] articulos)
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));
        _clientes.Add(cliente);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);
        UbicacionDto ubicacion = await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01");

        List<(Guid ArticuloId, Guid UnidadId)> deInstalacion = [];

        foreach (int numero in articulos)
        {
            deInstalacion.Add(await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, numero));
        }

        EjercicioDto pasado = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, AnioPasado);
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, pasado.Id, codigo);

        return new ElCaso(cliente, empresa.Id, almacen.Id, ubicacion.Id, deInstalacion, pasado.Id, serie);
    }

    private static async Task<Guid> AbrirAsync(
        ElModuloDeInventario modulo, ElCaso caso, Guid serieId, DateOnly fecha, params Linea[] lineas)
    {
        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                serieId,
                caso.AlmacenId,
                fecha,
                "Recuento del addendum del 2.8",
                [.. lineas.Select(linea => new LineaDeAjusteDto(
                    caso.UbicacionId,
                    caso.Articulos[linea.Articulo].ArticuloId,
                    linea.Cantidad,
                    caso.Articulos[linea.Articulo].UnidadId,
                    1m,
                    linea.Coste))]),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    private static async Task<Guid> ConfirmarAsync(
        ElModuloDeInventario modulo, ElCaso caso, DateOnly fecha, params Linea[] lineas)
    {
        Guid ajusteId = await AbrirAsync(modulo, caso, caso.Serie.Id, fecha, lineas);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

        return ajusteId;
    }

    private async Task<IReadOnlyList<MovimientoStock>> FilasDeAsync(Guid empresaId, Guid documentoId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await contexto.Movimientos
            .AsNoTracking()
            .Where(fila => fila.DocumentoOrigenId == documentoId)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<Valoracion>> LasValoracionesAsync(Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await contexto.Valoraciones.AsNoTracking().ToListAsync();
    }

    private async Task ExigirQueCuadraAsync(Guid empresaId, int valoraciones)
    {
        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, empresaId);

        cuadre.ValoracionesComparadas.ShouldBe(
            valoraciones, "sin valoraciones que mirar, «no hay descuadres» sale verde por no mirar");
        cuadre.Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// Espera a que la operación en vuelo se quede parada detrás del proceso dado, o falla.
    /// </summary>
    /// <remarks>
    /// El mecanismo, y por qué no se suelta por tiempo, están en <see cref="LaEspera"/>. Aquí solo
    /// va a quién se espera y qué habría hecho mal la operación si no esperara.
    /// </remarks>
    /// <param name="procesoQueFrena">El proceso de PostgreSQL de la transacción en vuelo.</param>
    /// <param name="enVuelo">La operación que tiene que quedarse esperando.</param>
    private Task EsperarAQueLaFreneAsync(int procesoQueFrena, Task enVuelo) =>
        LaEspera.AQueLaFreneAsync(
            postgres.CadenaDeConexion,
            procesoQueFrena,
            enVuelo,
            "la transacción en vuelo",
            "ha leído la fecha de la clave sin ver lo que la otra estaba a punto de confirmar");

    /// <summary>Una línea del caso.</summary>
    /// <param name="Articulo">Cuál de los artículos del caso, desde cero.</param>
    /// <param name="Cantidad">La cantidad, con signo; el factor es siempre uno.</param>
    /// <param name="Coste">El coste por unidad base, o nada.</param>
    private sealed record Linea(int Articulo, decimal Cantidad, decimal? Coste);

    /// <summary>Los maestros de un caso.</summary>
    /// <param name="Cliente">Cliente autenticado en la empresa.</param>
    /// <param name="EmpresaId">La empresa.</param>
    /// <param name="AlmacenId">El almacén.</param>
    /// <param name="UbicacionId">Su única ubicación.</param>
    /// <param name="Articulos">Los artículos, con su unidad base.</param>
    /// <param name="EjercicioId">El ejercicio del año pasado.</param>
    /// <param name="Serie">La serie de ese ejercicio.</param>
    private sealed record ElCaso(
        HttpClient Cliente,
        Guid EmpresaId,
        Guid AlmacenId,
        Guid UbicacionId,
        IReadOnlyList<(Guid ArticuloId, Guid UnidadId)> Articulos,
        Guid EjercicioId,
        SerieDto Serie);
}
