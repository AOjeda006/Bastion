using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El <c>EventId</c> de un <c>[LoggerMessage]</c> lo lleva un registro y solo uno, en todos los
/// ensamblados de Bastion.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta regla nace de haberla incumplido.</b> El despachador de la bandeja estrenó el 8200 el
/// 2026-08-29 para un evento ya procesado, y dos días después <c>AccesoALoBloqueado</c> tomó el
/// mismo número para una consulta que ve lo bloqueado. Nada se puso rojo: el generador de los
/// registros solo avisa de un número repetido dentro de una misma clase (SYSLIB1006), y entre dos
/// clases no lo mira nadie. Se vio al escribir el ADR-0078, en el 3.4, y el del despachador pasó al
/// 8202 en su epílogo.
/// </para>
/// <para>
/// <b>Por qué no es cosmético.</b> El número es lo que filtra un panel o una alerta: «los
/// reintentos del despachador», «quién ve lo bloqueado». Con dos registros en el mismo número, el
/// filtro trae los dos, y quien mira una consulta del artículo 32 ve también la bandeja.
/// </para>
/// <para>
/// <b>Lo que no mira.</b> Un <c>[LoggerMessage]</c> sin <c>EventId</c> lleva el −1 en el atributo,
/// y el número que sale al registro lo pone el generador. Esos no se comparan: nadie los eligió, y
/// ningún panel los filtra por número. El primer caso dice cuántos son, para que se vea.
/// </para>
/// </remarks>
public sealed partial class ElNumeroDeUnRegistroEsSuyoYDeNadieMasTests
{
    /// <summary>El valor que lleva el atributo cuando no se le pone número.</summary>
    private const int SinNumero = -1;

    /// <summary>El número de la pareja. Cualquiera vale: la pareja vive en este ensamblado.</summary>
    private const int NumeroDeLaPareja = 9999;

    /// <summary>
    /// Que haya registros que mirar, porque un barrido que no abre nada pasa el caso de los
    /// repetidos igual de bien que uno sin repetidos.
    /// </summary>
    [Fact]
    public void El_barrido_encuentra_los_registros_numerados_de_Bastion()
    {
        IReadOnlyList<Registro> registros = Registros(TiposDeBastion());
        int numerados = registros.Count(registro => registro.Numero != SinNumero);
        int sinNumero = registros.Count - numerados;

        // Un mínimo y no «no vacío»: hoy son veintiséis numerados, del 8100 al 8705, así que un
        // barrido que encuentre tres está roto aunque no esté vacío.
        numerados.ShouldBeGreaterThan(
            20,
            $"solo se han leído {numerados.ToString(CultureInfo.InvariantCulture)} registros con " +
            $"EventId, y {sinNumero.ToString(CultureInfo.InvariantCulture)} sin él. O el barrido ha " +
            "dejado de abrir los ensamblados, o ya no lee el atributo, y las dos cosas dejan esta " +
            "regla en verde sin haber comprobado nada");
    }

    /// <summary>Ningún número lo llevan dos registros.</summary>
    [Fact]
    public void Ningun_EventId_lo_llevan_dos_registros()
    {
        Repetidos(Registros(TiposDeBastion())).ShouldBeEmpty(
            "hay EventId que llevan dos registros. El número es lo que filtra un panel o una " +
            "alerta, así que un número compartido mezcla dos sucesos en el mismo filtro. Se arregla " +
            "moviendo el más nuevo a un número libre del tramo de su clase");
    }

    /// <summary>
    /// La pareja: dos registros de verdad, compilados por el generador en dos clases, con el mismo
    /// número. Es la forma exacta del 8200, y la regla tiene que marcarla.
    /// </summary>
    [Fact]
    public void Un_numero_repetido_entre_dos_clases_se_marca()
    {
        UnRegistro.Anotar(NullLogger.Instance);
        OtroRegistro.Anotar(NullLogger.Instance);

        Repetidos(Registros([typeof(UnRegistro), typeof(OtroRegistro)])).ShouldBe(
            [$"{NumeroDeLaPareja.ToString(CultureInfo.InvariantCulture)}: OtroRegistro.Anotar, UnRegistro.Anotar"],
            "la regla no ve dos registros con el mismo número en dos clases, que es justo lo que el " +
            "generador deja pasar");
    }

    /// <summary>
    /// Los tipos de los ensamblados de Bastion: los de cada módulo y del bloque común, como el
    /// resto de este carril, y el de la API, que tiene sus registros del arranque.
    /// </summary>
    private static IReadOnlyList<Type> TiposDeBastion()
    {
        var api = Assembly.Load(new AssemblyName(Inventario.Raiz + ".Api"));

        return
        [
            .. from ensamblado in Ensamblados.Todos.Values.Append(api)
               from tipo in ensamblado.GetTypes()
               select tipo,
        ];
    }

    /// <summary>Cada método con <c>[LoggerMessage]</c>, con su número y su sitio.</summary>
    private static IReadOnlyList<Registro> Registros(IEnumerable<Type> tipos) =>
    [
        .. from tipo in tipos
           from metodo in tipo.GetMethods(
               BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance |
               BindingFlags.DeclaredOnly)
           let atributo = metodo.GetCustomAttribute<LoggerMessageAttribute>()
           where atributo is not null
           select new Registro(atributo.EventId, $"{tipo.Name}.{metodo.Name}"),
    ];

    /// <summary>Los números con más de un registro, cada uno con sus sitios en orden.</summary>
    private static IReadOnlyList<string> Repetidos(IEnumerable<Registro> registros) =>
    [
        .. from registro in registros
           where registro.Numero != SinNumero
           group registro.Sitio by registro.Numero into mismo
           where mismo.Count() > 1
           orderby mismo.Key
           select $"{mismo.Key.ToString(CultureInfo.InvariantCulture)}: " +
                  string.Join(", ", mismo.Order(StringComparer.Ordinal)),
    ];

    /// <summary>Un registro: su número, o −1 si no lo lleva, y la clase y el método que lo declaran.</summary>
    private sealed record Registro(int Numero, string Sitio);

    private static partial class UnRegistro
    {
        [LoggerMessage(EventId = NumeroDeLaPareja, Level = LogLevel.Debug, Message = "Uno de la pareja.")]
        internal static partial void Anotar(ILogger registro);
    }

    private static partial class OtroRegistro
    {
        [LoggerMessage(EventId = NumeroDeLaPareja, Level = LogLevel.Debug, Message = "El otro de la pareja.")]
        internal static partial void Anotar(ILogger registro);
    }
}
