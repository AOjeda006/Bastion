using System.Net;
using System.Net.Http.Json;
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

    /// <summary>El motivo de los recuentos de los casos, que es el que llevará el ajuste.</summary>
    internal const string Motivo = "Recuento anual del almacén";

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
