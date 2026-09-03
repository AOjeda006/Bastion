using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Multiempresa;

/// <summary>
/// R8 por el efecto: dos empresas de verdad, dos sesiones de verdad, y ni una fila de la una
/// asomando en las consultas de la otra.
/// </summary>
/// <remarks>
/// <para>
/// Un filtro de inquilinato roto <b>no da error</b>: da menos filas, o de más, y solo cuando hay
/// un segundo inquilino. Por eso todos los casos de este fichero siembran <b>dos</b> empresas: con
/// una sola, un filtro que no filtra nada y otro que filtra bien son indistinguibles.
/// </para>
/// <para>
/// Y todo por HTTP, contra la API real: el filtro vive en el <c>DbContext</c>, pero lo que hay que
/// demostrar es que <b>una petición corriente</b> no ve lo que no es suyo. Un test que abriera el
/// contexto a mano probaría el filtro y no el sistema.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElFiltroDeEmpresaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Almacenes = "/api/v1/organizacion/almacenes";
    private const string Empresas = "/api/v1/organizacion/empresas";
    private const string Usuarios = "/api/v1/identidad/usuarios";

    private readonly ApiDeVerdad _api = new(postgres);

    // Los clientes se cierran aqui y no con un `using` por variable: cada caso abre dos, y dos
    // `using` de adorno por caso convierten en ruido lo unico que importa de estos tests, que es
    // que hay DOS empresas.
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
    public async Task Un_listado_sin_filtro_explicito_no_devuelve_datos_de_otra_empresa()
    {
        (HttpClient enA, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000018H");
        AlmacenDto deA = await CrearAlmacenAsync(enA, "SOLO-DE-A");

        (HttpClient enB, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000019L");
        // Nadie ha escrito «where empresa = la mía» en esta llamada: es el listado tal cual lo
        // publica el controlador. Si el almacén de A aparece aquí, R8 no existe.
        PaginaDe<AlmacenDto> pagina = await ListarAlmacenesAsync(enB);

        pagina.Elementos.ShouldNotContain(almacen => almacen.Id == deA.Id);
    }

    [Fact]
    public async Task El_total_de_la_pagina_tampoco_cuenta_las_filas_de_otra_empresa()
    {
        (HttpClient enA, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000022E");
        await CrearAlmacenAsync(enA, "CUENTA-A1");
        await CrearAlmacenAsync(enA, "CUENTA-A2");

        (HttpClient enB, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000023T");
        await CrearAlmacenAsync(enB, "CUENTA-B1");

        // El total se calcula con OTRA consulta que la de los elementos. Un filtro puesto en una
        // y no en la otra sale verde en el caso de arriba y aquí no: la página trae un elemento y
        // dice que hay tres, que es lo que ve un usuario como «página 1 de 2» que está vacía.
        PaginaDe<AlmacenDto> pagina = await ListarAlmacenesAsync(enB);

        pagina.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Una_escritura_por_identificador_contra_una_fila_de_otra_empresa_es_404()
    {
        (HttpClient enA, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000020C");
        AlmacenDto deA = await CrearAlmacenAsync(enA, "ESCRITURA-A");

        (HttpClient enB, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000021K");
        // B lleva `organizacion.almacen.modificar`: la puerta se le abre. Lo que no puede es que
        // se le abra sobre una fila que no es suya. Un filtro que protege el listado y deja pasar
        // esto sale verde en todas las lecturas y es exactamente el agujero.
        // El If-Match va a mano y con un valor cualquiera, porque B no puede leer la fila para
        // sacar el suyo: el GET le da 404. Y ese es justo el orden que hay que comprobar. Si la
        // versión se mirara antes que la existencia, B recibiría un 412 —«esa no es la versión
        // actual»— y acabaría de enterarse de que la fila existe, que es la mitad de lo que R8
        // esconde. Con 404, B no distingue una fila ajena de una que no está.
        using HttpResponseMessage escritura = await enB.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Almacenes}/{deA.Id}",
            "\"1\"",
            JsonContent.Create(new ModificarAlmacenDto
            {
                Nombre = "Tomado por B",
                Tipo = "Fisico",
                Direccion = Escenario.Domicilio(),
            }));

        escritura.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(escritura));

        // Y no basta con el código: si la fila hubiera cambiado, el 404 sería una mentira cortés.
        AlmacenDto sigueSiendoDeA = await ObtenerAlmacenAsync(enA, deA.Id);

        sigueSiendoDeA.Nombre.ShouldBe(deA.Nombre);
    }

    [Fact]
    public async Task Un_borrado_por_identificador_contra_una_fila_de_otra_empresa_es_404()
    {
        (HttpClient enA, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000024R");
        AlmacenDto deA = await CrearAlmacenAsync(enA, "BORRADO-A");

        (HttpClient enB, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000025W");
        // El borrado es un bloqueo (art. 32 LOPDGDD). Un bloqueo ajeno no cambiaría la fila,
        // pero SÍ la dejaría inservible para su dueño: es una escritura destructiva con nombre
        // suave, y por eso este test la cuenta como escritura.
        using HttpResponseMessage borrado = await enB.EnviarConVersionAsync(
            HttpMethod.Delete, $"{Almacenes}/{deA.Id}", "\"1\"");

        borrado.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(borrado));

        // La prueba de que no se ejecutó es que su DUEÑO lo sigue viendo. Desde el 0.10 ya no
        // hay `Estado` que mirar: si el bloqueo hubiera entrado, el filtro de R16 taparía el
        // almacén también para él y esto sería un 404. Que responda es la comprobación.
        AlmacenDto sigueVisible = await ObtenerAlmacenAsync(enA, deA.Id);

        sigueVisible.Id.ShouldBe(deA.Id);
    }

    [Fact]
    public async Task Una_fila_de_otra_empresa_no_se_distingue_de_una_que_no_existe()
    {
        (HttpClient enA, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000026A");
        AlmacenDto deA = await CrearAlmacenAsync(enA, "AJENO-A");

        (HttpClient enB, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000027G");
        using HttpResponseMessage ajena = await enB.GetAsync($"{Almacenes}/{deA.Id}");
        using HttpResponseMessage inventada = await enB.GetAsync($"{Almacenes}/{Guid.CreateVersion7()}");

        // 404 y NO 403. Un 403 sobre una fila ajena contesta «eso existe y no es tuyo», y con eso
        // se enumera el negocio del vecino sin leer un solo dato suyo. El 403 de 0.5
        // (`/errors/empresa-ajena`) es otra cosa: allí la empresa la NOMBRA la petición y lo que se
        // niega es la operación, no la existencia de una fila.
        ajena.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(ajena));
        inventada.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Y la respuesta tiene que ser la MISMA, no solo el código: un `type` distinto, o un
        // mensaje con el identificador dentro en un caso y no en el otro, vuelve a separarlas.
        (await TipoDelProblema(ajena)).ShouldBe(await TipoDelProblema(inventada));
        (await TipoDelProblema(ajena)).ShouldBe("/errors/almacen-no-encontrado");
    }

    [Fact]
    public async Task El_identificador_de_empresa_que_venga_en_la_peticion_se_ignora()
    {
        (HttpClient enA, EmpresaDto deA) = await EnUnaEmpresaNuevaAsync("00000028M");
        (HttpClient enB, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000029Y");
        // Por los tres sitios a la vez: cuerpo, cadena de consulta y cabecera. `CrearAlmacenDto` no
        // tiene campo de empresa —lo comprueba `NingunaPeticionNombraLaEmpresaTests`—, así que el
        // del cuerpo se manda a pelo, como lo mandaría quien intentara colarlo.
        string cuerpo = JsonSerializer.Serialize(new
        {
            codigo = "COLADO-B",
            nombre = "Almacén colado",
            tipo = "Fisico",
            empresaId = deA.Id,
            direccion = Escenario.Domicilio(),
        });

        using var peticion = new HttpRequestMessage(
            HttpMethod.Post,
            $"{Almacenes}?empresaId={deA.Id}")
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
        };

        peticion.Headers.Add("X-Empresa-Id", deA.Id.ToString());

        using HttpResponseMessage alta = await enB.SendAsync(peticion);

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        AlmacenDto creado = (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;

        // «No cambia nada» quiere decir las dos cosas: el almacén ha nacido en B —que es quien
        // llamaba— y en A no ha aparecido. Comprobar solo lo primero dejaría pasar un alta que
        // saliera por duplicado.
        PaginaDe<AlmacenDto> enLaDeB = await ListarAlmacenesAsync(enB);
        PaginaDe<AlmacenDto> enLaDeA = await ListarAlmacenesAsync(enA);

        enLaDeB.Elementos.ShouldContain(almacen => almacen.Id == creado.Id);
        enLaDeA.Elementos.ShouldNotContain(almacen => almacen.Id == creado.Id);
    }

    [Fact]
    public async Task El_padron_de_empresas_no_se_lee_desde_otra_empresa()
    {
        (HttpClient enA, EmpresaDto deA) = await EnUnaEmpresaNuevaAsync("00000030F");
        (HttpClient enB, EmpresaDto deB) = await EnUnaEmpresaNuevaAsync("00000031P");
        using HttpResponseMessage ajena = await enB.GetAsync($"{Empresas}/{deA.Id}");

        // La empresa es la raíz del inquilinato y se filtra por su propia clave. Sin esto, el
        // listado de empresas es el padrón de clientes de quien explote la instalación: razón
        // social y NIF de todos, legible desde dentro de cualquiera de ellas.
        ajena.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(ajena));

        PaginaDe<EmpresaDto>? padron = await enB
            .GetFromJsonAsync<PaginaDe<EmpresaDto>>($"{Empresas}?page=1&size=200");

        padron.ShouldNotBeNull();
        padron.Elementos.ShouldNotContain(empresa => empresa.Id == deA.Id);
        padron.Elementos.ShouldContain(empresa => empresa.Id == deB.Id);
    }

    [Fact]
    public async Task Un_usuario_que_no_comparte_empresa_no_se_ve()
    {
        (HttpClient enA, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000032D");
        // Nace con pertenencia SOLO a A: es la empresa desde la que se le invita.
        UsuarioDto soloDeA = await CrearUsuarioAsync(enA);

        (HttpClient enB, EmpresaDto _) = await EnUnaEmpresaNuevaAsync("00000033X");
        using HttpResponseMessage ajeno = await enB.GetAsync($"{Usuarios}/{soloDeA.Id}");

        // El usuario NO lleva `empresa_id` —una cuenta puede estar en varias empresas—, así que
        // aquí el filtro va por la pertenencia. Sin él, quien tenga `identidad.usuario.ver` en una
        // empresa lee el correo y el nombre de los usuarios de todas las demás enumerando
        // identificadores, y ni siquiera hace falta adivinarlos: los sirve el propio listado.
        ajeno.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(ajeno));
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(string nif)
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(nif);

        _clientes.Add(cliente);

        return (cliente, empresa);
    }

    private static async Task<string?> TipoDelProblema(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return problema.RootElement.GetProperty("type").GetString();
    }

    private static async Task<UsuarioDto> CrearUsuarioAsync(HttpClient cliente)
    {
        // Correo irrepetible: los tests comparten base, y el correo es único en toda la
        // instalación. La contraseña se genera aquí y no se escribe en ninguna parte.
        string sufijo = Guid.CreateVersion7().ToString("N")[^12..];

        HttpResponseMessage alta = await cliente.PostAsJsonAsync(Usuarios, new CrearUsuarioDto
        {
            Correo = $"inquilinato-{sufijo}@bastion.pruebas",
            Nombre = "Cuenta de una sola empresa",
            Contrasena = Guid.CreateVersion7().ToString("N") + "aA1!",
        });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<UsuarioDto>())!;
    }

    private static async Task<AlmacenDto> ObtenerAlmacenAsync(HttpClient cliente, Guid id)
    {
        AlmacenDto? almacen = await cliente.GetFromJsonAsync<AlmacenDto>($"{Almacenes}/{id}");

        almacen.ShouldNotBeNull();

        return almacen;
    }

    private static async Task<AlmacenDto> CrearAlmacenAsync(HttpClient cliente, string codigo)
    {
        HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Almacenes,
            new CrearAlmacenDto { Codigo = codigo, Nombre = $"Almacén {codigo}", Tipo = "Fisico", Direccion = Escenario.Domicilio() });

        alta.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;
    }

    private static async Task<PaginaDe<AlmacenDto>> ListarAlmacenesAsync(HttpClient cliente)
    {
        PaginaDe<AlmacenDto>? pagina = await cliente
            .GetFromJsonAsync<PaginaDe<AlmacenDto>>($"{Almacenes}?page=1&size=200");

        pagina.ShouldNotBeNull();

        return pagina;
    }
}
