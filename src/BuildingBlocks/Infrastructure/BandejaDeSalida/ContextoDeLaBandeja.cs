using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// El contexto con el que el trabajo de fondo <b>lee</b> la bandeja. No crea nada: las dos tablas
/// las migra el módulo Auditoría.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué uno propio y no el de un módulo.</b> El mecanismo de la bandeja vive en los bloques
/// comunes (§12) y no puede depender de ningún módulo — sería la frontera del §4 cruzada al revés,
/// y por el proyecto que todos referencian. Con un contexto propio, el publicador se cablea sin
/// nombrar a Organización, a Identidad ni a Auditoría, y el día que haya dieciséis módulos sigue
/// sin nombrarlos.
/// </para>
/// <para>
/// <b>Y filtra por empresa, como todos.</b> Es un contexto que ve una tabla con <c>empresa_id</c>:
/// dejarlo sin filtro sería abrir un segundo mecanismo para saltarse el inquilinato, sin lista
/// cerrada y sin dejar rastro. El publicador ve la cola entera porque abre un ámbito con
/// <see cref="MotivoSinInquilino.PublicacionDeEventos"/>, que es explícito, queda anotado en el
/// registro y está en la lista blanca que compara <c>ElFiltroNoSeSaltaPorAhiTests</c>.
/// </para>
/// <para>
/// <b>Quién lo apunta a PostgreSQL no es este proyecto.</b> Los bloques comunes traen EF Core pero
/// no el proveedor —a propósito: aquí no se sabe contra qué base corre el sistema—, así que el
/// <c>AddDbContext</c> de este contexto lo hace el módulo Auditoría, que es el que crea y migra las
/// dos tablas. Sin <c>MigrationsHistoryTable</c> ni ensamblado de migraciones: este contexto no
/// migra nada y no debe poder hacerlo.
/// </para>
/// <para>
/// <b>Sin interceptor de auditoría</b>, por lo mismo que el contexto del módulo Auditoría: lo único
/// que escribe —una fila marcada como publicada, una huella de consumidor— está clasificado como
/// no auditable, así que engancharlo sería pagar un barrido por cada vuelta del publicador para no
/// producir ni una fila.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa por la que filtra el inquilinato (R8).</param>
public sealed class ContextoDeLaBandeja(
    DbContextOptions<ContextoDeLaBandeja> opciones,
    IInquilinoActual inquilino)
    : ContextoDeModulo(opciones, inquilino)
{
    /// <summary>La cola de eventos por publicar.</summary>
    public DbSet<EventoDeLaBandeja> Bandeja => Set<EventoDeLaBandeja>();

    /// <summary>Qué consumidor ha atendido ya qué evento.</summary>
    public DbSet<EventoProcesado> Procesados => Set<EventoProcesado>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Las dos tablas llevan su esquema escrito en el mapeo compartido, así que este contexto no
        // declara esquema por omisión: no tiene ninguno propio, porque no es de ningún módulo.
        ConfiguracionDeLaBandeja.Mapear(modelBuilder, migra: false);

        modelBuilder.Entity<EventoDeLaBandeja>().HasQueryFilter(
            evento => EmpresaDelFiltro == null || evento.EmpresaId == EmpresaDelFiltro);
    }
}
