using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
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

        return new OrganizacionDbContext(opciones.Options, new InquilinoFijo(empresaId));
    }

    /// <summary>Abre un contexto de Identidad contra el contenedor, como una empresa concreta.</summary>
    /// <param name="empresaId">Como qué empresa se abre.</param>
    public IdentidadDbContext AbrirIdentidad(Guid empresaId)
    {
        DbContextOptionsBuilder<IdentidadDbContext> opciones = new();
        IdentidadDbContext.Configurar(opciones, CadenaDeConexion);

        return new IdentidadDbContext(opciones.Options, new InquilinoFijo(empresaId));
    }

    /// <summary>Un contexto de Organización solo para aplicar migraciones.</summary>
    /// <remarks>Migrar es DDL: no consulta ninguna entidad, así que el filtro no se evalúa.</remarks>
    public OrganizacionDbContext AbrirOrganizacionParaMigrar()
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, CadenaDeConexion);

        return new OrganizacionDbContext(opciones.Options, new InquilinoFijo(null));
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

        return new OrganizacionDbContext(opciones.Options, inquilino);
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

        return new AuditoriaDbContext(opciones.Options, new InquilinoFijo(empresaId));
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

        return new AuditoriaDbContext(opciones.Options, new InquilinoFijo(null));
    }

    /// <summary>Un contexto de Identidad solo para aplicar migraciones.</summary>
    /// <remarks>Migrar es DDL: no consulta ninguna entidad, así que el filtro no se evalúa.</remarks>
    public IdentidadDbContext AbrirIdentidadParaMigrar()
    {
        DbContextOptionsBuilder<IdentidadDbContext> opciones = new();
        IdentidadDbContext.Configurar(opciones, CadenaDeConexion);

        return new IdentidadDbContext(opciones.Options, new InquilinoFijo(null));
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
