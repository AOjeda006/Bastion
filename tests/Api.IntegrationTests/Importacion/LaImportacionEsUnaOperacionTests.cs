using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Errores;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Importacion;

/// <summary>
/// Una importación es UNA operación: se repite entera o no se repite, se cae entera o no se cae, y lo que
/// no cabe se rechaza antes de leerlo (ADR-0034 §1, §3 y §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>La repetición se comprueba por lo que NO pasa en la base.</b> Una importación repetida que volviera
/// a ejecutarse no crearía nada —todas sus filas serían ya <c>ya-existe</c>—, así que contar terceros no
/// la delata. Lo que la delata es que para decidirlo tiene que preguntar a la base qué identificadores están
/// ocupados, y esa pregunta deja su anotación (8401). La repetición de verdad no pregunta nada.
/// </para>
/// <para>
/// <b>La caída se provoca en la base, a mitad de la escritura</b>, con un disparador que revienta en la
/// segunda fila: es el punto en que una transacción por fila habría dejado la primera dentro y el recibo
/// fuera. Se quita en un <c>finally</c> y va fuera del esquema del módulo, para que ningún otro caso de la
/// colección se lo encuentre.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaImportacionEsUnaOperacionTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const int SucesoDeLaImportacionConOcupados = 8401;
    private const string RazonSocialQueRevienta = "Revienta a mitad de la importación";

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    [Fact]
    public async Task La_misma_clave_con_el_mismo_fichero_devuelve_el_mismo_informe_sin_volver_a_mirar_la_base()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(240);
        string clave = Guid.NewGuid().ToString();
        string[] nifs = [Importaciones.Nif(201), Importaciones.Nif(202), Importaciones.Nif(203)];
        byte[] fichero = Importaciones.Fichero([.. nifs.Select(nif => Importaciones.Fila(nif))]);

        using HttpResponseMessage primera = await Importaciones.ImportarAsync(cliente, fichero, clave);
        primera.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(primera));
        string informeDeLaPrimera = await primera.Content.ReadAsStringAsync();

        RegistroDeSucesos.Olvidar();

        using HttpResponseMessage segunda = await Importaciones.ImportarAsync(cliente, fichero, clave);

        // El efecto, antes que la respuesta: si la repetición se hubiera ejecutado, habría preguntado
        // qué identificadores están ocupados —los tres— y lo habría anotado.
        RegistroDeSucesos.Con(SucesoDeLaImportacionConOcupados).ShouldBeEmpty(
            "la repetición ha vuelto a mirar qué identificadores están ocupados: se ha ejecutado otra vez");
        (await Importaciones.CuantosAsync(postgres, empresa.Id, nifs)).ShouldBe(3);

        segunda.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(segunda));
        (await segunda.Content.ReadAsStringAsync()).ShouldBe(
            informeDeLaPrimera, "la repetición tiene que devolver el informe de la primera, que dice qué entró");
        segunda.Headers.GetValues(RespuestaRepetida.CabeceraDeRepeticion).ShouldContain("true");
    }

    [Fact]
    public async Task La_misma_clave_con_otro_fichero_es_un_409_y_el_otro_fichero_no_entra()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(241);
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage primera = await Importaciones.ImportarAsync(
            cliente, Importaciones.Fichero(Importaciones.Fila(Importaciones.Nif(211))), clave);
        primera.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(primera));

        using HttpResponseMessage otra = await Importaciones.ImportarAsync(
            cliente, Importaciones.Fichero(Importaciones.Fila(Importaciones.Nif(212))), clave);

        otra.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(otra));
        (await TipoAsync(otra)).ShouldBe("/errors/idempotencia-cuerpo-distinto");
        (await Importaciones.CuantosAsync(postgres, empresa.Id, Importaciones.Nif(212)))
            .ShouldBe(0, "el segundo fichero ha entrado con la clave del primero");
    }

    /// <summary>Si la escritura revienta a mitad, no queda nada: ni filas ni recibo.</summary>
    /// <remarks>
    /// Y el reintento con la misma clave es la primera vez, que es lo que la tercera mitad del caso
    /// comprueba: si hubiera quedado un recibo, contestaría lo que se guardó; si hubieran quedado filas,
    /// saldrían como <c>ya-existe</c>.
    /// </remarks>
    [Fact]
    public async Task Si_la_escritura_revienta_a_mitad_no_queda_ni_una_fila_ni_el_recibo_y_el_reintento_es_la_primera_vez()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(242);
        string clave = Guid.NewGuid().ToString();
        string[] nifs = [Importaciones.Nif(221), Importaciones.Nif(222), Importaciones.Nif(223)];
        byte[] fichero = Importaciones.Fichero(
            Importaciones.Fila(nifs[0]),
            Importaciones.Fila(nifs[1], razonSocial: RazonSocialQueRevienta),
            Importaciones.Fila(nifs[2]));

        await PonerElDisparadorAsync();

        try
        {
            using HttpResponseMessage caida = await Importaciones.ImportarAsync(cliente, fichero, clave);

            caida.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, await Escenario.Detalle(caida));
            string cuerpo = await caida.Content.ReadAsStringAsync();
            RastrosProhibidos.Todos.Where(rastro => cuerpo.Contains(rastro, StringComparison.OrdinalIgnoreCase))
                .ShouldBeEmpty("la caída cuenta por dónde ha roto: " + cuerpo);

            (await Importaciones.CuantosAsync(postgres, empresa.Id, nifs)).ShouldBe(
                0, "la escritura se ha caído a mitad y ha dejado dentro lo que llevaba");
            (await RecibosAsync(empresa.Id, clave)).ShouldBe(0, "ha quedado recibo de una importación que no entró");
        }
        finally
        {
            await QuitarElDisparadorAsync();
        }

        using HttpResponseMessage reintento = await Importaciones.ImportarAsync(cliente, fichero, clave);

        reintento.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(reintento));
        reintento.Headers.Contains(RespuestaRepetida.CabeceraDeRepeticion).ShouldBeFalse();
        InformeDeImportacionDto informe = await Importaciones.InformeAsync(reintento);
        informe.ShouldBe(informe with { Leidas = 3, Importadas = 3, Rechazadas = 0 });
        (await Importaciones.CuantosAsync(postgres, empresa.Id, nifs)).ShouldBe(3);
    }

    /// <summary>Un fichero que declara más que el tope se rechaza sin leerlo, y no deja ni filas ni recibo.</summary>
    [Fact]
    public async Task Un_fichero_que_declara_mas_que_el_tope_es_413_y_no_deja_nada()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(243);
        string clave = Guid.NewGuid().ToString();

        // Una fila buena y otra con un nombre comercial que llena el tope: si el tope no se impusiera, sería
        // un 200 con la segunda rechazada por demasiado larga, y la primera estaría dentro.
        byte[] fichero = Importaciones.Fichero(
            Importaciones.Fila(Importaciones.Nif(231)),
            Importaciones.Fila(Importaciones.Nif(232), nombreComercial: new string('x', (int)ImportacionDeTerceros.TopeDeBytes)));

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(cliente, fichero, clave);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge, await Escenario.Detalle(respuesta));
        (await TipoAsync(respuesta)).ShouldBe("/errors/cuerpo-demasiado-grande");
        (await Importaciones.CuantosAsync(postgres, empresa.Id, Importaciones.Nif(231))).ShouldBe(0);
        (await RecibosAsync(empresa.Id, clave)).ShouldBe(0);
    }

    /// <summary>Sin <c>Content-Length</c>, el servidor deja de pedir bytes en cuanto sabe que no cabe.</summary>
    /// <remarks>
    /// <para>
    /// El cuerpo es una corriente de 64 MiB que cuenta lo que entrega. Lo que se afirma es lo que el cliente
    /// llegó a mandar, que es lo que el servidor le pidió: el tope más lo que cabe en los búferes de por
    /// medio. Un tope comprobado después de volcar el cuerpo contestaría el mismo <c>413</c> tras haberlo
    /// pedido entero.
    /// </para>
    /// <para>
    /// <b>Con el manejador desnudo del servidor de pruebas, y no con el cliente de la fábrica.</b> El de la
    /// fábrica lleva el manejador de redirecciones, que copia el cuerpo entero a memoria ANTES de mandarlo
    /// para poder repetirlo ante un 307: la primera versión de este caso contó 64 MiB entregados con el
    /// lector cortando en el tope, porque medía al cliente y no al servidor. Del cliente de la sesión solo
    /// se toma el token, que es lo único que la autentica.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Sin_Content_Length_el_servidor_deja_de_leer_en_cuanto_el_fichero_no_cabe()
    {
        (HttpClient sesion, _) = await EnUnaEmpresaNuevaAsync(244);
        const long Enorme = 64L * 1024 * 1024;
        const long Holgura = 1024 * 1024;

        using HttpClient desnudo = new(_api.Server.CreateHandler()) { BaseAddress = sesion.BaseAddress };
        desnudo.DefaultRequestHeaders.Authorization = sesion.DefaultRequestHeaders.Authorization;

        CorrienteQueCuenta corriente = new(Enorme);
        StreamContent contenido = new(corriente);
        contenido.Headers.ContentType = new("text/csv");

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(desnudo, contenido, clave: null);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge, await Escenario.Detalle(respuesta));
        corriente.Entregados.ShouldBeLessThan(
            ImportacionDeTerceros.TopeDeBytes + Holgura,
            "el servidor ha seguido pidiendo el cuerpo después de pasarse del tope: se ha volcado antes de mirarlo");
    }

    /// <summary>Un cuerpo que no es un CSV es <c>415</c> sin que el servidor pida un solo byte.</summary>
    /// <remarks>
    /// El tipo se mira antes que el tope: lo que se va a rechazar por lo que declara no se lee. Con el orden al
    /// revés, esta misma corriente se leería hasta pasarse del tope y la respuesta sería un <c>413</c>, y la
    /// cuenta de lo entregado lo diría también. La misma corriente y el mismo manejador desnudo que el caso de
    /// arriba, por lo mismo.
    /// </remarks>
    [Fact]
    public async Task Un_cuerpo_que_no_es_un_CSV_es_415_antes_de_leer_nada()
    {
        (HttpClient sesion, _) = await EnUnaEmpresaNuevaAsync(248);
        const long Enorme = 64L * 1024 * 1024;
        const long Holgura = 1024 * 1024;

        using HttpClient desnudo = new(_api.Server.CreateHandler()) { BaseAddress = sesion.BaseAddress };
        desnudo.DefaultRequestHeaders.Authorization = sesion.DefaultRequestHeaders.Authorization;

        CorrienteQueCuenta corriente = new(Enorme);
        StreamContent contenido = new(corriente);
        contenido.Headers.ContentType = new("application/json");

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(desnudo, contenido, clave: null);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType, await Escenario.Detalle(respuesta));
        corriente.Entregados.ShouldBeLessThan(
            Holgura, "el servidor ha pedido el cuerpo de una petición que iba a rechazar por su tipo");
    }

    [Fact]
    public async Task Un_fichero_con_una_fila_mas_que_el_tope_es_413_sin_importar_ninguna()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(245);

        // La buena, delante; y detrás, filas vacías, que también cuentan.
        string[] filas =
        [
            Importaciones.Fila(Importaciones.Nif(241)),
            .. Enumerable.Repeat(string.Empty, ImportacionDeTerceros.TopeDeFilas),
        ];

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(cliente, Importaciones.Fichero(filas));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge, await Escenario.Detalle(respuesta));
        (await TipoAsync(respuesta)).ShouldBe("/errors/importacion-demasiadas-filas");
        (await Importaciones.CuantosAsync(postgres, empresa.Id, Importaciones.Nif(241))).ShouldBe(0);
    }

    /// <summary>
    /// El peor informe posible —cada columna de cada fila rechazada— tiene un tamaño acotado, y es el que se
    /// guarda en el recibo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la otra mitad del tope del fichero (ADR-0034 §3): un fichero de 2 MiB no puede producir una
    /// respuesta sin techo. Con un motivo por celda y las líneas agrupadas, el peor caso son las cinco mil
    /// filas con las dieciocho columnas rechazadas: 90 000 números de línea de hasta cuatro cifras, con su
    /// coma, más los nombres de 18 grupos. Unos 440 KiB, así que el techo se pone en 512.
    /// </para>
    /// <para>
    /// Y cinco mil filas justas entran —se leen y se informan—: es el borde del caso de al lado desde dentro.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_peor_informe_posible_tiene_techo_y_es_el_que_se_guarda_en_el_recibo()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(246);
        string clave = Guid.NewGuid().ToString();

        string todaMal = string.Join(';', Enumerable.Repeat("a\"", ImportacionDeTerceros.Cabecera.Count));
        byte[] fichero = Importaciones.Fichero([.. Enumerable.Repeat(todaMal, ImportacionDeTerceros.TopeDeFilas)]);

        using HttpResponseMessage respuesta = await Importaciones.ImportarAsync(cliente, fichero, clave);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));
        string cuerpo = await respuesta.Content.ReadAsStringAsync();

        InformeDeImportacionDto informe = await Importaciones.InformeAsync(respuesta);
        informe.ShouldBe(informe with { Leidas = 5000, Importadas = 0, Rechazadas = 5000 });
        informe.Rechazos.Count.ShouldBe(ImportacionDeTerceros.Cabecera.Count);
        informe.Rechazos.ShouldAllBe(rechazo =>
            rechazo.Motivo == MotivoDeRechazo.ComillasMalColocadas && rechazo.Lineas.Count == 5000);

        Encoding.UTF8.GetByteCount(cuerpo).ShouldBeLessThanOrEqualTo(
            512 * 1024,
            $"el peor informe pesa {Encoding.UTF8.GetByteCount(cuerpo).ToString(CultureInfo.InvariantCulture)} bytes");

        await using AuditoriaDbContext auditoria = postgres.AbrirAuditoria(empresa.Id);
        RegistroDeIdempotencia recibo = await auditoria.Set<RegistroDeIdempotencia>()
            .AsNoTracking()
            .SingleAsync(registro => registro.Clave == clave);

        recibo.Respuesta.Cuerpo.ShouldBe(cuerpo);
    }

    private static async Task<string?> TipoAsync(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.GetProperty("type").GetString();

    private async Task<int> RecibosAsync(Guid empresaId, string clave)
    {
        await using AuditoriaDbContext auditoria = postgres.AbrirAuditoria(empresaId);

        return await auditoria.Set<RegistroDeIdempotencia>().CountAsync(recibo => recibo.Clave == clave);
    }

    private Task PonerElDisparadorAsync() => EjecutarAsync(
        $"""
        CREATE OR REPLACE FUNCTION public.revienta_a_mitad_de_la_importacion() RETURNS trigger
        LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW.razon_social = '{RazonSocialQueRevienta}' THEN
                RAISE EXCEPTION 'la escritura revienta a mitad';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER revienta_a_mitad_de_la_importacion
            BEFORE INSERT ON {TercerosDbContext.Esquema}.terceros
            FOR EACH ROW EXECUTE FUNCTION public.revienta_a_mitad_de_la_importacion();
        """);

    private Task QuitarElDisparadorAsync() => EjecutarAsync(
        $"""
        DROP TRIGGER IF EXISTS revienta_a_mitad_de_la_importacion ON {TercerosDbContext.Esquema}.terceros;
        DROP FUNCTION IF EXISTS public.revienta_a_mitad_de_la_importacion();
        """);

    private async Task EjecutarAsync(string sql)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(sql, conexion);
        await orden.ExecuteNonQueryAsync();
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int orden)
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(orden));
        _clientes.Add(cliente);

        return (cliente, empresa);
    }

    // Una corriente sin longitud que entrega tantos bytes como se le diga, y cuenta cuántos ha entregado.
    private sealed class CorrienteQueCuenta(long total) : Stream
    {
        private long _entregados;

        public long Entregados => Interlocked.Read(ref _entregados);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Entregar(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Entregar(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Entregar(buffer.AsSpan(offset, count)));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Entregar(Span<byte> destino)
        {
            int cuantos = (int)Math.Min(destino.Length, total - Interlocked.Read(ref _entregados));
            destino[..cuantos].Fill((byte)';');
            Interlocked.Add(ref _entregados, cuantos);
            return cuantos;
        }
    }
}
