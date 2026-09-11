using Bastion.Api.FunctionalTests.Persistencia;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
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
        "Articulo.Version",
        "Categoria.Version",
        "ConversionUM.Version",
        "Divisa.Version",
        "Ejercicio.Version",
        "Empresa.Version",
        "Impuesto.Version",

        // Las dos del ítem 1.9, y las dos llevan testigo a propósito: la línea de tarifa NO es un
        // hijo del agregado como lo son el contacto o la cuenta bancaria. Tiene su propia ruta,
        // su propio `PUT` y su propio ETag, porque una tabla de precios se mantiene línea a línea
        // —dos personas cambiando el precio de dos artículos distintos de la misma tarifa no se
        // están pisando— y un testigo único en la tarifa haría que la segunda se llevara un 412
        // por tocar otra fila. Es la decisión contraria a la de los tres hijos del tercero, y lo
        // que la invierte es que allí el agregado entero es una ficha que se edita de una vez.
        "LineaTarifa.Version",
        "Rol.Version",
        "Serie.Version",
        "Tarifa.Version",
        "Tercero.Version",
        "TipoCambio.Version",
        "Ubicacion.Version",
        "UnidadMedida.Version",
        "Usuario.Version",
    ];

    // Heredan del tipo base y NO llevan testigo de concurrencia, a propósito y con su motivo.
    // Los tres son hijos del agregado del tercero: no son recursos que se editen por su cuenta,
    // así que el testigo que gobierna su edición es el del TERCERO —que es el que la ruta exige
    // en el cuerpo—. Un testigo por hijo dejaría pasar el caso que de verdad hay que detectar:
    // dos ediciones simultáneas de la MISMA ficha, una que cambia la razón social y otra que
    // cuelga un contacto. El motivo largo está en `ConfiguracionDeContacto`.
    private static readonly string[] s_delTipoBaseSinTestigo =
    [
        "CondicionPago",
        "Contacto",
        "CuentaBancaria",
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

        List<string> esperadas = [.. conTestigo, .. s_delTipoBaseSinTestigo];
        esperadas.Sort(StringComparer.Ordinal);

        // La divergencia que este caso anunciaba llevó hasta el ítem 1.6 en ponerse: los tres
        // hijos del tercero heredan del tipo base y no llevan testigo. No se permite en blanco,
        // se DECLARA —arriba, con su motivo—, que es lo que este caso pedía que se hiciera.
        string.Join(", ", delTipoBase).ShouldBe(
            string.Join(", ", esperadas),
            "una entidad hereda del tipo base y no lleva testigo sin estar declarada arriba, o al " +
            "revés. Si es nueva: o le pones testigo, o dices por qué no lo lleva");

        string.Join(", ", conTestigo).ShouldBe(
            string.Join(", ", s_generadasPorElServidor.Select(nombre => nombre.Split('.')[0])),
            "las entidades con testigo son las del ADR-0015, ni una más ni una menos");
    }

    /// <summary>
    /// Ninguna clave se genera al insertar: <b>todas</b> vienen puestas de la fábrica del dominio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Esta regla existía y daba por buena justamente la configuración que falló.</b> Miraba
    /// esta misma propiedad y eximia en bloque a toda clave <c>Guid</c>, con el motivo escrito de
    /// que <c>ValueGenerated.OnAdd</c> en un <c>Guid</c> no significa que la ponga la base. Eso es
    /// cierto <b>del INSERT</b>, y es todo lo que la regla miraba. Pero <c>OnAdd</c> es también lo
    /// que EF Core consulta para decidir algo muy distinto: si una entidad recién aparecida en la
    /// colección de un padre ya seguido es un ALTA o una fila que ya existía. Con la clave puesta
    /// por el dominio, la respuesta es siempre «ya existía», sale un <c>UPDATE</c> contra una fila
    /// que no está, y de ahí un <b>412</b> sobre un alta correcta. El ítem 1.6 lo cobró en siete
    /// casos del carril de integración.
    /// </para>
    /// <para>
    /// Por eso ya no se exime nada: se exige la declaración <b>positiva</b>, que es la verdad del
    /// sistema —aquí la clave la pone siempre el dominio— y la que apaga el otro uso de
    /// <c>OnAdd</c>. La pone la convención <c>LaClaveLaPoneElDominio</c>, y el efecto sobre el
    /// seguidor de cambios lo ejerce <c>LoQueCuelgaNaceComoAltaTests</c>, sin contenedor.
    /// </para>
    /// </remarks>
    [Fact]
    public void Toda_entidad_tiene_su_clave_completa_antes_de_guardar()
    {
        List<string> generadasAlInsertar = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .SelectMany(tipo => (tipo.FindPrimaryKey()?.Properties ?? [])
                .Where(clave => clave.ValueGenerated != ValueGenerated.Never)
                .Select(clave =>
                    $"{tipo.ShortName()}.{clave.Name} ({clave.ClrType.Name}, {clave.ValueGenerated})"))];

        generadasAlInsertar.ShouldBeEmpty(
            "ninguna clave de este sistema se genera al insertar: la pone la fábrica del dominio " +
            "antes de que EF vea nada. Una clave `OnAdd` no solo afecta al INSERT —ahí EF respeta " +
            "el valor que llega—: es lo que EF mira para decidir si un hijo que aparece en la " +
            "colección de un padre ya seguido es un ALTA o una fila que ya existía. Con la clave " +
            "siempre puesta, decide «ya existía», emite un UPDATE contra una fila que no está y " +
            "eso acaba en un 412 sobre un alta correcta. Lo declara la convención " +
            "`LaClaveLaPoneElDominio`; si algo se le escapa, aparece aquí.");
    }

    /// <summary>
    /// El universo de modelos es el declarado, entero.
    /// </summary>
    /// <remarks>
    /// Sin esto, todas las reglas de este fichero recorren la lista que el descubrimiento
    /// encuentre, y una lista que se queda corta —o vacía— las deja a todas en verde sin haber
    /// mirado nada. Es exactamente lo que pasó hasta el 1.6: Terceros entró en la fase 1 y no
    /// estaba en ninguna de las listas escritas a mano.
    /// </remarks>
    [Fact]
    public void El_universo_de_modelos_es_el_declarado()
    {
        List<string> encontrados = [.. LosModelosDeCadaModulo.Contextos().Select(tipo => tipo.Name)];
        encontrados.Sort(StringComparer.Ordinal);

        string.Join(", ", encontrados).ShouldBe(
            string.Join(", ", LosModelosDeCadaModulo.Declarados),
            "los contextos de este sistema son los declarados, ni uno más ni uno menos. Uno de " +
            "más: añádelo a la lista y comprueba que las reglas de modelo le valen. Uno de menos: " +
            "el descubrimiento se ha quedado corto y todas las reglas de este fichero están " +
            "diciendo «cada entidad declara…» de menos módulos de los que hay.");

        Modelos().Count().ShouldBe(
            LosModelosDeCadaModulo.Declarados.Length,
            "hay un contexto descubierto que el contenedor no sabe resolver");
    }

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

    private IEnumerable<IModel> Modelos() => LosModelosDeCadaModulo.De(_api.Services);
}
