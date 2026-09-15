using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Errores;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Bastion.BuildingBlocks.Infrastructure.CuerpoDeLaPeticion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Pruebas.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Importacion;

/// <summary>
/// Lo que la importación hace con un fichero que no es un fichero de Excel, contado con cuatro criterios
/// escritos antes de mandar el primero.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los cuatro criterios, por cada fichero hostil:</b>
/// </para>
/// <list type="number">
///   <item><b>Contesta</b>, con el código que le toca y nunca con un <c>5xx</c>, y cuando es un error con
///   nombre, con ESE nombre.</item>
///   <item><b>No cuenta nada de dentro</b>: ninguno de <see cref="RastrosProhibidos"/>, la lista que vive en
///   el test, en el cuerpo.</item>
///   <item><b>No devuelve lo que le mandaron</b>: el canario no sale ni en el cuerpo, ni en el recibo que se
///   guarda, ni en el registro del host.</item>
///   <item><b>Sigue atendiendo</b>: detrás de cada fichero hostil va uno bueno —uno de los que escribió
///   Excel, con NIF nuevos— y entra entero.</item>
/// </list>
/// <para>
/// <b>«Contesta» y no «contesta o cierra», y es más estricto a propósito.</b> Aquí delante está el servidor
/// de pruebas, y en él una conexión cerrada no se distingue de una excepción que se escapa de la tubería:
/// admitir el cierre sería admitir la caída. Ninguno de estos ficheros tiene por qué cerrar la conexión —el
/// que no cabe se contesta con <c>413</c>—; el cierre de verdad es cosa del proxy, y lo mira el humo.
/// </para>
/// <para>
/// <b>El canario se busca por su núcleo ASCII, <c>QZX</c>.</b> El canario entero lleva «Ñ» y «Ú» para que
/// un fichero leído en Windows-1252 no lo devuelva tal cual: si una respuesta lo repitiera mal decodificado,
/// buscar el canario entero no lo encontraría. Tres mayúsculas que no son hexadecimales no caben en un
/// <c>traceId</c> ni en un GUID, así que el núcleo tampoco aparece por azar.
/// </para>
/// <para>
/// <b>El registro se lee con un host propio</b>, que añade a los captadores de siempre uno que se queda con
/// TODO lo que se anota: mensaje, propiedades y excepción. Y ese captador se comprueba a sí mismo al final,
/// antes de fiarse de su silencio: tiene que haber visto el suceso 8401 de una importación de este host y,
/// después de afirmar que no hay canario, el canario de una ruta que lo lleva en el nombre.
/// </para>
/// <para>
/// Empresa 247, NIF buenos desde el 301 y NIF de las filas hostiles desde el 901 (<see cref="Importaciones"/>).
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaImportacionAguantaFicherosHostilesTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Canario = "CANARIO-ÑANDÚ-QZX";
    private const string NucleoDelCanario = "QZX";
    private const int SucesoDeLaImportacionConOcupados = 8401;
    private const int PrimerNifBueno = 301;
    private const string TextoCsv = "text/csv";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly TodoLoQueSeAnota _anotado = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Ningun_fichero_hostil_tumba_la_importacion_ni_cuenta_nada_ni_devuelve_lo_que_le_mandaron()
    {
        using WebApplicationFactory<Program> host = _api.WithWebHostBuilder(
            constructor => constructor.ConfigureTestServices(
                servicios => CapturaDeRegistro.Registrar(
                    servicios, new RegistroDeFallos(), new RegistroDeSucesos(), _anotado)));

        using HttpClient cliente = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        SesionDto sesion = await Sesiones.AutenticarAsync(
            cliente, ApiDeVerdad.CorreoDelAdministrador, ApiDeVerdad.ContrasenaDelAdministrador);
        EmpresaDto empresa = await Escenario.CrearEmpresaAsync(cliente, Escenario.NifInventado(247));
        await Escenario.EntrarEnAsync(cliente, sesion.UsuarioId, empresa.Id);

        IReadOnlyList<FicheroHostil> hostiles = Hostiles();
        int correctas = 0;
        int siguienteNifBueno = PrimerNifBueno;

        foreach (FicheroHostil hostil in hostiles)
        {
            using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(
                cliente, hostil.Contenido(), Guid.NewGuid().ToString());
            string cuerpo = await respuesta.Content.ReadAsStringAsync();

            respuesta.StatusCode.ShouldBe(hostil.Estado, $"{hostil.Nombre}: {cuerpo} {RegistroDeFallos.Ultimos}");
            ElTipoEsElQueToca(hostil, cuerpo);

            foreach (string rastro in RastrosProhibidos.Todos)
            {
                cuerpo.ShouldNotContain(rastro, Case.Insensitive, $"{hostil.Nombre}: la respuesta cuenta «{rastro}»");
            }

            cuerpo.ShouldNotContain(nameof(FicheroCsv), Case.Insensitive, $"{hostil.Nombre}: la respuesta nombra el tipo del parámetro");
            cuerpo.ShouldNotContain(NucleoDelCanario, Case.Sensitive, $"{hostil.Nombre}: la respuesta devuelve lo que le mandaron");

            if (respuesta.IsSuccessStatusCode)
            {
                correctas++;
            }

            // Y el bueno de detrás, con NIF nuevos y alternando los dos ficheros de Excel.
            string excel = (siguienteNifBueno - PrimerNifBueno) / 3 % 2 == 0
                ? Importaciones.ExcelUtf8
                : Importaciones.ExcelDelimitadoPorComas;

            using HttpResponseMessage buena = await Importaciones.ImportarAsync(
                cliente, Importaciones.DeExcel(excel, siguienteNifBueno), Guid.NewGuid().ToString());

            buena.StatusCode.ShouldBe(
                HttpStatusCode.OK, $"detrás de «{hostil.Nombre}», el fichero bueno no entra: {await Escenario.Detalle(buena)}");
            InformeDeImportacionDto informe = await Importaciones.InformeAsync(buena);
            informe.ShouldBe(
                informe with { Leidas = 3, Importadas = 3, Rechazadas = 0 },
                $"detrás de «{hostil.Nombre}», el fichero bueno no entra entero");

            correctas++;
            siguienteNifBueno += 3;
        }

        // El recibo: uno por cada respuesta correcta, y ninguno con el canario. La igualdad es la que dice
        // que la búsqueda ha mirado algo.
        await using (AuditoriaDbContext auditoria = postgres.AbrirAuditoria(empresa.Id))
        {
            List<RegistroDeIdempotencia> recibos = await auditoria.Set<RegistroDeIdempotencia>()
                .AsNoTracking()
                .ToListAsync();

            recibos.Count.ShouldBe(correctas, "cada respuesta correcta deja su recibo, y solo ellas");
            recibos.ShouldAllBe(recibo => recibo.Cuerpo == null || !recibo.Cuerpo.Contains(NucleoDelCanario));
        }

        await ElRegistroNoLlevaElCanarioYSeSabeQueMiraAsync(cliente);
    }

    // Los ficheros, cada uno con lo que tiene que contestar. El canario va en todos los que llegan a leerse.
    private static List<FicheroHostil> Hostiles()
    {
        string cabecera = Importaciones.Cabecera;
        string conCanario = Importaciones.Fila(Importaciones.Nif(901), razonSocial: Canario);
        string[] cabeceraEnColumnas = [.. ImportacionDeTerceros.Cabecera];

        const string CabeceraNoValida = "/errors/importacion-cabecera-no-valida";
        const string SeparadorNoAdmitido = "/errors/importacion-separador-no-admitido";
        const string CodificacionNoAdmitida = "/errors/importacion-codificacion-no-admitida";
        const string FinDeLineaNoAdmitido = "/errors/importacion-fin-de-linea-no-admitido";
        const string ComillasSinCerrar = "/errors/importacion-comillas-sin-cerrar";
        string[] cualquieraDeLosDelFichero =
            [CabeceraNoValida, SeparadorNoAdmitido, CodificacionNoAdmitida, FinDeLineaNoAdmitido, ComillasSinCerrar];

        static byte[] Utf8(string texto) => Encoding.UTF8.GetBytes(texto);
        static byte[] Con(Encoding codificacion, string texto) => [.. codificacion.GetPreamble(), .. codificacion.GetBytes(texto)];

        int casiDosMiB = (int)ImportacionDeTerceros.TopeDeBytes - (100 * 1024);
        string canarioRepetido = string.Concat(Enumerable.Repeat(Canario, casiDosMiB / Encoding.UTF8.GetByteCount(Canario)));

        return
        [
            new("vacío", [], TextoCsv, HttpStatusCode.BadRequest, CabeceraNoValida),
            new("solo el BOM de UTF-8", [0xEF, 0xBB, 0xBF], TextoCsv, HttpStatusCode.BadRequest, CabeceraNoValida),
            new("la cabecera sola", Utf8(cabecera + "\r\n"), TextoCsv, HttpStatusCode.OK),
            new("la cabecera con el canario de columna diecinueve", Utf8($"{cabecera};{Canario}\r\n"), TextoCsv, HttpStatusCode.BadRequest, CabeceraNoValida),
            new(
                "separado por comas",
                Utf8(string.Join(',', cabeceraEnColumnas) + "\r\n" + conCanario.Replace(';', ',') + "\r\n"),
                TextoCsv,
                HttpStatusCode.BadRequest,
                SeparadorNoAdmitido),
            new(
                "separado por tabuladores",
                Utf8(string.Join('\t', cabeceraEnColumnas) + "\r\n" + conCanario.Replace(';', '\t') + "\r\n"),
                TextoCsv,
                HttpStatusCode.BadRequest,
                SeparadorNoAdmitido),
            new("UTF-16 con BOM", Con(new UnicodeEncoding(false, true), $"{cabecera}\r\n{conCanario}\r\n"), TextoCsv, HttpStatusCode.BadRequest, CodificacionNoAdmitida),
            new("UTF-16 big endian con BOM", Con(new UnicodeEncoding(true, true), $"{cabecera}\r\n{conCanario}\r\n"), TextoCsv, HttpStatusCode.BadRequest, CodificacionNoAdmitida),
            new("UTF-32 con BOM", Con(new UTF32Encoding(false, true), $"{cabecera}\r\n{conCanario}\r\n"), TextoCsv, HttpStatusCode.BadRequest, CodificacionNoAdmitida),
            new("UTF-32 big endian con BOM", Con(new UTF32Encoding(true, true), $"{cabecera}\r\n{conCanario}\r\n"), TextoCsv, HttpStatusCode.BadRequest, CodificacionNoAdmitida),
            new("un byte nulo a mitad de fila", [.. Utf8($"{cabecera}\r\n{Canario}"), 0x00, .. Utf8($"{conCanario}\r\n")], TextoCsv, HttpStatusCode.BadRequest, CodificacionNoAdmitida),
            new(
                "el BOM de UTF-8 y detrás una secuencia que no lo es",
                [0xEF, 0xBB, 0xBF, .. Utf8($"{cabecera}\r\n{Canario}"), 0xC0, 0xAF, .. Utf8($"{conCanario}\r\n")],
                TextoCsv,
                HttpStatusCode.BadRequest,
                CodificacionNoAdmitida),
            new(
                "una secuencia sobrelarga sin BOM, que se lee en Windows-1252",
                [.. Utf8($"{cabecera}\r\n{Importaciones.Fila(Importaciones.Nif(902), razonSocial: Canario)}"), 0xC0, 0xAF, .. Utf8("\r\n")],
                TextoCsv,
                HttpStatusCode.OK),
            new("un CR suelto de fin de línea", Utf8($"{cabecera}\r{conCanario}\r\n"), TextoCsv, HttpStatusCode.BadRequest, FinDeLineaNoAdmitido),
            new("un LF seguido de un CR", Utf8($"{cabecera}\n\r{conCanario}\r\n"), TextoCsv, HttpStatusCode.BadRequest, FinDeLineaNoAdmitido),
            new("unas comillas que no cierran", Utf8($"{cabecera}\r\nES;\"{Canario};{Canario}\r\n"), TextoCsv, HttpStatusCode.BadRequest, ComillasSinCerrar),
            new(
                "unas comillas mal colocadas con el canario dentro",
                Utf8($"{cabecera}\r\n{Importaciones.Fila(Importaciones.Nif(903), razonSocial: $"a\"{Canario}\"b")}\r\n"),
                TextoCsv,
                HttpStatusCode.OK),
            new("el canario en cada campo", Utf8($"{cabecera}\r\n{string.Join(';', Enumerable.Repeat(Canario, cabeceraEnColumnas.Length))}\r\n"), TextoCsv, HttpStatusCode.OK),
            new(
                "un campo de casi 2 MiB",
                Utf8($"{cabecera}\r\n{Importaciones.Fila(Importaciones.Nif(904), razonSocial: canarioRepetido)}\r\n"),
                TextoCsv,
                HttpStatusCode.OK),
            new("una línea de casi 2 MiB sin un separador", Utf8($"{cabecera}\r\n{canarioRepetido}\r\n"), TextoCsv, HttpStatusCode.OK),
            new("cien mil separadores en una fila", Utf8($"{cabecera}\r\n{Canario}{new string(';', 100_000)}\r\n"), TextoCsv, HttpStatusCode.OK),
            new("bytes sin forma", BytesSinForma(256 * 1024, sinNulos: false), TextoCsv, HttpStatusCode.BadRequest, cualquieraDeLosDelFichero),
            new("bytes sin forma y sin un nulo", BytesSinForma(256 * 1024, sinNulos: true), TextoCsv, HttpStatusCode.BadRequest, cualquieraDeLosDelFichero),
            new("un JSON con el tipo de un CSV", Utf8($"{{\"razonSocial\":\"{Canario}\"}}"), TextoCsv, HttpStatusCode.BadRequest, CabeceraNoValida),
            new("un JSON con su tipo", Utf8($"{{\"razonSocial\":\"{Canario}\"}}"), "application/json", HttpStatusCode.UnsupportedMediaType),
            new("un CSV sin decir su tipo", Utf8($"{cabecera}\r\n{Importaciones.Fila(Importaciones.Nif(905), razonSocial: Canario)}\r\n"), null, HttpStatusCode.UnsupportedMediaType),
            new("un charset que dice UTF-16 con bytes de UTF-8", Utf8(cabecera + "\r\n"), "text/csv; charset=utf-16", HttpStatusCode.OK),
            new(
                "una fila vacía más que el tope",
                Utf8(cabecera + string.Concat(Enumerable.Repeat("\r\n", ImportacionDeTerceros.TopeDeFilas + 2))),
                TextoCsv,
                HttpStatusCode.RequestEntityTooLarge,
                "/errors/importacion-demasiadas-filas"),
            new(
                "un byte más que el tope",
                Utf8(Canario + new string(';', (int)ImportacionDeTerceros.TopeDeBytes + 1 - Encoding.UTF8.GetByteCount(Canario))),
                TextoCsv,
                HttpStatusCode.RequestEntityTooLarge,
                "/errors/cuerpo-demasiado-grande"),
        ];
    }

    // Deterministas —un SHA-256 por contador— para que un rojo se repita igual: unos bytes al azar de
    // verdad serían un fichero distinto en cada run.
    private static byte[] BytesSinForma(int cuantos, bool sinNulos)
    {
        List<byte> bytes = new(cuantos);

        for (int contador = 0; bytes.Count < cuantos; contador++)
        {
            foreach (byte cada in SHA256.HashData(BitConverter.GetBytes(contador)))
            {
                if (!sinNulos || cada != 0)
                {
                    bytes.Add(cada);
                }
            }
        }

        return [.. bytes.Take(cuantos)];
    }

    // Sin tipos esperados, el código basta: el 200 trae un informe y el 415 no es de este módulo.
    private static void ElTipoEsElQueToca(FicheroHostil hostil, string cuerpo)
    {
        if (hostil.Tipos.Length == 0)
        {
            return;
        }

        using var documento = JsonDocument.Parse(cuerpo);
        string? tipo = documento.RootElement.GetProperty("type").GetString();

        hostil.Tipos.ShouldContain(tipo!, $"{hostil.Nombre}: {cuerpo}");
    }

    private async Task ElRegistroNoLlevaElCanarioYSeSabeQueMiraAsync(HttpClient cliente)
    {
        // Primero, que el captador ve lo que anota la importación de ESTE host: el primer fichero bueno otra
        // vez, que ahora son tres ocupados.
        using (HttpResponseMessage repetida = await Importaciones.ImportarAsync(
            cliente, Importaciones.DeExcel(Importaciones.ExcelUtf8, PrimerNifBueno)))
        {
            repetida.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(repetida));
        }

        _anotado.Anotados.ShouldContain(
            anotado => anotado.EventId == SucesoDeLaImportacionConOcupados,
            "el captador no ha visto la importación de su propio host: su silencio no demuestra nada");

        List<string> conCanario = [.. _anotado.Anotados
            .Where(anotado => anotado.Texto.Contains(NucleoDelCanario, StringComparison.Ordinal))
            .Select(anotado => anotado.Texto)];

        conCanario.ShouldBeEmpty("el registro lleva lo que venía en un fichero");

        // Y después, que la búsqueda encuentra el canario cuando está: una petición que lo lleva en la ruta,
        // que el registro de peticiones anota tal cual.
        using ByteArrayContent vacio = new([]);

        using (HttpResponseMessage conRuta = await cliente.PostAsync($"{Importaciones.Ruta}-{NucleoDelCanario}", vacio))
        {
            conRuta.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
        }

        _anotado.Anotados.ShouldContain(
            anotado => anotado.Texto.Contains(NucleoDelCanario, StringComparison.Ordinal),
            "la búsqueda del canario en el registro no lo encuentra ni cuando está");
    }

    private sealed record FicheroHostil(
        string Nombre, byte[] Bytes, string? TipoDeContenido, HttpStatusCode Estado, params string[] Tipos)
    {
        public ByteArrayContent Contenido()
        {
            ByteArrayContent contenido = new(Bytes);

            if (TipoDeContenido is not null)
            {
                contenido.Headers.ContentType = MediaTypeHeaderValue.Parse(TipoDeContenido);
            }

            return contenido;
        }
    }

    // Todo lo que anota el host, ya convertido en texto: mensaje, cada propiedad y la excepción entera.
    private sealed class TodoLoQueSeAnota : ILogEventSink
    {
        private readonly ConcurrentQueue<Anotado> _anotados = new();

        public IReadOnlyList<Anotado> Anotados => [.. _anotados];

        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);

            using StringWriter texto = new(CultureInfo.InvariantCulture);
            texto.Write(logEvent.RenderMessage(CultureInfo.InvariantCulture));

            foreach (KeyValuePair<string, LogEventPropertyValue> propiedad in logEvent.Properties)
            {
                texto.Write(' ');
                texto.Write(propiedad.Key);
                texto.Write('=');
                propiedad.Value.Render(texto, null, CultureInfo.InvariantCulture);
            }

            if (logEvent.Exception is not null)
            {
                texto.Write(' ');
                texto.Write(logEvent.Exception.ToString());
            }

            _anotados.Enqueue(new Anotado(CapturaDeRegistro.EventIdDe(logEvent), texto.ToString()));
        }
    }

    private sealed record Anotado(int? EventId, string Texto);
}
