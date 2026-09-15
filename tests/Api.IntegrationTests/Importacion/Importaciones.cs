using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Infrastructure.Persistencia;
using Npgsql;

namespace Bastion.Api.IntegrationTests.Importacion;

/// <summary>Lo que comparten los casos de la importación de terceros: la ruta, los ficheros y el recuento.</summary>
/// <remarks>
/// <para>
/// <b>Los NIF de estos casos van del 33 000 001 al 33 000 999</b>, y cada clase usa su tramo: los casos
/// comparten base, y dos casos que importaran el mismo identificador en la misma empresa se leerían
/// uno al otro como <c>ya-existe</c>. Todos son <see cref="Escenario.NifInventado"/>: ni aquí ni en los
/// ficheros de Excel hay un identificador de nadie.
/// </para>
/// <para>
/// <b>Las empresas, del 234 al 259</b>, por la misma razón (del 230 al 233 las usa
/// <c>ElReciboCaducaYSeBorraTests</c>).
/// </para>
/// </remarks>
internal static class Importaciones
{
    /// <summary>La ruta de la importación.</summary>
    internal const string Ruta = "/api/v1/terceros/terceros/importacion";

    /// <summary>Primer número de los NIF de terceros de estos casos.</summary>
    internal const int BaseDeNifs = 33_000_000;

    /// <summary>El fichero «CSV (delimitado por comas)» que escribió Excel: Windows-1252 y sin BOM.</summary>
    internal const string ExcelDelimitadoPorComas = "terceros-csv-delimitado-por-comas.csv";

    /// <summary>El fichero «CSV UTF-8» que escribió Excel: UTF-8 con BOM.</summary>
    internal const string ExcelUtf8 = "terceros-csv-utf8.csv";

    /// <summary>La cabecera, tal como la pide el contrato.</summary>
    internal static string Cabecera { get; } = string.Join(';', ImportacionDeTerceros.Cabecera);

    /// <summary>El NIF inventado del orden pedido, dentro del tramo de estos casos.</summary>
    /// <param name="orden">Del 1 al 999.</param>
    internal static string Nif(int orden) => Escenario.NifInventado(BaseDeNifs + orden);

    /// <summary>Una fila válida de arriba abajo, con lo que el caso quiera cambiar.</summary>
    /// <param name="nif">El identificador fiscal, español.</param>
    /// <param name="razonSocial">La razón social.</param>
    /// <param name="esCliente">La columna tal cual.</param>
    /// <param name="esProveedor">La columna tal cual.</param>
    /// <param name="limite">El límite, tal cual; vacío es sin límite.</param>
    /// <param name="divisa">La divisa del límite, tal cual.</param>
    /// <param name="nombreComercial">El nombre comercial, ya con comillas si las necesita.</param>
    internal static string Fila(
        string nif,
        string razonSocial = "Tercero importado",
        string esCliente = "sí",
        string esProveedor = "no",
        string limite = "",
        string divisa = "",
        string nombreComercial = "") => string.Join(
        ';',
        "ES", nif, razonSocial, nombreComercial, "Gran Vía", "31", "28013", "Madrid", "Madrid", "ES",
        esCliente, esProveedor, string.Empty, "no", "no", "no", limite, divisa);

    /// <summary>El fichero en UTF-8 sin BOM, con la cabecera delante y CRLF entre registros, como Excel.</summary>
    /// <param name="filas">Las filas, sin la cabecera.</param>
    internal static byte[] Fichero(params string[] filas) =>
        Encoding.UTF8.GetBytes(string.Join("\r\n", [Cabecera, .. filas]) + "\r\n");

    /// <summary>Uno de los ficheros de Excel, con sus marcadores <c>{{NIF:n}}</c> ya sustituidos.</summary>
    /// <remarks>
    /// Se sustituye byte a byte, leyendo y escribiendo en Latin-1, que hace corresponder cada byte con un
    /// carácter y vuelta: los marcadores son ASCII en las dos codificaciones, y lo demás del fichero —el
    /// BOM, los bytes de la «ñ» en Windows-1252 o en UTF-8, los CRLF— sale exactamente como lo escribió
    /// Excel.
    /// </remarks>
    /// <param name="nombre">El nombre del fichero, en <c>Importacion/Excel</c>.</param>
    /// <param name="primerNif">El orden del NIF que sustituye a <c>{{NIF:1}}</c>; los siguientes, correlativos.</param>
    internal static byte[] DeExcel(string nombre, int primerNif)
    {
        string ruta = Path.Combine(AppContext.BaseDirectory, "Importacion", "Excel", nombre);
        string crudo = Encoding.Latin1.GetString(File.ReadAllBytes(ruta));

        for (int marcador = 1; marcador <= 3; marcador++)
        {
            string buscado = $"{{{{NIF:{marcador.ToString(CultureInfo.InvariantCulture)}}}}}";

            if (!crudo.Contains(buscado, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"El fichero {nombre} no trae el marcador {buscado}.");
            }

            crudo = crudo.Replace(buscado, Nif(primerNif + marcador - 1), StringComparison.Ordinal);
        }

        return Encoding.Latin1.GetBytes(crudo);
    }

    /// <summary>Manda el fichero, con clave si se le da.</summary>
    /// <param name="cliente">El cliente, ya en su empresa.</param>
    /// <param name="fichero">Los bytes del cuerpo.</param>
    /// <param name="clave">La clave de idempotencia; sin ella, la petición no la lleva.</param>
    internal static Task<HttpResponseMessage> ImportarAsync(HttpClient cliente, byte[] fichero, string? clave = null)
    {
        ByteArrayContent contenido = new(fichero);
        contenido.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        return ImportarAsync(cliente, contenido, clave);
    }

    /// <summary>Manda un contenido cualquiera a la ruta de la importación.</summary>
    /// <param name="cliente">El cliente, ya en su empresa.</param>
    /// <param name="contenido">El cuerpo, con su tipo.</param>
    /// <param name="clave">La clave de idempotencia; sin ella, la petición no la lleva.</param>
    internal static Task<HttpResponseMessage> ImportarAsync(HttpClient cliente, HttpContent contenido, string? clave)
    {
        HttpRequestMessage peticion = new(HttpMethod.Post, Ruta) { Content = contenido };

        if (clave is not null)
        {
            peticion.Headers.TryAddWithoutValidation(FiltroDeIdempotencia.Cabecera, clave);
        }

        return cliente.SendAsync(peticion);
    }

    /// <summary>El informe de una respuesta <c>200</c>.</summary>
    /// <param name="respuesta">La respuesta.</param>
    internal static async Task<InformeDeImportacionDto> InformeAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<InformeDeImportacionDto>().ConfigureAwait(false))!;

    /// <summary>Los terceros de la empresa con esos identificadores, bloqueados o no, contados en la tabla.</summary>
    /// <remarks>
    /// Con SQL y no con el contexto: «no ha entrado» tiene que querer decir que no hay fila, y el contexto
    /// pasa por los filtros de empresa y de bloqueo, que es justo lo que esta cuenta no puede atravesar.
    /// </remarks>
    /// <param name="postgres">La base.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="nifs">Los identificadores buscados.</param>
    internal static async Task<long> CuantosAsync(PostgresConTodosLosModulos postgres, Guid empresaId, params string[] nifs)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync().ConfigureAwait(false);

        await using NpgsqlCommand orden = new(
            $"SELECT count(*) FROM {TercerosDbContext.Esquema}.terceros " +
            "WHERE empresa_id = @empresa AND identificacion_numero = ANY(@nifs)",
            conexion);
        orden.Parameters.AddWithValue("empresa", empresaId);
        orden.Parameters.AddWithValue("nifs", nifs);

        return (long)(await orden.ExecuteScalarAsync().ConfigureAwait(false))!;
    }
}
