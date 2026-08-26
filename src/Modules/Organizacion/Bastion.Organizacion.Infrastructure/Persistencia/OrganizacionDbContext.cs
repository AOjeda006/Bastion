using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core del módulo Organización: su esquema, sus tablas y —esto es lo que hay
/// que mirar dos veces— <b>su propio historial de migraciones</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La trampa del historial.</b> Cada módulo tiene su <c>DbContext</c> y su propia cadena de
/// migraciones. EF Core, por omisión, guarda el historial en <c>public.__EFMigrationsHistory</c>,
/// que es un sitio COMPARTIDO: el segundo módulo que migrase encontraría allí las migraciones
/// del primero, se creería al día y no aplicaría las suyas. El fallo no sale por pantalla: sale
/// como un esquema incompleto en producción. Por eso el historial se escribe explícitamente en
/// el esquema del módulo, y hay un test de integración que lo comprueba <b>mirando la tabla</b>,
/// no la configuración.
/// </para>
/// <para>
/// <see cref="Configurar"/> es el único sitio donde se dicen esas tres cosas —proveedor,
/// historial y convención de nombres—, y lo usan por igual el <i>composition root</i> y el
/// arranque de los tests. Dos cableados que se pueden separar acaban separándose, y el que se
/// prueba deja de ser el que se despliega.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa por la que filtra el inquilinato (R8).</param>
public sealed class OrganizacionDbContext(
    DbContextOptions<OrganizacionDbContext> opciones,
    IInquilinoActual inquilino)
    : ContextoDeModulo(opciones, inquilino)
{
    /// <summary>
    /// Esquema de PostgreSQL del módulo: el nombre del módulo en minúsculas y sin acentos.
    /// </summary>
    /// <remarks>
    /// La convención vale para los dieciséis módulos del §5 y está enumerada en <c>docs/PLAN.md</c>.
    /// El Anexo A.1 abreviaba este —y solo este— a <c>org</c>; se corrigió el anexo en vez de
    /// quedarse con una excepción, porque una regla con una excepción es una regla que el próximo
    /// módulo redescubre a medias.
    /// </remarks>
    public const string Esquema = "organizacion";

    /// <summary>
    /// Tabla de historial de migraciones, DENTRO del esquema del módulo. El nombre va en
    /// <c>snake_case</c> como el resto; lo que no es negociable es el esquema.
    /// </summary>
    public const string TablaDelHistorial = "__historial_de_migraciones";

    /// <summary>Empresas.</summary>
    public DbSet<Empresa> Empresas => Set<Empresa>();

    /// <summary>Ejercicios contables.</summary>
    public DbSet<Ejercicio> Ejercicios => Set<Ejercicio>();

    /// <summary>Series documentales.</summary>
    public DbSet<Serie> Series => Set<Serie>();

    /// <summary>Almacenes.</summary>
    public DbSet<Almacen> Almacenes => Set<Almacen>();

    /// <summary>
    /// Cablea el contexto contra PostgreSQL. Único sitio donde se dice el proveedor, dónde vive
    /// el historial de migraciones y qué convención de nombres se aplica.
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        opciones
            .UseNpgsql(cadenaDeConexion, npgsql => npgsql
                .MigrationsHistoryTable(TablaDelHistorial, Esquema)
                .MigrationsAssembly(typeof(OrganizacionDbContext).Assembly.FullName))

            // `snake_case` en toda la base de datos (§3): identificadores sin comillas, que es
            // lo que espera cualquiera que abra una consola de psql.
            .UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizacionDbContext).Assembly);

        // R8, una línea por entidad y a la vista. `EmpresaDelFiltro` es una propiedad de la
        // instancia: EF Core la lee en CADA consulta, no al construir el modelo. Y `== null` no es
        // una válvula de escape silenciosa: la propiedad solo devuelve nulo dentro de un ámbito sin
        // inquilino abierto a propósito y con su motivo; fuera de él, lanza.
        modelBuilder.Entity<Ejercicio>().HasQueryFilter(
            ejercicio => EmpresaDelFiltro == null || ejercicio.EmpresaId == EmpresaDelFiltro);
        modelBuilder.Entity<Serie>().HasQueryFilter(
            serie => EmpresaDelFiltro == null || serie.EmpresaId == EmpresaDelFiltro);
        modelBuilder.Entity<Almacen>().HasQueryFilter(
            almacen => EmpresaDelFiltro == null || almacen.EmpresaId == EmpresaDelFiltro);

        // La empresa es la RAÍZ del inquilinato: no lleva `empresa_id` porque ella ES el
        // inquilino, así que se filtra por su propia clave. La consecuencia buscada es que el
        // padrón de empresas de la instalación deje de ser legible desde dentro de cualquiera de
        // ellas: sin esto, `GET /organizacion/empresas` devuelve la razón social y el NIF de todos
        // los clientes de quien explote el sistema. Dar de alta una empresa y administrarla desde
        // fuera —que es real, y es el arranque en frío del 0.5— pasa por un ámbito con su motivo.
        modelBuilder.Entity<Empresa>().HasQueryFilter(
            empresa => EmpresaDelFiltro == null || empresa.Id == EmpresaDelFiltro);
    }
}
