using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Series;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Los maestros que un ajuste necesita, dados de alta <b>por la API</b> y en la empresa del
/// cliente que se le pase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por la API y no por el contexto</b>, que es lo que hace que estos casos prueben algo: un
/// almacén insertado a mano pasaría por alto las validaciones del alta y los identificadores que
/// luego se envían describirían un estado que el sistema no sabe producir. Es el mismo criterio
/// con el que <c>ElLibro</c> escribe sus filas con el dominio y no con un <c>INSERT</c>.
/// </para>
/// <para>
/// <b>Vive aparte desde el ítem 2.4</b>, cuando el caso de la serie pasó a necesitar los mismos
/// maestros que el del almacén bloqueado. Las rutas están aquí dentro por lo mismo: dos copias de
/// la misma cadena se separan el día que una de las dos se corrige.
/// </para>
/// <para>
/// <b>Las semillas las elige quien llama, y no se reparten aquí.</b> Cada número acaba en un
/// código o un NIF único de una tabla que toda la colección comparte, así que el reparto es de los
/// ficheros de casos —que son los que se leen juntos— y está escrito en el <c>remarks</c> de cada
/// uno.
/// </para>
/// </remarks>
internal static class LosMaestrosPorLaApi
{
    internal const string Almacenes = "/api/v1/organizacion/almacenes";
    internal const string Ubicaciones = "/api/v1/organizacion/ubicaciones";
    internal const string Unidades = "/api/v1/organizacion/unidades-de-medida";
    internal const string Impuestos = "/api/v1/organizacion/impuestos";
    internal const string Articulos = "/api/v1/catalogo/articulos";
    internal const string Ejercicios = "/api/v1/organizacion/ejercicios";
    internal const string Series = "/api/v1/organizacion/series";

    private static readonly DateOnly s_desde = new(2000, 1, 1);

    internal static async Task<AlmacenDto> CrearAlmacenAsync(HttpClient cliente, string codigo)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Almacenes,
            new CrearAlmacenDto
            {
                Codigo = codigo,
                Nombre = $"Almacén {codigo}",
                Tipo = "Fisico",
                Direccion = Escenario.Domicilio(),
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<AlmacenDto>())!;
    }

    /// <summary>Un ejercicio y una serie activa que numere ajustes, por la API.</summary>
    /// <remarks>
    /// El ejercicio hace falta porque una serie numera <b>por serie y ejercicio</b>, que es la
    /// primera de las tres cláusulas de la R5, y <c>Serie</c> lo exige desde que existe.
    /// </remarks>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="codigo">Código de la serie, propio de este caso.</param>
    /// <returns>La serie recién creada.</returns>
    internal static async Task<SerieDto> CrearSerieAsync(HttpClient cliente, string codigo)
    {
        // El año de la fecha de operación del ajuste, que es «hoy». Hoy por hoy nada comprueba que
        // el documento caiga dentro del ejercicio de su serie —ni el dominio ni el `WHERE` que
        // numera—, pero un ejercicio de otro año dejaría escrito aquí lo contrario de lo que se
        // quiere el día que esa comprobación exista.
        int anioDelCaso = DateTime.UtcNow.Year;

        using HttpResponseMessage ejercicio = await cliente.PostAsJsonAsync(
            Ejercicios,
            new CrearEjercicioDto
            {
                Anio = anioDelCaso,
                FechaDeInicio = new DateOnly(anioDelCaso, 1, 1),
                FechaDeFin = new DateOnly(anioDelCaso, 12, 31),
            });

        ejercicio.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(ejercicio));

        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Series,
            new CrearSerieDto
            {
                EjercicioId = (await ejercicio.Content.ReadFromJsonAsync<EjercicioDto>())!.Id,
                TipoDeDocumento = nameof(TipoDeDocumento.AjusteDeInventario),
                Codigo = codigo,
                Formato = "{serie}-{numero:0000}",
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<SerieDto>())!;
    }

    internal static async Task<UbicacionDto> CrearUbicacionAsync(
        HttpClient cliente, Guid almacenId, string codigo)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Ubicaciones,
            new CrearUbicacionDto
            {
                AlmacenId = almacenId,
                Codigo = codigo,
                Pasillo = "A",
                Estante = "1",
                Hueco = "1",
                Descripcion = "Hueco del caso del ADR-0037",
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<UbicacionDto>())!;
    }

    /// <summary>Un artículo con su unidad y su tramo de impuesto, propios de este caso.</summary>
    /// <remarks>
    /// La unidad y el tramo llevan el número del caso porque son maestros de instalación: los ve
    /// toda la base, y dos casos con el mismo código chocarían contra el índice único.
    /// </remarks>
    internal static async Task<(Guid ArticuloId, Guid UnidadId)> CrearArticuloAsync(
        HttpClient cliente, int semilla)
    {
        string sufijo = semilla.ToString(CultureInfo.InvariantCulture);

        using HttpResponseMessage unidad = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto
            {
                Codigo = "W" + sufijo,
                Nombre = "Unidad " + sufijo,
                Decimales = 0,
            });

        unidad.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(unidad));

        using HttpResponseMessage impuesto = await cliente.PostAsJsonAsync(
            Impuestos,
            new CrearImpuestoDto
            {
                Codigo = "ART" + sufijo,
                Nombre = "Tramo ART" + sufijo,
                Tipo = "Iva",
                Porcentaje = 21m,
                VigenteDesde = s_desde,
                VigenteHasta = null,
            });

        impuesto.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(impuesto));

        Guid unidadId = (await unidad.Content.ReadFromJsonAsync<UnidadMedidaDto>())!.Id;

        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Articulos,
            new CrearArticuloDto
            {
                Codigo = "APT-" + sufijo,
                Descripcion = "Artículo " + sufijo,
                Tipo = "Bien",
                UnidadBaseId = unidadId,
                ImpuestoPorDefectoId =
                    (await impuesto.Content.ReadFromJsonAsync<ImpuestoDto>())!.Id,
                CategoriaId = null,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return ((await alta.Content.ReadFromJsonAsync<ArticuloDto>())!.Id, unidadId);
    }
}
