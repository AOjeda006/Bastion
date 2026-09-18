using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeUbicaciones"/>
/// <remarks>
/// Vive en Organización, que es la dueña de las dos tablas, y se resuelve en proceso (§4, reglas de
/// frontera 1 y 3). Que sean <b>dos</b> tablas es justamente por lo que este puerto existe con esta
/// forma: quien pregunta no puede unirlas, porque están en un esquema que no es el suyo.
/// </remarks>
internal sealed class ConsultaDeUbicaciones(
    OrganizacionDbContext contexto,
    IAccesoALoBloqueado bloqueados) : IConsultaDeUbicaciones
{
    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Una sola consulta con la unión dentro</b>, y no una lectura de la ubicación seguida de
    /// otra del almacén: entre las dos cabe un bloqueo, y la respuesta describiría una pareja de
    /// estados que no fue verdad en ningún instante. La unión es <c>INNER</c> y eso no pierde
    /// filas: hay clave ajena de <c>ubicaciones.almacen_id</c> con <c>Restrict</c>, y las dos
    /// tablas llevan los mismos dos filtros, así que o entran las dos o no entra ninguna.
    /// </para>
    /// <para>
    /// <b>El ámbito declarado se abre una vez y cubre las dos tablas</b>, que es lo que hace falta:
    /// el caso que decide —una ubicación activa dentro de un almacén bloqueado— necesita ver la
    /// fila del almacén, que el filtro de R16 esconde.
    /// </para>
    /// </remarks>
    /// <param name="almacenId">Almacén dentro del cual se pregunta.</param>
    /// <param name="ubicacionId">Identificador de la ubicación.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<EstadoDeMaestro> EstadoDeAsync(
        Guid almacenId, Guid ubicacionId, CancellationToken cancelacion)
    {
        using IDisposable _ = bloqueados.ViendoLoBloqueado(
            MotivoParaVerLoBloqueado.ResolucionDeUnMaestroApuntado);

        ParDeBloqueos? pareja = await (
            from ubicacion in contexto.Ubicaciones
            join almacen in contexto.Almacenes on ubicacion.AlmacenId equals almacen.Id
            where ubicacion.Id == ubicacionId && ubicacion.AlmacenId == almacenId
            select new ParDeBloqueos(
                almacen.Bloqueo.EstaBloqueado,
                ubicacion.Bloqueo.EstaBloqueado))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return pareja is null
            ? EstadoDeMaestro.NoExiste
            : LaPeorDeLasDos(
                ConsultaDeAlmacenes.Estado(pareja.Almacen),
                ConsultaDeAlmacenes.Estado(pareja.Ubicacion));
    }

    /// <summary>
    /// La composición del ADR-0037 §4: la ubicación hereda el estado de su almacén y el suyo propio
    /// solo puede empeorarlo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Escrita por casos y NO como un mínimo.</b> El orden de peor a mejor es
    /// <c>NoExiste</c> &lt; <c>SoloResuelveLoViejo</c> &lt; <c>SeOfreceParaLoNuevo</c>, y el orden
    /// de los <b>números</b> del enumerado es otro: <c>SeOfreceParaLoNuevo</c> vale 1 y
    /// <c>SoloResuelveLoViejo</c> vale 2. Un <c>Math.Min</c> sobre un almacén bloqueado (2) y una
    /// ubicación activa (1) devolvería 1 — que es justo la respuesta que deja crear el movimiento
    /// contra una nave cerrada. El enumerado no está ordenado por severidad y no tiene por qué
    /// estarlo; lo que no puede es que alguien lo suponga.
    /// </para>
    /// <para>
    /// Las ramas van de peor a mejor a propósito, para que leer el orden sea leer la regla.
    /// </para>
    /// </remarks>
    /// <param name="almacen">Lo que contestaría el puerto del almacén.</param>
    /// <param name="ubicacion">Lo que contestaría la fila de la ubicación por sí sola.</param>
    internal static EstadoDeMaestro LaPeorDeLasDos(
        EstadoDeMaestro almacen, EstadoDeMaestro ubicacion) => (almacen, ubicacion) switch
        {
            (EstadoDeMaestro.NoExiste, _) or (_, EstadoDeMaestro.NoExiste) =>
                EstadoDeMaestro.NoExiste,

            (EstadoDeMaestro.SoloResuelveLoViejo, _) or (_, EstadoDeMaestro.SoloResuelveLoViejo) =>
                EstadoDeMaestro.SoloResuelveLoViejo,

            _ => EstadoDeMaestro.SeOfreceParaLoNuevo,
        };

    /// <summary>Los dos bloqueos que decide la consulta, leídos en el mismo instante.</summary>
    /// <param name="Almacen">Si el almacén está bloqueado.</param>
    /// <param name="Ubicacion">Si la ubicación está bloqueada.</param>
    private sealed record ParDeBloqueos(bool Almacen, bool Ubicacion);
}
