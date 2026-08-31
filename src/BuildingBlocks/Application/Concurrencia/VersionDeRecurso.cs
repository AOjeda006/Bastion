using System.Globalization;
using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Concurrencia;

/// <summary>
/// La versión de un recurso: lo que sale en el <c>ETag</c> de su lectura y lo que el cliente
/// devuelve en <c>If-Match</c> cuando escribe.
/// </summary>
/// <remarks>
/// <para>
/// Por dentro es el testigo de concurrencia de la base —en PostgreSQL, la columna de sistema
/// <c>xmin</c>—, y por eso es un <see cref="uint"/> y no un contador nuestro. Va envuelto en un
/// tipo propio para que no viaje como un número suelto por firmas donde cualquier otro entero
/// encajaría: <c>Modificar(id, 756, ...)</c> compila igual de bien con el número de página.
/// </para>
/// <para>
/// <b>El valor no significa nada por sí solo y no se ordena.</b> Un <c>xmin</c> mayor no es «más
/// nuevo»: es el identificador de la transacción que escribió la fila, y esos identificadores dan
/// la vuelta. Lo único que se hace con dos versiones es preguntar si son la misma, que es
/// justamente lo que el <c>WHERE</c> del <c>UPDATE</c> pregunta.
/// </para>
/// </remarks>
/// <param name="Valor">El testigo tal como lo devuelve la base.</param>
public readonly record struct VersionDeRecurso(uint Valor)
{
    /// <summary>
    /// El valor tal como se publica en la cabecera <c>ETag</c>: fuerte y entrecomillado.
    /// </summary>
    /// <remarks>
    /// Fuerte —sin el prefijo <c>W/</c>— porque <c>If-Match</c> compara por igualdad estricta
    /// (RFC 9110, §13.1.1) y una etiqueta débil no vale ahí. Las comillas son parte de la
    /// sintaxis, no adorno: sin ellas la cabecera no es un <c>entity-tag</c> válido.
    /// </remarks>
    public string Etiqueta => "\"" + Valor.ToString(CultureInfo.InvariantCulture) + "\"";

    /// <summary>
    /// Lee la versión que trae la cabecera <c>If-Match</c> de una petición de escritura.
    /// </summary>
    /// <param name="cabecera">El valor de <c>If-Match</c>, o nulo si la petición no la trae.</param>
    /// <returns>
    /// La versión, o el desenlace que le corresponde a la petición: <c>428</c> si no viene la
    /// cabecera y <c>400</c> si viene y no se puede leer.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>No se admite el comodín <c>*</c></b>, que en el RFC significa «me vale cualquier versión
    /// con tal de que el recurso exista». Aquí eso es exactamente el agujero que esta cabecera
    /// viene a tapar: un cliente que mandara <c>If-Match: *</c> se saltaría el control de
    /// concurrencia entero sin dejar de cumplir el protocolo, y la actualización perdida volvería
    /// por la puerta grande. Se responde <c>400</c> y se dice qué hay que mandar.
    /// </para>
    /// <para>
    /// Tampoco se admite una lista de etiquetas ni una etiqueta débil, y por lo mismo: se escribe
    /// sobre UNA versión concreta.
    /// </para>
    /// </remarks>
    public static Resultado<VersionDeRecurso> DeLaCabecera(string? cabecera)
    {
        if (string.IsNullOrWhiteSpace(cabecera))
        {
            return Resultado.Fallo<VersionDeRecurso>(ErroresDeConcurrencia.FaltaLaCabecera());
        }

        string limpia = cabecera.Trim();

        if (limpia.Length < 3 || limpia[0] != '"' || limpia[^1] != '"')
        {
            return Resultado.Fallo<VersionDeRecurso>(ErroresDeConcurrencia.CabeceraNoValida(limpia));
        }

        return uint.TryParse(limpia[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out uint valor)
            ? Resultado.Correcto(new VersionDeRecurso(valor))
            : Resultado.Fallo<VersionDeRecurso>(ErroresDeConcurrencia.CabeceraNoValida(limpia));
    }
}
