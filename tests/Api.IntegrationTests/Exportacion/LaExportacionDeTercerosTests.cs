using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Importacion;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Importacion;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Exportacion;

/// <summary>
/// La exportación de terceros contra la base: sale con la cabecera de su importación, y lo que sale
/// vuelve a entrar igual en otra empresa (*La puerta del 3.2*, respuesta 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>El viaje de ida y vuelta es la prueba</b>: se exporta de una empresa, se importa en otra y se
/// vuelve a exportar, y los dos ficheros son el mismo byte a byte. Lo que la exportación escribiera
/// en una forma que la importación no lee, o leyera de otra manera, no pasa: la columna cambiada de
/// sitio, el límite con punto decimal, el «sí» sin tilde, el territorio con otro nombre, el
/// apóstrofo que no se quita al volver (ADR-0075).
/// </para>
/// <para>
/// <b>Semillas: del 940 al 942</b>, del bloque del 3.2 (ver
/// <c>ElTextoSoloLoRecibeQuienLoMiraTests</c>). Los NIF de los terceros, del 34 000 001 al
/// 34 000 999, que no usa nadie más.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaExportacionDeTercerosTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Terceros = "/api/v1/terceros/terceros";

    private const int BaseDeNifs = 34_000_000;

    private static readonly string s_cabecera = Importaciones.Cabecera + "\r\n";

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Lo_que_sale_vuelve_a_entrar_en_otra_empresa_y_sale_igual_sin_el_bloqueado()
    {
        (HttpClient origen, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(940));
        using HttpClient suyo = origen;

        string alfa = Nif(1);
        string beta = Nif(2);

        TerceroDto conTodo = await CrearAsync(origen, new CrearTerceroDto
        {
            Identificacion = new IdentificacionDeAltaDto { Pais = "ES", Numero = alfa },
            RazonSocial = "Alfa; la de \"fuera\" SL",
            NombreComercial = "=Comercial",
            DomicilioFiscal = Escenario.Domicilio() with { Calle = "+Calle Mayor", Numero = "-1" },
            EsCliente = true,
            EsProveedor = true,
            RegimenFiscal = new RegimenFiscalDeAltaDto
            {
                Territorio = "Canarias",
                RecargoDeEquivalencia = true,
                SujetoARetencionIrpf = true,
            },
        });
        await CrearAsync(origen, Sencillo(beta, "Beta SL"));
        TerceroDto cerrado = await CrearAsync(origen, Sencillo(Nif(3), "Gamma SL"));

        using (HttpResponseMessage limite = await FijarLimiteAsync(origen, conTodo.Id, 1_500.5m, "EUR"))
        {
            limite.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(limite));
        }

        string lineaDeAlfa =
            $"ES;{alfa};\"Alfa; la de \"\"fuera\"\" SL\";'=Comercial;'+Calle Mayor;'-1;28013;Madrid;Madrid;ES;" +
            "sí;sí;Canarias;sí;no;sí;1500,5;EUR\r\n";
        string lineaDeBeta = LineaSencilla(beta, "Beta SL");

        (await ExportarAsync(origen)).ShouldBe(
            s_cabecera + lineaDeAlfa + lineaDeBeta + LineaSencilla(Nif(3), "Gamma SL"),
            "la pareja: antes del bloqueo, el tercero sale");

        using (HttpResponseMessage bloqueo = await origen.SuprimirAsync($"{Terceros}/{cerrado.Id}"))
        {
            bloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(bloqueo));
        }

        byte[] fichero = await Exportaciones.FicheroAsync(origen, Terceros, "terceros");

        Exportaciones.Texto(fichero).ShouldBe(
            s_cabecera + lineaDeAlfa + lineaDeBeta,
            "el tercero bloqueado sale del listado, y con él del fichero (R16)");

        // El fichero vuelve a entrar tal como se descargó, con su BOM, en una empresa que no tiene
        // ninguno de los dos.
        (HttpClient destino, EmpresaDto otra) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(941));
        using HttpClient delDestino = destino;

        using (HttpResponseMessage importacion = await Importaciones.ImportarAsync(destino, fichero))
        {
            importacion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(importacion));

            InformeDeImportacionDto informe = await Importaciones.InformeAsync(importacion);
            informe.ShouldBe(informe with { Leidas = 2, Importadas = 2, Rechazadas = 0 });
            informe.Rechazos.ShouldBeEmpty();
        }

        // El arnés: la igualdad de abajo no es la de leer dos veces la misma empresa.
        (await Importaciones.CuantosAsync(postgres, otra.Id, alfa, beta)).ShouldBe(2L);

        (await ExportarAsync(destino)).ShouldBe(Exportaciones.Texto(fichero), "lo que sale vuelve a entrar igual");
    }

    [Fact]
    public async Task La_exportacion_lleva_el_orden_y_el_texto_del_listado_y_no_busca_por_el_identificador()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(942));
        using HttpClient suyo = cliente;

        await CrearAsync(cliente, Sencillo(Nif(11), "Alfa SL"));
        await CrearAsync(cliente, Sencillo(Nif(12), "Beta SL"));
        await CrearAsync(cliente, Sencillo(Nif(13), "Gamma SL") with { NombreComercial = "La de Beta" });

        Nifs(await ExportarAsync(cliente)).ShouldBe([Nif(11), Nif(12), Nif(13)], "sin orden, el del listado");
        Nifs(await ExportarAsync(cliente, "?sort=-razonSocial")).ShouldBe([Nif(13), Nif(12), Nif(11)]);
        Nifs(await ExportarAsync(cliente, "?q=beta")).ShouldBe(
            [Nif(12), Nif(13)], "el texto del listado: la razón social o el nombre comercial");

        // El identificador no está en el filtro del texto (ADR-0025), y tampoco en el de la exportación.
        (await ExportarAsync(cliente, $"?q={Nif(12)}")).ShouldBe(s_cabecera);
    }

    private static Task<string> ExportarAsync(HttpClient cliente, string consulta = "") =>
        Exportaciones.ExportarAsync(cliente, Terceros, "terceros", consulta);

    private static string Nif(int orden) => Escenario.NifInventado(BaseDeNifs + orden);

    private static CrearTerceroDto Sencillo(string nif, string razonSocial) => new()
    {
        Identificacion = new IdentificacionDeAltaDto { Pais = "ES", Numero = nif },
        RazonSocial = razonSocial,
        DomicilioFiscal = Escenario.Domicilio(),
        EsCliente = true,
    };

    /// <summary>La fila que sale de <see cref="Sencillo"/>.</summary>
    private static string LineaSencilla(string nif, string razonSocial) =>
        $"ES;{nif};{razonSocial};;Gran Vía;31;28013;Madrid;Madrid;ES;sí;no;PeninsulaYBaleares;no;no;no;;\r\n";

    private static async Task<TerceroDto> CrearAsync(HttpClient cliente, CrearTerceroDto alta)
    {
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Terceros, alta);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<TerceroDto>())!;
    }

    private static async Task<HttpResponseMessage> FijarLimiteAsync(
        HttpClient cliente,
        Guid terceroId,
        decimal cantidad,
        string divisa)
    {
        string etiqueta = await cliente.EtiquetaDeAsync($"{Terceros}/{terceroId}");

        return await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Terceros}/{terceroId}/limite-credito",
            etiqueta,
            JsonContent.Create(new LimiteCreditoDeAltaDto { Cantidad = cantidad, Divisa = divisa }));
    }

    /// <summary>La segunda columna de cada fila, el identificador, sin la cabecera.</summary>
    private static string[] Nifs(string fichero) =>
        [.. fichero.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(linea => linea.Split(';')[1])];
}
