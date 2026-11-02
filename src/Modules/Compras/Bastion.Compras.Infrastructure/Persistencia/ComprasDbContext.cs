using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Compras.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core del módulo Compras: su esquema, sus tablas y su propio historial de
/// migraciones (§14).
/// </summary>
/// <remarks>
/// <para>
/// <b>Entra en el 3.3 sin una sola tabla propia</b>, y a propósito: el primer documento del módulo
/// es el pedido del 3.7. Lo que trae es lo que no conviene estrenar a la vez que el pedido: el
/// esquema, el historial, el sitio en el migrador y en los censos, y las tablas compartidas
/// mapeadas, para que el pedido escriba su traza y su evento en el mismo <c>SaveChanges</c> que su
/// cambio.
/// </para>
/// <para>
/// El historial va explícitamente en el esquema del módulo por lo mismo que en los demás: por
/// omisión EF Core lo guarda en <c>public.__EFMigrationsHistory</c>, que es compartido, y el
/// segundo módulo que migrase encontraría allí las migraciones del primero.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa por la que filtra el inquilinato (R8).</param>
/// <param name="bloqueados">De dónde sale el permiso para ver lo bloqueado (R16).</param>
public sealed class ComprasDbContext(
    DbContextOptions<ComprasDbContext> opciones,
    IInquilinoActual inquilino,
    IAccesoALoBloqueado bloqueados)
    : ContextoDeModulo(opciones, inquilino, bloqueados)
{
    /// <summary>
    /// Esquema de PostgreSQL del módulo, según la convención del 0.4, que está en
    /// <c>docs/historia/fase-0.md</c>.
    /// </summary>
    public const string Esquema = "compras";

    /// <summary>Tabla de historial de migraciones, DENTRO del esquema del módulo.</summary>
    public const string TablaDelHistorial = "__historial_de_migraciones";

    /// <summary>
    /// Cablea el contexto contra PostgreSQL. Único sitio donde se dice el proveedor, dónde vive el
    /// historial de migraciones y qué convención de nombres se aplica.
    /// </summary>
    /// <param name="opciones">Constructor de opciones.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        opciones
            .UseNpgsql(cadenaDeConexion, npgsql => npgsql
                .MigrationsHistoryTable(TablaDelHistorial, Esquema)
                .MigrationsAssembly(typeof(ComprasDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Sin `ApplyConfigurationsFromAssembly`, que los demás contextos llaman aquí: sobre un
        // ensamblado sin ninguna configuración, EF Core escribe un aviso cada vez que construye el
        // modelo. Entra con la primera, la del pedido.
        modelBuilder.HasDefaultSchema(Esquema);

        // Las tres tablas compartidas, mapeadas aquí para que la traza, el evento y el recibo de
        // idempotencia entren en el MISMO `SaveChanges` que el cambio, y marcadas para no migrarse
        // desde aquí: las crea el módulo Auditoría, que es su dueño.
        ConfiguracionDeAuditoria.Mapear(modelBuilder, migra: false);
        ConfiguracionDeLaBandeja.Mapear(modelBuilder, migra: false);
        ConfiguracionDeIdempotencia.Mapear(modelBuilder, migra: false);

        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(
            "Inquilinato", registro => EmpresaDelFiltro == null || registro.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<EventoDeLaBandeja>().HasQueryFilter(
            "Inquilinato", evento => EmpresaDelFiltro == null || evento.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<RegistroDeIdempotencia>().HasQueryFilter(
            "Inquilinato", recibo => EmpresaDelFiltro == null || recibo.EmpresaId == EmpresaDelFiltro);
    }
}
