using Bastion.Api.FunctionalTests.Salud;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Auditoria;

/// <summary>
/// La premisa de la que depende que el interceptor de auditoría sea de <b>una sola fase</b>:
/// ningún valor de los que van a la traza lo pone la base de datos.
/// </summary>
/// <remarks>
/// <para>
/// La receta canónica del interceptor de auditoría en EF Core es de dos fases: recoger en
/// <c>SavingChanges</c>, completar en <c>SavedChanges</c> las claves que ha generado la base y
/// volver a guardar. Existe porque en el caso general la clave de un <c>INSERT</c> no se sabe
/// hasta después de mandarlo.
/// </para>
/// <para>
/// Aquí no hace falta, porque las claves salen del constructor del dominio (ADR-0010). Pero eso
/// es una <b>propiedad del modelo de hoy</b>, no una ley: el día que alguien ponga una columna
/// <c>IDENTITY</c>, un <c>DEFAULT gen_random_uuid()</c>, una columna calculada o el testigo de
/// concurrencia <c>xmin</c> del 0.9, la segunda fase deja de ser ceremonia y pasa a ser
/// obligatoria — y con ella una segunda escritura que hay que meter en la misma transacción.
/// </para>
/// <para>
/// Por eso esto es un test y no un párrafo del ADR: el párrafo envejece en silencio, el test se
/// pone rojo. Si se pone rojo, <b>no se añade a la lista</b>: se reabre la decisión del ADR-0012.
/// </para>
/// </remarks>
public sealed class LasClavesSeConocenAntesDeGuardarTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ningun_valor_del_modelo_lo_pone_la_base_de_datos()
    {
        List<string> generadas = [.. Modelos().SelectMany(DondeGeneraLaBase)];

        generadas.ShouldBeEmpty(
            "si la base genera un valor, no se conoce hasta después del INSERT y el interceptor " +
            "de auditoría necesita una segunda fase. Reabre el ADR-0012 antes de tocar esta lista.");
    }

    [Fact]
    public void Toda_entidad_tiene_su_clave_completa_antes_de_guardar()
    {
        List<string> sinClave = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .SelectMany(tipo => (tipo.FindPrimaryKey()?.Properties ?? [])
                .Where(clave => clave.ValueGenerated.HasFlag(ValueGenerated.OnAdd)
                    && clave.GetValueGeneratorFactory() is null
                    && !EsClienteQuienLaPone(clave))
                .Select(clave => $"{tipo.ShortName()}.{clave.Name}"))];

        // `ValueGenerated.OnAdd` en una clave `Guid` NO significa que la ponga la base: EF Core
        // marca así las claves que se rellenan al insertar, y quien las rellena es el generador
        // del lado del cliente —o, como aquí, el constructor del dominio, que llega con el valor
        // puesto y EF lo respeta—. Lo que sí sería un problema es una clave `OnAdd` que además
        // dependiera del servidor, y de eso se ocupa el caso de arriba.
        sinClave.ShouldBeEmpty("estas claves no las pone ni el dominio ni el cliente");
    }

    private static bool EsClienteQuienLaPone(IProperty clave) =>
        clave.ClrType == typeof(Guid) || clave.ClrType == typeof(Guid?);

    private static IEnumerable<string> DondeGeneraLaBase(IModel modelo) =>
        modelo.GetEntityTypes()
            .SelectMany(tipo => tipo.GetProperties()
                .Where(EsDelServidor)
                .Select(propiedad => $"{tipo.ShortName()}.{propiedad.Name}"));

    // Las cinco formas que tiene un valor de venir del servidor, cada una con su nombre: un
    // DEFAULT, una columna calculada, una columna IDENTITY o serial —esta es de Npgsql, y es la
    // que de verdad distingue «la pone la base» de «la pone el cliente al insertar»—, algo que se
    // regenera en cada UPDATE (la forma de un `rowversion`), y el testigo de concurrencia, que
    // llega con el 0.9 y en PostgreSQL suele ser `xmin`.
    private static bool EsDelServidor(IProperty propiedad) =>
        propiedad.GetDefaultValueSql() is not null
        || propiedad.GetComputedColumnSql() is not null
        || propiedad.GetValueGenerationStrategy() != NpgsqlValueGenerationStrategy.None
        || propiedad.ValueGenerated == ValueGenerated.OnAddOrUpdate
        || propiedad.IsConcurrencyToken;

    private IEnumerable<IModel> Modelos()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        yield return alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>().Model;
        yield return alcance.ServiceProvider.GetRequiredService<IdentidadDbContext>().Model;
    }
}
