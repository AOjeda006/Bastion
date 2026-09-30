using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Una empresa con dos almacenes, tres ubicaciones, una serie de ajustes y un artículo con su
/// marca, todo por la API: lo que necesita un caso del lote o de la serie (ítem 2.9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Dos ubicaciones en el primer almacén y una en el segundo</b>, porque «un número de serie no
/// puede estar en dos sitios a la vez» tiene dos formas de romperse: en otro hueco del mismo
/// almacén y en otro almacén. La segunda es la de la carrera, que necesita dos valoraciones
/// distintas para que la única espera sea la del índice.
/// </para>
/// <para>
/// <b>El borrador se abre por el caso de uso cableado a mano y se confirma por la API</b>, como en
/// <c>LaAnulacionConContraDocumentoTests</c>: el alta no tiene borde todavía, y lo que se confirma
/// o se anula por HTTP es lo que el borde traduce. Un caso que quiere ver el error del motor, y no
/// su traducción, confirma con <see cref="ElModuloDeInventario"/>, que deja pasar la excepción.
/// </para>
/// <para>
/// <b>Un artículo por escena</b>; el caso que necesita dos se hace el segundo con
/// <see cref="LosMaestrosPorLaApi.CrearArticuloAsync"/> y lo cambia con <c>with</c>.
/// </para>
/// </remarks>
/// <param name="Cliente">Cliente autenticado en la empresa de la escena.</param>
/// <param name="EmpresaId">La empresa (R8).</param>
/// <param name="Serie">La serie de ajustes, del ejercicio del año en curso.</param>
/// <param name="AlmacenA">El primer almacén.</param>
/// <param name="UbicacionA1">Un hueco del primer almacén.</param>
/// <param name="UbicacionA2">Otro hueco del primer almacén.</param>
/// <param name="AlmacenB">El segundo almacén.</param>
/// <param name="UbicacionB1">El hueco del segundo almacén.</param>
/// <param name="ArticuloId">El artículo, con la marca con la que se montó.</param>
/// <param name="UnidadId">Su unidad base.</param>
internal sealed record EscenaTrazable(
    HttpClient Cliente,
    Guid EmpresaId,
    SerieDto Serie,
    Guid AlmacenA,
    Guid UbicacionA1,
    Guid UbicacionA2,
    Guid AlmacenB,
    Guid UbicacionB1,
    Guid ArticuloId,
    Guid UnidadId)
{
    private const string Cabecera = "Idempotency-Key";

    /// <summary>El día de todos los documentos de la escena: hoy, con el reloj de verdad.</summary>
    internal static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Monta la escena en una empresa nueva.</summary>
    /// <param name="api">La API del caso, que da la empresa y su cliente.</param>
    /// <param name="semilla">Número de empresa, que acaba en su NIF.</param>
    /// <param name="codigo">Prefijo de los códigos de este caso.</param>
    /// <param name="maestro">Número de instalación del artículo, su unidad y su tramo.</param>
    /// <param name="trazabilidad">La marca del artículo.</param>
    /// <returns>La escena; su cliente lo cierra quien la pidió.</returns>
    internal static async Task<EscenaTrazable> MontarAsync(
        ApiDeVerdad api, int semilla, string codigo, int maestro, string trazabilidad)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        AlmacenDto almacenA = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo + "-A");
        AlmacenDto almacenB = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo + "-B");

        UbicacionDto a1 =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacenA.Id, codigo + "-A1");
        UbicacionDto a2 =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacenA.Id, codigo + "-A2");
        UbicacionDto b1 =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacenB.Id, codigo + "-B1");

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, maestro, trazabilidad);

        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, codigo);

        return new EscenaTrazable(
            cliente, empresa.Id, serie, almacenA.Id, a1.Id, a2.Id, almacenB.Id, b1.Id, articuloId, unidadId);
    }

    /// <summary>
    /// Otra serie de ajustes en el mismo ejercicio, para que dos documentos no se esperen en el
    /// contador.
    /// </summary>
    /// <param name="codigo">Su código.</param>
    /// <returns>La serie.</returns>
    internal Task<SerieDto> OtraSerieAsync(string codigo) =>
        LosMaestrosPorLaApi.CrearSerieEnAsync(Cliente, Serie.EjercicioId, codigo);

    /// <summary>Una línea que sube, con coste: sin existencias no hay precio medio.</summary>
    /// <param name="ubicacionId">Dónde.</param>
    /// <param name="cantidad">Cuánto, en unidad base.</param>
    /// <param name="lote">El código del lote, o nada.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <returns>La línea.</returns>
    internal LineaDeAjusteDto Entrada(
        Guid ubicacionId, decimal cantidad, string? lote = null, string? serie = null) =>
        new(ubicacionId, ArticuloId, cantidad, UnidadId, 1m, 10m, lote, serie);

    /// <summary>Una línea que baja, sin coste: sale al precio medio.</summary>
    /// <param name="ubicacionId">De dónde.</param>
    /// <param name="cantidad">Cuánto, en unidad base y sin signo.</param>
    /// <param name="lote">El código del lote, o nada.</param>
    /// <param name="serie">El número de serie, o nada.</param>
    /// <returns>La línea.</returns>
    internal LineaDeAjusteDto Salida(
        Guid ubicacionId, decimal cantidad, string? lote = null, string? serie = null) =>
        new(ubicacionId, ArticuloId, -cantidad, UnidadId, 1m, null, lote, serie);

    /// <summary>Un borrador, abierto por el caso de uso con el módulo que se le pase.</summary>
    /// <param name="modulo">El módulo de la empresa de la escena.</param>
    /// <param name="almacenId">El almacén del documento.</param>
    /// <param name="lineas">Lo que mueve.</param>
    /// <param name="serieId">La serie que lo numerará; la de la escena si no se dice.</param>
    /// <returns>El identificador del borrador.</returns>
    internal async Task<Guid> AbrirAsync(
        ElModuloDeInventario modulo,
        Guid almacenId,
        IReadOnlyList<LineaDeAjusteDto> lineas,
        Guid? serieId = null)
    {
        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(serieId ?? Serie.Id, almacenId, Hoy, "Regularización de un recuento", lineas),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    /// <summary>Un borrador, abierto con un módulo propio que se cierra al volver.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="almacenId">El almacén del documento.</param>
    /// <param name="lineas">Lo que mueve.</param>
    /// <returns>El identificador del borrador.</returns>
    internal async Task<Guid> AbrirAsync(
        PostgresConTodosLosModulos postgres, Guid almacenId, IReadOnlyList<LineaDeAjusteDto> lineas)
    {
        await using ElModuloDeInventario modulo = new(postgres, EmpresaId);

        return await AbrirAsync(modulo, almacenId, lineas);
    }

    /// <summary>Abre un documento y lo confirma por la API, que tiene que salir bien.</summary>
    /// <param name="postgres">El contenedor.</param>
    /// <param name="almacenId">El almacén del documento.</param>
    /// <param name="lineas">Lo que mueve.</param>
    /// <returns>El identificador del documento, ya confirmado.</returns>
    internal async Task<Guid> MoverAsync(
        PostgresConTodosLosModulos postgres, Guid almacenId, IReadOnlyList<LineaDeAjusteDto> lineas)
    {
        Guid ajusteId = await AbrirAsync(postgres, almacenId, lineas);

        using HttpResponseMessage confirmado = await ConfirmarPorLaApiAsync(ajusteId);

        confirmado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(confirmado));

        return ajusteId;
    }

    /// <summary>La confirmación por HTTP, con su clave, tal como salga.</summary>
    /// <param name="ajusteId">El borrador.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> ConfirmarPorLaApiAsync(Guid ajusteId) =>
        EnviarConClaveAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/inventario/ajustes/{ajusteId}/confirmacion"));

    /// <summary>La anulación por HTTP, con su clave, tal como salga.</summary>
    /// <param name="ajusteId">El documento confirmado.</param>
    /// <returns>La respuesta cruda.</returns>
    internal Task<HttpResponseMessage> AnularPorLaApiAsync(Guid ajusteId) =>
        EnviarConClaveAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/inventario/ajustes/{ajusteId}/anulacion")
            {
                Content = JsonContent.Create(new AnularAjusteDto("Me equivoqué")),
            });

    /// <summary>Por dónde va una serie de ajustes, leído por la API.</summary>
    /// <param name="serieId">La serie; la de la escena si no se dice.</param>
    /// <returns>Cuántos correlativos ha entregado.</returns>
    internal async Task<long> ContadorAsync(Guid? serieId = null) =>
        (await Cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serieId ?? Serie.Id}"))!.Contador;

    private async Task<HttpResponseMessage> EnviarConClaveAsync(HttpRequestMessage peticion)
    {
        using (peticion)
        {
            peticion.Headers.TryAddWithoutValidation(Cabecera, Guid.NewGuid().ToString());

            return await Cliente.SendAsync(peticion);
        }
    }
}
