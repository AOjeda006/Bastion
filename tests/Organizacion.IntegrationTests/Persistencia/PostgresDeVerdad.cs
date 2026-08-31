using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// Un PostgreSQL de verdad, compartido por los tests de esta colección, con las migraciones
/// del módulo ya aplicadas.
/// </summary>
/// <remarks>
/// <para>
/// La imagen es <b>la misma que sirve <c>deploy/docker-compose.yml</c></b>. Dejar que
/// Testcontainers use la suya por omisión sería probar contra una versión de PostgreSQL que no
/// es la que se despliega, y las diferencias de una versión mayor no son teóricas.
/// </para>
/// <para>
/// Se aplican <c>Migrate()</c> y no <c>EnsureCreated()</c>: lo que hay que probar es el esquema
/// que van a crear las migraciones en producción, no uno equivalente generado por otro camino.
/// </para>
/// </remarks>
public sealed class PostgresDeVerdad : IAsyncLifetime
{
    /// <summary>Versión de PostgreSQL, la misma del compose. Si allí sube, aquí también.</summary>
    public const string Imagen = "postgres:17.6-alpine";

    // La imagen va en el CONSTRUCTOR: desde Testcontainers 4.14 el constructor sin argumentos
    // está obsoleto, precisamente para que nadie herede una versión por omisión sin darse cuenta.
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder(Imagen)
        .WithDatabase("bastion_pruebas")
        .WithUsername("bastion")
        .WithPassword("bastion")
        .Build();

    /// <summary>Cadena de conexión al contenedor ya arrancado.</summary>
    public string CadenaDeConexion => _contenedor.GetConnectionString();

    /// <summary>Abre un contexto nuevo contra el contenedor.</summary>
    public OrganizacionDbContext AbrirContexto()
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, CadenaDeConexion);
        return new OrganizacionDbContext(
            opciones.Options,
            new InquilinoQueNadieDebeConsultar(),
            new AccesoQueNadieDebeAbrir());
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();

        await using OrganizacionDbContext contexto = AbrirContexto();
        await contexto.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync() => await _contenedor.DisposeAsync();
}

/// <summary>
/// Colección que comparte el contenedor. Levantar PostgreSQL por cada clase de test costaría
/// minutos en la CI sin comprar nada: los tests se limpian sus propios datos.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionDePostgres : ICollectionFixture<PostgresDeVerdad>
{
    /// <summary>Nombre de la colección.</summary>
    public const string Nombre = "PostgreSQL de verdad";
}

/// <summary>
/// Un inquilino que <b>lanza en cuanto se le pregunta</b>, y ese es todo su trabajo.
/// </summary>
/// <remarks>
/// Los tests de este proyecto miran el esquema por <c>information_schema</c>: no consultan ni una
/// entidad, así que el filtro de R8 no llega a evaluarse nunca. Con un doble que devolviera una
/// empresa cualquiera eso sería una suposición; con este, es una afirmación comprobada, porque el
/// día que alguien añada aquí una consulta a una tabla del módulo, el test se cae y se entera.
/// </remarks>
internal sealed class InquilinoQueNadieDebeConsultar : IInquilinoActual
{
    public bool HayEmpresaActiva => false;

    public Guid? EmpresaDelFiltro => throw new FaltaLaEmpresaActivaException(
        "Este proyecto solo mira el esquema. Si has llegado aquí es que has escrito una consulta " +
        "a una entidad del módulo, y esa se prueba en Api.IntegrationTests, con sesión de verdad.");

    public MotivoSinInquilino? MotivoDelAmbito => null;

    public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
        "Abrir un ámbito sin inquilino aquí no significa nada: no hay ninguno que suspender.");
}

/// <summary>
/// El gemelo del anterior por el lado de R16: también lanza en cuanto se le pregunta.
/// </summary>
/// <remarks>
/// Por lo mismo. El filtro de bloqueo solo se evalúa al consultar una entidad, y aquí no se
/// consulta ninguna; si alguien escribe la primera consulta, se entera por una excepción y no
/// por un resultado que parece correcto.
/// </remarks>
internal sealed class AccesoQueNadieDebeAbrir : IAccesoALoBloqueado
{
    public bool Abierto => throw new NotSupportedException(
        "Este proyecto solo mira el esquema. Si has llegado aquí es que has escrito una consulta " +
        "a una entidad del módulo, y esa se prueba en Api.IntegrationTests.");

    public MotivoParaVerLoBloqueado? MotivoDelAmbito => null;

    public IDisposable ViendoLoBloqueado(MotivoParaVerLoBloqueado motivo) =>
        throw new NotSupportedException(
            "Ver lo bloqueado aquí no significa nada: no hay ninguna consulta que filtrar.");
}
