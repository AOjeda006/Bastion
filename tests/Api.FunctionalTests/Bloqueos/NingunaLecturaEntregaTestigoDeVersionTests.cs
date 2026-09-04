using System.Collections;
using System.Reflection;
using System.Text;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Bloqueos;

/// <summary>
/// Ninguna respuesta de la API lleva un testigo de versión <b>en el cuerpo</b>: la versión sale por
/// la cabecera <c>ETag</c> de las lecturas de un recurso, y por ningún otro sitio.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto existe para sostener cuatro exenciones que hoy son prosa.</b> Las cuatro exenciones de
/// <c>If-Match</c> de los desbloqueos —empresa, almacén, ubicación y usuario— se apoyan todas en la
/// misma premisa: una fila bloqueada no se puede leer, así que no emite <c>ETag</c>, así que
/// <c>If-Match</c> pediría una llave que no hay manera de conseguir. La premisa está escrita dentro
/// de cada exención, con la palabra «DEPENDE DE» delante para que caduque en voz alta.
/// </para>
/// <para>
/// <b>Pero la prosa no falla.</b> El ítem 1.4 construye justamente la lectura de la que esas cuatro
/// cláusulas decían que no existía, y la construye como listado precisamente porque un listado no
/// lleva versión por elemento. El día que alguien añada un <c>Version</c>, un <c>ETag</c> o un
/// <c>Xmin</c> a las filas de ese listado —«para que el cliente pueda actuar luego», que es la frase
/// que lo va a acompañar—, la llave vuelve a existir y las cuatro exenciones caducan a la vez, en
/// silencio, dentro del mismo cambio que las rompe. Nada del carril lo diría. Esto sí.
/// </para>
/// <para>
/// <b>Y no está acotado al listado de lo bloqueado, a propósito.</b> La regla mira TODA respuesta de
/// la API, porque la premisa que sostiene las exenciones habla de «toda lectura», no de una. Acotarla
/// al camino nuevo dejaría el siguiente <c>GET</c> que devolviera una versión en el cuerpo fuera del
/// barrido, que es la forma exacta en que el ítem 1.2 vio fallar un universo escrito a mano.
/// </para>
/// </remarks>
public sealed class NingunaLecturaEntregaTestigoDeVersionTests : IDisposable
{
    /// <summary>
    /// Las palabras que, siendo una palabra entera del nombre de un miembro, dicen que ese miembro
    /// es un testigo de concurrencia. Cada una con su motivo.
    /// </summary>
    /// <remarks>
    /// Se comparan por PALABRA y no por contenido, y esa decisión tiene un contraejemplo real en
    /// este repositorio: <c>ConversionUmDto</c> y todo lo que se llame «conversión» CONTIENE
    /// «version», y no tiene nada que ver. Una comparación por contenido saldría roja el primer día
    /// sobre un DTO legítimo, y la forma de arreglar ese rojo sería quitar la palabra de la lista
    /// —o sea, desarmar la regla para que dejara de molestar—.
    /// </remarks>
    private static readonly Dictionary<string, string> s_testigos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["version"] = "es el testigo de concurrencia optimista tal cual. En el cuerpo obliga a que " +
            "esté también en las listas, que se leen sin rastreo y no lo traen: el mismo campo " +
            "valdría una cosa en una respuesta y cero en otra",

        ["etag"] = "es el nombre de la cabecera. Metido en el cuerpo deja de ser protocolo y pasa a " +
            "ser un campo del contrato del recurso, que es lo que `ConVersion<T>` existe para evitar",

        ["xmin"] = "es la columna de sistema de PostgreSQL que EF Core usa como testigo. Publicarla " +
            "filtra el mecanismo de almacenamiento y da la llave igual que darla con su nombre bueno",

        ["rowversion"] = "el nombre del mismo mecanismo en el otro dialecto. Está aquí porque una " +
            "regla que solo conoce el nombre de la casa no caza al que llega copiando de fuera",

        ["concurrencia"] = "un campo que se llame así en el cuerpo solo puede ser esto, y llamarlo " +
            "distinto no lo convierte en otra cosa",
    };

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ninguna_respuesta_de_la_api_lleva_testigo_de_version_en_el_cuerpo()
    {
        List<string> fugas = [];

        foreach (ApiDescription descripcion in Descripciones())
        {
            foreach (Type raiz in TiposDeRespuesta(descripcion))
            {
                foreach (string fuga in TestigosDentroDe(raiz))
                {
                    fugas.Add($"{Nombre(descripcion)} -> {fuga}");
                }
            }
        }

        fugas.ShouldBeEmpty(
            "estas respuestas entregan un testigo de versión en el cuerpo, así que la llave que " +
            "las cuatro exenciones de If-Match de los desbloqueos dan por inalcanzable ya se " +
            "puede conseguir leyendo. O se quita el campo, o esas cuatro exenciones caducan y hay " +
            "que volver a exigir If-Match:\n" + string.Join("\n", fugas));
    }

    /// <summary>
    /// El arnés de la regla de arriba: que vea respuestas, que baje hasta los DTO, y que sepa
    /// distinguir un testigo de una palabra que se le parece.
    /// </summary>
    /// <remarks>
    /// Sin esto, la regla de arriba sale verde de las cuatro maneras en que puede estar rota: si el
    /// explorador no devuelve descripciones, si ninguna declara tipo de respuesta, si el recorrido
    /// no entra en las propiedades, o si la comparación no casa nunca. Las cuatro dejan la fuga
    /// abierta con el carril en verde (ADR-0020).
    /// </remarks>
    [Fact]
    public void El_barrido_ve_los_cuerpos_y_reconoce_un_testigo()
    {
        List<ApiDescription> descripciones = [.. Descripciones()];

        descripciones.ShouldNotBeEmpty(
            "el explorador de API no ha devuelto ni una descripción: la regla de al lado " +
            "recorrería una lista vacía y saldría verde sin mirar ni un cuerpo");

        SortedSet<string> visitados = new(StringComparer.Ordinal);

        foreach (Type raiz in descripciones.SelectMany(TiposDeRespuesta))
        {
            foreach (Type tipo in Recorrer(raiz))
            {
                visitados.Add(tipo.Name);
            }
        }

        visitados.ShouldNotBeEmpty(
            "el recorrido no ha visitado ni un tipo de respuesta, así que no habría mirado " +
            "ninguna propiedad de ningún DTO");

        // No es un suelo redondo por gusto: hoy son bastantes más, y lo que este número dice es
        // «el recorrido baja de verdad al grafo», no «hay exactamente tantos». Un recorrido que se
        // quedara en la raíz visitaría un puñado y esto lo vería.
        visitados.Count.ShouldBeGreaterThan(
            10,
            "el recorrido ha visitado muy pocos tipos para el tamaño de esta API: lo normal es que " +
            "se haya quedado en la raíz sin entrar en las propiedades. Visitados: " +
            string.Join(", ", visitados));

        // Las tres preguntas de control. El silencio de la regla de al lado solo significa algo si
        // el detector sabe hablar, sabe callarse, y sabe mirar dentro de un tipo de verdad.
        Testigo("Version").ShouldNotBeNull(
            "el detector no reconoce el testigo escrito con su propio nombre");

        Testigo("EtiquetaETag").ShouldNotBeNull(
            "el detector no reconoce el testigo cuando es una palabra de un nombre compuesto, que " +
            "es como se escriben las propiedades de verdad");

        Testigo("ETag").ShouldNotBeNull(
            "el detector no reconoce el testigo escrito a secas. Es el caso que destapó que un " +
            "separador de PascalCase parte «ETag» en «E» y «Tag»: sin mirar tambien las parejas " +
            "contiguas, la propiedad con el nombre más obvio de todos se colaba");

        Testigo("ConversionUm").ShouldBeNull(
            "el detector marca como testigo un nombre que solo CONTIENE «version»: en este " +
            "repositorio eso es `ConversionUmDto`, un DTO legítimo, y con ese falso positivo la " +
            "regla se desarmaría para dejar de molestar");

        // Y el arnés del recorrido, no solo el del detector: un tipo hecho a propósito con la
        // versión dentro tiene que salir señalado, y con la ruta hasta ella.
        List<string> deLaSonda = [.. TestigosDentroDe(typeof(SondaConVersion))];

        deLaSonda.ShouldNotBeEmpty(
            "el recorrido no encuentra un testigo de versión en un tipo que lo lleva declarado, " +
            "así que su silencio sobre los DTO de verdad no significa nada");
    }

    /// <summary>
    /// Un DTO de mentira con la versión dentro, que es exactamente la mutación de la que este
    /// fichero defiende. Existe para que el arnés pueda comprobar que el recorrido la encuentra.
    /// </summary>
    private sealed record SondaConVersion(Guid Id, string Nombre, VersionDeRecurso Version);

    private static string Nombre(ApiDescription descripcion) =>
        (descripcion.HttpMethod ?? "?") + " /" + descripcion.RelativePath;

    private IEnumerable<ApiDescription> Descripciones()
    {
        IApiDescriptionGroupCollectionProvider explorador =
            _api.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

        return from grupo in explorador.ApiDescriptionGroups.Items
               from descripcion in grupo.Items
               select descripcion;
    }

    private static IEnumerable<Type> TiposDeRespuesta(ApiDescription descripcion) =>
        descripcion.SupportedResponseTypes
            .Select(respuesta => respuesta.Type)
            .Where(tipo => tipo is not null)
            .Select(tipo => tipo!);

    /// <summary>Los testigos que hay dentro del grafo de un tipo, con el camino hasta cada uno.</summary>
    private static IEnumerable<string> TestigosDentroDe(Type raiz)
    {
        foreach (Type tipo in Recorrer(raiz))
        {
            foreach (PropertyInfo propiedad in Propiedades(tipo))
            {
                // Dos detectores, y hacen falta los dos. Por TIPO caza al que se llame `Sello` y
                // sea un `VersionDeRecurso`; por NOMBRE caza al que se llame `Version` y sea un
                // `uint` copiado a mano, que es la forma en que esto se cuela de verdad.
                if (EsTipoDeTestigo(propiedad.PropertyType))
                {
                    yield return $"{tipo.Name}.{propiedad.Name} : {propiedad.PropertyType.Name} " +
                        "(es el tipo del testigo de concurrencia)";

                    continue;
                }

                if (Testigo(propiedad.Name) is string motivo)
                {
                    yield return $"{tipo.Name}.{propiedad.Name} ({motivo})";
                }
            }
        }
    }

    private static bool EsTipoDeTestigo(Type tipo) =>
        tipo == typeof(VersionDeRecurso)
        || (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(ConVersion<>));

    /// <summary>El motivo por el que ese nombre es un testigo, o <c>null</c> si no lo es.</summary>
    /// <remarks>
    /// <b>Se miran las palabras sueltas Y las parejas contiguas</b>, y la pareja no es un adorno: la
    /// pregunta de control la trajo. <c>ETag</c> se parte en «E» y «Tag» —ningún separador de
    /// PascalCase distingue un acrónimo de una letra de una palabra de una letra— así que sin la
    /// pareja, una propiedad llamada literalmente <c>ETag</c> no la veía nadie. Y la pareja no abre
    /// la puerta a los falsos positivos que la comparación por contenido tenía: <c>ConversionUm</c>
    /// da «Conversion», «Um» y «ConversionUm», y ninguna de las tres es una palabra de la lista.
    /// </remarks>
    private static string? Testigo(string nombre)
    {
        string[] palabras = [.. Palabras(nombre)];

        for (int i = 0; i < palabras.Length; i++)
        {
            if (s_testigos.TryGetValue(palabras[i], out string? motivo))
            {
                return motivo;
            }

            if (i + 1 < palabras.Length
                && s_testigos.TryGetValue(palabras[i] + palabras[i + 1], out string? deLaPareja))
            {
                return deLaPareja;
            }
        }

        return null;
    }

    /// <summary>
    /// Parte un nombre en PascalCase en sus palabras, tratando una racha de mayúsculas como una
    /// sola: <c>EtiquetaETag</c> son «Etiqueta» y «ETag», y <c>ConversionUm</c> son «Conversion» y
    /// «Um» —ninguna de las dos es «Version», que es todo el asunto—.
    /// </summary>
    private static IEnumerable<string> Palabras(string nombre)
    {
        StringBuilder actual = new();

        for (int i = 0; i < nombre.Length; i++)
        {
            char letra = nombre[i];

            bool empiezaPalabra = char.IsUpper(letra)
                && actual.Length > 0
                && (!char.IsUpper(nombre[i - 1])
                    || (i + 1 < nombre.Length && char.IsLower(nombre[i + 1])));

            if (empiezaPalabra)
            {
                yield return actual.ToString();
                actual.Clear();
            }

            actual.Append(letra);
        }

        if (actual.Length > 0)
        {
            yield return actual.ToString();
        }
    }

    /// <summary>Los tipos del grafo de respuesta, sin repetir y sin dar vueltas.</summary>
    private static IEnumerable<Type> Recorrer(Type raiz)
    {
        HashSet<Type> vistos = [];
        Queue<Type> pendientes = new();

        foreach (Type inicial in Desenvolver(raiz))
        {
            pendientes.Enqueue(inicial);
        }

        while (pendientes.Count > 0)
        {
            Type tipo = pendientes.Dequeue();

            if (!EsPropio(tipo) || !vistos.Add(tipo))
            {
                continue;
            }

            yield return tipo;

            foreach (PropertyInfo propiedad in Propiedades(tipo))
            {
                foreach (Type dentro in Desenvolver(propiedad.PropertyType))
                {
                    pendientes.Enqueue(dentro);
                }
            }
        }
    }

    /// <summary>Quita las envolturas —página, tramo, colección, anulable— y deja lo que hay dentro.</summary>
    private static IEnumerable<Type> Desenvolver(Type tipo)
    {
        if (tipo.IsArray)
        {
            yield return tipo.GetElementType()!;

            yield break;
        }

        if (tipo.IsGenericType)
        {
            // Se devuelve TAMBIÉN el tipo de fuera, no solo sus argumentos: `ConVersion<T>` es
            // justamente una envoltura cuya presencia es la fuga, así que perderla al desenvolver
            // sería perder la mitad de la regla.
            yield return tipo;

            foreach (Type argumento in tipo.GetGenericArguments())
            {
                yield return argumento;
            }

            yield break;
        }

        yield return tipo;
    }

    private static IEnumerable<PropertyInfo> Propiedades(Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(propiedad => propiedad.CanRead && propiedad.GetIndexParameters().Length == 0);

    /// <summary>
    /// Si el tipo es de este proyecto y merece que se le miren las propiedades.
    /// </summary>
    /// <remarks>
    /// Entrar en los tipos del framework y de la BCL no añadiría ninguna fuga —nadie publica un
    /// <c>string</c> con un <c>Version</c> dentro— y sí añadiría un grafo enorme y varias horas de
    /// recorrido. El corte es por el nombre del ensamblado, que es un hecho y no una heurística.
    /// </remarks>
    private static bool EsPropio(Type tipo) =>
        !tipo.IsPrimitive
        && !tipo.IsEnum
        && tipo != typeof(string)
        && !typeof(IEnumerable).IsAssignableFrom(tipo)
        && (tipo.Assembly.GetName().Name?.StartsWith("Bastion.", StringComparison.Ordinal) ?? false);
}
