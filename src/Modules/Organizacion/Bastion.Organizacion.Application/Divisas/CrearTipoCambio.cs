using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Domain.Divisas;

namespace Bastion.Organizacion.Application.Divisas;

/// <summary>Registra la cotización de un par de divisas en un día.</summary>
public interface ICrearTipoCambio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos de la cotización.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TipoCambioDto>> EjecutarAsync(
        CrearTipoCambioDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearTipoCambio"/>
internal sealed class CrearTipoCambio(
    IRepositorioDeTiposDeCambio cambios,
    IRepositorioDeDivisas divisas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    TimeProvider reloj) : ICrearTipoCambio
{
    public async Task<Resultado<TipoCambioDto>> EjecutarAsync(
        CrearTipoCambioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();

        if (peticion.DivisaOrigenId == peticion.DivisaDestinoId)
        {
            // El dominio también lo rechaza, y allí lanza. Se adelanta porque quien lo manda ha
            // elegido dos veces la misma divisa en un desplegable, no ha roto ninguna invariante
            // interna: es un campo del formulario y merece que se le diga cuál.
            errores.Agregar(
                "divisaDestinoId",
                "Origen y destino son la misma divisa: una divisa vale exactamente uno de sí misma.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<TipoCambioDto>(errores.AError());
        }

        // Las dos claves ajenas son `Restrict`, así que la base también lo impediría; pero lo haría
        // con una violación de integridad convertida en 500, y esto no es un fallo del programa:
        // es una divisa que no está dada de alta, que es un 404 con su nombre.
        if (!await divisas
                .ExistenAsync([peticion.DivisaOrigenId, peticion.DivisaDestinoId], cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<TipoCambioDto>(ErrorDeOperacion.NoEncontrado(
                "divisa-no-encontrada",
                "Alguna de las dos divisas no está dada de alta."));
        }

        if (await cambios
                .ExisteAsync(peticion.DivisaOrigenId, peticion.DivisaDestinoId, peticion.Fecha, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<TipoCambioDto>(ErrorDeOperacion.Conflicto(
                "tipo-cambio-duplicado",
                $"Ese par de divisas ya tiene cotización del {peticion.Fecha:yyyy-MM-dd}. " +
                "Rectifíquela en vez de registrar otra: dos cotizaciones del mismo día " +
                "convertirían el mismo importe a dos valores distintos."));
        }

        var cambio = TipoCambio.Crear(
            peticion.DivisaOrigenId,
            peticion.DivisaDestinoId,
            peticion.Fecha,
            peticion.Tasa,
            reloj.GetUtcNow());

        cambios.Agregar(cambio);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(cambio.ADto());
    }
}
