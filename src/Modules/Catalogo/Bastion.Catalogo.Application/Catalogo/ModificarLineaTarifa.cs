using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Cambia el precio o el descuento de una línea de tarifa.</summary>
public interface IModificarLineaTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la línea.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<LineaTarifaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarLineaTarifaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarLineaTarifa"/>
/// <remarks>
/// <b>Ni el destino ni la cantidad desde la que se aplica</b>, y lo segundo es lo que cierra el
/// hueco del todo: si el tramo que empieza en cero pudiera moverse a cinco, una cantidad de tres se
/// quedaría sin tramo <b>sin que nadie hubiera borrado nada</b> y sin ninguna comprobación que
/// pudiera enterarse. Corregir la escala de una tarifa es añadir el tramo que falta.
/// </remarks>
internal sealed class ModificarLineaTarifa(
    IRepositorioDeLineasDeTarifa lineas,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : IModificarLineaTarifa
{
    public async Task<Resultado<LineaTarifaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarLineaTarifaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        LineaTarifa? linea = await lineas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (linea is null)
        {
            return Resultado.Fallo<LineaTarifaDto>(ErroresDeTarifa.LineaNoEncontrada(id));
        }

        versiones.Exigir(linea, version);

        // Los dos negativos otra vez, y aquí importan igual: una línea que ya tenía precio y se
        // modifica dejando los dos campos vacíos se quedaría sin decir nada, que es el cero por la
        // puerta de atrás con un paso más.
        if (peticion.Precio is not null == (peticion.DescuentoPorcentaje is not null))
        {
            return Resultado.Fallo<LineaTarifaDto>(ErroresDeTarifa.PrecioODescuento());
        }

        linea.Modificar(PrecioODescuento.De(peticion.Precio, peticion.DescuentoPorcentaje));

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(linea.ADto());
    }
}
