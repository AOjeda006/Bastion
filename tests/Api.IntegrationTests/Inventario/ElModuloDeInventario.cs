using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Application.Recuentos;
using Bastion.Inventario.Application.Transferencias;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Los cuatro casos de uso del ajuste, los cuatro de la transferencia y el alta del recuento, con sus
/// adaptadores REALES y los contextos que necesitan.
/// </summary>
/// <remarks>
/// <para>
/// <b>El alta del recuento entró en el ítem 2.12</b>, aunque tiene borde: la carrera de dos altas
/// necesita parar la primera con su fila escrita y sin publicar, y una petición HTTP no deja hacerlo.
/// Y el caso de uso solo, sin el borde que traduce el índice, es como se ve que la comprobación previa
/// contesta por sí misma.
/// </para>
/// <para>
/// <b>La transferencia entró en el ítem 2.11</b>, por lo mismo que el ajuste: su alta no tiene borde,
/// y las carreras necesitan parar una operación a medias, cosa que una petición HTTP no deja hacer.
/// Comparte con el ajuste el contexto, la unidad de trabajo, el numerador y los puertos, como en el
/// contenedor de la API.
/// </para>
/// <para>
/// <b>Vive en su propio fichero desde el ítem 2.4</b>, cuando dejó de tener un solo cliente: lo
/// usan el caso del almacén bloqueado y el de la serie. Se cablea a mano y no se pide al
/// contenedor del host porque el host todavía no tiene ningún borde de Inventario —los endpoints
/// son del 2.4 y del 2.5—, así que no hay petición que resolver. Que el cableado de verdad
/// registre estas mismas piezas se comprueba en otro sitio:
/// <c>AgregarCasosDeUsoDeInventario</c> tiene su propio caso en el carril rápido.
/// </para>
/// <para>
/// <b>Los contextos se abren a mano y no con <c>postgres.Abrir…</c></b>: el doble de aquellos
/// lleva <c>AccesoCerrado</c>, que lanza en cuanto alguien abre un ámbito del art. 32, y los
/// puertos de almacén y de ubicación abren uno. Es el mismo motivo por el que
/// <c>UnaEstanteriaBloqueadaSigueExistiendoTests</c> abre el suyo.
/// </para>
/// <para>
/// <b>El acceso es uno solo y compartido por los dos puertos</b>, como en el contenedor de la
/// API: el ámbito es <c>AsyncLocal</c> y se abre y se cierra dentro de cada llamada, así que
/// compartirlo no deja ninguna puerta abierta entre una y la siguiente.
/// </para>
/// </remarks>
internal sealed class ElModuloDeInventario : IAsyncDisposable
{
    private readonly OrganizacionDbContext _organizacion;
    private readonly CatalogoDbContext _catalogo;
    private readonly InventarioDbContext _inventario;
    private readonly RepositorioDeAjustes _ajustes;

    // Lo que este módulo ha lanzado con su propia transacción, para que no se cierre su conexión
    // con algo todavía dentro. Ver `DisposeAsync`.
    private readonly List<Task> _lanzadas = [];

    // EL RELOJ ES EL DE VERDAD, SALVO QUE EL CASO FIJE EL SUYO (ítem 2.9). Los casos de uso del
    // ajuste y de la transferencia deciden con él qué fecha es futura y con qué fecha va el
    // inverso, y un caso cuya secuencia
    // sale de «hoy» —el de la propiedad— tiene que repetirla igual cualquier otro día.
    internal ElModuloDeInventario(
        PostgresConTodosLosModulos postgres, Guid empresaId, TimeProvider? reloj = null)
    {
        TimeProvider elReloj = reloj ?? TimeProvider.System;

        AccesoALoBloqueado acceso =
            new(NullLogger<AccesoALoBloqueado>.Instance, new NadieEnConcreto());

        DbContextOptionsBuilder<OrganizacionDbContext> deOrganizacion = new();
        OrganizacionDbContext.Configurar(deOrganizacion, postgres.CadenaDeConexion);
        _organizacion = new OrganizacionDbContext(
            deOrganizacion.Options, new InquilinoFijo(empresaId), acceso);

        DbContextOptionsBuilder<CatalogoDbContext> deCatalogo = new();
        CatalogoDbContext.Configurar(deCatalogo, postgres.CadenaDeConexion);
        _catalogo = new CatalogoDbContext(
            deCatalogo.Options, new InquilinoFijo(empresaId), acceso);

        _inventario = postgres.AbrirInventario(empresaId);

        RepositorioDeAjustes ajustes = new(_inventario, new InquilinoFijo(empresaId));
        _ajustes = ajustes;
        RepositorioDeTransferencias transferencias = new(_inventario, new InquilinoFijo(empresaId));
        RepositorioDeRecuentos recuentos = new(_inventario);
        UnidadDeTrabajoDeInventario unidadDeTrabajo = new(_inventario);
        ConsultaDeEmpresas empresas = new(_organizacion);
        ConsultaDeAlmacenes almacenes = new(_organizacion, acceso);
        ConsultaDeSeries series = new(_organizacion);
        ConsultaDeUbicaciones ubicaciones = new(_organizacion, acceso);
        ConsultaDeArticulos articulos = new(_catalogo);
        ConsultaDeUnidadesDeMedida unidades = new(_organizacion);

        // La marca va sobre EL CONTEXTO DE INVENTARIO, como el ejercicio y por lo mismo: al
        // confirmar trae un cerrojo compartido sobre la fila del artículo (ADR-0048 §4).
        LaTrazabilidadDesdeInventario trazabilidad = new(_inventario, new InquilinoFijo(empresaId));

        Alta = new AbrirAjuste(
            new ElUsuarioDeLaEmpresa(empresaId),
            ajustes,
            empresas,
            almacenes,
            series,
            ubicaciones,
            articulos,
            trazabilidad,
            unidades,
            unidadDeTrabajo,
            elReloj);

        // El puerto del ejercicio va sobre EL CONTEXTO DE INVENTARIO, igual que en produccion y
        // por el mismo motivo: la respuesta trae un cerrojo compartido sobre la fila, y un cerrojo
        // sobre otra conexion no serializa nada. Con `_organizacion` aqui, el arnes probaria un
        // mecanismo que no es el que se despliega.
        LosEjerciciosDesdeInventario ejercicios =
            new(_inventario, new InquilinoFijo(empresaId));

        Confirmacion = new ConfirmarAjuste(
            ajustes,
            new NumeradorDeSeriesDeInventario(_inventario, new InquilinoFijo(empresaId)),
            ejercicios,
            trazabilidad,
            new ElPrecioMedioPonderado(),
            unidadDeTrabajo,
            elReloj);

        Anulacion = new AnularAjuste(
            ajustes,
            new NumeradorDeSeriesDeInventario(_inventario, new InquilinoFijo(empresaId)),
            ejercicios,
            new ElPrecioMedioPonderado(),
            unidadDeTrabajo,
            elReloj);

        Lectura = new MovimientosDelDocumento(ajustes, almacenes);

        AltaDeTransferencia = new AbrirTransferencia(
            new ElUsuarioDeLaEmpresa(empresaId),
            transferencias,
            empresas,
            almacenes,
            series,
            ubicaciones,
            articulos,
            trazabilidad,
            unidades,
            unidadDeTrabajo,
            elReloj);

        Envio = new EnviarTransferencia(
            transferencias,
            new NumeradorDeSeriesDeInventario(_inventario, new InquilinoFijo(empresaId)),
            ejercicios,
            trazabilidad,
            new ElPrecioMedioPonderado(),
            unidadDeTrabajo,
            elReloj);

        Recepcion = new RecibirTransferencia(
            transferencias, ejercicios, new ElPrecioMedioPonderado(), unidadDeTrabajo, elReloj);

        AnulacionDeTransferencia = new AnularTransferencia(
            transferencias,
            new NumeradorDeSeriesDeInventario(_inventario, new InquilinoFijo(empresaId)),
            ejercicios,
            new ElPrecioMedioPonderado(),
            unidadDeTrabajo,
            elReloj);

        AltaDeRecuento = new AbrirRecuento(
            new ElUsuarioDeLaEmpresa(empresaId),
            recuentos,
            empresas,
            almacenes,
            series,
            articulos,
            unidadDeTrabajo,
            elReloj);
    }

    internal AbrirAjuste Alta { get; }

    internal ConfirmarAjuste Confirmacion { get; }

    internal AnularAjuste Anulacion { get; }

    internal MovimientosDelDocumento Lectura { get; }

    internal AbrirTransferencia AltaDeTransferencia { get; }

    internal EnviarTransferencia Envio { get; }

    internal RecibirTransferencia Recepcion { get; }

    internal AnularTransferencia AnulacionDeTransferencia { get; }

    internal AbrirRecuento AltaDeRecuento { get; }

    /// <summary>Confirma un ajuste con una transacción abierta, como llegaría de verdad.</summary>
    /// <remarks>
    /// <b>La transacción no es adorno del caso: el mecanismo de numeración revienta sin
    /// ella</b>, y a propósito —un número tomado fuera queda gastado si el documento no llega
    /// a guardarse—. En una petición de verdad la abre el filtro de idempotencia, que este
    /// cableado a mano no tiene porque no hay borde todavía; aquí la abre esto, con el mismo
    /// criterio: solo se confirma lo que sale bien.
    /// <para>
    /// <b>Y si el motor la rechaza, el contexto olvida lo que el caso de uso había cambiado</b>
    /// (ítem 2.8). El agregado ya había transitado en memoria —confirmado, con su número— cuando
    /// la sentencia del libro chocó contra la restricción del stock. En una petición de verdad ese
    /// contexto muere con ella; aquí se reutiliza, y el siguiente <c>SaveChanges</c> escribiría un
    /// documento confirmado que la base acaba de rechazar.
    /// </para>
    /// </remarks>
    /// <param name="ajusteId">El documento que confirmar.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal Task<Resultado<AjusteDto>> ConfirmarAsync(Guid ajusteId) =>
        Lanzada(EnSuTransaccionAsync(() => Confirmacion.EjecutarAsync(ajusteId, CancellationToken.None)));

    /// <summary>
    /// Confirma <b>dentro</b> de una transacción y la deja abierta, con el cerrojo compartido
    /// sobre la fila del ejercicio todavía puesto.
    /// </summary>
    /// <remarks>
    /// <b>Es lo que convierte «cerrar después de confirmar» en «cerrar MIENTRAS se confirma».</b>
    /// Dos llamadas seguidas no prueban nada del cerrojo: la segunda se encuentra el trabajo de la
    /// primera ya hecho y publicado. Lo que hay que ejercer es el solape —una confirmación que ha
    /// leído el ejercicio y todavía no ha llegado a su <c>COMMIT</c>—, y eso exige poder parar una
    /// a medias. Quien llama decide cuándo suelta.
    /// </remarks>
    /// <param name="ajusteId">El documento que confirmar.</param>
    /// <returns>Lo que contestó el caso de uso, y la transacción todavía abierta.</returns>
    internal async Task<(Resultado<AjusteDto> Confirmacion, IDbContextTransaction Transaccion)>
        ConfirmarYQuedarseDentroAsync(Guid ajusteId)
    {
        IDbContextTransaction transaccion = await _inventario.Database.BeginTransactionAsync();

        Resultado<AjusteDto> confirmacion =
            await Confirmacion.EjecutarAsync(ajusteId, CancellationToken.None);

        return (confirmacion, transaccion);
    }

    /// <summary>Confirma dentro de la transacción que ya abrió quien llama.</summary>
    /// <param name="ajusteId">El documento que confirmar.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal Task<Resultado<AjusteDto>> ConfirmarSinAbrirTransaccionAsync(Guid ajusteId) =>
        Confirmacion.EjecutarAsync(ajusteId, CancellationToken.None);

    /// <summary>
    /// Bloquea la valoración de unas claves en la transacción que ya abrió quien llama, como hace el
    /// caso de uso antes de valorar, y no hace nada más.
    /// </summary>
    /// <remarks>
    /// <b>Para la carrera de la valoración, que tiene que ver el cerrojo solo.</b> Si esta
    /// transacción ya hubiera sumado la fila, quien llega segundo esperaría igual: su
    /// <c>INSERT … ON CONFLICT</c> espera en el índice único a la transacción que cambió la fila,
    /// bloquee lo que ya está o no. Con la fila bloqueada y nada escrito, solo espera si el cerrojo
    /// bloquea de verdad.
    /// </remarks>
    /// <param name="claves">Las claves que bloquear.</param>
    /// <param name="divisa">La del documento, para las claves que nazcan.</param>
    /// <returns>Lo que la valoración de cada clave tenía al bloquearla.</returns>
    internal Task<IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado>> BloquearLasValoracionesAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves,
        string divisa) =>
        _ajustes.BloquearLasValoracionesAsync(claves, divisa, CancellationToken.None);

    /// <summary>Abre la transacción de este módulo sin hacer nada más.</summary>
    /// <remarks>
    /// Para los casos que necesitan poner un <c>lock_timeout</c> en la conexión antes de que el
    /// caso de uso pida el cerrojo: sin plazo, el que pierde la carrera se queda esperando para
    /// siempre y el caso no termina nunca en vez de fallar.
    /// </remarks>
    /// <returns>La transacción, abierta.</returns>
    internal Task<IDbContextTransaction> AbrirTransaccionAsync() =>
        _inventario.Database.BeginTransactionAsync();

    /// <summary>
    /// Pone un plazo corto al cerrojo en la conexión de este módulo, dentro de su transacción.
    /// </summary>
    /// <remarks>
    /// <b>La sentencia va entera en una constante y no se compone</b>: un <c>SET</c> no admite
    /// parámetros, así que cualquier plazo variable acabaría concatenado dentro del SQL y el
    /// analizador de EF Core lo para —con razón—. El plazo es el mismo para todos los casos que lo
    /// necesitan, así que no hace falta que varíe.
    /// </remarks>
    internal Task PonerPlazoCortoDeCerrojoAsync() =>
        _inventario.Database.ExecuteSqlRawAsync(PlazoCorto);

    /// <summary>Lo que se espera a una operación en vuelo antes de cerrar su contexto.</summary>
    private static readonly TimeSpan s_plazoDeLoQueQuedoEnVuelo = TimeSpan.FromSeconds(30);

    /// <summary>Lo que espera quien llega segundo antes de rendirse con un 55P03.</summary>
    internal const string PlazoCorto = "SET LOCAL lock_timeout = '300ms'";

    /// <summary>
    /// Quita los índices a las lecturas de este módulo, dentro de su transacción: el motor recorre
    /// cada tabla en el orden en que están guardadas sus filas.
    /// </summary>
    /// <remarks>
    /// Para el caso del orden de las líneas. Con el índice de <c>(ajuste_id, numero)</c> el motor
    /// las devolvería casi siempre ya ordenadas, y el caso saldría verde aunque el agregado no las
    /// ordenara. Sin él, las devuelve en el orden en que están guardadas, que el caso ha cambiado.
    /// </remarks>
    internal Task LeerSinIndicesAsync() =>
        _inventario.Database.ExecuteSqlRawAsync(SinIndices);

    /// <summary>Los tres recorridos por índice, apagados hasta el final de la transacción.</summary>
    internal const string SinIndices =
        "SET LOCAL enable_indexscan = off; SET LOCAL enable_bitmapscan = off; "
        + "SET LOCAL enable_indexonlyscan = off";

    /// <summary>El proceso de PostgreSQL que atiende la conexión de este módulo.</summary>
    /// <remarks>
    /// Para los casos en que quien llega segundo <b>no</b> puede llevar plazo, porque lo que se
    /// afirma es lo que decide después de esperar: con esto se pregunta al motor si ya está
    /// esperando a esta transacción, y solo entonces se la suelta. Soltarla antes convertiría la
    /// carrera en dos operaciones seguidas, y dos operaciones seguidas salen bien sin cerrojo.
    /// </remarks>
    internal int ProcesoDeLaBase =>
        ((NpgsqlConnection)_inventario.Database.GetDbConnection()).ProcessID;

    /// <summary>Anula con una transacción abierta, como llegaría de verdad.</summary>
    /// <remarks>
    /// Mismo motivo que en la confirmación, y aquí pesa el doble: el inverso toma su propio
    /// correlativo, y el mecanismo de numeración <b>revienta</b> sin transacción abierta.
    /// </remarks>
    /// <param name="ajusteId">El documento que anular.</param>
    /// <param name="motivo">Por qué se anula.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal Task<Resultado<AnulacionDto>> AnularAsync(Guid ajusteId, string motivo) =>
        Lanzada(EnSuTransaccionAsync(() => AnularSinAbrirTransaccionAsync(ajusteId, motivo)));

    /// <summary>
    /// Abre la transacción de este módulo y lee el documento <b>dentro</b>, sin tocarlo, dejando
    /// la transacción abierta para que otra se le solape.
    /// </summary>
    /// <remarks>
    /// <b>Es lo que convierte «dos llamadas seguidas» en «dos anulaciones simultáneas».</b> Dos
    /// llamadas seguidas no prueban nada: la segunda se encuentra el documento ya anulado y la
    /// guarda de estado la rechaza sin que nada concurrente haya ocurrido. Lo que hay que ejercer
    /// es que las dos <b>lean</b> el mismo <c>Confirmado</c> antes de que ninguna escriba, y eso
    /// exige poder parar una a medias. Quien las separa es el índice único del inverso, y detrás
    /// el testigo de concurrencia de la fila.
    /// </remarks>
    /// <param name="ajusteId">El documento que se va a anular.</param>
    /// <returns>La transacción, todavía abierta, para deshacerla al terminar.</returns>
    internal async Task<IDbContextTransaction> LeerElAjusteYQuedarseDentroAsync(Guid ajusteId)
    {
        IDbContextTransaction transaccion = await _inventario.Database.BeginTransactionAsync();

        _ = await _inventario.Ajustes.SingleAsync(fila => fila.Id == ajusteId);

        return transaccion;
    }

    /// <summary>La anulación sin abrir nada: la transacción ya está puesta por quien llama.</summary>
    /// <param name="ajusteId">El documento que anular.</param>
    /// <param name="motivo">Por qué se anula.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal Task<Resultado<AnulacionDto>> AnularSinAbrirTransaccionAsync(
        Guid ajusteId, string motivo) =>
        Anulacion.EjecutarAsync(ajusteId, new AnularAjusteDto(motivo), CancellationToken.None);

    /// <summary>Envía con una transacción abierta, como llegaría de verdad.</summary>
    /// <remarks>
    /// Mismo motivo que en la confirmación: el envío numera, y el mecanismo de numeración revienta
    /// sin transacción abierta. Si el motor lo rechaza, el contexto olvida lo que el caso de uso
    /// había cambiado.
    /// </remarks>
    /// <param name="transferenciaId">El borrador que enviar.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal Task<Resultado<TransferenciaDto>> EnviarAsync(Guid transferenciaId) =>
        Lanzada(EnSuTransaccionAsync(() => Envio.EjecutarAsync(transferenciaId, CancellationToken.None)));

    /// <summary>
    /// Envía <b>dentro</b> de una transacción y la deja abierta, con todos los cerrojos del envío
    /// puestos y lo que escribió sin publicar.
    /// </summary>
    /// <remarks>
    /// Para la carrera de la serie en tránsito (ADR-0053 §7): la serie ya salió del origen y ya vuela
    /// hacia el destino, y nadie más lo ve todavía. Quien llama decide cuándo suelta.
    /// </remarks>
    /// <param name="transferenciaId">El borrador que enviar.</param>
    /// <returns>Lo que contestó el caso de uso, y la transacción todavía abierta.</returns>
    internal async Task<(Resultado<TransferenciaDto> Envio, IDbContextTransaction Transaccion)>
        EnviarYQuedarseDentroAsync(Guid transferenciaId)
    {
        IDbContextTransaction transaccion = await _inventario.Database.BeginTransactionAsync();

        Resultado<TransferenciaDto> envio = await Envio.EjecutarAsync(transferenciaId, CancellationToken.None);

        return (envio, transaccion);
    }

    /// <summary>Recibe con una transacción abierta, como llegaría de verdad.</summary>
    /// <remarks>
    /// La recepción no numera, pero sin transacción cada sentencia se confirmaría por su cuenta y el
    /// cerrojo de la valoración se soltaría al acabar la suya: la carrera que se quiere ver no
    /// existiría.
    /// </remarks>
    /// <param name="transferenciaId">La transferencia enviada.</param>
    /// <param name="fecha">El día en que llega.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal Task<Resultado<TransferenciaDto>> RecibirAsync(Guid transferenciaId, DateOnly fecha) =>
        Lanzada(EnSuTransaccionAsync(() => Recepcion.EjecutarAsync(
            transferenciaId, new RecibirTransferenciaDto { FechaDeRecepcion = fecha }, CancellationToken.None)));

    /// <summary>
    /// Recibe <b>dentro</b> de una transacción y la deja abierta, con la valoración del destino
    /// bloqueada y la fila del documento ya cambiada.
    /// </summary>
    /// <remarks>
    /// Es la ganadora de las dos carreras del ADR-0053 §10: la otra operación lee la transferencia
    /// todavía enviada, porque esto no se ha publicado, y se para en la valoración del destino.
    /// </remarks>
    /// <param name="transferenciaId">La transferencia enviada.</param>
    /// <param name="fecha">El día en que llega.</param>
    /// <returns>Lo que contestó el caso de uso, y la transacción todavía abierta.</returns>
    internal async Task<(Resultado<TransferenciaDto> Recepcion, IDbContextTransaction Transaccion)>
        RecibirYQuedarseDentroAsync(Guid transferenciaId, DateOnly fecha)
    {
        IDbContextTransaction transaccion = await _inventario.Database.BeginTransactionAsync();

        Resultado<TransferenciaDto> recepcion = await Recepcion.EjecutarAsync(
            transferenciaId, new RecibirTransferenciaDto { FechaDeRecepcion = fecha }, CancellationToken.None);

        return (recepcion, transaccion);
    }

    /// <summary>Anula una transferencia con una transacción abierta, como llegaría de verdad.</summary>
    /// <remarks>
    /// Mismo motivo que en el ajuste: el inverso toma su correlativo de la serie del original, y el
    /// mecanismo de numeración revienta sin transacción abierta.
    /// </remarks>
    /// <param name="transferenciaId">La transferencia enviada o recibida.</param>
    /// <param name="motivo">Por qué se anula.</param>
    /// <returns>Lo que contestó el caso de uso.</returns>
    internal Task<Resultado<AnulacionDeTransferenciaDto>> AnularLaTransferenciaAsync(
        Guid transferenciaId, string motivo) =>
        Lanzada(EnSuTransaccionAsync(() => AnulacionDeTransferencia.EjecutarAsync(
            transferenciaId, new AnularTransferenciaDto(motivo), CancellationToken.None)));

    // Lo que hace el filtro de idempotencia con una petición: abre la transacción, y solo confirma
    // lo que sale bien. Lo que sale mal se deshace entero, número incluido.
    private async Task<Resultado<T>> EnSuTransaccionAsync<T>(Func<Task<Resultado<T>>> operacion)
    {
        await using IDbContextTransaction transaccion =
            await _inventario.Database.BeginTransactionAsync();

        Resultado<T> resultado = await OlvidandoSiFallaAsync(operacion);

        if (resultado.EsCorrecto)
        {
            await transaccion.CommitAsync();
        }
        else
        {
            await transaccion.RollbackAsync();
        }

        return resultado;
    }

    // Lo que haría el fin de la petición: si la operación revienta, el contexto suelta todo lo
    // que el caso de uso había cambiado en memoria, y la excepción sigue su camino.
    private async Task<T> OlvidandoSiFallaAsync<T>(Func<Task<T>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch
        {
            _inventario.ChangeTracker.Clear();
            throw;
        }
    }

    private Task<T> Lanzada<T>(Task<T> operacion)
    {
        _lanzadas.Add(operacion);

        return operacion;
    }

    /// <summary>
    /// Espera a lo que este módulo dejó en vuelo, y solo entonces cierra sus contextos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un caso de carrera que falla a medias sale sin esperar a la operación que dejó en
    /// vuelo</b>, y el <c>await using</c> cerraría el contexto con ella dentro. La operación se
    /// queda colgada para siempre, con su transacción abierta y su cerrojo puesto, y el carril no
    /// termina: la tanda del addendum del 2.8 estuvo tres horas y media parada en la mutación 73.
    /// Quien la tenía frenada ya soltó al salir de su bloque, así que aquí termina sola.
    /// </para>
    /// <para>
    /// <b>Su desenlace no se mira</b>, porque es cosa del caso: o ya lo afirmó, o el caso ya ha
    /// fallado por otra cosa y ese es el fallo que tiene que salir. Si no termina en el plazo, eso
    /// sí se dice, porque cerrar el contexto entonces volvería a colgar el carril.
    /// </para>
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        var todas = Task.WhenAll(_lanzadas);

        if (await Task.WhenAny(todas, Task.Delay(s_plazoDeLoQueQuedoEnVuelo)) != todas)
        {
            throw new TimeoutException(
                "una operación de este módulo sigue en vuelo tras treinta segundos: cerrar su " +
                "contexto la dejaría colgada con su transacción abierta");
        }

        await _inventario.DisposeAsync();
        await _catalogo.DisposeAsync();
        await _organizacion.DisposeAsync();
    }
}

/// <summary>De dónde saca el alta la empresa (R8): del usuario, nunca de la petición.</summary>
/// <remarks>
/// No concede ningún permiso —quien decide si la operación se permite es la autorización de la
/// API, y esta clase no la sustituye—: lo único que aporta es la empresa, que es justo lo que
/// la R8 dice que no puede viajar en el cuerpo.
/// </remarks>
/// <param name="empresaId">La empresa activa.</param>
internal sealed class ElUsuarioDeLaEmpresa(Guid empresaId) : IUsuarioActual
{
    public bool EstaAutenticado => true;

    public Guid UsuarioId => throw new NotSupportedException(
        "El alta de un ajuste no firma la fila: de eso se encarga el interceptor de auditoría.");

    public Guid EmpresaId => empresaId;

    public bool Tiene(Permiso permiso) => false;
}

/// <summary>
/// Quien queda anotado al abrir un ámbito del art. 32 cuando no hay nadie identificado.
/// </summary>
/// <remarks>
/// El <c>AccesoALoBloqueado</c> de verdad anota en el registro quién pidió la apertura; aquí
/// no hay petición HTTP, así que no hay nadie. Lanzar en vez de inventarse un identificador es
/// lo que hace imposible una traza con un usuario falso.
/// </remarks>
internal sealed class NadieEnConcreto : IUsuarioActual
{
    public bool EstaAutenticado => false;

    public Guid UsuarioId => throw new NotSupportedException("No hay nadie autenticado.");

    public Guid EmpresaId => throw new NotSupportedException("No hay nadie autenticado.");

    public bool Tiene(Permiso permiso) => false;
}
