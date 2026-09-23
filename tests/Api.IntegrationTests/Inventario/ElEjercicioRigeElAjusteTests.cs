using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Cruces;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La R9 sobre un documento: la fecha de operación decide el ejercicio, y el ejercicio decide si
/// el documento se puede hacer definitivo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Quién es la guarda y quién la cortesía.</b> La pregunta que el <i>cierre</i> le hace a cada
/// módulo —«¿te quedan borradores dentro?»— es la <b>cortesía</b>: avisa a quien cierra de que va a
/// dejar papeles colgando, y se puede saltar sin que nada se rompa, porque esos papeles siguen
/// siendo borradores. Lo de este fichero es la <b>guarda</b>: lo que impide que un documento se
/// haga definitivo en un periodo que ya lo era. Sin la cortesía alguien se lleva un susto; sin la
/// guarda, el libro deja de cuadrar con lo presentado, y eso no se arregla después.
/// </para>
/// <para>
/// <b>Una sola fecha por documento, y por eso no hay documentos a caballo.</b> Un ajuste lleva
/// <c>FechaDeOperacion</c> y nada más —ni un «desde» ni un «hasta»—, así que cae entero en un
/// ejercicio o en ninguno. El caso que lo intenta no parte el documento: abre el borrador con el
/// ejercicio abierto, lo cierra por debajo y confirma. No queda medio dentro: no queda nada.
/// </para>
/// <para>
/// <b>Los dos casos del cerrojo usan dos transacciones DE VERDAD, y un plazo.</b> Lo que hay que
/// ejercer no es «cerrar después de confirmar» sino el <b>solape</b>: una operación que ya leyó el
/// ejercicio y todavía no ha llegado a su <c>COMMIT</c>. Quien llega segundo se queda esperando, y
/// esperar para siempre es un caso que no termina en vez de un caso que falla; así que el segundo
/// lleva <c>SET LOCAL lock_timeout</c> y el desenlace que se afirma es el <c>55P03</c> de
/// PostgreSQL. Es determinista: o consigue el cerrojo o da ese error, sin depender de lo rápida que
/// sea la máquina.
/// </para>
/// <para>
/// <b>Semillas: las empresas van del 384 al 389 y los maestros de instalación del 390 al 395.</b>
/// Este carril comparte la base entre todos sus ficheros, así que una semilla repetida no falla
/// aquí: falla en el fichero de otro que la pedía primero.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElEjercicioRigeElAjusteTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    [CubreEstadoDelPuerto(
        typeof(IConsultaDeEjercicios), EstadoDelEjercicioParaEscribir.Abierto)]
    public async Task Con_el_ejercicio_abierto_la_confirmacion_pasa_y_el_documento_queda_numerado()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(384);
        (AbrirAjusteDto peticion, _) = await UnAjusteCompletoAsync(cliente, "EJE-A", 390, Hoy);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);

        confirmacion.EsCorrecto.ShouldBeTrue(
            $"la fecha cae en el ejercicio del año en curso, que está abierto. Contestó " +
            $"«{confirmacion.Error?.Codigo}»");

        confirmacion.Valor.Numero.ShouldBe(1);
    }

    [Fact]
    [CubreEstadoDelPuerto(
        typeof(IConsultaDeEjercicios), EstadoDelEjercicioParaEscribir.Cerrado)]
    public async Task Cerrado_el_ejercicio_el_borrador_ya_no_se_confirma_y_no_queda_nada_a_medias()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(385);

        (AbrirAjusteDto peticion, SerieDto serie, EjercicioDto ejercicio) =
            await UnAjusteCompletoConSuEjercicioAsync(cliente, "EJE-B", 391, Hoy);

        // EL CIERRE VA PRIMERO, Y ESTE ORDEN ES EL CASO. El barrido de borradores —la cortesía— se
        // hace AL CERRAR: mira los que ya existen y avisa. Un borrador que nace DESPUÉS del cierre
        // no lo ve ninguna cortesía, porque cuando se preguntó no estaba. Esto no es un agujero de
        // la cortesía: es la prueba de que la cortesía no puede ser la guarda. Quien para esto es
        // la comprobación del ejercicio al confirmar, y no hay otra.
        //
        // La serie NO se cierra con su ejercicio, así que el borrador nace sin ningún problema. Es
        // el intento de dejar un documento a caballo del cierre, por el único hueco por el que se
        // puede intentar.
        await CerrarPorLaApiAsync(cliente, ejercicio.Id);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue(
            $"el BORRADOR nace aunque el ejercicio esté cerrado: el alta mira la SERIE, que sigue " +
            $"activa, y la R9 protege lo definitivo, no lo que todavía se puede tirar. Contestó " +
            $"«{alta.Error?.Codigo}»");

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);

        confirmacion.EsCorrecto.ShouldBeFalse(
            "el periodo ya es definitivo: un documento nuevo dentro cambiaría lo presentado");

        confirmacion.Error!.Codigo.ShouldBe("ajuste-en-ejercicio-cerrado");

        // Y NO QUEDA NADA A MEDIAS, leído de la base y no de lo que devolvió el caso de uso. Es la
        // mitad que dice que el documento no se ha partido: sigue siendo un borrador entero, sin
        // número, confirmable el día que alguien reabra el ejercicio con su motivo.
        await using (InventarioDbContext inventario = postgres.AbrirInventario(empresa.Id))
        {
            Ajuste comoQuedo = await inventario.Ajustes.SingleAsync(fila => fila.Id == alta.Valor.Id);

            comoQuedo.Estado.ShouldBe(EstadoDeAjuste.Borrador);
            comoQuedo.Numero.ShouldBeNull();

            (await inventario.Movimientos.AnyAsync(fila => fila.DocumentoOrigenId == alta.Valor.Id))
                .ShouldBeFalse("ni una fila del libro: la transacción se deshizo entera");
        }

        SerieDto? despues = await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serie.Id}");

        despues!.Contador.ShouldBe(
            0,
            "la guarda va ANTES del numerador a propósito: un periodo que no admite el documento " +
            "no debe gastar un correlativo que luego haría hueco");
    }

    [Fact]
    [CubreEstadoDelPuerto(
        typeof(IConsultaDeEjercicios), EstadoDelEjercicioParaEscribir.SinEjercicio)]
    public async Task Una_fecha_fuera_de_todo_ejercicio_no_se_confirma_y_lo_dice_con_otro_codigo()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(386);

        // UNA FECHA DE UN AÑO QUE NADIE HA ABIERTO. No hay que borrar ningún ejercicio ni
        // inventar ningún estado: basta con pedir el documento para un año del que esta empresa no
        // tiene ejercicio, que es exactamente lo que pasa cada 1 de enero hasta que alguien abre el
        // año. La SERIE sigue colgando del ejercicio en curso, que es lo normal, y por eso este
        // caso no se lo lleva el numerador: la guarda va antes.
        (AbrirAjusteDto peticion, _) = await UnAjusteCompletoAsync(
            cliente, "EJE-C", 392, new DateOnly(Hoy.Year - 9, 6, 15), Hoy.Year);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue(
            $"el BORRADOR sí nace: el ejercicio se mira al confirmar, no al abrir, porque lo que " +
            $"la R9 protege es lo definitivo. Contestó «{alta.Error?.Codigo}»");

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);

        confirmacion.EsCorrecto.ShouldBeFalse(
            "sin ejercicio no hay periodo al que imputar el documento, y un documento definitivo " +
            "que ninguna autoliquidación recoge es peor que un documento rechazado");

        confirmacion.Error!.Codigo.ShouldBe(
            "ajuste-sin-ejercicio",
            "es un código DISTINTO del ejercicio cerrado porque se arreglan de maneras distintas: " +
            "éste abriendo el ejercicio que falta, el otro reabriéndolo o moviendo el documento");
    }

    [Fact]
    public async Task Anular_un_ajuste_de_un_ejercicio_cerrado_deja_el_inverso_en_el_abierto()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(387);

        // El documento original vive en el año pasado, que se abre, se usa y se cierra. El de este
        // año se abre después: es donde tiene que caer el inverso.
        var elAnioPasado = new DateOnly(Hoy.Year - 1, 6, 15);

        (AbrirAjusteDto peticion, _, EjercicioDto viejo) =
            await UnAjusteCompletoConSuEjercicioAsync(cliente, "EJE-D", 393, elAnioPasado);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);
        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

        await CrearEjercicioAsync(cliente, Hoy.Year);
        await CerrarPorLaApiAsync(cliente, viejo.Id);

        Resultado<AnulacionDto> anulacion =
            await modulo.AnularAsync(alta.Valor.Id, "Duplicado detectado en la revisión");

        // LA ANULACIÓN FUNCIONA, y esto es lo que la fecha de hoy compra. Con el inverso heredando
        // la fecha del original, anular un documento de un periodo cerrado sería escribir dentro de
        // él: la R2 dejaría de poder aplicarse justo a los documentos por los que existe —los
        // viejos, los que ya nadie puede corregir de otra manera—.
        anulacion.EsCorrecto.ShouldBeTrue(
            $"el inverso nace con la fecha de HOY, que cae en el ejercicio abierto. Contestó " +
            $"«{anulacion.Error?.Codigo}»");

        anulacion.Valor.Inverso.FechaDeOperacion.ShouldBe(Hoy);
        anulacion.Valor.Original.FechaDeOperacion.ShouldBe(elAnioPasado);

        // Y EL CERRADO NO SE TOCA: el original sigue con su fecha y sus movimientos donde estaban.
        // Anular no borra ni revierte, añade (R3), así que el saldo a una fecha anterior a la
        // anulación sigue enseñando lo que el original movió, porque así fue.
        await using InventarioDbContext inventario = postgres.AbrirInventario(empresa.Id);

        Ajuste original = await inventario.Ajustes.SingleAsync(fila => fila.Id == alta.Valor.Id);

        original.FechaDeOperacion.ShouldBe(elAnioPasado);
        original.Estado.ShouldBe(EstadoDeAjuste.Anulado);

        (await inventario.Movimientos.CountAsync(fila => fila.DocumentoOrigenId == alta.Valor.Id))
            .ShouldBeGreaterThan(0, "las filas que el original escribió siguen ahí (R3)");
    }

    [Fact]
    public async Task El_cierre_espera_a_la_confirmacion_que_ya_estaba_dentro()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(388);

        (AbrirAjusteDto peticion, _, EjercicioDto ejercicio) =
            await UnAjusteCompletoConSuEjercicioAsync(cliente, "EJE-E", 394, Hoy);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        // TRANSACCIÓN 1: confirma de verdad y NO suelta. El cerrojo compartido sobre la fila del
        // ejercicio sigue puesto.
        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await modulo.ConfirmarYQuedarseDentroAsync(alta.Valor.Id);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            // TRANSACCIÓN 2: el cierre pide el exclusivo, que no convive con el compartido de
            // arriba. Con plazo, para que el caso falle en vez de colgarse.
            PostgresException choque = await ElChoqueDeCerrojoAsync(
                () => ElCerrojoDeCerrarAsync(empresa.Id, ejercicio.Id));

            choque.SqlState.ShouldBe(
                PostgresErrorCodes.LockNotAvailable,
                "el cierre no ha podido tomar el exclusivo porque la confirmación de arriba " +
                "todavía no ha llegado a su COMMIT: eso es «se serializa», y sin el FOR SHARE " +
                "del lado de Inventario este cierre habría pasado por encima");

            await enVuelo.CommitAsync();
        }

        // Y SOLTADO EL COMPARTIDO, EL CIERRE PASA. Sin esta mitad, un cierre roto por cualquier
        // otro motivo daría el mismo rojo de arriba y el caso no distinguiría «espera» de «no
        // funciona».
        using HttpResponseMessage cierre = await CerrarAsync(cliente, ejercicio.Id);

        cierre.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cierre));
    }

    [Fact]
    public async Task La_confirmacion_espera_al_cierre_que_ya_estaba_dentro_y_luego_lo_obedece()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(389);

        (AbrirAjusteDto peticion, _, EjercicioDto ejercicio) =
            await UnAjusteCompletoConSuEjercicioAsync(cliente, "EJE-F", 395, Hoy);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        // TRANSACCIÓN 1: el cierre toma el exclusivo y no suelta.
        await using OrganizacionDbContext organizacion = postgres.AbrirOrganizacion(empresa.Id);
        await using IDbContextTransaction cerrando = await organizacion.Database.BeginTransactionAsync();

        CerrojoDeEjercicios cerrojo = new(organizacion, new InquilinoFijo(empresa.Id));

        (await cerrojo.TomarEnExclusivaAsync(ejercicio.Id, CancellationToken.None))
            .ShouldBe(EstadoDeEjercicio.Abierto);

        // TRANSACCIÓN 2: la confirmación pide el compartido y se lo encuentra ocupado.
        await using (IDbContextTransaction confirmando = await modulo.AbrirTransaccionAsync())
        {
            await modulo.PonerPlazoCortoDeCerrojoAsync();

            PostgresException choque = await ElChoqueDeCerrojoAsync(
                () => modulo.ConfirmarSinAbrirTransaccionAsync(alta.Valor.Id));

            choque.SqlState.ShouldBe(
                PostgresErrorCodes.LockNotAvailable,
                "la confirmación no lee el ejercicio mientras haya un cierre a medias, que es " +
                "justo lo que impide decidir sobre un estado que está a punto de cambiar");

            await confirmando.RollbackAsync();
        }
    }

    /// <summary>La fecha de hoy, en el mismo calendario que usa el caso de uso.</summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>El choque contra el cerrojo, sacado de donde EF Core lo deja.</summary>
    /// <remarks>
    /// <b>No llega tal cual, y eso es una decisión del sistema que conviene ver escrita.</b> La
    /// estrategia de reintentos de EF Core clasifica el <c>55P03</c> como <i>fallo transitorio</i>
    /// —lo es: quien no consiguió el cerrojo puede volver a intentarlo—, así que lo reintenta y,
    /// cuando se le acaban los intentos, lo envuelve en un <c>InvalidOperationException</c>. Que la
    /// espera por cerrojo se reintente sola es el comportamiento correcto en producción: el que
    /// vuelve lee el estado otra vez y, si el cierre ganó, se encuentra «Cerrado» y lo obedece. Aquí
    /// se desenvuelve para poder afirmar el código de PostgreSQL, que es el hecho que importa.
    /// </remarks>
    /// <param name="loQuePierde">La operación que llega segunda.</param>
    /// <returns>La excepción de PostgreSQL que hay dentro.</returns>
    private static async Task<PostgresException> ElChoqueDeCerrojoAsync(Func<Task> loQuePierde)
    {
        Exception crudo = await Should.ThrowAsync<Exception>(loQuePierde);

        for (Exception? mirando = crudo; mirando is not null; mirando = mirando.InnerException)
        {
            if (mirando is PostgresException dePostgres)
            {
                return dePostgres;
            }
        }

        throw new ShouldAssertException(
            "se esperaba un choque de cerrojo de PostgreSQL y no hay ninguno en la cadena: " +
            crudo);
    }

    /// <summary>
    /// El cerrojo del cierre pedido a pelo, con su propia transacción y su propio plazo.
    /// </summary>
    /// <remarks>
    /// Se pide el <b>cerrojo</b> y no el caso de uso entero porque lo que se afirma es el choque,
    /// y el choque ocurre en la primera orden que el caso de uso ejecuta: <c>CerrarEjercicio</c>
    /// llama a esto antes de nada, a propósito.
    /// </remarks>
    /// <param name="empresaId">La empresa del caso (R8).</param>
    /// <param name="ejercicioId">El ejercicio que se intenta cerrar.</param>
    private async Task ElCerrojoDeCerrarAsync(Guid empresaId, Guid ejercicioId)
    {
        await using OrganizacionDbContext organizacion = postgres.AbrirOrganizacion(empresaId);
        await using IDbContextTransaction transaccion =
            await organizacion.Database.BeginTransactionAsync();

        await organizacion.Database.ExecuteSqlRawAsync(ElModuloDeInventario.PlazoCorto);

        CerrojoDeEjercicios cerrojo = new(organizacion, new InquilinoFijo(empresaId));

        await cerrojo.TomarEnExclusivaAsync(ejercicioId, CancellationToken.None);

        await transaccion.RollbackAsync();
    }

    /// <summary>Cierra por la API, con el <c>If-Match</c> que la R11 exige.</summary>
    /// <remarks>
    /// La versión se lee del <b>ejercicio</b> y se manda contra el <b>cierre</b>, porque el cierre
    /// no es otro recurso: es otra puerta al mismo. Sin la cabecera, la respuesta sería un 428 y el
    /// caso leería «falta una precondición» donde quería leer «el periodo ya es definitivo».
    /// </remarks>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="ejercicioId">El ejercicio que cerrar.</param>
    /// <returns>Lo que contestó la API.</returns>
    private static Task<HttpResponseMessage> CerrarAsync(HttpClient cliente, Guid ejercicioId) =>
        cliente.AccionarAsync(
            $"{LosMaestrosPorLaApi.Ejercicios}/{ejercicioId}",
            $"{LosMaestrosPorLaApi.Ejercicios}/{ejercicioId}/cierre",
            HttpMethod.Post);

    private static async Task CerrarPorLaApiAsync(HttpClient cliente, Guid ejercicioId)
    {
        using HttpResponseMessage cierre = await CerrarAsync(cliente, ejercicioId);

        cierre.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cierre));
    }

    private static async Task<EjercicioDto> CrearEjercicioAsync(HttpClient cliente, int anio)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            LosMaestrosPorLaApi.Ejercicios,
            new CrearEjercicioDto
            {
                Anio = anio,
                FechaDeInicio = new DateOnly(anio, 1, 1),
                FechaDeFin = new DateOnly(anio, 12, 31),
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<EjercicioDto>())!;
    }

    /// <summary>Los maestros de un ajuste y su petición, con la fecha de operación que se pida.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <param name="fecha">La fecha de operación del documento.</param>
    /// <param name="anioDelEjercicio">
    /// El año del ÚNICO ejercicio que se abre. Por omisión, el de la fecha. Pasarle otro es lo que
    /// deja la fecha del documento fuera de todo ejercicio sin tener que borrar ninguno.
    /// </param>
    /// <returns>La petición lista para el alta, y la serie que la numerará.</returns>
    private static async Task<(AbrirAjusteDto Peticion, SerieDto Serie)> UnAjusteCompletoAsync(
        HttpClient cliente,
        string codigo,
        int semillaDeInstalacion,
        DateOnly fecha,
        int? anioDelEjercicio = null)
    {
        (AbrirAjusteDto peticion, SerieDto serie, _) = await UnAjusteCompletoConSuEjercicioAsync(
            cliente, codigo, semillaDeInstalacion, fecha, anioDelEjercicio);

        return (peticion, serie);
    }

    /// <summary>
    /// Lo mismo, devolviendo además el ejercicio <b>de la fecha</b>, que es el que estos casos
    /// cierran.
    /// </summary>
    /// <remarks>
    /// El ejercicio va por la fecha del documento y no por el año en curso: los casos que anulan
    /// necesitan uno viejo, y el que la serie numera es el suyo. <c>LosMaestrosPorLaApi</c> abre
    /// siempre el del año en curso, así que aquí se abre a mano el que haga falta y la serie se
    /// cuelga de él.
    /// </remarks>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <param name="fecha">La fecha de operación del documento.</param>
    /// <param name="anioDelEjercicio">El año del ejercicio que se abre; por omisión, el de la fecha.</param>
    /// <returns>La petición, la serie que la numerará y el ejercicio que se ha abierto.</returns>
    private static async Task<(AbrirAjusteDto Peticion, SerieDto Serie, EjercicioDto Ejercicio)>
        UnAjusteCompletoConSuEjercicioAsync(
            HttpClient cliente,
            string codigo,
            int semillaDeInstalacion,
            DateOnly fecha,
            int? anioDelEjercicio = null)
    {
        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        UbicacionDto ubicacion =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01");

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semillaDeInstalacion);

        EjercicioDto ejercicio =
            await CrearEjercicioAsync(cliente, anioDelEjercicio ?? fecha.Year);

        SerieDto serie =
            await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, ejercicio.Id, codigo);

        AbrirAjusteDto peticion = new(
            serie.Id,
            almacen.Id,
            fecha,
            "Regularización de un recuento",
            [new LineaDeAjusteDto(ubicacion.Id, articuloId, 4m, unidadId, 2m, 1.50m, "EUR")]);

        return (peticion, serie, ejercicio);
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }
}
