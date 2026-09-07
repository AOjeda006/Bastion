using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Cuánto se le fía a un tercero, si se le fía.</summary>
public interface IObtenerLimiteCredito
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<LimiteCreditoDto>> EjecutarAsync(Guid terceroId, CancellationToken cancelacion);
}

/// <summary>Fija —o retira— el límite de crédito de un tercero.</summary>
public interface IFijarLimiteCredito
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">El importe con su divisa, o nada para retirarlo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<LimiteCreditoDto>> EjecutarAsync(
        Guid terceroId,
        VersionDeRecurso version,
        LimiteCreditoDeAltaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IObtenerLimiteCredito"/>
internal sealed class ObtenerLimiteCredito(IRepositorioDeTerceros terceros)
    : IObtenerLimiteCredito
{
    public async Task<Resultado<LimiteCreditoDto>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros.ObtenerAsync(terceroId, cancelacion).ConfigureAwait(false);

        // `ObtenerAsync` y no la versión con lo que cuelga: el límite vive en la propia fila de la
        // ficha, así que cargarle las tres colecciones sería pagar tres consultas por nada.
        return tercero is null
            ? Resultado.Fallo<LimiteCreditoDto>(ErroresDeTercero.NoEncontrado(terceroId))
            : Resultado.Correcto(tercero.ALimiteDto());
    }
}

/// <inheritdoc cref="IFijarLimiteCredito"/>
internal sealed class FijarLimiteCredito(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones) : IFijarLimiteCredito
{
    public async Task<Resultado<LimiteCreditoDto>> EjecutarAsync(
        Guid terceroId,
        VersionDeRecurso version,
        LimiteCreditoDeAltaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Tercero? tercero = await terceros.ObtenerAsync(terceroId, cancelacion).ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo<LimiteCreditoDto>(ErroresDeTercero.NoEncontrado(terceroId));
        }

        versiones.Exigir(tercero, version);

        var errores = new ErroresPorCampo();
        Importe? limite = LeerImporte(peticion, errores);

        if (errores.Hay)
        {
            return Resultado.Fallo<LimiteCreditoDto>(errores.AError());
        }

        tercero.FijarLimiteDeCredito(limite);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tercero.ALimiteDto());
    }

    /// <summary>
    /// Convierte el par (cantidad, divisa) en un importe, o en errores por campo.
    /// </summary>
    /// <remarks>
    /// <b>LA DIVISA NO SE HEREDA EN SILENCIO DE LA EMPRESA</b>, y esta función es donde esa
    /// decisión se puede desobedecer, así que queda escrita aquí. Heredarla —leer
    /// <c>Empresa.DivisaBase</c> cuando el cuerpo no la traiga— parece cómodo y tiene un modo de
    /// fallo mudo: el día que alguien cambie la divisa base de la empresa, todos los límites ya
    /// guardados cambiarían de significado sin que se toque ninguna fila y sin que nada falle. Lo
    /// que la pantalla ofrece por omisión es otra cosa, y es de la pantalla.
    /// </remarks>
    private static Importe? LeerImporte(LimiteCreditoDeAltaDto peticion, ErroresPorCampo errores)
    {
        // Sin cantidad, no hay límite. Que es distinto de un límite de cero: cero es «no se le
        // fía ni un euro», y no tener límite es «no se le controla el crédito».
        if (peticion.Cantidad is null)
        {
            if (!string.IsNullOrWhiteSpace(peticion.Divisa))
            {
                errores.Agregar(
                    "cantidad",
                    "Ha llegado una divisa sin importe. Para retirar el límite, envíe los dos " +
                    "campos vacíos.");
            }

            return null;
        }

        if (peticion.Cantidad < 0m)
        {
            errores.Agregar("cantidad", "Un límite de crédito no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(peticion.Divisa))
        {
            errores.Agregar(
                "divisa",
                "Un límite de crédito lleva su divisa (ISO 4217). No se hereda de la empresa: un " +
                "importe que no dice de qué es sería la única cantidad de dinero del sistema que " +
                "no lo dice.");

            return null;
        }

        // `EsConocida` y no `Normalizar`: la que PREGUNTA, no la que EXIGE. Una divisa que el
        // catálogo no sabe redondear es un error del formulario y tiene que salir por el campo
        // `divisa`; dentro, en cambio, lanzaría — es la puerta doble del ADR-0004.
        if (!CatalogoDeDivisas.EsConocida(peticion.Divisa))
        {
            errores.Agregar(
                "divisa",
                "No es una divisa de las que el sistema sabe redondear (ISO 4217).");

            return null;
        }

        return errores.Hay ? null : Importe.De(peticion.Cantidad.Value, peticion.Divisa);
    }
}
