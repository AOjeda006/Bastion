using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.IntegrationTests.Persistencia;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Api;

/// <summary>
/// El contrato de <c>/api/v1/organizacion/*</c> tal como lo ve un cliente: por HTTP, contra la
/// API real y contra PostgreSQL de verdad.
/// </summary>
/// <remarks>
/// Lo que se prueba aquí no lo puede probar un test de dominio ni uno de repositorio: los códigos
/// de estado, el <c>Location</c> de la creación, el <c>ProblemDetails</c> con sus errores por
/// campo, y que el enlace de modelo rechaza lo que tiene que rechazar antes de llegar al caso de
/// uso.
/// </remarks>
[Collection(ColeccionDePostgres.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ContratoDeLaApiTests(PostgresDeVerdad postgres) : IDisposable
{
    private const string Empresas = "/api/v1/organizacion/empresas";
    private const string Ejercicios = "/api/v1/organizacion/ejercicios";
    private const string Series = "/api/v1/organizacion/series";
    private const string Almacenes = "/api/v1/organizacion/almacenes";

    private readonly ApiContraPostgres _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Crear_una_empresa_devuelve_201_con_Location_que_lleva_al_recurso()
    {
        using HttpClient cliente = _api.CreateClient();

        HttpResponseMessage creacion = await cliente.PostAsJsonAsync(Empresas, NuevaEmpresa("00000001R"));

        creacion.StatusCode.ShouldBe(HttpStatusCode.Created);

        // El Location no es adorno: tiene que llevar al recurso de verdad. Comprobarlo siguiéndolo
        // es la única manera de saber que la ruta con nombre y la acción de consulta casan; si el
        // `nameof` no cuadrara, CreatedAtAction devolvería un 500 y no un Location roto.
        creacion.Headers.Location.ShouldNotBeNull();

        // En minúsculas, y con el sustantivo en plural del §9. Sin forzarlo, el token
        // `[controller]` copia el nombre de la clase y publica `…/Empresas/…`: el nombre de un
        // tipo de C# asomando por el contrato, y una ruta distinta de la documentada.
        creacion.Headers.Location.AbsolutePath.ShouldStartWith(Empresas + "/");

        HttpResponseMessage seguimiento = await cliente.GetAsync(creacion.Headers.Location);
        seguimiento.StatusCode.ShouldBe(HttpStatusCode.OK);

        EmpresaDto? empresa = await seguimiento.Content.ReadFromJsonAsync<EmpresaDto>();
        empresa.ShouldNotBeNull();
        empresa.Nif.ShouldBe("00000001R");
        empresa.Estado.ShouldBe("Activa");
    }

    [Fact]
    public async Task Los_enumerados_viajan_como_texto_y_no_como_numero()
    {
        using HttpClient cliente = _api.CreateClient();
        HttpResponseMessage creacion = await cliente.PostAsJsonAsync(Empresas, NuevaEmpresa("11111111H"));

        string cuerpo = await creacion.Content.ReadAsStringAsync();

        // Un ordinal es un contrato que se rompe solo con reordenar el enumerado, y quien lo
        // reordena no ve que está rompiendo a nadie.
        cuerpo.ShouldContain("\"regimenDeIva\":\"General\"");
        cuerpo.ShouldContain("\"estado\":\"Activa\"");
    }

    [Fact]
    public async Task Un_NIF_con_letra_de_control_incorrecta_es_400_del_campo_nif()
    {
        using HttpClient cliente = _api.CreateClient();

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Empresas, NuevaEmpresa("12345678A"));

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        JsonElement problema = await LeerProblema(respuesta);
        problema.GetProperty("type").GetString().ShouldBe("/errors/datos-no-validos");
        problema.GetProperty("errors").GetProperty("nif").GetArrayLength().ShouldBe(1);

        // El identificador de traza va en TODA respuesta de error, también en las que no vienen
        // de una excepción: es lo que convierte «me ha dado un 400» en algo localizable.
        problema.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Varios_campos_malos_se_devuelven_todos_de_una_vez()
    {
        using HttpClient cliente = _api.CreateClient();

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Empresas, new CrearEmpresaDto
        {
            Nif = "12345678A",
            RazonSocial = "Prueba",
            DomicilioFiscal = Domicilio(),
            DivisaBase = "JPY",
            RegimenDeIva = "Inventado",
        });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        JsonElement errores = (await LeerProblema(respuesta)).GetProperty("errors");

        // Corregir, reenviar y descubrir el siguiente fallo es como se pierde la paciencia con
        // un formulario. Los tres salen juntos.
        errores.TryGetProperty("nif", out _).ShouldBeTrue();
        errores.TryGetProperty("divisaBase", out _).ShouldBeTrue();
        errores.TryGetProperty("regimenDeIva", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Lo_que_falta_en_el_cuerpo_lo_rechaza_el_enlace_de_modelo_con_400_por_campo()
    {
        using HttpClient cliente = _api.CreateClient();

        // Sin razón social ni domicilio: no llega al caso de uso, lo para [ApiController]. La
        // forma del error es la MISMA que la del caso de uso —extensión `errors`—, y por eso un
        // cliente no tiene que distinguir quién lo detectó.
        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Empresas, new { nif = "00000001R" });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await LeerProblema(respuesta)).TryGetProperty("errors", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Dos_empresas_con_el_mismo_NIF_es_409_y_no_una_excepcion_de_PostgreSQL()
    {
        using HttpClient cliente = _api.CreateClient();
        await cliente.PostAsJsonAsync(Empresas, NuevaEmpresa("22222222J"));

        HttpResponseMessage repetida = await cliente.PostAsJsonAsync(Empresas, NuevaEmpresa("22222222J"));

        repetida.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(repetida)).GetProperty("type").GetString()
            .ShouldBe("/errors/empresa-ya-registrada");
    }

    [Fact]
    public async Task Una_empresa_que_no_existe_es_404_con_ProblemDetails()
    {
        using HttpClient cliente = _api.CreateClient();

        HttpResponseMessage respuesta = await cliente.GetAsync($"{Empresas}/{Guid.NewGuid()}");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await LeerProblema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/empresa-no-encontrada");
    }

    [Fact]
    public async Task Borrar_una_empresa_la_bloquea_pero_no_la_borra()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "33333333P");

        HttpResponseMessage borrado = await cliente.DeleteAsync($"{Empresas}/{empresa.Id}");
        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Sigue ahí, y con su estado: el art. 32 de la LOPDGDD manda bloquear, no destruir.
        EmpresaDto? despues = await cliente.GetFromJsonAsync<EmpresaDto>($"{Empresas}/{empresa.Id}");
        despues.ShouldNotBeNull();
        despues.Estado.ShouldBe("Bloqueada");
        despues.BloqueadaEn.ShouldNotBeNull();
    }

    [Fact]
    public async Task Una_empresa_bloqueada_no_se_puede_modificar_y_da_409()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "44444444A");
        await cliente.DeleteAsync($"{Empresas}/{empresa.Id}");

        HttpResponseMessage respuesta = await cliente.PutAsJsonAsync(
            $"{Empresas}/{empresa.Id}",
            new ModificarEmpresaDto
            {
                RazonSocial = "Otro nombre",
                DomicilioFiscal = Domicilio(),
                DivisaBase = "EUR",
                RegimenDeIva = "General",
            });

        // 409 y no 500: modificar algo bloqueado es un desenlace de negocio esperable, no un
        // fallo del programa.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/empresa-bloqueada");
    }

    [Fact]
    public async Task Un_ejercicio_de_mas_de_doce_meses_es_400_del_campo_fechaDeFin()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "55555555K");

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Ejercicios, new CrearEjercicioDto
        {
            EmpresaId = empresa.Id,
            Anio = 2026,
            FechaDeInicio = new DateOnly(2026, 1, 1),
            FechaDeFin = new DateOnly(2027, 6, 30),
        });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await LeerProblema(respuesta)).GetProperty("errors")
            .TryGetProperty("fechaDeFin", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Las_fechas_de_un_ejercicio_van_y_vuelven_como_fechas_de_calendario()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "66666666Q");

        HttpResponseMessage creacion = await cliente.PostAsJsonAsync(Ejercicios, new CrearEjercicioDto
        {
            EmpresaId = empresa.Id,
            Anio = 2026,
            FechaDeInicio = new DateOnly(2026, 1, 1),
            FechaDeFin = new DateOnly(2026, 12, 31),
        });

        string cuerpo = await creacion.Content.ReadAsStringAsync();

        // Sin hora y sin zona. El 1 de enero de 2026 es el 1 de enero en Madrid y en Canarias;
        // un instante habría obligado a elegir zona y en UTC-1 caería el 31 de diciembre.
        cuerpo.ShouldContain("\"fechaDeInicio\":\"2026-01-01\"");
        cuerpo.ShouldContain("\"fechaDeFin\":\"2026-12-31\"");
    }

    [Fact]
    public async Task Un_ejercicio_de_una_empresa_que_no_existe_es_400_del_campo_empresaId()
    {
        using HttpClient cliente = _api.CreateClient();

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Ejercicios, new CrearEjercicioDto
        {
            EmpresaId = Guid.NewGuid(),
            Anio = 2026,
            FechaDeInicio = new DateOnly(2026, 1, 1),
            FechaDeFin = new DateOnly(2026, 12, 31),
        });

        // 400 del campo, y no una excepción de clave ajena convertida en 500: el usuario no ha
        // hecho nada raro más que escribir mal un identificador.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await LeerProblema(respuesta)).GetProperty("errors")
            .TryGetProperty("empresaId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Suprimir_una_serie_que_no_ha_numerado_es_204()
    {
        using HttpClient cliente = _api.CreateClient();
        SerieDto serie = await CrearSerie(cliente, "77777777B", "FAC");

        HttpResponseMessage borrado = await cliente.DeleteAsync($"{Series}/{serie.Id}");

        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await cliente.GetAsync($"{Series}/{serie.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Suprimir_una_serie_que_ya_ha_numerado_es_409()
    {
        using HttpClient cliente = _api.CreateClient();
        SerieDto serie = await CrearSerie(cliente, "88888888Y", "FAC");

        // Numerar todavía no tiene puerta HTTP —es de la fase de facturación—, así que se hace
        // por el dominio, que es quien manda: subir el contador a mano en la base saltándose
        // `RegistrarNumeroAsignado` probaría un estado que el sistema no sabe producir.
        await using (OrganizacionDbContext contexto = postgres.AbrirContexto())
        {
            Serie guardada = await contexto.Series.SingleAsync(fila => fila.Id == serie.Id);
            guardada.RegistrarNumeroAsignado(1);
            await contexto.SaveChangesAsync();
        }

        HttpResponseMessage borrado = await cliente.DeleteAsync($"{Series}/{serie.Id}");

        borrado.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(borrado)).GetProperty("type").GetString()
            .ShouldBe("/errors/serie-ya-numerada");
    }

    [Fact]
    public async Task Una_serie_colgada_del_ejercicio_de_otra_empresa_es_400_del_campo_ejercicioId()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto propia = await CrearEmpresa(cliente, "99999999R");
        EmpresaDto ajena = await CrearEmpresa(cliente, "A58818501");
        EjercicioDto deLaAjena = await CrearEjercicio(cliente, ajena.Id, 2026);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Series, new CrearSerieDto
        {
            EmpresaId = propia.Id,
            EjercicioId = deLaAjena.Id,
            TipoDeDocumento = "FacturaEmitida",
            Codigo = "CRUZADA",
            Formato = "{serie}-{numero:0000}",
        });

        // Las dos claves ajenas serían válidas por separado, y la fila quedaría apuntando a dos
        // contabilidades a la vez. Eso no lo para ninguna restricción de la base.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await LeerProblema(respuesta)).GetProperty("errors")
            .TryGetProperty("ejercicioId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Un_tipo_de_documento_inventado_dice_cuales_se_admiten()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "00000002W");
        EjercicioDto ejercicio = await CrearEjercicio(cliente, empresa.Id, 2026);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Series, new CrearSerieDto
        {
            EmpresaId = empresa.Id,
            EjercicioId = ejercicio.Id,
            TipoDeDocumento = "Inventado",
            Codigo = "X",
            Formato = "{numero}",
        });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        string motivo = (await LeerProblema(respuesta)).GetProperty("errors")
            .GetProperty("tipoDeDocumento")[0].GetString()!;

        // Decir «no es válido» y callarse los valores admitidos obliga a ir a buscar el OpenAPI.
        motivo.ShouldContain(nameof(TipoDeDocumento.FacturaEmitida));
    }

    [Fact]
    public async Task Un_almacen_fisico_sin_direccion_es_400_del_campo_direccion()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "00000003A");

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Almacenes, new CrearAlmacenDto
        {
            EmpresaId = empresa.Id,
            Codigo = "CENTRAL",
            Nombre = "Almacén central",
            Direccion = null,
            Tipo = "Fisico",
        });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await LeerProblema(respuesta)).GetProperty("errors")
            .TryGetProperty("direccion", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Un_almacen_virtual_sin_direccion_se_acepta()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "00000004G");

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Almacenes, new CrearAlmacenDto
        {
            EmpresaId = empresa.Id,
            Codigo = "REGUL",
            Nombre = "Regularizaciones",
            Direccion = null,
            Tipo = "Virtual",
        });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);

        AlmacenDto? almacen = await respuesta.Content.ReadFromJsonAsync<AlmacenDto>();
        almacen.ShouldNotBeNull();
        almacen.Direccion.ShouldBeNull();
    }

    [Fact]
    public async Task El_codigo_de_almacen_se_normaliza_y_el_duplicado_en_minusculas_es_409()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "00000005M");

        await cliente.PostAsJsonAsync(Almacenes, NuevoAlmacen(empresa.Id, "CENTRAL"));
        HttpResponseMessage repetido = await cliente.PostAsJsonAsync(
            Almacenes, NuevoAlmacen(empresa.Id, "  central  "));

        // Preguntando por lo que escribió el usuario, «central» habría pasado el filtro y habría
        // chocado contra el índice único: un 500 en lugar de este 409.
        repetido.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(repetido)).GetProperty("type").GetString()
            .ShouldBe("/errors/almacen-duplicado");
    }

    [Fact]
    public async Task La_direccion_va_y_vuelve_en_los_seis_campos_de_R17()
    {
        using HttpClient cliente = _api.CreateClient();
        EmpresaDto empresa = await CrearEmpresa(cliente, "00000006Y");

        empresa.DomicilioFiscal.Calle.ShouldBe("Gran Vía");
        empresa.DomicilioFiscal.Numero.ShouldBe("31");
        empresa.DomicilioFiscal.CodigoPostal.ShouldBe("28013");
        empresa.DomicilioFiscal.Poblacion.ShouldBe("Madrid");
        empresa.DomicilioFiscal.Subdivision.ShouldBe("Madrid");
        empresa.DomicilioFiscal.Pais.ShouldBe("ES");
    }

    [Fact]
    public async Task El_listado_viene_paginado_y_con_su_total()
    {
        using HttpClient cliente = _api.CreateClient();

        PaginaDe<EmpresaDto>? pagina = await cliente
            .GetFromJsonAsync<PaginaDe<EmpresaDto>>($"{Empresas}?page=1&size=2");

        pagina.ShouldNotBeNull();
        pagina.Pagina.ShouldBe(1);
        pagina.Tamanio.ShouldBe(2);
        pagina.Elementos.Count.ShouldBeLessThanOrEqualTo(2);

        // El total es del conjunto, no de la página: sin él, el cliente no sabe si hay más.
        pagina.Total.ShouldBeGreaterThanOrEqualTo(pagina.Elementos.Count);
    }

    [Fact]
    public async Task Pedir_una_pagina_gigante_no_se_lleva_la_tabla()
    {
        using HttpClient cliente = _api.CreateClient();

        HttpResponseMessage respuesta = await cliente.GetAsync($"{Empresas}?page=1&size=100000");

        // El tope lo aplica el enlace de modelo, que es lo único que se ejecuta de verdad: un
        // objeto de paginación construido a mano en el controlador se saltaría la validación
        // entera y el tope no existiría.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static CrearEmpresaDto NuevaEmpresa(string nif) => new()
    {
        Nif = nif,
        RazonSocial = $"Empresa {nif}",
        DomicilioFiscal = Domicilio(),
        DivisaBase = "EUR",
        RegimenDeIva = "General",
    };

    private static DireccionDto Domicilio() => new()
    {
        Calle = "Gran Vía",
        Numero = "31",
        CodigoPostal = "28013",
        Poblacion = "Madrid",
        Subdivision = "Madrid",
        Pais = "ES",
    };

    private static CrearAlmacenDto NuevoAlmacen(Guid empresaId, string codigo) => new()
    {
        EmpresaId = empresaId,
        Codigo = codigo,
        Nombre = "Almacén",
        Direccion = Domicilio(),
        Tipo = "Fisico",
    };

    private static async Task<JsonElement> LeerProblema(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<EmpresaDto> CrearEmpresa(HttpClient cliente, string nif)
    {
        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Empresas, NuevaEmpresa(nif));
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<EmpresaDto>())!;
    }

    private static async Task<EjercicioDto> CrearEjercicio(HttpClient cliente, Guid empresaId, int anio)
    {
        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Ejercicios, new CrearEjercicioDto
        {
            EmpresaId = empresaId,
            Anio = anio,
            FechaDeInicio = new DateOnly(anio, 1, 1),
            FechaDeFin = new DateOnly(anio, 12, 31),
        });
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<EjercicioDto>())!;
    }

    private static async Task<SerieDto> CrearSerie(HttpClient cliente, string nif, string codigo)
    {
        EmpresaDto empresa = await CrearEmpresa(cliente, nif);
        EjercicioDto ejercicio = await CrearEjercicio(cliente, empresa.Id, 2026);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Series, new CrearSerieDto
        {
            EmpresaId = empresa.Id,
            EjercicioId = ejercicio.Id,
            TipoDeDocumento = "FacturaEmitida",
            Codigo = codigo,
            Formato = "{serie}-{numero:0000}",
        });
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<SerieDto>())!;
    }
}
