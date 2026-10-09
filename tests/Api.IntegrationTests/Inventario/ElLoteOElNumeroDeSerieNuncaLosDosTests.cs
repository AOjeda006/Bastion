using System.Globalization;
using Bastion.Api.IntegrationTests.Persistencia;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Una fila de la existencia o del libro lleva el lote o el número de serie, nunca los dos, y el
/// motor no acepta otra cosa (ADR-0050 §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>La regla está escrita dos veces, como la del factor.</b> En el dominio, la marca de
/// trazabilidad del artículo decide cuál de las dos lleva cada línea, y ninguna fábrica deja
/// pasar las dos. En el motor, un <c>CHECK</c> por tabla con <c>num_nonnulls</c>. El dominio
/// protege el único camino que hoy escribe, y el <c>CHECK</c>, los que no existen todavía: una
/// importación, una corrección a mano, un documento nuevo que se olvide de preguntar.
/// </para>
/// <para>
/// <b>No se traduce</b>: si salta, es un defecto, y sale un <c>500</c>. Por eso el caso escribe
/// en crudo, que es el único camino por el que la fila llega al motor, y dentro de una
/// transacción que se deshace. El lote y el número de serie se crean en la misma transacción,
/// porque las claves ajenas no dejarían apuntar a uno inventado, y el caso afirmaría entonces el
/// rechazo de la clave y no el del <c>CHECK</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElLoteOElNumeroDeSerieNuncaLosDosTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>El SQLSTATE de un <c>CHECK</c> incumplido.</summary>
    private const string CheckIncumplido = "23514";

    /// <summary>Una existencia con las dos marcas no entra.</summary>
    /// <remarks>
    /// La fila lleva una unidad, que el <c>CHECK</c> del número de serie admite: si llevara dos,
    /// el rechazo podría ser de ese.
    /// </remarks>
    [Fact]
    public async Task Una_existencia_con_lote_y_numero_de_serie_la_rechaza_el_motor()
    {
        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres,
            ConLasDosMarcas(
                """
                INSERT INTO inventario.existencias
                    (id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id,
                     numero_de_serie_id, fisico, en_transito)
                VALUES (gen_random_uuid(), '{0}', '{1}', gen_random_uuid(), gen_random_uuid(),
                        '{2}', '{3}', 1, 0)
                """));

        fallo.SqlState.ShouldBe(CheckIncumplido, fallo.MessageText);
        fallo.ConstraintName.ShouldBe("ck_existencias_lote_o_numero_de_serie");
    }

    /// <summary>Una fila del libro con las dos marcas tampoco entra.</summary>
    /// <remarks>
    /// La cantidad es una unidad base, introducida en la misma unidad con factor uno, y así los
    /// dos <c>CHECK</c> de la cantidad, que el motor mira antes por orden alfabético, la dejan
    /// pasar.
    /// </remarks>
    [Fact]
    public async Task Una_fila_del_libro_con_lote_y_numero_de_serie_la_rechaza_el_motor()
    {
        PostgresException fallo = await ElLibro.ElMotorRechazaAsync(
            postgres,
            ConLasDosMarcas(
                """
                INSERT INTO inventario.movimiento_stock
                    (id, fecha_de_operacion, empresa_id, almacen_id, ubicacion_id, articulo_id,
                     lote_id, numero_de_serie_id, cantidad_en_unidad_base, cantidad_introducida,
                     unidad_introducida_id, factor_a_unidad_base, documento_origen_tipo,
                     documento_origen_id, coste_unitario, divisa, valor, precio_medio, creado_en,
                     modificado_en)
                VALUES (gen_random_uuid(), current_date, '{0}', gen_random_uuid(),
                        gen_random_uuid(), '{1}', '{2}', '{3}', 1, 1, gen_random_uuid(), 1,
                        'Ajuste', gen_random_uuid(), 1.0, 'EUR', 1, 1.0, now(), now())
                """));

        fallo.SqlState.ShouldBe(CheckIncumplido, fallo.MessageText);
        fallo.ConstraintName.ShouldBe("ck_movimiento_stock_lote_o_numero_de_serie");
    }

    /// <summary>
    /// Antepone a la sentencia el lote y el número de serie de un mismo artículo, y le pasa sus
    /// identificadores: <c>{0}</c> la empresa, <c>{1}</c> el artículo, <c>{2}</c> el lote y
    /// <c>{3}</c> el número de serie.
    /// </summary>
    private static string ConLasDosMarcas(string sentencia)
    {
        var empresa = Guid.CreateVersion7();
        var articulo = Guid.CreateVersion7();
        var lote = Guid.CreateVersion7();
        var numeroDeSerie = Guid.CreateVersion7();

        return string.Format(
            CultureInfo.InvariantCulture,
            """
            INSERT INTO inventario.lotes (id, empresa_id, articulo_id, codigo)
            VALUES ('{2}', '{0}', '{1}', 'L-1');
            INSERT INTO inventario.numeros_de_serie (id, empresa_id, articulo_id, numero)
            VALUES ('{3}', '{0}', '{1}', 'S-1');
            """ + "\n" + sentencia,
            empresa,
            articulo,
            lote,
            numeroDeSerie);
    }
}
