using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>Da de alta una conversión entre dos unidades de medida.</summary>
public interface ICrearConversionUm
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos de la conversión.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ConversionUmDto>> EjecutarAsync(
        CrearConversionUmDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearConversionUm"/>
internal sealed class CrearConversionUm(
    IRepositorioDeConversiones conversiones,
    IRepositorioDeUnidadesDeMedida unidades,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    TimeProvider reloj) : ICrearConversionUm
{
    public async Task<Resultado<ConversionUmDto>> EjecutarAsync(
        CrearConversionUmDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        if (peticion.UnidadOrigenId == peticion.UnidadDestinoId)
        {
            var errores = new ErroresPorCampo();
            errores.Agregar(
                "unidadDestinoId",
                "Origen y destino son la misma unidad: convertir un kilo a kilos es multiplicar " +
                "por uno, y esa fila solo puede sobrar o mentir.");

            return Resultado.Fallo<ConversionUmDto>(errores.AError());
        }

        // Las claves ajenas son `Restrict` y la base también lo impediría, con una violación de
        // integridad convertida en 500. Una unidad que no está dada de alta no es un fallo del
        // programa: es un 404 con su nombre.
        if (!await unidades
                .ExistenAsync([peticion.UnidadOrigenId, peticion.UnidadDestinoId], cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<ConversionUmDto>(ErrorDeOperacion.NoEncontrado(
                "unidad-medida-no-encontrada",
                "Alguna de las dos unidades no está dada de alta."));
        }

        if (await conversiones
                .ExisteAsync(peticion.UnidadOrigenId, peticion.UnidadDestinoId, cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<ConversionUmDto>(ErrorDeOperacion.Conflicto(
                "conversion-um-duplicada",
                "Ese par de unidades ya tiene conversión. Corrija el factor en vez de dar de alta " +
                "otra: dos factores para el mismo par convertirían la misma cantidad a dos valores."));
        }

        // ADR-0023, decisión 2: si la fila del sentido contrario está declarada, este factor tiene
        // que ser su inversa dentro del margen que impone la escala. Se lee POR EL PAR y no por
        // identificador, y trae también las retiradas — una fila retirada sigue resolviendo, así
        // que sigue siendo verdad y sigue restringiendo (decisión 2 del ítem 1.7).
        ConversionUM? inversa = await conversiones
            .DelParAsync(peticion.UnidadDestinoId, peticion.UnidadOrigenId, cancelacion)
            .ConfigureAwait(false);

        // Con el factor YA redondeado: es el número que se va a guardar, y el ADR compara los dos
        // sentidos «ambos ya redondeados a seis decimales».
        Resultado plausible = LaInversaEsPlausible.Comprobar(
            inversa, ConversionUM.RedondearFactor(peticion.Factor));

        if (!plausible.EsCorrecto)
        {
            return Resultado.Fallo<ConversionUmDto>(plausible.Error!);
        }

        var conversion = ConversionUM.Crear(
            peticion.UnidadOrigenId, peticion.UnidadDestinoId, peticion.Factor, reloj.GetUtcNow());

        conversiones.Agregar(conversion);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(conversion.ADto());
    }
}
