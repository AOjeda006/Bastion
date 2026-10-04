using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>Lo que un ajuste confirmado dejó escrito, con lo que hace falta para ir a buscarlo.</summary>
/// <param name="EmpresaId">Empresa dueña de todo lo de abajo (R8).</param>
/// <param name="AjusteId">El documento.</param>
/// <param name="AlmacenId">Almacén contra el que se ajustó.</param>
/// <param name="FechaDeOperacion">Día al que se imputó, que es su clave de partición.</param>
/// <param name="Movimientos">Las filas del libro que escribió.</param>
internal sealed record UnAjusteConfirmado(
    Guid EmpresaId,
    Guid AjusteId,
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    IReadOnlyList<MovimientoStock> Movimientos);

/// <summary>
/// Las claves de una transferencia sin la API: dos almacenes con un hueco cada uno, un artículo y su
/// unidad.
/// </summary>
/// <param name="AlmacenOrigenId">De dónde sale.</param>
/// <param name="UbicacionOrigenId">De qué hueco.</param>
/// <param name="AlmacenDestinoId">A dónde va.</param>
/// <param name="UbicacionDestinoId">A qué hueco.</param>
/// <param name="ArticuloId">Qué.</param>
/// <param name="UnidadId">En qué unidad, que es la base.</param>
internal sealed record ClavesDeUnaTransferencia(
    Guid AlmacenOrigenId,
    Guid UbicacionOrigenId,
    Guid AlmacenDestinoId,
    Guid UbicacionDestinoId,
    Guid ArticuloId,
    Guid UnidadId)
{
    /// <summary>Todas inventadas: ninguna clave ajena cruza de esquema, y la tabla no las mira.</summary>
    /// <returns>Seis identificadores nuevos.</returns>
    internal static ClavesDeUnaTransferencia Inventadas() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7());
}

/// <summary>
/// La puerta al libro de movimientos para los casos de integración: escribe con el dominio y
/// pregunta a la base en crudo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las filas salen del dominio y del repositorio de verdad</b>, no de un <c>INSERT</c> escrito a
/// mano. Un <c>INSERT</c> montado aquí probaría una fila que el sistema no produce: el libro solo
/// se escribe confirmando un documento, y esa es justamente la mitad de la R13 que hay que poder
/// afirmar después.
/// </para>
/// <para>
/// <b>Los identificadores de fuera —almacén, ubicación, artículo, unidad— son inventados, y eso no
/// afloja nada.</b> Ninguna clave ajena cruza de esquema (§5, regla 4), así que la tabla no los
/// mira; quien los valida es el alta, por sus cuatro puertos, y eso se comprueba donde se decide
/// —<c>UnAlmacenBloqueadoNoAdmiteAjustesTests</c>, contra maestros de verdad dados de alta por la
/// API—. Lo que estos casos miran es el motor: particiones, claves, <c>CHECK</c> y disparadores.
/// </para>
/// <para>
/// <b>Toda sentencia que pueda hacer daño va en una transacción que se deshace.</b> La razón es
/// estrecha y vale la pena decirla: si un día el disparador de solo-añadido no estuviera, el caso
/// que lo comprueba <b>vaciaría el libro de verdad</b> —<c>TRUNCATE</c> no pregunta— y dejaría en
/// rojo a los demás casos de este carril por un motivo que no es el suyo. Con la transacción, un
/// disparador que falta se ve como un caso rojo y nada más.
/// </para>
/// </remarks>
internal static class ElLibro
{
    /// <summary>Nombre de la partición mensual en la que cae una fecha.</summary>
    /// <param name="fecha">La fecha de operación.</param>
    /// <returns>El nombre de la tabla, sin esquema.</returns>
    internal static string ParticionDe(DateOnly fecha) => string.Create(
        CultureInfo.InvariantCulture, $"movimiento_stock_{fecha.Year:D4}_{fecha.Month:D2}");

    /// <summary>Abre un ajuste, le pone líneas, lo confirma y guarda las dos cosas a la vez.</summary>
    /// <remarks>
    /// <para>
    /// Es la transacción que dobla la R12 a sabiendas —el documento y las filas del libro en el
    /// mismo <c>COMMIT</c>—, hecha por el camino de producción: el agregado, el repositorio del
    /// módulo y su unidad de trabajo.
    /// </para>
    /// <para>
    /// <b>Con su transacción abierta aquí</b>, desde el 2.7: anotar el libro mueve también la
    /// existencia, con una sentencia que revienta si no hay transacción. En una petición de verdad
    /// la abre el filtro de idempotencia.
    /// </para>
    /// </remarks>
    /// <param name="postgres">El contenedor con las migraciones puestas.</param>
    /// <param name="fechaDeOperacion">Día al que se imputa, y con él la partición de destino.</param>
    /// <param name="lineas">Cuántas líneas lleva el documento.</param>
    /// <returns>Lo que quedó escrito.</returns>
    internal static async Task<UnAjusteConfirmado> ConfirmarUnAjusteAsync(
        PostgresConTodosLosModulos postgres,
        DateOnly fechaDeOperacion,
        int lineas = 1)
    {
        var empresaId = Guid.CreateVersion7();
        var almacenId = Guid.CreateVersion7();

        // UNA SERIE NUEVA POR DOCUMENTO, y así el número puede ser siempre el primero sin que dos
        // llamadas choquen contra el índice único de `(serie_id, numero)`. La serie es inventada
        // por lo mismo que el almacén —ninguna clave ajena cruza de esquema—, y el número NO sale
        // aquí del mecanismo de numeración a propósito: lo que estos casos miran es el motor
        // debajo del libro. Que el número salga del cerrojo se comprueba donde se decide, en
        // `ElCerrojoDeLaNumeracionTests` y en la confirmación por la API.
        var serieId = Guid.CreateVersion7();
        const long PrimerNumeroDeEsaSerie = 1;

        DateTimeOffset momento = DateTimeOffset.UtcNow;

        var ajuste = Ajuste.Abrir(
            empresaId,
            serieId,
            almacenId,
            fechaDeOperacion,
            "Recuento de prueba del carril",
            "EUR",
            momento);

        for (int numero = 1; numero <= lineas; numero++)
        {
            ajuste.AnadirLinea(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                numero,
                Guid.CreateVersion7(),
                1.5m,
                3.25m,
                momento);
        }

        var evento = new AjusteConfirmado(
            ajuste.Id, empresaId, almacenId, fechaDeOperacion, ajuste.Lineas.Count);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        RepositorioDeAjustes repositorio = new(contexto, new InquilinoFijo(empresaId));

        IReadOnlyList<MovimientoStock> movimientos = await ConfirmarBajoCerrojoAsync(
            repositorio, ajuste, PrimerNumeroDeEsaSerie, evento, momento);

        repositorio.Agregar(ajuste);
        await repositorio.AnotarEnElLibroAsync(movimientos, CancellationToken.None);

        await new UnidadDeTrabajoDeInventario(contexto).ConfirmarAsync(CancellationToken.None);
        await transaccion.CommitAsync();

        return new UnAjusteConfirmado(
            empresaId, ajuste.Id, almacenId, fechaDeOperacion, movimientos);
    }

    /// <summary>
    /// Confirma un documento como lo hace el caso de uso: bloquea la valoración de sus claves, la
    /// lee, valora contra ella y después confirma.
    /// </summary>
    /// <remarks>
    /// <b>Bajo el cerrojo y no desde cero</b>, porque anotar el libro suma sobre la fila de
    /// valoración de cada clave, y la sentencia exige que esa fila se haya bloqueado antes en la
    /// misma transacción (ADR-0046 §2). Valorado desde cero, un inverso restaría un valor que no
    /// es el que dejó su original.
    /// </remarks>
    /// <param name="repositorio">El repositorio de la empresa, con la transacción ya abierta.</param>
    /// <param name="documento">El documento en borrador.</param>
    /// <param name="numero">El correlativo que se le da.</param>
    /// <param name="evento">El hecho que cuenta la confirmación.</param>
    /// <param name="momento">Instante de la confirmación.</param>
    /// <returns>Las filas del libro que el documento deja para anotar.</returns>
    internal static async Task<IReadOnlyList<MovimientoStock>> ConfirmarBajoCerrojoAsync(
        RepositorioDeAjustes repositorio,
        Ajuste documento,
        long numero,
        EventoDeIntegracion evento,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(documento);

        IReadOnlyList<LineaAValorar> lineas = documento.LineasAValorar();

        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos =
            await repositorio.BloquearLasValoracionesAsync(
                [.. lineas.Select(linea => linea.Clave).Distinct()],
                documento.Divisa,
                CancellationToken.None);

        return documento.Confirmar(
            numero,
            evento,
            new ElPrecioMedioPonderado().Valorar(saldos, lineas, documento.Divisa, documento.FechaDeOperacion),
            LotesYSeriesResueltos.Ninguno,
            momento);
    }

    /// <summary>Valora un documento como si ninguna de sus claves se hubiera movido nunca.</summary>
    /// <remarks>
    /// Solo para filas que no llegan a la base: las que se anotan salen de
    /// <see cref="ConfirmarBajoCerrojoAsync"/>, porque la sentencia que suma la valoración rechaza
    /// una clave que no se bloqueó antes.
    /// </remarks>
    /// <param name="documento">El documento en borrador.</param>
    /// <returns>Una valoración por línea, en su orden.</returns>
    internal static IReadOnlyList<LineaValorada> ValoracionDesdeCero(Ajuste documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        IReadOnlyList<LineaAValorar> lineas = documento.LineasAValorar();

        return new ElPrecioMedioPonderado().Valorar(
            lineas
                .Select(linea => linea.Clave)
                .Distinct()
                .ToDictionary(clave => clave, _ => SaldoValorado.Vacio(documento.Divisa)),
            lineas,
            documento.Divisa,
            documento.FechaDeOperacion);
    }

    /// <summary>Anula un ajuste ya confirmado: crea el inverso, lo confirma y guarda las dos cosas.</summary>
    /// <remarks>
    /// <b>Por el camino de producción, igual que la confirmación</b>: el agregado decide, el
    /// repositorio apunta y la unidad de trabajo confirma. El número del inverso se pone a mano
    /// —el segundo de la misma serie inventada— por lo mismo que el del original: lo que estos
    /// casos miran es el motor debajo del libro, y que el correlativo salga del cerrojo se
    /// comprueba donde se decide, en la anulación por la API.
    /// </remarks>
    /// <param name="postgres">El contenedor con las migraciones puestas.</param>
    /// <param name="original">Lo que dejó escrito el documento que se anula.</param>
    /// <param name="fechaDelInverso">Día al que se imputa el inverso, que no es la del original.</param>
    /// <returns>Lo que dejó escrito el inverso.</returns>
    internal static async Task<UnAjusteConfirmado> AnularUnAjusteAsync(
        PostgresConTodosLosModulos postgres,
        UnAjusteConfirmado original,
        DateOnly fechaDelInverso)
    {
        ArgumentNullException.ThrowIfNull(original);

        const long SegundoNumeroDeEsaSerie = 2;

        DateTimeOffset momento = DateTimeOffset.UtcNow;

        await using InventarioDbContext contexto = postgres.AbrirInventario(original.EmpresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        RepositorioDeAjustes repositorio = new(contexto, new InquilinoFijo(original.EmpresaId));

        Ajuste confirmado = await repositorio.ObtenerAsync(original.AjusteId, CancellationToken.None)
            ?? throw new InvalidOperationException("El ajuste que se iba a anular no está.");

        Ajuste inverso = confirmado.CrearInverso(fechaDelInverso, "Anulación del carril", momento);

        var evento = new AjusteConfirmado(
            inverso.Id,
            inverso.EmpresaId,
            inverso.AlmacenId,
            inverso.FechaDeOperacion,
            inverso.Lineas.Count);

        IReadOnlyList<MovimientoStock> movimientos = await ConfirmarBajoCerrojoAsync(
            repositorio, inverso, SegundoNumeroDeEsaSerie, evento, momento);

        confirmado.Anular(inverso, new AjusteAnulado(confirmado.Id, confirmado.EmpresaId));

        repositorio.Agregar(inverso);
        await repositorio.AnotarEnElLibroAsync(movimientos, CancellationToken.None);

        await new UnidadDeTrabajoDeInventario(contexto).ConfirmarAsync(CancellationToken.None);
        await transaccion.CommitAsync();

        return new UnAjusteConfirmado(
            original.EmpresaId, inverso.Id, inverso.AlmacenId, fechaDelInverso, movimientos);
    }

    /// <summary>Mete unidades en el origen de una transferencia sin la API, con un ajuste confirmado.</summary>
    /// <remarks>
    /// Por el camino de producción, como <see cref="ConfirmarUnAjusteAsync"/>, pero con las claves
    /// que se le pasen: los casos que lo usan necesitan que la entrada y la transferencia hablen del
    /// mismo almacén, del mismo hueco y del mismo artículo. Una serie inventada por documento, y su
    /// primer número.
    /// </remarks>
    /// <param name="postgres">El contenedor con las migraciones puestas.</param>
    /// <param name="empresaId">La empresa, inventada o no (R8).</param>
    /// <param name="claves">Dónde entra: el almacén y el hueco del origen.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="coste">El coste unitario.</param>
    /// <param name="fecha">El día de la entrada.</param>
    /// <returns>Una tarea que acaba con la entrada confirmada.</returns>
    internal static async Task EntrarEnElOrigenAsync(
        PostgresConTodosLosModulos postgres,
        Guid empresaId,
        ClavesDeUnaTransferencia claves,
        decimal cantidad,
        decimal coste,
        DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(claves);

        DateTimeOffset momento = DateTimeOffset.UtcNow;

        var ajuste = Ajuste.Abrir(
            empresaId, Guid.CreateVersion7(), claves.AlmacenOrigenId, fecha, "Existencias que transferir", "EUR", momento);

        ajuste.AnadirLinea(
            claves.UbicacionOrigenId, claves.ArticuloId, cantidad, claves.UnidadId, 1m, coste, momento);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        RepositorioDeAjustes repositorio = new(contexto, new InquilinoFijo(empresaId));

        IReadOnlyList<MovimientoStock> movimientos = await ConfirmarBajoCerrojoAsync(
            repositorio,
            ajuste,
            1,
            new AjusteConfirmado(ajuste.Id, empresaId, claves.AlmacenOrigenId, fecha, 1),
            momento);

        repositorio.Agregar(ajuste);
        await repositorio.AnotarEnElLibroAsync(movimientos, CancellationToken.None);

        await new UnidadDeTrabajoDeInventario(contexto).ConfirmarAsync(CancellationToken.None);
        await transaccion.CommitAsync();
    }

    /// <summary>Envía una transferencia de una línea sin la API, como lo hace el caso de uso.</summary>
    /// <remarks>
    /// <para>
    /// <b>Las dos puntas bajo cerrojo, la valoración del origen y el repositorio de verdad</b>: el
    /// agregado decide, <c>MoverAsync</c> guarda el documento y mueve en el orden del ADR-0053 §7.
    /// Lo que no pasa por aquí es lo que el caso de uso pregunta fuera del módulo de Inventario
    /// —el ejercicio, la serie, la marca—, porque las claves son inventadas.
    /// </para>
    /// <para>
    /// <b>El número es el primero de una serie inventada</b>, por lo mismo que en
    /// <see cref="ConfirmarUnAjusteAsync"/>.
    /// </para>
    /// </remarks>
    /// <param name="postgres">El contenedor con las migraciones puestas.</param>
    /// <param name="empresaId">La empresa, inventada o no (R8).</param>
    /// <param name="claves">De dónde sale, a dónde va y qué.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="fecha">El día del envío.</param>
    /// <returns>El identificador de la transferencia, ya enviada.</returns>
    internal static async Task<Guid> EnviarSinLaApiAsync(
        PostgresConTodosLosModulos postgres,
        Guid empresaId,
        ClavesDeUnaTransferencia claves,
        decimal cantidad,
        DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(claves);

        DateTimeOffset momento = DateTimeOffset.UtcNow;

        var transferencia = Transferencia.Abrir(
            empresaId, Guid.CreateVersion7(), claves.AlmacenOrigenId, claves.AlmacenDestinoId, fecha, "EUR", momento);

        transferencia.AnadirLinea(
            claves.UbicacionOrigenId,
            claves.UbicacionDestinoId,
            claves.ArticuloId,
            cantidad,
            claves.UnidadId,
            1m,
            momento);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        RepositorioDeTransferencias repositorio = new(contexto, new InquilinoFijo(empresaId));

        IReadOnlyList<LineaAValorar> lineas = transferencia.LineasAValorarEnElOrigen();

        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos = await repositorio.BloquearLasValoracionesAsync(
            [new(claves.ArticuloId, claves.AlmacenOrigenId), new(claves.ArticuloId, claves.AlmacenDestinoId)],
            "EUR",
            CancellationToken.None);

        LoQueMueveLaTransferencia movido = transferencia.Enviar(
            1,
            new TransferenciaEnviada(
                transferencia.Id, empresaId, claves.AlmacenOrigenId, claves.AlmacenDestinoId, fecha, 1),
            new ElPrecioMedioPonderado().Valorar(saldos, lineas, "EUR", fecha),
            LotesYSeriesResueltos.Ninguno,
            momento);

        repositorio.Agregar(transferencia);
        await repositorio.MoverAsync(movido, CancellationToken.None);

        await new UnidadDeTrabajoDeInventario(contexto).ConfirmarAsync(CancellationToken.None);
        await transaccion.CommitAsync();

        return transferencia.Id;
    }

    /// <summary>Anula sin la API una transferencia enviada, como lo hace el caso de uso.</summary>
    /// <remarks>
    /// <b>Las dos puntas bajo cerrojo y el agregado decide</b>, como en
    /// <see cref="EnviarSinLaApiAsync"/>: el inverso nace recibido y devuelve al origen lo que salió,
    /// y el original pasa a anulado. El número del inverso es el segundo de la serie inventada del
    /// original, por lo mismo que en <see cref="AnularUnAjusteAsync"/>.
    /// </remarks>
    /// <param name="postgres">El contenedor con las migraciones puestas.</param>
    /// <param name="empresaId">La empresa de la transferencia (R8).</param>
    /// <param name="transferenciaId">La transferencia enviada.</param>
    /// <param name="hoy">El día del inverso, que no puede ser anterior al envío.</param>
    /// <returns>El identificador del inverso.</returns>
    internal static async Task<Guid> AnularSinLaApiAsync(
        PostgresConTodosLosModulos postgres,
        Guid empresaId,
        Guid transferenciaId,
        DateOnly hoy)
    {
        const long SegundoNumeroDeEsaSerie = 2;

        DateTimeOffset momento = DateTimeOffset.UtcNow;

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        RepositorioDeTransferencias repositorio = new(contexto, new InquilinoFijo(empresaId));

        Transferencia original = await repositorio.ObtenerAsync(transferenciaId, CancellationToken.None)
            ?? throw new InvalidOperationException("La transferencia que se iba a anular no está.");

        Transferencia inverso = original.CrearInverso(hoy, "Anulación del carril", momento);

        IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado> saldos = await repositorio.BloquearLasValoracionesAsync(
            [
                .. inverso.Lineas
                    .SelectMany(linea => (ClaveDeValoracion[])
                    [
                        new(linea.ArticuloId, inverso.AlmacenOrigenId),
                        new(linea.ArticuloId, inverso.AlmacenDestinoId),
                    ])
                    .Distinct(),
            ],
            inverso.Divisa,
            CancellationToken.None);

        IReadOnlyList<LineaValorada> enElOrigen = new ElPrecioMedioPonderado().Valorar(
            saldos, inverso.LineasAValorarEnElOrigen(), inverso.Divisa, hoy);

        LoQueMueveLaTransferencia movido = inverso.ConfirmarComoInverso(
            original,
            SegundoNumeroDeEsaSerie,
            new TransferenciaRecibida(inverso.Id, empresaId, inverso.AlmacenDestinoId, hoy, inverso.Lineas.Count),
            null,
            enElOrigen,
            LotesYSeriesResueltos.Ninguno,
            momento);

        original.Anular(inverso, new TransferenciaAnulada(original.Id, empresaId));

        repositorio.Agregar(inverso);
        await repositorio.MoverAsync(movido, CancellationToken.None);

        await new UnidadDeTrabajoDeInventario(contexto).ConfirmarAsync(CancellationToken.None);
        await transaccion.CommitAsync();

        return inverso.Id;
    }

    /// <summary>Un escalar de la base, sin EF Core por medio.</summary>
    /// <typeparam name="T">Qué se espera leer.</typeparam>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="consulta">La consulta.</param>
    /// <returns>El valor de la primera columna de la primera fila.</returns>
    internal static async Task<T> EscalarAsync<T>(
        PostgresConTodosLosModulos postgres, string consulta)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(consulta, conexion);

        object? valor = await orden.ExecuteScalarAsync();

        return (T)valor!;
    }

    /// <summary>Una columna de texto, entera.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="consulta">La consulta.</param>
    /// <returns>Las filas leídas, en el orden en que llegan.</returns>
    internal static async Task<IReadOnlyList<string>> TextosAsync(
        PostgresConTodosLosModulos postgres, string consulta)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(consulta, conexion);
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        List<string> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add(lector.GetString(0));
        }

        return filas;
    }

    /// <summary>
    /// Ejecuta una sentencia <b>dentro de una transacción que se deshace</b> y exige que el motor
    /// la rechace.
    /// </summary>
    /// <remarks>
    /// La transacción no es prudencia genérica: es lo que convierte un disparador ausente en un
    /// caso rojo en vez de en un libro vaciado. Y se devuelve la excepción entera para que quien
    /// llama afirme <b>de qué</b> rechazo se trata: un <c>UPDATE</c> que fallara por una columna
    /// que no existe también lanzaría, y sería un verde que no ha comprobado la regla.
    /// </remarks>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="sentencia">Lo que se intenta.</param>
    /// <returns>El error que devolvió PostgreSQL.</returns>
    internal static async Task<PostgresException> ElMotorRechazaAsync(
        PostgresConTodosLosModulos postgres, string sentencia)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync();
        await using NpgsqlCommand orden = new(sentencia, conexion, transaccion);

        PostgresException fallo = await Should.ThrowAsync<PostgresException>(
            () => orden.ExecuteNonQueryAsync());

        await transaccion.RollbackAsync();

        return fallo;
    }

    /// <summary>El código SQLSTATE con el que el libro dice «aquí solo se añade».</summary>
    /// <remarks>
    /// Es el que la función del motor pone con <c>USING ERRCODE = 'restrict_violation'</c>. Se
    /// afirma el código y además la operación que aparece en el mensaje: sin lo segundo, un
    /// rechazo cualquiera con el mismo código daría el caso por bueno.
    /// </remarks>
    internal const string SoloSeAnade = "23001";
}
