using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Inventario.Application.Reservas;

/// <summary>Aparta una cantidad de un artículo en un almacén para una línea de otro módulo.</summary>
public interface IReservar
{
    /// <summary>Ejecuta la reserva.</summary>
    /// <param name="peticion">Qué se aparta, de dónde y para qué línea.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La reserva, o el motivo por el que no se aparta.</returns>
    Task<Resultado<ReservaDto>> EjecutarAsync(ReservarDto peticion, CancellationToken cancelacion);
}

/// <summary>
/// Reservar, en el orden del ADR-0059 §5: la petición, el cerrojo de la clave, el origen, la
/// caducidad, los maestros, las caducadas, el disponible y la reserva.
/// </summary>
/// <remarks>
/// <para>
/// <b>El disponible se mira con la valoración de la clave bloqueada</b>, que es la fila de cerrojo
/// de toda escritura sobre las reservas de la clave (ADR-0059 §2). Dos reservas a la vez sobre la
/// misma clave se esperan ahí, y la segunda lee lo que dejó la primera.
/// </para>
/// <para>
/// <b>La idempotencia es la del origen</b> (precisión 6): con la misma petición se devuelve la
/// reserva que ya tiene, esté como esté, y con otra, un <c>409</c>. Va después del cerrojo, para
/// que dos peticiones iguales a la vez se encuentren. Dos del mismo origen sobre claves distintas
/// no comparten cerrojo, y la segunda revienta en el índice único: es una excepción, y el
/// reintento de la bandeja encuentra la reserva.
/// </para>
/// <para>
/// <b>No tiene filtro de idempotencia que le abra la transacción</b>, porque no tiene ruta: la abre
/// la unidad de trabajo, como al contar una línea de un recuento.
/// </para>
/// </remarks>
/// <param name="usuarioActual">De dónde sale la empresa (R8).</param>
/// <param name="reservas">El cerrojo, el origen, lo reservado y la reserva.</param>
/// <param name="articulos">Si el artículo se almacena, y su unidad base.</param>
/// <param name="almacenes">Si el almacén admite operaciones nuevas.</param>
/// <param name="unidadTrabajo">La transacción.</param>
/// <param name="reloj">De dónde sale «ahora».</param>
internal sealed class Reservar(
    IUsuarioActual usuarioActual,
    IRepositorioDeReservas reservas,
    IConsultaDeArticulos articulos,
    IConsultaDeAlmacenes almacenes,
    IUnidadTrabajoDeInventario unidadTrabajo,
    TimeProvider reloj) : IReservar
{
    /// <inheritdoc/>
    public Task<Resultado<ReservaDto>> EjecutarAsync(ReservarDto peticion, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        return unidadTrabajo.EnTransaccionAsync(enCurso => ReservarAsync(peticion, enCurso), cancelacion);
    }

    private async Task<Resultado<ReservaDto>> ReservarAsync(ReservarDto peticion, CancellationToken cancelacion)
    {
        DateTimeOffset ahora = reloj.GetUtcNow();

        // 1. LA PETICIÓN, antes de cualquier cerrojo: su forma, que el alta lanzaría, se contesta aquí.
        if (!LoQueSeLePideALaReserva.EsUnOrigen(peticion.Origen))
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.OrigenNoValido());
        }

        if (!Reserva.EsUnaCantidadValida(peticion.Cantidad))
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.CantidadNoValida(null));
        }

        OrigenDeLaReserva origen = peticion.Origen;
        var clave = new ClaveDeValoracion(peticion.ArticuloId, peticion.AlmacenId);

        // 2. EL CERROJO, sin crear la fila: una clave sin ella tiene el físico a cero.
        decimal? fisico = await reservas.BloquearLaValoracionAsync(clave, cancelacion).ConfigureAwait(false);

        // 3. EL ORIGEN, ya con el cerrojo: la misma petición devuelve su reserva, y otra es un 409.
        Reserva? yaReservada = await reservas
            .ObtenerPorOrigenAsync(origen.Tipo, origen.Id, origen.Linea, cancelacion)
            .ConfigureAwait(false);

        if (yaReservada is not null)
        {
            return yaReservada.PideLoMismo(peticion.ArticuloId, peticion.AlmacenId, peticion.Cantidad, peticion.CaducaEl)
                ? Resultado.Correcto(yaReservada.ADto(ahora))
                : Resultado.Fallo<ReservaDto>(ErroresDeReserva.OrigenConOtraReserva(origen, yaReservada.Id));
        }

        // 4. LA CADUCIDAD, después del origen y no con la petición: depende de «ahora», y el
        // reintento de una petición que salió bien encuentra su reserva aunque haya caducado.
        if (!Reserva.CaducaDespuesDe(peticion.CaducaEl, ahora))
        {
            return Resultado.Fallo<ReservaDto>(ErroresDeReserva.CaducidadNoValida(peticion.CaducaEl!.Value));
        }

        // 5. LOS MAESTROS: el artículo se almacena, el almacén admite lo nuevo y hay unidad base.
        AptitudParaMoverExistencias aptitud = await articulos
            .AptitudDeAsync(peticion.ArticuloId, cancelacion)
            .ConfigureAwait(false);

        Resultado elArticulo = LosMaestrosDeLaReserva.ElArticulo(aptitud, peticion.ArticuloId);

        if (!elArticulo.EsCorrecto)
        {
            return Resultado.Fallo<ReservaDto>(elArticulo.Error!);
        }

        EstadoDeMaestro estadoDelAlmacen = await almacenes
            .EstadoDeAsync(peticion.AlmacenId, cancelacion)
            .ConfigureAwait(false);

        Resultado elAlmacen = LosMaestrosDeLaReserva.ElAlmacen(estadoDelAlmacen, peticion.AlmacenId);

        if (!elAlmacen.EsCorrecto)
        {
            return Resultado.Fallo<ReservaDto>(elAlmacen.Error!);
        }

        IReadOnlyDictionary<Guid, Guid> unidadesBase = await articulos
            .UnidadesBaseDeAsync([peticion.ArticuloId], cancelacion)
            .ConfigureAwait(false);

        if (!unidadesBase.TryGetValue(peticion.ArticuloId, out Guid unidadBaseId))
        {
            return Resultado.Fallo<ReservaDto>(LosMaestrosDeLaReserva.ArticuloNoEncontrado(peticion.ArticuloId));
        }

        // 6. LAS CADUCADAS DE LA CLAVE, que se escriben si la reserva sale (ADR-0059 §4).
        await LasCaducadasDeLaClave.LiberarAsync(reservas, clave, ahora, cancelacion).ConfigureAwait(false);

        // 7. EL DISPONIBLE, con la valoración bloqueada: lo reservado no puede cambiar mientras se lee.
        IReadOnlyDictionary<ClaveDeValoracion, decimal> reservado = await reservas
            .ReservadoDeAsync([clave], ahora, cancelacion)
            .ConfigureAwait(false);

        decimal disponible = (fisico ?? 0m) - reservado[clave];

        if (peticion.Cantidad > disponible)
        {
            return Resultado.Fallo<ReservaDto>(
                ErroresDeReserva.PorEncimaDelDisponible(clave, peticion.Cantidad, disponible));
        }

        // 8. LA RESERVA.
        var reserva = Reserva.Reservar(
            usuarioActual.EmpresaId,
            origen.Tipo,
            origen.Id,
            origen.Linea,
            peticion.ArticuloId,
            peticion.AlmacenId,
            peticion.Cantidad,
            unidadBaseId,
            peticion.CaducaEl,
            ahora);

        reservas.Agregar(reserva);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(reserva.ADto(ahora));
    }
}
