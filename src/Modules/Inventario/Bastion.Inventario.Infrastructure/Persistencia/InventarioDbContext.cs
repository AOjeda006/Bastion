using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Recuentos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core del módulo Inventario: su esquema, sus tablas y su propio historial de
/// migraciones (§14).
/// </summary>
/// <remarks>
/// <para>
/// El historial va explícitamente en el esquema del módulo, y no es una preferencia: por omisión
/// EF Core lo guarda en <c>public.__EFMigrationsHistory</c>, que es un sitio compartido, y el
/// segundo módulo que migrase encontraría allí las migraciones del primero, se creería al día y no
/// aplicaría las suyas.
/// </para>
/// <para>
/// <b>Sin filtro de R16 sobre el libro, y dicho aquí.</b> Los cinco tipos bloqueables del sistema
/// llevan su filtro <c>"Bloqueo"</c>; el movimiento de stock no es uno de ellos porque no
/// implementa <c>IBloqueable</c>, y no lo implementa porque bloquear un hecho contable sería
/// esconderlo. Que el almacén o la ubicación a los que apunta sí puedan estar bloqueados no le
/// afecta: una estantería bloqueada sigue existiendo y sus movimientos viejos siguen contando
/// (ADR-0037).
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa por la que filtra el inquilinato (R8).</param>
/// <param name="bloqueados">De dónde sale el permiso para ver lo bloqueado (R16).</param>
public sealed class InventarioDbContext(
    DbContextOptions<InventarioDbContext> opciones,
    IInquilinoActual inquilino,
    IAccesoALoBloqueado bloqueados)
    : ContextoDeModulo(opciones, inquilino, bloqueados)
{
    /// <summary>Esquema de PostgreSQL del módulo, según la tabla del 0.4 de <c>docs/PLAN.md</c>.</summary>
    public const string Esquema = "inventario";

    /// <summary>Tabla de historial de migraciones, DENTRO del esquema del módulo.</summary>
    public const string TablaDelHistorial = "__historial_de_migraciones";

    /// <summary>El libro mayor de existencias (R3). Solo se añade.</summary>
    public DbSet<MovimientoStock> Movimientos => Set<MovimientoStock>();

    /// <summary>Los ajustes de inventario: el primer documento que mueve el libro.</summary>
    /// <remarks>
    /// No hay <c>DbSet</c> de <c>LineaDeAjuste</c>, y la ausencia es la decisión: una línea es
    /// parte del ajuste y se lee siempre con él, así que no filtra por empresa -filtra el
    /// documento del que cuelga-. Que siga sin consultarse suelta lo comprueba
    /// <c>ElFiltroNoSeSaltaPorAhiTests</c>, que prohíbe su <c>Set&lt;&gt;</c> por nombre.
    /// </remarks>
    public DbSet<Ajuste> Ajustes => Set<Ajuste>();

    /// <summary>Las transferencias entre almacenes de una empresa (ADR-0053).</summary>
    /// <remarks>Sin <c>DbSet</c> de sus líneas, por lo mismo que el ajuste.</remarks>
    public DbSet<Transferencia> Transferencias => Set<Transferencia>();

    /// <summary>Los recuentos de inventario, que no escriben en el libro: lo mueve su ajuste (ADR-0055).</summary>
    /// <remarks>Sin <c>DbSet</c> de sus líneas, por lo mismo que el ajuste.</remarks>
    public DbSet<Recuento> Recuentos => Set<Recuento>();

    /// <summary>
    /// Lo apartado de cada almacén para la línea de un pedido, con sus consumos (ADR-0059). No
    /// escriben en el libro: lo escribe el consumo, con su salida.
    /// </summary>
    /// <remarks>Sin <c>DbSet</c> de sus consumos, por lo mismo que las líneas del ajuste.</remarks>
    public DbSet<Reserva> Reservas => Set<Reserva>();

    /// <summary>La fila viva de cada existencia: la suma del libro, guardada (ADR-0044).</summary>
    /// <remarks>
    /// Se lee por aquí y no se escribe por aquí: la mueve en crudo la sentencia que anota el libro.
    /// </remarks>
    public DbSet<Existencia> Existencias => Set<Existencia>();

    /// <summary>El saldo de cada existencia al cierre de cada mes, hasta el corte (ADR-0044).</summary>
    public DbSet<InstantaneaMensual> InstantaneasMensuales => Set<InstantaneaMensual>();

    /// <summary>Hasta qué mes tiene instantáneas cada empresa (ADR-0044).</summary>
    public DbSet<CorteDeLaInstantanea> CortesDeLaInstantanea => Set<CorteDeLaInstantanea>();

    /// <summary>
    /// Lo que vale lo que hay de cada artículo en cada almacén (ADR-0046). Solo se lee por aquí: la
    /// escriben las sentencias que anotan el libro.
    /// </summary>
    public DbSet<Valoracion> Valoraciones => Set<Valoracion>();

    /// <summary>
    /// Los lotes de cada artículo (ADR-0048 §2). Solo se leen por aquí: los crea la sentencia que
    /// resuelve los códigos al confirmar.
    /// </summary>
    public DbSet<Lote> Lotes => Set<Lote>();

    /// <summary>Los números de serie de cada artículo, como los lotes (ADR-0048 §2).</summary>
    public DbSet<NumeroDeSerie> NumerosDeSerie => Set<NumeroDeSerie>();

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
                .MigrationsAssembly(typeof(InventarioDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventarioDbContext).Assembly);

        // Las tres tablas compartidas, mapeadas aquí para que la traza, el evento y el recibo de
        // idempotencia entren en el MISMO `SaveChanges` que el cambio, y marcadas para no migrarse
        // desde aquí: las crea el módulo Auditoría, que es su dueño.
        ConfiguracionDeAuditoria.Mapear(modelBuilder, migra: false);
        ConfiguracionDeLaBandeja.Mapear(modelBuilder, migra: false);
        ConfiguracionDeIdempotencia.Mapear(modelBuilder, migra: false);

        // R8, y en el libro es lo que impide el peor de los errores posibles: sumar existencias de
        // dos empresas de la misma instalación en un mismo saldo.
        modelBuilder.Entity<MovimientoStock>().HasQueryFilter(
            "Inquilinato",
            movimiento => EmpresaDelFiltro == null || movimiento.EmpresaId == EmpresaDelFiltro);

        // El documento también es de una empresa, y su filtro es el de siempre. Las LÍNEAS no
        // llevan el suyo: cuelgan del ajuste, se cargan con él y no se consultan sueltas.
        modelBuilder.Entity<Ajuste>().HasQueryFilter(
            "Inquilinato", ajuste => EmpresaDelFiltro == null || ajuste.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<Transferencia>().HasQueryFilter(
            "Inquilinato",
            transferencia => EmpresaDelFiltro == null || transferencia.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<Recuento>().HasQueryFilter(
            "Inquilinato", recuento => EmpresaDelFiltro == null || recuento.EmpresaId == EmpresaDelFiltro);

        // Los CONSUMOS no llevan el suyo: cuelgan de la reserva, como las líneas del ajuste.
        modelBuilder.Entity<Reserva>().HasQueryFilter(
            "Inquilinato", reserva => EmpresaDelFiltro == null || reserva.EmpresaId == EmpresaDelFiltro);

        // La proyección del libro, con el filtro del libro: las existencias de otra empresa no se
        // leen, igual que sus movimientos. Las sentencias crudas que la escriben no pasan por aquí,
        // y por eso cada una compara la empresa ella misma.
        modelBuilder.Entity<Existencia>().HasQueryFilter(
            "Inquilinato",
            existencia => EmpresaDelFiltro == null || existencia.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<InstantaneaMensual>().HasQueryFilter(
            "Inquilinato",
            instantanea => EmpresaDelFiltro == null || instantanea.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<CorteDeLaInstantanea>().HasQueryFilter(
            "Inquilinato", corte => EmpresaDelFiltro == null || corte.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<Valoracion>().HasQueryFilter(
            "Inquilinato",
            valoracion => EmpresaDelFiltro == null || valoracion.EmpresaId == EmpresaDelFiltro);

        // Un lote es de una empresa aunque el código se repita en otra: la empresa va en su clave.
        modelBuilder.Entity<Lote>().HasQueryFilter(
            "Inquilinato", lote => EmpresaDelFiltro == null || lote.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<NumeroDeSerie>().HasQueryFilter(
            "Inquilinato", serie => EmpresaDelFiltro == null || serie.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(
            "Inquilinato", registro => EmpresaDelFiltro == null || registro.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<EventoDeLaBandeja>().HasQueryFilter(
            "Inquilinato", evento => EmpresaDelFiltro == null || evento.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<RegistroDeIdempotencia>().HasQueryFilter(
            "Inquilinato", recibo => EmpresaDelFiltro == null || recibo.EmpresaId == EmpresaDelFiltro);
    }
}
