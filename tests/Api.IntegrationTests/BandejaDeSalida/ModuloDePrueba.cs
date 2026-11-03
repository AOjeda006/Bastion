using System.Collections.Concurrent;
using Bastion.Api.IntegrationTests.Persistencia;
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

    /// <summary>La empresa con la que escribe un manejador: la activa, que es la de su fila.</summary>
    /// <exception cref="InvalidOperationException">
    /// Si el manejador corre bajo un ámbito sin inquilino, que no tiene empresa con la que escribir.
    /// </exception>
    public Guid EmpresaActiva => EmpresaDelFiltro ?? throw new InvalidOperationException(
        "El manejador corre bajo un ámbito sin inquilino, y no tiene empresa con la que escribir.");

    /// <summary>Una base nueva, con todo migrado y la tabla del módulo de prueba creada.</summary>
    /// <param name="postgres">El contenedor compartido.</param>
    /// <returns>La cadena de conexión de la base.</returns>
    public static async Task<string> BaseNuevaAsync(PostgresConTodosLosModulos postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);

        string cadena = await postgres.CrearBaseNuevaAsync(migrada: true);

        await CrearTablaAsync(cadena);

        return cadena;
    }

    /// <summary>Deja un documento a pelo, sin EF: sin filtro, sin guarda y sin traza.</summary>
    /// <param name="cadena">La base del caso.</param>
    /// <param name="empresaId">La empresa del documento.</param>
    /// <param name="marca">Su marca.</param>
    public static async Task SembrarAsync(string cadena, Guid empresaId, string marca)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand sembrar = new(
            $"INSERT INTO {Esquema}.{Tabla} (id, empresa_id, marca, veces, creado_en, modificado_en) " +
            "VALUES (gen_random_uuid(), @empresa, @marca, 1, now(), now())",
            conexion);
        sembrar.Parameters.AddWithValue("empresa", empresaId);
        sembrar.Parameters.AddWithValue("marca", marca);

        await sembrar.ExecuteNonQueryAsync();
    }

    /// <summary>Las marcas de los documentos de un caso, en orden, leídas a pelo y sin filtro.</summary>
    /// <param name="cadena">La base del caso.</param>
    /// <param name="marca">La marca del caso, que es el principio de las de sus documentos.</param>
    /// <returns>Las marcas.</returns>
    public static async Task<List<string>> MarcasAsync(string cadena, string marca)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand consulta = new(
            $"SELECT marca FROM {Esquema}.{Tabla} WHERE marca LIKE @marca || '%' ORDER BY marca",
            conexion);
        consulta.Parameters.AddWithValue("marca", marca);

        List<string> marcas = [];

        await using NpgsqlDataReader lector = await consulta.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            marcas.Add(lector.GetString(0));
        }

        return marcas;
    }

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

    /// <summary>Lo cuenta una vez más: una modificación, que guarda con su testigo de concurrencia.</summary>
    public void Contar() => Veces++;
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

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(contexto.EmpresaActiva, evento.Marca));

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
            DocumentoDePrueba.Nuevo(contexto.EmpresaActiva, $"{evento.Marca}/primero"));

        await contexto.SaveChangesAsync(cancelacion);

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(contexto.EmpresaActiva, evento.Marca));

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

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(contexto.EmpresaActiva, evento.Marca));

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

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(contexto.EmpresaActiva, evento.Marca));

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
            DocumentoDePrueba.Nuevo(contexto.EmpresaActiva, $"{evento.Marca}/a-medias"));

        await contexto.SaveChangesAsync(cancelacion);

        contexto.Documentos.Add(
            DocumentoDePrueba.Nuevo(contexto.EmpresaActiva, $"{evento.Marca}/sobrante"));
    }
}

/// <summary>Un manejador que solo lee: apunta qué documentos de su caso ve.</summary>
/// <remarks>
/// Los lee por EF, con el filtro del inquilinato puesto. En una petición vería los de la empresa
/// de su <i>claim</i>; detrás del despachador, los de la empresa de su fila.
/// </remarks>
/// <param name="contexto">El contexto de su módulo.</param>
public sealed class ManejadorQueMiraLoQueVe(ContextoDePrueba contexto) : ManejadorDeEvento<HechoDePrueba>
{
    private static readonly ConcurrentDictionary<string, List<string>> s_vistos = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-mira";

    /// <summary>Qué documentos vio al atender el hecho de esa marca.</summary>
    /// <param name="marca">La marca del hecho.</param>
    /// <returns>Las marcas de los documentos que vio, en orden; ninguna si no lo atendió.</returns>
    public static IReadOnlyList<string> LoQueVio(string marca) => s_vistos.GetValueOrDefault(marca) ?? [];

    /// <inheritdoc/>
    protected override async Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        s_vistos[evento.Marca] = await contexto.Documentos
            .Where(documento => EF.Functions.Like(documento.Marca, evento.Marca + "%"))
            .OrderBy(documento => documento.Marca)
            .Select(documento => documento.Marca)
            .ToListAsync(cancelacion);
    }
}

/// <summary>Un manejador que escribe su documento en una empresa que no es la de su fila.</summary>
/// <remarks>
/// Es el fallo que la guarda de la auditoría para en una petición: el filtro no interviene en un
/// <c>INSERT</c>, y sin la guarda la fila nacería en otra empresa sin dar error.
/// </remarks>
/// <param name="contexto">El contexto de su módulo.</param>
public sealed class ManejadorQueEscribeEnOtraEmpresa(ContextoDePrueba contexto) : ManejadorDeEvento<HechoDePrueba>
{
    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-escribe-en-otra-empresa";

    /// <inheritdoc/>
    protected override async Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        contexto.Documentos.Add(DocumentoDePrueba.Nuevo(Guid.CreateVersion7(), evento.Marca));

        await contexto.SaveChangesAsync(cancelacion);
    }
}

/// <summary>Un manejador que choca con otra escritura las veces que se le diga, y después cuenta.</summary>
/// <remarks>
/// <para>
/// El choque es de dos transacciones de verdad (3.4, hueco 5): lee su documento en la del
/// despachador, otra conexión lo cambia y confirma, y al guardar, el <c>xmin</c> que leyó ya no es
/// el de la fila. EF Core lo dice con un <see cref="DbUpdateConcurrencyException"/>, como con dos
/// peticiones que cambian el mismo documento a la vez.
/// </para>
/// <para>
/// Apunta el contexto de cada intento, para que el caso vea que cada reintento lleva uno nuevo.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de su módulo.</param>
public sealed class ManejadorQueChoca(ContextoDePrueba contexto) : ManejadorDeEvento<HechoDePrueba>
{
    private static readonly ConcurrentDictionary<string, int> s_choquesQueQuedan = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, ConcurrentQueue<Guid>> s_contextos =
        new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public override string Consumidor => "pruebas.el-que-choca";

    /// <summary>Cuántas veces más chocará al atender el hecho de esa marca. Con cero, deja de chocar.</summary>
    /// <param name="marca">La marca del hecho.</param>
    /// <param name="veces">Las veces.</param>
    public static void Chocara(string marca, int veces) => s_choquesQueQuedan[marca] = veces;

    /// <summary>El contexto de cada intento de atender el hecho de esa marca, en orden.</summary>
    /// <param name="marca">La marca del hecho.</param>
    /// <returns>Uno por intento; ninguno si no lo ha intentado.</returns>
    public static IReadOnlyList<Guid> ContextosDe(string marca) =>
        s_contextos.TryGetValue(marca, out ConcurrentQueue<Guid>? suyos) ? [.. suyos] : [];

    /// <inheritdoc/>
    protected override async Task AtenderAsync(HechoDePrueba evento, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(evento);

        s_contextos.GetOrAdd(evento.Marca, _ => new()).Enqueue(contexto.ContextId.InstanceId);

        DocumentoDePrueba documento = await contexto.Documentos
            .SingleAsync(candidato => candidato.Marca == evento.Marca, cancelacion);

        if (QuedaPorChocar(evento.Marca))
        {
            await OtraTransaccionLoCambiaAsync(documento.Id, cancelacion);
        }

        documento.Contar();

        await contexto.SaveChangesAsync(cancelacion);
    }

    private static bool QuedaPorChocar(string marca) =>
        s_choquesQueQuedan.TryGetValue(marca, out int quedan)
        && quedan > 0
        && s_choquesQueQuedan.TryUpdate(marca, quedan - 1, quedan);

    // Otra conexión, y por tanto otra transacción, que cambia el documento y confirma mientras la
    // del manejador lo tiene leído y sin guardar.
    private async Task OtraTransaccionLoCambiaAsync(Guid documentoId, CancellationToken cancelacion)
    {
        await using NpgsqlConnection otra = new(contexto.Database.GetConnectionString());
        await otra.OpenAsync(cancelacion);

        await using NpgsqlCommand cambiar = new(
            $"UPDATE {ContextoDePrueba.Esquema}.{ContextoDePrueba.Tabla} SET veces = veces + 1 WHERE id = @id",
            otra);
        cambiar.Parameters.AddWithValue("id", documentoId);

        if (await cambiar.ExecuteNonQueryAsync(cancelacion) != 1)
        {
            throw new InvalidOperationException("La otra transacción no ha encontrado el documento.");
        }
    }
}
