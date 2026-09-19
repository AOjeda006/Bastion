using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El libro de movimientos está particionado por rango mensual sobre la fecha de operación, y se
/// le pregunta <b>al catálogo</b>, no a la intención.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué a <c>pg_partitioned_table</c> y no a la migración.</b> Leer el SQL de la migración
/// comprueba que alguien escribió <c>PARTITION BY RANGE</c>; leer <c>pg_partitioned_table</c>
/// comprueba que la base <b>está</b> particionada, que es otra cosa. Entre las dos caben una
/// migración que no se aplicó, un <c>Down</c> que se ejecutó y un despliegue sobre una base que
/// alguien tocó a mano. El catálogo del motor es el único sitio donde la respuesta es la del
/// sistema en marcha.
/// </para>
/// <para>
/// <b>Y ninguna afirmación de este fichero es un recuento global</b>, salvo la que describe el
/// conjunto de particiones, que es lo que este fichero afirma. Dónde caen las filas se pregunta
/// <b>por documento</b>: «el libro tiene N filas» sería verdad o mentira según qué otros casos del
/// carril hayan corrido antes, y un caso cuyo verde depende de sus vecinos no es una afirmación.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElLibroEstaParticionadoTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>La tabla está particionada por rango, y el rango es la fecha de operación.</summary>
    /// <remarks>
    /// <b>La estrategia y la clave, juntas y en un solo caso</b>, porque por separado cada una deja
    /// pasar lo que la otra caza: una tabla particionada por <c>LIST</c> sobre la fecha, o por
    /// <c>RANGE</c> sobre <c>creado_en</c> — que es el error de verdad, porque un ajuste de
    /// diciembre grabado en enero es una fila de diciembre y en la partición de enero está mal
    /// contada—.
    /// </remarks>
    [Fact]
    public async Task La_tabla_del_libro_esta_particionada_por_RANGO_sobre_la_fecha_de_operacion()
    {
        IReadOnlyList<string> declaracion = await ElLibro.TextosAsync(
            postgres,
            """
            SELECT particionada.partstrat::text || ' ' || pg_get_partkeydef(tabla.oid)
            FROM pg_catalog.pg_partitioned_table AS particionada
            JOIN pg_catalog.pg_class AS tabla ON tabla.oid = particionada.partrelid
            JOIN pg_catalog.pg_namespace AS esquema ON esquema.oid = tabla.relnamespace
            WHERE esquema.nspname = 'inventario' AND tabla.relname = 'movimiento_stock'
            """);

        // Una sola fila, y la que es. Cero filas significa que la tabla NO está particionada, que
        // es el estado en el que todo lo demás de este fichero seguiría compilando y no querría
        // decir nada.
        declaracion.ShouldBe(
            ["r RANGE (fecha_de_operacion)"],
            "`inventario.movimiento_stock` tiene que estar en `pg_partitioned_table` con " +
            "estrategia de rango (`r`) sobre `fecha_de_operacion`");
    }

    /// <summary>La clave primaria incluye la clave de partición, porque PostgreSQL lo exige.</summary>
    /// <remarks>
    /// No es una preferencia del diseño: una tabla particionada no admite un índice único que no
    /// contenga la clave de partición. Se afirma aquí porque es lo que explica que <c>Id</c> no sea
    /// la clave él solo, y porque una migración reescrita que la dejara en <c>(id)</c> ni siquiera
    /// llegaría a aplicarse — y este caso dice por qué antes de que alguien lo intente.
    /// </remarks>
    [Fact]
    public async Task La_clave_primaria_del_libro_incluye_la_clave_de_particion()
    {
        IReadOnlyList<string> clave = await ElLibro.TextosAsync(
            postgres,
            """
            SELECT pg_get_constraintdef(restriccion.oid)
            FROM pg_catalog.pg_constraint AS restriccion
            JOIN pg_catalog.pg_class AS tabla ON tabla.oid = restriccion.conrelid
            JOIN pg_catalog.pg_namespace AS esquema ON esquema.oid = tabla.relnamespace
            WHERE esquema.nspname = 'inventario'
              AND tabla.relname = 'movimiento_stock'
              AND restriccion.contype = 'p'
            """);

        clave.ShouldBe(["PRIMARY KEY (id, fecha_de_operacion)"]);
    }

    /// <summary>
    /// Las particiones son el mes en curso, los doce siguientes y la de por defecto — y no está
    /// vacío el conjunto.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La lista entera y en los dos sentidos</b>, no un recuento: que falte un mes es la pista
    /// que se acorta, y que sobre uno es alguien creando particiones por su cuenta. Y los meses no
    /// se escriben a mano: se calculan con <c>current_date</c> en la propia base, igual que los
    /// calcula la función que los crea. Escritos a mano, este caso se pondría rojo el día 1 de
    /// cada mes sin que nada se hubiera roto.
    /// </para>
    /// <para>
    /// <b>Y se afirma que el conjunto no está vacío (ADR-0020)</b>, que aquí no es ceremonia: la
    /// comprobación de los disparadores del segundo arranque —«ninguna partición sin su guarda de
    /// <c>TRUNCATE</c>»— sale verde sola cuando no hay ninguna partición, así que alguien tiene que
    /// afirmar que las hay. Es este caso.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_conjunto_de_particiones_es_el_mes_en_curso_los_doce_siguientes_y_la_de_por_defecto()
    {
        IReadOnlyList<string> encontradas = await ElLibro.TextosAsync(
            postgres,
            """
            SELECT hija.relname
            FROM pg_catalog.pg_inherits AS herencia
            JOIN pg_catalog.pg_class AS padre ON padre.oid = herencia.inhparent
            JOIN pg_catalog.pg_namespace AS esquema ON esquema.oid = padre.relnamespace
            JOIN pg_catalog.pg_class AS hija ON hija.oid = herencia.inhrelid
            WHERE esquema.nspname = 'inventario' AND padre.relname = 'movimiento_stock'
            ORDER BY hija.relname
            """);

        encontradas.ShouldNotBeEmpty(
            "sin ni una partición, «toda partición lleva su disparador de TRUNCATE» sale verde " +
            "por no tener nada que mirar, y la tabla particionada sería una tabla normal con una " +
            "declaración encima");

        IReadOnlyList<string> esperadas = await ElLibro.TextosAsync(
            postgres,
            """
            SELECT nombre FROM (
                SELECT 'movimiento_stock_' || to_char(
                    (date_trunc('month', current_date) + make_interval(months => mes))::date,
                    'YYYY_MM') AS nombre
                FROM generate_series(0, 12) AS mes
                UNION ALL
                SELECT 'movimiento_stock_por_defecto'
            ) AS pista
            ORDER BY nombre
            """);

        encontradas.ShouldBe(esperadas);
    }

    /// <summary>
    /// Las filas de un ajuste caen en la partición de SU mes, y ninguna de ellas en la de por
    /// defecto.
    /// </summary>
    /// <remarks>
    /// <b>Las dos mitades acotadas al documento</b>, que es lo que las hace afirmaciones. «La
    /// partición por defecto está vacía» sería un recuento global: verdadero o falso según qué
    /// otro caso del carril haya escrito antes, y rojo por un motivo ajeno. Preguntado por
    /// documento, lo que se afirma es lo que se quería afirmar — estas filas, y solo estas, están
    /// donde les toca—.
    /// </remarks>
    [Fact]
    public async Task Las_filas_de_un_ajuste_caen_en_la_particion_de_su_mes_y_ninguna_en_la_de_por_defecto()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        UnAjusteConfirmado confirmado = await ElLibro.ConfirmarUnAjusteAsync(postgres, hoy, lineas: 3);

        IReadOnlyList<string> particiones = await ElLibro.TextosAsync(
            postgres,
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT DISTINCT tableoid::regclass::text
                FROM inventario.movimiento_stock
                WHERE documento_origen_id = '{confirmado.AjusteId}'
                """));

        particiones.ShouldBe([$"inventario.{ElLibro.ParticionDe(hoy)}"]);

        long enLaDePorDefecto = await ElLibro.EscalarAsync<long>(
            postgres,
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT count(*) FROM inventario.movimiento_stock_por_defecto
                WHERE documento_origen_id = '{confirmado.AjusteId}'
                """));

        enLaDePorDefecto.ShouldBe(
            0,
            "las tres filas de este ajuste son del mes en curso, que tiene partición: si han " +
            "caído en la de por defecto, el enrutado por fecha de operación no está funcionando");

        // Y que había tres, dicho por el documento y no por la tabla: si el ajuste hubiera
        // escrito cero filas, las dos afirmaciones de arriba saldrían verdes sin haber mirado
        // ninguna fila (ADR-0020).
        confirmado.Movimientos.Count.ShouldBe(3);
    }
}
