using Bastion.BuildingBlocks.Application.Concurrencia;
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
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<AlmacenDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarAlmacenDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarAlmacen"/>
internal sealed class ModificarAlmacen(
    IRepositorioDeAlmacenes almacenes,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarAlmacen
{
    public async Task<Resultado<AlmacenDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarAlmacenDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Almacen? almacen = await almacenes.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (almacen is null)
        {
            return Resultado.Fallo<AlmacenDto>(ErroresDeAlmacen.NoEncontrado(id));
        }

        versiones.Exigir(almacen, version);

        // Aquí NO se comprueba si está bloqueado, y desde el 0.10 no se puede: la consulta de
        // arriba ya no trae lo bloqueado, así que la respuesta ordinaria a modificar un almacén
        // bloqueado es el 404 de ahí arriba.
        //
        // El motivo de bloquear un almacén no es el art. 32 —un almacén no es una persona— sino
        // no romper la valoración histórica; pero el MECANISMO es el mismo y no lleva lista de
        // excepciones, a propósito. Una excepción «solo para el almacén, que no es un dato
        // personal» sería el primer sitio donde mirar para saber si el filtro tapa de verdad, y
        // la segunda excepción llegaría con menos discusión que la primera.
        //
        // La invariante sigue dentro de la entidad (`Modificar` lanza si está bloqueado) porque
        // ahí protege a quien lo modifique DESDE un ámbito abierto a propósito, que es el único
        // sitio desde el que se puede llegar a él.

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
