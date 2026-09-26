using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Numeracion;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Contrato;

/// <summary>
/// El contrato de <c>/api/v1/organizacion/*</c> tal como lo ve un cliente: por HTTP, contra la
/// API real, contra PostgreSQL de verdad y <b>con credenciales de verdad</b>.
/// </summary>
/// <remarks>
/// <para>
/// Lo que se prueba aquí no lo puede probar un test de dominio ni uno de repositorio: los códigos
/// de estado, el <c>Location</c> de la creación, el <c>ProblemDetails</c> con sus errores por
/// campo, y que el enlace de modelo rechaza lo que tiene que rechazar antes de llegar al caso de
/// uso.
/// </para>
/// <para>
/// Desde 0.5 ninguna de estas rutas es anónima, así que cada test empieza abriendo sesión. Que los
/// veintitantos casos de contrato sigan pasando <b>es</b> la prueba de que cerrar el sistema por
/// defecto no ha cerrado también lo que tenía que seguir abierto para quien lleva el permiso.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ContratoDeOrganizacionTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Empresas = "/api/v1/organizacion/empresas";
    private const string Ejercicios = "/api/v1/organizacion/ejercicios";
    private const string Series = "/api/v1/organizacion/series";
    private const string Almacenes = "/api/v1/organizacion/almacenes";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Crear_una_empresa_devuelve_201_con_Location_que_lleva_al_recurso()
    {
        (HttpClient cliente, SesionDto sesion) = await _api.AbrirComoAdministradorAsync();
        using HttpClient suyo = cliente;

        HttpResponseMessage creacion = await cliente.PostAsJsonAsync(
            Empresas, Escenario.NuevaEmpresa("00000001R"));

        creacion.StatusCode.ShouldBe(HttpStatusCode.Created);

        // El Location no es adorno: tiene que llevar al recurso de verdad. Comprobarlo siguiéndolo
        // es la única manera de saber que la ruta con nombre y la acción de consulta casan; si el
        // `nameof` no cuadrara, CreatedAtAction devolvería un 500 y no un Location roto.
        creacion.Headers.Location.ShouldNotBeNull();

        // En minúsculas, y con el sustantivo en plural del §9. Sin forzarlo, el token
        // `[controller]` copia el nombre de la clase y publica `…/Empresas/…`: el nombre de un
        // tipo de C# asomando por el contrato, y una ruta distinta de la documentada.
        creacion.Headers.Location.AbsolutePath.ShouldStartWith(Empresas + "/");

        // Desde 0.6, «lleva al recurso» quiere decir «lleva al recurso para quien opera DENTRO de
        // esa empresa». El filtro de inquilinato (R8) no hace una excepción con la que uno acaba
        // de crear, así que se entra por donde se entra —pertenencia, rol y empresa activa— y solo
        // entonces se sigue el enlace. Que desde fuera dé 404 es lo que comprueba
        // `ElFiltroDeEmpresaTests`, que es su sitio.
        EmpresaDto creada = (await creacion.Content.ReadFromJsonAsync<EmpresaDto>())!;
        await Escenario.EntrarEnAsync(cliente, sesion.UsuarioId, creada.Id);

        HttpResponseMessage seguimiento = await cliente.GetAsync(creacion.Headers.Location);
        seguimiento.StatusCode.ShouldBe(HttpStatusCode.OK);

        EmpresaDto? empresa = await seguimiento.Content.ReadFromJsonAsync<EmpresaDto>();
        empresa.ShouldNotBeNull();
        empresa.Nif.ShouldBe("00000001R");
        empresa.Id.ShouldBe(creada.Id);
    }

    [Fact]
    public async Task Los_enumerados_viajan_como_texto_y_no_como_numero()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        HttpResponseMessage creacion = await cliente.PostAsJsonAsync(
            Empresas, Escenario.NuevaEmpresa("11111111H"));

        string cuerpo = await creacion.Content.ReadAsStringAsync();

        // Un ordinal es un contrato que se rompe solo con reordenar el enumerado, y quien lo
        // reordena no ve que está rompiendo a nadie.
        cuerpo.ShouldContain("\"regimenDeIva\":\"General\"");

        // `estado` ya no viaja: desapareció del DTO en el 0.10. Un campo que solo puede decir
        // «activa» no informa de nada, porque si la empresa estuviera bloqueada no habría
        // respuesta que mirar —el filtro de R16 la habría tapado antes—.
        cuerpo.ShouldNotContain("\"estado\"");
    }

    [Fact]
    public async Task Un_NIF_con_letra_de_control_incorrecta_es_400_del_campo_nif()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Empresas, Escenario.NuevaEmpresa("00000001A"));

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
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Empresas, new CrearEmpresaDto
        {
            Nif = "00000001A",
            RazonSocial = "Prueba",
            DomicilioFiscal = Escenario.Domicilio(),

            // Una divisa con forma ISO correcta cuyo redondeo no se conoce. Era el yen hasta el
            // 0.15, cuando entró en el catálogo con su caso dorado; el dinar kuwaití sigue fuera.
            DivisaBase = "KWD",
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
        using HttpClient cliente = await _api.ComoAdministradorAsync();

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
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        await cliente.PostAsJsonAsync(Empresas, Escenario.NuevaEmpresa("22222222J"));

        HttpResponseMessage repetida = await cliente.PostAsJsonAsync(
            Empresas, Escenario.NuevaEmpresa("22222222J"));

        repetida.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(repetida)).GetProperty("type").GetString()
            .ShouldBe("/errors/empresa-ya-registrada");
    }

    [Fact]
    public async Task Una_empresa_que_no_existe_es_404_con_ProblemDetails()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        HttpResponseMessage respuesta = await cliente.GetAsync($"{Empresas}/{Guid.NewGuid()}");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await LeerProblema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/empresa-no-encontrada");
    }

    [Fact]
    public async Task Borrar_una_empresa_la_bloquea_pero_no_la_borra()
    {
        // Dentro de la empresa que se va a bloquear: desde fuera, R8 la esconde y el 404 taparía
        // lo que este caso quiere ver, que es el estado en el que queda.
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("33333333P");
        using HttpClient suyo = cliente;

        HttpResponseMessage borrado = await cliente.SuprimirAsync($"{Empresas}/{empresa.Id}");
        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Hasta el 0.9 este GET devolvía la empresa con su estado «Bloqueada». Desde el 0.10
        // devuelve 404, y el cambio ES el ítem: el art. 32 de la LOPDGDD manda bloquear —no
        // destruir—, pero bloquear impide el tratamiento «incluida su visualización». Una
        // respuesta que dijera «existe, y está bloqueada» sería tratamiento.
        //
        // Que la fila SIGUE en PostgreSQL no lo puede demostrar esta puerta, porque el filtro que
        // estamos probando es justo el que la tapa: lo demuestra
        // `LaFilaBloqueadaSigueEnLaBaseTests` leyendo la tabla con SQL en crudo.
        HttpResponseMessage despues = await cliente.GetAsync($"{Empresas}/{empresa.Id}");
        despues.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Una_empresa_bloqueada_se_desbloquea_por_su_puerta_y_vuelve_a_estar_activa()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("00000011B");
        using HttpClient suyo = cliente;

        await cliente.SuprimirAsync($"{Empresas}/{empresa.Id}");

        // En 0.4 desbloquear existía en el dominio y no tenía puerta HTTP: se podía bloquear y no
        // se podía deshacer. La puerta se abre en 0.5, detrás de su propio permiso.
        //
        // Y va SIN `If-Match`, que es lo que cambió en el 0.10: hasta entonces la versión se leía
        // del recurso con un GET previo, y ese GET ahora devuelve 404 porque la empresa está
        // bloqueada. Pedir una versión que no hay forma de conseguir habría dejado esta puerta
        // cerrada para siempre (ADR-0017).
        using HttpResponseMessage desbloqueo = await cliente.EnviarConVersionAsync(
            HttpMethod.Post, $"{Empresas}/{empresa.Id}/desbloqueo", etiqueta: null);

        desbloqueo.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Y aquí está la otra mitad de la prueba de que el bloqueo no borra: lo que el GET no
        // veía hace tres líneas vuelve a estar, entero. Si la supresión hubiera destruido la
        // fila, esto no habría nada que desbloquear.
        EmpresaDto? despues = await cliente.GetFromJsonAsync<EmpresaDto>($"{Empresas}/{empresa.Id}");
        despues.ShouldNotBeNull();
        despues.Id.ShouldBe(empresa.Id);
        despues.Nif.ShouldBe("00000011B");
    }

    [Fact]
    public async Task Una_empresa_bloqueada_no_se_puede_modificar_y_da_404()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("44444444A");
        using HttpClient suyo = cliente;

        await cliente.SuprimirAsync($"{Empresas}/{empresa.Id}");

        // La versión se cita a mano: `ModificarAsync` la leería con un GET previo, y ese GET ya
        // no trae nada. Con cualquier `If-Match` vale, porque el 404 llega antes de mirarlo.
        using HttpResponseMessage respuesta = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            $"{Empresas}/{empresa.Id}",
            "\"1\"",
            JsonContent.Create(new ModificarEmpresaDto
            {
                RazonSocial = "Otro nombre",
                DomicilioFiscal = Escenario.Domicilio(),
                DivisaBase = "EUR",
                RegimenDeIva = "General",
            }));

        // Hasta el 0.9 esto era un 409 «empresa-bloqueada». Ahora es un 404, y por lo mismo que
        // el GET: el caso de uso pide la empresa al repositorio, el filtro de R16 no se la da, y
        // lo que responde es «no hay ninguna empresa con ese identificador». Un 409 explicando
        // que está bloqueada revelaría a la vez que existe y en qué estado está.
        //
        // No queda ninguna rama que produzca aquel 409: por eso el código de error
        // `empresa-bloqueada` se ha borrado del catálogo en vez de quedarse sin quien lo emita.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await LeerProblema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/empresa-no-encontrada");
    }

    [Fact]
    public async Task Un_ejercicio_de_mas_de_doce_meses_es_400_del_campo_fechaDeFin()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("55555555K");
        using HttpClient suyo = cliente;

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Ejercicios, new CrearEjercicioDto
        {
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
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("66666666Q");
        using HttpClient suyo = cliente;

        HttpResponseMessage creacion = await cliente.PostAsJsonAsync(Ejercicios, new CrearEjercicioDto
        {
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
    public async Task El_ejercicio_se_cuelga_de_la_empresa_del_token_y_no_de_la_del_cuerpo()
    {
        (HttpClient cliente, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync("00000009D");
        using HttpClient suyo = cliente;
        EmpresaDto ajena = await Escenario.CrearEmpresaAsync(cliente, "00000010X");

        // `CrearEjercicioDto` ya no tiene campo de empresa, así que el cuerpo se manda a mano: es
        // la única forma de comprobar qué pasa con un `empresaId` que un cliente añada por su
        // cuenta. La respuesta tiene que ser «nada».
        using StringContent cuerpo = new(
            $"{{\"anio\":2028,\"fechaDeInicio\":\"2028-01-01\",\"fechaDeFin\":\"2028-12-31\"," +
            $"\"empresaId\":\"{ajena.Id}\"}}",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage creacion = await cliente.PostAsync(Ejercicios, cuerpo);

        creacion.StatusCode.ShouldBe(HttpStatusCode.Created);

        EjercicioDto? ejercicio = await creacion.Content.ReadFromJsonAsync<EjercicioDto>();
        ejercicio.ShouldNotBeNull();
        ejercicio.EmpresaId.ShouldBe(propia.Id);
        ejercicio.EmpresaId.ShouldNotBe(ajena.Id);
    }

    [Fact]
    public async Task Con_la_empresa_activa_bloqueada_no_se_puede_crear_nada_y_es_409()
    {
        (HttpClient cliente, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync("00000012N");
        using HttpClient suyo = cliente;

        // El token sigue siendo válido y sigue llevando esta empresa dentro. Lo que ha cambiado es
        // la empresa, y sin esta comprobación el ejercicio se crearía colgando de una empresa que
        // ya no opera —una fila que nadie sabría de dónde ha salido—.
        (await cliente.SuprimirAsync($"{Empresas}/{propia.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Ejercicios, new CrearEjercicioDto
        {
            Anio = 2029,
            FechaDeInicio = new DateOnly(2029, 1, 1),
            FechaDeFin = new DateOnly(2029, 12, 31),
        });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/empresa-activa-no-operativa");
    }

    /// <summary>
    /// Dos ejercicios de la misma empresa no pueden pisarse, <b>aunque se llamen distinto</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los años son distintos a propósito</b>, y es lo que da sentido al caso: el único
    /// <c>(empresa_id, anio)</c> que ya existía mira la etiqueta, y para él «2026» y «2027» son
    /// dos ejercicios que no se parecen en nada. Con solo ese índice, este alta entraba. Y
    /// entonces cualquier día del segundo semestre de 2026 caía en <b>dos</b> ejercicios, y «el
    /// ejercicio de esta operación» dejaba de tener una sola respuesta.
    /// </para>
    /// <para>
    /// Es el ejercicio partido de verdad —julio a junio, que la ley admite—, no un caso
    /// inventado: por eso el hueco existía y por eso no se veía.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Dos_ejercicios_de_la_misma_empresa_no_pueden_pisarse_aunque_se_llamen_distinto()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000094W");
        using HttpClient suyo = cliente;

        await CrearEjercicio(cliente, 2026);

        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            Ejercicios,
            new CrearEjercicioDto
            {
                Anio = 2027,
                FechaDeInicio = new DateOnly(2026, 7, 1),
                FechaDeFin = new DateOnly(2027, 6, 30),
            });

        respuesta.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "han entrado dos ejercicios que comparten seis meses. El `type` tiene que decir que " +
            "es un solape y no un duplicado: se arreglan distinto —uno cambiando el año, el otro " +
            "moviendo las fechas—");

        (await LeerProblema(respuesta)).GetProperty("type").GetString()
            .ShouldBe("/errors/ejercicio-solapado");
    }

    /// <summary>
    /// Mover un ejercicio encima de otro es 409; <b>dejarlo donde está, no</b>.
    /// </summary>
    /// <remarks>
    /// La segunda mitad es la que prueba el <c>excepto</c> de <c>HaySolapeAsync</c>. Sin él, un
    /// ejercicio se encontraría solapado <b>consigo mismo</b> y guardar sin mover las fechas sería
    /// imposible: el 409 más absurdo que se puede dar, y uno que la primera mitad sola no
    /// distingue de un acierto.
    /// </remarks>
    [Fact]
    public async Task Mover_un_ejercicio_encima_de_otro_es_409_y_dejarlo_donde_esta_no_lo_es()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000095A");
        using HttpClient suyo = cliente;

        await CrearEjercicio(cliente, 2026);
        EjercicioDto siguiente = await CrearEjercicio(cliente, 2027);

        string recurso = $"{Ejercicios}/{siguiente.Id}";

        using HttpResponseMessage encima = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            recurso,
            await cliente.EtiquetaDeAsync(recurso),
            JsonContent.Create(new ModificarEjercicioDto
            {
                FechaDeInicio = new DateOnly(2026, 7, 1),
                FechaDeFin = new DateOnly(2027, 6, 30),
            }));

        encima.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(encima)).GetProperty("type").GetString()
            .ShouldBe("/errors/ejercicio-solapado");

        // Y ahora las MISMAS fechas que ya tiene. Si esto saliera 409, la comprobación estaría
        // contando al propio ejercicio entre los que le estorban.
        using HttpResponseMessage quieto = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            recurso,
            await cliente.EtiquetaDeAsync(recurso),
            JsonContent.Create(new ModificarEjercicioDto
            {
                FechaDeInicio = siguiente.FechaDeInicio,
                FechaDeFin = siguiente.FechaDeFin,
            }));

        quieto.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "guardar un ejercicio sin moverlo se ha encontrado solapado consigo mismo");
    }

    /// <summary>
    /// El solape lo impide <b>la base</b>, y no solo la comprobación previa del caso de uso.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se escribe con SQL desnudo a propósito. Por el caso de uso, quien contesta es
    /// <c>HaySolapeAsync</c>, y esa comprobación no cubre <b>dos peticiones a la vez</b>: las dos
    /// preguntan antes de que ninguna escriba, las dos se van satisfechas y el solape entra. Lo
    /// que este caso afirma es que por debajo hay una restricción que no depende de que nadie se
    /// acuerde de preguntar, y que es de <b>exclusión</b>: un índice único no sabe decir que lo
    /// que no se repite es un RANGO.
    /// </para>
    /// <para>
    /// La segunda mitad —la otra empresa— es la que se cae si alguien quita <c>empresa_id WITH =</c>
    /// de la restricción: el ejercicio de una ferretería impediría a la imprenta de al lado abrir
    /// el suyo, con un rechazo que habla de una fila que no puede ni ver (R8).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_solape_lo_impide_la_BASE_y_no_solo_la_comprobacion_previa()
    {
        (HttpClient cliente, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync("00000096G");
        using HttpClient suyo = cliente;
        (HttpClient otro, EmpresaDto ajena) = await _api.EnUnaEmpresaNuevaAsync("00000097M");
        otro.Dispose();

        await InsertarEjercicioAsync(propia.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        PostgresException choque = await Should.ThrowAsync<PostgresException>(
            () => InsertarEjercicioAsync(
                propia.Id, 2027, new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30)));

        choque.SqlState.ShouldBe(
            PostgresErrorCodes.ExclusionViolation,
            "la base ha dejado entrar dos ejercicios de la misma empresa que se pisan. El " +
            "síntoma no sería un error: sería un cierre que congela medio año de otro ejercicio");

        choque.ConstraintName.ShouldBe("ejercicios_sin_intervalos_solapados");

        // La otra empresa NO estorba, con el intervalo IDÉNTICO al primero.
        await InsertarEjercicioAsync(ajena.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
    }

    /// <summary>El día en que un ejercicio <b>acaba</b> todavía cuenta para el solape.</summary>
    /// <remarks>
    /// La convención <c>'[]'</c> —cerrada por los dos lados— escrita como un caso. Con el
    /// <c>'[)'</c> por omisión, estos dos ejercicios convivirían y el 31 de diciembre pertenecería
    /// a los dos; y como <c>Ejercicio.Comprende</c> sí incluye el último día, la base y el dominio
    /// dirían cosas distintas de ese día. El solape que se cuela es peor que el que se rechaza,
    /// porque nadie lo ve hasta que hay que cerrar.
    /// </remarks>
    [Fact]
    public async Task El_dia_en_que_un_ejercicio_ACABA_todavia_cuenta_para_el_solape()
    {
        (HttpClient cliente, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync("00000098Y");
        using HttpClient suyo = cliente;
        DateOnly frontera = new(2026, 12, 31);

        await InsertarEjercicioAsync(propia.Id, 2026, new DateOnly(2026, 1, 1), frontera);

        PostgresException choque = await Should.ThrowAsync<PostgresException>(
            () => InsertarEjercicioAsync(propia.Id, 2027, frontera, new DateOnly(2027, 12, 30)));

        choque.SqlState.ShouldBe(
            PostgresErrorCodes.ExclusionViolation,
            "el último día de un ejercicio y el primero del siguiente son el mismo y han entrado " +
            "los dos. El rango de la restricción no está cerrado por arriba");

        // Y el día SIGUIENTE sí entra: sin esta mitad, una restricción que rechazara todo pasaría
        // la aserción de arriba y nadie podría encadenar dos ejercicios consecutivos.
        await InsertarEjercicioAsync(
            propia.Id, 2027, frontera.AddDays(1), new DateOnly(2027, 12, 31));
    }

    [Fact]
    public async Task Un_ejercicio_se_cierra_y_se_reabre_por_sus_puertas()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000013J");
        using HttpClient suyo = cliente;
        EjercicioDto ejercicio = await CrearEjercicio(cliente, 2026);

        // El cierre es POST sobre `…/cierre`, que es el sub-recurso: el estado es algo que se
        // crea, no un verbo inventado colgando del ejercicio.
        (await cliente.AccionarAsync(
            $"{Ejercicios}/{ejercicio.Id}",
            $"{Ejercicios}/{ejercicio.Id}/cierre",
            HttpMethod.Post)).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        EjercicioDto? cerrado = await cliente.GetFromJsonAsync<EjercicioDto>($"{Ejercicios}/{ejercicio.Id}");
        cerrado.ShouldNotBeNull();
        cerrado.Estado.ShouldBe("Cerrado");

        // Y la reapertura NO es borrar ese sub-recurso, que es como estaba hasta el 2.6: tiene
        // ruta propia porque lleva cuerpo —el motivo—, y un `DELETE` con cuerpo no es fiable.
        (await cliente.AccionarAsync(
            $"{Ejercicios}/{ejercicio.Id}",
            $"{Ejercicios}/{ejercicio.Id}/reapertura",
            HttpMethod.Post,
            JsonContent.Create(new ReabrirEjercicioDto("Subsanación de un asiento mal fechado"))))
            .StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        EjercicioDto? reabierto = await cliente.GetFromJsonAsync<EjercicioDto>($"{Ejercicios}/{ejercicio.Id}");
        reabierto.ShouldNotBeNull();
        reabierto.Estado.ShouldBe("Abierto");
    }

    /// <summary>
    /// Cerrar lo ya cerrado y reabrir lo ya abierto son <b>409</b>, no un 204 mudo.
    /// </summary>
    /// <remarks>
    /// Las dos operaciones dejaron de ser idempotentes en el ítem 2.6, y el caso que eso destapa
    /// es el que importa: que otro se adelantó. Con un 204, quien pide el cierre se lleva la misma
    /// respuesta tanto si lo cerró él como si se lo encontró cerrado por alguien que además pudo
    /// reabrirlo y volver a cerrarlo por en medio. Los dos <c>type</c> son distintos a propósito:
    /// se arreglan distinto.
    /// </remarks>
    [Fact]
    public async Task Cerrar_lo_ya_cerrado_y_reabrir_lo_ya_abierto_son_409_y_no_un_204_mudo()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000099F");
        using HttpClient suyo = cliente;
        EjercicioDto ejercicio = await CrearEjercicio(cliente, 2031);

        string recurso = $"{Ejercicios}/{ejercicio.Id}";

        // Reabrir uno que nunca se cerró: el estado no cambia, así que no hay nada que auditar.
        // Va con un motivo válido a propósito: lo que se comprueba aquí es el estado, y con el
        // motivo vacío el 400 llegaría antes y este caso no diría nada del 409.
        using HttpResponseMessage reapertura = await cliente.AccionarAsync(
            recurso,
            $"{recurso}/reapertura",
            HttpMethod.Post,
            JsonContent.Create(new ReabrirEjercicioDto("Da igual: nunca estuvo cerrado")));

        reapertura.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await LeerProblema(reapertura)).GetProperty("type").GetString()
            .ShouldBe("/errors/ejercicio-ya-abierto");

        (await cliente.AccionarAsync(recurso, $"{recurso}/cierre", HttpMethod.Post)).StatusCode
            .ShouldBe(
                HttpStatusCode.NoContent,
                "el primer cierre sí cierra, o el 409 de abajo no diría nada del segundo");

        using HttpResponseMessage segundo = await cliente.AccionarAsync(
            recurso, $"{recurso}/cierre", HttpMethod.Post);

        segundo.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await LeerProblema(segundo)).GetProperty("type").GetString()
            .ShouldBe("/errors/ejercicio-ya-cerrado");
    }

    [Fact]
    public async Task Suprimir_una_serie_que_no_ha_numerado_es_204()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("77777777B");
        using HttpClient suyo = cliente;
        SerieDto serie = await CrearSerie(cliente, "FAC");

        HttpResponseMessage borrado = await cliente.SuprimirAsync($"{Series}/{serie.Id}");

        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await cliente.GetAsync($"{Series}/{serie.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Suprimir_una_serie_que_ya_ha_numerado_es_409()
    {
        (HttpClient cliente, EmpresaDto empresa) = await _api.EnUnaEmpresaNuevaAsync("88888888Y");
        using HttpClient suyo = cliente;
        SerieDto serie = await CrearSerie(cliente, "FAC");

        // Numerar NO se hace a mano: desde el ítem 2.4 no hay forma de subir un contador desde
        // C# -`Serie.RegistrarNumeroAsignado` se borró-, así que se numera con el mecanismo de
        // verdad, con su sentencia y su transacción. Subir la columna con un UPDATE a mano
        // probaría un estado que el sistema no sabe producir.
        await using (OrganizacionDbContext contexto = postgres.AbrirOrganizacion(empresa.Id))
        {
            // Con el tipo y una fecha del ejercicio con que `CrearSerie` la monta: la sentencia los
            // mira desde el ADR-0043, y con otros no numeraría.
            Resultado<long> numero = await NumeradorDePruebas.NumerarAsync(
                contexto, empresa.Id, serie.Id, TipoDeDocumento.FacturaEmitida, new DateOnly(2026, 6, 15));

            numero.EsCorrecto.ShouldBeTrue();
            numero.Valor.ShouldBe(1);
        }

        HttpResponseMessage borrado = await cliente.SuprimirAsync($"{Series}/{serie.Id}");

        borrado.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(borrado)).GetProperty("type").GetString()
            .ShouldBe("/errors/serie-ya-numerada");
    }

    [Fact]
    public async Task Una_serie_colgada_del_ejercicio_de_otra_empresa_es_400_del_campo_ejercicioId()
    {
        (HttpClient cliente, EmpresaDto propia) = await _api.EnUnaEmpresaNuevaAsync("00000008P");
        using HttpClient suyo = cliente;

        // El ejercicio ajeno se crea DENTRO de la otra empresa, que es la única manera de crearlo
        // ahora, y luego se vuelve a la propia. Así el identificador que se cuela en el cuerpo es
        // uno de verdad: con un Guid inventado, el 400 lo daría el «no existe» y no la frontera.
        EmpresaDto ajena = await Escenario.CrearEmpresaAsync(cliente, "A99999997");
        Guid usuarioId = await UsuarioActualAsync(cliente);

        await Escenario.EntrarEnAsync(cliente, usuarioId, ajena.Id);
        EjercicioDto deLaAjena = await CrearEjercicio(cliente, 2026);
        await Escenario.EntrarEnAsync(cliente, usuarioId, propia.Id);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Series, new CrearSerieDto
        {
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
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000002W");
        using HttpClient suyo = cliente;
        EjercicioDto ejercicio = await CrearEjercicio(cliente, 2026);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Series, new CrearSerieDto
        {
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
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000003A");
        using HttpClient suyo = cliente;

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Almacenes, new CrearAlmacenDto
        {
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
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000004G");
        using HttpClient suyo = cliente;

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Almacenes, new CrearAlmacenDto
        {
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
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync("00000005M");
        using HttpClient suyo = cliente;

        await cliente.PostAsJsonAsync(Almacenes, NuevoAlmacen("CENTRAL"));
        HttpResponseMessage repetido = await cliente.PostAsJsonAsync(Almacenes, NuevoAlmacen("  central  "));

        // Preguntando por lo que escribió el usuario, «central» habría pasado el filtro y habría
        // chocado contra el índice único: un 500 en lugar de este 409.
        repetido.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LeerProblema(repetido)).GetProperty("type").GetString()
            .ShouldBe("/errors/almacen-duplicado");
    }

    [Fact]
    public async Task La_direccion_va_y_vuelve_en_los_seis_campos_de_R17()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        EmpresaDto empresa = await Escenario.CrearEmpresaAsync(cliente, "00000006Y");

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
        using HttpClient cliente = await _api.ComoAdministradorAsync();

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
        using HttpClient cliente = await _api.ComoAdministradorAsync();

        HttpResponseMessage respuesta = await cliente.GetAsync($"{Empresas}?page=1&size=100000");

        // El tope lo aplica el enlace de modelo, que es lo único que se ejecuta de verdad: un
        // objeto de paginación construido a mano en el controlador se saltaría la validación
        // entera y el tope no existiría.
        respuesta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static CrearAlmacenDto NuevoAlmacen(string codigo) => new()
    {
        Codigo = codigo,
        Nombre = "Almacén",
        Direccion = Escenario.Domicilio(),
        Tipo = "Fisico",
    };

    /// <summary>Mete un ejercicio SIN pasar por el caso de uso, para llegar a la restricción.</summary>
    /// <remarks>
    /// La empresa tiene que existir de verdad: <c>ejercicios.empresa_id</c> lleva clave ajena
    /// dentro del esquema, así que un identificador inventado moriría en la clave ajena y el caso
    /// diría que la restricción funciona cuando lo que habría funcionado es otra cosa.
    /// </remarks>
    private async Task InsertarEjercicioAsync(Guid empresaId, int anio, DateOnly inicio, DateOnly fin)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand insercion = new(
            """
            INSERT INTO organizacion.ejercicios
                (id, empresa_id, anio, fecha_de_inicio, fecha_de_fin, estado, creado_en, modificado_en)
            VALUES (@id, @empresa, @anio, @inicio, @fin, 'Abierto', now(), now());
            """,
            conexion);

        insercion.Parameters.AddWithValue("id", Guid.NewGuid());
        insercion.Parameters.AddWithValue("empresa", empresaId);
        insercion.Parameters.AddWithValue("anio", anio);
        insercion.Parameters.AddWithValue("inicio", inicio);
        insercion.Parameters.AddWithValue("fin", fin);

        await insercion.ExecuteNonQueryAsync();
    }

    private static async Task<JsonElement> LeerProblema(HttpResponseMessage respuesta)
    {
        respuesta.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> UsuarioActualAsync(HttpClient cliente)
    {
        SesionDto sesion = await Sesiones.AbrirAsync(
            cliente, ApiDeVerdad.CorreoDelAdministrador, ApiDeVerdad.ContrasenaDelAdministrador);

        return sesion.UsuarioId;
    }

    private static async Task<EjercicioDto> CrearEjercicio(HttpClient cliente, int anio)
    {
        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Ejercicios, new CrearEjercicioDto
        {
            Anio = anio,
            FechaDeInicio = new DateOnly(anio, 1, 1),
            FechaDeFin = new DateOnly(anio, 12, 31),
        });
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<EjercicioDto>())!;
    }

    private static async Task<SerieDto> CrearSerie(HttpClient cliente, string codigo)
    {
        EjercicioDto ejercicio = await CrearEjercicio(cliente, 2026);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(Series, new CrearSerieDto
        {
            EjercicioId = ejercicio.Id,
            TipoDeDocumento = "FacturaEmitida",
            Codigo = codigo,
            Formato = "{serie}-{numero:0000}",
        });
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<SerieDto>())!;
    }
}
