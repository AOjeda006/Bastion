using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Idempotencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La anulación del ítem 2.5 contra la base de verdad: el par suma cero, el inverso es un
/// documento entero, y anular dos veces —seguidas o a la vez— deja un solo inverso.
/// </summary>
/// <remarks>
/// <para>
/// <b>La afirmación del ítem es que el par suma cero</b>, y no que exista un inverso. Las cinco
/// comprobaciones estructurales —hay inverso, está confirmado, lleva número, la flecha va en los
/// dos sentidos, no hay dos— salen las cinco en verde con un error de signo dentro, y el almacén
/// habría quedado movido al revés de lo que el documento dice. Por eso
/// <see cref="El_par_suma_cero_en_el_libro_por_articulo_almacen_y_ubicacion"/> va primero: es la
/// que se pone roja si se quita la negación de <c>Ajuste.CrearInverso</c>.
/// </para>
/// <para>
/// <b>Aquí se suma el LIBRO, no lo que devolvió el agregado.</b> La misma suma sobre los objetos
/// en memoria está en el carril rápido y no sustituye a esta: lo que lee el stock son las filas,
/// y entre el agregado y la fila hay una conversión de decimales, un <c>CHECK</c> y una
/// partición. Lo que se afirma aquí es lo que quedó escrito.
/// </para>
/// <para>
/// <b>Y la anulación entra por la API</b>, que es lo que hace que el número del inverso signifique
/// algo: sale del cerrojo del 2.4, dentro de la transacción que abre el filtro de idempotencia, y
/// el borde exige su permiso y su clave. Un cableado a mano habría numerado igual sin ejercer
/// nada de eso.
/// </para>
/// <para>
/// <b>Semillas: las empresas van de la 315 a la 319 y los maestros de instalación de la 354 a la
/// 360.</b> El resto del reparto de este carril está en <c>ElCerrojoDeLaNumeracionTests</c> —del
/// 301 al 308 y del 320 en adelante—, en <c>LaSerieDelAjusteTests</c> —309 al 312, maestros 350 y
/// 351— y en <c>ElNumeroEntraEnElReciboTests</c> —313 y 314, maestros 352 y 353—. Un número de
/// empresa acaba en un NIF único y uno de maestro en el código único de otra tabla, así que son
/// dos cuentas y no una.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaAnulacionConContraDocumentoTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
    private const string Cabecera = "Idempotency-Key";

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

    /// <summary>El par original + inverso deja cada artículo donde estaba, hueco por hueco.</summary>
    /// <remarks>
    /// <para>
    /// <b>Se agrupa por artículo, almacén y ubicación</b> y no en un solo montón: un total general
    /// a cero lo dan también dos errores que se compensan entre artículos distintos, y mover
    /// mercancía de hueco al anular es justamente el daño que esto tiene que cazar.
    /// </para>
    /// <para>
    /// <b>Y se afirma que hay grupos antes de exigirles el cero</b> (ADR-0020), y que el original
    /// movió algo: sobre cero filas, «todos los grupos suman cero» es verdad y no dice nada, y un
    /// par de ceros también suma cero.
    /// </para>
    /// <para>
    /// <b>Las dos líneas llevan factor distinto de uno y signos contrarios</b>, y las dos cosas
    /// hacen falta. Con factor uno, la cantidad introducida y la de unidad base coinciden y un
    /// cálculo que se saltara el factor saldría verde; con las dos entradas del mismo signo, un
    /// inverso que en vez de negar pusiera el valor absoluto en negativo también saldría verde.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_par_suma_cero_en_el_libro_por_articulo_almacen_y_ubicacion()
    {
        UnCaso caso = await UnAjusteConfirmadoAsync(315, "ANU-A", 354, 355);

        using HttpResponseMessage anulacion = await AnularAsync(caso, "Me equivoqué de almacén");

        anulacion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(anulacion));

        long filas = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.movimiento_stock " +
                "WHERE empresa_id = '{0}' AND cantidad_en_unidad_base <> 0",
                caso.EmpresaId));

        filas.ShouldBe(
            4,
            "el par tenía que dejar cuatro filas que mueven algo —dos del original y dos del " +
            "inverso—: sobre cero filas, «todo suma cero» sale verde por no haber sumado " +
            "(ADR-0020), y un par de ceros también sumaría cero");

        long grupos = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                """
                SELECT count(*) FROM (
                    SELECT 1 FROM inventario.movimiento_stock
                    WHERE empresa_id = '{0}'
                    GROUP BY articulo_id, almacen_id, ubicacion_id) AS grupos
                """,
                caso.EmpresaId));

        grupos.ShouldBe(2, "dos artículos, cada uno en su hueco: dos grupos que tienen que cuadrar");

        IReadOnlyList<string> descuadrados = await ElLibro.TextosAsync(
            postgres, Descuadres(caso.EmpresaId));

        descuadrados.ShouldBeEmpty(
            "el par tenía que dejar cada artículo donde estaba. Lo que sale aquí es «artículo → " +
            "lo que ha quedado movido», y con un solo signo mal puesto es lo que se lee");
    }

    /// <summary>El inverso es un documento entero, no una marca: número, estado y flecha.</summary>
    /// <remarks>
    /// <b>Y la fecha del inverso es la de hoy y no la del original</b>, que es lo que el ítem
    /// decidió y lo que hace que anular un documento de un ejercicio cerrado no sea escribir
    /// dentro de él. Aquí se afirma que la del par son DOS fechas distintas cuando el original es
    /// de otro día, porque un inverso que heredara la fecha pasaría cualquier afirmación hecha
    /// solo sobre el día de hoy.
    /// </remarks>
    [Fact]
    public async Task El_inverso_es_un_documento_confirmado_con_su_numero_y_su_flecha()
    {
        UnCaso caso = await UnAjusteConfirmadoAsync(316, "ANU-B", 356, 357, diasAtras: 3);

        using HttpResponseMessage respuesta = await AnularAsync(caso, "Recuento mal hecho");

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));

        AnulacionDto par = (await respuesta.Content.ReadFromJsonAsync<AnulacionDto>())!;

        par.Original.Id.ShouldBe(caso.AjusteId);
        par.Original.Estado.ShouldBe(nameof(EstadoDeAjuste.Anulado));
        par.Original.Numero.ShouldBe(1);

        par.Inverso.Id.ShouldNotBe(caso.AjusteId);
        par.Inverso.Estado.ShouldBe(nameof(EstadoDeAjuste.Confirmado));
        par.Inverso.AnulaAId.ShouldBe(caso.AjusteId);
        par.Inverso.Lineas.ShouldBe(par.Original.Lineas);

        // EL NÚMERO SALE DEL CERROJO, y es el siguiente de la MISMA serie: el inverso no estrena
        // tipo de documento, así que tampoco estrena serie. Un dos aquí es lo que distingue un
        // documento de pleno derecho de una marca con aspecto de documento.
        par.Inverso.Numero.ShouldBe(2);
        par.Inverso.SerieId.ShouldBe(caso.Serie.Id);
        (await ContadorAsync(caso.Cliente, caso.Serie.Id)).ShouldBe(2);

        par.Inverso.FechaDeOperacion.ShouldBe(DateOnly.FromDateTime(DateTime.UtcNow));
        par.Inverso.FechaDeOperacion.ShouldNotBe(
            par.Original.FechaDeOperacion,
            "el inverso NO hereda la fecha del original: con la heredada se asentaría en su mismo " +
            "periodo, y anular un documento de un ejercicio cerrado sería escribir dentro de él");

        // Y lo mismo en la base, que no depende de lo que conteste ninguna respuesta.
        await using InventarioDbContext inventario = postgres.AbrirInventario(caso.EmpresaId);

        Ajuste original = await inventario.Ajustes.SingleAsync(fila => fila.Id == caso.AjusteId);
        original.Estado.ShouldBe(EstadoDeAjuste.Anulado);

        Ajuste inverso = await inventario.Ajustes.SingleAsync(fila => fila.Id == par.Inverso.Id);
        inverso.AnulaAId.ShouldBe(caso.AjusteId);
        inverso.AlmacenId.ShouldBe(original.AlmacenId);
    }

    /// <summary>Anular dos veces seguidas no crea dos inversos: la segunda es un 409.</summary>
    /// <remarks>
    /// <b>Esta es la mitad fácil, y se dice que lo es.</b> La segunda llamada se encuentra el
    /// documento ya <c>Anulado</c> y la guarda de estado la rechaza sin que nada concurrente haya
    /// ocurrido. Lo que de verdad hay que ejercer —dos que leen el mismo <c>Confirmado</c> antes
    /// de que ninguna escriba— está en
    /// <see cref="Dos_anulaciones_simultaneas_dejan_un_solo_inverso"/>, y lo separa otra cosa.
    /// </remarks>
    [Fact]
    public async Task Anular_dos_veces_seguidas_no_crea_dos_inversos()
    {
        UnCaso caso = await UnAjusteConfirmadoAsync(317, "ANU-C", 358, 359);

        using HttpResponseMessage primera = await AnularAsync(caso, "Me equivoqué");
        using HttpResponseMessage segunda = await AnularAsync(caso, "Me vuelvo a equivocar");

        primera.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(primera));

        segunda.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            $"un ajuste anulado ya tiene su inverso. {await Escenario.Detalle(segunda)}");

        (await segunda.Content.ReadAsStringAsync()).ShouldContain("/errors/ajuste-no-esta-confirmado");

        (await InversosDeAsync(caso)).ShouldBe(1);

        // Y por el efecto: la serie gastó DOS correlativos, el del original y el del inverso. Si
        // la segunda hubiera llegado a numerar antes de encontrarse la guarda, aquí habría un
        // tres y la R5 tendría un hueco.
        (await ContadorAsync(caso.Cliente, caso.Serie.Id)).ShouldBe(2);
    }

    /// <summary>
    /// Dos anulaciones que leen el mismo documento confirmado: una escribe y la otra se estrella
    /// contra el índice único del inverso.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos transacciones de verdad, y por eso no valen dos llamadas seguidas.</b> Las dos leen
    /// <c>Confirmado</c> y las dos se creen con derecho a crear el inverso: la guarda de estado
    /// del caso de uso las deja pasar a las dos, porque cuando cada una mira, el documento
    /// todavía lo está. Lo que las separa son, en este orden, el índice único de
    /// <c>anula_a_id</c> y la R11 sobre la fila del ajuste —el testigo es <c>xmin</c>, puesto desde
    /// el 2.3—: la segunda escribe un inverso que ya no cabe, y si cupiera escribiría contra una
    /// versión que ya no está.
    /// </para>
    /// <para>
    /// <b>Lo que se lleva el perdedor, MEDIDO y no supuesto.</b> Aquí hubo escrito que era un
    /// <c>DbUpdateConcurrencyException</c>; al poner el índice único se midió y no lo es. Anular
    /// escribe DOS filas —inserta el inverso y cambia el estado del original— y cuál de las dos
    /// llega antes a la base lo decide el ORM: llega antes el <c>INSERT</c>, así que el perdedor
    /// choca contra <c>ix_ajustes_anula_a_id</c> y se lleva un <c>DbUpdateException</c> con el
    /// <c>23505</c> de PostgreSQL dentro. El testigo del original habría dicho lo mismo un
    /// instante después; no le da tiempo.
    /// </para>
    /// <para>
    /// <b>Y el borde contesta lo mismo por los dos caminos</b>, que es lo que impide que esta
    /// diferencia se note desde fuera: <c>ManejadorDeCarreraPerdidaEnLaBase</c> traduce ESE
    /// índice, por su nombre, al mismo <c>412</c> con el mismo <c>version-obsoleta</c> que da
    /// <c>ManejadorDeVersionObsoleta</c> —no un <c>409</c> y no un <c>500</c>—. Eso se afirma en
    /// el carril rápido, en <c>PoliticaDeErroresTests</c>, porque aquí no hay borde: este caso
    /// llama al caso de uso y lo que ve es la excepción. Ninguno de los dos prueba la cadena
    /// entera solo; los dos juntos, sí.
    /// </para>
    /// <para>
    /// <b>Y NINGÚN inverso</b>: la transacción entera del perdedor se deshace, y con ella el
    /// número que había tomado, que la serie reutiliza. Eso se afirma por el contador, que es lo
    /// único que lo demuestra: queda en dos y no en tres.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Dos_anulaciones_simultaneas_dejan_un_solo_inverso()
    {
        UnCaso caso = await UnAjusteConfirmadoAsync(318, "ANU-D", 360, 361);

        await using ElModuloDeInventario ganadora = new(postgres, caso.EmpresaId);
        await using ElModuloDeInventario perdedora = new(postgres, caso.EmpresaId);

        // LA PERDEDORA LEE PRIMERO y se queda dentro de su transacción, que es lo que convierte
        // «dos seguidas» en «dos a la vez»: cuando llegue a escribir, su documento seguirá siendo
        // el `Confirmado` que leyó.
        await using IDbContextTransaction laQueEspera =
            await perdedora.LeerElAjusteYQuedarseDentroAsync(caso.AjusteId);

        Resultado<AnulacionDto> gana = await ganadora.AnularAsync(caso.AjusteId, "La primera");

        gana.EsCorrecto.ShouldBeTrue($"«{gana.Error?.Codigo}»");

        DbUpdateException choque = await Should.ThrowAsync<DbUpdateException>(
            () => perdedora.AnularSinAbrirTransaccionAsync(caso.AjusteId, "La segunda"));

        // POR EL NOMBRE DEL ÍNDICE, y no solo por el tipo. Un `DbUpdateException` lo lanza
        // cualquier escritura que la base rechace —una clave ajena, una columna obligatoria—, y
        // afirmar solo el tipo daría por buena una carrera que hubiera fallado por otra cosa. El
        // nombre es además el que el borde traduce: si una migración lo cambia, esto se pone rojo
        // antes de que la traducción deje de aplicarse en silencio y la carrera vuelva a ser 500.
        PostgresException motor = choque.InnerException.ShouldBeOfType<PostgresException>();

        motor.SqlState.ShouldBe("23505");
        motor.ConstraintName.ShouldBe("ix_ajustes_anula_a_id");

        await laQueEspera.RollbackAsync();

        (await InversosDeAsync(caso)).ShouldBe(
            1,
            "la perdedora no dejó inverso: su transacción entera se deshizo. El índice impidió la " +
            "fila, y el rollback se llevó todo lo demás que hubiera escrito");

        (await ContadorAsync(caso.Cliente, caso.Serie.Id)).ShouldBe(
            2,
            "el número que la perdedora tomó volvió a la serie al deshacerse su transacción: si " +
            "se hubiera quedado gastado, la R5 tendría un hueco");
    }

    /// <summary>Sin la cabecera, la anulación es un 428 y no toca nada.</summary>
    /// <remarks>
    /// <b>Es la segunda acción de toda la API que EXIGE la clave, y el criterio no se ha ampliado
    /// para que quepa.</b> Es el mismo de la confirmación: el inverso toma un correlativo, y lo
    /// que hace atómicos el número y el documento es la transacción, que en este sistema abre el
    /// filtro de idempotencia y nadie más (ADR-0014). Sin cabecera el filtro se aparta en su
    /// primera línea, así que un <c>200</c> silencioso aquí sería un camino por el que se anula
    /// fuera de transacción — el que deja un número gastado sin documento.
    /// </remarks>
    [Fact]
    public async Task Sin_la_cabecera_la_anulacion_es_428_y_no_toca_nada()
    {
        UnCaso caso = await UnAjusteConfirmadoAsync(319, "ANU-E", 362, 363);

        using HttpResponseMessage sinClave = await AnularAsync(caso, "Me equivoqué", clave: null);

        sinClave.StatusCode.ShouldBe(
            HttpStatusCode.PreconditionRequired,
            "la petición está impecable y lo que falta es una precondición, igual que en la " +
            $"confirmación. {await Escenario.Detalle(sinClave)}");

        (await sinClave.Content.ReadAsStringAsync())
            .ShouldContain("/errors/" + ErroresDeIdempotencia.CodigoDeObligatoria);

        // NADA se ha movido: ni el documento, ni el contador, ni el libro.
        (await InversosDeAsync(caso)).ShouldBe(0);
        (await ContadorAsync(caso.Cliente, caso.Serie.Id)).ShouldBe(1);

        await using (InventarioDbContext inventario = postgres.AbrirInventario(caso.EmpresaId))
        {
            Ajuste intacto = await inventario.Ajustes.SingleAsync(fila => fila.Id == caso.AjusteId);
            intacto.Estado.ShouldBe(EstadoDeAjuste.Confirmado);
        }

        // Y el 428 no ha quemado nada: con la clave puesta, el MISMO ajuste se anula. Sin esta
        // segunda mitad, un rechazo que hubiera consumido algo por el camino saldría verde arriba.
        using HttpResponseMessage conClave = await AnularAsync(caso, "Me equivoqué");

        conClave.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(conClave));

        (await conClave.Content.ReadFromJsonAsync<AnulacionDto>())!.Inverso.Numero.ShouldBe(2);
    }

    /// <summary>La anulación por HTTP, con la clave o deliberadamente sin ella.</summary>
    /// <param name="caso">Lo que este caso montó.</param>
    /// <param name="motivo">Por qué se anula.</param>
    /// <param name="clave">La <c>Idempotency-Key</c>, o <c>null</c> para no mandar la cabecera.</param>
    /// <returns>La respuesta cruda, que es lo que estos casos miran.</returns>
    private static Task<HttpResponseMessage> AnularAsync(
        UnCaso caso, string motivo, string? clave = "")
    {
        HttpRequestMessage peticion =
            new(HttpMethod.Post, $"/api/v1/inventario/ajustes/{caso.AjusteId}/anulacion")
            {
                Content = JsonContent.Create(new AnularAjusteDto(motivo)),
            };

        if (clave is not null)
        {
            peticion.Headers.TryAddWithoutValidation(
                Cabecera, clave.Length == 0 ? Guid.NewGuid().ToString() : clave);
        }

        return caso.Cliente.SendAsync(peticion);
    }

    /// <summary>Por dónde va la serie, leído por la API y no por el contexto.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="serieId">La serie.</param>
    /// <returns>Cuántos correlativos ha entregado.</returns>
    private static async Task<long> ContadorAsync(HttpClient cliente, Guid serieId) =>
        (await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serieId}"))!.Contador;

    /// <summary>Cuántos documentos apuntan al original de este caso.</summary>
    /// <param name="caso">Lo que este caso montó.</param>
    /// <returns>El recuento.</returns>
    private async Task<long> InversosDeAsync(UnCaso caso) => await ElLibro.EscalarAsync<long>(
        postgres,
        Consulta(
            "SELECT count(*) FROM inventario.ajustes WHERE anula_a_id = '{0}'",
            caso.AjusteId));

    /// <summary>Los grupos del libro que NO han quedado donde estaban, con lo que les sobra.</summary>
    /// <param name="empresaId">La empresa del caso, que es su universo entero.</param>
    /// <returns>La consulta.</returns>
    private static string Descuadres(Guid empresaId) => Consulta(
        """
        SELECT articulo_id::text || ' → ' || sum(cantidad_en_unidad_base)::text
        FROM inventario.movimiento_stock
        WHERE empresa_id = '{0}'
        GROUP BY articulo_id, almacen_id, ubicacion_id
        HAVING sum(cantidad_en_unidad_base) <> 0
        """,
        empresaId);

    private static string Consulta(string plantilla, params object[] valores) =>
        string.Format(CultureInfo.InvariantCulture, plantilla, valores);

    /// <summary>Lo que un caso de este fichero necesita tener delante.</summary>
    /// <param name="Cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="EmpresaId">La empresa (R8).</param>
    /// <param name="AjusteId">El ajuste ya confirmado, listo para anular.</param>
    /// <param name="Serie">La serie que lo numeró y que numerará a su inverso.</param>
    private sealed record UnCaso(
        HttpClient Cliente, Guid EmpresaId, Guid AjusteId, SerieDto Serie);

    /// <summary>
    /// Una empresa nueva con sus maestros y, dentro, un ajuste de DOS líneas ya confirmado por la
    /// API.
    /// </summary>
    /// <remarks>
    /// El borrador se abre por el caso de uso cableado a mano y no por la API porque el borde del
    /// módulo publica dos acciones —la confirmación y la anulación— y ninguna es el alta: esa va
    /// con sus pantallas, en otro ítem. Lo que sí entra por la API es todo lo que estos casos
    /// miran.
    /// </remarks>
    /// <param name="semilla">Número de empresa, que acaba en su NIF.</param>
    /// <param name="codigo">Prefijo de los códigos de este caso.</param>
    /// <param name="primerArticulo">Número de instalación del primer artículo y su unidad.</param>
    /// <param name="segundoArticulo">Número de instalación del segundo.</param>
    /// <param name="diasAtras">Cuántos días atrás se imputa el original.</param>
    /// <returns>Lo que el caso necesita.</returns>
    private async Task<UnCaso> UnAjusteConfirmadoAsync(
        int semilla,
        string codigo,
        int primerArticulo,
        int segundoArticulo,
        int diasAtras = 0)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        UbicacionDto ubicacion =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01");

        (Guid unArticulo, Guid unaUnidad) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, primerArticulo);

        (Guid otroArticulo, Guid otraUnidad) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, segundoArticulo);

        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, codigo);

        Guid ajusteId;

        await using (ElModuloDeInventario modulo = new(postgres, empresa.Id))
        {
            Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
                new AbrirAjusteDto(
                    serie.Id,
                    almacen.Id,
                    DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-diasAtras),
                    "Regularización de un recuento",
                    [
                        new LineaDeAjusteDto(
                            ubicacion.Id, unArticulo, 3m, unaUnidad, 12m, 1.50m, "EUR"),
                        new LineaDeAjusteDto(
                            ubicacion.Id, otroArticulo, -2.5m, otraUnidad, 1m, 4.20m, "EUR"),
                    ]),
                CancellationToken.None);

            alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

            ajusteId = alta.Valor.Id;
        }

        HttpRequestMessage confirmacion =
            new(HttpMethod.Post, $"/api/v1/inventario/ajustes/{ajusteId}/confirmacion");

        confirmacion.Headers.TryAddWithoutValidation(Cabecera, Guid.NewGuid().ToString());

        using HttpResponseMessage confirmado = await cliente.SendAsync(confirmacion);

        confirmado.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(confirmado));

        (await confirmado.Content.ReadFromJsonAsync<AjusteDto>())!.Numero.ShouldBe(
            1, "el original gasta el primer correlativo, y es lo que hace que el 2 del inverso " +
               "signifique algo");

        return new UnCaso(cliente, empresa.Id, ajusteId, serie);
    }
}
