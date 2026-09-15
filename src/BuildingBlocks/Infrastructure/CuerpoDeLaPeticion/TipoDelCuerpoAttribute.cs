using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Bastion.BuildingBlocks.Infrastructure.CuerpoDeLaPeticion;

/// <summary>
/// Declara el único tipo de contenido que admite una acción, y contesta <c>415</c> a cualquier otro
/// DESPUÉS de la autorización.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no <c>[Consumes]</c>.</b> <see cref="ConsumesAttribute"/> es también metadato del
/// enrutador, y el enrutador descarta la acción al elegirla: el <c>415</c> sale del enrutamiento, antes
/// de <c>UseAuthorization</c>, y a esa respuesta solo le alcanza la política por omisión —estar
/// autenticado—. Cualquiera que haya entrado, con el permiso que sea, recibía <c>415</c> en vez de
/// <c>403</c>: la puerta, después del tipo de contenido. Lo destapó <c>LaPuertaDeCadaAccionTests</c> con
/// la importación del ítem 1.11. Como filtro de recurso, la puerta ya ha decidido cuando esto mira.
/// </para>
/// <para>
/// <b>Corre el primero de los filtros de recurso</b> (<see cref="Order"/> es el mínimo), antes que
/// <see cref="TopeDelCuerpoAttribute"/>: rechazar por el tipo no necesita leer un solo byte, y un cuerpo
/// que se va a rechazar no se lee. Y antes que el filtro de idempotencia, que no llega a abrir su
/// transacción.
/// </para>
/// <para>
/// <b>Lo que el contrato publica sigue siendo el mismo</b>: el explorador de la API toma el tipo de
/// <see cref="SetContentTypes"/>, igual que lo tomaba de <c>[Consumes]</c>.
/// </para>
/// </remarks>
/// <param name="tipo">El tipo de contenido, sin parámetros; los que traiga la petición no cuentan.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TipoDelCuerpoAttribute(string tipo) : Attribute, IResourceFilter, IOrderedFilter, IApiRequestMetadataProvider
{
    private readonly MediaType _admitido = new(tipo);

    /// <summary>El tipo de contenido admitido.</summary>
    public string Tipo { get; } = tipo;

    /// <inheritdoc />
    public int Order => int.MinValue;

    /// <inheritdoc />
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Sin tipo también es 415: lo que no se declara no se adivina. Y un tipo mal formado no
        // lanza, se queda sin tipo y no es subconjunto de nada.
        string? recibido = context.HttpContext.Request.ContentType;

        if (string.IsNullOrEmpty(recibido) || !new MediaType(recibido).IsSubsetOf(_admitido))
        {
            context.Result = new UnsupportedMediaTypeResult();
        }
    }

    /// <inheritdoc />
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }

    /// <inheritdoc />
    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
        ArgumentNullException.ThrowIfNull(contentTypes);

        contentTypes.Clear();
        contentTypes.Add(Tipo);
    }
}
