using System.Data.Common;
using System.Globalization;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La R13 en los dos sentidos: ninguna fila del libro sin documento, ningún documento confirmado
/// sin fila del libro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta flecha no la sostiene el motor, y por eso hace falta este fichero.</b> El documento
/// origen es un par «tipo + identificador», y ninguna clave ajena puede expresar «apunta a la
/// tabla que diga esta otra columna»: los documentos de inventario viven en tablas distintas y un
/// ajuste no es un recuento. Lo que sostiene la integridad de la flecha es la regla que la
/// comprueba, y es esta.
/// </para>
/// <para>
/// <b>Cada mitad se comprueba con su barrido y con su arnés</b>, que es lo que pide el ADR-0020.
/// Un barrido que no encuentra nada roto puede estar diciendo dos cosas muy distintas —«no hay
/// nada roto» o «no he mirado»— y desde fuera se ven iguales. Así que cada caso hace tres cosas:
/// afirma <b>cuántas filas ha mirado</b>, mete a propósito la fila rota que la mitad contraria
/// tendría que ver y <b>exige que el barrido la encuentre</b>, y solo después de deshacer eso
/// exige que el barrido salga limpio.
/// </para>
/// <para>
/// <b>La fila rota se escribe a mano, y eso es parte de lo que se afirma.</b> El dominio no sabe
/// producir ninguna de las dos: un movimiento nace únicamente de confirmar un documento —el ajuste,
/// o el envío, la recepción y el inverso de la transferencia—, que le pone el documento del que
/// sale, y un documento sin líneas no se confirma —lanza—. Que haya que bajar a SQL crudo para
/// fabricar el defecto <b>es</b> la evidencia de que el camino de producción no lo produce; y que
/// el barrido lo vea es la evidencia de que serviría si alguien abriera otro camino.
/// </para>
/// <para>
/// <b>Un par de casos por cada tabla de documentos</b>, desde el ítem 2.11: el tipo de la flecha
/// dice a qué tabla mira, y un barrido que solo uniera con <c>ajustes</c> daría por huérfana toda
/// fila de una transferencia, o no la miraría nunca si filtrara por el tipo. Cada par filtra por el
/// suyo.
/// </para>
/// <para>
/// <b>Y el par del albarán, desde el ítem 2.13</b>, que no tiene tabla en este módulo: ninguna
/// consulta cruza esquemas, y su flecha se cierra contra <c>consumos_de_reserva</c>, que guarda el
/// albarán que consumió cada reserva (ADR-0059 §8). Los dos casos montan su escena por la API, con
/// las semillas 852 y 853 del bloque del 2.13 que cuenta la cabecera de <c>LasReservasTests</c>.
/// </para>
/// <para>
/// <b>Los barridos van acotados a la empresa del caso</b>, que es una empresa recién inventada y
/// de la que no hay nada más en la base. No es un recuento global disfrazado: es el universo
/// entero de este caso, y ningún otro caso del carril puede meterle ni quitarle una fila.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaDobleFlechaDelLibroTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>La ida: de toda fila del libro se llega a un documento que existe.</summary>
    [Fact]
    public async Task Ninguna_fila_del_libro_apunta_a_un_documento_que_no_existe()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        UnAjusteConfirmado confirmado = await ElLibro.ConfirmarUnAjusteAsync(postgres, hoy, lineas: 2);

        long miradas = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.movimiento_stock WHERE empresa_id = '{0}'",
                confirmado.EmpresaId));

        miradas.ShouldBe(
            2,
            "sin filas del libro que mirar, «ninguna apunta a un documento que no existe» sale " +
            "verde por no haber mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(confirmado.EmpresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        // El arnés: una fila con un documento inventado. Va dentro de la transacción que se
        // deshace porque el libro es de solo añadido — una vez confirmada, no habría forma de
        // quitarla, y el barrido de abajo se quedaría rojo para siempre.
        var documentoQueNoExiste = Guid.CreateVersion7();

        contexto.Movimientos.Add(MovimientoStock.Registrar(
            confirmado.EmpresaId,
            hoy,
            confirmado.AlmacenId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            null,
            1m,
            Guid.CreateVersion7(),
            1m,
            "EUR",
            Importe.De(1m, "EUR"),
            Importe.De(1m, "EUR"),
            PrecioUnitario.De(1m, "EUR"),
            TipoDeDocumentoOrigen.Ajuste,
            documentoQueNoExiste,
            DateTimeOffset.UtcNow));

        await contexto.SaveChangesAsync();

        IReadOnlyList<string> huerfanas = await HuerfanasAsync(contexto, transaccion, confirmado.EmpresaId);

        huerfanas.ShouldBe(
            [documentoQueNoExiste.ToString()],
            "el barrido tenía que ver la fila huérfana que acaba de escribirse: si no la ve, su " +
            "verde de abajo no significa nada");

        await transaccion.RollbackAsync();

        IReadOnlyList<string> despues = await ElLibro.TextosAsync(
            postgres,
            Consulta(
                """
                SELECT fila.documento_origen_id::text
                FROM inventario.movimiento_stock AS fila
                LEFT JOIN inventario.ajustes AS documento ON documento.id = fila.documento_origen_id
                WHERE fila.empresa_id = '{0}'
                  AND fila.documento_origen_tipo = 'Ajuste'
                  AND documento.id IS NULL
                """,
                confirmado.EmpresaId));

        despues.ShouldBeEmpty();
    }

    /// <summary>La vuelta: todo documento confirmado dejó al menos una fila del libro.</summary>
    /// <remarks>
    /// Esta mitad la sostiene además el dominio, antes de que la fila exista:
    /// <c>Ajuste.Confirmar</c> lanza sobre un documento sin líneas, y eso lo comprueban los tests
    /// unitarios del agregado. Aquí se mira lo que hay <b>en la base</b>, que es donde acabarían
    /// las filas si algún día se abriera otro camino para confirmar — una importación, una
    /// migración de datos, un caso de uso nuevo—.
    /// </remarks>
    [Fact]
    public async Task Ningun_ajuste_confirmado_se_queda_sin_una_sola_fila_del_libro()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        UnAjusteConfirmado confirmado = await ElLibro.ConfirmarUnAjusteAsync(postgres, hoy, lineas: 1);

        long mirados = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.ajustes " +
                "WHERE empresa_id = '{0}' AND estado = 'Confirmado'",
                confirmado.EmpresaId));

        mirados.ShouldBe(
            1,
            "sin ningún documento confirmado que mirar, «todos tienen su fila» sale verde por no " +
            "haber mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(confirmado.EmpresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        // El arnés, y aquí el SQL crudo dice algo: un ajuste confirmado sin líneas NO SE PUEDE
        // construir con el dominio —`Confirmar` lanza—, así que la única manera de fabricar el
        // defecto es escribir la fila a mano. Eso es exactamente lo que se quería saber.
        var confirmadoSinFilas = Guid.CreateVersion7();

        await EjecutarAsync(
            contexto,
            transaccion,
            Consulta(
                """
                INSERT INTO inventario.ajustes
                    (id, empresa_id, almacen_id, fecha_de_operacion, motivo, divisa, creado_en,
                     modificado_en, estado)
                VALUES ('{1}', '{0}', '{2}', current_date, 'Confirmado sin mover el libro', 'EUR',
                        now(), now(), 'Confirmado')
                """,
                confirmado.EmpresaId,
                confirmadoSinFilas,
                confirmado.AlmacenId));

        IReadOnlyList<string> sinFilas = await SinMovimientosAsync(
            contexto, transaccion, confirmado.EmpresaId);

        sinFilas.ShouldBe(
            [confirmadoSinFilas.ToString()],
            "el barrido tenía que ver el documento confirmado que no movió el libro");

        await transaccion.RollbackAsync();

        IReadOnlyList<string> despues = await ElLibro.TextosAsync(
            postgres,
            Consulta(
                """
                SELECT documento.id::text
                FROM inventario.ajustes AS documento
                WHERE documento.empresa_id = '{0}'
                  AND documento.estado = 'Confirmado'
                  AND NOT EXISTS (
                      SELECT 1 FROM inventario.movimiento_stock AS fila
                      WHERE fila.documento_origen_tipo = 'Ajuste'
                        AND fila.documento_origen_id = documento.id)
                """,
                confirmado.EmpresaId));

        despues.ShouldBeEmpty();
    }

    /// <summary>La ida, para la transferencia: de toda fila suya se llega a una transferencia que existe.</summary>
    [Fact]
    public async Task Ninguna_fila_del_libro_apunta_a_una_transferencia_que_no_existe()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var empresaId = Guid.CreateVersion7();
        var claves = ClavesDeUnaTransferencia.Inventadas();

        await ElLibro.EntrarEnElOrigenAsync(postgres, empresaId, claves, 5m, 2m, hoy);
        await ElLibro.EnviarSinLaApiAsync(postgres, empresaId, claves, 3m, hoy);

        long miradas = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.movimiento_stock " +
                "WHERE empresa_id = '{0}' AND documento_origen_tipo = 'Transferencia'",
                empresaId));

        miradas.ShouldBe(
            1,
            "sin filas de una transferencia que mirar, «ninguna apunta a una que no existe» sale " +
            "verde por no haber mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        // El arnés: una fila de transferencia con un documento inventado, en la transacción que se
        // deshace, por lo mismo que en el ajuste.
        var documentoQueNoExiste = Guid.CreateVersion7();

        contexto.Movimientos.Add(MovimientoStock.Registrar(
            empresaId,
            hoy,
            claves.AlmacenDestinoId,
            claves.UbicacionDestinoId,
            claves.ArticuloId,
            null,
            null,
            1m,
            claves.UnidadId,
            1m,
            "EUR",
            Importe.De(1m, "EUR"),
            Importe.De(1m, "EUR"),
            PrecioUnitario.De(1m, "EUR"),
            TipoDeDocumentoOrigen.Transferencia,
            documentoQueNoExiste,
            DateTimeOffset.UtcNow));

        await contexto.SaveChangesAsync();

        IReadOnlyList<string> huerfanas = await LeerAsync(
            contexto, transaccion, Consulta(TransferenciasHuerfanas, empresaId));

        huerfanas.ShouldBe(
            [documentoQueNoExiste.ToString()],
            "el barrido tenía que ver la fila huérfana que acaba de escribirse, y solo esa: la del " +
            "envío apunta a su transferencia");

        await transaccion.RollbackAsync();

        (await ElLibro.TextosAsync(postgres, Consulta(TransferenciasHuerfanas, empresaId))).ShouldBeEmpty();
    }

    /// <summary>
    /// La vuelta, para la transferencia: toda transferencia que ha salido de borrador dejó al menos
    /// una fila del libro.
    /// </summary>
    /// <remarks>
    /// <b>«Ha salido de borrador», y no «enviada»</b>: una recibida y una anulada también se
    /// enviaron, y el inverso nace recibido. Todas movieron el origen; ninguna puede estar sin filas.
    /// </remarks>
    [Fact]
    public async Task Ninguna_transferencia_fuera_de_borrador_se_queda_sin_una_sola_fila_del_libro()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var empresaId = Guid.CreateVersion7();
        var claves = ClavesDeUnaTransferencia.Inventadas();

        await ElLibro.EntrarEnElOrigenAsync(postgres, empresaId, claves, 5m, 2m, hoy);
        await ElLibro.EnviarSinLaApiAsync(postgres, empresaId, claves, 3m, hoy);

        long mirados = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.transferencias " +
                "WHERE empresa_id = '{0}' AND estado <> 'Borrador'",
                empresaId));

        mirados.ShouldBe(
            1,
            "sin ninguna transferencia enviada que mirar, «todas tienen su fila» sale verde por no " +
            "haber mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        // El arnés: una transferencia enviada sin líneas, que el dominio no sabe construir —enviar
        // un documento sin líneas lanza—, escrita a mano.
        var enviadaSinFilas = Guid.CreateVersion7();

        await EjecutarAsync(
            contexto,
            transaccion,
            Consulta(
                """
                INSERT INTO inventario.transferencias
                    (id, empresa_id, serie_id, almacen_origen_id, almacen_destino_id, fecha_de_envio,
                     divisa, creado_en, modificado_en, estado)
                VALUES ('{1}', '{0}', '{1}', '{2}', '{3}', current_date, 'EUR', now(), now(), 'Enviada')
                """,
                empresaId,
                enviadaSinFilas,
                claves.AlmacenOrigenId,
                claves.AlmacenDestinoId));

        IReadOnlyList<string> sinFilas = await LeerAsync(
            contexto, transaccion, Consulta(TransferenciasSinMovimientos, empresaId));

        sinFilas.ShouldBe(
            [enviadaSinFilas.ToString()],
            "el barrido tenía que ver la transferencia enviada que no movió el libro");

        await transaccion.RollbackAsync();

        (await ElLibro.TextosAsync(postgres, Consulta(TransferenciasSinMovimientos, empresaId))).ShouldBeEmpty();
    }

    /// <summary>
    /// La ida, para el albarán: de toda fila suya se llega a un consumo de reserva con ese
    /// documento, de una reserva de su mismo artículo y almacén (ADR-0059 §8).
    /// </summary>
    /// <remarks>
    /// <b>El arnés lleva dos filas rotas</b>: una con un albarán inventado, y otra con el albarán de
    /// verdad pero en otro almacén. La segunda solo la ve un barrido que mira el almacén, y es la
    /// mitad de la flecha que no da el documento solo.
    /// </remarks>
    [Fact]
    public async Task Ninguna_fila_de_un_albaran_se_queda_sin_el_consumo_que_la_saco()
    {
        using ApiDeVerdad api = new(postgres);
        (EscenaDeTransferencia escena, Guid albaranId, _) = await ConsumirUnaReservaAsync(api, 852, "DFA-A");

        long miradas = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.movimiento_stock " +
                "WHERE empresa_id = '{0}' AND documento_origen_tipo = 'Albaran'",
                escena.EmpresaId));

        miradas.ShouldBe(
            1,
            "sin filas de un albarán que mirar, «todas tienen su consumo» sale verde por no haber " +
            "mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        // El arnés, en la transacción que se deshace, por lo mismo que en el ajuste.
        var albaranQueNoConsumio = Guid.CreateVersion7();

        contexto.Movimientos.Add(UnaEntradaDeAlbaran(escena, escena.AlmacenA, escena.UbicacionA, albaranQueNoConsumio));
        contexto.Movimientos.Add(UnaEntradaDeAlbaran(escena, escena.AlmacenB, escena.UbicacionB, albaranId));

        await contexto.SaveChangesAsync();

        IReadOnlyList<string> huerfanas = await LeerAsync(
            contexto, transaccion, Consulta(AlbaranesSinConsumo, escena.EmpresaId));

        huerfanas.ShouldBe(
            [albaranQueNoConsumio.ToString(), albaranId.ToString()],
            ignoreOrder: true,
            "el barrido tenía que ver las dos filas rotas, y solo esas: la del consumo apunta a él");

        await transaccion.RollbackAsync();

        (await ElLibro.TextosAsync(postgres, Consulta(AlbaranesSinConsumo, escena.EmpresaId))).ShouldBeEmpty();
    }

    /// <summary>
    /// La vuelta, para el albarán: todo consumo de reserva dejó al menos una fila del libro con su
    /// documento, su artículo y su almacén (ADR-0059 §8).
    /// </summary>
    /// <remarks>
    /// <b>El consumo roto tiene una fila con su documento, pero en otro almacén</b>: el dominio no
    /// sabe producir ni eso ni un consumo sin filas, porque los dos caen en el mismo <c>COMMIT</c>
    /// (ADR-0059 §6). Un barrido que solo mirara el documento lo daría por bueno.
    /// </remarks>
    [Fact]
    public async Task Ningun_consumo_de_reserva_se_queda_sin_su_fila_del_libro()
    {
        using ApiDeVerdad api = new(postgres);
        (EscenaDeTransferencia escena, _, Guid reservaId) = await ConsumirUnaReservaAsync(api, 853, "DFA-B");

        long mirados = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.consumos_de_reserva AS consumo " +
                "JOIN inventario.reservas AS reserva ON reserva.id = consumo.reserva_id " +
                "WHERE reserva.empresa_id = '{0}'",
                escena.EmpresaId));

        mirados.ShouldBe(
            1,
            "sin ningún consumo que mirar, «todos tienen su fila» sale verde por no haber mirado " +
            "(ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        // El arnés: una fila del libro con un albarán inventado, en otro almacén, y un consumo de la
        // reserva de verdad con ese mismo albarán, escrito a mano.
        var albaranEnOtroAlmacen = Guid.CreateVersion7();

        contexto.Movimientos.Add(UnaEntradaDeAlbaran(escena, escena.AlmacenB, escena.UbicacionB, albaranEnOtroAlmacen));

        await contexto.SaveChangesAsync();

        await EjecutarAsync(
            contexto,
            transaccion,
            Consulta(
                """
                INSERT INTO inventario.consumos_de_reserva
                    (id, reserva_id, documento_tipo, documento_id, fecha_de_operacion, cantidad,
                     creado_en, modificado_en)
                VALUES ('{0}', '{1}', 'Albaran', '{2}', current_date, 1, now(), now())
                """,
                Guid.CreateVersion7(),
                reservaId,
                albaranEnOtroAlmacen));

        IReadOnlyList<string> sinFilas = await LeerAsync(
            contexto, transaccion, Consulta(ConsumosSinMovimientos, escena.EmpresaId));

        sinFilas.ShouldBe(
            [albaranEnOtroAlmacen.ToString()],
            "el barrido tenía que ver el consumo cuya fila está en otro almacén, y solo ese");

        await transaccion.RollbackAsync();

        (await ElLibro.TextosAsync(postgres, Consulta(ConsumosSinMovimientos, escena.EmpresaId))).ShouldBeEmpty();
    }

    private static string Consulta(string plantilla, params object[] valores) =>
        string.Format(CultureInfo.InvariantCulture, plantilla, valores);

    private const string TransferenciasHuerfanas =
        """
        SELECT fila.documento_origen_id::text
        FROM inventario.movimiento_stock AS fila
        LEFT JOIN inventario.transferencias AS documento ON documento.id = fila.documento_origen_id
        WHERE fila.empresa_id = '{0}'
          AND fila.documento_origen_tipo = 'Transferencia'
          AND documento.id IS NULL
        """;

    private const string TransferenciasSinMovimientos =
        """
        SELECT documento.id::text
        FROM inventario.transferencias AS documento
        WHERE documento.empresa_id = '{0}'
          AND documento.estado <> 'Borrador'
          AND NOT EXISTS (
              SELECT 1 FROM inventario.movimiento_stock AS fila
              WHERE fila.documento_origen_tipo = 'Transferencia'
                AND fila.documento_origen_id = documento.id)
        """;

    private const string AlbaranesSinConsumo =
        """
        SELECT fila.documento_origen_id::text
        FROM inventario.movimiento_stock AS fila
        WHERE fila.empresa_id = '{0}'
          AND fila.documento_origen_tipo = 'Albaran'
          AND NOT EXISTS (
              SELECT 1
              FROM inventario.consumos_de_reserva AS consumo
              JOIN inventario.reservas AS reserva ON reserva.id = consumo.reserva_id
              WHERE consumo.documento_tipo = fila.documento_origen_tipo
                AND consumo.documento_id = fila.documento_origen_id
                AND reserva.empresa_id = fila.empresa_id
                AND reserva.articulo_id = fila.articulo_id
                AND reserva.almacen_id = fila.almacen_id)
        """;

    private const string ConsumosSinMovimientos =
        """
        SELECT consumo.documento_id::text
        FROM inventario.consumos_de_reserva AS consumo
        JOIN inventario.reservas AS reserva ON reserva.id = consumo.reserva_id
        WHERE reserva.empresa_id = '{0}'
          AND NOT EXISTS (
              SELECT 1 FROM inventario.movimiento_stock AS fila
              WHERE fila.empresa_id = reserva.empresa_id
                AND fila.documento_origen_tipo = consumo.documento_tipo
                AND fila.documento_origen_id = consumo.documento_id
                AND fila.articulo_id = reserva.articulo_id
                AND fila.almacen_id = reserva.almacen_id)
        """;

    // Una fila de albarán que mete una unidad en el hueco que se diga, escrita a mano.
    private static MovimientoStock UnaEntradaDeAlbaran(
        EscenaDeTransferencia escena, Guid almacenId, Guid ubicacionId, Guid albaranId) =>
        MovimientoStock.Registrar(
            escena.EmpresaId,
            EscenaDeTransferencia.Hoy,
            almacenId,
            ubicacionId,
            escena.ArticuloId,
            null,
            null,
            1m,
            escena.UnidadId,
            1m,
            "EUR",
            Importe.De(1m, "EUR"),
            Importe.De(1m, "EUR"),
            PrecioUnitario.De(1m, "EUR"),
            TipoDeDocumentoOrigen.Albaran,
            albaranId,
            DateTimeOffset.UtcNow);

    // Una reserva de 5 sobre 10 de físico, consumida en 2 por un albarán: una fila del libro y un
    // consumo, que es lo que cada mitad de la flecha mira.
    private async Task<(EscenaDeTransferencia Escena, Guid AlbaranId, Guid ReservaId)> ConsumirUnaReservaAsync(
        ApiDeVerdad api, int semilla, string codigo)
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(api, semilla, codigo, semilla);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2m);

        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId);

        OrigenDeLaReserva origen = LasReservas.OrigenNuevo();
        ReservaDto reservada = await LasReservas.ReservadaAsync(modulo, escena, origen, 5m);

        var albaranId = Guid.CreateVersion7();
        LasReservas.Exigir(await LasReservas.ConsumirAsync(
            modulo, origen, albaranId, EscenaDeTransferencia.Hoy, new LineaDeConsumoDto(escena.UbicacionA, 2m)));

        return (escena, albaranId, reservada.Id);
    }

    private static Task<IReadOnlyList<string>> HuerfanasAsync(
        InventarioDbContext contexto,
        IDbContextTransaction transaccion,
        Guid empresaId) =>
        LeerAsync(
            contexto,
            transaccion,
            Consulta(
                """
                SELECT fila.documento_origen_id::text
                FROM inventario.movimiento_stock AS fila
                LEFT JOIN inventario.ajustes AS documento ON documento.id = fila.documento_origen_id
                WHERE fila.empresa_id = '{0}'
                  AND fila.documento_origen_tipo = 'Ajuste'
                  AND documento.id IS NULL
                """,
                empresaId));

    private static Task<IReadOnlyList<string>> SinMovimientosAsync(
        InventarioDbContext contexto,
        IDbContextTransaction transaccion,
        Guid empresaId) =>
        LeerAsync(
            contexto,
            transaccion,
            Consulta(
                """
                SELECT documento.id::text
                FROM inventario.ajustes AS documento
                WHERE documento.empresa_id = '{0}'
                  AND documento.estado = 'Confirmado'
                  AND NOT EXISTS (
                      SELECT 1 FROM inventario.movimiento_stock AS fila
                      WHERE fila.documento_origen_tipo = 'Ajuste'
                        AND fila.documento_origen_id = documento.id)
                """,
                empresaId));

    private static async Task<IReadOnlyList<string>> LeerAsync(
        InventarioDbContext contexto,
        IDbContextTransaction transaccion,
        string consulta)
    {
        await using DbCommand orden = contexto.Database.GetDbConnection().CreateCommand();
        orden.CommandText = consulta;
        orden.Transaction = transaccion.GetDbTransaction();

        await using DbDataReader lector = await orden.ExecuteReaderAsync();

        List<string> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add(lector.GetString(0));
        }

        return filas;
    }

    private static async Task EjecutarAsync(
        InventarioDbContext contexto,
        IDbContextTransaction transaccion,
        string sentencia)
    {
        await using DbCommand orden = contexto.Database.GetDbConnection().CreateCommand();
        orden.CommandText = sentencia;
        orden.Transaction = transaccion.GetDbTransaction();

        await orden.ExecuteNonQueryAsync();
    }
}
