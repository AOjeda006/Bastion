using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Domain.Divisas;

namespace Bastion.Organizacion.Application.Divisas;

/// <summary>Cambia el nombre de una divisa.</summary>
public interface IModificarDivisa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la divisa.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<DivisaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarDivisaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarDivisa"/>
internal sealed class ModificarDivisa(
    IRepositorioDeDivisas divisas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IModificarDivisa
{
    public async Task<Resultado<DivisaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarDivisaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Divisa? divisa = await divisas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (divisa is null)
        {
            return Resultado.Fallo<DivisaDto>(ErroresDeDivisa.NoEncontrada(id));
        }

        versiones.Exigir(divisa, version);

        divisa.Modificar(peticion.Nombre);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(divisa.ADto());
    }
}
