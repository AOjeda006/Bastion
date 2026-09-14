namespace Bastion.Api.IntegrationTests.Errores;

/// <summary>
/// Lo que NO puede salir por la puerta, pase lo que pase dentro, escrito una vez para todos los
/// sondeos de este carril.
/// </summary>
/// <remarks>
/// <para>
/// Cada línea es algo que le ahorra trabajo a quien esté sondeando: la versión del motor, la forma
/// de la consulta, la ruta del despliegue, el nombre de la máquina o el tipo que ha estallado. La
/// lista vive en el test y no en la cabeza de quien revisa (<c>principios/testing.md</c>), y es
/// una sola para que el sondeo nuevo no nazca con una copia que se queda atrás.
/// </para>
/// <para>
/// <b>Ningún rastro puede estar hecho solo de cifras hexadecimales y guiones</b>, y lo vigila
/// <see cref="LaListaDeRastrosProhibidosTests"/>. Hasta el ítem 1.11 la lista traía «5432», el
/// puerto de PostgreSQL, y la batería local sobre <c>051449d</c> salió roja porque el
/// <c>traceId</c> de un 415 —treinta y dos cifras hexadecimales al azar— decía
/// <c>…e495432077…</c>. Un rastro que cabe en un identificador aleatorio no detecta una fuga: tira
/// un dado en cada respuesta.
/// </para>
/// </remarks>
internal static class RastrosProhibidos
{
    /// <summary>Las cadenas prohibidas, que se buscan sin distinguir mayúsculas.</summary>
    internal static readonly string[] Todos =
    [
        "Npgsql",
        "PostgresException",
        "DbUpdateException",
        "Microsoft.EntityFrameworkCore",
        "System.InvalidOperationException",
        "StackTrace",
        "   at ",
        "SELECT ",
        "INSERT INTO",
        "UPDATE ",
        "DELETE FROM",
        "relation \"",
        "column \"",
        "constraint \"",
        "bastion_pruebas",
        "Host=",
        "Password=",
        "Username=",

        // El puerto, con la sintaxis en la que se fuga —«localhost:5432», «Port=5432»— y nunca
        // suelto: «5432» a secas es hexadecimal, y en treinta y dos cifras al azar cabe en 29
        // posiciones de 16⁴ combinaciones posibles: uno de cada 2 260 traceId.
        ":5432",
        "Port=5432",
        "C:\\",
        "/home/runner",
        "/usr/share",
        ".cs:line",
    ];
}
