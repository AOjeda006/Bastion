using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Bastion.Terceros.Domain.Terceros;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Terceros.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core del módulo Terceros: su esquema, sus tablas y su propio historial de
/// migraciones (§14).
/// </summary>
/// <remarks>
/// El historial va explícitamente en el esquema del módulo, y no es una preferencia: por omisión
/// EF Core lo guarda en <c>public.__EFMigrationsHistory</c>, que es un sitio compartido, y el
/// segundo módulo que migrase encontraría allí las migraciones del primero, se creería al día y no
/// aplicaría las suyas. El fallo no sale por pantalla: sale como un esquema incompleto en
/// producción.
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa por la que filtra el inquilinato (R8).</param>
/// <param name="bloqueados">De dónde sale el permiso para ver lo bloqueado (R16).</param>
public sealed class TercerosDbContext(
    DbContextOptions<TercerosDbContext> opciones,
    IInquilinoActual inquilino,
    IAccesoALoBloqueado bloqueados)
    : ContextoDeModulo(opciones, inquilino, bloqueados)
{
    /// <summary>Esquema de PostgreSQL del módulo, según la tabla del 0.4 de <c>docs/PLAN.md</c>.</summary>
    public const string Esquema = "terceros";

    /// <summary>Tabla de historial de migraciones, DENTRO del esquema del módulo.</summary>
    public const string TablaDelHistorial = "__historial_de_migraciones";

    /// <summary>Clientes, proveedores, o las dos cosas.</summary>
    public DbSet<Tercero> Terceros => Set<Tercero>();

    /// <summary>
    /// Cablea el contexto contra PostgreSQL. Único sitio donde se dice el proveedor, dónde vive el
    /// historial de migraciones y qué convención de nombres se aplica.
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        opciones
            .UseNpgsql(cadenaDeConexion, npgsql => npgsql
                .MigrationsHistoryTable(TablaDelHistorial, Esquema)
                .MigrationsAssembly(typeof(TercerosDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TercerosDbContext).Assembly);

        // Las tres tablas compartidas, mapeadas aquí para que la traza, el evento y el recibo de
        // idempotencia entren en el MISMO `SaveChanges` que el cambio, y marcadas para no migrarse
        // desde aquí: las crea el módulo Auditoría, que es su dueño.
        ConfiguracionDeAuditoria.Mapear(modelBuilder, migra: false);
        ConfiguracionDeLaBandeja.Mapear(modelBuilder, migra: false);
        ConfiguracionDeIdempotencia.Mapear(modelBuilder, migra: false);

        // R8. Un tercero es de la empresa que lo conoce: dos empresas que le compran al mismo
        // proveedor tienen cada una su ficha.
        modelBuilder.Entity<Tercero>().HasQueryFilter(
            "Inquilinato", tercero => EmpresaDelFiltro == null || tercero.EmpresaId == EmpresaDelFiltro);

        // R16, y aquí por el motivo que la ley nombra: un tercero puede ser una persona física.
        // Es el filtro DE REPOSITORIO que pide el art. 32 —lo bloqueado no se ve porque la
        // consulta no lo trae, no porque la pantalla lo esconda—, y quien necesita verlo abre su
        // ámbito con su motivo. El alta lo abre para comprobar la unicidad; nadie más, de momento.
        modelBuilder.Entity<Tercero>().HasQueryFilter(
            "Bloqueo", tercero => VerLoBloqueado || !tercero.Bloqueo.EstaBloqueado);

        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(
            "Inquilinato", registro => EmpresaDelFiltro == null || registro.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<EventoDeLaBandeja>().HasQueryFilter(
            "Inquilinato", evento => EmpresaDelFiltro == null || evento.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<RegistroDeIdempotencia>().HasQueryFilter(
            "Inquilinato", recibo => EmpresaDelFiltro == null || recibo.EmpresaId == EmpresaDelFiltro);
    }
}
