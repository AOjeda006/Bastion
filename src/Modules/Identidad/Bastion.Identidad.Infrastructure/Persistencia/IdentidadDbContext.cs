using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Bastion.Identidad.Domain.Roles;
using Bastion.Identidad.Domain.Sesiones;
using Bastion.Identidad.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Identidad.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core del módulo Identidad: su esquema, sus tablas y su propio historial de
/// migraciones.
/// </summary>
/// <remarks>
/// <para>
/// <b>Este es el módulo que comprueba de verdad lo que el 0.4 solo pudo afirmar.</b> Con un único
/// módulo, el historial de migraciones podía estar en <c>public.__EFMigrationsHistory</c> y no
/// pasaba nada: no había nadie con quien chocar. Con dos, el sitio compartido es una avería
/// esperando —el segundo módulo lee allí las migraciones del primero, se cree al día y no aplica
/// las suyas—, y el fallo no sale por pantalla: sale como un esquema incompleto en producción.
/// </para>
/// <para>
/// Por eso el historial va dentro del esquema del módulo, y por eso el test de integración lo
/// comprueba <b>mirando las tablas</b>, no la configuración: leer la configuración solo demuestra
/// que la configuración dice lo que dice.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa por la que filtra el inquilinato (R8).</param>
public sealed class IdentidadDbContext(
    DbContextOptions<IdentidadDbContext> opciones,
    IInquilinoActual inquilino)
    : ContextoDeModulo(opciones, inquilino)
{
    /// <summary>
    /// Esquema de PostgreSQL del módulo: el nombre del módulo en minúsculas y sin acentos.
    /// </summary>
    public const string Esquema = "identidad";

    /// <summary>
    /// Tabla de historial de migraciones, DENTRO del esquema del módulo.
    /// </summary>
    public const string TablaDelHistorial = "__historial_de_migraciones";

    /// <summary>Cuentas de usuario.</summary>
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    /// <summary>Pertenencias de un usuario a una empresa.</summary>
    public DbSet<Membresia> Membresias => Set<Membresia>();

    /// <summary>Roles, que son agrupaciones de permisos.</summary>
    public DbSet<Rol> Roles => Set<Rol>();

    /// <summary>Emisiones de token de refresco.</summary>
    public DbSet<TokenDeRefresco> TokensDeRefresco => Set<TokenDeRefresco>();

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
                .MigrationsAssembly(typeof(IdentidadDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentidadDbContext).Assembly);

        // La tabla de traza, apuntando al esquema `auditoria` y marcada para NO migrarse desde
        // aquí: la crea el módulo Auditoría, que es su dueño. Se mapea en este contexto porque es
        // lo que permite que la traza se añada en el MISMO `SaveChanges` que el cambio, sin
        // transacción explícita en ningún caso de uso (ADR-0012).
        ConfiguracionDeAuditoria.Mapear(modelBuilder, migra: false);

        // Y la bandeja de salida, por lo mismo. Este módulo todavía no emite ningún evento; la
        // mapea igual para que el día que lo emita no haya que acordarse de venir aquí, y para
        // que el barrido del inquilinato la vea también desde este contexto.
        ConfiguracionDeLaBandeja.Mapear(modelBuilder, migra: false);

        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(
            registro => EmpresaDelFiltro == null || registro.EmpresaId == EmpresaDelFiltro);

        // La cola de eventos es un dato de la empresa que los emitió: sin filtro, la primera
        // consulta que se escriba sobre esta tabla enseñaría los hechos de todos los clientes de
        // la instalación desde dentro de cualquiera de ellos. El publicador la ve entera porque
        // abre un ámbito con su motivo, no porque aquí falte una línea.
        modelBuilder.Entity<EventoDeLaBandeja>().HasQueryFilter(
            evento => EmpresaDelFiltro == null || evento.EmpresaId == EmpresaDelFiltro);

        // La pertenencia es el PUENTE del inquilinato: lleva `empresa_id` y se filtra por él.
        // Filtrarla tiene una consecuencia que hay que mirar de frente: el acceso carga al usuario
        // con TODAS sus pertenencias para saber a qué empresas puede entrar, y esa carga ocurre
        // antes de que haya empresa activa. Corre dentro de un ámbito con su motivo
        // (`AutenticacionYSesion`), que es lo que la deja ver la lista entera; el resto del sistema
        // solo ve las de la empresa en la que está.
        modelBuilder.Entity<Membresia>().HasQueryFilter(
            membresia => EmpresaDelFiltro == null || membresia.EmpresaId == EmpresaDelFiltro);

        // El usuario NO lleva `empresa_id`: una cuenta es una, con un correo, y puede pertenecer a
        // varias empresas. Pero «global» no puede significar «consultable desde cualquier
        // empresa»: sin este filtro, quien tenga `identidad.usuario.ver` en una empresa lee el
        // correo y el nombre de los usuarios de todas las demás enumerando identificadores. Así
        // que la entidad es global y la CONSULTA se acota por la pertenencia, que es la relación
        // que dice quién comparte empresa con quién.
        modelBuilder.Entity<Usuario>().HasQueryFilter(
            usuario => EmpresaDelFiltro == null
                || usuario.Membresias.Any(membresia => membresia.EmpresaId == EmpresaDelFiltro));
    }
}
