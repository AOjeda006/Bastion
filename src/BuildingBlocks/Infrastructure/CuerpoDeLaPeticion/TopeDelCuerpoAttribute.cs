using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bastion.BuildingBlocks.Infrastructure.CuerpoDeLaPeticion;

/// <summary>
/// Declara cuántos bytes de cuerpo admite una acción, y lo hace cumplir antes de que nadie lo lea.
/// </summary>
/// <remarks>
/// <para>
/// <b>El atributo ES el filtro</b>, y no una marca que otro filtro registrado aparte tenga que ir a
/// buscar. Si fuera solo una marca, olvidarse de registrar ese filtro dejaría la acción con su tope
/// escrito y sin nadie que lo impusiera, y en la revisión se leería como protegida.
/// </para>
/// <para>
/// <b>Corre el primero de los filtros de recurso</b> (<see cref="Order"/> es el mínimo), así que llega
/// antes que el de idempotencia, que es el otro que vuelca el cuerpo. Aun así el de idempotencia no
/// confía en ese orden: si ve este atributo, lee con el mismo <see cref="LectorAcotadoDelCuerpo"/>,
/// que no lee dos veces. El orden de los filtros decide quién lee primero, nunca si se lee con tope.
/// </para>
/// </remarks>
/// <param name="bytes">El tope, en bytes. Tiene que ser positivo.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TopeDelCuerpoAttribute(long bytes) : Attribute, IAsyncResourceFilter, IOrderedFilter
{
    /// <summary>El tope, en bytes.</summary>
    public long Bytes { get; } = bytes > 0
        ? bytes
        : throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "El tope del cuerpo tiene que ser positivo.");

    /// <inheritdoc />
    public int Order => int.MinValue;

    /// <inheritdoc />
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        Resultado<ReadOnlyMemory<byte>> cuerpo = await LectorAcotadoDelCuerpo
            .LeerAsync(context.HttpContext, Bytes, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!cuerpo.EsCorrecto)
        {
            context.Result = cuerpo.Error!.AResultadoDeAccion();
            return;
        }

        await next().ConfigureAwait(false);
    }
}
