using System.Data.Common;
using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
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
/// producir ninguna de las dos: un movimiento nace únicamente de <c>Ajuste.Confirmar</c>, que le
/// pone el documento del que sale, y un ajuste sin líneas no se confirma —lanza—. Que haya que
/// bajar a SQL crudo para fabricar el defecto <b>es</b> la evidencia de que el camino de
/// producción no lo produce; y que el barrido lo vea es la evidencia de que serviría si alguien
/// abriera otro camino.
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
            1m,
            Guid.CreateVersion7(),
            1m,
            Importe.De(1m, "EUR"),
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
                    (id, empresa_id, almacen_id, fecha_de_operacion, motivo, creado_en,
                     modificado_en, estado)
                VALUES ('{1}', '{0}', '{2}', current_date, 'Confirmado sin mover el libro',
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

    private static string Consulta(string plantilla, params object[] valores) =>
        string.Format(CultureInfo.InvariantCulture, plantilla, valores);

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
