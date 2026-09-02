using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Domain.Impuestos;

namespace Bastion.Organizacion.Application.Impuestos;

/// <summary>
/// Cambia el nombre y las cuentas contables de un tramo.
/// </summary>
/// <remarks>
/// Ni el porcentaje ni las fechas: eso sería reescribir lo que decía el BOE en un periodo que ya
/// pasó, y la cuota de las facturas emitidas con ese tramo cambiaría sin dejar rastro. Un tipo
/// nuevo se abre con <see cref="ICrearImpuesto"/> después de cerrar el anterior.
/// </remarks>
public interface IModificarImpuesto
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ImpuestoDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarImpuestoDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarImpuesto"/>
internal sealed class ModificarImpuesto(
    IRepositorioDeImpuestos impuestos,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarImpuesto
{
    public async Task<Resultado<ImpuestoDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarImpuestoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Impuesto? impuesto = await impuestos.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (impuesto is null)
        {
            return Resultado.Fallo<ImpuestoDto>(ErroresDeImpuesto.NoEncontrado(id));
        }

        versiones.Exigir(impuesto, version);

        impuesto.Modificar(peticion.Nombre, peticion.CuentaRepercutido, peticion.CuentaSoportado);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(impuesto.ADto());
    }
}
