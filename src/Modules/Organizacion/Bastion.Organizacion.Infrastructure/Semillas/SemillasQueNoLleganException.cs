namespace Bastion.Organizacion.Infrastructure.Semillas;

/// <summary>
/// Las semillas de <c>db/semillas/</c> no han llegado al sitio donde se cargan, o han llegado
/// vacías.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un fallo de construcción, no de datos.</b> Nadie que use el sistema puede provocarlo: o
/// el ensamblado se publicó sin los ficheros —falta el <c>&lt;Content Include&gt;</c>, o el
/// <c>.dockerignore</c> los dejó fuera del contexto—, o alguien editó un <c>.json</c> y lo dejó
/// sin filas. Las dos cosas se arreglan en el repositorio, y las dos se ven en la CI.
/// </para>
/// <para>
/// Por eso lanza en vez de anotar un aviso: el migrador la convierte en código de salida 1 y el
/// compose no arranca la API. Un aviso dejaría el sistema en pie con el catálogo de impuestos
/// vacío, y el síntoma —«no puedo facturar, no hay tipos de IVA»— aparecería semanas después y en
/// otro sitio.
/// </para>
/// </remarks>
public sealed class SemillasQueNoLleganException : InvalidOperationException
{
    /// <summary>Con el detalle de qué falta y dónde se buscó.</summary>
    /// <param name="message">El mensaje.</param>
    public SemillasQueNoLleganException(string message)
        : base(message)
    {
    }

    /// <summary>Con un mensaje propio y la causa.</summary>
    /// <param name="message">El mensaje.</param>
    /// <param name="innerException">La causa.</param>
    public SemillasQueNoLleganException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Sin detalle.</summary>
    public SemillasQueNoLleganException()
        : base("Las semillas de Organización no han llegado al sitio donde se cargan.")
    {
    }
}
