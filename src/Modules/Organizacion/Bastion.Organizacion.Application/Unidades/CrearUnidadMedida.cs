using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>Da de alta una unidad de medida.</summary>
public interface ICrearUnidadMedida
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos de la unidad.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<UnidadMedidaDto>> EjecutarAsync(
        CrearUnidadMedidaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearUnidadMedida"/>
internal sealed class CrearUnidadMedida(
    IRepositorioDeUnidadesDeMedida unidades,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    TimeProvider reloj) : ICrearUnidadMedida
{
    public async Task<Resultado<UnidadMedidaDto>> EjecutarAsync(
        CrearUnidadMedidaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        string codigo = UnidadMedida.NormalizarCodigo(peticion.Codigo);

        if (await unidades.ExisteElCodigoAsync(codigo, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<UnidadMedidaDto>(ErrorDeOperacion.Conflicto(
                "unidad-medida-duplicada",
                $"Ya hay una unidad de medida con el código {codigo}."));
        }

        var unidad = UnidadMedida.Crear(
            peticion.Codigo, peticion.Nombre, peticion.Decimales, reloj.GetUtcNow());

        unidades.Agregar(unidad);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(unidad.ADto());
    }
}
