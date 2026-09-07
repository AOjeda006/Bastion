using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Terceros.Application.Comun;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Las condiciones de pago de un tercero: como mucho una por rol.</summary>
public interface IListarCondicionesPago
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelgan.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<IReadOnlyList<CondicionPagoDto>>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion);
}

/// <summary>Fija la condición de pago de un rol: la crea, o cambia la que ya hubiera.</summary>
public interface IFijarCondicionPago
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="terceroId">La ficha de la que cuelga.</param>
    /// <param name="rol">De qué cara es, tal como llegó en la ruta.</param>
    /// <param name="version">La versión de la FICHA sobre la que se escribe.</param>
    /// <param name="peticion">El plazo, el día fijo y el descuento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<CondicionPagoDto>> EjecutarAsync(
        Guid terceroId,
        string rol,
        VersionDeRecurso version,
        CondicionPagoDeAltaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IListarCondicionesPago"/>
internal sealed class ListarCondicionesPago(IRepositorioDeTerceros terceros)
    : IListarCondicionesPago
{
    public async Task<Resultado<IReadOnlyList<CondicionPagoDto>>> EjecutarAsync(
        Guid terceroId,
        CancellationToken cancelacion)
    {
        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        return tercero is null
            ? Resultado.Fallo<IReadOnlyList<CondicionPagoDto>>(
                ErroresDeTercero.NoEncontrado(terceroId))
            : Resultado.Correcto<IReadOnlyList<CondicionPagoDto>>(
                [.. tercero.CondicionesPago.Select(condicion => condicion.ADto())]);
    }
}

/// <inheritdoc cref="IFijarCondicionPago"/>
internal sealed class FijarCondicionPago(
    IRepositorioDeTerceros terceros,
    IUnidadTrabajoDeTerceros unidadTrabajo,
    IVersionesDeTerceros versiones,
    TimeProvider reloj) : IFijarCondicionPago
{
    public async Task<Resultado<CondicionPagoDto>> EjecutarAsync(
        Guid terceroId,
        string rol,
        VersionDeRecurso version,
        CondicionPagoDeAltaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();

        // El rol viene de la RUTA, así que un valor que no existe es una ruta que no existe. Aun
        // así se contesta como error de campo y no como 404, porque el recurso que se ha nombrado
        // —la ficha— sí existe, y lo que está mal es el segmento que el cliente compuso.
        if (!Array.Exists(
                Enum.GetNames<RolDeCondicionPago>(),
                nombre => string.Equals(nombre, rol, StringComparison.OrdinalIgnoreCase)))
        {
            errores.Agregar(
                "rol",
                "La condición de pago es del cliente o del proveedor: " +
                string.Join(", ", Enum.GetNames<RolDeCondicionPago>()) + ".");

            return Resultado.Fallo<CondicionPagoDto>(errores.AError());
        }

        Tercero? tercero = await terceros
            .ObtenerConLoQueCuelgaAsync(terceroId, cancelacion)
            .ConfigureAwait(false);

        if (tercero is null)
        {
            return Resultado.Fallo<CondicionPagoDto>(ErroresDeTercero.NoEncontrado(terceroId));
        }

        versiones.Exigir(tercero, version);

        // EL TOPE DE SESENTA DÍAS SE ADELANTA AQUÍ, y el dominio lo vuelve a exigir lanzando. No
        // es duplicar por duplicar: este lo pone para que quien rellena el formulario lea POR QUÉ
        // se le rechaza en lugar de recibir un 500, y el del dominio para que la regla siga puesta
        // cuando la llamada venga de una importación de CSV, de una migración o de un test. El que
        // manda es el del dominio; este solo es el que sabe explicarse.
        if (peticion.DiasDePlazo is > CondicionPago.DiasMaximosDePlazo or < 0)
        {
            errores.Agregar(
                "diasDePlazo",
                $"El plazo máximo de pago son {CondicionPago.DiasMaximosDePlazo} días naturales " +
                "desde la entrega o la prestación (art. 4 de la Ley 3/2004), y no es ampliable " +
                "por acuerdo entre las partes.");
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<CondicionPagoDto>(errores.AError());
        }

        CondicionPago condicion = tercero.FijarCondicionPago(
            Enum.Parse<RolDeCondicionPago>(rol, ignoreCase: true),
            peticion.DiasDePlazo,
            peticion.DiaDePagoFijo,
            peticion.DescuentoPorProntoPago,
            reloj.GetUtcNow());

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(condicion.ADto());
    }
}
