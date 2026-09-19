using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Identidad.Application.Arranque;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Semillas;
using Bastion.Terceros.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Api.Arranque;

/// <summary>
/// El modo migrador: aplica las migraciones de cada módulo con persistencia, carga las semillas
/// del §12, pone al día los permisos del rol del sistema y <b>sale</b>.
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
/// <c>auditoria.registros</c> y los demás escriben ahí en cuanto guardan algo. Con el orden
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
    /// Aplica las migraciones pendientes de cada módulo, carga las semillas y devuelve el
    /// código de salida.
    /// </summary>
    /// <param name="app">La aplicación ya construida, con los módulos registrados.</param>
    /// <returns>
    /// <c>0</c> si el esquema quedó al día, los maestros dentro y el rol del sistema con el catálogo
    /// entero; <c>1</c> si algo falló.
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
            await MigrarAsync<CatalogoDbContext>(alcance, registro).ConfigureAwait(false);
            await MigrarAsync<InventarioDbContext>(alcance, registro).ConfigureAwait(false);

            // Y DESPUÉS las semillas, en el mismo proceso y con el mismo código de salida. Van
            // aquí y no en el arranque de la API por lo mismo que el DDL: con dos réplicas, dos
            // procesos cargarían los maestros a la vez y el segundo se estrellaría contra el
            // índice único del primero. El orden tampoco da igual —cargar antes de migrar es
            // insertar en tablas que aún no existen—, y por eso está detrás de todas.
            await alcance.ServiceProvider
                .GetRequiredService<CargadorDeSemillasDeOrganizacion>()
                .CargarAsync(CancellationToken.None)
                .ConfigureAwait(false);

            // Y el rol del sistema, en CADA despliegue (ADR-0035). La semilla de la API solo
            // entra con la base sin usuarios, así que una versión que trae permisos nuevos no se
            // los daba a nadie. Va aquí por lo mismo que los maestros —un solo proceso, antes de
            // que arranque ninguna réplica— y detrás de todo, porque escribe en la traza.
            await PonerAlDiaLosRolesDelSistemaAsync(alcance, registro).ConfigureAwait(false);

            // Y LA PISTA DE PARTICIONES DEL LIBRO, en CADA despliegue y por el mismo argumento
            // que el rol del sistema: lo que una migración escribe se queda congelado el día en
            // que se escribió esa migración. Los meses de `inventario.movimiento_stock` no pueden
            // ser eso — desde el mes siguiente al de la instalación, todas las filas caerían en la
            // partición por defecto y la partición sería decorativa.
            await AsegurarLasParticionesDelLibroAsync(alcance, registro).ConfigureAwait(false);
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

    private static async Task PonerAlDiaLosRolesDelSistemaAsync(AsyncServiceScope alcance, ILogger registro)
    {
        int enElCatalogo = alcance.ServiceProvider.GetRequiredService<ICatalogoDePermisos>().Todos.Count;

        IReadOnlyList<RolDelSistemaActualizado> roles = await alcance.ServiceProvider
            .GetRequiredService<IActualizarRolesDelSistema>()
            .EjecutarAsync(CancellationToken.None)
            .ConfigureAwait(false);

        if (roles.Count == 0)
        {
            // El primer arranque: el migrador corre antes que la API, y el rol lo crea la
            // semilla de la API con el catálogo entero. Se dice, por lo mismo que «al día».
            SinRolesDelSistema(registro, enElCatalogo);

            return;
        }

        foreach (RolDelSistemaActualizado rol in roles)
        {
            if (rol.Concedidos.Count == 0 && rol.Retirados.Count == 0)
            {
                RolDelSistemaAlDia(registro, rol.Codigo, enElCatalogo);

                continue;
            }

            // Un permiso por línea, y por el mismo motivo que una migración por línea: lo que se
            // busca después es «¿cuándo le llegó este permiso?», y eso se filtra por evento.
            foreach (string permiso in rol.Concedidos)
            {
                PermisoConcedidoAlRolDelSistema(registro, rol.Codigo, permiso);
            }

            foreach (string permiso in rol.Retirados)
            {
                PermisoRetiradoDelRolDelSistema(registro, rol.Codigo, permiso);
            }

            RolDelSistemaAlineado(registro, rol.Codigo, rol.Concedidos.Count, rol.Retirados.Count, enElCatalogo);
        }
    }

    /// <summary>Deja creadas las particiones mensuales del libro, doce meses por delante.</summary>
    /// <remarks>
    /// <para>
    /// <b>El mecanismo es UNO y vive en la base</b>, y esto es quien lo llama por segunda vez: la
    /// primera es la migración que crea la tabla, para que el mes de la instalación exista desde
    /// el primer instante. Escrito en C# no lo podría llamar la migración; escrito dos veces
    /// serían dos verdades que se separan el día que alguien toque una.
    /// </para>
    /// <para>
    /// <b>Se dice cuántas ha creado, también cuando son cero.</b> Cero es lo normal a partir del
    /// segundo despliegue del mes, y un migrador que callara no distinguiría «ya estaban» de «no
    /// he mirado» — que es el mismo argumento por el que <c>EsquemaAlDia</c> existe.
    /// </para>
    /// <para>
    /// <b>Y si el día llega en que no puede crear un mes, esto revienta</b>, el migrador sale con
    /// 1 y la API no arranca: la función lanza con el mes en el mensaje cuando la partición por
    /// defecto ya tiene filas de ese mes. Es una parada ruidosa a propósito, porque una partición
    /// por defecto con filas dentro es un invariante ya roto y seguir en silencio lo deja crecer.
    /// </para>
    /// </remarks>
    private static async Task AsegurarLasParticionesDelLibroAsync(
        AsyncServiceScope alcance,
        ILogger registro)
    {
        InventarioDbContext contexto = alcance.ServiceProvider.GetRequiredService<InventarioDbContext>();

        // El alias `Value` no es decorativo y tampoco es nuestro: `SqlQueryRaw<T>` de un escalar
        // exige que la columna se llame así. Sin él, EF Core no sabe a qué proyectar el `int`.
        int creadas = await contexto.Database
            .SqlQueryRaw<int>("""SELECT inventario.asegurar_particiones_de_movimientos() AS "Value";""")
            .SingleAsync()
            .ConfigureAwait(false);

        ParticionesDelLibroAlDia(registro, creadas);
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
        Level = LogLevel.Information,
        Message = "Todavía no hay ningún rol del sistema: lo creará la semilla de la API con los {Permisos} permisos del catálogo.")]
    private static partial void SinRolesDelSistema(ILogger logger, int permisos);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Rol}: el rol del sistema ya tenía los {Permisos} permisos del catálogo.")]
    private static partial void RolDelSistemaAlDia(ILogger logger, string rol, int permisos);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Rol}: concedido {Permiso}, que declara la versión desplegada y el rol del sistema no tenía.")]
    private static partial void PermisoConcedidoAlRolDelSistema(ILogger logger, string rol, string permiso);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Rol}: retirado {Permiso}, que el catálogo de la versión desplegada ya no declara.")]
    private static partial void PermisoRetiradoDelRolDelSistema(ILogger logger, string rol, string permiso);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Rol}: {Concedidos} permisos concedidos y {Retirados} retirados; el rol del sistema queda con los {Permisos} del catálogo.")]
    private static partial void RolDelSistemaAlineado(ILogger logger, string rol, int concedidos, int retirados, int permisos);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "El libro de movimientos tiene su pista de particiones al día; se han creado {Creadas} en este arranque.")]
    private static partial void ParticionesDelLibroAlDia(ILogger logger, int creadas);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "El migrador no ha podido dejar el esquema al día con sus maestros dentro.")]
    private static partial void MigracionFallida(ILogger logger, Exception excepcion);
}
