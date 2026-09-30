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
}
