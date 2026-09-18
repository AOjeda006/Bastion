using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeAlmacenes"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeAlmacenes(
    OrganizacionDbContext contexto,
    IAccesoALoBloqueado bloqueados) : IConsultaDeAlmacenes
{
    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Abre el ámbito declarado porque si no, no hay nada que contestar.</b> El filtro de R16
    /// esconde el almacén bloqueado, así que sin esto la fila no llegaría aquí y el puerto diría
    /// <c>NoExiste</c> — que es exactamente la respuesta que el ADR-0037 descarta, y la que
    /// rompería la doble flecha del libro de movimientos. Ámbito con su motivo de la lista cerrada
    /// y anotado en el registro; nunca un <c>IgnoreQueryFilters</c>, que además apagaría de paso el
    /// filtro de empresa.
    /// </para>
    /// <para>
    /// <b>El filtro de empresa sigue puesto</b>, y por eso un almacén de otra empresa contesta
    /// <c>NoExiste</c>: lo que se abre es la puerta del art. 32, no la del inquilinato.
    /// </para>
    /// <para>
    /// <b>Una sola consulta y no dos</b>, como en los puertos del 1.2: dos lecturas dejan un hueco
    /// en el que la fila cambia, y la respuesta describiría un estado que no fue verdad en ningún
    /// instante. El nulo del anulable <b>es</b> la respuesta a «¿existe?».
    /// </para>
    /// </remarks>
    /// <param name="almacenId">Identificador del almacén.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<EstadoDeMaestro> EstadoDeAsync(
        Guid almacenId, CancellationToken cancelacion)
    {
        using IDisposable _ = bloqueados.ViendoLoBloqueado(
            MotivoParaVerLoBloqueado.ResolucionDeUnMaestroApuntado);

        bool? bloqueado = await contexto.Almacenes
            .Where(almacen => almacen.Id == almacenId)
            .Select(almacen => (bool?)almacen.Bloqueo.EstaBloqueado)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return Estado(bloqueado);
    }

    /// <summary>La traducción de «existe / está bloqueado» a la respuesta del puerto.</summary>
    /// <remarks>
    /// Compartida con <c>ConsultaDeUbicaciones</c>, que la aplica dos veces —al almacén y a la
    /// ubicación— y se queda con la peor de las dos. Escrita una vez: si el día de mañana un
    /// almacén bloqueado dejara de resolver lo viejo, la respuesta tiene que cambiar en un solo
    /// sitio o los dos puertos divergirían sin que nada lo dijera.
    /// </remarks>
    /// <param name="bloqueado">Si la fila está bloqueada, o <c>null</c> si no hay fila.</param>
    internal static EstadoDeMaestro Estado(bool? bloqueado) => bloqueado switch
    {
        null => EstadoDeMaestro.NoExiste,
        true => EstadoDeMaestro.SoloResuelveLoViejo,
        false => EstadoDeMaestro.SeOfreceParaLoNuevo,
    };
}
