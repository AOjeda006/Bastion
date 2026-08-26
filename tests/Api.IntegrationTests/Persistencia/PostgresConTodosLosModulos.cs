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
    public OrganizacionDbContext AbrirOrganizacion()
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, CadenaDeConexion);

        return new OrganizacionDbContext(opciones.Options);
    }

    /// <summary>Abre un contexto de Identidad contra el contenedor.</summary>
    public IdentidadDbContext AbrirIdentidad()
    {
        DbContextOptionsBuilder<IdentidadDbContext> opciones = new();
        IdentidadDbContext.Configurar(opciones, CadenaDeConexion);

        return new IdentidadDbContext(opciones.Options);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();

        await using (OrganizacionDbContext organizacion = AbrirOrganizacion())
        {
            await organizacion.Database.MigrateAsync();
        }

        await using (IdentidadDbContext identidad = AbrirIdentidad())
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
