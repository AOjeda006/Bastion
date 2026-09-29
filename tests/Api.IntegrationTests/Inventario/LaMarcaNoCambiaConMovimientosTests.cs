using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La marca de trazabilidad de un artículo cambia por la API mientras su libro está vacío, y deja
/// de cambiar con el primer movimiento (ADR-0048 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el segundo cruce mutuo en marcha, de punta a punta</b>: el <c>PUT</c> de Catálogo pregunta
/// a Inventario por su puerto, y lo que contesta es el libro de verdad. Los casos unitarios prueban
/// el orden de los pasos con dobles; aquí se prueba que la pregunta llega a la tabla y que el
/// <c>409</c> sale por el borde con su <c>type</c>.
/// </para>
/// <para>
/// <b>La carrera no está aquí.</b> Una confirmación en vuelo no se ve hasta su <c>COMMIT</c>, así
/// que lo que la cierra es que la confirmación lea la marca con un cerrojo compartido, y eso es de
/// Inventario y entra con el lote en la línea.
/// </para>
/// <para>
/// <b>Semillas: el fichero entero es del 540 al 549.</b> Las empresas, del 540 al 542; los
/// maestros de instalación, del 545 al 547. El tramo anterior, del 520 al 539, es de
/// <c>NingunaFechaAnteriorAlUltimoMovimientoTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaMarcaNoCambiaConMovimientosTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    [Fact]
    public async Task Sin_movimientos_la_marca_cambia_y_se_lee_por_la_api()
    {
        Escena escena = await UnArticuloAsync(540, "MAR-A", 545);

        using HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            escena.Ruta, escena.Cambio("Artículo con lote", "PorLote"));

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        (await cambio.Content.ReadFromJsonAsync<ArticuloDto>())!.Trazabilidad.ShouldBe("PorLote");

        // Leída otra vez, y no solo de la respuesta: la respuesta sale del agregado en memoria.
        (await escena.Cliente.GetFromJsonAsync<ArticuloDto>(escena.Ruta))!
            .Trazabilidad.ShouldBe("PorLote");
    }

    [Fact]
    public async Task Con_un_ajuste_confirmado_la_marca_no_cambia_y_se_contesta_409()
    {
        Escena escena = await UnArticuloAsync(541, "MAR-B", 546);

        Guid ajusteId = await UnBorradorAsync(escena);

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");
        }

        using HttpResponseMessage rechazo = await escena.Cliente.ModificarAsync(
            escena.Ruta, escena.Cambio("Artículo con serie", "PorNumeroSerie"));

        rechazo.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(rechazo));
        (await rechazo.Content.ReadAsStringAsync())
            .ShouldContain("/errors/articulo-trazabilidad-con-movimientos");

        ArticuloDto comoQuedo = (await escena.Cliente.GetFromJsonAsync<ArticuloDto>(escena.Ruta))!;

        comoQuedo.Trazabilidad.ShouldBe("Ninguna");
        comoQuedo.Descripcion.ShouldBe(
            escena.Descripcion, "el rechazo es de la ficha entera, no solo de la marca");

        // Y la contrapartida: con la marca de siempre, la ficha se sigue corrigiendo. Sin esto,
        // el 409 de arriba lo daría igual un caso de uso que preguntara siempre.
        using HttpResponseMessage correccion = await escena.Cliente.ModificarAsync(
            escena.Ruta, escena.Cambio("Descripción corregida", "Ninguna"));

        correccion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(correccion));
    }

    [Fact]
    public async Task Un_borrador_no_es_un_movimiento()
    {
        // Un borrador no ha escrito nada en el libro. Si la marca cambia con él dentro, la
        // confirmación lo comprobará otra vez contra la marca nueva.
        Escena escena = await UnArticuloAsync(542, "MAR-C", 547);

        await UnBorradorAsync(escena);

        using HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            escena.Ruta, escena.Cambio("Artículo con lote", "PorLote"));

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
    }

    /// <summary>Una empresa nueva con un almacén, una ubicación, una serie y un artículo sin marca.</summary>
    /// <param name="semilla">Número de empresa, que acaba en su NIF.</param>
    /// <param name="codigo">Prefijo de los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    private async Task<Escena> UnArticuloAsync(int semilla, string codigo, int semillaDeInstalacion)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        UbicacionDto ubicacion =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01");

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semillaDeInstalacion);

        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, codigo);

        string ruta = $"{LosMaestrosPorLaApi.Articulos}/{articuloId}";
        ArticuloDto articulo = (await cliente.GetFromJsonAsync<ArticuloDto>(ruta))!;

        articulo.Trazabilidad.ShouldBe("Ninguna", "el alta sin marca es sin trazabilidad");

        return new Escena(
            cliente, empresa.Id, almacen.Id, ubicacion.Id, serie.Id, articulo, unidadId, ruta);
    }

    /// <summary>Un ajuste en borrador que sube cuatro unidades del artículo de la escena.</summary>
    /// <param name="escena">Dónde.</param>
    private async Task<Guid> UnBorradorAsync(Escena escena)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                escena.SerieId,
                escena.AlmacenId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Regularización de un recuento",
                [new LineaDeAjusteDto(
                    escena.UbicacionId, escena.Articulo.Id, 4m, escena.UnidadId, 2m, 1.50m)]),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    /// <summary>Lo que un caso necesita de su empresa, ya montado.</summary>
    private sealed record Escena(
        HttpClient Cliente,
        Guid EmpresaId,
        Guid AlmacenId,
        Guid UbicacionId,
        Guid SerieId,
        ArticuloDto Articulo,
        Guid UnidadId,
        string Ruta)
    {
        internal string Descripcion => Articulo.Descripcion;

        internal ModificarArticuloDto Cambio(string descripcion, string trazabilidad) => new()
        {
            Descripcion = descripcion,
            Tipo = Articulo.Tipo,
            Trazabilidad = trazabilidad,
            ImpuestoPorDefectoId = Articulo.ImpuestoPorDefectoId,
            CategoriaId = Articulo.CategoriaId,
        };
    }
}
