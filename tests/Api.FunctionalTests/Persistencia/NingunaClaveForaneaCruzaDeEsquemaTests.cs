using Bastion.Api.FunctionalTests.Salud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Persistencia;

/// <summary>
/// La regla 4 del §5, comprobada sobre el modelo entero y sin base de datos: ninguna clave ajena
/// sale del esquema de su módulo.
/// </summary>
/// <remarks>
/// <para>
/// <b>La garantía ya existía, y estaba enumerada.</b> Hasta este ítem se comprobaba módulo a
/// módulo y contra PostgreSQL: <c>EsquemaDelModuloTests</c> lo pregunta de Organización y
/// <c>EsquemaDeTercerosTests</c> de Terceros, cada uno con su consulta a
/// <c>information_schema</c>. Es el mismo modo de fallo que el ítem 1.6 le encontró a las cuatro
/// reglas de «cada entidad declara…»: un módulo nuevo no aparece en ninguna de las listas y su
/// garantía no se comprueba, sin que nada se ponga rojo. Catálogo habría entrado exactamente así.
/// </para>
/// <para>
/// Por eso esta versión no pregunta por módulos: recorre el <b>universo descubierto</b> de
/// <see cref="LosModelosDeCadaModulo"/>, que es toda clase concreta que hereda de
/// <c>ContextoDeModulo</c>. Un módulo que se monte mañana entra solo.
/// </para>
/// <para>
/// <b>Y corre en el carril rápido, que es el otro cambio.</b> Las dos comprobaciones de
/// integración necesitan Testcontainers: con Docker parado no se ejecutan, y un desarrollador
/// puede escribir la clave ajena que cruza, verla compilar, verse el carril rápido en verde y
/// enterarse en la CI — o no enterarse, si ese módulo no tenía su fichero de esquema. El modelo
/// de EF Core sabe a qué esquema va cada tabla antes de que exista ninguna: no hace falta la base
/// de datos para responder.
/// </para>
/// <para>
/// <b>No sustituye a las dos de integración, y no debe.</b> Ellas preguntan por el esquema que
/// hay <i>de verdad</i> en PostgreSQL, que es lo único que descarta que la migración diga otra
/// cosa que el modelo. Esta responde antes y para todos. Las dos afirman cosas distintas.
/// </para>
/// </remarks>
public sealed class NingunaClaveForaneaCruzaDeEsquemaTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ninguna_clave_foranea_cruza_de_esquema()
    {
        List<string> cruzadas = [.. Foraneas()
            .Where(una => !string.Equals(una.Desde, una.Hasta, StringComparison.Ordinal))
            .Select(una => $"{una.Nombre}: {una.Desde} -> {una.Hasta}")];

        cruzadas.Sort(StringComparer.Ordinal);

        // Una clave ajena entre esquemas ata los dos módulos a migrarse y desplegarse juntos para
        // siempre, y lo hace en silencio: compila, migra y funciona. Lo que se pierde no se nota
        // hasta el día que uno de los dos tiene que moverse solo.
        //
        // Lo que se sustituye NO es la clave ajena por nada: es por un `uuid` y un puerto del
        // Contracts del dueño que dice si eso existe y en qué estado está (ADR-0024). Guardar el
        // identificador sin preguntarle a nadie es la cuarta vía, y esa la vigila
        // `LosIdentificadoresAjenosTests`. Las dos reglas juntas son la frontera; cada una sola
        // deja pasar la mitad.
        cruzadas.ShouldBeEmpty(
            "hay claves ajenas que salen del esquema de su módulo (§5, regla 4). Lo que cruza de " +
            "módulo se guarda como identificador y se valida por el puerto del dueño:" +
            Environment.NewLine + string.Join(Environment.NewLine, cruzadas));
    }

    /// <summary>El arnés: que haya claves ajenas que mirar, y más de un esquema donde cruzar.</summary>
    /// <remarks>
    /// Las dos maneras que tiene la regla de arriba de salir verde sin afirmar nada, y ninguna
    /// tendría otro síntoma. Sin claves ajenas en el barrido, la lista de cruzadas está vacía
    /// porque no hay ninguna clave, no porque ninguna cruce. Y con un solo esquema en el modelo,
    /// «ninguna cruza» es cierto por aritmética: no hay adónde cruzar.
    /// </remarks>
    [Fact]
    public void El_barrido_ve_claves_foraneas_y_mas_de_un_esquema()
    {
        IReadOnlyList<(string Nombre, string Desde, string Hasta)> foraneas = Foraneas();

        foraneas.ShouldNotBeEmpty(
            "no se ha encontrado ni una clave ajena en el modelo entero, cuando los agregados con " +
            "hijos las tienen por definición. El barrido está roto y la regla de al lado recorre " +
            "una lista vacía");

        SortedSet<string> esquemas = new(
            foraneas.SelectMany(una => new[] { una.Desde, una.Hasta }), StringComparer.Ordinal);

        esquemas.Count.ShouldBeGreaterThan(
            1,
            "todas las claves ajenas del modelo están en un solo esquema (" +
            string.Join(", ", esquemas) + "), así que «ninguna cruza» sale verde por no haber " +
            "adónde cruzar. Con un esquema por módulo (§5), eso significa que el barrido solo " +
            "está viendo un módulo");
    }

    /// <summary>Toda clave ajena del universo descubierto, con el esquema de sus dos extremos.</summary>
    /// <remarks>
    /// Sin repetidas: las tablas COMPARTIDAS —la auditoría, la bandeja y la idempotencia— están
    /// mapeadas en el contexto de cada módulo, así que sus claves aparecerían una vez por módulo.
    /// Contarlas cinco veces no cambiaría el veredicto de la regla, pero sí haría que el arnés
    /// dijera «hay muchas» cuando lo que hay es una vista cinco veces.
    /// </remarks>
    private IReadOnlyList<(string Nombre, string Desde, string Hasta)> Foraneas() =>
    [
        .. new SortedSet<(string, string, string)>(
           from modelo in LosModelosDeCadaModulo.De(_api.Services)
           let porOmision = modelo.GetDefaultSchema()
           from tipo in modelo.GetEntityTypes()
           where tipo.GetTableName() is not null
           from foranea in tipo.GetForeignKeys()
           where foranea.PrincipalEntityType.GetTableName() is not null
           let desde = Esquema(tipo, porOmision)
           let hasta = Esquema(foranea.PrincipalEntityType, porOmision)
           select ($"{tipo.ShortName()}.{foranea.GetConstraintName()}", desde, hasta)),
    ];

    /// <summary>
    /// El esquema al que va una tabla: el suyo si lo declara, y si no el del modelo.
    /// </summary>
    /// <remarks>
    /// <c>GetSchema()</c> devuelve <c>null</c> cuando la tabla se queda con el esquema por omisión
    /// del contexto, que es el caso normal aquí. Leerlo sin este relleno haría que todo saliera
    /// «sin esquema» y que ninguna clave cruzara nunca: verde perfecto y ciego.
    /// </remarks>
    private static string Esquema(IReadOnlyEntityType tipo, string? porOmision) =>
        tipo.GetSchema() ?? porOmision ?? "(sin esquema)";
}
