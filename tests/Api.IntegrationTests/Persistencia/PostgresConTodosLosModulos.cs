using Bastion.BuildingBlocks.Application.Multiempresa;
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

    public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
        "La puerta de atrás de los tests no abre ámbitos: si un test necesita ver más de una " +
        "empresa, abre un contexto por empresa y lo dice.");
}
