using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Auditoria;

/// <summary>
/// La premisa de la que depende que el interceptor de auditoría sea de <b>una sola fase</b>, y el
/// inventario cerrado de lo que en este modelo genera el servidor.
/// </summary>
/// <remarks>
/// <para>
/// La receta canónica del interceptor de auditoría en EF Core es de dos fases: recoger en
/// <c>SavingChanges</c>, completar en <c>SavedChanges</c> las claves que ha generado la base y
/// volver a guardar. Existe porque en el caso general la clave de un <c>INSERT</c> no se sabe
/// hasta después de mandarlo.
/// </para>
/// <para>
/// <b>Este fichero se puso rojo en el 0.9, como estaba anunciado</b>, en cuanto el testigo de
/// concurrencia entró en el modelo: <c>xmin</c> lo genera PostgreSQL en cada escritura. La
/// premisa se reenunció —ADR-0015, que sustituye al punto 2 del ADR-0012— y la comprobación se
/// partió en dos, porque una sola habría tenido que aflojarse.
/// </para>
/// <para>
/// <b>Por qué dos y no una más laxa.</b> Pasar de «ninguna propiedad viene del servidor» a
/// «ninguna propiedad AUDITADA viene del servidor» habría dejado de mirar todo lo demás: un
/// <c>DEFAULT gen_random_uuid()</c> en una columna no auditada habría entrado sin que nadie se
/// enterase. Las dos de aquí, juntas, afirman al menos tanto como la de antes: la primera cubre
/// exactamente lo que el interceptor necesita, y la segunda enumera <b>por nombre</b> lo único
/// que puede venir del servidor. Una forma nueva de generar valor —una sexta, o una séptima—
/// aparece en la lista real, no está en la declarada, y esto se pone rojo igual que antes.
/// </para>
/// <para>
/// Si se pone rojo, <b>no se añade a la lista sin más</b>: se mira si la premisa del interceptor
/// sigue en pie, y si no, se reabre la decisión con un ADR que sustituya al vigente.
/// </para>
/// </remarks>
public sealed class LasClavesSeConocenAntesDeGuardarTests : IDisposable
{
    // El inventario declarado, ENTERO y por nombre. No es «lo que hay»: es lo que se ha decidido
    // que haya. Por eso se compara la lista completa y no se pregunta si cada una está permitida.
    private static readonly string[] s_generadasPorElServidor =
    [
        "Almacen.Version",
        "Ejercicio.Version",
        "Empresa.Version",
        "Rol.Version",
        "Serie.Version",
        "Usuario.Version",
    ];

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ninguna_propiedad_auditada_la_pone_la_base_de_datos()
    {
        List<string> generadas = [.. Modelos().SelectMany(modelo => Donde(modelo, EsAuditadaYDelServidor))];

        generadas.ShouldBeEmpty(
            "si la base genera un valor QUE VA A LA TRAZA, no se conoce hasta después del INSERT " +
            "y el interceptor de auditoría necesita una segunda fase. Reabre el ADR-0015 antes " +
            "de tocar esta lista.");
    }

    [Fact]
    public void Lo_unico_que_genera_el_servidor_son_los_testigos_de_concurrencia()
    {
        List<string> generadas = [.. Modelos().SelectMany(modelo => Donde(modelo, EsDelServidor))];

        generadas.Sort(StringComparer.Ordinal);

        // Las dos listas ENTERAS y en el mismo orden, no «lo que sobra»: un testigo que
        // DESAPARECE del modelo deja ese recurso sin control de concurrencia, y eso también
        // tiene que verse aquí.
        string.Join(", ", generadas).ShouldBe(
            string.Join(", ", s_generadasPorElServidor),
            "el servidor solo genera los testigos de concurrencia del R11. Cualquier otra cosa " +
            "—un DEFAULT, una columna calculada, un IDENTITY— vuelve a poner en duda la fase " +
            "única del interceptor de auditoría, y eso se decide en un ADR, no aquí.");
    }

    [Fact]
    public void Todo_lo_que_genera_el_servidor_es_de_verdad_un_testigo_de_concurrencia()
    {
        // Y no algo que se le PAREZCA. La lista de arriba se compara por nombre, así que una
        // propiedad llamada `Version` con un DEFAULT en la base pasaría por testigo sin serlo:
        // aquí se comprueba que cada una lo es por lo que la hace serlo —uint, generada en cada
        // escritura y marcada como testigo—, que es lo que mete el valor en el WHERE del UPDATE.
        List<string> impostoras = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .SelectMany(tipo => tipo.PropiedadesConCamino()
                .Where(par => EsDelServidor(par.Propiedad) && !par.Propiedad.EsElTestigo())
                .Select(par => $"{tipo.ShortName()}.{par.Camino}"))];

        impostoras.ShouldBeEmpty("esto lo genera el servidor y no es el testigo de concurrencia");
    }

    [Fact]
    public void Las_entidades_del_tipo_base_y_las_que_llevan_testigo_son_las_MISMAS()
    {
        // El ítem 0.10 extrajo `EntidadBase`, y con él las dos preguntas se juntaron: hoy las seis
        // entidades que heredan del tipo base son exactamente las seis que llevan testigo. Que
        // coincidan no es casualidad —son las que se modifican, y lo que se modifica necesita
        // saber cuándo y contra qué versión— pero tampoco es una ley: podría haber una entidad de
        // solo-inserción con marcas de tiempo y sin testigo.
        //
        // Por eso esto no PROHÍBE la divergencia, la hace visible. Si un día una entidad hereda del
        // base y no lleva testigo, este caso se pone rojo y hay que decir por qué en el ADR-0015,
        // que es donde vive la lista. Sin este caso, la entidad nueva entraría sin control de
        // concurrencia y sin que nada lo dijera.
        List<string> delTipoBase = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .Where(tipo => typeof(EntidadBase).IsAssignableFrom(tipo.ClrType))
            .Select(tipo => tipo.ShortName())];

        List<string> conTestigo = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .Where(tipo => tipo.GetProperties().Any(propiedad => propiedad.EsElTestigo()))
            .Select(tipo => tipo.ShortName())];

        delTipoBase.Sort(StringComparer.Ordinal);
        conTestigo.Sort(StringComparer.Ordinal);

        // Y las dos contra la lista del ADR-0015, que sigue siendo la declaración: sin esto, dos
        // listas derivadas del mismo modelo podrían quedarse iguales y a la vez vacías.
        string.Join(", ", delTipoBase).ShouldBe(string.Join(", ", conTestigo));
        string.Join(", ", conTestigo).ShouldBe(
            string.Join(", ", s_generadasPorElServidor.Select(nombre => nombre.Split('.')[0])),
            "las entidades con testigo son las del ADR-0015, ni una más ni una menos");
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
        // dependiera del servidor, y de eso se ocupan los casos de arriba.
        sinClave.ShouldBeEmpty("estas claves no las pone ni el dominio ni el cliente");
    }

    private static bool EsClienteQuienLaPone(IProperty clave) =>
        clave.ClrType == typeof(Guid) || clave.ClrType == typeof(Guid?);

    // Con camino, para que una propiedad de un tipo complejo no se escape: `GetProperties()` no
    // las devuelve, y un DEFAULT puesto ahí dentro se saltaría este inventario entero.
    private static IEnumerable<string> Donde(IModel modelo, Func<IReadOnlyProperty, bool> condicion) =>
        modelo.GetEntityTypes()
            .SelectMany(tipo => tipo.PropiedadesConCamino()
                .Where(par => condicion(par.Propiedad))
                .Select(par => $"{tipo.ShortName()}.{par.Camino}"));

    private static bool EsAuditadaYDelServidor(IReadOnlyProperty propiedad) =>
        propiedad.Auditoria().Que == ClasificacionDeAuditoria.Auditada && EsDelServidor(propiedad);

    // Las cinco formas que tiene un valor de venir del servidor, cada una con su nombre: un
    // DEFAULT, una columna calculada, una columna IDENTITY o serial —esta es de Npgsql, y es la
    // que de verdad distingue «la pone la base» de «la pone el cliente al insertar»—, algo que se
    // regenera en cada UPDATE (la forma de un `rowversion`), y el testigo de concurrencia, que
    // en PostgreSQL es `xmin`.
    private static bool EsDelServidor(IReadOnlyProperty propiedad) =>
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
