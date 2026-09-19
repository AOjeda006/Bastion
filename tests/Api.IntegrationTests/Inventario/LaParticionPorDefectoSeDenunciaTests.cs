using System.Data.Common;
using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Un mes sin partición cae en la de por defecto, no se pierde — y eso es una avería que hay que
/// denunciar, no una red de la que colgarse.
/// </summary>
/// <remarks>
/// <para>
/// <b>AVISO, Y HAY QUE LEERLO ANTES DE TOCAR ESTE FICHERO.</b> Una fila confirmada que llegue a la
/// partición por defecto <b>no se puede quitar</b>: <c>DELETE</c> y <c>TRUNCATE</c> están cerrados
/// a propósito sobre el libro entero, así que no hay ninguna sentencia que la borre — ni desde EF
/// Core, ni desde <c>psql</c>, ni desde una migración—. Y una fila de un mes M en la partición por
/// defecto deja el siguiente <c>asegurar_particiones_de_movimientos()</c> que intente crear el mes
/// M en <b>fallo duro</b>. Las dos consecuencias juntas son las que obligan a escribir este caso
/// como está escrito:
/// </para>
/// <para>
/// — <b>envenena este carril</b>: la base del contenedor se comparte entre todos los casos de
/// integración, así que una fila confirmada ahí los pone en rojo a partir de ese momento, por un
/// motivo que no es el suyo y sin forma de limpiarla; y
/// </para>
/// <para>
/// — <b>envenena el segundo arranque</b>: el migrador llama a esa misma función en cada despliegue,
/// sale con 1, y la API no arranca (<c>service_completed_successfully</c>). Que la parada sea
/// ruidosa es la decisión —una partición por defecto con filas dentro es un invariante ya roto—,
/// pero es una parada.
/// </para>
/// <para>
/// <b>De ahí que todo lo de abajo viva dentro de una transacción que se deshace</b>, con puntos de
/// guardado para que cada rechazo no se lleve por delante a los siguientes. Nadie debe
/// «simplificar» esto quitando la transacción o confirmándola: el verde sería el mismo y la base
/// quedaría inservible.
/// </para>
/// <para>
/// <b>Y el mes se elige a veinte meses vista</b>, no a trece: la pista son trece particiones —la
/// del mes en curso y las doce siguientes—, y elegir el primero que falta dejaría el caso rojo el
/// día que alguien subiera el número de meses por delante sin que nada se hubiera roto.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaParticionPorDefectoSeDenunciaTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>El SQLSTATE con el que la función dice que la pista se agotó.</summary>
    private const string PistaAgotada = "55000";

    /// <summary>
    /// Una fila de un mes sin partición cae en la de por defecto, no se puede quitar, y deja el
    /// siguiente <c>asegurar_particiones</c> de ese mes en fallo duro.
    /// </summary>
    /// <remarks>
    /// <b>Las cuatro afirmaciones van en un solo caso y en una sola transacción, y no es
    /// comodidad.</b> Cada una es el estado de partida de la siguiente: que la fila cayó donde se
    /// dice es lo que hace que «no se puede borrar» signifique algo; que no se puede borrar es lo
    /// que convierte el fallo de la función en un callejón sin salida en vez de en un inconveniente
    /// que se arregla con un <c>DELETE</c>. Repartidas en cuatro casos, cada uno tendría que volver
    /// a montar el estado — y montarlo es justo lo que no se puede dejar escrito en la base.
    /// </remarks>
    [Fact]
    public async Task Una_fila_de_un_mes_sin_particion_cae_en_la_de_por_defecto_y_denuncia_la_averia()
    {
        var fueraDeLaPista = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(20));
        string mesSinParticion = ElLibro.ParticionDe(fueraDeLaPista);

        // El arnés del arnés: si ese mes YA tuviera partición, la fila caería en ella y el caso
        // entero estaría comprobando lo contrario de lo que dice comprobar.
        IReadOnlyList<string> yaExiste = await ElLibro.TextosAsync(
            postgres,
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT tabla.relname
                FROM pg_catalog.pg_class AS tabla
                JOIN pg_catalog.pg_namespace AS esquema ON esquema.oid = tabla.relnamespace
                WHERE esquema.nspname = 'inventario' AND tabla.relname = '{mesSinParticion}'
                """));

        yaExiste.ShouldBeEmpty(
            $"el mes {mesSinParticion} está a veinte meses vista y no tenía que tener partición; " +
            "con ella, la fila de abajo no llegaría nunca a la de por defecto");

        var empresaId = Guid.CreateVersion7();
        (Ajuste ajuste, IReadOnlyList<MovimientoStock> movimientos) =
            UnAjusteConfirmadoDe(empresaId, fueraDeLaPista);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        RepositorioDeAjustes repositorio = new(contexto);
        repositorio.Agregar(ajuste);
        repositorio.AgregarMovimientos(movimientos);
        await contexto.SaveChangesAsync();

        DbConnection conexion = contexto.Database.GetDbConnection();
        DbTransaction nativa = transaccion.GetDbTransaction();

        // 1 — dónde cayó. Acotado al documento, como todo lo de este carril.
        IReadOnlyList<string> donde = await LeerAsync(
            conexion,
            nativa,
            $"SELECT DISTINCT tableoid::regclass::text FROM inventario.movimiento_stock " +
            $"WHERE documento_origen_id = '{ajuste.Id}'");

        donde.ShouldBe(
            ["inventario.movimiento_stock_por_defecto"],
            "la fila es de un mes que no tiene partición: tenía que haber caído en la de por " +
            "defecto, que para eso está — perderla sería peor");

        // 2 — y de ahí no se saca. El DELETE, cerrado.
        await transaccion.CreateSavepointAsync("antesDeBorrar");

        PostgresException alBorrar = await RechazoAsync(
            conexion,
            nativa,
            $"DELETE FROM inventario.movimiento_stock WHERE documento_origen_id = '{ajuste.Id}'");

        alBorrar.SqlState.ShouldBe(ElLibro.SoloSeAnade);
        alBorrar.MessageText.ShouldContain("DELETE");

        await transaccion.RollbackToSavepointAsync("antesDeBorrar");

        // 3 — y el TRUNCATE de la partición por defecto, también. Son las dos únicas formas de
        // quitar una fila, y las dos están cerradas: por eso esto es un callejón sin salida.
        await transaccion.CreateSavepointAsync("antesDeVaciar");

        PostgresException alVaciar = await RechazoAsync(
            conexion, nativa, "TRUNCATE inventario.movimiento_stock_por_defecto");

        alVaciar.SqlState.ShouldBe(ElLibro.SoloSeAnade);
        alVaciar.MessageText.ShouldContain("TRUNCATE");

        await transaccion.RollbackToSavepointAsync("antesDeVaciar");

        // 4 — y el siguiente despliegue que intente crear ese mes se para, con el mes en el
        // mensaje. Es la denuncia: no se degrada en silencio hacia una sola tabla gigante.
        await transaccion.CreateSavepointAsync("antesDeAsegurar");

        PostgresException alAsegurar = await RechazoAsync(
            conexion, nativa, "SELECT inventario.asegurar_particiones_de_movimientos(24)");

        alAsegurar.SqlState.ShouldBe(
            PistaAgotada,
            "la función tenía que denunciar con `object_not_in_prerequisite_state` y dijo " +
            $"«{alAsegurar.MessageText}»");

        alAsegurar.MessageText.ShouldContain(mesSinParticion);
        alAsegurar.MessageText.ShouldContain("particion por defecto");

        await transaccion.RollbackToSavepointAsync("antesDeAsegurar");

        // Y nada de esto queda escrito. Sin esta vuelta atrás, el carril entero se queda con una
        // fila que nadie puede borrar y con la función de particiones rota para siempre.
        await transaccion.RollbackAsync();

        long quedan = await ElLibro.EscalarAsync<long>(
            postgres,
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT count(*) FROM inventario.movimiento_stock_por_defecto
                WHERE documento_origen_id = '{ajuste.Id}'
                """));

        quedan.ShouldBe(
            0,
            "la transacción se ha deshecho: si la fila sigue ahí, este caso acaba de dejar el " +
            "carril y el segundo arranque envenenados");
    }

    private static (Ajuste Ajuste, IReadOnlyList<MovimientoStock> Movimientos) UnAjusteConfirmadoDe(
        Guid empresaId,
        DateOnly fecha)
    {
        var almacenId = Guid.CreateVersion7();
        var serieId = Guid.CreateVersion7();
        DateTimeOffset momento = DateTimeOffset.UtcNow;

        var ajuste = Ajuste.Abrir(
            empresaId,
            serieId,
            almacenId,
            fecha,
            "Ajuste de un mes que no tiene partición",
            momento);

        ajuste.AnadirLinea(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            7m,
            Guid.CreateVersion7(),
            1m,
            Importe.De(2m, "EUR"),
            momento);

        var evento = new AjusteConfirmado(ajuste.Id, empresaId, almacenId, fecha, ajuste.Lineas.Count);

        // La serie es nueva en cada llamada, así que el primer número nunca choca con el índice
        // único de `(serie_id, numero)`. Lo que este caso persigue está en la partición, no en el
        // correlativo.
        return (ajuste, ajuste.Confirmar(numero: 1, evento, momento));
    }

    private static async Task<IReadOnlyList<string>> LeerAsync(
        DbConnection conexion,
        DbTransaction transaccion,
        string consulta)
    {
        await using DbCommand orden = conexion.CreateCommand();
        orden.CommandText = consulta;
        orden.Transaction = transaccion;

        await using DbDataReader lector = await orden.ExecuteReaderAsync();

        List<string> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add(lector.GetString(0));
        }

        return filas;
    }

    private static async Task<PostgresException> RechazoAsync(
        DbConnection conexion,
        DbTransaction transaccion,
        string sentencia)
    {
        await using DbCommand orden = conexion.CreateCommand();
        orden.CommandText = sentencia;
        orden.Transaction = transaccion;

        return await Should.ThrowAsync<PostgresException>(() => orden.ExecuteNonQueryAsync());
    }
}
