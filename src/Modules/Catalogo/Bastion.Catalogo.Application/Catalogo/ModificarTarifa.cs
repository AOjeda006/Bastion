using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Cambia el nombre de un tramo de tarifa. Ni el código, ni la divisa, ni la vigencia.</summary>
public interface IModificarTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TarifaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarTarifaDto peticion,
        CancellationToken cancelacion);
}

/// <summary>Cierra un tramo de tarifa, que es como se sustituye una tarifa por la siguiente.</summary>
public interface ICerrarTarifa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del tramo.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">El último día en que rige.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TarifaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CerrarTarifaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarTarifa"/>
/// <remarks>
/// <b>Solo el nombre, y ésa es la decisión entera.</b> El código, la divisa y la vigencia describen
/// los precios que ya se aplicaron bajo esta fila: cambiarlos los reinterpretaría hacia atrás, sin
/// tocar un solo número, la próxima vez que alguien reimprimiera un albarán emitido mientras regía.
/// Subir precios es <see cref="CerrarTarifa"/> y abrir el siguiente tramo con el mismo código.
/// Y como no se revalida nada ajeno —la divisa no se puede cambiar—, una divisa retirada no puede
/// impedir que se corrija el nombre de una tarifa que la usa: la otra mitad del ADR-0023, otra vez
/// en forma de algo que aquí no se hace.
/// </remarks>
internal sealed class ModificarTarifa(
    IRepositorioDeTarifas tarifas,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : IModificarTarifa
{
    public async Task<Resultado<TarifaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarTarifaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Tarifa? tarifa = await tarifas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (tarifa is null)
        {
            return Resultado.Fallo<TarifaDto>(ErroresDeTarifa.NoEncontrada(id));
        }

        versiones.Exigir(tarifa, version);

        tarifa.Modificar(peticion.Nombre);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tarifa.ADto());
    }
}

/// <inheritdoc cref="ICerrarTarifa"/>
/// <remarks>
/// <b>Cerrar también puede solapar, y por eso pregunta lo mismo que el alta.</b> Cerrar un tramo
/// abierto lo estrecha y no puede pisar a nadie; pero <b>alargar</b> uno ya cerrado sí, y es la
/// misma operación vista desde la otra punta. La consulta se hace con <c>excepto</c> puesto en este
/// tramo, porque un tramo siempre se pisa consigo mismo.
/// </remarks>
internal sealed class CerrarTarifa(
    IRepositorioDeTarifas tarifas,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : ICerrarTarifa
{
    public async Task<Resultado<TarifaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CerrarTarifaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Tarifa? tarifa = await tarifas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (tarifa is null)
        {
            return Resultado.Fallo<TarifaDto>(ErroresDeTarifa.NoEncontrada(id));
        }

        versiones.Exigir(tarifa, version);

        if (peticion.UltimoDia < tarifa.VigenteDesde)
        {
            return Resultado.Fallo<TarifaDto>(
                ErroresDeTarifa.VigenciaAlReves(tarifa.VigenteDesde, peticion.UltimoDia));
        }

        if (await tarifas
                .HaySolapeAsync(
                    tarifa.EmpresaId,
                    tarifa.Codigo,
                    tarifa.VigenteDesde,
                    peticion.UltimoDia,
                    tarifa.Id,
                    cancelacion)
                .ConfigureAwait(false))
        {
            return Resultado.Fallo<TarifaDto>(ErroresDeTarifa.VigenciasSolapadas(tarifa.Codigo));
        }

        tarifa.Cerrar(peticion.UltimoDia);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(tarifa.ADto());
    }
}
