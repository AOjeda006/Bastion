using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Auditoria.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core del módulo Auditoría: el <b>dueño</b> de la tabla de traza y el único que la
/// migra.
/// </summary>
/// <remarks>
/// <para>
/// <b>La tabla la escriben todos y la crea uno.</b> Los contextos de Organización e Identidad
/// mapean la misma entidad —es lo que hace que la traza vaya en la transacción del cambio— pero la
/// marcan <c>ExcludeFromMigrations</c>: si cada módulo la creara, cada uno llevaría su versión de
/// la misma tabla en su cadena de migraciones y mandaría la primera en aplicarse. Aquí se crea, y
/// aquí se cambia.
/// </para>
/// <para>
/// <b>Y filtra por empresa como cualquier otra.</b> Una traza es un dato: dice qué NIF tenía antes
/// una empresa y quién lo cambió. Sin filtro, la primera consulta que se escriba sobre esta tabla
/// —el 0.7 no escribe ninguna, pero la fase 10 sí— serviría el historial de todos los clientes de
/// la instalación desde dentro de cualquiera de ellos. Las filas sin empresa —las de la semilla y
/// las del acceso— no las ve nadie desde dentro de una empresa, que es lo correcto: no son de
/// ninguna.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa por la que filtra el inquilinato (R8).</param>
public sealed class AuditoriaDbContext(
    DbContextOptions<AuditoriaDbContext> opciones,
    IInquilinoActual inquilino)
    : ContextoDeModulo(opciones, inquilino)
{
    /// <summary>
    /// Esquema de PostgreSQL del módulo: el nombre del módulo en minúsculas y sin acentos.
    /// </summary>
    public const string Esquema = ConfiguracionDeAuditoria.Esquema;

    /// <summary>Tabla de historial de migraciones, DENTRO del esquema del módulo.</summary>
    public const string TablaDelHistorial = "__historial_de_migraciones";

    /// <summary>La traza. De solo lectura desde aquí: quien escribe es el interceptor.</summary>
    public DbSet<RegistroDeAuditoria> Registros => Set<RegistroDeAuditoria>();

    /// <summary>
    /// Cablea el contexto contra PostgreSQL. Único sitio donde se dice el proveedor, dónde vive
    /// el historial de migraciones y qué convención de nombres se aplica.
    /// </summary>
    /// <param name="opciones">Constructor de opciones que se va a rellenar.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        opciones
            .UseNpgsql(cadenaDeConexion, npgsql => npgsql
                .MigrationsHistoryTable(TablaDelHistorial, Esquema)
                .MigrationsAssembly(typeof(AuditoriaDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Esquema);

        ConfiguracionDeAuditoria.Mapear(modelBuilder, migra: true);

        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(
            registro => EmpresaDelFiltro == null || registro.EmpresaId == EmpresaDelFiltro);
    }
}
