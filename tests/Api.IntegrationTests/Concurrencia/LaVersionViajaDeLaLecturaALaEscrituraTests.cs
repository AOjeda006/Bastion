using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Auditoria;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Concurrencia;

/// <summary>
/// La mitad R11 del criterio: <b><c>If-Match</c> obsoleto → 412</b>, y sin cabecera → <c>428</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los tres códigos no son grados de lo mismo.</b> El <c>428</c> dice «no me has dicho sobre qué
/// versión escribes» y es culpa del cliente por no mandar la cabecera; el <c>400</c>, «me lo has
/// dicho de una manera que no entiendo»; el <c>412</c>, «me lo has dicho bien y ya no es verdad», y
/// ese no es culpa de nadie: es que otro guardó primero. Un cliente que los mezclara reintentaría
/// el <c>400</c> igual que el <c>412</c> y se quedaría en un bucle.
/// </para>
/// <para>
/// <b>El ida y vuelta se ejercita entero.</b> No basta con que el <c>PUT</c> exija la cabecera: hace
/// falta que la etiqueta que emite el <c>GET</c> sea exactamente la que el <c>PUT</c> acepta. Son
/// dos caminos distintos —uno lee con <c>AsNoTracking</c>, el otro con seguimiento— y ahí es donde
/// la versión se pierde en silencio.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaVersionViajaDeLaLecturaALaEscrituraTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
    private const string Almacenes = "/api/v1/organizacion/almacenes";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task La_etiqueta_que_emite_la_lectura_es_la_que_acepta_la_escritura()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000055D");
        using HttpClient suyo = cliente;
        AlmacenDto almacen = await CrearAsync(cliente, "VERSION-IDA");

        string etiqueta = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");

        // Fuerte y entrecomillada: es lo que exige If-Match (RFC 9110, §13.1.1). Una etiqueta
        // débil aquí valdría para el GET condicional y no valdría para escribir, y la diferencia
        // no se vería hasta que un cliente intentara guardar.
        etiqueta.ShouldStartWith("\"");
        etiqueta.ShouldNotStartWith("W/");

        using HttpResponseMessage cambio = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Almacenes}/{almacen.Id}",
            etiqueta,
            JsonContent.Create(Modificado("Con la versión de la lectura")));

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
    }

    [Fact]
    public async Task La_version_cambia_cuando_el_recurso_cambia()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000056X");
        using HttpClient suyo = cliente;
        AlmacenDto almacen = await CrearAsync(cliente, "VERSION-MUEVE");

        string antes = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");

        using HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Almacenes}/{almacen.Id}", Modificado("Otro nombre"));
        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));

        string despues = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");

        // Sin esto, una versión constante pasaría todos los demás tests de este fichero: el
        // If-Match siempre coincidiría y nunca habría un 412 que ver.
        despues.ShouldNotBe(antes);
    }

    [Fact]
    public async Task Sin_la_cabecera_es_428_y_no_toca_nada()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000057B");
        using HttpClient suyo = cliente;
        AlmacenDto almacen = await CrearAsync(cliente, "VERSION-428");

        using HttpResponseMessage respuesta = await cliente.PutAsJsonAsync(
            $"{Almacenes}/{almacen.Id}", Modificado("Sin decir sobre qué versión"));

        ((int)respuesta.StatusCode).ShouldBe(428, await Escenario.Detalle(respuesta));
        (await ProblemaDe(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/falta-if-match");

        // Y el 428 es de verdad una negativa, no un aviso: la fila sigue como estaba.
        (await LeerAsync(cliente, almacen.Id)).Nombre.ShouldBe(almacen.Nombre);
    }

    // El comodín significa «me vale cualquier versión con tal de que exista», que es saltarse el
    // control entero sin dejar de cumplir el protocolo. Se rechaza, y con 400: la cabecera vino,
    // así que no es un 428, y no es una versión concreta, así que no es un 412.
    [Theory]
    [InlineData("*")]
    [InlineData("W/\"7\"")]
    [InlineData("\"7\", \"8\"")]
    [InlineData("7")]
    [InlineData("\"no-es-un-numero\"")]
    public async Task Una_cabecera_que_no_es_una_version_concreta_es_400(string cabecera)
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(NifDe(cabecera));
        using HttpClient suyo = cliente;
        AlmacenDto almacen = await CrearAsync(cliente, "VERSION-400");

        using HttpResponseMessage respuesta = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Almacenes}/{almacen.Id}",
            cabecera,
            JsonContent.Create(Modificado("Con una cabecera rara")));

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, await Escenario.Detalle(respuesta));
        (await ProblemaDe(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/if-match-no-valido");
    }

    [Fact]
    public async Task Una_version_obsoleta_es_412_y_trae_la_actual()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000058N");
        using HttpClient suyo = cliente;
        AlmacenDto almacen = await CrearAsync(cliente, "VERSION-412");

        // Lo que hace un cliente que abre el formulario y se va a comer.
        string vieja = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");

        using HttpResponseMessage otro = await cliente.ModificarAsync(
            $"{Almacenes}/{almacen.Id}", Modificado("Lo cambió el otro"));
        otro.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(otro));

        using HttpResponseMessage tarde = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Almacenes}/{almacen.Id}",
            vieja,
            JsonContent.Create(Modificado("Llego tarde")));

        tarde.StatusCode.ShouldBe(
            HttpStatusCode.PreconditionFailed, await Escenario.Detalle(tarde));

        // El estado actual del conflicto, servido como versión y EN EL CUERPO. Con esto el
        // cliente sabe contra qué está compitiendo sin tener que volver a leer a ciegas.
        string actual = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");

        (await ProblemaDe(tarde)).GetProperty("versionActual").GetString().ShouldBe(actual);

        // Y NO en la cabecera, que es donde se puso primero. El middleware de excepciones borra el
        // ETag de toda respuesta de error, así que la cabecera no llegaba; y hace bien, porque el
        // ETag de una respuesta etiqueta lo que esa respuesta lleva dentro, que aquí es un
        // documento de problema y no el almacén (ADR-0014). Esta línea está para que nadie vuelva
        // a ponerla «porque parece que falta» y se quede con una cabecera que no viaja.
        tarde.Headers.ETag.ShouldBeNull();

        // Y lo que había escrito el otro sigue ahí: el que llegó tarde no lo ha pisado. Es la
        // actualización perdida, que es de lo que va todo esto.
        (await LeerAsync(cliente, almacen.Id)).Nombre.ShouldBe("Lo cambió el otro");
    }

    // Dos personas que abren el mismo almacén a la vez y guardan las dos. Que el segundo escriba
    // sobre la versión que leyó ANTES del primero es lo que ocurre de verdad, y es lo único que
    // distingue este test de dos escrituras seguidas.
    [Fact]
    public async Task De_dos_que_leyeron_lo_mismo_solo_guarda_el_primero()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000059J");
        using HttpClient suyo = cliente;
        AlmacenDto almacen = await CrearAsync(cliente, "VERSION-DOS");

        string laDeAna = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");
        string laDeLuis = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");

        laDeLuis.ShouldBe(laDeAna, "los dos han leído el mismo recurso sin que nadie lo tocara");

        using HttpResponseMessage ana = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Almacenes}/{almacen.Id}",
            laDeAna,
            JsonContent.Create(Modificado("Lo de Ana")));

        using HttpResponseMessage luis = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Almacenes}/{almacen.Id}",
            laDeLuis,
            JsonContent.Create(Modificado("Lo de Luis")));

        ana.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(ana));
        luis.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed, await Escenario.Detalle(luis));

        (await LeerAsync(cliente, almacen.Id)).Nombre.ShouldBe("Lo de Ana");
    }

    /// <summary>Tras un <c>412</c>, ni traza ni evento.</summary>
    /// <remarks>
    /// <para>
    /// Es la comprobación que ata este ítem con los dos anteriores. El <c>412</c> sale de una
    /// excepción que <c>SaveChanges</c> lanza <b>después</b> de que el interceptor de auditoría
    /// haya compuesto sus filas y de que la bandeja haya recogido los eventos del agregado: los
    /// tres viajan en el mismo <c>SaveChanges</c>, así que o se deshacen los tres o queda una traza
    /// de un cambio que no ocurrió y un evento anunciando algo que nadie hizo.
    /// </para>
    /// <para>
    /// Y ese rastro fantasma no tendría ningún otro síntoma: la respuesta al cliente es correcta,
    /// la fila de negocio está bien, y lo que queda mal son dos tablas que en la fase 0 no lee
    /// nadie. Se vería meses después, cuando alguien audite.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Tras_un_412_ni_traza_ni_evento()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000060Z");
        using HttpClient suyo = cliente;
        AlmacenDto almacen = await CrearAsync(cliente, "VERSION-RASTRO");

        string vieja = await cliente.EtiquetaDeAsync($"{Almacenes}/{almacen.Id}");

        using HttpResponseMessage primero = await cliente.ModificarAsync(
            $"{Almacenes}/{almacen.Id}", Modificado("El que sí"));
        primero.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(primero));

        int trazasAntes = (await Trazas.DeAsync(postgres, "Almacen", almacen.Id)).Count;
        int eventosAntes = await EventosDeLaBaseAsync();

        using HttpResponseMessage tarde = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Almacenes}/{almacen.Id}",
            vieja,
            JsonContent.Create(Modificado("El que no")));

        tarde.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed, await Escenario.Detalle(tarde));

        IReadOnlyList<RegistroDeAuditoria> trazas = await Trazas.DeAsync(postgres, "Almacen", almacen.Id);

        trazas.Count.ShouldBe(
            trazasAntes,
            "el 412 ha dejado una traza de un cambio que no llegó a guardarse: " +
            string.Join(", ", trazas.Select(fila => $"{fila.Cambio} {fila.OcurridoEn:O}")));

        (await EventosDeLaBaseAsync()).ShouldBe(
            eventosAntes, "el 412 ha dejado un evento anunciando un cambio que no ocurrió");
    }

    private static ModificarAlmacenDto Modificado(string nombre) => new()
    {
        Nombre = nombre,
        Tipo = "Fisico",
        Direccion = Escenario.Domicilio(),
    };

    // Un NIF válido y distinto por fila de la teoría: los tests comparten base, así que dos filas
    // con el mismo NIF chocarían contra el índice único y el rojo hablaría de otra cosa.
    private static string NifDe(string cabecera) => cabecera switch
    {
        "*" => "00000061S",
        "W/\"7\"" => "00000062Q",
        "\"7\", \"8\"" => "00000063V",
        "7" => "00000064H",
        _ => "00000065L",
    };

    private static async Task<JsonElement> ProblemaDe(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
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

    private static async Task<AlmacenDto> LeerAsync(HttpClient cliente, Guid id)
    {
        AlmacenDto? almacen = await cliente.GetFromJsonAsync<AlmacenDto>($"{Almacenes}/{id}");

        almacen.ShouldNotBeNull();

        return almacen;
    }

    // La cola entera de la instalación, no la de esta empresa: lo que se comprueba es que el 412
    // no AÑADE nada, y otros tests de la misma base pueden estar dejando lo suyo.
    private async Task<int> EventosDeLaBaseAsync()
    {
        await using ContextoDeLaBandeja bandeja = postgres.AbrirBandejaEntera();

        return await bandeja.Bandeja.CountAsync();
    }
}
