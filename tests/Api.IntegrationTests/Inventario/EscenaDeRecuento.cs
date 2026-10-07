using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Series;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La escena de la transferencia con una serie más, la de recuentos: lo que necesita un caso del
/// recuento (ítem 2.12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sobre la de la transferencia y no una nueva</b>: un recuento necesita almacenes con existencias,
/// y esa escena ya las mete con ajustes confirmados por la API, y también las pone en vuelo, que es
/// el tránsito que la ficha enseña (ADR-0055 §7). Lo único que le falta es la serie del recuento.
/// </para>
/// <para>
/// <b>El alta va por la API</b>, porque el recuento sí tiene borde desde su primer commit. La carrera
/// de dos altas usa además el caso de uso cableado a mano, que es lo único que deja parar una a
/// medias.
/// </para>
/// </remarks>
/// <param name="Escena">La escena de siempre: empresa, almacenes, artículo y serie de ajustes.</param>
/// <param name="SerieDeRecuentos">La que numera los recuentos.</param>
internal sealed record EscenaDeRecuento(EscenaDeTransferencia Escena, SerieDto SerieDeRecuentos)
{
    /// <summary>La ruta del recurso.</summary>
    internal const string Recuentos = "/api/v1/inventario/recuentos";

    private const string CabeceraDeIdempotencia = "Idempotency-Key";

    /// <summary>El motivo de los recuentos de los casos, que es el que llevará el ajuste.</summary>
    internal const string Motivo = "Recuento anual del almacén";

    /// <summary>El motivo de las anulaciones de los casos, que es también el del inverso.</summary>
    internal const string MotivoDeLaAnulacion = "Se contó el almacén equivocado";

    /// <summary>El motivo de los descartes de los casos.</summary>
    internal const string MotivoDelDescarte = "Se abrió con las series del año pasado";

    /// <summary>El cliente de la empresa de la escena.</summary>
    internal HttpClient Cliente => Escena.Cliente;

    /// <summary>Monta la escena en una empresa nueva.</summary>
    /// <param name="api">La API del caso.</param>
    /// <param name="semilla">Número de empresa y de los maestros de instalación del artículo.</param>
    /// <param name="codigo">Prefijo de los códigos de este caso.</param>
    /// <param name="trazabilidad">La marca del artículo.</param>
    /// <returns>La escena; su cliente lo cierra quien la pidió.</returns>
    internal static async Task<EscenaDeRecuento> MontarAsync(
        ApiDeVerdad api, int semilla, string codigo, string trazabilidad = "Ninguna")
    {
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(api, semilla, codigo, semilla, trazabilidad);

        SerieDto deRecuentos = await LosMaestrosPorLaApi.CrearSerieEnAsync(
            escena.Cliente, escena.Ejercicio.Id, codigo + "-RC", TipoDeDocumento.RecuentoDeInventario);

        return new EscenaDeRecuento(escena, deRecuentos);
    }

    /// <summary>La petición del alta, con las dos series de la escena.</summary>
    /// <param name="almacenId">El almacén que se cuenta.</param>
    /// <param name="motivo">Por qué; el de los casos si no se dice.</param>
    /// <returns>La petición.</returns>
    internal AbrirRecuentoDto Peticion(Guid almacenId, string? motivo = null) =>
        new(SerieDeRecuentos.Id, Escena.SerieDeAjustes.Id, almacenId, motivo ?? Motivo);

    /// <summary>El alta por la API, tal como salga.</summary>
    /// <param name="peticion">Lo que se manda.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> AbrirPorLaApiAsync(AbrirRecuentoDto peticion) =>
        Cliente.PostAsJsonAsync(Recuentos, peticion);

    /// <summary>Abre el recuento de un almacén por la API, que tiene que salir bien.</summary>
    /// <param name="almacenId">El almacén que se cuenta.</param>
    /// <returns>El recuento en curso, tal como lo devolvió el alta.</returns>
    internal async Task<RecuentoDto> AbrirAsync(Guid almacenId)
    {
        using HttpResponseMessage alta = await AbrirPorLaApiAsync(Peticion(almacenId));

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<RecuentoDto>())!;
    }

    /// <summary>La ficha por la API, que tiene que salir bien.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <returns>La ficha.</returns>
    internal async Task<RecuentoDto> FichaAsync(Guid recuentoId)
    {
        using HttpResponseMessage lectura = await Cliente.GetAsync($"{Recuentos}/{recuentoId}");

        return await EscenaDeTransferencia.LeerAsync<RecuentoDto>(lectura);
    }

    /// <summary>Una página de las líneas por la API, que tiene que salir bien.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="consulta">Lo que va detrás de la ruta, con su <c>?</c>, o nada.</param>
    /// <returns>La página.</returns>
    internal async Task<PaginaDe<LineaDeRecuentoDto>> LineasAsync(Guid recuentoId, string consulta = "")
    {
        using HttpResponseMessage lectura = await Cliente.GetAsync($"{Recuentos}/{recuentoId}/lineas{consulta}");

        return await EscenaDeTransferencia.LeerAsync<PaginaDe<LineaDeRecuentoDto>>(lectura);
    }

    /// <summary>Todas las líneas de un recuento, en una página del tamaño máximo.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <returns>Sus líneas, por número.</returns>
    internal async Task<IReadOnlyList<LineaDeRecuentoDto>> TodasLasLineasAsync(Guid recuentoId)
    {
        PaginaDe<LineaDeRecuentoDto> pagina = await LineasAsync(recuentoId, $"?size={Paginacion.TamanioMaximo}");

        pagina.Total.ShouldBeLessThanOrEqualTo(Paginacion.TamanioMaximo, "una página no las trae todas");

        return pagina.Elementos;
    }

    /// <summary>La ruta de una línea, que es también la de su versión.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="lineaId">La línea.</param>
    /// <returns>La ruta.</returns>
    internal static string RutaDeLaLinea(Guid recuentoId, Guid lineaId) =>
        $"{Recuentos}/{recuentoId}/lineas/{lineaId}";

    /// <summary>Una línea por la API, que tiene que salir bien.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="lineaId">La línea.</param>
    /// <returns>La línea.</returns>
    internal async Task<LineaDeRecuentoDto> LineaAsync(Guid recuentoId, Guid lineaId)
    {
        using HttpResponseMessage lectura = await Cliente.GetAsync(RutaDeLaLinea(recuentoId, lineaId));

        return await EscenaDeTransferencia.LeerAsync<LineaDeRecuentoDto>(lectura);
    }

    /// <summary>Cuenta una línea por la API con el cliente de la escena, tal como salga.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="lineaId">La línea.</param>
    /// <param name="etiqueta">El <c>If-Match</c>, o <c>null</c> para no mandarlo.</param>
    /// <param name="contado">Lo contado, o <c>null</c> para mandarlo vacío.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> ContarAsync(Guid recuentoId, Guid lineaId, string? etiqueta, decimal? contado) =>
        ContarAsync(Cliente, recuentoId, lineaId, etiqueta, contado);

    /// <summary>Cuenta una línea por la API con el cliente que se diga, tal como salga.</summary>
    /// <param name="cliente">El cliente, que puede ser el de otra empresa.</param>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="lineaId">La línea.</param>
    /// <param name="etiqueta">El <c>If-Match</c>, o <c>null</c> para no mandarlo.</param>
    /// <param name="contado">Lo contado, o <c>null</c> para mandarlo vacío.</param>
    /// <returns>La respuesta cruda.</returns>
    internal static Task<HttpResponseMessage> ContarAsync(
        HttpClient cliente, Guid recuentoId, Guid lineaId, string? etiqueta, decimal? contado) =>
        cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            RutaDeLaLinea(recuentoId, lineaId),
            etiqueta,
            JsonContent.Create(new ContarLineaDeRecuentoDto { Contado = contado }));

    /// <summary>Quita una línea por la API, tal como salga.</summary>
    /// <param name="cliente">El cliente, que puede ser el de otra empresa.</param>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="lineaId">La línea.</param>
    /// <param name="etiqueta">El <c>If-Match</c>, o <c>null</c> para no mandarlo.</param>
    /// <returns>La respuesta cruda.</returns>
    internal static Task<HttpResponseMessage> QuitarAsync(
        HttpClient cliente, Guid recuentoId, Guid lineaId, string? etiqueta) =>
        cliente.EnviarConVersionAsync(HttpMethod.Delete, RutaDeLaLinea(recuentoId, lineaId), etiqueta);

    /// <summary>Añade una clave por la API, tal como salga.</summary>
    /// <param name="cliente">El cliente, que puede ser el de otra empresa.</param>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="peticion">La clave.</param>
    /// <param name="clave">La <c>Idempotency-Key</c>, o <c>null</c> para no mandarla.</param>
    /// <returns>La respuesta cruda.</returns>
    internal static Task<HttpResponseMessage> AnadirAsync(
        HttpClient cliente, Guid recuentoId, AnadirLineaDeRecuentoDto peticion, string? clave = null)
    {
        HttpRequestMessage alta = new(HttpMethod.Post, $"{Recuentos}/{recuentoId}/lineas")
        {
            Content = JsonContent.Create(peticion),
        };

        if (clave is not null)
        {
            alta.Headers.TryAddWithoutValidation(CabeceraDeIdempotencia, clave);
        }

        return cliente.SendAsync(alta);
    }

    /// <summary>La ficha y su <c>ETag</c>, de una sola lectura: lo que ve quien va a confirmar.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <returns>La ficha, con su huella, y la versión de la cabecera.</returns>
    internal async Task<(RecuentoDto Ficha, string Etiqueta)> LoQueVeQuienConfirmaAsync(Guid recuentoId)
    {
        using HttpResponseMessage lectura = await Cliente.GetAsync($"{Recuentos}/{recuentoId}");

        RecuentoDto ficha = await EscenaDeTransferencia.LeerAsync<RecuentoDto>(lectura);

        lectura.Headers.ETag.ShouldNotBeNull("la ficha no emite ETag, así que no hay versión que citar");

        return (ficha, lectura.Headers.ETag.ToString());
    }

    /// <summary>Cuenta una línea con la versión que tiene ahora, y tiene que salir bien.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="lineaId">La línea.</param>
    /// <param name="contado">Lo contado.</param>
    /// <returns>La tarea.</returns>
    internal async Task ContarConSuVersionAsync(Guid recuentoId, Guid lineaId, decimal contado)
    {
        string etiqueta = await Cliente.EtiquetaDeAsync(RutaDeLaLinea(recuentoId, lineaId));

        using HttpResponseMessage conteo = await ContarAsync(recuentoId, lineaId, etiqueta, contado);

        conteo.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(conteo));
    }

    /// <summary>La confirmación por la API, tal como salga.</summary>
    /// <param name="cliente">El cliente, que puede ser el de otra empresa.</param>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="etiqueta">El <c>If-Match</c>, o <c>null</c> para no mandarlo.</param>
    /// <param name="huella">La huella del cuerpo, tal cual, o <c>null</c> para mandarla vacía.</param>
    /// <param name="clave">La <c>Idempotency-Key</c>, o <c>null</c> para no mandarla.</param>
    /// <returns>La respuesta cruda.</returns>
    internal static Task<HttpResponseMessage> ConfirmarAsync(
        HttpClient cliente, Guid recuentoId, string? etiqueta, string? huella, string? clave) =>
        EnLaCabeceraAsync(
            cliente,
            recuentoId,
            "confirmacion",
            JsonContent.Create(new ConfirmarRecuentoDto { HuellaDelTeorico = huella! }),
            etiqueta,
            clave);

    /// <summary>La anulación por la API, tal como salga.</summary>
    /// <param name="cliente">El cliente, que puede ser el de otra empresa.</param>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="etiqueta">El <c>If-Match</c>, o <c>null</c> para no mandarlo.</param>
    /// <param name="motivo">Por qué se anula, tal cual.</param>
    /// <param name="clave">La <c>Idempotency-Key</c>, o <c>null</c> para no mandarla.</param>
    /// <returns>La respuesta cruda.</returns>
    internal static Task<HttpResponseMessage> AnularAsync(
        HttpClient cliente, Guid recuentoId, string? etiqueta, string motivo, string? clave) =>
        EnLaCabeceraAsync(
            cliente, recuentoId, "anulacion", JsonContent.Create(new AnularRecuentoDto(motivo)), etiqueta, clave);

    /// <summary>La anulación por la API con una clave nueva, tal como salga.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="etiqueta">El <c>If-Match</c>.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> AnularAsync(Guid recuentoId, string etiqueta) =>
        AnularAsync(Cliente, recuentoId, etiqueta, MotivoDeLaAnulacion, Guid.NewGuid().ToString());

    /// <summary>El descarte por la API, tal como salga.</summary>
    /// <param name="cliente">El cliente, que puede ser el de otra empresa.</param>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="etiqueta">El <c>If-Match</c>, o <c>null</c> para no mandarlo.</param>
    /// <param name="motivo">Por qué se descarta, tal cual.</param>
    /// <param name="clave">La <c>Idempotency-Key</c>, o <c>null</c> para no mandarla, que se admite.</param>
    /// <returns>La respuesta cruda.</returns>
    internal static Task<HttpResponseMessage> DescartarAsync(
        HttpClient cliente, Guid recuentoId, string? etiqueta, string motivo, string? clave) =>
        EnLaCabeceraAsync(
            cliente, recuentoId, "descarte", JsonContent.Create(new DescartarRecuentoDto(motivo)), etiqueta, clave);

    /// <summary>El descarte por la API sin clave, tal como salga.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="etiqueta">El <c>If-Match</c>.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> DescartarAsync(Guid recuentoId, string etiqueta) =>
        DescartarAsync(Cliente, recuentoId, etiqueta, MotivoDelDescarte, clave: null);

    /// <summary>La confirmación por la API con una clave nueva, tal como salga.</summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="etiqueta">El <c>If-Match</c>.</param>
    /// <param name="huella">La huella del teórico.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> ConfirmarAsync(Guid recuentoId, string etiqueta, string huella) =>
        ConfirmarAsync(Cliente, recuentoId, etiqueta, huella, Guid.NewGuid().ToString());

    /// <summary>
    /// Confirma con lo que enseña la ficha en este instante, su versión y su huella, y tiene que salir
    /// bien.
    /// </summary>
    /// <param name="recuentoId">El recuento.</param>
    /// <returns>El recuento confirmado, tal como lo devolvió la confirmación.</returns>
    internal async Task<RecuentoDto> ConfirmarConLaFichaDeAhoraAsync(Guid recuentoId)
    {
        (RecuentoDto ficha, string etiqueta) = await LoQueVeQuienConfirmaAsync(recuentoId);

        using HttpResponseMessage confirmacion = await ConfirmarAsync(recuentoId, etiqueta, ficha.HuellaDelTeorico!);

        return await EscenaDeTransferencia.LeerAsync<RecuentoDto>(confirmacion);
    }

    /// <summary>Las líneas que el problema trae en <c>actual</c>.</summary>
    /// <param name="respuesta">La respuesta, que es un problema.</param>
    /// <returns>Lo que trae, leído como lo lee el frontal.</returns>
    internal static async Task<LineasEnConflictoDto> LasLineasEnConflictoAsync(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        problema.RootElement.TryGetProperty("actual", out JsonElement actual)
            .ShouldBeTrue("el problema no trae las líneas en «actual»");

        return actual.Deserialize<LineasEnConflictoDto>(JsonSerializerOptions.Web)!;
    }

    /// <summary>Que la respuesta sea el problema que se espera, con su estado y su <c>type</c>.</summary>
    /// <param name="respuesta">La respuesta.</param>
    /// <param name="estado">El estado.</param>
    /// <param name="codigo">El código, sin el <c>/errors/</c> delante.</param>
    /// <param name="cual">Qué petición era, para el mensaje.</param>
    /// <returns>La tarea.</returns>
    internal static async Task ExigirElProblemaAsync(
        HttpResponseMessage respuesta, HttpStatusCode estado, string codigo, string cual)
    {
        respuesta.StatusCode.ShouldBe(estado, $"{cual}: {await Escenario.Detalle(respuesta)}");
        (await EscenaDeTransferencia.TipoDelProblemaAsync(respuesta)).ShouldBe($"/errors/{codigo}", cual);
    }

    /// <summary>Una acción de la cabecera por la API, con sus dos cabeceras si las hay.</summary>
    private static Task<HttpResponseMessage> EnLaCabeceraAsync(
        HttpClient cliente, Guid recuentoId, string accion, HttpContent cuerpo, string? etiqueta, string? clave)
    {
        HttpRequestMessage peticion = new(HttpMethod.Post, $"{Recuentos}/{recuentoId}/{accion}") { Content = cuerpo };

        if (etiqueta is not null)
        {
            peticion.Headers.TryAddWithoutValidation("If-Match", etiqueta);
        }

        if (clave is not null)
        {
            peticion.Headers.TryAddWithoutValidation(CabeceraDeIdempotencia, clave);
        }

        return cliente.SendAsync(peticion);
    }

    /// <summary>Mete unidades en un hueco con un ajuste confirmado por la API.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="almacenId">Dónde.</param>
    /// <param name="ubicacionId">En qué hueco.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="lote">El código del lote, o nada.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <returns>El ajuste, ya confirmado.</returns>
    internal Task<Guid> EntrarAsync(
        PostgresConTodosLosModulos postgres,
        Guid almacenId,
        Guid ubicacionId,
        decimal cantidad,
        string? lote = null,
        string? serie = null) =>
        Escena.EntrarAsync(postgres, almacenId, ubicacionId, cantidad, 2.50m, serie: serie, lote: lote);

    /// <summary>Saca unidades de un hueco con un ajuste confirmado por la API, al precio medio.</summary>
    /// <remarks>
    /// La de la escena de la transferencia no lleva lote ni serie: aquí hacen falta, para vaciar una
    /// clave de un artículo con marca.
    /// </remarks>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="almacenId">De dónde.</param>
    /// <param name="ubicacionId">De qué hueco.</param>
    /// <param name="cantidad">Cuánto, en unidad base y sin signo.</param>
    /// <param name="lote">El código del lote, o nada.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <returns>La tarea.</returns>
    internal async Task SalirAsync(
        PostgresConTodosLosModulos postgres,
        Guid almacenId,
        Guid ubicacionId,
        decimal cantidad,
        string? lote = null,
        string? serie = null)
    {
        Guid ajusteId;

        await using (ElModuloDeInventario modulo = new(postgres, Escena.EmpresaId))
        {
            ajusteId = await Escena.AbrirUnAjusteAsync(
                modulo,
                almacenId,
                new LineaDeAjusteDto(
                    ubicacionId, Escena.ArticuloId, -cantidad, Escena.UnidadId, 1m, null, lote, serie));
        }

        using HttpResponseMessage confirmado = await Escena.ConfirmarElAjustePorLaApiAsync(ajusteId);

        confirmado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(confirmado));
    }
}
