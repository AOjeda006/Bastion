using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Cruces;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Concurrencia;
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
/// <b>Los seis casos del cerrojo usan dos transacciones DE VERDAD.</b> Lo que hay que ejercer no es
/// «cerrar después de confirmar» sino el <b>solape</b>: una operación que ya leyó el ejercicio y
/// todavía no ha llegado a su <c>COMMIT</c>. Quien llega segundo se queda esperando, y esperar para
/// siempre es un caso que no termina en vez de un caso que falla; así que, cuando lo que se afirma
/// es la espera, el segundo lleva <c>SET LOCAL lock_timeout</c> y el desenlace es el <c>55P03</c>
/// de PostgreSQL. Es determinista: o consigue el cerrojo o da ese error, sin depender de lo rápida
/// que sea la máquina.
/// </para>
/// <para>
/// <b>Los dos de «el documento primero» no pueden llevar plazo</b>, porque lo que afirman no es
/// que mover o borrar esperen —esperan también sin el cerrojo, en su <c>UPDATE</c> o su
/// <c>DELETE</c>— sino <b>lo que deciden después de esperar</b>. Así que el segundo corre entero y
/// el caso suelta al primero solo cuando el motor dice que el segundo ya le está esperando.
/// </para>
/// <para>
/// <b>Semillas: el fichero entero es del 384 al 407.</b> Las empresas, del 384 al 389, la 396, la
/// 398, la 399 y del 400 al 403; los maestros de instalación, del 390 al 395, la 397 y del 404 al
/// 407. Este carril comparte la base entre todos sus ficheros, así que una semilla repetida no
/// falla aquí: falla en el fichero de otro que la pedía primero.
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

        // Y NUMERA EN LA SERIE DEL ORIGINAL, que cuelga del ejercicio cerrado: es la excepción del
        // ADR-0043, escrita en `AnularAjuste`. La sentencia exige que la fecha caiga en el
        // ejercicio de la serie, y la que se le pasa es la del ORIGINAL, no la de hoy: con la de
        // hoy, esta anulación contestaría `fecha-fuera-del-ejercicio-de-la-serie`, y la R2 promete
        // que anular se puede siempre.
        anulacion.Valor.Inverso.SerieId.ShouldBe(anulacion.Valor.Original.SerieId);
        anulacion.Valor.Inverso.Numero.ShouldBe(2, "el original se llevó el 1 de esa misma serie");

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
    public async Task Anular_con_hoy_fuera_de_todo_ejercicio_no_escribe_el_inverso()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(396);

        // EL HUECO, QUE ES EL CASO QUE HACE VISIBLE ESTA GUARDA. El de arriba no la ejerce: allí
        // el inverso cae en el ejercicio abierto y la pregunta se contesta que sí. Aquí el
        // documento vive en un año viejo CON su ejercicio abierto -se confirma sin problema-, y
        // del año en curso no hay ninguno, que es lo que pasa cada 1 de enero hasta que alguien
        // abre el año. Anular tendría que escribir el inverso con fecha de HOY, y hoy no cae en
        // ningún periodo.
        var haceNueveAnios = new DateOnly(Hoy.Year - 9, 6, 15);

        (AbrirAjusteDto peticion, _, _) =
            await UnAjusteCompletoConSuEjercicioAsync(cliente, "EJE-G", 397, haceNueveAnios);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);
        confirmacion.EsCorrecto.ShouldBeTrue(
            $"el ejercicio del documento SÍ está abierto. Contestó «{confirmacion.Error?.Codigo}»");

        Resultado<AnulacionDto> anulacion =
            await modulo.AnularAsync(alta.Valor.Id, "Duplicado detectado en la revisión");

        anulacion.EsCorrecto.ShouldBeFalse(
            "el inverso es un documento como cualquier otro y la R9 le vale igual: escribirlo " +
            "fuera de todo ejercicio dejaría un movimiento que ninguna autoliquidación recoge");

        anulacion.Error!.Codigo.ShouldBe(
            "ajuste-sin-ejercicio",
            "y lo dice de HOY, que es la fecha que el inverso iba a llevar, no de la del original");

        // Y NO QUEDA NADA A MEDIAS: ni el inverso escrito, ni el original marcado. La guarda va
        // ANTES de `CrearInverso`, así que no hay documento que deshacer.
        await using InventarioDbContext inventario = postgres.AbrirInventario(empresa.Id);

        (await inventario.Ajustes.CountAsync())
            .ShouldBe(1, "el inverso no llegó a nacer");

        Ajuste original = await inventario.Ajustes.SingleAsync(fila => fila.Id == alta.Valor.Id);

        original.Estado.ShouldBe(
            EstadoDeAjuste.Confirmado,
            "anular es todo o nada: si el inverso no se escribe, el original no se marca");
    }

    [Fact]
    public async Task Cerrar_el_ejercicio_cerrado_de_otra_empresa_es_el_mismo_404_que_uno_inventado()
    {
        // EL CERROJO DEL CIERRE ES SQL CRUDO, y el filtro global de empresa no lo alcanza: la
        // sentencia compara la empresa ella misma. Este es el caso que se pone rojo si esa
        // comparación desaparece, y no por casualidad. Sin ella, la lectura con cerrojo encontraría
        // la fila de A por su identificador, vería «Cerrado» y el caso de uso contestaría
        // `ejercicio-ya-cerrado` ANTES de llegar a la lectura por el ORM, que es la que sí lleva
        // el filtro. B se enteraría de que ese ejercicio existe y de que está cerrado, y además se
        // habría llevado un `FOR UPDATE` sobre una fila de otra sociedad.
        //
        // Por eso el ejercicio de A va CERRADO: con uno abierto la fuga no se ve —el cerrojo lo
        // encontraría, diría «Abierto», y la lectura por el ORM, con su filtro, daría el 404 de
        // todas formas—.
        (HttpClient enA, EmpresaDto _) = await EnUnaEmpresaNuevaAsync(398);

        EjercicioDto deA = await CrearEjercicioAsync(enA, Hoy.Year);
        await CerrarPorLaApiAsync(enA, deA.Id);

        (HttpClient enB, EmpresaDto _) = await EnUnaEmpresaNuevaAsync(399);

        // El If-Match va a mano y con un valor cualquiera, por lo mismo que en
        // `ElFiltroDeEmpresaTests`: B no puede leer la fila para sacar el suyo.
        using HttpResponseMessage ajeno = await enB.EnviarConVersionAsync(
            HttpMethod.Post, $"{LosMaestrosPorLaApi.Ejercicios}/{deA.Id}/cierre", "\"1\"");

        using HttpResponseMessage inventado = await enB.EnviarConVersionAsync(
            HttpMethod.Post,
            $"{LosMaestrosPorLaApi.Ejercicios}/{Guid.CreateVersion7()}/cierre",
            "\"1\"");

        ajeno.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(ajeno));
        inventado.StatusCode.ShouldBe(HttpStatusCode.NotFound, await Escenario.Detalle(inventado));

        // Y LA MISMA RESPUESTA, no solo el mismo código: un `type` distinto volvería a separarlas.
        (await TipoDelProblemaAsync(ajeno)).ShouldBe(await TipoDelProblemaAsync(inventado));
        (await TipoDelProblemaAsync(ajeno)).ShouldBe("/errors/ejercicio-no-encontrado");
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

    [Fact]
    public async Task Mover_el_ejercicio_espera_a_la_anulacion_que_ya_estaba_dentro_y_ve_su_inverso()
    {
        (HttpClient cliente, Guid empresaId, Guid originalId, EjercicioDto esteAnio) =
            await UnOriginalDelAnioPasadoYEsteAnioVacioAsync(400, "EJE-H", 404);

        string recurso = $"{LosMaestrosPorLaApi.Ejercicios}/{esteAnio.Id}";
        string etiqueta = await cliente.EtiquetaDeAsync(recurso);

        await using ElModuloDeInventario modulo = new(postgres, empresaId);

        // TRANSACCIÓN 1: la anulación escribe el inverso con fecha de hoy —dentro del ejercicio
        // que se va a mover— y NO suelta. ES EL ORDEN QUE DISTINGUE, y por eso el documento es un
        // inverso y no un borrador: nace confirmado dentro de una transacción que todavía no ha
        // terminado, así que el puerto no lo ve. Un borrador de antes sí lo vería, y un caso
        // montado con uno saldría verde también sin el cerrojo.
        await using IDbContextTransaction anulando = await modulo.AbrirTransaccionAsync();

        Resultado<AnulacionDto> anulacion =
            await modulo.AnularSinAbrirTransaccionAsync(originalId, MotivoDeLaAnulacion);

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»");

        // TRANSACCIÓN 2: mover por la API, empezando mañana, que deja fuera el día del inverso.
        Task<HttpResponseMessage> moviendo = cliente.EnviarConVersionAsync(
            HttpMethod.Put, recurso, etiqueta, JsonContent.Create(DesdeManana()));

        await EsperarAQueLaFreneAsync(modulo.ProcesoDeLaBase, moviendo);

        await anulando.CommitAsync();

        using HttpResponseMessage movido = await moviendo.WaitAsync(TimeSpan.FromSeconds(30));

        // SIN EL CERROJO ESTO ES UN 200, y el inverso se queda fuera de todo ejercicio: mover
        // pregunta al puerto, no ve nada y su `UPDATE` se queda esperando al `FOR SHARE` de la
        // anulación. Cuando ésta suelta, el `UPDATE` pasa, porque un `FOR SHARE` no cambia el
        // `xmin` y la R11 no tiene nada que chocar. Con el cerrojo, mover espera ANTES de
        // preguntar, y pregunta con el inverso ya a la vista.
        movido.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(movido));

        (await TipoDelProblemaAsync(movido)).ShouldBe(
            "/errors/ejercicio-dejaria-documentos-fuera",
            "el inverso de hoy es un documento como otro cualquiera, y mover el ejercicio lo " +
            "dejaría sin periodo al que pertenecer");
    }

    [Fact]
    public async Task Borrar_el_ejercicio_espera_a_la_anulacion_que_ya_estaba_dentro_y_ve_su_inverso()
    {
        (HttpClient cliente, Guid empresaId, Guid originalId, EjercicioDto esteAnio) =
            await UnOriginalDelAnioPasadoYEsteAnioVacioAsync(401, "EJE-I", 405);

        string recurso = $"{LosMaestrosPorLaApi.Ejercicios}/{esteAnio.Id}";
        string etiqueta = await cliente.EtiquetaDeAsync(recurso);

        await using ElModuloDeInventario modulo = new(postgres, empresaId);

        // TRANSACCIÓN 1: la misma anulación en vuelo que en el caso de mover, por lo mismo.
        await using IDbContextTransaction anulando = await modulo.AbrirTransaccionAsync();

        Resultado<AnulacionDto> anulacion =
            await modulo.AnularSinAbrirTransaccionAsync(originalId, MotivoDeLaAnulacion);

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»");

        // TRANSACCIÓN 2: borrar por la API. El ejercicio de este año no tiene series —el inverso
        // numera en la del original—, así que la clave ajena no lo para: lo único que hay entre
        // el borrado y un inverso sin ejercicio es la pregunta al puerto.
        Task<HttpResponseMessage> borrando =
            cliente.EnviarConVersionAsync(HttpMethod.Delete, recurso, etiqueta);

        await EsperarAQueLaFreneAsync(modulo.ProcesoDeLaBase, borrando);

        await anulando.CommitAsync();

        using HttpResponseMessage borrado = await borrando.WaitAsync(TimeSpan.FromSeconds(30));

        // SIN EL CERROJO ESTO ES UN 204, por el mismo camino que mover: el `DELETE` espera al
        // `FOR SHARE`, el `xmin` no ha cambiado y la fila se va.
        borrado.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(borrado));

        (await TipoDelProblemaAsync(borrado)).ShouldBe(
            "/errors/ejercicio-con-documentos",
            "el ejercicio tiene dentro el inverso de hoy, y borrarlo lo dejaría huérfano");
    }

    [Fact]
    public async Task La_anulacion_espera_al_movimiento_que_ya_estaba_dentro_y_luego_lo_obedece()
    {
        (HttpClient cliente, Guid empresaId, Guid originalId, EjercicioDto esteAnio) =
            await UnOriginalDelAnioPasadoYEsteAnioVacioAsync(402, "EJE-J", 406);

        VersionDeRecurso version = await LaVersionDeAsync(cliente, esteAnio.Id);

        await using ElEjercicioAMano ejercicio = new(postgres, empresaId);
        await using ElModuloDeInventario modulo = new(postgres, empresaId);

        // TRANSACCIÓN 1: mover de verdad —el caso de uso entero, hasta su guardado— y NO soltar.
        //
        // ESTE ORDEN NO NECESITABA EL CERROJO NUEVO, y se dice para que nadie cuente este caso
        // como prueba del arreglo: el `UPDATE` de la fila toma él solo un cerrojo que el
        // `FOR SHARE` de la anulación respeta. Está para que siga siendo así, y para afirmar la
        // otra mitad: que la anulación, al soltarse, obedece lo que el movimiento dejó escrito.
        await using (IDbContextTransaction moviendo = await ejercicio.AbrirTransaccionAsync())
        {
            Resultado<EjercicioDto> movido = await ejercicio.Mover.EjecutarAsync(
                esteAnio.Id, version, DesdeManana(), CancellationToken.None);

            movido.EsCorrecto.ShouldBeTrue($"«{movido.Error?.Codigo}»");

            // TRANSACCIÓN 2: la anulación pide el compartido sobre el ejercicio de hoy, que es la
            // fila que el movimiento tiene cogida. Con plazo, para que el caso falle en vez de
            // colgarse.
            await using (IDbContextTransaction anulando = await modulo.AbrirTransaccionAsync())
            {
                await modulo.PonerPlazoCortoDeCerrojoAsync();

                PostgresException choque = await ElChoqueDeCerrojoAsync(
                    () => modulo.AnularSinAbrirTransaccionAsync(originalId, MotivoDeLaAnulacion));

                choque.SqlState.ShouldBe(
                    PostgresErrorCodes.LockNotAvailable,
                    "la anulación no decide sobre un ejercicio que otro está moviendo");

                await anulando.RollbackAsync();
            }

            await moviendo.CommitAsync();
        }

        // Y SOLTADO, LA ANULACIÓN LO OBEDECE: hoy ya no cae en ningún ejercicio. Con un módulo
        // nuevo, porque el de arriba se quedó con la transacción abortada por el choque.
        await using ElModuloDeInventario despues = new(postgres, empresaId);

        Resultado<AnulacionDto> anulacion =
            await despues.AnularAsync(originalId, MotivoDeLaAnulacion);

        anulacion.EsCorrecto.ShouldBeFalse(
            "el ejercicio empieza mañana, así que el inverso de hoy no tiene dónde caer");

        anulacion.Error!.Codigo.ShouldBe("ajuste-sin-ejercicio");
    }

    [Fact]
    public async Task La_anulacion_espera_al_borrado_que_ya_estaba_dentro_y_luego_lo_obedece()
    {
        (HttpClient cliente, Guid empresaId, Guid originalId, EjercicioDto esteAnio) =
            await UnOriginalDelAnioPasadoYEsteAnioVacioAsync(403, "EJE-K", 407);

        VersionDeRecurso version = await LaVersionDeAsync(cliente, esteAnio.Id);

        await using ElEjercicioAMano ejercicio = new(postgres, empresaId);
        await using ElModuloDeInventario modulo = new(postgres, empresaId);

        // TRANSACCIÓN 1: borrar de verdad y NO soltar. Mismo aviso que al mover: el `DELETE`
        // toma él solo el cerrojo de la fila, así que este orden no depende del arreglo.
        await using (IDbContextTransaction borrando = await ejercicio.AbrirTransaccionAsync())
        {
            Resultado borrado =
                await ejercicio.Borrar.EjecutarAsync(esteAnio.Id, version, CancellationToken.None);

            borrado.EsCorrecto.ShouldBeTrue($"«{borrado.Error?.Codigo}»");

            await using (IDbContextTransaction anulando = await modulo.AbrirTransaccionAsync())
            {
                await modulo.PonerPlazoCortoDeCerrojoAsync();

                PostgresException choque = await ElChoqueDeCerrojoAsync(
                    () => modulo.AnularSinAbrirTransaccionAsync(originalId, MotivoDeLaAnulacion));

                choque.SqlState.ShouldBe(
                    PostgresErrorCodes.LockNotAvailable,
                    "la anulación no decide sobre un ejercicio que otro está borrando");

                await anulando.RollbackAsync();
            }

            await borrando.CommitAsync();
        }

        await using ElModuloDeInventario despues = new(postgres, empresaId);

        Resultado<AnulacionDto> anulacion =
            await despues.AnularAsync(originalId, MotivoDeLaAnulacion);

        anulacion.EsCorrecto.ShouldBeFalse(
            "el ejercicio de hoy ya no existe, así que el inverso no tiene dónde caer");

        anulacion.Error!.Codigo.ShouldBe("ajuste-sin-ejercicio");
    }

    /// <summary>Por qué anulan los casos de mover y borrar. No decide nada.</summary>
    private const string MotivoDeLaAnulacion = "Duplicado detectado en la revisión";

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
    private static async Task<string?> TipoDelProblemaAsync(HttpResponseMessage respuesta)
    {
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return problema.RootElement.GetProperty("type").GetString();
    }

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

    /// <summary>
    /// El escenario de los cuatro casos de mover y borrar: un ajuste confirmado el año pasado, y
    /// el ejercicio de este año vacío y sin series.
    /// </summary>
    /// <remarks>
    /// <b>El original vive en el año pasado a propósito.</b> Su inverso lleva la fecha de hoy y cae
    /// en el ejercicio de este año, que es el que se mueve o se borra; y como el original no está
    /// dentro, lo único que el puerto puede encontrar ahí es el inverso. Con el original dentro, el
    /// puerto lo vería desde el principio y el caso no distinguiría nada.
    /// </remarks>
    /// <param name="semilla">La empresa del caso.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <returns>El cliente, la empresa, el ajuste confirmado y el ejercicio de este año.</returns>
    private async Task<(HttpClient Cliente, Guid EmpresaId, Guid OriginalId, EjercicioDto EsteAnio)>
        UnOriginalDelAnioPasadoYEsteAnioVacioAsync(int semilla, string codigo, int semillaDeInstalacion)
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(semilla);

        (AbrirAjusteDto peticion, _, _) = await UnAjusteCompletoConSuEjercicioAsync(
            cliente, codigo, semillaDeInstalacion, new DateOnly(Hoy.Year - 1, 6, 15));

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(alta.Valor.Id);
        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

        EjercicioDto esteAnio = await CrearEjercicioAsync(cliente, Hoy.Year);

        return (cliente, empresa.Id, alta.Valor.Id, esteAnio);
    }

    /// <summary>Las fechas nuevas de los casos que mueven: empieza mañana, y hoy se queda fuera.</summary>
    private static ModificarEjercicioDto DesdeManana()
    {
        DateOnly manana = Hoy.AddDays(1);

        return new ModificarEjercicioDto
        {
            FechaDeInicio = manana,
            FechaDeFin = manana.AddMonths(Ejercicio.MesesMaximos).AddDays(-1),
        };
    }

    /// <summary>La versión del ejercicio que la API publica, para los casos de uso a mano.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="ejercicioId">El ejercicio.</param>
    /// <returns>La versión, sacada del <c>ETag</c>.</returns>
    private static async Task<VersionDeRecurso> LaVersionDeAsync(HttpClient cliente, Guid ejercicioId)
    {
        Resultado<VersionDeRecurso> version = VersionDeRecurso.DeLaCabecera(
            await cliente.EtiquetaDeAsync($"{LosMaestrosPorLaApi.Ejercicios}/{ejercicioId}"));

        version.EsCorrecto.ShouldBeTrue($"«{version.Error?.Codigo}»");

        return version.Valor;
    }

    /// <summary>
    /// Espera a que la operación en vuelo se quede parada detrás del proceso dado, o falla.
    /// </summary>
    /// <remarks>
    /// <b>Es lo que hace que el caso ejerza la carrera y no dos operaciones seguidas.</b> Si la
    /// anulación soltara antes de que la otra operación llegara a esperarla, ésta preguntaría con
    /// el inverso ya a la vista y saldría bien con cerrojo o sin él. Así que no se suelta por
    /// tiempo sino cuando el motor dice que hay alguien esperando, que es lo que contesta
    /// <c>pg_blocking_pids</c>. Y si la operación termina sin haber esperado, eso ya es el fallo:
    /// ha decidido sin pedir la fila del ejercicio.
    /// </remarks>
    /// <param name="procesoQueFrena">El proceso de PostgreSQL de la anulación en vuelo.</param>
    /// <param name="enVuelo">La operación que tiene que quedarse esperando.</param>
    private async Task EsperarAQueLaFreneAsync(int procesoQueFrena, Task enVuelo)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand quienEspera = new(
            "SELECT count(*) FROM pg_stat_activity WHERE @frena = ANY(pg_blocking_pids(pid))",
            conexion);

        quienEspera.Parameters.AddWithValue("frena", procesoQueFrena);

        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < limite)
        {
            enVuelo.IsCompleted.ShouldBeFalse(
                "la operación ha terminado sin esperar a la anulación: ha decidido sin pedir la " +
                "fila del ejercicio que la anulación tiene cogida");

            if ((long)(await quienEspera.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new ShouldAssertException(
            "en treinta segundos nadie se ha puesto a esperar a la anulación");
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }
}
