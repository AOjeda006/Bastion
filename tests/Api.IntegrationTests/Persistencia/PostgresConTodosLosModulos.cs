using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Entidades;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// Un PostgreSQL de verdad con las migraciones de <b>todos</b> los módulos aplicadas.
/// </summary>
/// <remarks>
/// <para>
/// La imagen es <b>la misma que sirve <c>deploy/docker-compose.yml</c></b>: probar contra otra
/// versión mayor sería probar contra una base que no es la que se despliega.
/// </para>
/// <para>
/// Cada módulo migra por su cuenta, con su propio <c>DbContext</c>, contra la MISMA base. Es
/// exactamente lo que hará el despliegue, y es lo que convierte el test del historial por esquema
/// en una prueba de verdad: con un solo módulo migrado, un historial mal ubicado no se nota.
/// </para>
/// </remarks>
public sealed class PostgresConTodosLosModulos : IAsyncLifetime
{
    /// <summary>Versión de PostgreSQL, la misma del compose. Si allí sube, aquí también.</summary>
    public const string Imagen = "postgres:17.6-alpine";

    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder(Imagen)
        .WithDatabase("bastion_pruebas")
        .WithUsername("bastion")
        .WithPassword("bastion")
        .Build();

    /// <summary>Cadena de conexión al contenedor ya arrancado.</summary>
    public string CadenaDeConexion => _contenedor.GetConnectionString();

    /// <summary>Abre un contexto de Organización contra el contenedor.</summary>
    /// <remarks>
    /// Para llegar donde la API todavía no llega: hay estados —una serie que ya ha numerado— que
    /// solo sabe producir el dominio, y montarlos con SQL a mano probaría una fila que el sistema
    /// no produce nunca.
    /// </remarks>
    /// <param name="empresaId">
    /// Como qué empresa se abre. Es <b>obligatorio</b>: un contexto de prueba sin empresa vería
    /// las filas de todas, y entonces la puerta de atrás del test tendría más alcance que la API
    /// que está ayudando a probar. Para el único caso que de verdad no tiene empresa —migrar—
    /// está <see cref="AbrirOrganizacionParaMigrar"/>, que lo dice en el nombre.
    /// </param>
    public OrganizacionDbContext AbrirOrganizacion(Guid empresaId)
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, CadenaDeConexion);

        return new OrganizacionDbContext(
            opciones.Options, new InquilinoFijo(empresaId), new AccesoCerrado());
    }

    /// <summary>Abre un contexto de Identidad contra el contenedor, como una empresa concreta.</summary>
    /// <param name="empresaId">Como qué empresa se abre.</param>
    public IdentidadDbContext AbrirIdentidad(Guid empresaId)
    {
        DbContextOptionsBuilder<IdentidadDbContext> opciones = new();
        IdentidadDbContext.Configurar(opciones, CadenaDeConexion);

        return new IdentidadDbContext(
            opciones.Options, new InquilinoFijo(empresaId), new AccesoCerrado());
    }

    /// <summary>Un contexto de Organización solo para aplicar migraciones.</summary>
    /// <remarks>Migrar es DDL: no consulta ninguna entidad, así que el filtro no se evalúa.</remarks>
    public OrganizacionDbContext AbrirOrganizacionParaMigrar()
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, CadenaDeConexion);

        return new OrganizacionDbContext(
            opciones.Options, new InquilinoFijo(null), new AccesoCerrado());
    }

    /// <summary>
    /// Un contexto de Organizacion CON el interceptor de auditoria puesto, como lo tiene el host.
    /// </summary>
    /// <remarks>
    /// Los demas `Abrir...` de esta clase no lo llevan: son la puerta de atras para montar estados
    /// que solo sabe producir el dominio, y ahi la traza estorbaria. Este existe para lo contrario
    /// —probar el interceptor— y para los dos casos que la API no puede provocar por diseno: una
    /// escritura que revienta a mitad y una fila con la empresa de otro. Ninguna peticion puede
    /// nombrar una empresa (lo comprueba `NingunaPeticionNombraLaEmpresaTests`), asi que la unica
    /// forma de comprobar que la guarda salta es ponerse aqui.
    /// </remarks>
    /// <param name="empresaId">Empresa activa.</param>
    /// <param name="usuarioId">Quien firma los cambios.</param>
    public OrganizacionDbContext AbrirOrganizacionAuditada(Guid empresaId, Guid usuarioId)
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, CadenaDeConexion);

        InquilinoFijo inquilino = new(empresaId);
        opciones.AddInterceptors(
            new InterceptorDeAuditoria(inquilino, new UsuarioFijo(empresaId, usuarioId), TimeProvider.System));

        return new OrganizacionDbContext(opciones.Options, inquilino, new AccesoCerrado());
    }

    /// <summary>
    /// Un contexto de Organización con el interceptor de marcas de tiempo y el reloj que se le diga.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El reloj entra por parámetro y ese es todo el motivo de que este método exista.</b> Es lo
    /// único que hace comprobable la frase «la hora la pone el reloj inyectado»: con
    /// <c>TimeProvider.System</c>, el instante que acaba en la columna se parece tanto al que
    /// pondría un <c>DEFAULT now()</c> que la comprobación no distinguiría entre los dos
    /// mecanismos. Con un reloj parado en un instante que la base de datos no puede producir, sí.
    /// </para>
    /// <para>
    /// No sustituye ningún registro del contenedor de la API —eso lo prohíbe <c>ApiDeVerdad</c>—:
    /// construye otro contexto, por la puerta de atrás, como los dos de arriba. Que el host lleve
    /// el interceptor puesto de verdad se comprueba por el otro lado, pasando por la API.
    /// </para>
    /// </remarks>
    /// <param name="empresaId">Empresa activa.</param>
    /// <param name="reloj">De dónde sale la hora que se escribirá en <c>modificado_en</c>.</param>
    public OrganizacionDbContext AbrirOrganizacionConMarcasDeTiempo(Guid empresaId, TimeProvider reloj)
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, CadenaDeConexion);
        opciones.AddInterceptors(new InterceptorDeMarcasDeTiempo(reloj));

        return new OrganizacionDbContext(
            opciones.Options, new InquilinoFijo(empresaId), new AccesoCerrado());
    }

    /// <summary>Abre un contexto de Auditoria contra el contenedor, como una empresa concreta.</summary>
    /// <remarks>
    /// Es la unica manera de LEER la traza en el 0.7: no hay endpoint de consulta, y no lo hay a
    /// proposito (eso es de la fase 10). La evidencia de que un cambio deja rastro es esta tabla
    /// leida de la base, no una pantalla.
    /// </remarks>
    /// <param name="empresaId">Como que empresa se abre.</param>
    public AuditoriaDbContext AbrirAuditoria(Guid empresaId)
    {
        DbContextOptionsBuilder<AuditoriaDbContext> opciones = new();
        AuditoriaDbContext.Configurar(opciones, CadenaDeConexion);

        return new AuditoriaDbContext(
            opciones.Options, new InquilinoFijo(empresaId), new AccesoCerrado());
    }

    /// <summary>Un contexto de Auditoria sin empresa, para ver TODA la traza.</summary>
    /// <remarks>
    /// Se llama asi y no `AbrirAuditoria(null)` por lo mismo que <see cref="AbrirOrganizacionParaMigrar"/>:
    /// mirar la tabla entera es lo que hace falta para comprobar que una fila NO esta —que ningun
    /// resumen de contrasena aparece en ninguna traza, por ejemplo—, y una comprobacion asi hecha
    /// con el filtro puesto daria verde por no estar mirando.
    /// </remarks>
    public AuditoriaDbContext AbrirAuditoriaEntera()
    {
        DbContextOptionsBuilder<AuditoriaDbContext> opciones = new();
        AuditoriaDbContext.Configurar(opciones, CadenaDeConexion);

        return new AuditoriaDbContext(
            opciones.Options, new InquilinoFijo(null), new AccesoCerrado());
    }

    /// <summary>
    /// El catálogo de eventos de la puerta de atrás: una línea por evento declarado.
    /// </summary>
    /// <remarks>
    /// Repite las declaraciones que hacen los <c>Modulo…</c> porque construirlo desde el
    /// contenedor obligaría a levantar el host, y el host trae el publicador puesto — que es
    /// justo lo que no puede estar corriendo mientras se comprueba en qué transacción entró una
    /// fila. Que la lista se quede corta no da un verde silencioso: el interceptor lanza al
    /// volcar un evento sin declarar, con el nombre del tipo en el mensaje. Y que la de verdad
    /// esté completa lo comprueba <c>CadaEventoEstaDeclaradoTests</c>, en el paso rápido.
    /// </remarks>
    public static CatalogoDeEventos Catalogo { get; } =
        new([new DeclaracionDeEvento(EmpresaCreada.Nombre, typeof(EmpresaCreada))]);

    /// <summary>
    /// Un contexto de Organización CON el interceptor de la bandeja puesto, como lo tiene el host.
    /// </summary>
    /// <remarks>
    /// Existe por lo mismo que <see cref="AbrirOrganizacionAuditada"/>: para probar el interceptor
    /// —y, sobre todo, para mirar en qué transacción entró cada fila— hace falta un guardado que
    /// no esté compartiendo la base con un publicador que va por detrás actualizando las filas de
    /// la cola. Un <c>UPDATE</c> le cambia el <c>xmin</c> a la fila, así que la comparación que
    /// prueba la atomicidad tiene que hacerse sobre una cola que nadie está vaciando.
    /// </remarks>
    /// <param name="empresaId">Empresa activa, la que quedará escrita en el evento.</param>
    public OrganizacionDbContext AbrirOrganizacionConBandeja(Guid empresaId)
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, CadenaDeConexion);

        InquilinoFijo inquilino = new(empresaId);
        opciones.AddInterceptors(new InterceptorDeLaBandeja(inquilino, Catalogo, TimeProvider.System));

        return new OrganizacionDbContext(opciones.Options, inquilino, new AccesoCerrado());
    }

    /// <summary>Abre el contexto de la bandeja como una empresa concreta.</summary>
    /// <remarks>
    /// Es la única manera de LEER la cola: no hay endpoint que la consulte, y no lo hay a
    /// propósito. Con empresa, para comprobar que el filtro de la tabla es de verdad.
    /// </remarks>
    /// <param name="empresaId">Como qué empresa se abre.</param>
    public ContextoDeLaBandeja AbrirBandeja(Guid empresaId) => AbrirBandeja((Guid?)empresaId);

    /// <summary>Abre el contexto de la bandeja sin empresa, para ver la cola entera.</summary>
    /// <remarks>
    /// Se llama así y no <c>AbrirBandeja(null)</c> por lo mismo que
    /// <see cref="AbrirAuditoriaEntera"/>: comprobar que una fila NO está exige mirar la tabla
    /// entera, y hacerlo con el filtro puesto daría verde por no estar mirando.
    /// </remarks>
    public ContextoDeLaBandeja AbrirBandejaEntera() => AbrirBandeja((Guid?)null);

    /// <summary>Un contexto de Identidad solo para aplicar migraciones.</summary>
    /// <remarks>Migrar es DDL: no consulta ninguna entidad, así que el filtro no se evalúa.</remarks>
    public IdentidadDbContext AbrirIdentidadParaMigrar()
    {
        DbContextOptionsBuilder<IdentidadDbContext> opciones = new();
        IdentidadDbContext.Configurar(opciones, CadenaDeConexion);

        return new IdentidadDbContext(
            opciones.Options, new InquilinoFijo(null), new AccesoCerrado());
    }

    // El contexto de la bandeja no tiene `Configurar` a propósito: vive en los bloques comunes,
    // que traen EF Core pero NO el proveedor de PostgreSQL, así que quien elige proveedor es el
    // módulo Auditoría en su cableado. Aquí se repite esa elección, que es la misma y es de una
    // línea; lo que no se puede es llamar a un método que allí no existe.
    private ContextoDeLaBandeja AbrirBandeja(Guid? empresaId)
    {
        DbContextOptionsBuilder<ContextoDeLaBandeja> opciones = new();
        opciones.UseNpgsql(CadenaDeConexion).UseSnakeCaseNamingConvention();

        return new ContextoDeLaBandeja(
            opciones.Options, new InquilinoFijo(empresaId), new AccesoCerrado());
    }

    /// <summary>
    /// Crea una base de datos NUEVA en el mismo servidor, para los tests que necesitan una cola
    /// que nadie más esté tocando —o una base a la que nadie ha aplicado nada—.
    /// </summary>
    /// <remarks>
    /// La base compartida tiene las migraciones puestas y a los demás tests escribiendo en ella;
    /// hay dos cosas que ahí no se pueden comprobar: qué hace el publicador cuando la tabla no
    /// está, y cuánto vale una métrica que habla del elemento más viejo de la cola.
    /// </remarks>
    /// <param name="migrada">Si se le aplican las migraciones de Auditoría, que son las de la bandeja.</param>
    /// <returns>La cadena de conexión a la base nueva.</returns>
    public async Task<string> CrearBaseNuevaAsync(bool migrada)
    {
        string nombre = "bastion_" + Guid.CreateVersion7().ToString("N")[..12];

        await using (NpgsqlConnection servidor = new(CadenaDeConexion))
        {
            await servidor.OpenAsync();

            // El nombre lo compone este método a partir de un identificador recién creado: no hay
            // entrada de nadie por medio, y `CREATE DATABASE` no admite parámetros.
            await using NpgsqlCommand crear = new($"CREATE DATABASE \"{nombre}\"", servidor);
            await crear.ExecuteNonQueryAsync();
        }

        string cadena = new NpgsqlConnectionStringBuilder(CadenaDeConexion)
        {
            Database = nombre,
        }.ConnectionString;

        if (migrada)
        {
            DbContextOptionsBuilder<AuditoriaDbContext> opciones = new();
            AuditoriaDbContext.Configurar(opciones, cadena);

            await using AuditoriaDbContext auditoria =
                new(opciones.Options, new InquilinoFijo(null), new AccesoCerrado());

            await auditoria.Database.MigrateAsync();
        }

        return cadena;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();

        // Auditoria PRIMERO: es la duenna de `auditoria.registros`, y los otros dos modulos
        // escriben ahi en cuanto guardan algo. Con este orden invertido, la semilla de arranque
        // reventaria contra una tabla que no existe.
        await using (AuditoriaDbContext auditoria = AbrirAuditoriaEntera())
        {
            await auditoria.Database.MigrateAsync();
        }

        await using (OrganizacionDbContext organizacion = AbrirOrganizacionParaMigrar())
        {
            await organizacion.Database.MigrateAsync();
        }

        await using (IdentidadDbContext identidad = AbrirIdentidadParaMigrar())
        {
            await identidad.Database.MigrateAsync();
        }
    }

    /// <inheritdoc/>
    public async Task DisposeAsync() => await _contenedor.DisposeAsync();
}

/// <summary>
/// Colección que comparte el contenedor y, con él, la semilla de arranque.
/// </summary>
/// <remarks>
/// <para>
/// Compartirlo no es solo por tiempo. La semilla se aplica <b>una vez</b>, cuando no hay ningún
/// usuario, y de ahí salen las credenciales con las que llaman todos los tests. Un contenedor por
/// clase daría una semilla distinta por clase, y la contraseña —que es estática del proceso— solo
/// valdría en la primera.
/// </para>
/// </remarks>
[CollectionDefinition(Nombre)]
public sealed class ColeccionDeLaApi : ICollectionFixture<PostgresConTodosLosModulos>
{
    /// <summary>Nombre de la colección.</summary>
    public const string Nombre = "API contra PostgreSQL de verdad";
}

/// <summary>
/// El inquilino de la puerta de atrás de los tests: fijo, sin <i>claim</i> y sin ningún ingenio.
/// </summary>
/// <remarks>
/// <para>
/// No sustituye nada del contenedor de la API —ahí el inquilino sale del <i>claim</i>, como en
/// producción—: esto es para los contextos que un test abre <b>a mano</b> para montar un estado
/// que solo sabe producir el dominio.
/// </para>
/// <para>
/// El nulo significa «migrar» y nada más. Se pide siempre por el constructor, para que abrir un
/// contexto de prueba sin decir con qué empresa sea imposible por descuido.
/// </para>
/// </remarks>
/// <param name="empresaId">La empresa fija, o nulo para migrar.</param>
internal sealed class InquilinoFijo(Guid? empresaId) : IInquilinoActual
{
    public bool HayEmpresaActiva => empresaId is not null;

    public Guid? EmpresaDelFiltro => empresaId;

    // Nunca hay ambito abierto, porque `SinInquilino` no abre ninguno. Un contexto de la puerta de
    // atras que escribiera dejaria traza sin empresa y sin motivo, y eso lo rechaza el interceptor;
    // aqui no pasa porque estos contextos no llevan interceptor.
    public MotivoSinInquilino? MotivoDelAmbito => null;

    public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
        "La puerta de atrás de los tests no abre ámbitos: si un test necesita ver más de una " +
        "empresa, abre un contexto por empresa y lo dice.");
}

/// <summary>
/// El usuario de la puerta de atras: uno fijo, sin token y sin permisos.
/// </summary>
/// <remarks>
/// No concede nada —<see cref="Tiene"/> dice que no a todo—, porque quien decide si una operacion
/// se permite es la autorizacion de la API, y esta clase no la sustituye: solo le dice al
/// interceptor quien firma la fila de traza.
/// </remarks>
/// <param name="empresaId">Empresa activa.</param>
/// <param name="usuarioId">Quien firma.</param>
internal sealed class UsuarioFijo(Guid empresaId, Guid usuarioId) : IUsuarioActual
{
    public bool EstaAutenticado => true;

    public Guid UsuarioId => usuarioId;

    public Guid EmpresaId => empresaId;

    public bool Tiene(Permiso permiso) => false;
}

/// <summary>
/// El acceso a lo bloqueado de la puerta de atrás: cerrado, como en producción.
/// </summary>
/// <remarks>
/// Que la puerta de atrás vea lo mismo que la API es justamente lo que la hace útil: si aquí
/// estuviera abierta, un test podría montar un estado bloqueado, leerlo sin enterarse de que
/// está bloqueado, y dar por buena una API que no lo está filtrando. Un test que necesite
/// comprobar que la fila bloqueada SIGUE en la base la lee con SQL en crudo, que es la evidencia
/// de verdad y no depende de ningún filtro.
/// </remarks>
internal sealed class AccesoCerrado : IAccesoALoBloqueado
{
    public bool Abierto => false;

    public MotivoParaVerLoBloqueado? MotivoDelAmbito => null;

    public IDisposable ViendoLoBloqueado(MotivoParaVerLoBloqueado motivo) =>
        throw new NotSupportedException(
            "La puerta de atrás de los tests no abre ámbitos de R16: lo que hay que comprobar es " +
            "que el filtro tapa la fila, no esquivarlo desde el propio test.");
}
