using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Divisas;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Impuestos;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Domain.Ubicaciones;
using Bastion.Organizacion.Domain.Unidades;
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
/// <param name="bloqueados">De dónde sale el permiso para ver lo bloqueado (R16).</param>
public sealed class OrganizacionDbContext(
    DbContextOptions<OrganizacionDbContext> opciones,
    IInquilinoActual inquilino,
    IAccesoALoBloqueado bloqueados)
    : ContextoDeModulo(opciones, inquilino, bloqueados)
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

    /// <summary>Ubicaciones dentro de los almacenes.</summary>
    public DbSet<Ubicacion> Ubicaciones => Set<Ubicacion>();

    /// <summary>Tipos impositivos, por tramos de vigencia. Maestro de la instalación (R8).</summary>
    public DbSet<Impuesto> Impuestos => Set<Impuesto>();

    /// <summary>Divisas con las que se opera. Maestro de la instalación (R8).</summary>
    public DbSet<Divisa> Divisas => Set<Divisa>();

    /// <summary>Tipos de cambio por par de divisas y día. Maestro de la instalación (R8).</summary>
    public DbSet<TipoCambio> TiposDeCambio => Set<TipoCambio>();

    /// <summary>Unidades de medida. Maestro de la instalación (R8).</summary>
    public DbSet<UnidadMedida> UnidadesDeMedida => Set<UnidadMedida>();

    /// <summary>Conversiones entre unidades. Maestro de la instalación (R8).</summary>
    public DbSet<ConversionUM> ConversionesDeUnidades => Set<ConversionUM>();

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

        // La tabla de traza, apuntando al esquema `auditoria` y marcada para NO migrarse desde
        // aquí: la crea el módulo Auditoría, que es su dueño. Se mapea en este contexto porque es
        // lo que permite que la traza se añada en el MISMO `SaveChanges` que el cambio, sin
        // transacción explícita en ningún caso de uso (ADR-0012).
        ConfiguracionDeAuditoria.Mapear(modelBuilder, migra: false);

        // Y la bandeja de salida, igual: se mapea aquí para que el evento entre en la misma
        // transacción que el cambio, y la migra el módulo Auditoría (ADR-0013).
        ConfiguracionDeLaBandeja.Mapear(modelBuilder, migra: false);

        // Y la tabla de claves de idempotencia (R10), otra vez igual: se mapea aquí para que el
        // recibo de la petición caiga en la misma transacción que el trabajo, y la migra el
        // módulo Auditoría.
        ConfiguracionDeIdempotencia.Mapear(modelBuilder, migra: false);

        // R8, una línea por entidad y a la vista. `EmpresaDelFiltro` es una propiedad de la
        // instancia: EF Core la lee en CADA consulta, no al construir el modelo. Y `== null` no es
        // una válvula de escape silenciosa: la propiedad solo devuelve nulo dentro de un ámbito sin
        // inquilino abierto a propósito y con su motivo; fuera de él, lanza.
        modelBuilder.Entity<Ejercicio>().HasQueryFilter(
            "Inquilinato", ejercicio => EmpresaDelFiltro == null || ejercicio.EmpresaId == EmpresaDelFiltro);
        modelBuilder.Entity<Serie>().HasQueryFilter(
            "Inquilinato", serie => EmpresaDelFiltro == null || serie.EmpresaId == EmpresaDelFiltro);
        modelBuilder.Entity<Almacen>().HasQueryFilter(
            "Inquilinato", almacen => EmpresaDelFiltro == null || almacen.EmpresaId == EmpresaDelFiltro);
        modelBuilder.Entity<Ubicacion>().HasQueryFilter(
            "Inquilinato", ubicacion => EmpresaDelFiltro == null || ubicacion.EmpresaId == EmpresaDelFiltro);

        // Impuesto, Divisa, TipoCambio, UnidadMedida y ConversionUM NO llevan filtro, y su
        // ausencia está declarada —una por una y con su motivo— en la lista de globales de
        // `CadaEntidadDeclaraSuInquilinatoTests`. Es lo que la R8 llama marcar explícitamente los
        // maestros que se comparten entre sociedades: el tipo general del IVA lo fija el BOE y el
        // euro es el euro en todas. La consecuencia asumida es que un impuesto o una unidad
        // creados desde una empresa se ven desde las demás, igual que un rol (ADR-0011).

        // R16, con la misma forma y en el mismo sitio: una línea por entidad bloqueable, a la
        // vista. `VerLoBloqueado` es otra propiedad de instancia, y vale `true` solo dentro de un
        // ámbito abierto a propósito y con su motivo. Es el filtro DE REPOSITORIO que pide el
        // art. 32: lo bloqueado no se ve porque la consulta no lo trae, no porque la pantalla lo
        // esconda.
        modelBuilder.Entity<Almacen>().HasQueryFilter(
            "Bloqueo", almacen => VerLoBloqueado || !almacen.Bloqueo.EstaBloqueado);
        modelBuilder.Entity<Ubicacion>().HasQueryFilter(
            "Bloqueo", ubicacion => VerLoBloqueado || !ubicacion.Bloqueo.EstaBloqueado);

        // La empresa es la RAÍZ del inquilinato: no lleva `empresa_id` porque ella ES el
        // inquilino, así que se filtra por su propia clave. La consecuencia buscada es que el
        // padrón de empresas de la instalación deje de ser legible desde dentro de cualquiera de
        // ellas: sin esto, `GET /organizacion/empresas` devuelve la razón social y el NIF de todos
        // los clientes de quien explote el sistema. Dar de alta una empresa y administrarla desde
        // fuera —que es real, y es el arranque en frío del 0.5— pasa por un ámbito con su motivo.
        modelBuilder.Entity<Empresa>().HasQueryFilter(
            "Inquilinato", empresa => EmpresaDelFiltro == null || empresa.Id == EmpresaDelFiltro);

        modelBuilder.Entity<Empresa>().HasQueryFilter(
            "Bloqueo", empresa => VerLoBloqueado || !empresa.Bloqueo.EstaBloqueado);

        // Una traza es un dato: dice qué NIF tenía antes una empresa y quién lo cambió. Filtra
        // igual que todo lo demás, y por su propia columna, que es anulable — las filas sin empresa
        // (la semilla, el acceso) no son de nadie y desde dentro de una empresa no se ven.
        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(
            "Inquilinato", registro => EmpresaDelFiltro == null || registro.EmpresaId == EmpresaDelFiltro);

        // La cola de eventos es un dato de la empresa que los emitió: sin filtro, la primera
        // consulta que se escriba sobre esta tabla enseñaría los hechos de todos los clientes de
        // la instalación desde dentro de cualquiera de ellos. El publicador la ve entera porque
        // abre un ámbito con su motivo, no porque aquí falte una línea.
        modelBuilder.Entity<EventoDeLaBandeja>().HasQueryFilter(
            "Inquilinato", evento => EmpresaDelFiltro == null || evento.EmpresaId == EmpresaDelFiltro);

        // Y el recibo de las peticiones repetibles (R10), por lo mismo que la cola: es un dato
        // de la empresa que la pidió. Sin filtro, una consulta sobre esta tabla enseñaría desde
        // dentro de una empresa qué está dando de alta otra, y con qué respuesta.
        modelBuilder.Entity<RegistroDeIdempotencia>().HasQueryFilter(
            "Inquilinato", recibo => EmpresaDelFiltro == null || recibo.EmpresaId == EmpresaDelFiltro);
    }
}
