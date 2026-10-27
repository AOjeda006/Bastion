using Bastion.Api.FunctionalTests.Salud;
using Bastion.Inventario.Contracts.Saldos;
using Bastion.Pruebas.Comun;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Listados;

/// <summary>
/// Ningún listado recibe por la cadena de consulta un criterio que no debería quedar escrito.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué una regla y no una convención.</b> El ADR-0025 se cumple hoy por construcción: los
/// listados solo llevan <c>page</c>, <c>size</c>, <c>sort</c>, <c>q</c> y —los cuatro maestros de
/// instalación— <c>retiradas</c>. Eso dura hasta que
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
            .. from listado in ListadosDeLaApi.De(_api.Services)
               from parametro in ListadosDeLaApi.DeLaUrl(listado)
               from sensible in s_sensibles
               where Contiene(parametro, sensible.Key)
               select $"{ListadosDeLaApi.Nombre(listado)} recibe «{parametro}» por la URL — {sensible.Value}",
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
        List<ApiDescription> listados = [.. ListadosDeLaApi.De(_api.Services)];

        listados.ShouldNotBeEmpty(
            "el explorador de API no ha devuelto ni un listado, así que la regla de al lado " +
            "recorrería una lista vacía y saldría verde sin mirar ninguna URL");

        SortedSet<string> parametros = new(
            listados.SelectMany(ListadosDeLaApi.DeLaUrl), StringComparer.Ordinal);

        // La lista entera de lo que los listados aceptan por la URL, hoy. Un parámetro nuevo
        // —sensible o no— pone esto rojo y obliga a decidirlo aquí, que es donde están escritos
        // los motivos, en vez de en el controlador donde se añadió.
        //
        // `retiradas` entró en el ítem 1.7 y este rojo es el que obligó a justificarlo. Es un
        // booleano de los cuatro maestros de INSTALACIÓN —divisas, cotizaciones, unidades y
        // conversiones (R8)—: dice si la página incluye también las retiradas, que por omisión no
        // salen (ADR-0023). No identifica a nadie ni acota por nadie: lo que deja escrito en el
        // registro de acceso es «alguien pidió ver también las divisas retiradas», que no es un
        // dato de nadie. Por eso va en la URL y no en el cuerpo, y por eso NO es un extremo
        // aparte: lo que hace nominativo y trazado al listado de lo bloqueado (ADR-0027) es que
        // expone datos personales del art. 32, y una divisa no tiene ninguno. Cobrar cuatro
        // permisos y cuatro rutas por una protección que aquí no protege de nada, además,
        // declararía en el contrato que ver una divisa retirada es un acto reservado, y no lo es.
        //
        // `categoria` entró en el ítem 1.8 y este rojo salió el mismo día, sin que nadie apuntara
        // la regla a Catálogo: es lo que se le pedía al módulo nuevo. Acota el listado de
        // artículos por una rama del árbol de clasificación de la propia empresa. No es un dato de
        // nadie —una categoría no identifica a una persona ni se puede asociar a una—, así que lo
        // que deja escrito en el registro de acceso es «alguien listó los artículos de tal rama»,
        // que es exactamente lo que un ERP tiene que poder decir en voz alta.
        //
        // Y es un `Guid`, no un nombre, lo que aquí importa por una razón distinta de la
        // habitual: un identificador opaco no filtra por sí mismo ni siquiera CÓMO se llama la
        // rama, así que la URL no cuenta nada del catálogo a quien solo vea el registro.
        //
        // `codigo` entró en el ítem 1.9 y este rojo volvió a salir solo, igual que el anterior.
        // Acota el listado de tarifas a los tramos de UNA tarifa, por igualdad exacta. Lo que
        // viaja es el código de una lista de precios de la propia empresa —`PVP`, `MAYORISTA`—:
        // no identifica a nadie ni acota por nadie, y lo que queda escrito en el registro de
        // acceso es «alguien listó los tramos de la tarifa PVP».
        //
        // A diferencia de `categoria`, aquí SÍ viaja un texto que la empresa ha escrito, y por eso
        // conviene decir qué texto es: el de una tarifa, no el de un tercero. `Tercero.Tarifa`
        // —qué tarifa tiene asignada cada cliente— es del ítem 1.10, y ese cruce no se puede
        // consultar por aquí: este listado va contra `tarifas`, no contra `terceros`, así que
        // ningún `?codigo=` de esta ruta dice a qué precios compra nadie. El día que exista un
        // listado de terceros acotado por tarifa, la pregunta se hace otra vez y en este sitio,
        // porque será OTRO parámetro en OTRO listado y volverá a poner esto rojo.
        //
        // El extremo de resolución de precio —`GET /tarifas/{codigo}/precio?articulo=&cantidad=`—
        // no aparece en esta lista y no es un olvido: no devuelve una página, así que no es un
        // listado. Su `?articulo=` es un `Guid` de catálogo y su `?cantidad=` un número, y ninguno
        // de los dos dice de quién es la compra: quién pregunta el precio no está en la URL.
        //
        // `estado`, `almacen` y `solo` entraron en el ítem 2.12, con los dos listados del recuento,
        // que son los primeros del inventario. `estado` acota la lista por uno de cuatro nombres
        // fijos —EnCurso, Confirmado, Anulado, Descartado— y `almacen` por el `Guid` de un almacén
        // de la propia empresa, opaco como el de `categoria`. `solo` acota la página de líneas de un
        // recuento a una de dos palabras fijas, las sin contar o las de teórico cambiado. Ninguno
        // identifica a nadie ni acota por nadie: lo que deja escrito el registro de acceso es
        // «alguien listó los recuentos en curso de tal almacén», que es lo que un ERP tiene que
        // poder decir en voz alta.
        //
        // `fecha` y `articulo` entraron en el 2.14, con el saldo a una fecha (ADR-0063 §8). `fecha`
        // es un día, y lo que deja escrito el registro de acceso es «alguien miró el saldo del 31 de
        // marzo», que no es un dato de nadie. `articulo` acota por el `Guid` de un artículo de la
        // propia empresa, opaco como `almacen`, y tampoco dice cómo se llama. El saldo es también el
        // primer listado cuya respuesta no es una página a secas —lleva la fecha al lado—, y por eso
        // el barrido reconoce la página dentro de la respuesta: sin eso, este listado no estaría en
        // la lista y sus dos parámetros no habrían puesto esto rojo.
        //
        // `origen` y `destino` entraron en el 2.19, con el listado de las transferencias (ADR-0068
        // §1). Son el `almacen` del ajuste partido en dos, porque una transferencia tiene dos: el
        // `Guid` del almacén del que sale y el del almacén al que llega, de la propia empresa y
        // opacos como aquel. Lo que deja escrito el registro de acceso es «alguien listó lo que va de
        // tal almacén a tal otro», que no es un dato de nadie.
        parametros.ShouldBe(
            new SortedSet<string>(StringComparer.Ordinal)
            {
                "almacen", "articulo", "categoria", "codigo", "destino", "estado", "fecha", "origen", "page",
                "q", "retiradas", "size", "solo", "sort",
            },
            customMessage: "los parámetros de consulta de los listados no son los del contrato: " +
            string.Join(", ", parametros));

        // Y las dos preguntas de control, porque un silencio de la regla de al lado solo vale si
        // esa regla sabe hablar: tiene que ver la fuga en un nombre compuesto y tiene que callarse
        // con uno de los de arriba.
        Contiene("nifDelCliente", "nif").ShouldBeTrue(
            "la comparación no ve un campo sensible dentro de un nombre compuesto, que es " +
            "justamente como se escriben los parámetros de verdad");

        // Y que reconoce un listado que lleva su página dentro de la respuesta, que es como responde
        // el saldo: sin esta, una respuesta envuelta sacaría su listado del barrido sin avisar.
        ListadosDeLaApi.EsColeccion(typeof(SaldosAUnaFechaDto)).ShouldBeTrue(
            "el barrido no reconoce como listado una respuesta que lleva la página dentro");

        ListadosDeLaApi.EsColeccion(typeof(SaldoDto)).ShouldBeFalse(
            "el barrido toma por listado una respuesta que no lleva ninguna página");

        s_sensibles.Keys.Any(sensible => Contiene("page", sensible)).ShouldBeFalse(
            "la comparación marca como sensible un parámetro que no lo es, así que su silencio " +
            "sobre los demás tampoco significaría nada");
    }

    private static bool Contiene(string parametro, string sensible) =>
        parametro.Contains(sensible, StringComparison.OrdinalIgnoreCase);
}
