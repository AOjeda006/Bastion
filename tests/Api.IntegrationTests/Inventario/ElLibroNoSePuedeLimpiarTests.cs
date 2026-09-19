using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Los seis caminos por los que se podría modificar o vaciar el libro, cerrados en el motor y
/// comprobados uno a uno.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seis y no dos, y la cuenta es la de una tabla particionada.</b> Sobre una tabla normal
/// bastarían <c>UPDATE</c>, <c>DELETE</c> y <c>TRUNCATE</c>. Aquí cada uno de los tres tiene dos
/// puertas —por el padre y por la partición—, y no son la misma: un disparador <c>FOR EACH ROW</c>
/// creado en el padre <b>sí</b> se clona a las particiones, incluidas las que se creen después,
/// pero uno <c>FOR EACH STATEMENT</c> —que es lo único que puede vigilar un <c>TRUNCATE</c>— <b>no
/// se clona</b>. Está medido contra <c>postgres:17.6-alpine</c>, y el agujero se vio abierto: con
/// el disparador solo en el padre, <c>TRUNCATE inventario.movimiento_stock</c> salía rechazado y
/// <c>TRUNCATE inventario.movimiento_stock_2026_09</c> <b>se ejecutaba y se llevaba el mes</b>. El
/// sexto caso de este fichero existe por eso y se llama así por eso.
/// </para>
/// <para>
/// <b>Por qué en el motor y no en el repositorio.</b> Un repositorio sin método de borrado protege
/// del código que pasa por el repositorio, que es el que menos preocupa. Un <c>REVOKE</c> tampoco
/// vale: los permisos los da y los quita el dueño de la tabla, que es el usuario con el que se
/// conecta la aplicación, y un permiso que el interesado puede devolverse a sí mismo es una frase.
/// El disparador está por debajo de todo el mundo — de EF Core, de una importación, de una
/// corrección a mano con <c>psql</c>—.
/// </para>
/// <para>
/// <b>Toda sentencia va en una transacción que se deshace.</b> No es prudencia genérica: si un día
/// faltara el disparador, el caso que lo comprueba <i>vaciaría el libro de verdad</i> —un
/// <c>TRUNCATE</c> no pregunta— y pondría en rojo a los demás casos del carril por un motivo que no
/// es el suyo, escondiendo cuál era la avería. Con la transacción, un disparador que falta se ve
/// como un caso rojo y nada más.
/// </para>
/// <para>
/// <b>Y ninguna afirmación es global.</b> Cada caso escribe su propio ajuste y pregunta por él:
/// «el libro sigue teniendo N filas» sería verdad o mentira según qué otro caso haya corrido
/// antes.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElLibroNoSePuedeLimpiarTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>1 de 6 — modificar por el padre.</summary>
    /// <remarks>
    /// <b>Que el disparador salte es además la prueba de que la fila estaba.</b> Un
    /// <c>BEFORE UPDATE FOR EACH ROW</c> no se dispara sin una fila que casar, así que un
    /// <c>WHERE</c> que no encontrara nada no lanzaría: el caso saldría rojo por «no lanzó», no
    /// verde por no haber mirado.
    /// </remarks>
    [Fact]
    public async Task Modificar_una_fila_por_la_tabla_padre_lo_rechaza_el_motor()
    {
        UnAjusteConfirmado confirmado = await UnAjusteAsync();

        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres,
            Sentencia(
                "UPDATE inventario.movimiento_stock SET coste_unitario_cantidad = " +
                "coste_unitario_cantidad + 1 WHERE documento_origen_id = '{0}'",
                confirmado));

        ElRechazoEsElDelLibro(fallo, "UPDATE");
    }

    /// <summary>2 de 6 — borrar por el padre.</summary>
    [Fact]
    public async Task Borrar_una_fila_por_la_tabla_padre_lo_rechaza_el_motor()
    {
        UnAjusteConfirmado confirmado = await UnAjusteAsync();

        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres,
            Sentencia(
                "DELETE FROM inventario.movimiento_stock WHERE documento_origen_id = '{0}'",
                confirmado));

        ElRechazoEsElDelLibro(fallo, "DELETE");
    }

    /// <summary>3 de 6 — modificar entrando por la partición.</summary>
    /// <remarks>
    /// Este y el siguiente saldrían verdes aunque el disparador de fila no se heredara, porque la
    /// medición dice que sí se hereda. Están escritos igual: lo que se comprueba es que la puerta
    /// está cerrada, no por qué mecanismo, y el día que una versión de PostgreSQL cambiara ese
    /// comportamiento estos dos son los únicos que lo notarían.
    /// </remarks>
    [Fact]
    public async Task Modificar_una_fila_entrando_por_la_particion_lo_rechaza_el_motor()
    {
        UnAjusteConfirmado confirmado = await UnAjusteAsync();

        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres,
            Sentencia(
                "UPDATE inventario." + ElLibro.ParticionDe(confirmado.FechaDeOperacion) +
                " SET coste_unitario_cantidad = coste_unitario_cantidad + 1 " +
                "WHERE documento_origen_id = '{0}'",
                confirmado));

        ElRechazoEsElDelLibro(fallo, "UPDATE");
    }

    /// <summary>4 de 6 — borrar entrando por la partición.</summary>
    [Fact]
    public async Task Borrar_una_fila_entrando_por_la_particion_lo_rechaza_el_motor()
    {
        UnAjusteConfirmado confirmado = await UnAjusteAsync();

        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres,
            Sentencia(
                "DELETE FROM inventario." + ElLibro.ParticionDe(confirmado.FechaDeOperacion) +
                " WHERE documento_origen_id = '{0}'",
                confirmado));

        ElRechazoEsElDelLibro(fallo, "DELETE");
    }

    /// <summary>5 de 6 — vaciar la tabla padre.</summary>
    /// <remarks>
    /// Aquí el disparador es <c>FOR EACH STATEMENT</c>, así que salta haya filas o no: de eso que
    /// el caso afirme ANTES que sus filas están, porque si no estuvieran este verde no diría nada
    /// de lo que dice que dice (ADR-0020).
    /// </remarks>
    [Fact]
    public async Task Vaciar_la_tabla_padre_lo_rechaza_el_motor()
    {
        await UnAjusteAsync();

        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres, "TRUNCATE inventario.movimiento_stock");

        ElRechazoEsElDelLibro(fallo, "TRUNCATE");
    }

    /// <summary>
    /// 6 de 6 — vaciar <b>la partición</b>, el que se llamó así por haberse visto abierto.
    /// </summary>
    /// <remarks>
    /// Es el caso que justifica que crear una partición sea una función del motor y no un
    /// <c>CREATE TABLE</c> suelto: la función le pone su disparador de <c>TRUNCATE</c> a cada
    /// partición que crea, porque heredarlo del padre no ocurre. Y es el que de verdad podría
    /// hacer daño — se lleva un mes entero del libro, en silencio—, así que la transacción que se
    /// deshace no es aquí una precaución de estilo.
    /// </remarks>
    [Fact]
    public async Task Vaciar_LA_PARTICION_lo_rechaza_el_motor_y_este_es_el_que_se_vio_abierto()
    {
        UnAjusteConfirmado confirmado = await UnAjusteAsync();

        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres, "TRUNCATE inventario." + ElLibro.ParticionDe(confirmado.FechaDeOperacion));

        ElRechazoEsElDelLibro(fallo, "TRUNCATE");
    }

    /// <summary>
    /// Un ajuste confirmado del mes en curso, con sus filas comprobadas en su partición.
    /// </summary>
    /// <remarks>
    /// La comprobación previa es la mitad del ADR-0020 que estos casos necesitan: los dos de
    /// <c>TRUNCATE</c> saltarían igual sobre una partición vacía, y entonces el verde estaría
    /// diciendo «se rechaza vaciar algo que no tenía nada dentro».
    /// </remarks>
    private async Task<UnAjusteConfirmado> UnAjusteAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        UnAjusteConfirmado confirmado = await ElLibro.ConfirmarUnAjusteAsync(postgres, hoy, lineas: 2);

        long suyas = await ElLibro.EscalarAsync<long>(
            postgres,
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT count(*) FROM inventario.{ElLibro.ParticionDe(hoy)}
                WHERE documento_origen_id = '{confirmado.AjusteId}'
                """));

        suyas.ShouldBe(
            2,
            "el ajuste tenía que haber dejado sus dos filas en la partición de su mes; sin ellas, " +
            "lo de abajo comprobaría que no se puede vaciar una partición vacía");

        return confirmado;
    }

    private static string Sentencia(string plantilla, UnAjusteConfirmado confirmado) =>
        string.Format(CultureInfo.InvariantCulture, plantilla, confirmado.AjusteId);

    /// <summary>
    /// El rechazo es el del libro y es el de la operación que se intentó, no uno cualquiera.
    /// </summary>
    /// <remarks>
    /// Sin la segunda mitad, un <c>UPDATE</c> que fallara por una columna mal escrita daría el
    /// caso por bueno: habría lanzado. El mensaje de la función lleva dentro <c>TG_OP</c>, así que
    /// decir qué operación se rechazó es preguntarle al propio disparador qué creyó estar parando.
    /// </remarks>
    private static void ElRechazoEsElDelLibro(PostgresException fallo, string operacion)
    {
        fallo.SqlState.ShouldBe(
            ElLibro.SoloSeAnade,
            $"el rechazo tenía que venir del disparador de solo añadido y vino con «{fallo.MessageText}»");

        fallo.MessageText.ShouldContain(operacion);
        fallo.MessageText.ShouldContain("solo anadido");
    }
}
