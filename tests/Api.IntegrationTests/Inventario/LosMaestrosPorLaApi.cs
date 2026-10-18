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
    internal const string Conversiones = "/api/v1/organizacion/conversiones-de-unidades";
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
    /// <param name="tipo">Qué documentos numera. Por omisión, ajustes.</param>
    /// <returns>La serie recién creada.</returns>
    internal static async Task<SerieDto> CrearSerieAsync(
        HttpClient cliente, string codigo, TipoDeDocumento tipo = TipoDeDocumento.AjusteDeInventario)
    {
        // EL EJERCICIO DEL AÑO EN CURSO, que es el de «hoy», que es la fecha de operación con la
        // que estos casos abren sus ajustes. Desde el ítem 2.6 esto NO es una comodidad: confirmar
        // exige que la fecha del documento caiga en un ejercicio ABIERTO de la empresa (R9), así
        // que un ejercicio de otro año dejaría todos estos casos contestando
        // `ajuste-sin-ejercicio`.
        //
        // Y desde el ADR-0043 el ejercicio de la fecha tiene que ser, además, el mismo del que
        // cuelga la SERIE: el `WHERE` que numera lo mira. Aquí coinciden porque se abren juntos.
        EjercicioDto abierto = await CrearEjercicioAsync(cliente, DateTime.UtcNow.Year);

        return await CrearSerieEnAsync(cliente, abierto.Id, codigo, tipo);
    }

    /// <summary>El ejercicio de un año natural entero, abierto, por la API.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="anio">El año.</param>
    /// <returns>El ejercicio recién creado.</returns>
    internal static async Task<EjercicioDto> CrearEjercicioAsync(HttpClient cliente, int anio)
    {
        using HttpResponseMessage ejercicio = await cliente.PostAsJsonAsync(
            Ejercicios,
            new CrearEjercicioDto
            {
                Anio = anio,
                FechaDeInicio = new DateOnly(anio, 1, 1),
                FechaDeFin = new DateOnly(anio, 12, 31),
            });

        ejercicio.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(ejercicio));

        return (await ejercicio.Content.ReadFromJsonAsync<EjercicioDto>())!;
    }

    /// <summary>Una serie activa colgada del ejercicio que se le pase.</summary>
    /// <remarks>
    /// <b>Existe desde el ítem 2.6</b>, cuando aparecieron casos que necesitan un ejercicio que no
    /// es el del año en curso —uno viejo y cerrado, para anular contra él—. Abrir el ejercicio y
    /// colgarle la serie son dos pasos distintos desde que hay quien quiere quedarse con el
    /// primero.
    /// </remarks>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="ejercicioId">El ejercicio del que cuelga la serie (primera cláusula de R5).</param>
    /// <param name="codigo">Código de la serie, propio de este caso.</param>
    /// <param name="tipo">
    /// Qué documentos numera. Por omisión, ajustes; otro tipo es el caso que la sentencia rechaza.
    /// </param>
    /// <returns>La serie recién creada.</returns>
    internal static async Task<SerieDto> CrearSerieEnAsync(
        HttpClient cliente,
        Guid ejercicioId,
        string codigo,
        TipoDeDocumento tipo = TipoDeDocumento.AjusteDeInventario)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Series,
            new CrearSerieDto
            {
                EjercicioId = ejercicioId,
                TipoDeDocumento = tipo.ToString(),
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
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="semilla">El número del caso, que va en los códigos.</param>
    /// <param name="trazabilidad">La marca con la que nace: sin trazabilidad si no se dice.</param>
    internal static async Task<(Guid ArticuloId, Guid UnidadId)> CrearArticuloAsync(
        HttpClient cliente, int semilla, string trazabilidad = "Ninguna")
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
                Trazabilidad = trazabilidad,
                UnidadBaseId = unidadId,
                ImpuestoPorDefectoId =
                    (await impuesto.Content.ReadFromJsonAsync<ImpuestoDto>())!.Id,
                CategoriaId = null,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return ((await alta.Content.ReadFromJsonAsync<ArticuloDto>())!.Id, unidadId);
    }

    /// <summary>Una unidad de medida nueva, sin decimales y sin ninguna conversión.</summary>
    /// <remarks>
    /// Es un maestro de instalación, como la del artículo: el código lo elige quien llama, de hasta
    /// diez caracteres, y no lo repite ningún otro caso.
    /// </remarks>
    /// <param name="cliente">Cliente autenticado.</param>
    /// <param name="codigo">El código, único en toda la base.</param>
    /// <returns>Su identificador.</returns>
    internal static async Task<Guid> CrearUnidadAsync(HttpClient cliente, string codigo)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto { Codigo = codigo, Nombre = "Unidad " + codigo, Decimales = 0 });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<UnidadMedidaDto>())!.Id;
    }

    /// <summary>Una conversión de una unidad a otra, por la API de Organización.</summary>
    /// <param name="cliente">Cliente autenticado.</param>
    /// <param name="origenId">La unidad de la que se parte.</param>
    /// <param name="destinoId">La unidad a la que se llega.</param>
    /// <param name="factor">Unidades de destino en una de origen.</param>
    /// <returns>La conversión dada de alta.</returns>
    internal static async Task<ConversionUmDto> CrearConversionAsync(
        HttpClient cliente, Guid origenId, Guid destinoId, decimal factor)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Conversiones,
            new CrearConversionUmDto { UnidadOrigenId = origenId, UnidadDestinoId = destinoId, Factor = factor });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ConversionUmDto>())!;
    }

    /// <summary>
    /// Una unidad nueva que convierte, con el mismo factor, a cada una de las unidades base que se le
    /// den.
    /// </summary>
    /// <remarks>
    /// <b>Desde el 2.17 el factor de una línea de ajuste lo pone el servidor</b> (ADR-0065 §2): una
    /// línea que no va en la unidad base de su artículo necesita una conversión de verdad a ella, y
    /// esta es la forma corta de darla de alta.
    /// </remarks>
    /// <param name="cliente">Cliente autenticado.</param>
    /// <param name="codigo">El código de la unidad nueva, único en toda la base.</param>
    /// <param name="factor">Unidades base que hay en una de la nueva.</param>
    /// <param name="unidadesBase">Las bases a las que convierte.</param>
    /// <returns>El identificador de la unidad nueva.</returns>
    internal static async Task<Guid> CrearUnidadQueConvierteAAsync(
        HttpClient cliente, string codigo, decimal factor, params Guid[] unidadesBase)
    {
        unidadesBase.ShouldNotBeEmpty("una unidad que convierte tiene que convertir a algo");

        Guid unidadId = await CrearUnidadAsync(cliente, codigo);

        foreach (Guid unidadBaseId in unidadesBase)
        {
            await CrearConversionAsync(cliente, unidadId, unidadBaseId, factor);
        }

        return unidadId;
    }
}
