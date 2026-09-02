using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Bastion.Api.FunctionalTests.Salud;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Validacion;

/// <summary>
/// Los atributos de validación de todo lo que se puede enviar a la API, <b>usados de verdad</b> con
/// la cultura con la que Bastion formatea: ninguno revienta, y ninguno que escriba sus límites como
/// texto los deja a merced del idioma de la máquina.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto no es una regla de estilo: es la autopsia de un 500.</b>
/// <c>[Range(typeof(decimal), "0.000001", "1000000")]</c> guarda sus dos límites como CADENAS y los
/// convierte la primera vez que valida algo, con <see cref="CultureInfo.CurrentCulture"/>. En
/// <c>es-ES</c> el punto separa millares, así que <c>"0.000001"</c> no es un decimal: el atributo
/// lanza, y como lanza DENTRO del enlazado de modelos, la acción entera contesta 500 a cualquier
/// petición con cuerpo —también a las bien formadas—. La validación no rechaza: se cae.
/// </para>
/// <para>
/// <b>Y por eso la cultura se fija aquí a mano.</b> Un test que se conformara con la cultura del
/// que lo ejecuta sería verde en la CI —los ejecutores de GitHub corren en <c>en-US</c>, donde el
/// punto SÍ es el separador decimal— y rojo solo en el portátil de quien tenga Windows en español.
/// Es el falso verde de manual: la máquina que tiene que avisar es justo la que no puede. Se prueba
/// con <c>es-ES</c> porque es lo que Bastion declara ser (<c>Directory.Build.props</c> pone
/// <c>InvariantGlobalization=false</c> y <c>NeutralLanguage=es-ES</c>).
/// </para>
/// <para>
/// <b>La fuente son los parámetros de la tabla de acciones</b>, no una lista de tipos: lo que se
/// quiere cubrir es exactamente lo que el enlazado de modelos va a validar, y eso es lo que cuelga
/// de cada acción publicada. Un DTO nuevo entra en este barrido el día que un endpoint lo acepta,
/// sin que nadie tenga que acordarse de venir aquí.
/// </para>
/// </remarks>
public sealed class LosLimitesSeLeenEnCulturaInvarianteTests : IDisposable
{
    // La cultura con la que corre la aplicación, y la que hace daño. No se lee del entorno a
    // propósito: ver el segundo párrafo de arriba.
    private static readonly CultureInfo s_culturaDeLaAplicacion = CultureInfo.GetCultureInfo("es-ES");

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ningun_atributo_de_validacion_revienta_al_validar_en_la_cultura_de_la_aplicacion()
    {
        List<Validacion> validaciones = Validaciones();

        // Las dos afirmaciones de conjunto no vacío, y no sobra ninguna. La primera descarta que el
        // recorrido se haya quedado sin tipos que recorrer -un cambio en cómo se publican las
        // acciones dejaría este barrido mirando una lista vacía y saliendo verde-. La segunda es
        // más estrecha y es la que importa: de todos los atributos, los únicos que pueden reventar
        // por la cultura son los que escriben sus límites como texto, y si no queda ninguno este
        // test no está comprobando nada aunque siga pasando.
        validaciones.ShouldNotBeEmpty("el recorrido no ha encontrado ni un atributo de validación");
        validaciones.Count(validacion => EscribeSusLimitesComoTexto(validacion.Atributo))
            .ShouldBeGreaterThan(0, "no queda ningún límite escrito como texto que comprobar");

        List<string> reventadas = Sondear(validaciones);

        // `IsValid(null)` no es un caso de prueba: es la manera de obligar al atributo a preparar
        // su conversión, que es donde está la avería. Lo que se afirma no es que null valga, es que
        // preguntar no mata.
        reventadas.ShouldBeEmpty(
            "estos atributos lanzan al validar en es-ES, así que su acción contesta 500 a toda " +
            "petición con cuerpo: " + string.Join("; ", reventadas));
    }

    [Fact]
    public void Todo_limite_escrito_como_texto_se_lee_en_cultura_invariante()
    {
        List<Validacion> conLimitesDeTexto =
            [.. Validaciones().Where(validacion => EscribeSusLimitesComoTexto(validacion.Atributo))];

        conLimitesDeTexto.ShouldNotBeEmpty("no queda ningún límite escrito como texto que comprobar");

        List<string> aMerced = [.. conLimitesDeTexto
            .Where(validacion => !((RangeAttribute)validacion.Atributo).ParseLimitsInInvariantCulture)
            .Select(validacion => validacion.Donde)];

        // Es una regla aparte del test de arriba, y no la misma dicha dos veces. Aquel comprueba
        // que hoy no revienta; este, que no depende de la suerte. Un `[Range(typeof(DateTime),
        // "01/02/2020", ...)]` sin la bandera NO lanza en ninguna de las dos culturas: parsea, y
        // significa febrero en una y enero en la otra. Ese fallo no tiene excepción que atrapar, y
        // solo se ve desde aquí.
        aMerced.ShouldBeEmpty(
            "estos límites se convierten con la cultura de la máquina, así que el rango que " +
            "imponen depende de dónde corra el proceso; pon ParseLimitsInInvariantCulture = true: " +
            string.Join(", ", aMerced));
    }

    [Fact]
    public void Todos_los_atributos_encontrados_se_pueden_sondear()
    {
        // Un atributo que solo implementa `IsValid(object, ValidationContext)` deja la sobrecarga
        // simple lanzando `NotImplementedException`, así que el sondeo de arriba no podría
        // distinguirlo de una avería y tendría que saltárselo. Hoy no hay ninguno -solo se usan
        // Required, StringLength y Range-, y este test está para que el día que aparezca uno se vea
        // aquí y se decida cómo probarlo, en vez de quedar callado dentro de un `catch`.
        List<string> ciegos = [.. Validaciones()
            .Where(validacion => !SePuedeSondear(validacion.Atributo))
            .Select(validacion => validacion.Donde)];

        ciegos.ShouldBeEmpty(
            "estos atributos no responden a IsValid(object) y quedan fuera del sondeo: " +
            string.Join(", ", ciegos));
    }

    // El sondeo corre en su propio hilo con su cultura puesta, y no tocando la del hilo del test:
    // xunit ejecuta colecciones en paralelo y `CurrentCulture` es del hilo, así que cambiarla aquí
    // se la cambiaría de paso a lo que estuviera corriendo al lado. Un test que altera el entorno
    // de otro no se le nota a nadie hasta que falla el otro.
    private static List<string> Sondear(IEnumerable<Validacion> validaciones)
    {
        List<string> reventadas = [];

        Thread hilo = new(() =>
        {
            foreach (Validacion validacion in validaciones.Where(cual => SePuedeSondear(cual.Atributo)))
            {
                try
                {
                    validacion.Atributo.IsValid(null);
                }
                catch (Exception error)
                {
                    reventadas.Add($"{validacion.Donde}: {error.GetType().Name} :: {error.Message}");
                }
            }
        })
        {
            CurrentCulture = s_culturaDeLaAplicacion,
            IsBackground = true,
        };

        hilo.Start();
        hilo.Join();

        return reventadas;
    }

    private static bool SePuedeSondear(ValidationAttribute atributo) =>
        atributo.GetType().GetMethod(nameof(ValidationAttribute.IsValid), [typeof(object)])?.DeclaringType
            != typeof(ValidationAttribute);

    private static bool EscribeSusLimitesComoTexto(ValidationAttribute atributo) =>
        atributo is RangeAttribute rango && (rango.Minimum is string || rango.Maximum is string);

    // Recursivo porque un DTO puede llevar dentro otro DTO, y el enlazado de modelos valida los dos.
    // Los tipos ya vistos se saltan, que es lo que impide que una referencia circular cuelgue esto.
    private static void Recorrer(Type tipo, HashSet<Type> vistos, List<Validacion> encontradas)
    {
        Type real = Nullable.GetUnderlyingType(tipo) ?? tipo;

        // Solo lo nuestro: recorrer `string` o `Guid` no daría ningún atributo y sí un paseo por
        // media biblioteca base.
        if (real.Assembly.GetName().Name?.StartsWith("Bastion.", StringComparison.Ordinal) != true ||
            !vistos.Add(real))
        {
            return;
        }

        foreach (PropertyInfo propiedad in real.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (ValidationAttribute atributo in
                propiedad.GetCustomAttributes<ValidationAttribute>(inherit: true))
            {
                encontradas.Add(new Validacion(
                    $"{real.Name}.{propiedad.Name} -> {atributo.GetType().Name}", atributo));
            }

            Recorrer(propiedad.PropertyType, vistos, encontradas);
        }
    }

    private List<Validacion> Validaciones()
    {
        HashSet<Type> vistos = [];
        List<Validacion> encontradas = [];

        IReadOnlyList<ActionDescriptor> acciones = _api.Services
            .GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors
            .Items;

        foreach (ParameterDescriptor parametro in acciones.SelectMany(accion => accion.Parameters))
        {
            // Los atributos del propio parámetro además de los de su tipo: un `[FromQuery, Range]`
            // escrito en la firma de la acción valida igual y no aparecería recorriendo tipos.
            if (parametro is ControllerParameterDescriptor deControlador)
            {
                foreach (ValidationAttribute atributo in
                    deControlador.ParameterInfo.GetCustomAttributes<ValidationAttribute>(inherit: true))
                {
                    encontradas.Add(new Validacion(
                        $"parámetro {deControlador.Name} -> {atributo.GetType().Name}", atributo));
                }
            }

            Recorrer(parametro.ParameterType, vistos, encontradas);
        }

        return encontradas;
    }

    /// <summary>Un atributo de validación y el sitio del que se ha sacado, para poder nombrarlo.</summary>
    /// <param name="Donde">Tipo y propiedad -o parámetro- donde está escrito.</param>
    /// <param name="Atributo">
    /// La instancia, recién creada por reflexión. Que sea recién creada importa:
    /// <see cref="RangeAttribute"/> guarda su conversión tras el primer uso, y una instancia
    /// reutilizada podría venir ya convertida con otra cultura.
    /// </param>
    private sealed record Validacion(string Donde, ValidationAttribute Atributo);
}
