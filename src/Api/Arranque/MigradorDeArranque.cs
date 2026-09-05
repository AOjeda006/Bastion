using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Semillas;
using Bastion.Terceros.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Api.Arranque;

/// <summary>
/// El modo migrador: aplica las migraciones de los tres módulos con persistencia, carga las
/// semillas del §12 y <b>sale</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La API no migra al arrancar, y eso es la decisión, no un detalle.</b> Migrar en el arranque
/// es cómodo con un proceso y es una avería con dos: dos réplicas que arrancan a la vez ejecutan
/// DDL a la vez, y la que pierde la carrera se encuentra el esquema a medio cambiar. Peor todavía,
/// el despliegue que cambia el esquema lo aplica <i>la réplica que arranque primero</i>, o sea
/// nadie en concreto, y sin nada que mirar cuando falla.
/// </para>
/// <para>
/// Aquí el DDL lo ejecuta <b>un contenedor de un solo uso</b>: el mismo artefacto de la API
/// invocado con <c>--migrar</c>, que aplica lo que falte y termina. El resto de servicios espera a
/// que termine <i>bien</i> (<c>service_completed_successfully</c> en el compose). Con eso, el
/// esquema es un paso del despliegue con su propio resultado —verde o rojo, atribuible, con su
/// registro— en vez de un efecto secundario del arranque de un servidor web.
/// </para>
/// <para>
/// Se pide por <b>argumento</b> y no por variable de entorno, también a propósito: una variable se
/// hereda: basta con que alguien la ponga en el <c>.env</c> compartido para que <i>todas</i> las
/// réplicas se conviertan en migradores. Un argumento se escribe servicio a servicio, se ve en
/// <c>docker compose config</c> y no lo arrastra nadie sin querer.
/// </para>
/// <para>
/// El <b>orden</b> no es alfabético y no da igual: Auditoría primero, porque es la dueña de
/// <c>auditoria.registros</c> y los otros dos escriben ahí en cuanto guardan algo. Con el orden
/// invertido, la semilla de arranque reventaría contra una tabla que todavía no existe. Es el mismo
/// orden que usa el arranque de los tests de integración, y por el mismo motivo.
/// </para>
/// </remarks>
public static partial class MigradorDeArranque
{
    /// <summary>El argumento que pide el modo migrador.</summary>
    public const string Argumento = "--migrar";

    /// <summary>Si los argumentos de la línea de órdenes piden migrar y salir.</summary>
    /// <param name="args">Los argumentos con los que se invocó el proceso.</param>
    /// <returns><c>true</c> si entre ellos está <c>--migrar</c>.</returns>
    public static bool LoPiden(string[] args) =>
        args is not null && Array.Exists(args, arg => string.Equals(arg, Argumento, StringComparison.Ordinal));

    /// <summary>Los mismos argumentos sin <c>--migrar</c>, para dárselos al constructor del host.</summary>
    /// <param name="args">Los argumentos con los que se invocó el proceso.</param>
    /// <returns>Los argumentos que sí son configuración.</returns>
    /// <remarks>
    /// El proveedor de configuración de línea de órdenes espera <c>--clave=valor</c> o
    /// <c>--clave valor</c>. Un <c>--migrar</c> suelto no es ninguna de las dos cosas: o se lo
    /// traga y se come el argumento siguiente, o revienta. Se quita antes de que lo vea.
    /// </remarks>
    public static string[] SinElArgumento(string[] args) =>
        args is null
            ? []
            : [.. args.Where(arg => !string.Equals(arg, Argumento, StringComparison.Ordinal))];

    /// <summary>
    /// Aplica las migraciones pendientes de los tres módulos, carga las semillas y devuelve el
    /// código de salida.
    /// </summary>
    /// <param name="app">La aplicación ya construida, con los módulos registrados.</param>
    /// <returns>
    /// <c>0</c> si el esquema quedó al día y los maestros dentro; <c>1</c> si algo falló.
    /// </returns>
    public static async Task<int> MigrarYSalirAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ILogger registro = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(MigradorDeArranque));

        await using AsyncServiceScope alcance = app.Services.CreateAsyncScope();

        try
        {
            // Auditoría primero. Ver el porqué en la documentación de la clase.
            await MigrarAsync<AuditoriaDbContext>(alcance, registro).ConfigureAwait(false);
            await MigrarAsync<OrganizacionDbContext>(alcance, registro).ConfigureAwait(false);
            await MigrarAsync<IdentidadDbContext>(alcance, registro).ConfigureAwait(false);
            await MigrarAsync<TercerosDbContext>(alcance, registro).ConfigureAwait(false);

            // Y DESPUÉS las semillas, en el mismo proceso y con el mismo código de salida. Van
            // aquí y no en el arranque de la API por lo mismo que el DDL: con dos réplicas, dos
            // procesos cargarían los maestros a la vez y el segundo se estrellaría contra el
            // índice único del primero. El orden tampoco da igual —cargar antes de migrar es
            // insertar en tablas que aún no existen—, y por eso está detrás de las tres.
            await alcance.ServiceProvider
                .GetRequiredService<CargadorDeSemillasDeOrganizacion>()
                .CargarAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // Se traga la excepción a propósito y se sale con 1: el compose lee el CÓDIGO DE
            // SALIDA para decidir si arranca la API, y una excepción sin capturar sale con 134 y
            // un volcado de pila que no cabe en ninguna anotación. El mensaje va al registro
            // estructurado, que es donde se mira.
            MigracionFallida(registro, excepcion);

            return 1;
        }

        return 0;
    }

    private static async Task MigrarAsync<TContexto>(AsyncServiceScope alcance, ILogger registro)
        where TContexto : DbContext
    {
        TContexto contexto = alcance.ServiceProvider.GetRequiredService<TContexto>();

        // LA AFIRMACIÓN DE CONJUNTO NO VACÍO DEL MIGRADOR, y no es ceremonia: la primera vez que
        // esto se ejecutó dentro de un contenedor salió «el esquema ya estaba al día» tres veces y
        // código 0 sobre una base sin una sola tabla. El motivo era que las migraciones viven fuera
        // de los proyectos (§14) y entran por un `<Compile Include="../../db/migraciones/…" />`; el
        // `.dockerignore` excluía esa carpeta, el glob no casaba con nada, y el ensamblado
        // publicado no llevaba ni una. Un glob vacío no da error, y «cero pendientes» es
        // indistinguible de «al día».
        //
        // Un módulo con persistencia tiene migraciones. Cero significa que el ensamblado está mal
        // construido, no que la base esté al día.
        IReadOnlyList<string> conocidas = [.. contexto.Database.GetMigrations()];

        if (conocidas.Count == 0)
        {
            throw new InvalidOperationException(
                $"{typeof(TContexto).Name} no conoce ninguna migración. El ensamblado " +
                $"{typeof(TContexto).Assembly.GetName().Name} se ha construido sin ellas: revise " +
                "que el contexto de construcción incluya `db/migraciones/` (el `.dockerignore`) y " +
                "que el `<Compile Include>` del .csproj siga apuntando a la carpeta correcta.");
        }

        IReadOnlyList<string> pendientes =
            [.. await contexto.Database.GetPendingMigrationsAsync().ConfigureAwait(false)];

        if (pendientes.Count == 0)
        {
            // Se dice también cuando no hay nada que hacer. Un migrador silencioso no distingue
            // «el esquema ya estaba al día» de «no he mirado», y las dos salen con 0.
            EsquemaAlDia(registro, typeof(TContexto).Name, conocidas.Count);

            return;
        }

        // Una línea POR MIGRACIÓN y no una con todas juntas. No es estilo: si una migración deja
        // el esquema a medias, lo que hace falta saber es por cuál iba, y un evento por nombre se
        // filtra en el visor; una cadena unida con comas, no. Y de paso no hay ningún argumento
        // caro que evaluar si el registro está apagado.
        foreach (string migracion in pendientes)
        {
            MigracionPendiente(registro, typeof(TContexto).Name, migracion);
        }

        await contexto.Database.MigrateAsync().ConfigureAwait(false);

        EsquemaMigrado(registro, typeof(TContexto).Name, pendientes.Count, conocidas.Count);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Contexto}: el esquema ya estaba al día, con {Conocidas} migraciones conocidas.")]
    private static partial void EsquemaAlDia(ILogger logger, string contexto, int conocidas);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Contexto}: pendiente la migración {Migracion}.")]
    private static partial void MigracionPendiente(ILogger logger, string contexto, string migracion);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Contexto}: aplicadas {Cuantas} de {Conocidas} migraciones.")]
    private static partial void EsquemaMigrado(ILogger logger, string contexto, int cuantas, int conocidas);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "El migrador no ha podido dejar el esquema al día con sus maestros dentro.")]
    private static partial void MigracionFallida(ILogger logger, Exception excepcion);
}
