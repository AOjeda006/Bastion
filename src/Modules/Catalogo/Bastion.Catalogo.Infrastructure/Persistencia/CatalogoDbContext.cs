using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Bastion.Catalogo.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core del módulo Catálogo: su esquema, sus tablas y su propio historial de
/// migraciones (§14).
/// </summary>
/// <remarks>
/// <para>
/// El historial va explícitamente en el esquema del módulo, y no es una preferencia: por omisión
/// EF Core lo guarda en <c>public.__EFMigrationsHistory</c>, que es un sitio compartido, y el
/// segundo módulo que migrase encontraría allí las migraciones del primero, se creería al día y no
/// aplicaría las suyas. El fallo no sale por pantalla: sale como un esquema incompleto en
/// producción.
/// </para>
/// <para>
/// <b>Sin filtro de bloqueo, y está comprobado en vez de supuesto.</b> Los otros contextos declaran
/// un <c>HasQueryFilter</c> de R16 sobre las entidades bloqueables, porque el art. 32 de la LOPDGDD
/// exige que los datos reservados no se vean por el camino ordinario. Ni el artículo ni la
/// categoría son bloqueables: no guardan ningún dato de una persona —ni nombre, ni identificador
/// fiscal, ni domicilio, ni contacto—. Quien lo comprueba es
/// <c>ElCatalogoNoGuardaDatosDeNadieTests</c>, recorriendo el modelo de este contexto; el día que
/// alguien le cuelgue a la ficha un responsable de compras, la respuesta deja de ser esta y ese
/// test se pone rojo para obligar a volver a contestarla.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa por la que filtra el inquilinato (R8).</param>
/// <param name="bloqueados">De dónde sale el permiso para ver lo bloqueado (R16).</param>
public sealed class CatalogoDbContext(
    DbContextOptions<CatalogoDbContext> opciones,
    IInquilinoActual inquilino,
    IAccesoALoBloqueado bloqueados)
    : ContextoDeModulo(opciones, inquilino, bloqueados)
{
    /// <summary>Esquema de PostgreSQL del módulo, según la tabla del 0.4 de <c>docs/PLAN.md</c>.</summary>
    public const string Esquema = "catalogo";

    /// <summary>Tabla de historial de migraciones, DENTRO del esquema del módulo.</summary>
    public const string TablaDelHistorial = "__historial_de_migraciones";

    /// <summary>Lo que la empresa compra, vende o fabrica.</summary>
    public DbSet<Articulo> Articulos => Set<Articulo>();

    /// <summary>El árbol con el que la empresa clasifica su catálogo.</summary>
    public DbSet<Categoria> Categorias => Set<Categoria>();

    /// <summary>
    /// Cablea el contexto contra PostgreSQL. Único sitio donde se dice el proveedor, dónde vive el
    /// historial de migraciones y qué convención de nombres se aplica.
    /// </summary>
    /// <param name="opciones">Constructor de opciones que se cablea.</param>
    /// <param name="cadenaDeConexion">Cadena de conexión a PostgreSQL.</param>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        opciones
            .UseNpgsql(cadenaDeConexion, npgsql => npgsql
                .MigrationsHistoryTable(TablaDelHistorial, Esquema)
                .MigrationsAssembly(typeof(CatalogoDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogoDbContext).Assembly);

        // Las tres tablas compartidas, mapeadas aquí para que la traza, el evento y el recibo de
        // idempotencia entren en el MISMO `SaveChanges` que el cambio, y marcadas para no migrarse
        // desde aquí: las crea el módulo Auditoría, que es su dueño.
        ConfiguracionDeAuditoria.Mapear(modelBuilder, migra: false);
        ConfiguracionDeLaBandeja.Mapear(modelBuilder, migra: false);
        ConfiguracionDeIdempotencia.Mapear(modelBuilder, migra: false);

        // R8. El catálogo es de la empresa que lo mantiene: dos empresas pueden tener el mismo
        // código de artículo y no ser el mismo artículo.
        modelBuilder.Entity<Articulo>().HasQueryFilter(
            "Inquilinato", articulo => EmpresaDelFiltro == null || articulo.EmpresaId == EmpresaDelFiltro);

        // Y el árbol también, con más motivo: cómo clasifica su catálogo una ferretería no tiene
        // por qué parecerse a cómo lo clasifica una imprenta. Este filtro es además lo que hace
        // que el ascenso de `ElArbolSigueSiendoUnArbol` no pueda salirse de la empresa: un padre
        // prestado de otra empresa sale como «no existe» sin una rama propia que se pueda olvidar.
        modelBuilder.Entity<Categoria>().HasQueryFilter(
            "Inquilinato", categoria => EmpresaDelFiltro == null || categoria.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(
            "Inquilinato", registro => EmpresaDelFiltro == null || registro.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<EventoDeLaBandeja>().HasQueryFilter(
            "Inquilinato", evento => EmpresaDelFiltro == null || evento.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<RegistroDeIdempotencia>().HasQueryFilter(
            "Inquilinato", recibo => EmpresaDelFiltro == null || recibo.EmpresaId == EmpresaDelFiltro);
    }
}
