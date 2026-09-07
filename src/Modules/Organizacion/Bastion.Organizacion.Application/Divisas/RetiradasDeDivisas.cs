using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;

namespace Bastion.Organizacion.Application.Divisas;

/// <summary>
/// Retira una divisa: deja de ofrecerse para operaciones nuevas y sigue resolviendo lo viejo.
/// </summary>
/// <remarks>
/// <b>No es un borrado y no hay ninguno</b> (ADR-0023): una factura emitida en pesetas tiene que
/// poder seguir diciendo en qué se emitió mucho después de que nadie pueda emitir una nueva. Por
/// eso el <c>GET</c> por identificador la sigue devolviendo, al revés que una fila bloqueada.
/// </remarks>
public interface IRetirarDivisa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la divisa.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>Vuelve a ofrecer una divisa retirada.</summary>
/// <remarks>
/// Existe porque la retirada tiene el mismo radio que el error que arregla: los cuatro maestros
/// son de <b>instalación</b> (R8), así que retirar por error deja a todas las empresas sin una
/// divisa. Una retirada irreversible cambiaría un error permanente por otro.
/// </remarks>
public interface IReincorporarDivisa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la divisa.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>Retira una cotización.</summary>
/// <remarks>
/// Es el caso del ADR-0023 escrito con todas las letras: una cotización dada de alta con el par o
/// la fecha que no eran no se puede arreglar cambiando esos campos —<c>Modificar</c> solo toca la
/// tasa, a propósito— y tampoco se puede borrar. Retirarla es lo único que quedaba por existir.
/// </remarks>
public interface IRetirarTipoCambio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la cotización.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>Vuelve a ofrecer una cotización retirada.</summary>
public interface IReincorporarTipoCambio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la cotización.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IRetirarDivisa"/>
internal sealed class RetirarDivisa(
    IRepositorioDeDivisas divisas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IRetirarDivisa
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        await CambioDeRetirada.AplicarAsync(
            await divisas.ObtenerAsync(id, cancelacion).ConfigureAwait(false),
            version,
            retirar: true,
            ErroresDeDivisa.NoEncontrada(id),
            versiones,
            unidadTrabajo,
            cancelacion).ConfigureAwait(false);
}

/// <inheritdoc cref="IReincorporarDivisa"/>
internal sealed class ReincorporarDivisa(
    IRepositorioDeDivisas divisas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IReincorporarDivisa
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        await CambioDeRetirada.AplicarAsync(
            await divisas.ObtenerAsync(id, cancelacion).ConfigureAwait(false),
            version,
            retirar: false,
            ErroresDeDivisa.NoEncontrada(id),
            versiones,
            unidadTrabajo,
            cancelacion).ConfigureAwait(false);
}

/// <inheritdoc cref="IRetirarTipoCambio"/>
internal sealed class RetirarTipoCambio(
    IRepositorioDeTiposDeCambio cambios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IRetirarTipoCambio
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        await CambioDeRetirada.AplicarAsync(
            await cambios.ObtenerAsync(id, cancelacion).ConfigureAwait(false),
            version,
            retirar: true,
            ErroresDeDivisa.CambioNoEncontrado(id),
            versiones,
            unidadTrabajo,
            cancelacion).ConfigureAwait(false);
}

/// <inheritdoc cref="IReincorporarTipoCambio"/>
internal sealed class ReincorporarTipoCambio(
    IRepositorioDeTiposDeCambio cambios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IReincorporarTipoCambio
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        await CambioDeRetirada.AplicarAsync(
            await cambios.ObtenerAsync(id, cancelacion).ConfigureAwait(false),
            version,
            retirar: false,
            ErroresDeDivisa.CambioNoEncontrado(id),
            versiones,
            unidadTrabajo,
            cancelacion).ConfigureAwait(false);
}
