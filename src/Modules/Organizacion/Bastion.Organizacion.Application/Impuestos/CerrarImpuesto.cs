using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Domain.Impuestos;

namespace Bastion.Organizacion.Application.Impuestos;

/// <summary>
/// Pone fecha de fin a un tramo, que es como se sustituye un tipo impositivo por otro.
/// </summary>
/// <remarks>
/// Permiso propio, distinto del de modificar, y con motivo: cerrar un tramo deja al código sin
/// tipo a partir del día siguiente. Si nadie abre el siguiente, la primera factura de ese día no
/// encuentra impuesto y la emisión se para.
/// </remarks>
public interface ICerrarImpuesto
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">El último día de vigencia.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ImpuestoDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CerrarImpuestoDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICerrarImpuesto"/>
internal sealed class CerrarImpuesto(
    IRepositorioDeImpuestos impuestos,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : ICerrarImpuesto
{
    public async Task<Resultado<ImpuestoDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CerrarImpuestoDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Impuesto? impuesto = await impuestos.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (impuesto is null)
        {
            return Resultado.Fallo<ImpuestoDto>(ErroresDeImpuesto.NoEncontrado(id));
        }

        versiones.Exigir(impuesto, version);

        if (peticion.UltimoDia < impuesto.VigenteDesde)
        {
            var errores = new ErroresPorCampo();
            errores.Agregar(
                "ultimoDia",
                "Un impuesto no puede dejar de regir antes de empezar a regir.");

            return Resultado.Fallo<ImpuestoDto>(errores.AError());
        }

        // Cerrar normalmente ENCOGE el tramo, y encoger no puede crear un solape. Pero cerrar un
        // tramo YA cerrado con una fecha posterior lo alarga, y ahí sí se puede montar encima del
        // siguiente. Por eso se comprueba también aquí, excluyéndose a sí mismo del recuento: sin
        // el `excepto`, todo tramo se solaparía consigo mismo y no se podría cerrar ninguno.
        if (await impuestos
                .HaySolapeAsync(impuesto.Codigo, impuesto.VigenteDesde, peticion.UltimoDia, id, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<ImpuestoDto>(ErroresDeImpuesto.Solapado(impuesto.Codigo));
        }

        impuesto.Cerrar(peticion.UltimoDia);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(impuesto.ADto());
    }
}
