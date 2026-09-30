using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La R3 en su forma entera: el saldo es la suma del libro, y todo lo demás es una copia que se
/// puede tirar (ítem 2.7, ADR-0044).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tres copias, y cada una con su caso.</b> La fila viva —una por clave— se mueve en la misma
/// sentencia y la misma transacción que escriben el libro. La instantánea mensual es el saldo al
/// acabar cada mes, y se puede borrar y recalcular sin que cambie un número. Y el cuadre las
/// recorre las dos contra el libro, y dice cuántas ha comparado además de lo que no cuadra.
/// </para>
/// <para>
/// <b>Las confirmaciones van por el caso de uso de verdad</b>, con sus adaptadores y su
/// transacción, y no escribiendo el libro a mano: lo que se afirma es que la copia se mueve sola
/// cuando el sistema anota, y un <c>INSERT</c> en el libro no es algo que el sistema haga. Lo único
/// que se escribe por debajo del sistema son las <b>copias estropeadas</b> del caso del cuadre, y
/// van dentro de una transacción que se deshace.
/// </para>
/// <para>
/// <b>Semillas: el fichero entero es del 440 al 459.</b> Las empresas, del 440 al 449; los maestros
/// de instalación, del 450 al 459. El caso de las dos empresas se lleva la 447 y la 448, y la 457 y
/// la 458; el de las dos salidas del ítem 2.8, la 449 y la 459.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasExistenciasSonLaSumaDelLibroTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
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

    [Fact]
    public async Task Dos_lineas_de_la_misma_clave_y_otro_documento_dejan_una_sola_fila_viva_con_la_suma()
    {
        ElAlmacenDelCaso caso = await UnAlmacenAsync(440, "EXI-A", 450);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        // DOS LÍNEAS DE LA MISMA CLAVE EN EL MISMO DOCUMENTO, que la sentencia agrupa antes de
        // tocar la fila: sin agrupar, el mismo INSERT querría actualizar dos veces la misma fila y
        // PostgreSQL lo rechaza. Y un factor que no es uno, para que se vea que lo que se suma es
        // la cantidad en unidad base y no la que se tecleó.
        await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 4m, 2m), new Linea(0, -1m, 2m));

        // Y UN SEGUNDO DOCUMENTO, que es el que encuentra la fila ya creada: es el camino del
        // ON CONFLICT, el que suma a lo que había en vez de ponerlo.
        await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 3m, 1.5m));

        IReadOnlyList<ApunteDelLibro> libro = await LasExistencias.LibroAsync(postgres, caso.EmpresaId);
        libro.Count.ShouldBe(3, "tres líneas, tres filas del libro: la existencia no se come ninguna");

        Existencia viva = (await LasExistencias.VivasAsync(postgres, caso.EmpresaId)).ShouldHaveSingleItem(
            "una sola fila viva por clave, aunque la muevan tres líneas y dos documentos");

        viva.Fisico.ShouldBe(
            libro.Sum(apunte => apunte.Cantidad),
            "el saldo es la suma del libro: 8 - 2 + 4,5");

        viva.Fisico.ShouldBe(10.5m);
        viva.Reservado.ShouldBe(0m, "nada reserva todavía: las reservas llegan con el ítem 2.13");
        viva.Disponible.ShouldBe(viva.Fisico);
        viva.LoteId.ShouldBeNull("el libro todavía no lleva lote: lo trae el ítem 2.9");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(1, "ha comparado la única clave que hay");
        cuadre.InstantaneasComparadas.ShouldBe(0, "sin corte no hay instantáneas que comparar");
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task Lo_disponible_es_lo_fisico_menos_lo_reservado_y_solo_lo_escribe_el_motor()
    {
        ElAlmacenDelCaso caso = await UnAlmacenAsync(441, "EXI-B", 451);

        await using (ElModuloDeInventario modulo = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 5m));
        }

        // LA RESERVA SE ESCRIBE A MANO porque todavía no hay quien reserve —el ítem 2.13—, y con
        // ella a cero la resta no se ve: disponible y físico dirían lo mismo tanto con la fórmula
        // como sin ella. Dentro de una transacción que se deshace, para no dejar en la base una
        // reserva que nada del sistema ha hecho.
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();
        await using NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync();

        await using (NpgsqlCommand reservar = new(
            "UPDATE inventario.existencias SET reservado = 2 WHERE empresa_id = @empresa " +
            "RETURNING fisico, reservado, disponible",
            conexion,
            transaccion))
        {
            reservar.Parameters.AddWithValue("empresa", caso.EmpresaId);

            await using NpgsqlDataReader lector = await reservar.ExecuteReaderAsync();

            (await lector.ReadAsync()).ShouldBeTrue("la clave tiene su fila viva");
            lector.GetDecimal(0).ShouldBe(5m);
            lector.GetDecimal(1).ShouldBe(2m);
            lector.GetDecimal(2).ShouldBe(3m, "disponible es físico menos reservado, y lo calcula el motor");
        }

        // Y NADIE MÁS LO ESCRIBE: es una columna generada, y el motor rechaza un valor a mano con su
        // propio código. Sin eso, disponible sería una tercera cifra que alguien tendría que
        // acordarse de mover a la vez que las otras dos.
        await using NpgsqlCommand escribirlo = new(
            "UPDATE inventario.existencias SET disponible = 1 WHERE empresa_id = @empresa",
            conexion,
            transaccion);

        escribirlo.Parameters.AddWithValue("empresa", caso.EmpresaId);

        PostgresException rechazo =
            await Should.ThrowAsync<PostgresException>(() => escribirlo.ExecuteNonQueryAsync());

        rechazo.SqlState.ShouldBe(SoloLaEscribeElMotor, rechazo.MessageText);

        await transaccion.RollbackAsync();
    }

    [Fact]
    public async Task Dos_confirmaciones_a_la_vez_sobre_la_misma_clave_suman_las_dos()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(442);

        // DOS SERIES DEL MISMO EJERCICIO, y es lo que hace que el caso ejerza la fila viva y no
        // otra cosa: con una sola serie, las dos confirmaciones se pararían antes en el contador
        // de la serie, y la segunda llegaría a la existencia con la primera ya confirmada.
        EjercicioDto esteAnio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year);
        SerieDto primera = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-C1");
        SerieDto segunda = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-C2");

        ElAlmacenDelCaso caso = await LosMaestrosDeAsync(cliente, empresa.Id, "EXI-C", 452, primera);

        await using ElModuloDeInventario unos = new(postgres, caso.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, caso.EmpresaId);

        // LA FILA YA EXISTE, y así las dos van por la actualización. Es donde se esconde el fallo
        // caro: leer la fila y escribir lo leído más lo nuevo pierde en silencio lo que la otra
        // sumó, mientras que sin fila previa el mismo fallo revienta con un nulo y se ve solo.
        await ConfirmarAsync(unos, caso, Hoy, new Linea(0, 10m));

        Guid delPrimero = await AbrirAsync(unos, caso, primera.Id, Hoy, [new Linea(0, 3m)]);
        Guid delSegundo = await AbrirAsync(otros, caso, segunda.Id, Hoy, [new Linea(0, 4m)]);

        // TRANSACCIÓN 1: confirma, mueve la fila viva y NO suelta.
        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await unos.ConfirmarYQuedarseDentroAsync(delPrimero);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            // TRANSACCIÓN 2: la otra serie, la misma clave. Se para en la fila viva, que es lo
            // único que las dos comparten, y el caso no suelta a la primera hasta que el motor dice
            // que la segunda ya está esperando.
            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(delSegundo);

            await EsperarAQueLaFreneAsync(unos.ProcesoDeLaBase, laOtra);

            await enVuelo.CommitAsync();

            Resultado<AjusteDto> segundaConfirmacion = await laOtra.WaitAsync(TimeSpan.FromSeconds(30));

            segundaConfirmacion.EsCorrecto.ShouldBeTrue($"«{segundaConfirmacion.Error?.Codigo}»");
        }

        (await LasExistencias.VivasAsync(postgres, caso.EmpresaId))
            .ShouldHaveSingleItem()
            .Fisico.ShouldBe(
                17m,
                "10 de antes, más 3 de la primera, más 4 de la segunda: la segunda suma sobre lo que " +
                "la primera dejó al confirmarse, no sobre lo que había cuando empezó");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(1);
        cuadre.Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// Dos salidas a la vez que caben una a una y no juntas: una confirma, la otra choca con la
    /// restricción del motor, y el físico no baja de cero en ningún momento (ítem 2.8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el caso por el que la guarda está en el motor y no en el dominio</b> (ADR-0046 §4). Con
    /// una comprobación previa, las dos leerían 10, las dos verían que 6 caben y las dos
    /// escribirían: el físico acabaría en −2. El <c>CHECK</c> se evalúa sobre la fila ya bloqueada y
    /// con la cantidad ya sumada, así que la segunda, al despertar, suma sobre el 4 que dejó la
    /// primera y choca.
    /// </para>
    /// <para>
    /// <b>Se afirma el nombre de la restricción y no solo el código</b>, por lo mismo que en la
    /// carrera del inverso: es el nombre que el borde traduce a <c>422</c>
    /// <c>stock-insuficiente</c>, y el carril funcional afirma el mismo, escrito a mano.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Dos_salidas_a_la_vez_que_caben_una_a_una_y_no_juntas_dejan_pasar_solo_una()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(449);

        // DOS SERIES, como en el caso de las dos entradas: con una sola, la segunda se pararía en
        // el contador y llegaría a la existencia con la primera ya confirmada.
        EjercicioDto esteAnio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year);
        SerieDto primera = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-J1");
        SerieDto segunda = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-J2");

        ElAlmacenDelCaso caso = await LosMaestrosDeAsync(cliente, empresa.Id, "EXI-J", 459, primera);

        await using ElModuloDeInventario unos = new(postgres, caso.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(unos, caso, Hoy, new Linea(0, 10m));

        Guid delPrimero = await AbrirAsync(unos, caso, primera.Id, Hoy, [new Linea(0, -6m)]);
        Guid delSegundo = await AbrirAsync(otros, caso, segunda.Id, Hoy, [new Linea(0, -6m)]);

        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await unos.ConfirmarYQuedarseDentroAsync(delPrimero);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(delSegundo);

            await EsperarAQueLaFreneAsync(unos.ProcesoDeLaBase, laOtra);

            await enVuelo.CommitAsync();

            PostgresException rechazo = await Should.ThrowAsync<PostgresException>(
                () => laOtra.WaitAsync(TimeSpan.FromSeconds(30)));

            rechazo.SqlState.ShouldBe("23514", rechazo.MessageText);
            rechazo.ConstraintName.ShouldBe(
                "ck_existencias_fisico_no_negativo",
                "es el nombre que el borde traduce a 422: con otro, la segunda salida saldría 500");
        }

        (await LasExistencias.VivasAsync(postgres, caso.EmpresaId))
            .ShouldHaveSingleItem()
            .Fisico.ShouldBe(4m, "10 menos los 6 de la primera; la segunda no ha movido nada");

        IReadOnlyList<ApunteDelLibro> libro = await LasExistencias.LibroAsync(postgres, caso.EmpresaId);

        libro.Select(apunte => apunte.Cantidad).ShouldBe(
            [10m, -6m],
            ignoreOrder: true,
            "la entrada y la primera salida: de la segunda no queda ni una fila");

        await using (InventarioDbContext inventario = postgres.AbrirInventario(caso.EmpresaId))
        {
            Ajuste rechazado = await inventario.Ajustes.SingleAsync(fila => fila.Id == delSegundo);

            rechazado.Estado.ShouldBe(EstadoDeAjuste.Borrador, "su transacción entera se deshizo");
            rechazado.Numero.ShouldBeNull();
        }

        (await cliente.GetFromJsonAsync<SerieDto>($"{LosMaestrosPorLaApi.Series}/{segunda.Id}"))!
            .Contador.ShouldBe(
                0,
                "el número que tomó volvió a la serie con el rollback: si se hubiera quedado " +
                "gastado, la R5 tendría un hueco");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(1);
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task Una_fecha_futura_no_se_confirma_y_no_deja_nada_en_el_libro_ni_en_la_existencia()
    {
        DateOnly manana = Hoy.AddDays(1);

        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(443);

        // EL EJERCICIO ES EL DE MAÑANA, abierto, y la serie cuelga de él: todo lo demás que mira
        // la confirmación diría que sí. Lo único que el documento tiene mal es la fecha.
        EjercicioDto ejercicio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, manana.Year);
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, ejercicio.Id, "EXI-D");

        ElAlmacenDelCaso caso = await LosMaestrosDeAsync(cliente, empresa.Id, "EXI-D", 453, serie);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        Guid ajusteId = await AbrirAsync(modulo, caso, serie.Id, manana, [new Linea(0, 5m)]);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        confirmacion.EsCorrecto.ShouldBeFalse(
            "una fila del libro con fecha de mañana haría que la existencia contara hoy algo que " +
            "todavía no ha pasado, y el saldo a hoy dejaría de ser la suma del libro hasta hoy");

        confirmacion.Error!.Codigo.ShouldBe("ajuste-con-fecha-futura");

        await using InventarioDbContext inventario = postgres.AbrirInventario(caso.EmpresaId);

        Ajuste comoQuedo = await inventario.Ajustes.SingleAsync(fila => fila.Id == ajusteId);

        comoQuedo.Estado.ShouldBe(EstadoDeAjuste.Borrador, "se confirma el día que llegue su fecha");
        comoQuedo.Numero.ShouldBeNull();

        (await inventario.Movimientos.AnyAsync()).ShouldBeFalse("ni una fila del libro");
        (await inventario.Existencias.AnyAsync()).ShouldBeFalse("ni la fila viva de la clave");

        SerieDto? despues = await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serie.Id}");

        despues!.Contador.ShouldBe(
            0,
            "la guarda va antes del numerador: un documento que no se confirma no gasta número");
    }

    [Fact]
    public async Task Borrar_las_instantaneas_y_recalcularlas_no_cambia_ningun_numero()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(444, "EXI-E", 454, ubicaciones: 3, conElAnioPasado: true);

        int pasado = Hoy.Year - 1;
        DateOnly corte = LasExistencias.MesDe(Hoy);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 3, 10), new Linea(0, 5m));

        Guid delVerano = await ConfirmarAsync(
            modulo, caso, new DateOnly(pasado, 7, 20), new Linea(0, -2m), new Linea(1, 7m));

        // EL PRIMER RECÁLCULO pone el corte en este mes y saca las instantáneas del libro.
        int repuestas = await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, corte);

        IReadOnlyList<FotoDelMes> debidas = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), corte);

        repuestas.ShouldBe(debidas.Count);
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(debidas);

        // DESPUÉS DEL CORTE, TRES CONFIRMACIONES CON FECHA DE UN MES ANTERIOR, UNA DE HOY Y UNA
        // ANULACIÓN. Es la decisión (a) del ítem: quien anota el libro suma también en las
        // instantáneas de ese mes y de todos los que vienen detrás hasta el corte. Una clave nueva
        // —que estrena meses—, una fila en los meses que ya tiene cada una de las otras dos, la de
        // hoy, que suma solo en el mes del corte, y el inverso, también de hoy.
        //
        // NINGUNA VA POR DETRÁS DE SU CLAVE (addendum del 2.8, ADR-0047). Dos de las tres de antes
        // iban por delante del primer mes de su clave, y ya no se confirman: el artículo se había
        // movido después en el mismo almacén. Las fechas van ahora en el orden en que se confirman,
        // y lo que el caso afirma de las instantáneas es lo mismo.
        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 9, 1), new Linea(2, 4m));
        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 11, 5), new Linea(1, 1m));
        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 12, 2), new Linea(0, -1m));
        await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 3m));

        Resultado<AnulacionDto> anulacion =
            await modulo.AnularAsync(delVerano, "Recuento repetido por error");

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»");

        IReadOnlyList<ApunteDelLibro> libro = await LasExistencias.LibroAsync(postgres, caso.EmpresaId);

        IReadOnlyList<FotoDelMes> antes = await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId);

        antes.ShouldBe(
            LasExistencias.DebidasEnCSharp(libro, corte),
            "las instantáneas que dejó la sentencia del libro son las que el libro dice");

        antes.ShouldNotBeEmpty();

        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(libro);
        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, caso.EmpresaId);

        vivas.Count.ShouldBe(saldos.Count, "una fila viva por clave del libro, y ninguna más");

        foreach (Existencia viva in vivas)
        {
            viva.Fisico.ShouldBe(saldos[LasExistencias.ClaveDe(viva)]);
        }

        // LA BORRA, a la vista y por debajo del sistema, para que el caso no dependa de que el
        // recálculo borre: lo que se afirma es que se puede partir de nada.
        int borradas = await BorrarLasInstantaneasAsync(caso.EmpresaId);

        borradas.ShouldBe(antes.Count);
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBeEmpty();

        // LA RECALCULA...
        (await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, corte)).ShouldBe(antes.Count);

        // ...Y COMPARA: ni un número distinto.
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            antes,
            "la instantánea es una optimización: tirada y vuelta a sacar del libro, dice lo mismo");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(3, "las tres claves que se movieron");
        cuadre.InstantaneasComparadas.ShouldBe(antes.Count, "cada clave, cada mes hasta el corte");
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_cuadre_encuentra_cada_copia_que_no_dice_lo_que_el_libro()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(445, "EXI-F", 455, ubicaciones: 2, conElAnioPasado: true);

        DateOnly junio = new(Hoy.Year - 1, 6, 1);
        DateOnly corte = LasExistencias.MesDe(Hoy);

        await using (ElModuloDeInventario modulo = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(modulo, caso, junio.AddDays(9), new Linea(0, 5m), new Linea(1, 3m));
            await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 1m));
        }

        await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, corte);

        int debidas = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), corte).Count;

        CuadreDeLasExistencias limpio = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        limpio.ExistenciasComparadas.ShouldBe(2);
        limpio.InstantaneasComparadas.ShouldBe(debidas);
        limpio.Descuadres.ShouldBeEmpty("recién recalculado, todo cuadra: lo de abajo lo pone el caso");

        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, caso.EmpresaId);
        Guid laPrimera = vivas.Single(viva => viva.UbicacionId == caso.Ubicaciones[0]).Id;
        Guid laSegunda = vivas.Single(viva => viva.UbicacionId == caso.Ubicaciones[1]).Id;
        var unaUbicacionSinLibro = Guid.CreateVersion7();

        // SIETE COPIAS ESTROPEADAS, una de cada manera en que una copia puede mentir, y todas en una
        // transacción que se deshace: el cuadre corre dentro de ella, con el mismo contexto.
        await using InventarioDbContext contexto = postgres.AbrirInventario(caso.EmpresaId);
        await using IDbContextTransaction estropeando = await contexto.Database.BeginTransactionAsync();

        // Una fila viva que dice de más.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET fisico = fisico + 1 WHERE id = {0}", laSegunda))
            .ShouldBe(1);

        // Una instantánea que dice otra cosa.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.instantaneas_mensuales SET fisico = fisico + 10 " +
            "WHERE existencia_id = {0} AND mes = {1}",
            laPrimera,
            junio)).ShouldBe(1);

        // Una instantánea que falta.
        (await contexto.Database.ExecuteSqlRawAsync(
            "DELETE FROM inventario.instantaneas_mensuales WHERE existencia_id = {0} AND mes = {1}",
            laSegunda,
            junio.AddMonths(1))).ShouldBe(1);

        // Una fila viva de una clave que el libro no ha movido nunca.
        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.existencias " +
            "(id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id, fisico, reservado) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, NULL, 2, 0)",
            Guid.CreateVersion7(),
            caso.EmpresaId,
            caso.ArticuloId,
            caso.AlmacenId,
            unaUbicacionSinLibro)).ShouldBe(1);

        // Una instantánea que sobra: la de después del corte.
        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.instantaneas_mensuales (existencia_id, mes, empresa_id, fisico) " +
            "VALUES ({0}, {1}, {2}, 6)",
            laPrimera,
            corte.AddMonths(1),
            caso.EmpresaId)).ShouldBe(1);

        // Una clave con dos filas vivas que, juntas, dicen lo que el libro. La suma no la delata:
        // solo la cuenta de filas. Para plantarla hay que quitar el índice único, que es justo lo
        // que la cuenta vigila por si un día deja de distinguir el lote nulo; el índice vuelve con
        // el rollback.
        await contexto.Database.ExecuteSqlRawAsync("DROP INDEX inventario.ix_existencias_una_por_clave");

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET fisico = fisico - 2 WHERE id = {0}", laPrimera))
            .ShouldBe(1);

        var laCopia = Guid.CreateVersion7();

        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.existencias " +
            "(id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id, fisico, reservado) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, NULL, 2, 0)",
            laCopia,
            caso.EmpresaId,
            caso.ArticuloId,
            caso.AlmacenId,
            caso.Ubicaciones[0])).ShouldBe(1);

        // Y una instantánea de más, a cero, para una clave y un mes que ya tenían la suya: tampoco
        // cambia la suma.
        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.instantaneas_mensuales (existencia_id, mes, empresa_id, fisico) " +
            "VALUES ({0}, {1}, {2}, 0)",
            laCopia,
            junio.AddMonths(1),
            caso.EmpresaId)).ShouldBe(1);

        CuadreDeLasExistencias sucio = await LasExistencias.CuadrarEnAsync(contexto, caso.EmpresaId);

        sucio.ExistenciasComparadas.ShouldBe(3, "las dos de antes y la que no tiene libro");
        sucio.InstantaneasComparadas.ShouldBe(debidas + 1, "las debidas y la que sobra");

        sucio.Descuadres.ShouldAllBe(descuadre =>
            descuadre.ArticuloId == caso.ArticuloId
            && descuadre.AlmacenId == caso.AlmacenId
            && descuadre.LoteId == null);

        sucio.Descuadres
            .Select(descuadre => new Hallado(
                descuadre.Que,
                descuadre.UbicacionId,
                descuadre.Mes,
                descuadre.Esperado,
                descuadre.Guardado,
                descuadre.Filas))
            .ShouldBe(
                [
                    new Hallado("existencia", caso.Ubicaciones[1], null, 3m, 4m, 1),
                    new Hallado("existencia", unaUbicacionSinLibro, null, 0m, 2m, 1),
                    new Hallado("instantanea", caso.Ubicaciones[0], junio, 5m, 15m, 1),
                    new Hallado("instantanea", caso.Ubicaciones[1], junio.AddMonths(1), 3m, 0m, 0),
                    new Hallado("instantanea", caso.Ubicaciones[0], corte.AddMonths(1), 0m, 6m, 1),
                    new Hallado("existencia", caso.Ubicaciones[0], null, 6m, 6m, 2),
                    new Hallado("instantanea", caso.Ubicaciones[0], junio.AddMonths(1), 5m, 5m, 2),
                ],
                ignoreOrder: true,
                customMessage: "las siete, y ninguna más: cada una dice qué esperaba y qué encontró");

        await estropeando.RollbackAsync();

        // Y DESHECHO, OTRA VEZ LIMPIO: lo que el cuadre encontró era lo que el caso puso.
        CuadreDeLasExistencias otraVez = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        otraVez.ExistenciasComparadas.ShouldBe(2);
        otraVez.InstantaneasComparadas.ShouldBe(debidas);
        otraVez.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_recalculo_espera_a_la_confirmacion_de_una_clave_nueva_que_ya_estaba_dentro()
    {
        ElAlmacenDelCaso caso = await UnAlmacenAsync(446, "EXI-G", 456);

        DateOnly esteMes = LasExistencias.MesDe(Hoy);

        // EL CORTE EMPIEZA EN EL MES PASADO, así que la confirmación de hoy no escribe ninguna
        // instantánea: su mes cae después del corte. Se las tiene que dar el recálculo que lo
        // avanza a este mes, y el recálculo no ve una fila viva que todavía no está confirmada.
        (await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, esteMes.AddMonths(-1)))
            .ShouldBe(0, "no hay libro todavía: solo queda puesto el corte");

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        Guid ajusteId = await AbrirAsync(modulo, caso, caso.Serie.Id, Hoy, [new Linea(0, 5m)]);

        // TRANSACCIÓN 1: confirma una clave NUEVA —su fila viva nace aquí— y no suelta.
        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await modulo.ConfirmarYQuedarseDentroAsync(ajusteId);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            // TRANSACCIÓN 2: el recálculo avanza el corte. SIN SU CERROJO NO ESPERA A NADA: el
            // corte no lo tiene cogido nadie, no hay instantáneas que borrar y la fila viva no se
            // ve, así que acaba enseguida sin darle a la clave nueva la instantánea de este mes.
            Task<int> recalculo = LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, esteMes);

            await EsperarAQueLaFreneAsync(modulo.ProcesoDeLaBase, recalculo);

            await enVuelo.CommitAsync();

            (await recalculo.WaitAsync(TimeSpan.FromSeconds(30))).ShouldBe(
                1,
                "esperó al COMMIT de la confirmación, y con él ya a la vista le dio su instantánea");
        }

        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            [new FotoDelMes(caso.Clave(0), esteMes, 5m)]);

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(1);
        cuadre.InstantaneasComparadas.ShouldBe(1);
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task Las_existencias_y_las_instantaneas_de_dos_empresas_no_se_mezclan()
    {
        // LAS SENTENCIAS DE LA PROYECCIÓN SON SQL CRUDO, y el filtro global de empresa no las
        // alcanza: cada una compara la empresa ella misma. Este caso es el que se pone rojo si una
        // de esas comparaciones desaparece, y por eso las dos empresas tienen de todo —libro, filas
        // vivas, corte e instantáneas—: con una de ellas vacía, casi ninguna fuga se vería.
        ElAlmacenDelCaso deA = await UnAlmacenAsync(447, "EXI-H", 457);
        ElAlmacenDelCaso deB = await UnAlmacenAsync(448, "EXI-I", 458);

        DateOnly esteMes = LasExistencias.MesDe(Hoy);

        await using (ElModuloDeInventario enB = new(postgres, deB.EmpresaId))
        {
            await ConfirmarAsync(enB, deB, Hoy, new Linea(0, 7m));
        }

        await LasExistencias.RecalcularAsync(postgres, deB.EmpresaId, esteMes);

        await using (ElModuloDeInventario enA = new(postgres, deA.EmpresaId))
        {
            await ConfirmarAsync(enA, deA, Hoy, new Linea(0, 5m));
            await LasExistencias.RecalcularAsync(postgres, deA.EmpresaId, esteMes);

            // CON LOS DOS CORTES PUESTOS, la sentencia del libro tiene que leer el de su empresa:
            // si leyera los dos, su subconsulta devolvería dos filas y la confirmación reventaría.
            await ConfirmarAsync(enA, deA, Hoy, new Linea(0, 2m));
        }

        // Y EL RECÁLCULO DE A tira y repone las de A, no las de B.
        (await LasExistencias.RecalcularAsync(postgres, deA.EmpresaId, esteMes)).ShouldBe(1);

        (await LasExistencias.InstantaneasAsync(postgres, deB.EmpresaId)).ShouldBe(
            [new FotoDelMes(deB.Clave(0), esteMes, 7m)],
            "la instantánea de B sigue ahí después de que A recalculara las suyas");

        (await LasExistencias.InstantaneasAsync(postgres, deA.EmpresaId)).ShouldBe(
            [new FotoDelMes(deA.Clave(0), esteMes, 7m)]);

        (await LasExistencias.VivasAsync(postgres, deA.EmpresaId))
            .ShouldHaveSingleItem()
            .Fisico.ShouldBe(7m);

        // Y CADA CUADRE COMPARA LO SUYO, contado: un cuadre que se llevara las claves de la otra
        // empresa compararía dos y no una, y además las daría por descuadradas.
        foreach (Guid empresaId in new[] { deA.EmpresaId, deB.EmpresaId })
        {
            CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, empresaId);

            cuadre.ExistenciasComparadas.ShouldBe(1);
            cuadre.InstantaneasComparadas.ShouldBe(1);
            cuadre.Descuadres.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Anotar_el_libro_sin_transaccion_sin_inquilino_o_con_filas_de_otra_empresa_revienta()
    {
        var empresaId = Guid.CreateVersion7();
        IReadOnlyList<MovimientoStock> movimientos = UnasFilasDelLibro(empresaId);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        // SIN TRANSACCIÓN, la sentencia de la proyección se confirmaría sola, y el libro —que se
        // guarda después, en el SaveChanges— podría no llegar a escribirse nunca.
        RepositorioDeAjustes suyo = new(contexto, new InquilinoFijo(empresaId));

        InvalidOperationException sinTransaccion = await Should.ThrowAsync<InvalidOperationException>(
            () => suyo.AnotarEnElLibroAsync(movimientos, CancellationToken.None));

        sinTransaccion.Message.ShouldContain("No hay transacción abierta");

        // CON FILAS DE OTRA EMPRESA, la sentencia sumaría con la empresa del inquilino lo que el
        // libro apunta en otra.
        await using IDbContextTransaction transaccion = await contexto.Database.BeginTransactionAsync();

        // SIN INQUILINO, la sentencia no sabría en qué empresa sumar. Va dentro de la transacción
        // para que la de la transacción no salte antes que la suya. Y se afirma el mensaje, no solo
        // el tipo: si el repositorio sumara en una empresa vacía, la comprobación de la otra
        // empresa lo pararía igual, con otras palabras, y el caso seguiría en verde sin la suya.
        RepositorioDeAjustes deNadie = new(contexto, new InquilinoFijo(null));

        InvalidOperationException sinInquilino = await Should.ThrowAsync<InvalidOperationException>(
            () => deNadie.AnotarEnElLibroAsync(movimientos, CancellationToken.None));

        sinInquilino.Message.ShouldContain("sin inquilino");

        RepositorioDeAjustes deOtra = new(contexto, new InquilinoFijo(Guid.CreateVersion7()));

        InvalidOperationException ajenas = await Should.ThrowAsync<InvalidOperationException>(
            () => deOtra.AnotarEnElLibroAsync(movimientos, CancellationToken.None));

        ajenas.Message.ShouldContain("de otra empresa");

        await transaccion.RollbackAsync();
    }

    /// <summary>El código con el que PostgreSQL rechaza escribir una columna generada.</summary>
    private const string SoloLaEscribeElMotor = "428C9";

    /// <summary>La fecha de hoy, en el mismo calendario que usa el caso de uso.</summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Unas filas del libro hechas por el dominio, sin guardar nada.</summary>
    /// <param name="empresaId">La empresa del documento.</param>
    /// <returns>Lo que la confirmación devolvería para anotar.</returns>
    private static IReadOnlyList<MovimientoStock> UnasFilasDelLibro(Guid empresaId)
    {
        DateTimeOffset momento = DateTimeOffset.UtcNow;
        var almacenId = Guid.CreateVersion7();

        var ajuste = Ajuste.Abrir(
            empresaId, Guid.CreateVersion7(), almacenId, Hoy, "Recuento del ítem 2.7", "EUR", momento);

        ajuste.AnadirLinea(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            3m,
            Guid.CreateVersion7(),
            1m,
            1.50m,
            momento);

        return ajuste.Confirmar(
            1,
            new AjusteConfirmado(ajuste.Id, empresaId, almacenId, Hoy, 1),
            ElLibro.ValoracionDesdeCero(ajuste),
            LotesYSeriesResueltos.Ninguno,
            momento);
    }

    /// <summary>Tira a mano las instantáneas de la empresa, por debajo del sistema.</summary>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>Cuántas ha tirado.</returns>
    private async Task<int> BorrarLasInstantaneasAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand borrar = new(
            "DELETE FROM inventario.instantaneas_mensuales WHERE empresa_id = @empresa", conexion);

        borrar.Parameters.AddWithValue("empresa", empresaId);

        return await borrar.ExecuteNonQueryAsync();
    }

    /// <summary>Abre y confirma un ajuste con la serie del año de su fecha.</summary>
    /// <param name="modulo">El módulo cableado a mano de la empresa.</param>
    /// <param name="caso">Los maestros del caso.</param>
    /// <param name="fecha">La fecha de operación.</param>
    /// <param name="lineas">Las líneas, sobre las ubicaciones del caso.</param>
    /// <returns>El ajuste confirmado.</returns>
    private static async Task<Guid> ConfirmarAsync(
        ElModuloDeInventario modulo, ElAlmacenDelCaso caso, DateOnly fecha, params Linea[] lineas)
    {
        Guid ajusteId = await AbrirAsync(modulo, caso, caso.SerieDe(fecha).Id, fecha, lineas);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

        return ajusteId;
    }

    /// <summary>Abre un ajuste en borrador.</summary>
    /// <param name="modulo">El módulo cableado a mano de la empresa.</param>
    /// <param name="caso">Los maestros del caso.</param>
    /// <param name="serieId">La serie que lo numerará.</param>
    /// <param name="fecha">La fecha de operación.</param>
    /// <param name="lineas">Las líneas, sobre las ubicaciones del caso.</param>
    /// <returns>El borrador.</returns>
    private static async Task<Guid> AbrirAsync(
        ElModuloDeInventario modulo,
        ElAlmacenDelCaso caso,
        Guid serieId,
        DateOnly fecha,
        IReadOnlyList<Linea> lineas)
    {
        AbrirAjusteDto peticion = new(
            serieId,
            caso.AlmacenId,
            fecha,
            "Recuento del ítem 2.7",
            [.. lineas.Select(linea => new LineaDeAjusteDto(
                caso.Ubicaciones[linea.Ubicacion],
                caso.ArticuloId,
                linea.Cantidad,
                caso.UnidadId,
                linea.Factor,
                linea.Cantidad > 0m ? 1.50m : null))]);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    /// <summary>
    /// Una empresa con su almacén, sus ubicaciones, un artículo, el ejercicio de este año con su
    /// serie y, si se pide, el del año pasado con la suya.
    /// </summary>
    /// <param name="semilla">La empresa del caso.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <param name="ubicaciones">Cuántas ubicaciones, que son cuántas claves puede mover.</param>
    /// <param name="conElAnioPasado">Si abre también el ejercicio del año pasado.</param>
    /// <returns>Los maestros del caso.</returns>
    private async Task<ElAlmacenDelCaso> UnAlmacenAsync(
        int semilla,
        string codigo,
        int semillaDeInstalacion,
        int ubicaciones = 1,
        bool conElAnioPasado = false)
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(semilla);

        EjercicioDto esteAnio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year);
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, codigo);

        SerieDto? delAnioPasado = null;

        if (conElAnioPasado)
        {
            EjercicioDto pasado = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year - 1);
            delAnioPasado = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, pasado.Id, codigo + "-P");
        }

        return await LosMaestrosDeAsync(
            cliente, empresa.Id, codigo, semillaDeInstalacion, serie, delAnioPasado, ubicaciones);
    }

    /// <summary>El almacén, las ubicaciones y el artículo, para una empresa y unas series que ya hay.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <param name="serie">La serie de este año.</param>
    /// <param name="delAnioPasado">La del año pasado, si la hay.</param>
    /// <param name="ubicaciones">Cuántas ubicaciones.</param>
    /// <returns>Los maestros del caso.</returns>
    private static async Task<ElAlmacenDelCaso> LosMaestrosDeAsync(
        HttpClient cliente,
        Guid empresaId,
        string codigo,
        int semillaDeInstalacion,
        SerieDto serie,
        SerieDto? delAnioPasado = null,
        int ubicaciones = 1)
    {
        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        List<Guid> huecos = [];

        for (int numero = 1; numero <= ubicaciones; numero++)
        {
            huecos.Add((await LosMaestrosPorLaApi.CrearUbicacionAsync(
                cliente, almacen.Id, $"{codigo}-{numero:00}")).Id);
        }

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semillaDeInstalacion);

        return new ElAlmacenDelCaso(
            empresaId, almacen.Id, huecos, articuloId, unidadId, serie, delAnioPasado);
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
            "ha decidido sin ver lo que la otra estaba a punto de confirmar");

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }

    /// <summary>Una línea de ajuste sobre una de las ubicaciones del caso.</summary>
    /// <param name="Ubicacion">Cuál de las ubicaciones del caso, desde cero.</param>
    /// <param name="Cantidad">La cantidad tecleada, con signo.</param>
    /// <param name="Factor">El factor a unidad base.</param>
    private sealed record Linea(int Ubicacion, decimal Cantidad, decimal Factor = 1m);

    /// <summary>Un descuadre, sin lo que todos comparten.</summary>
    private sealed record Hallado(
        string Que, Guid? UbicacionId, DateOnly? Mes, decimal Esperado, decimal Guardado, long Filas);

    /// <summary>Los maestros de un caso.</summary>
    /// <param name="EmpresaId">La empresa.</param>
    /// <param name="AlmacenId">El almacén.</param>
    /// <param name="Ubicaciones">Sus ubicaciones: una clave por cada una.</param>
    /// <param name="ArticuloId">El artículo.</param>
    /// <param name="UnidadId">Su unidad base.</param>
    /// <param name="Serie">La serie del ejercicio de este año.</param>
    /// <param name="SerieDelAnioPasado">La del año pasado, si el caso la abrió.</param>
    private sealed record ElAlmacenDelCaso(
        Guid EmpresaId,
        Guid AlmacenId,
        IReadOnlyList<Guid> Ubicaciones,
        Guid ArticuloId,
        Guid UnidadId,
        SerieDto Serie,
        SerieDto? SerieDelAnioPasado)
    {
        internal ClaveDeExistencia Clave(int ubicacion) =>
            new(ArticuloId, AlmacenId, Ubicaciones[ubicacion]);

        internal SerieDto SerieDe(DateOnly fecha) =>
            fecha.Year == Hoy.Year
                ? Serie
                : SerieDelAnioPasado ?? throw new InvalidOperationException(
                    "El caso no abrió el ejercicio del año pasado.");
    }
}
