using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Domain.Almacenes;

namespace Bastion.Organizacion.Application.Almacenes;

/// <summary>Cambia los datos de un almacén.</summary>
public interface IModificarAlmacen
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del almacén.</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<AlmacenDto>> EjecutarAsync(
        Guid id,
        ModificarAlmacenDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarAlmacen"/>
internal sealed class ModificarAlmacen(IRepositorioDeAlmacenes almacenes, IUnidadTrabajoDeOrganizacion unidadTrabajo)
    : IModificarAlmacen
{
    public async Task<Resultado<AlmacenDto>> EjecutarAsync(
        Guid id,
        ModificarAlmacenDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Almacen? almacen = await almacenes.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (almacen is null)
        {
            return Resultado.Fallo<AlmacenDto>(ErroresDeAlmacen.NoEncontrado(id));
        }

        if (almacen.Estado == EstadoDeAlmacen.Bloqueado)
        {
            return Resultado.Fallo<AlmacenDto>(ErroresDeAlmacen.Bloqueado(id));
        }

        var errores = new ErroresPorCampo();

        if (!Enumerados.Intentar(peticion.Tipo, out TipoDeAlmacen tipo))
        {
            errores.Agregar(
                "tipo",
                $"No es un tipo de almacén conocido. Admitidos: {Enumerados.Admitidos<TipoDeAlmacen>()}.");
        }
        else if (tipo == TipoDeAlmacen.Fisico && peticion.Direccion is null)
        {
            errores.Agregar("direccion", "Un almacén físico tiene dirección: es un sitio al que llega mercancía.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<AlmacenDto>(errores.AError());
        }

        almacen.Modificar(peticion.Nombre, peticion.Direccion?.ADireccion(), tipo);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(almacen.ADto());
    }
}
