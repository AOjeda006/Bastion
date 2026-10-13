using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Espera a que una operación en vuelo se quede parada detrás de un proceso de PostgreSQL, o falla.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es lo que hace que un caso de carrera ejerza la carrera y no dos operaciones seguidas.</b> Si
/// la transacción que tiene el cerrojo soltara antes de que la otra llegara a esperarla, la segunda
/// se encontraría el trabajo de la primera ya publicado y saldría bien con cerrojo o sin él. Así
/// que no se suelta por tiempo sino cuando el motor dice que hay alguien esperando, que es lo que
/// contesta <c>pg_blocking_pids</c>. Y si la operación termina sin haber esperado, eso ya es el
/// fallo: ha decidido sin ver lo que la otra estaba a punto de confirmar.
/// </para>
/// <para>
/// <b>Vive aquí desde el 2.9</b>, cuando iba a hacer falta una quinta copia. Las cuatro que había
/// —en <c>ElEjercicioRigeElAjusteTests</c>, <c>LasExistenciasSonLaSumaDelLibroTests</c>,
/// <c>LaValoracionDelAjusteTests</c> y <c>NingunaFechaAnteriorAlUltimoMovimientoTests</c>— solo se
/// distinguían por el texto de sus dos fallos, y ese texto es lo único que cada caso le pasa.
/// </para>
/// </remarks>
internal static class LaEspera
{
    /// <summary>
    /// Espera a que <paramref name="enVuelo"/> se pare detrás de
    /// <paramref name="procesoQueFrena"/>.
    /// </summary>
    /// <param name="cadenaDeConexion">
    /// La base del caso; la consulta va por una conexión propia.
    /// </param>
    /// <param name="procesoQueFrena">
    /// El proceso de PostgreSQL de la transacción que tiene el cerrojo.
    /// </param>
    /// <param name="enVuelo">La operación que tiene que quedarse esperando.</param>
    /// <param name="aQuien">
    /// A quién espera, para los dos mensajes: «la transacción en vuelo».
    /// </param>
    /// <param name="sinEsperar">Qué habría hecho mal si terminara sin esperar.</param>
    /// <returns>Cuando el motor dice que la operación ya está esperando.</returns>
    internal static async Task AQueLaFreneAsync(
        string cadenaDeConexion,
        int procesoQueFrena,
        Task enVuelo,
        string aQuien,
        string sinEsperar)
    {
        await using NpgsqlConnection conexion = new(cadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand quienEspera = new(
            "SELECT count(*) FROM pg_stat_activity WHERE @frena = ANY(pg_blocking_pids(pid))",
            conexion);

        quienEspera.Parameters.AddWithValue("frena", procesoQueFrena);

        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < limite)
        {
            enVuelo.IsCompleted.ShouldBeFalse(
                $"la operación ha terminado sin esperar a {aQuien}: {sinEsperar}");

            if ((long)(await quienEspera.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new ShouldAssertException(
            $"en treinta segundos nadie se ha puesto a esperar a {aQuien}");
    }

    /// <summary>
    /// Espera a que <paramref name="enVuelo"/> termine sin pararse detrás de
    /// <paramref name="procesoQueNoFrena"/>, o falla en cuanto se para.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La otra mitad, desde el 2.14</b>: dos operaciones que NO se esperan. Esperar a que la
    /// segunda termine no basta, porque una que se queda parada detrás de la primera tampoco
    /// termina, y el caso solo lo sabría por el plazo. Así que la pregunta es la misma que arriba,
    /// con la respuesta al revés: si el motor dice que alguien espera a ese proceso, ese es el
    /// fallo, y se dice en el momento.
    /// </para>
    /// <para>
    /// Mira a quien espera al proceso y no a la operación, por lo mismo que arriba: el proceso de la
    /// operación en vuelo no se conoce hasta que abre su conexión.
    /// </para>
    /// </remarks>
    /// <param name="cadenaDeConexion">La base del caso.</param>
    /// <param name="procesoQueNoFrena">
    /// El proceso de PostgreSQL de la transacción que tiene su cerrojo, y que no debe frenar a la
    /// operación.
    /// </param>
    /// <param name="enVuelo">La operación que tiene que terminar sin esperarla.</param>
    /// <param name="quien">Quién no debe pararla: «el recálculo de otra empresa».</param>
    /// <param name="conEspera">
    /// Qué estaría mal si la parara. El motor dice que espera, no por qué: si el caso puede esperar
    /// por más de una causa, las nombra.
    /// </param>
    /// <returns>Cuando la operación ha terminado.</returns>
    internal static async Task SinQueLaFreneAsync(
        string cadenaDeConexion,
        int procesoQueNoFrena,
        Task enVuelo,
        string quien,
        string conEspera)
    {
        await using NpgsqlConnection conexion = new(cadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand quienEspera = new(
            "SELECT count(*) FROM pg_stat_activity WHERE @frena = ANY(pg_blocking_pids(pid))",
            conexion);

        quienEspera.Parameters.AddWithValue("frena", procesoQueNoFrena);

        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < limite)
        {
            if (enVuelo.IsCompleted)
            {
                return;
            }

            ((long)(await quienEspera.ExecuteScalarAsync())!).ShouldBe(
                0,
                $"{quien} ha parado la operación: {conEspera}");

            await Task.Delay(50);
        }

        throw new ShouldAssertException(
            $"en treinta segundos la operación no ha terminado, y no la ha parado {quien}");
    }

    /// <summary>El proceso que está parado detrás de <paramref name="procesoQueFrena"/>, que es uno.</summary>
    /// <remarks>
    /// Para las operaciones que el caso no abre con una conexión suya, como el recálculo, que abre
    /// la propia: se las para detrás de algo que el caso sí tiene, y se las encuentra por ahí.
    /// </remarks>
    /// <param name="cadenaDeConexion">La base del caso.</param>
    /// <param name="procesoQueFrena">El proceso que la tiene parada.</param>
    /// <returns>El proceso de la operación parada.</returns>
    internal static async Task<int> LaParadaDetrasDeAsync(string cadenaDeConexion, int procesoQueFrena)
    {
        await using NpgsqlConnection conexion = new(cadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand quienes = new(
            "SELECT pid FROM pg_stat_activity WHERE @frena = ANY(pg_blocking_pids(pid))",
            conexion);

        quienes.Parameters.AddWithValue("frena", procesoQueFrena);

        List<int> procesos = [];

        await using (NpgsqlDataReader lector = await quienes.ExecuteReaderAsync())
        {
            while (await lector.ReadAsync())
            {
                procesos.Add(lector.GetInt32(0));
            }
        }

        return procesos.ShouldHaveSingleItem($"una sola operación parada detrás de {procesoQueFrena}");
    }
}
