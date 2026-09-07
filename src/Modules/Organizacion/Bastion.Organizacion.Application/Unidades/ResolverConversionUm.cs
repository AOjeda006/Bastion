using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Unidades;

namespace Bastion.Organizacion.Application.Unidades;

/// <summary>
/// Resuelve la conversión declarada entre dos unidades, o falla con nombre (ADR-0023, decisión 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>El caso que importa no es el par declarado.</b> Es este: <c>kg→g</c> y <c>g→mg</c> dados de
/// alta, <c>kg→mg</c> preguntado. La respuesta es el error con nombre y <b>no</b> un 1000000. El
/// dominio ya decía «no hay transitividad»; lo que faltaba —y es la mitad que se usa— era qué pasa
/// cuando alguien la pide, porque sin decidirlo lo decide la primera línea que lo necesite y las
/// tres salidas posibles (multiplicar, cero, nulo) son las tres peores.
/// </para>
/// <para>
/// <b>Una fila retirada SÍ resuelve.</b> «No se ofrece para operaciones nuevas, pero sigue
/// resolviendo para lo que ya apunta a ella»: resolver es exactamente lo que sigue haciendo. Lo que
/// deja de hacer es salir en la colección por omisión y ofrecerse para elegir. Quien resuelve por
/// una fila retirada se entera, porque la respuesta lo dice.
/// </para>
/// <para>
/// <b>Ni identidad ni simetría.</b> Preguntar <c>kg→kg</c> falla igual que cualquier otro par no
/// declarado, y no devuelve 1: una fila «una unidad a sí misma» no se puede ni dar de alta —el
/// dominio lo prohíbe— así que inventarla aquí sería el sistema respondiendo por una conversión que
/// nadie declaró, que es justo lo que este caso de uso existe para no hacer. Y preguntar
/// <c>B→A</c> no resuelve con la fila <c>A→B</c> invertida, por lo mismo que los dos sentidos son
/// dos filas: <c>1/12</c> no cabe en seis decimales.
/// </para>
/// </remarks>
public interface IResolverConversionUm
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="unidadOrigenId">Unidad de la que se parte.</param>
    /// <param name="unidadDestinoId">Unidad a la que se llega.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ResolucionDeConversionDto>> EjecutarAsync(
        Guid unidadOrigenId,
        Guid unidadDestinoId,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IResolverConversionUm"/>
internal sealed class ResolverConversionUm(IRepositorioDeConversiones conversiones)
    : IResolverConversionUm
{
    public async Task<Resultado<ResolucionDeConversionDto>> EjecutarAsync(
        Guid unidadOrigenId,
        Guid unidadDestinoId,
        CancellationToken cancelacion)
    {
        ConversionUM? conversion = await conversiones
            .DelParAsync(unidadOrigenId, unidadDestinoId, cancelacion)
            .ConfigureAwait(false);

        // Una sola lectura, del par exacto. No hay segundo intento por el sentido contrario ni
        // búsqueda de caminos: las dos cosas serían componer una conversión que nadie declaró.
        return conversion is null
            ? Resultado.Fallo<ResolucionDeConversionDto>(
                ErroresDeUnidad.ConversionNoDeclarada(unidadOrigenId, unidadDestinoId))
            : Resultado.Correcto(new ResolucionDeConversionDto(
                conversion.Id,
                conversion.UnidadOrigenId,
                conversion.UnidadDestinoId,
                conversion.Factor,
                conversion.EstaRetirada));
    }
}
