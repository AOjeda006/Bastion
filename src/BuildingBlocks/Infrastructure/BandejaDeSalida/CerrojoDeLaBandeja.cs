using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>
/// El cerrojo que garantiza que <b>solo un publicador a la vez</b> vacía la bandeja.
/// </summary>
/// <remarks>
/// <para>
/// <b>ESTE FICHERO ES LA ÚNICA EXCEPCIÓN AUTORIZADA A LA PROHIBICIÓN DE SQL CRUDO DEL 0.6</b>, está
/// listada por su ruta en <c>ElFiltroNoSeSaltaPorAhiTests</c> y no se extiende a nada más. El
/// argumento, entero, porque de él depende que no sirva de precedente:
/// </para>
/// <para>
/// La prohibición existe porque el SQL escrito a mano <b>no pasa por el traductor de consultas</b>,
/// así que el filtro de empresa no se le aplica y devuelve filas de otros inquilinos sin fallar.
/// Las dos órdenes de este fichero no leen ninguna tabla: <c>pg_try_advisory_lock</c> y
/// <c>pg_advisory_unlock</c> toman y sueltan un cerrojo consultivo del propio PostgreSQL, con una
/// clave numérica constante, y devuelven un booleano. <b>No hay fila que filtrar</b>, así que no
/// hay nada que el filtro pudiera haber protegido. La excepción se justifica precisamente por lo
/// que la hace inútil para cualquier otro caso: el día que alguien quiera SQL crudo para leer
/// <i>filas</i>, este argumento no le vale y tendrá que traer el suyo.
/// </para>
/// <para>
/// <b>Y por qué un cerrojo y no una suposición.</b> Las tres formas de garantizar un solo lector se
/// miraron: (1) esta; (2) suponer que solo hay una instancia desplegada, que es gratis y falla
/// publicando dos veces, en silencio, el día que alguien escale la API a dos réplicas —el fallo no
/// da error, da dos correos, dos asientos o dos remesas—; y (3) <c>FOR UPDATE SKIP LOCKED</c> con
/// varios lectores, que es <b>más</b> SQL crudo, este sí sobre filas, y que además <b>pierde el
/// orden</b>. Lo tercero pesa: la R15 obliga a que la cadena de registros de facturación sea una
/// sola por (obligado tributario, sistema informático), o sea, un consumidor serializado. Un diseño
/// que hoy repartiera la cola entre varios lectores haría imposible mañana ese consumidor sin
/// rehacer el mecanismo. Un lector único ordenado ya es la forma que la R15 va a necesitar.
/// </para>
/// <para>
/// <b>El cerrojo es de sesión, así que la conexión se abre a mano.</b> Un contexto de EF Core abre
/// y cierra la conexión por orden; si se dejara así, el cerrojo se soltaría al devolver la conexión
/// a la reserva —o peor, se quedaría tomado en una conexión reutilizada por otro—. Por eso
/// <see cref="TomarAsync"/> abre la conexión y <see cref="SoltarAsync"/> la suelta y la cierra,
/// siempre, también cuando la vuelta ha reventado.
/// </para>
/// </remarks>
/// <param name="contexto">Contexto sobre cuya conexión se toma el cerrojo.</param>
internal sealed class CerrojoDeLaBandeja(ContextoDeLaBandeja contexto)
{
    // Constante y arbitraria: lo único que importa es que todos los publicadores de esta base usen
    // la misma y que no choque con otro uso de cerrojos consultivos. Hoy no hay ninguno más.
    private const long Clave = 8_0800_8080;

    /// <summary>Intenta tomar el cerrojo sin esperar.</summary>
    /// <remarks>
    /// <b>Sin esperar</b>, a propósito: si otro publicador está trabajando, esta vuelta no tiene
    /// nada que hacer y lo correcto es volver en el intervalo siguiente. Un cerrojo con espera
    /// dejaría hilos bloqueados acumulándose contra la base.
    /// </remarks>
    /// <param name="cancelacion">Cancelación de la parada del trabajo de fondo.</param>
    /// <returns><c>true</c> si el cerrojo es nuestro.</returns>
    public async Task<bool> TomarAsync(CancellationToken cancelacion)
    {
        await contexto.Database.OpenConnectionAsync(cancelacion).ConfigureAwait(false);

        try
        {
            return await contexto.Database
                .SqlQueryRaw<bool>("SELECT pg_try_advisory_lock({0}) AS \"Value\"", Clave)
                .SingleAsync(cancelacion)
                .ConfigureAwait(false);
        }
        catch
        {
            // Si la orden falla —la base no responde, el esquema no está—, la conexión no se puede
            // quedar abierta esperando a un `SoltarAsync` que ya no va a llegar.
            await contexto.Database.CloseConnectionAsync().ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>Suelta el cerrojo y cierra la conexión.</summary>
    /// <param name="cancelacion">Cancelación de la parada del trabajo de fondo.</param>
    public async Task SoltarAsync(CancellationToken cancelacion)
    {
        try
        {
            await contexto.Database
                .SqlQueryRaw<bool>("SELECT pg_advisory_unlock({0}) AS \"Value\"", Clave)
                .SingleAsync(cancelacion)
                .ConfigureAwait(false);
        }
        finally
        {
            // Cerrar la conexión sin haber soltado el cerrojo lo dejaría tomado en una conexión de
            // la reserva, y el publicador no volvería a entrar nunca más. Por eso el cierre va en
            // el `finally` y la suelta delante.
            await contexto.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }
}
