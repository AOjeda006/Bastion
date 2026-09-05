using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Direcciones;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Cambia los datos de un tercero. El identificador fiscal no está entre ellos.</summary>
public interface IModificarTercero
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tercero.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TerceroDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarTerceroDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarTercero"/>
internal sealed class ModificarTercero(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones) : IModificarTercero
{
    public async Task<Resultado<TerceroDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarTerceroDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Tercero? tercero = await terceros.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo<TerceroDto>(ErroresDeTercero.NoEncontrado(id));
        }

        versiones.Exigir(tercero, version);

        // Aquí NO se comprueba si está bloqueado, y no se puede: la consulta de arriba no trae lo
        // bloqueado, así que la respuesta a modificar un tercero bloqueado es el 404 de ahí
        // arriba. La invariante sigue dentro de la entidad porque ahí protege a quien lo modifique
        // DESDE un ámbito abierto a propósito, que es el único sitio desde el que se puede llegar.
        var errores = new ErroresPorCampo();

        if (!peticion.EsCliente && !peticion.EsProveedor)
        {
            errores.Agregar(
                "esCliente",
                "Marque al menos uno de los dos: a un tercero se le vende, se le compra, o las " +
                "dos cosas.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<TerceroDto>(errores.AError());
        }

        tercero.Modificar(
            peticion.RazonSocial,
            peticion.NombreComercial,
            peticion.DomicilioFiscal.ADireccion(),
            peticion.EsCliente,
            peticion.EsProveedor);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tercero.ADto());
    }
}
