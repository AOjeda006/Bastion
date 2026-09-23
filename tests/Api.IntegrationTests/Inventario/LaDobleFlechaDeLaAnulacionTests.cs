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
                (id, empresa_id, almacen_id, fecha_de_operacion, motivo, creado_en,
                 modificado_en, estado, anula_a_id)
            VALUES ('{1}', '{0}', '{2}', current_date, 'Segundo inverso del mismo',
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
                        (id, empresa_id, almacen_id, fecha_de_operacion, motivo, creado_en,
                         modificado_en, estado)
                    VALUES ('{1}', '{0}', '{2}', current_date, 'Anulado sin nada que lo compense',
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

    /// <summary>Inversos que compensan a un documento que no está anulado, o que no está.</summary>
    /// <remarks>
    /// Va con <c>LEFT JOIN</c> y no con el interno, aunque la clave ajena garantice la fila: con
    /// el interno, un inverso apuntando a la nada se caería del resultado y el barrido diría que
    /// todo está bien. La clave ajena está para que eso no pase; esta consulta está para que se
    /// vea si algún día deja de estar.
    /// </remarks>
    /// <param name="empresaId">La empresa del caso, que es su universo entero.</param>
    /// <returns>La consulta.</returns>
    /// <summary>El SQLSTATE de una violación de unicidad, <c>23505</c>.</summary>
    /// <remarks>
    /// Escrito y no calculado, como el <c>23001</c> del libro: el catálogo de códigos de
    /// PostgreSQL es contrato suyo. Es el mismo que <c>ManejadorDeCarreraPerdidaEnLaBase</c>
    /// reconoce para traducir la carrera perdida a <c>412</c>.
    /// </remarks>
    private const string InversoDuplicado = "23505";

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
