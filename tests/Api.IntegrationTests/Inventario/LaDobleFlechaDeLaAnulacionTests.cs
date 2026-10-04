using System.Data.Common;
using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La R2 en los dos sentidos: ningún inverso compensa a un documento que no está anulado, y
/// ningún anulado se queda sin exactamente un inverso.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta flecha tampoco la sostiene el motor, y no por falta de clave ajena.</b> La hay —es la
/// única de este módulo, porque apunta a la misma tabla y al mismo esquema—, y con ella el motor
/// garantiza que <c>anula_a_id</c> señala una fila que existe. Eso es todo lo que puede
/// garantizar: que esa fila esté en <c>Anulado</c> es una condición sobre el ESTADO de otra fila,
/// y que no haya dos inversos del mismo original es una condición sobre el NÚMERO de filas que
/// apuntan. Ninguna de las dos cabe en una restricción de columna.
/// </para>
/// <para>
/// <b>La segunda mitad SÍ la sostiene ahora el motor, y este fichero sigue haciendo falta.</b>
/// <c>ix_ajustes_anula_a_id</c> es único, así que dos inversos del mismo original no entran. Aquí
/// hubo un párrafo diciendo lo contrario —que el único estaba descartado porque convertiría el
/// <c>412</c> del perdedor en un <c>500</c>— y era una conclusión mal sacada de una medición
/// buena: cambiaba una garantía por un código de estado pudiendo tener las dos. La respuesta la
/// arregla <c>ManejadorDeCarreraPerdidaEnLaBase</c>, que traduce ESE índice por su nombre.
/// </para>
/// <para>
/// <b>Y el barrido se queda, de respaldo y no de adorno.</b> Lo que el índice no puede ver sigue
/// siendo suyo —un inverso cuyo original NO está anulado, y un anulado sin nadie que le apunte—,
/// y además es lo único que quedaría en pie si alguien tirara el índice. Por eso su mitad del
/// «exactamente uno» se arma <b>tirando el índice dentro de la transacción que luego se deshace</b>:
/// así se afirman las dos cosas por separado —que el índice lo impide, y que si no estuviera el
/// barrido lo vería— en vez de dejar una de las dos sin ejercer.
/// </para>
/// <para>
/// <b>Cada mitad va con su barrido y con su arnés</b>, que es lo que pide el ADR-0020. Un barrido
/// limpio puede estar diciendo «no hay nada roto» o «no he mirado», y desde fuera se ven iguales.
/// Así que cada caso afirma <b>cuántas filas ha mirado</b>, mete a propósito el defecto que esa
/// mitad tendría que ver y <b>exige que el barrido lo encuentre</b>, y solo después de deshacerlo
/// exige que salga limpio.
/// </para>
/// <para>
/// <b>El defecto se escribe a mano, y eso es parte de lo que se afirma.</b> El dominio no sabe
/// producir ninguno de los tres: <c>CrearInverso</c> se niega sobre un documento que no está
/// confirmado, <c>Anular</c> exige un inverso confirmado que apunte a ESE ajuste, y un segundo
/// inverso del mismo original no existe porque el primero ya lo dejó anulado. Que haya que bajar
/// a SQL crudo para fabricarlos <b>es</b> la evidencia de que el camino de producción no los
/// produce; que el barrido los vea es la evidencia de que serviría si alguien abriera otro.
/// </para>
/// <para>
/// <b>Los barridos van acotados a la empresa del caso</b>, que es una empresa recién inventada y
/// de la que no hay nada más en la base. No es un recuento global disfrazado: es el universo
/// entero de este caso, y ningún otro caso del carril puede meterle ni quitarle una fila.
/// </para>
/// <para>
/// <b>Un par de casos por cada tabla de documentos</b>, desde el ítem 2.11. La transferencia
/// tiene su propio enlace del par —<c>anula_a_id</c>, con su clave ajena a la misma tabla y su
/// índice único filtrado, <c>ix_transferencias_anula_a_id</c>—, y su propio nombre para el estado:
/// <c>Anulada</c>, y no <c>Anulado</c>. Un barrido escrito para los ajustes no miraría ninguna
/// transferencia, así que cada tabla lleva el suyo, con el mismo arnés.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaDobleFlechaDeLaAnulacionTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>La ida: todo inverso compensa a un documento que está anulado.</summary>
    /// <remarks>
    /// El defecto que esta mitad caza es la costura entre los dos pasos de la anulación: entre
    /// crear el inverso y dar el original por anulado hay un instante, y si algo se quedara ahí
    /// el libro tendría dos documentos que se compensan y un original que cualquier consulta
    /// sigue contando como vivo.
    /// </remarks>
    [Fact]
    public async Task Ningun_inverso_compensa_a_un_documento_que_no_esta_anulado()
    {
        UnAjusteConfirmado original = await UnParAnuladoAsync();

        long mirados = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.ajustes " +
                "WHERE empresa_id = '{0}' AND anula_a_id IS NOT NULL",
                original.EmpresaId));

        mirados.ShouldBe(
            1,
            "sin ningún inverso que mirar, «todos compensan a un anulado» sale verde por no " +
            "haber mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(original.EmpresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        // El arnés: se le devuelve al original el estado que tenía antes de anularse, dejando a su
        // inverso apuntando a un `Confirmado`. Es exactamente lo que quedaría si la anulación se
        // partiera por la mitad, y el dominio no sabe llegar ahí: `Anular` es lo último que pasa.
        await EjecutarAsync(
            contexto,
            transaccion,
            Consulta(
                "UPDATE inventario.ajustes SET estado = 'Confirmado' WHERE id = '{0}'",
                original.AjusteId));

        IReadOnlyList<string> sueltos = await LeerAsync(
            contexto, transaccion, InversosSinAnulado(original.EmpresaId));

        sueltos.Count.ShouldBe(
            1,
            "el barrido tenía que ver el inverso que se quedó compensando a un documento vivo: " +
            "si no lo ve, su verde de abajo no significa nada");

        await transaccion.RollbackAsync();

        (await ElLibro.TextosAsync(postgres, InversosSinAnulado(original.EmpresaId)))
            .ShouldBeEmpty();
    }

    /// <summary>La vuelta: todo anulado tiene exactamente un inverso apuntándole.</summary>
    /// <remarks>
    /// <b>Exactamente uno, y por eso el arnés es doble.</b> «Al menos uno» dejaría pasar dos
    /// inversos del mismo original —el daño que la anulación repetida haría— y «como mucho uno»
    /// dejaría pasar un anulado sin nada que lo compense, que es media R2. Las dos averías se
    /// meten, una detrás de otra, y las dos tienen que verse.
    /// </remarks>
    [Fact]
    public async Task Ningun_anulado_se_queda_sin_exactamente_un_inverso()
    {
        UnAjusteConfirmado original = await UnParAnuladoAsync();

        long mirados = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.ajustes " +
                "WHERE empresa_id = '{0}' AND estado = 'Anulado'",
                original.EmpresaId));

        mirados.ShouldBe(
            1,
            "sin ningún documento anulado que mirar, «todos tienen su inverso» sale verde por no " +
            "haber mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(original.EmpresaId);

        // PRIMERA AVERÍA: un segundo inverso del mismo original. Es lo que quedaría si dos
        // anulaciones simultáneas llegaran las dos hasta el final, y va en DOS pasos porque desde
        // el índice único hay dos cosas distintas que afirmar.
        string segundoInverso = Consulta(
            """
            INSERT INTO inventario.ajustes
                (id, empresa_id, almacen_id, fecha_de_operacion, motivo, divisa, creado_en,
                 modificado_en, estado, anula_a_id)
            VALUES ('{1}', '{0}', '{2}', current_date, 'Segundo inverso del mismo', 'EUR',
                    now(), now(), 'Confirmado', '{3}')
            """,
            original.EmpresaId,
            Guid.CreateVersion7(),
            original.AlmacenId,
            original.AjusteId);

        // PASO 1: la base no lo deja entrar, y se afirma POR EL NOMBRE DEL ÍNDICE. Sin el nombre,
        // cualquier otro rechazo —una columna que no existe, una clave ajena— daría el caso por
        // bueno sin haber ejercido la unicidad. Y el nombre es además el que traduce el borde: si
        // alguien lo cambia en una migración, este caso se pone rojo antes de que la traducción
        // deje de aplicarse en silencio.
        PostgresException rechazo = await ElLibro.ElMotorRechazaAsync(postgres, segundoInverso);

        rechazo.SqlState.ShouldBe(
            InversoDuplicado,
            "un segundo inverso del mismo original tiene que chocar contra la unicidad");
        rechazo.ConstraintName.ShouldBe("ix_ajustes_anula_a_id");

        // PASO 2: y si el índice no estuviera, el barrido lo vería. Se TIRA el índice dentro de la
        // transacción —en PostgreSQL el DDL es transaccional, así que el rollback lo devuelve— y
        // se mete el defecto que ya no cabe de otra forma. Sin este paso, la rama «más de uno» del
        // barrido no se ejercería nunca más y podría romperse sin que nadie se enterara.
        await using (IDbContextTransaction dosInversos =
            await contexto.Database.BeginTransactionAsync())
        {
            await EjecutarAsync(
                contexto, dosInversos, "DROP INDEX inventario.ix_ajustes_anula_a_id");

            await EjecutarAsync(contexto, dosInversos, segundoInverso);

            IReadOnlyList<string> mal = await LeerAsync(
                contexto, dosInversos, AnuladosSinUnSoloInverso(original.EmpresaId));

            mal.ShouldBe(
                [original.AjusteId.ToString()],
                "el barrido tenía que ver el original con DOS inversos: sin esto, «al menos uno» " +
                "y «exactamente uno» darían el mismo verde");

            await dosInversos.RollbackAsync();
        }

        // Y el índice ha vuelto: el rollback deshace también el DROP. Se comprueba, porque un
        // carril que se dejara el índice tirado envenenaría a todos los casos de detrás con un
        // verde que no significa nada.
        (await ElLibro.EscalarAsync<long>(
            postgres,
            "SELECT count(*) FROM pg_indexes WHERE schemaname = 'inventario' " +
            "AND indexname = 'ix_ajustes_anula_a_id'"))
            .ShouldBe(1, "el DROP iba dentro de una transacción que se deshace");

        // SEGUNDA AVERÍA: un anulado sin nadie que le apunte, que es la media R2 —un documento
        // sin efecto y su efecto sin compensar—. El dominio no llega aquí: `Anular` exige el
        // inverso por parámetro y lo exige confirmado.
        await using (IDbContextTransaction sinInverso =
            await contexto.Database.BeginTransactionAsync())
        {
            var anuladoAPelo = Guid.CreateVersion7();

            await EjecutarAsync(
                contexto,
                sinInverso,
                Consulta(
                    """
                    INSERT INTO inventario.ajustes
                        (id, empresa_id, almacen_id, fecha_de_operacion, motivo, divisa, creado_en,
                         modificado_en, estado)
                    VALUES ('{1}', '{0}', '{2}', current_date, 'Anulado sin nada que lo compense', 'EUR',
                            now(), now(), 'Anulado')
                    """,
                    original.EmpresaId,
                    anuladoAPelo,
                    original.AlmacenId));

            IReadOnlyList<string> mal = await LeerAsync(
                contexto, sinInverso, AnuladosSinUnSoloInverso(original.EmpresaId));

            mal.ShouldBe(
                [anuladoAPelo.ToString()],
                "el barrido tenía que ver el anulado que no tiene quien lo compense");

            await sinInverso.RollbackAsync();
        }

        (await ElLibro.TextosAsync(postgres, AnuladosSinUnSoloInverso(original.EmpresaId)))
            .ShouldBeEmpty();
    }

    /// <summary>La ida, para la transferencia: todo inverso compensa a una transferencia anulada.</summary>
    /// <remarks>
    /// La misma costura que en el ajuste: el inverso se confirma antes de que el original pase a
    /// <c>Anulada</c>, y una anulación partida por la mitad dejaría una enviada que sigue contando
    /// su tránsito con un inverso que ya lo ha devuelto al origen.
    /// </remarks>
    [Fact]
    public async Task Ninguna_transferencia_inversa_compensa_a_una_que_no_esta_anulada()
    {
        UnaTransferenciaAnulada original = await UnaTransferenciaAnuladaAsync();

        long mirados = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.transferencias " +
                "WHERE empresa_id = '{0}' AND anula_a_id IS NOT NULL",
                original.EmpresaId));

        mirados.ShouldBe(
            1,
            "sin ninguna transferencia inversa que mirar, «todas compensan a una anulada» sale " +
            "verde por no haber mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(original.EmpresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        // El arnés: el original vuelve a `Enviada`, el estado que tenía antes de anularse, y su
        // inverso se queda compensando a una transferencia viva.
        await EjecutarAsync(
            contexto,
            transaccion,
            Consulta(
                "UPDATE inventario.transferencias SET estado = 'Enviada' WHERE id = '{0}'",
                original.TransferenciaId));

        IReadOnlyList<string> sueltos = await LeerAsync(
            contexto, transaccion, TransferenciasInversasSinAnulada(original.EmpresaId));

        sueltos.Count.ShouldBe(
            1,
            "el barrido tenía que ver el inverso que se quedó compensando a una transferencia viva: " +
            "si no lo ve, su verde de abajo no significa nada");

        await transaccion.RollbackAsync();

        (await ElLibro.TextosAsync(postgres, TransferenciasInversasSinAnulada(original.EmpresaId)))
            .ShouldBeEmpty();
    }

    /// <summary>
    /// La vuelta, para la transferencia: toda transferencia anulada tiene exactamente un inverso
    /// apuntándole.
    /// </summary>
    /// <remarks>
    /// <b>El mismo arnés doble que en el ajuste</b>: el segundo inverso choca contra
    /// <c>ix_transferencias_anula_a_id</c>, que es el nombre que traduce el borde; sin el índice, el
    /// barrido lo ve; y una anulada sin inverso, que el índice no puede ver, la ve también.
    /// </remarks>
    [Fact]
    public async Task Ninguna_transferencia_anulada_se_queda_sin_exactamente_un_inverso()
    {
        UnaTransferenciaAnulada original = await UnaTransferenciaAnuladaAsync();

        long mirados = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.transferencias " +
                "WHERE empresa_id = '{0}' AND estado = 'Anulada'",
                original.EmpresaId));

        mirados.ShouldBe(
            1,
            "sin ninguna transferencia anulada que mirar, «todas tienen su inverso» sale verde por " +
            "no haber mirado (ADR-0020)");

        await using InventarioDbContext contexto = postgres.AbrirInventario(original.EmpresaId);

        // PRIMERA AVERÍA: un segundo inverso de la misma transferencia, recibido como nace el de
        // verdad. Sin número, que el índice de `(serie_id, numero)` no mira.
        string segundoInverso = Consulta(
            """
            INSERT INTO inventario.transferencias
                (id, empresa_id, serie_id, almacen_origen_id, almacen_destino_id, fecha_de_envio,
                 fecha_de_recepcion, divisa, creado_en, modificado_en, estado, anula_a_id)
            VALUES ('{1}', '{0}', '{1}', '{2}', '{3}', current_date, current_date, 'EUR', now(), now(),
                    'Recibida', '{4}')
            """,
            original.EmpresaId,
            Guid.CreateVersion7(),
            original.Claves.AlmacenOrigenId,
            original.Claves.AlmacenDestinoId,
            original.TransferenciaId);

        PostgresException rechazo = await ElLibro.ElMotorRechazaAsync(postgres, segundoInverso);

        rechazo.SqlState.ShouldBe(
            InversoDuplicado,
            "un segundo inverso de la misma transferencia tiene que chocar contra la unicidad");
        rechazo.ConstraintName.ShouldBe("ix_transferencias_anula_a_id");

        await using (IDbContextTransaction dosInversos =
            await contexto.Database.BeginTransactionAsync())
        {
            await EjecutarAsync(
                contexto, dosInversos, "DROP INDEX inventario.ix_transferencias_anula_a_id");

            await EjecutarAsync(contexto, dosInversos, segundoInverso);

            IReadOnlyList<string> mal = await LeerAsync(
                contexto, dosInversos, TransferenciasAnuladasSinUnSoloInverso(original.EmpresaId));

            mal.ShouldBe(
                [original.TransferenciaId.ToString()],
                "el barrido tenía que ver la transferencia con DOS inversos: sin esto, «al menos " +
                "uno» y «exactamente uno» darían el mismo verde");

            await dosInversos.RollbackAsync();
        }

        (await ElLibro.EscalarAsync<long>(
            postgres,
            "SELECT count(*) FROM pg_indexes WHERE schemaname = 'inventario' " +
            "AND indexname = 'ix_transferencias_anula_a_id'"))
            .ShouldBe(1, "el DROP iba dentro de una transacción que se deshace");

        // SEGUNDA AVERÍA: una anulada sin nadie que le apunte, que el dominio no sabe construir:
        // `Anular` exige el inverso por parámetro, ya confirmado.
        await using (IDbContextTransaction sinInverso =
            await contexto.Database.BeginTransactionAsync())
        {
            var anuladaAPelo = Guid.CreateVersion7();

            await EjecutarAsync(
                contexto,
                sinInverso,
                Consulta(
                    """
                    INSERT INTO inventario.transferencias
                        (id, empresa_id, serie_id, almacen_origen_id, almacen_destino_id,
                         fecha_de_envio, divisa, creado_en, modificado_en, estado)
                    VALUES ('{1}', '{0}', '{1}', '{2}', '{3}', current_date, 'EUR', now(), now(),
                            'Anulada')
                    """,
                    original.EmpresaId,
                    anuladaAPelo,
                    original.Claves.AlmacenOrigenId,
                    original.Claves.AlmacenDestinoId));

            IReadOnlyList<string> mal = await LeerAsync(
                contexto, sinInverso, TransferenciasAnuladasSinUnSoloInverso(original.EmpresaId));

            mal.ShouldBe(
                [anuladaAPelo.ToString()],
                "el barrido tenía que ver la anulada que no tiene quien la compense");

            await sinInverso.RollbackAsync();
        }

        (await ElLibro.TextosAsync(postgres, TransferenciasAnuladasSinUnSoloInverso(original.EmpresaId)))
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Un ajuste confirmado y anulado con su inverso, por el camino de producción, en una empresa
    /// recién inventada.
    /// </summary>
    /// <returns>Lo que dejó escrito el ORIGINAL, que es por donde se entra a buscar el par.</returns>
    private async Task<UnAjusteConfirmado> UnParAnuladoAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        UnAjusteConfirmado original =
            await ElLibro.ConfirmarUnAjusteAsync(postgres, hoy, lineas: 2);

        await ElLibro.AnularUnAjusteAsync(postgres, original, hoy.AddDays(1));

        return original;
    }

    /// <summary>
    /// Una transferencia enviada y anulada con su inverso, por el camino de producción, en una
    /// empresa recién inventada.
    /// </summary>
    /// <returns>La transferencia original, con sus claves.</returns>
    private async Task<UnaTransferenciaAnulada> UnaTransferenciaAnuladaAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var empresaId = Guid.CreateVersion7();
        var claves = ClavesDeUnaTransferencia.Inventadas();

        await ElLibro.EntrarEnElOrigenAsync(postgres, empresaId, claves, 5m, 2m, hoy);

        Guid transferenciaId = await ElLibro.EnviarSinLaApiAsync(postgres, empresaId, claves, 3m, hoy);

        await ElLibro.AnularSinLaApiAsync(postgres, empresaId, transferenciaId, hoy);

        return new UnaTransferenciaAnulada(empresaId, transferenciaId, claves);
    }

    /// <summary>El SQLSTATE de una violación de unicidad, <c>23505</c>.</summary>
    /// <remarks>
    /// Escrito y no calculado, como el <c>23001</c> del libro: el catálogo de códigos de
    /// PostgreSQL es contrato suyo. Es el mismo que <c>ManejadorDeCarreraPerdidaEnLaBase</c>
    /// reconoce para traducir la carrera perdida a <c>412</c>.
    /// </remarks>
    private const string InversoDuplicado = "23505";

    /// <summary>Inversos que compensan a un documento que no está anulado, o que no está.</summary>
    /// <remarks>
    /// Va con <c>LEFT JOIN</c> y no con el interno, aunque la clave ajena garantice la fila: con
    /// el interno, un inverso apuntando a la nada se caería del resultado y el barrido diría que
    /// todo está bien. La clave ajena está para que eso no pase; esta consulta está para que se
    /// vea si algún día deja de estar.
    /// </remarks>
    /// <param name="empresaId">La empresa del caso, que es su universo entero.</param>
    /// <returns>La consulta.</returns>
    private static string InversosSinAnulado(Guid empresaId) => Consulta(
        """
        SELECT inverso.id::text
        FROM inventario.ajustes AS inverso
        LEFT JOIN inventario.ajustes AS original ON original.id = inverso.anula_a_id
        WHERE inverso.empresa_id = '{0}'
          AND inverso.anula_a_id IS NOT NULL
          AND (original.id IS NULL OR original.estado <> 'Anulado')
        """,
        empresaId);

    /// <summary>Anulados a los que no apunta exactamente un inverso.</summary>
    /// <param name="empresaId">La empresa del caso, que es su universo entero.</param>
    /// <returns>La consulta.</returns>
    private static string AnuladosSinUnSoloInverso(Guid empresaId) => Consulta(
        """
        SELECT original.id::text
        FROM inventario.ajustes AS original
        WHERE original.empresa_id = '{0}'
          AND original.estado = 'Anulado'
          AND (SELECT count(*) FROM inventario.ajustes AS inverso
               WHERE inverso.anula_a_id = original.id) <> 1
        """,
        empresaId);

    /// <summary>Transferencias inversas que compensan a una que no está anulada, o que no está.</summary>
    /// <remarks>Con <c>LEFT JOIN</c>, por lo mismo que en el ajuste.</remarks>
    /// <param name="empresaId">La empresa del caso, que es su universo entero.</param>
    /// <returns>La consulta.</returns>
    private static string TransferenciasInversasSinAnulada(Guid empresaId) => Consulta(
        """
        SELECT inverso.id::text
        FROM inventario.transferencias AS inverso
        LEFT JOIN inventario.transferencias AS original ON original.id = inverso.anula_a_id
        WHERE inverso.empresa_id = '{0}'
          AND inverso.anula_a_id IS NOT NULL
          AND (original.id IS NULL OR original.estado <> 'Anulada')
        """,
        empresaId);

    /// <summary>Transferencias anuladas a las que no apunta exactamente un inverso.</summary>
    /// <param name="empresaId">La empresa del caso, que es su universo entero.</param>
    /// <returns>La consulta.</returns>
    private static string TransferenciasAnuladasSinUnSoloInverso(Guid empresaId) => Consulta(
        """
        SELECT original.id::text
        FROM inventario.transferencias AS original
        WHERE original.empresa_id = '{0}'
          AND original.estado = 'Anulada'
          AND (SELECT count(*) FROM inventario.transferencias AS inverso
               WHERE inverso.anula_a_id = original.id) <> 1
        """,
        empresaId);

    private static string Consulta(string plantilla, params object[] valores) =>
        string.Format(CultureInfo.InvariantCulture, plantilla, valores);

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

/// <summary>Una transferencia anulada sin la API, con lo que hace falta para ir a buscarla.</summary>
/// <param name="EmpresaId">La empresa inventada del caso (R8).</param>
/// <param name="TransferenciaId">La original, ya anulada.</param>
/// <param name="Claves">Sus dos almacenes, sus huecos y su artículo.</param>
internal sealed record UnaTransferenciaAnulada(
    Guid EmpresaId,
    Guid TransferenciaId,
    ClavesDeUnaTransferencia Claves);
