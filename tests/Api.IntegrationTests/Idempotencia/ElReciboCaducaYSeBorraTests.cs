using System.Net;
using System.Text;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.Auditoria.Infrastructure.Recibos;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Idempotencia;

/// <summary>
/// El recibo de idempotencia tiene plazo, el plazo va en la fila, y la purga lo cumple (ADR-0034 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Se comprueba con un instante elegido, no esperando un día.</b> La purga recibe el «ahora» por
/// parámetro precisamente para esto: se la llama justo antes del vencimiento de un recibo y justo en
/// él, y lo que se mira es qué filas quedan. Un plazo quitado —un recibo que no caduca nunca— sale
/// rojo en el segundo borde; uno mal calculado, en el primero.
/// </para>
/// <para>
/// <b>La purga de estos casos borra también los recibos de otros casos</b> que ya han terminado: todo
/// lo que caduque antes del instante elegido, de todas las empresas. No rompe a nadie porque la
/// colección corre en serie y ningún caso depende del recibo de otro; y es, además, lo que se quiere
/// comprobar: que la purga no mira empresas.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElReciboCaducaYSeBorraTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Almacenes = "/api/v1/organizacion/almacenes";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task El_recibo_nace_con_su_caducidad_a_las_24_horas_de_reclamarse()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(230));
        using HttpClient suyo = cliente;
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage alta = await AltaAsync(cliente, "CADUCA-PLAZO", clave);
        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        RegistroDeIdempotencia recibo = await ReciboAsync(empresa.Id, clave);

        (recibo.CaducaEn - recibo.CreadaEn).ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task La_purga_deja_lo_que_no_ha_vencido_y_se_lleva_lo_vencido_de_todas_las_empresas()
    {
        (HttpClient enA, EmpresaDto a) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(231));
        using HttpClient deA = enA;
        (HttpClient enB, EmpresaDto b) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(232));
        using HttpClient deB = enB;
        string claveDeA = Guid.NewGuid().ToString();
        string claveDeB = Guid.NewGuid().ToString();

        using HttpResponseMessage altaDeA = await AltaAsync(enA, "CADUCA-A", claveDeA);
        altaDeA.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(altaDeA));
        using HttpResponseMessage altaDeB = await AltaAsync(enB, "CADUCA-B", claveDeB);
        altaDeB.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(altaDeB));

        DateTimeOffset venceA = (await ReciboAsync(a.Id, claveDeA)).CaducaEn;
        DateTimeOffset venceB = (await ReciboAsync(b.Id, claveDeB)).CaducaEn;
        DateTimeOffset primero = venceA < venceB ? venceA : venceB;
        DateTimeOffset ultimo = venceA < venceB ? venceB : venceA;

        await PurgarAsync(primero.AddTicks(-1));

        (await CuantosRecibosAsync(a.Id, claveDeA)).ShouldBe(1, "la purga se ha llevado un recibo que aún no había vencido");
        (await CuantosRecibosAsync(b.Id, claveDeB)).ShouldBe(1, "la purga se ha llevado un recibo que aún no había vencido");

        await PurgarAsync(ultimo);

        (await CuantosRecibosAsync(a.Id, claveDeA)).ShouldBe(0, "el recibo vencido de A sigue en la tabla: el plazo no se cumple");
        (await CuantosRecibosAsync(b.Id, claveDeB)).ShouldBe(0, "el recibo vencido de B sigue en la tabla: la purga no ve todas las empresas");
    }

    /// <summary>Una clave caducada es una clave nueva, y se ve en el efecto.</summary>
    /// <remarks>
    /// Es lo que el contrato avisa: pasado el plazo, el reintento no se repite, se vuelve a hacer. En
    /// un alta con clave natural eso es un duplicado con nombre, y aquí se comprueba que lo es y que
    /// no hay un segundo almacén.
    /// </remarks>
    [Fact]
    public async Task Purgado_el_recibo_el_mismo_reintento_vuelve_a_hacer_el_trabajo()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(233));
        using HttpClient suyo = cliente;
        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage primera = await AltaAsync(cliente, "CADUCA-REINTENTO", clave);
        primera.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(primera));

        await PurgarAsync((await ReciboAsync(empresa.Id, clave)).CaducaEn);

        using HttpResponseMessage segunda = await AltaAsync(cliente, "CADUCA-REINTENTO", clave);

        segunda.Headers.Contains(RespuestaRepetida.CabeceraDeRepeticion).ShouldBeFalse(
            "se ha repetido una respuesta cuyo recibo ya se había purgado");
        segunda.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(segunda));
        JsonDocument.Parse(await segunda.Content.ReadAsStringAsync())
            .RootElement.GetProperty("type").GetString().ShouldBe("/errors/almacen-duplicado");
    }

    // La purga no sirve de nada si nadie la llama. Se pregunta al contenedor del host de verdad —con
    // base de datos— si el trabajo de fondo está, que es la mitad de la promesa que un test que llama
    // a la purga a mano no puede comprobar.
    [Fact]
    public void Con_base_de_datos_el_host_arranca_el_trabajo_que_purga_cada_hora()
    {
        IEnumerable<IHostedService> trabajos = _api.Services.GetServices<IHostedService>();

        trabajos.Select(trabajo => trabajo.GetType().Name).ShouldContain("PurgadorDeRecibos");
    }

    private async Task PurgarAsync(DateTimeOffset ahora)
    {
        using IServiceScope ambito = _api.Services.CreateScope();
        PurgaDeRecibosCaducados purga = ambito.ServiceProvider.GetRequiredService<PurgaDeRecibosCaducados>();

        await purga.PurgarAsync(ahora, CancellationToken.None);
    }

    private async Task<RegistroDeIdempotencia> ReciboAsync(Guid empresaId, string clave)
    {
        await using AuditoriaDbContext auditoria = postgres.AbrirAuditoria(empresaId);

        return await auditoria.Set<RegistroDeIdempotencia>().AsNoTracking().SingleAsync(recibo => recibo.Clave == clave);
    }

    private async Task<int> CuantosRecibosAsync(Guid empresaId, string clave)
    {
        await using AuditoriaDbContext auditoria = postgres.AbrirAuditoria(empresaId);

        return await auditoria.Set<RegistroDeIdempotencia>().CountAsync(recibo => recibo.Clave == clave);
    }

    private static Task<HttpResponseMessage> AltaAsync(HttpClient cliente, string codigo, string clave)
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

        peticion.Headers.TryAddWithoutValidation(FiltroDeIdempotencia.Cabecera, clave);

        return cliente.SendAsync(peticion);
    }
}
