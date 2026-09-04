using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Listados;

/// <summary>
/// Ningún listado recibe por la cadena de consulta un criterio que no debería quedar escrito.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué una regla y no una convención.</b> El ADR-0025 se cumple hoy por construcción: los
/// doce listados solo llevan <c>page</c>, <c>size</c>, <c>sort</c> y <c>q</c>. Eso dura hasta que
/// alguien añada un <c>?nif=</c> porque es cómodo para la pantalla de terceros. No rompería
/// ningún test —funcionaría— y el NIF pasaría a quedar escrito en el historial del navegador, en
/// el enlace que se copia por chat, en la referencia que el navegador manda al sitio siguiente y
/// en el registro de acceso del servidor de delante, que suele guardarse más tiempo y con menos
/// cuidado que la base de datos.
/// </para>
/// <para>
/// <b>De dónde salen los parámetros.</b> Del explorador de API, que es la misma fuente de la que
/// sale <c>docs/api/openapi.json</c> y por tanto el cliente del frontal. No se reconstruye por
/// reflexión: un parámetro sin <c>[FromQuery]</c> en un <c>GET</c> también se enlaza desde la
/// URL, por convenio, y una reconstrucción que solo mirase el atributo lo pasaría por alto — que
/// es exactamente la fuga que esta regla existe para ver.
/// </para>
/// <para>
/// <b>La comparación es por contención y no por igualdad</b>, a propósito. Un parámetro llamado
/// <c>nifDelCliente</c> o <c>filtroPorCorreo</c> escribe el dato en la URL igual que uno llamado
/// <c>nif</c>, y una regla que solo mirase el nombre exacto se saltaría el caso más probable: el
/// de quien escribe un nombre descriptivo.
/// </para>
/// </remarks>
public sealed class NingunCriterioSensibleViajaEnLaUrlTests : IDisposable
{
    /// <summary>
    /// Lo que no puede viajar en una URL, con el motivo de cada uno. Se comprueba la lista entera
    /// contra todos los parámetros de todos los listados.
    /// </summary>
    /// <remarks>
    /// No es «datos personales» en abstracto: es la lista de lo que en este dominio identifica a
    /// una persona o a una empresa concreta, o permite suplantarla. Un nombre comercial no está
    /// aquí —es lo que <c>?q=</c> busca— y un código de artículo tampoco.
    /// </remarks>
    private static readonly Dictionary<string, string> s_sensibles = new(StringComparer.Ordinal)
    {
        ["nif"] = "identifica a una empresa o a un autónomo concreto y es la llave con la que se " +
            "cruza con cualquier otro fichero. Buscar por NIF va por cuerpo (ADR-0025)",

        ["cif"] = "lo mismo que el NIF, con el nombre que se usaba antes de 2008 y que sigue " +
            "apareciendo en formularios",

        ["dni"] = "identifica a una persona física, y en el registro de acceso queda junto a la " +
            "hora y la dirección desde la que se pidió",

        ["nie"] = "lo mismo que el DNI para quien no lo tiene",

        ["correo"] = "identifica a una persona y además es la mitad de una credencial: quien lea " +
            "el registro sabe a quién intentar suplantar",

        ["email"] = "lo mismo, con el nombre en inglés, que es el que se cuela cuando alguien " +
            "copia un parámetro de otra API",

        ["telefono"] = "identifica a una persona y es el segundo factor de media internet",

        ["movil"] = "lo mismo, y con más motivo: es a donde llegan los códigos de un solo uso",

        ["iban"] = "es una cuenta bancaria. No hace falta más",

        ["contrasena"] = "una credencial no viaja NUNCA en una URL, ni siquiera para comprobarla",

        ["password"] = "lo mismo, con el nombre en inglés",

        ["token"] = "una credencial de portador en la URL es una credencial regalada a todo el " +
            "que lea el registro o el historial",
    };

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ningun_listado_recibe_un_criterio_sensible_por_la_url()
    {
        List<string> fugas =
        [
            .. from listado in Listados()
               from parametro in DeLaUrl(listado)
               from sensible in s_sensibles
               where Contiene(parametro, sensible.Key)
               select $"{Nombre(listado)} recibe «{parametro}» por la URL — {sensible.Value}",
        ];

        fugas.ShouldBeEmpty(
            "estos listados reciben un criterio sensible por la cadena de consulta:" +
            Environment.NewLine + string.Join(Environment.NewLine, fugas));
    }

    /// <summary>
    /// El arnés de la regla de arriba: que encuentre listados, que les vea los parámetros, y que
    /// sepa distinguir un nombre sensible de uno que no lo es.
    /// </summary>
    /// <remarks>
    /// Sin esto, la regla de arriba sale verde de las tres maneras en que puede estar rota: si el
    /// explorador de API deja de devolver descripciones, si deja de marcar los parámetros como de
    /// consulta, o si la comparación deja de casar. Ninguna de las tres tiene otro síntoma, y las
    /// tres dejan la fuga abierta con el carril en verde.
    /// </remarks>
    [Fact]
    public void El_barrido_ve_los_listados_y_sus_parametros()
    {
        List<ApiDescription> listados = [.. Listados()];

        listados.ShouldNotBeEmpty(
            "el explorador de API no ha devuelto ni un listado, así que la regla de al lado " +
            "recorrería una lista vacía y saldría verde sin mirar ninguna URL");

        SortedSet<string> parametros = new(
            listados.SelectMany(DeLaUrl), StringComparer.Ordinal);

        // La lista entera de lo que los listados aceptan por la URL, hoy. Un parámetro nuevo
        // —sensible o no— pone esto rojo y obliga a decidirlo aquí, que es donde están escritos
        // los motivos, en vez de en el controlador donde se añadió.
        parametros.ShouldBe(
            new SortedSet<string>(StringComparer.Ordinal) { "page", "q", "size", "sort" },
            customMessage: "los parámetros de consulta de los listados no son los cuatro del " +
            "contrato: " + string.Join(", ", parametros));

        // Y las dos preguntas de control, porque un silencio de la regla de al lado solo vale si
        // esa regla sabe hablar: tiene que ver la fuga en un nombre compuesto y tiene que callarse
        // con uno de los cuatro de arriba.
        Contiene("nifDelCliente", "nif").ShouldBeTrue(
            "la comparación no ve un campo sensible dentro de un nombre compuesto, que es " +
            "justamente como se escriben los parámetros de verdad");

        s_sensibles.Keys.Any(sensible => Contiene("page", sensible)).ShouldBeFalse(
            "la comparación marca como sensible un parámetro que no lo es, así que su silencio " +
            "sobre los demás tampoco significaría nada");
    }

    private static bool Contiene(string parametro, string sensible) =>
        parametro.Contains(sensible, StringComparison.OrdinalIgnoreCase);

    private static string Nombre(ApiDescription listado) =>
        (listado.HttpMethod ?? "?") + " /" + listado.RelativePath;

    private static IEnumerable<string> DeLaUrl(ApiDescription listado) =>
        listado.ParameterDescriptions
            .Where(parametro => parametro.Source == BindingSource.Query)
            .Select(parametro => parametro.Name);

    /// <summary>
    /// Los listados, reconocidos por lo que DEVUELVEN y no por cómo se llaman.
    /// </summary>
    /// <remarks>
    /// Una heurística por nombre —los métodos que se llamen <c>Listar</c>— es la que el ítem 1.2
    /// tumbó: se le escapa el primero que se llame <c>Consultar</c>, y no se le escapa con un
    /// rojo, se le escapa en silencio. El tipo de la respuesta es un hecho del contrato: si
    /// devuelve una página o un tramo, es un listado.
    /// </remarks>
    private IEnumerable<ApiDescription> Listados()
    {
        IApiDescriptionGroupCollectionProvider explorador =
            _api.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

        return from grupo in explorador.ApiDescriptionGroups.Items
               from descripcion in grupo.Items
               where descripcion.SupportedResponseTypes.Any(respuesta => EsColeccion(respuesta.Type))
               select descripcion;
    }

    private static bool EsColeccion(Type? tipo) =>
        tipo is { IsGenericType: true }
        && (tipo.GetGenericTypeDefinition() == typeof(PaginaDe<>)
            || tipo.GetGenericTypeDefinition() == typeof(TramoDe<>));
}
