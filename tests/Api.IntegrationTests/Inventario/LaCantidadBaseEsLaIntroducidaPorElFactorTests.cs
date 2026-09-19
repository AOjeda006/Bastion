using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las tres cantidades de una fila del libro se sostienen entre ellas: la base es la introducida
/// por el factor, y el motor no acepta otra cosa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tres cantidades y no una, que es lo que pide la trazabilidad.</b> En la unidad base es en lo
/// que se suma —todo el libro habla la misma unidad por artículo, o sumar no significaría nada—; la
/// introducida y su unidad son lo que escribió la persona, que es lo que aparece en el papel que
/// firmó; y el factor es el puente, guardado en la fila porque una conversión puede cambiar mañana
/// y la fila de ayer tiene que seguir explicándose sola.
/// </para>
/// <para>
/// <b>La regla está escrita dos veces a propósito, y las dos dicen lo mismo.</b> En el dominio,
/// <c>MovimientoStock.EnUnidadBase</c> la calcula al crear la fila —nadie puede enviar la cantidad
/// base—; y en el motor, un <c>CHECK</c> con el mismo <c>round(..., 6)</c>. No es duplicación
/// defensiva: el dominio protege el único camino que hoy escribe, y el <c>CHECK</c> protege los que
/// no existen todavía —una importación, una migración de datos, una corrección a mano—. Y las dos
/// redondean <b>alejándose del cero</b>: el <c>round</c> de PostgreSQL sobre <c>numeric</c> lo hace
/// así, y el de .NET se lo dice explícitamente porque su omisión es el del banquero.
/// </para>
/// <para>
/// <b>Lo que se afirma aquí va acotado al documento</b>; el redondeo en sus fronteras —el medio
/// exacto, la base que se redondea a cero— se comprueba donde no hace falta una base de datos, en
/// los casos unitarios del agregado.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaCantidadBaseEsLaIntroducidaPorElFactorTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>El SQLSTATE de un <c>CHECK</c> incumplido.</summary>
    private const string CheckIncumplido = "23514";

    /// <summary>
    /// Cada fila que un ajuste escribió cumple la regla, y quien lo dice es la propia base.
    /// </summary>
    /// <remarks>
    /// <b>La comprobación se le hace a PostgreSQL y no a C#</b>, y la diferencia importa: rehacer
    /// la multiplicación en el test con los valores que el test mismo envió comprobaría que el
    /// test sabe multiplicar. Preguntándole a la base, lo que se compara es lo que quedó
    /// <b>escrito en las tres columnas</b>, con la aritmética del motor, que es la que sostiene el
    /// <c>CHECK</c>.
    /// </remarks>
    [Fact]
    public async Task Cada_fila_de_un_ajuste_cumple_la_regla_del_factor_medida_en_la_base()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        UnAjusteConfirmado confirmado = await ElLibro.ConfirmarUnAjusteAsync(postgres, hoy, lineas: 3);

        long miradas = await ElLibro.EscalarAsync<long>(
            postgres,
            Consulta(
                "SELECT count(*) FROM inventario.movimiento_stock WHERE documento_origen_id = '{0}'",
                confirmado.AjusteId));

        miradas.ShouldBe(
            3,
            "sin filas que mirar, «todas cumplen la regla» sale verde por no haber mirado (ADR-0020)");

        IReadOnlyList<string> incumplen = await ElLibro.TextosAsync(
            postgres,
            Consulta(
                """
                SELECT fila.id::text
                FROM inventario.movimiento_stock AS fila
                WHERE fila.documento_origen_id = '{0}'
                  AND fila.cantidad_en_unidad_base
                      <> round(fila.cantidad_introducida * fila.factor_a_unidad_base, 6)
                """,
                confirmado.AjusteId));

        incumplen.ShouldBeEmpty();
    }

    /// <summary>
    /// Una fila con la cantidad base cambiada a mano no entra: el <c>CHECK</c> la rechaza.
    /// </summary>
    /// <remarks>
    /// Es el camino que el dominio no vigila porque no pasa por él. Se escribe con SQL crudo a
    /// propósito —no hay forma de enviar una cantidad base desde el agregado— y dentro de una
    /// transacción que se deshace, como todo lo de este carril que toca el libro.
    /// </remarks>
    [Fact]
    public async Task Una_cantidad_base_que_no_es_la_introducida_por_el_factor_la_rechaza_el_motor()
    {
        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres, Insercion(cantidadBase: "99", cantidadIntroducida: "2", factor: "3"));

        fallo.SqlState.ShouldBe(CheckIncumplido);
        fallo.ConstraintName.ShouldBe("ck_movimiento_stock_cantidad_por_factor");
    }

    /// <summary>Una fila que no mueve nada tampoco entra.</summary>
    /// <remarks>
    /// Una fila con cantidad cero deja constancia de un movimiento que no ocurrió, y la suma no la
    /// distingue de no haberla escrito. Lo impiden las dos: la fábrica del dominio y este
    /// <c>CHECK</c>.
    /// </remarks>
    [Fact]
    public async Task Una_fila_que_no_mueve_nada_la_rechaza_el_motor()
    {
        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres, Insercion(cantidadBase: "0", cantidadIntroducida: "0", factor: "3"));

        fallo.SqlState.ShouldBe(CheckIncumplido);
        fallo.ConstraintName.ShouldBe("ck_movimiento_stock_cantidad_no_nula");
    }

    private static string Insercion(string cantidadBase, string cantidadIntroducida, string factor) =>
        Consulta(
            """
            INSERT INTO inventario.movimiento_stock
                (id, fecha_de_operacion, empresa_id, almacen_id, ubicacion_id, articulo_id,
                 cantidad_en_unidad_base, cantidad_introducida, unidad_introducida_id,
                 factor_a_unidad_base, documento_origen_tipo, documento_origen_id,
                 coste_unitario_cantidad, coste_unitario_divisa, creado_en, modificado_en)
            VALUES (gen_random_uuid(), current_date, gen_random_uuid(), gen_random_uuid(),
                    gen_random_uuid(), gen_random_uuid(), {0}, {1}, gen_random_uuid(), {2},
                    'Ajuste', gen_random_uuid(), 1.0, 'EUR', now(), now())
            """,
            cantidadBase,
            cantidadIntroducida,
            factor);

    private static string Consulta(string plantilla, params object[] valores) =>
        string.Format(CultureInfo.InvariantCulture, plantilla, valores);
}
