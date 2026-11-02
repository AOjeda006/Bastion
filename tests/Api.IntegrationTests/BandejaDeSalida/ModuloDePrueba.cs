using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Eventos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Domain.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// El contexto de un módulo que solo existe en los tests: donde escriben los manejadores con efecto.
/// </summary>
/// <remarks>
/// <para>
/// Es un contexto de módulo como los de verdad: hereda de <see cref="ContextoDeModulo"/>, mapea la
/// traza y la bandeja sin migrarlas, lleva los tres interceptores en el mismo orden que los
/// módulos (los pone <see cref="BandejaDeVerdad"/>) y su documento lleva el testigo de
/// concurrencia. Con un contexto de un módulo de verdad, el manejador de prueba escribiría en sus
/// tablas, y un test de la fontanería dependería de las reglas de un negocio.
/// </para>
/// <para>
/// <b>Lo único que no tiene es migración.</b> Su tabla la crea <see cref="CrearTablaAsync"/> en una
/// base nueva, la de cada caso, así que ningún otro test la ve: ni los barridos del esquema, ni el
/// publicador de la API, que se encontraría respuestas de prueba que no declara.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">La empresa activa, la del filtro.</param>
/// <param name="bloqueados">Si el ámbito ve lo bloqueado.</param>
public sealed class ContextoDePrueba(
    DbContextOptions<ContextoDePrueba> opciones,
    IInquilinoActual inquilino,
    IAccesoALoBloqueado bloqueados)
    : ContextoDeModulo(opciones, inquilino, bloqueados)
{
    /// <summary>El esquema del módulo de prueba.</summary>
    public const string Esquema = "pruebas_de_la_bandeja";

    /// <summary>La tabla de sus documentos.</summary>
    public const string Tabla = "documentos";

    /// <summary>Los documentos que escriben los manejadores.</summary>
    public DbSet<DocumentoDePrueba> Documentos => Set<DocumentoDePrueba>();

    /// <summary>Crea el esquema y la tabla del módulo de prueba, a mano, en una base nueva.</summary>
    /// <param name="cadenaDeConexion">La base, ya con Auditoría migrada.</param>
    public static async Task CrearTablaAsync(string cadenaDeConexion)
    {
        await using NpgsqlConnection conexion = new(cadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand crear = new(
            $"""
            CREATE SCHEMA {Esquema};
            CREATE TABLE {Esquema}.{Tabla} (
                id uuid PRIMARY KEY,
                empresa_id uuid NOT NULL,
                marca text NOT NULL,
                veces integer NOT NULL,
                creado_en timestamp with time zone NOT NULL,
                modificado_en timestamp with time zone NOT NULL);
            """,
            conexion);

        await crear.ExecuteNonQueryAsync();
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Esquema);

        ConfiguracionDeAuditoria.Mapear(modelBuilder, migra: false);
        ConfiguracionDeLaBandeja.Mapear(modelBuilder, migra: false);

        modelBuilder.Entity<DocumentoDePrueba>(documento =>
        {
            documento.ToTable(Tabla);
            documento.HasKey(fila => fila.Id);
            documento.Property(fila => fila.EmpresaId).SeAudita();
            documento.Property(fila => fila.Marca).IsRequired().SeAudita();
            documento.Property(fila => fila.Veces).SeAudita();
            documento.LlevaTestigoDeConcurrencia();
            documento.SeAudita();
            documento.HasQueryFilter(
                "Inquilinato", fila => EmpresaDelFiltro == null || fila.EmpresaId == EmpresaDelFiltro);
        });

        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(
            "Inquilinato", registro => EmpresaDelFiltro == null || registro.EmpresaId == EmpresaDelFiltro);

        modelBuilder.Entity<EventoDeLaBandeja>().HasQueryFilter(
            "Inquilinato", evento => EmpresaDelFiltro == null || evento.EmpresaId == EmpresaDelFiltro);
    }
}

/// <summary>El documento del módulo de prueba: una raíz de agregado con su empresa.</summary>
/// <remarks>
/// Al nacer registra su <see cref="RespuestaDePrueba"/>, que es el evento de respuesta de un
/// manejador: lo vuelca el interceptor de la bandeja del contexto, en el mismo <c>SaveChanges</c>.
/// </remarks>
public sealed class DocumentoDePrueba : RaizAgregado, IDeInquilino
{
    // Constructor para EF Core.
    private DocumentoDePrueba() => Marca = string.Empty;

    private DocumentoDePrueba(Guid empresaId, string marca, DateTimeOffset momento)
        : base(momento)
    {
        Id = Guid.CreateVersion7();
        EmpresaId = empresaId;
        Marca = marca;
        Veces = 1;
    }

    /// <summary>La empresa con la que escriben los manejadores mientras no reciben la de su fila.</summary>
    public static Guid EmpresaDePrueba { get; } = Guid.Parse("0196f3a0-7c00-7000-8000-000000003401");

    /// <summary>Identificador del documento.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>Qué caso y qué manejador lo han escrito.</summary>
    public string Marca { get; private set; }

    /// <summary>Cuántas veces se ha contado.</summary>
    public int Veces { get; private set; }

    /// <summary>Un documento nuevo, con su respuesta registrada.</summary>
    /// <param name="empresaId">La empresa del documento.</param>
    /// <param name="marca">Qué caso y qué manejador lo escriben.</param>
    /// <returns>El documento.</returns>
    public static DocumentoDePrueba Nuevo(Guid empresaId, string marca)
    {
        DocumentoDePrueba documento = new(empresaId, marca, DateTimeOffset.UtcNow);

        documento.Registrar(new RespuestaDePrueba(marca));

        return documento;
    }
}

/// <summary>El evento de respuesta de un manejador de prueba.</summary>
/// <param name="Marca">La marca del documento que lo registró.</param>
public sealed record RespuestaDePrueba(string Marca) : EventoDeIntegracion
{
    /// <summary>Con qué nombre viaja en la cola.</summary>
    public const string Nombre = "pruebas.respuesta-de-prueba";
}

/// <summary>Un manejador con efecto: escribe su documento y lo confirma.</summary>
/// <param name="contexto">El contexto de su módulo, el mismo en el que el despachador abre la transacción.</param>
public sealed class ManejadorQueEscribe(ContextoDePrueba contexto) : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-escribe";

    /// <inheritdoc/>
    protected override async Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(DocumentoDePrueba.EmpresaDePrueba, evento.Marca));

        await contexto.SaveChangesAsync(cancelacion);
    }
}

/// <summary>Un manejador que confirma dos veces: un documento, y después otro.</summary>
/// <remarks>
/// La huella entra con el primer <c>SaveChanges</c>, y el documento con la marca del evento, su
/// traza y su respuesta, con el segundo. Con puntos de guardado, cada uno abriría su
/// subtransacción y las filas de los dos llevarían <c>xmin</c> distintos.
/// </remarks>
/// <param name="contexto">El contexto de su módulo.</param>
public sealed class ManejadorQueConfirmaDosVeces(ContextoDePrueba contexto) : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-confirma-dos-veces";

    /// <inheritdoc/>
    protected override async Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        contexto.Documentos.Add(
            DocumentoDePrueba.Nuevo(DocumentoDePrueba.EmpresaDePrueba, $"{evento.Marca}/primero"));

        await contexto.SaveChangesAsync(cancelacion);

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(DocumentoDePrueba.EmpresaDePrueba, evento.Marca));

        await contexto.SaveChangesAsync(cancelacion);
    }
}

/// <summary>Un manejador que confirma su efecto y revienta después.</summary>
/// <remarks>
/// Es el caso que la transacción del despachador cierra: lo confirmado ANTES de fallar no puede
/// quedarse, porque la huella no se ha escrito y el evento se va a reintentar.
/// </remarks>
/// <param name="contexto">El contexto de su módulo.</param>
public sealed class ManejadorQueEscribeYFalla(ContextoDePrueba contexto) : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-escribe-y-falla";

    /// <inheritdoc/>
    protected override async Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(DocumentoDePrueba.EmpresaDePrueba, evento.Marca));

        await contexto.SaveChangesAsync(cancelacion);

        throw new InvalidOperationException("Lo escrito ya está guardado, y aun así no ha ido bien.");
    }
}

/// <summary>Un manejador que deja su documento en el contexto y no lo confirma.</summary>
/// <remarks>
/// Es lo que hace un caso de uso que dice que no: no llama a confirmar. Detrás de HTTP, lo que dejó
/// en memoria se va con la petición, y detrás del despachador tiene que pasar lo mismo.
/// </remarks>
/// <param name="contexto">El contexto de su módulo.</param>
public sealed class ManejadorQueNoConfirma(ContextoDePrueba contexto) : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-no-confirma";

    /// <inheritdoc/>
    protected override Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(DocumentoDePrueba.EmpresaDePrueba, evento.Marca));

        return Task.CompletedTask;
    }
}

/// <summary>Un manejador que confirma un documento y deja otro a medias.</summary>
/// <remarks>
/// El de <c>/a-medias</c> se confirma, y el <c>/sobrante</c> se queda en el contexto. Con un contexto
/// compartido entre manejadores, el <c>SaveChanges</c> del siguiente se lo llevaría puesto.
/// </remarks>
/// <param name="contexto">El contexto de su módulo.</param>
public sealed class ManejadorQueDejaAlgoAMedias(ContextoDePrueba contexto) : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-deja-algo-a-medias";

    /// <inheritdoc/>
    protected override async Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        contexto.Documentos.Add(
            DocumentoDePrueba.Nuevo(DocumentoDePrueba.EmpresaDePrueba, $"{evento.Marca}/a-medias"));

        await contexto.SaveChangesAsync(cancelacion);

        contexto.Documentos.Add(
            DocumentoDePrueba.Nuevo(DocumentoDePrueba.EmpresaDePrueba, $"{evento.Marca}/sobrante"));
    }
}
