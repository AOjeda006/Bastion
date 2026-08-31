using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Idempotencia;

/// <summary>
/// La mitad R10 del criterio: <b>la misma <c>Idempotency-Key</c> devuelve el mismo recurso</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué protege esto, dicho con el caso de verdad.</b> Un comercial da de alta un almacén desde
/// el móvil, el ascensor se come la cobertura al enviar y la aplicación reintenta sola. Sin clave,
/// hay dos almacenes; con clave, la segunda petición devuelve el primero. Nadie se entera de nada,
/// que es como tiene que ser.
/// </para>
/// <para>
/// <b>Y la invariante que sostiene todo lo demás: la fila existe si y solo si el trabajo ocurrió.</b>
/// Por eso hay aquí un test de que un alta rechazada NO consume la clave, y otro de que la fila del
/// recibo y la de negocio comparten <c>xmin</c>. Sin el primero, un cliente que se equivocara una
/// vez se quedaría con la clave quemada y sin manera de reintentar; sin el segundo, el recibo
/// podría confirmarse por su cuenta y anunciar un trabajo que se deshizo.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaMismaClaveDevuelveElMismoRecursoTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
    private const string Almacenes = "/api/v1/organizacion/almacenes";
    private const string Cabecera = "Idempotency-Key";

    // Un carácter más del máximo, escrito así para que se vea de dónde sale el número.
    private const string LaDemasiadoLarga =
        "0123456789012345678901234567890123456789012345678901234567890123" +
        "01234567890123456789012345678901234567890123456789012345678901234";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task El_reintento_con_la_misma_clave_devuelve_los_mismos_bytes_y_no_crea_otro()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000066C");
        using HttpClient suyo = cliente;
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage primera = await AltaAsync(cliente, "IDEM-UNO", clave);
        using HttpResponseMessage segunda = await AltaAsync(cliente, "IDEM-UNO", clave);

        primera.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(primera));
        segunda.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(segunda));

        // Los MISMOS bytes, no una respuesta equivalente: un cliente que compare las dos, o que
        // verifique una firma sobre el cuerpo, tiene que ver exactamente lo mismo.
        string bytesDeLaPrimera = await primera.Content.ReadAsStringAsync();
        string bytesDeLaSegunda = await segunda.Content.ReadAsStringAsync();

        bytesDeLaSegunda.ShouldBe(bytesDeLaPrimera);
        segunda.Headers.Location.ShouldBe(primera.Headers.Location);

        // La cabecera es informativa —la respuesta es la misma con ella y sin ella— pero distingue
        // «te la he repetido» de «lo he vuelto a hacer», que es la diferencia que este test mide.
        primera.Headers.Contains(RespuestaRepetida.CabeceraDeRepeticion).ShouldBeFalse();
        segunda.Headers.GetValues(RespuestaRepetida.CabeceraDeRepeticion).ShouldContain("true");

        // Y por el efecto, que es lo único que un cliente notaría: hay UN almacén, no dos.
        (await AlmacenesConCodigoAsync(empresa.Id, "IDEM-UNO")).ShouldBe(1);
    }

    [Fact]
    public async Task La_misma_clave_con_otro_cuerpo_es_409()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000067K");
        using HttpClient suyo = cliente;
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage primera = await AltaAsync(cliente, "IDEM-CUERPO-A", clave);
        primera.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(primera));

        using HttpResponseMessage otra = await AltaAsync(cliente, "IDEM-CUERPO-B", clave);

        // No se repite la respuesta del primero —sería devolverle el almacén equivocado— ni se
        // hace el trabajo del segundo: la clave dice «esta petición», y esta no es aquella.
        otra.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(otra));
        (await ProblemaDe(otra)).GetProperty("type").GetString()
            .ShouldBe("/errors/idempotencia-cuerpo-distinto");

        (await AlmacenesConCodigoAsync(empresa.Id, "IDEM-CUERPO-B")).ShouldBe(0);
    }

    // Una ruta sin `[AdmiteIdempotencia]` que recibiera la cabecera y la ignorase sería lo peor de
    // los dos mundos: el cliente cree que está protegido, reintenta con confianza y duplica.
    [Fact]
    public async Task La_cabecera_en_una_ruta_que_no_la_admite_es_400()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000068E");
        using HttpClient suyo = cliente;

        AlmacenDto almacen = await CrearAsync(cliente, "IDEM-NO-ADMITE");
        string etiqueta = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");

        HttpRequestMessage peticion = new(HttpMethod.Put, $"{Almacenes}/{almacen.Id}")
        {
            Content = JsonContent.Create(new ModificarAlmacenDto
            {
                Nombre = "Con una clave que aquí no vale",
                Tipo = "Fisico",
                Direccion = Escenario.Domicilio(),
            }),
        };

        peticion.Headers.TryAddWithoutValidation("If-Match", etiqueta);
        peticion.Headers.TryAddWithoutValidation(Cabecera, Guid.NewGuid().ToString());

        using HttpResponseMessage respuesta = await cliente.SendAsync(peticion);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));
        (await ProblemaDe(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/idempotencia-no-admitida");
    }

    /// <summary>Una clave que no identifica nada es un <c>400</c>, no una petición sin proteger.</summary>
    /// <remarks>
    /// <para>
    /// Una clave en blanco aceptada sería peor que ninguna: la tupla quedaría siendo (empresa,
    /// usuario, método, ruta), o sea una clave global, y el siguiente alta distinta del mismo
    /// usuario recibiría la respuesta de esta. Y una clave enorme es lo que un tercero usaría para
    /// hacer crecer una tabla nuestra mandando cabeceras de un megabyte.
    /// </para>
    /// <para>
    /// <b>No hay fila para la cadena vacía</b>, y no por descuido: una cabecera de valor vacío no
    /// llega a salir del cliente HTTP, así que un test así estaría afirmando algo del transporte y
    /// no de esta API. Los espacios en blanco sí viajan, y son el caso que de verdad puede llegar
    /// —una plantilla que interpola una variable sin valor—.
    /// </para>
    /// </remarks>
    /// <param name="clave">Lo que manda el cliente en la cabecera.</param>
    /// <param name="nif">NIF de la empresa del caso; uno por fila, que la base se comparte.</param>
    [Theory]
    [InlineData("   ", "00000069T")]
    [InlineData(LaDemasiadoLarga, "00000070R")]
    public async Task Una_clave_que_no_identifica_nada_es_400(string clave, string nif)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(nif);
        using HttpClient suyo = cliente;

        using HttpResponseMessage respuesta = await AltaAsync(cliente, "IDEM-VACIA", clave);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));
        (await ProblemaDe(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/idempotencia-clave-no-valida");
    }

    /// <summary>La misma clave en otra empresa es otra clave.</summary>
    /// <remarks>
    /// La clave la elige el cliente, y dos clientes eligen la misma antes o después: <c>1</c>,
    /// <c>prueba</c>, el UUID de una plantilla copiada. Si la identidad fuera solo la clave, el
    /// segundo recibiría el recurso del primero —de otra empresa— y lo leería como suyo. Aquí no
    /// hay error que ver: hay un dato de otro presentado como propio, que es lo peor que puede
    /// pasar en un sistema multiempresa.
    /// </remarks>
    [Fact]
    public async Task La_misma_clave_desde_otra_empresa_hace_su_propio_trabajo()
    {
        string clave = "la-misma-de-siempre";

        (HttpClient enA, EmpresaDto a) = await _api.EnUnaEmpresaNuevaAsync("00000071W");
        using HttpClient deA = enA;
        using HttpResponseMessage altaDeA = await AltaAsync(enA, "IDEM-COMPARTIDA", clave);
        altaDeA.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(altaDeA));

        (HttpClient enB, EmpresaDto b) = await _api.EnUnaEmpresaNuevaAsync("00000072A");
        using HttpClient deB = enB;
        using HttpResponseMessage altaDeB = await AltaAsync(enB, "IDEM-COMPARTIDA", clave);

        altaDeB.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(altaDeB));
        altaDeB.Headers.Contains(RespuestaRepetida.CabeceraDeRepeticion).ShouldBeFalse(
            "B ha recibido repetida la respuesta de A: la clave no lleva la empresa dentro");

        // Cada una tiene el suyo, y son distintos.
        (await AlmacenesConCodigoAsync(a.Id, "IDEM-COMPARTIDA")).ShouldBe(1);
        (await AlmacenesConCodigoAsync(b.Id, "IDEM-COMPARTIDA")).ShouldBe(1);

        AlmacenDto deLaA = (await altaDeA.Content.ReadFromJsonAsync<AlmacenDto>())!;
        AlmacenDto deLaB = (await altaDeB.Content.ReadFromJsonAsync<AlmacenDto>())!;

        deLaB.Id.ShouldNotBe(deLaA.Id);
    }

    /// <summary>Dos peticiones simultáneas con la misma clave: solo una hace el trabajo.</summary>
    /// <remarks>
    /// <para>
    /// Este es el test que justifica el <c>INSERT … ON CONFLICT</c>. Con «mirar y luego insertar»,
    /// las dos peticiones ven «no está» dentro de la ventana entre las dos consultas y las dos dan
    /// de alta: el mecanismo entero fallaría precisamente en el caso que viene a resolver, y solo
    /// bajo carga, que es cuando nadie está mirando.
    /// </para>
    /// <para>
    /// <b>Se comprueba por el efecto</b>, contando filas, y no leyendo el código ni contando
    /// consultas: cuántos almacenes hay es lo único que le importa a quien los da de alta.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task De_dos_peticiones_simultaneas_con_la_misma_clave_solo_una_hace_el_trabajo()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000073G");
        using HttpClient suyo = cliente;
        string clave = Guid.NewGuid().ToString();

        Task<HttpResponseMessage> una = AltaAsync(cliente, "IDEM-A-LA-VEZ", clave);
        Task<HttpResponseMessage> otra = AltaAsync(cliente, "IDEM-A-LA-VEZ", clave);

        HttpResponseMessage[] respuestas = await Task.WhenAll(una, otra);

        try
        {
            foreach (HttpResponseMessage respuesta in respuestas)
            {
                respuesta.StatusCode.ShouldBe(
                    HttpStatusCode.Created, await Escenario.Detalle(respuesta));
            }

            string[] cuerpos =
            [
                .. await Task.WhenAll(respuestas.Select(r => r.Content.ReadAsStringAsync())),
            ];

            cuerpos[1].ShouldBe(cuerpos[0], "las dos tenían que ver el mismo recurso");

            // Exactamente una de las dos hizo el trabajo; la otra recibió lo que hizo la primera.
            respuestas.Count(r => r.Headers.Contains(RespuestaRepetida.CabeceraDeRepeticion))
                .ShouldBe(1, "o han trabajado las dos, o no ha trabajado ninguna");
        }
        finally
        {
            foreach (HttpResponseMessage respuesta in respuestas)
            {
                respuesta.Dispose();
            }
        }

        (await AlmacenesConCodigoAsync(empresa.Id, "IDEM-A-LA-VEZ")).ShouldBe(1);
    }

    /// <summary>Un alta rechazada no quema la clave.</summary>
    /// <remarks>
    /// La fila existe si y solo si el trabajo ocurrió. Guardar el recibo de un <c>409</c> dejaría
    /// al cliente atrapado: corrige el dato, reintenta con la misma clave —que es lo que manda
    /// hacer— y recibe para siempre el error de la primera vez, sin ninguna manera de salir de ahí
    /// salvo inventarse otra clave, que es justo lo que la cabecera existe para no tener que hacer.
    /// </remarks>
    [Fact]
    public async Task Un_alta_rechazada_deja_la_clave_libre_para_el_reintento()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000074M");
        using HttpClient suyo = cliente;
        string clave = Guid.NewGuid().ToString();

        // Un código en blanco no pasa la validación: el alta se rechaza antes de tocar nada.
        using HttpResponseMessage rechazada = await AltaAsync(cliente, string.Empty, clave);
        rechazada.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, await Escenario.Detalle(rechazada));

        // El mismo cuerpo no vale para reintentar —seguiría siendo inválido—, así que el cliente
        // corrige y reintenta. Con la clave quemada esto sería un 409 de cuerpo distinto.
        using HttpResponseMessage corregida = await AltaAsync(cliente, "IDEM-REINTENTO", clave);

        corregida.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(corregida));
        (await AlmacenesConCodigoAsync(empresa.Id, "IDEM-REINTENTO")).ShouldBe(1);
    }

    /// <summary>El recibo y el trabajo caen en la misma transacción, y se demuestra con el testigo.</summary>
    /// <remarks>
    /// <para>
    /// <b>Es la tercera vez que este proyecto tiene que probar lo mismo</b> —la traza en el 0.7, el
    /// evento en el 0.8, el recibo aquí— y se prueba igual: mirando el <c>xmin</c>. En PostgreSQL
    /// el <c>xmin</c> de una fila es el identificador de la transacción que la escribió, así que
    /// dos filas con el mismo <c>xmin</c> se escribieron en la misma, y no hay manera de fingirlo
    /// desde el código de aplicación.
    /// </para>
    /// <para>
    /// Un test que solo comprobara que las dos filas están se pasaría en verde con dos
    /// <c>SaveChanges</c> seguidos, que es exactamente el fallo: si el proceso se cae entre uno y
    /// otro, queda un almacén sin recibo —y el reintento lo duplica— o un recibo sin almacén —y el
    /// reintento devuelve un 201 que no creó nada—.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_recibo_y_el_almacen_llevan_el_mismo_xmin()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000075Y");
        using HttpClient suyo = cliente;
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage alta = await AltaAsync(cliente, "IDEM-XMIN", clave);
        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        AlmacenDto almacen = (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;

        uint delAlmacen;
        await using (OrganizacionDbContext organizacion = postgres.AbrirOrganizacion(empresa.Id))
        {
            delAlmacen = await organizacion.Almacenes
                .Where(fila => fila.Id == almacen.Id)
                .Select(fila => EF.Property<uint>(fila, TestigoDeConcurrencia.Nombre))
                .SingleAsync();
        }

        // El recibo se lee con SQL y no con `EF.Property`, y no es una inconsistencia con la línea
        // de arriba: el recibo NO declara testigo de concurrencia, porque nadie escribe sobre él
        // con If-Match —su única modificación ocurre dentro de la transacción que lo insertó—.
        // Declararle uno para que este test fuera más bonito sería añadir un mecanismo que nadie
        // usa. El `xmin` que se lee aquí es la columna de sistema que PostgreSQL tiene en TODAS
        // las tablas, la declare el modelo o no, que es justamente lo que hace de él una prueba
        // que el código de aplicación no puede fingir.
        // Se pide como TEXTO porque `xid` no es un entero de los que el lector sabe convertir:
        // pedirlo como número da «Reading as System.Int64 is not supported for fields having
        // DataTypeName xid». El texto vale igual, que lo único que se hace con dos testigos es
        // preguntar si son el mismo.
        string delRecibo;
        await using (AuditoriaDbContext auditoria = postgres.AbrirAuditoriaEntera())
        {
            delRecibo = await auditoria.Database
                .SqlQueryRaw<string>(
                    "SELECT xmin::text AS \"Value\" FROM auditoria.claves_de_idempotencia " +
                    "WHERE empresa_id = {0} AND clave = {1}",
                    empresa.Id,
                    clave)
                .SingleAsync();
        }

        delRecibo.ShouldBe(
            delAlmacen.ToString(CultureInfo.InvariantCulture),
            "el recibo de idempotencia y el almacén se han escrito en transacciones distintas");
    }

    private static async Task<JsonElement> ProblemaDe(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
    }

    // El cuerpo se compone a mano y no con `PostAsJsonAsync` porque la huella se calcula sobre los
    // BYTES: dos llamadas tienen que mandar los mismos, hasta el espacio en blanco.
    private static Task<HttpResponseMessage> AltaAsync(
        HttpClient cliente, string codigo, string clave)
    {
        string cuerpo = JsonSerializer.Serialize(new CrearAlmacenDto
        {
            Codigo = codigo,
            Nombre = $"Almacén {codigo}",
            Tipo = "Fisico",
            Direccion = Escenario.Domicilio(),
        });

        HttpRequestMessage peticion = new(HttpMethod.Post, Almacenes)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
        };

        peticion.Headers.TryAddWithoutValidation(Cabecera, clave);

        return cliente.SendAsync(peticion);
    }

    private static async Task<AlmacenDto> CrearAsync(HttpClient cliente, string codigo)
    {
        HttpResponseMessage alta = await cliente.PostAsJsonAsync(Almacenes, new CrearAlmacenDto
        {
            Codigo = codigo,
            Nombre = $"Almacén {codigo}",
            Tipo = "Fisico",
            Direccion = Escenario.Domicilio(),
        });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;
    }

    private async Task<int> AlmacenesConCodigoAsync(Guid empresaId, string codigo)
    {
        await using OrganizacionDbContext organizacion = postgres.AbrirOrganizacion(empresaId);

        return await organizacion.Almacenes.CountAsync(fila => fila.Codigo == codigo);
    }
}
