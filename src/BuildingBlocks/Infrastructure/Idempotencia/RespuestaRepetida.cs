using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.BuildingBlocks.Infrastructure.Idempotencia;

/// <summary>
/// Vuelve a emitir la respuesta que se guardó la primera vez, tal cual.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los mismos bytes, no una respuesta equivalente.</b> Se guarda el cuerpo ya serializado y se
/// escribe sin volver a pasarlo por ningún formateador: un cliente que compare las dos respuestas
/// —o que verifique una firma sobre el cuerpo— tiene que ver exactamente lo mismo. Volver a
/// serializar el objeto haría que un cambio de opciones del serializador cambiara la respuesta de
/// una petición ya atendida.
/// </para>
/// <para>
/// <b>Y solo las dos cabeceras guardadas.</b> Lo que la tubería ponga por su cuenta —traza,
/// negociación— lo pone igual que en cualquier otra respuesta; de la primera vez solo vuelven
/// <c>ETag</c> y <c>Location</c>, que son las que hablan del recurso.
/// </para>
/// </remarks>
/// <param name="respuesta">Lo que se guardó.</param>
public sealed class RespuestaRepetida(RespuestaGuardada respuesta) : IActionResult
{
    /// <summary>
    /// Cabecera con la que el cliente puede distinguir la repetición del primer intento.
    /// </summary>
    /// <remarks>
    /// No la manda ninguna norma —el borrador del IETF sobre <c>Idempotency-Key</c> no define
    /// ninguna—, así que se usa la grafía más extendida en la práctica. <b>Es informativa</b>: el
    /// cliente correcto no necesita mirarla, porque la respuesta es la misma con ella y sin ella.
    /// Sirve para depurar y para las métricas de «cuántos reintentos estamos absorbiendo».
    /// </remarks>
    public const string CabeceraDeRepeticion = "Idempotent-Replayed";

    /// <inheritdoc />
    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(respuesta);

        HttpResponse salida = context.HttpContext.Response;

        salida.StatusCode = respuesta.CodigoDeEstado;
        salida.Headers[CabeceraDeRepeticion] = "true";

        if (respuesta.Etiqueta is not null)
        {
            salida.Headers.ETag = respuesta.Etiqueta;
        }

        if (respuesta.Ubicacion is not null)
        {
            salida.Headers.Location = respuesta.Ubicacion;
        }

        if (respuesta.Cuerpo is null)
        {
            return;
        }

        salida.ContentType = respuesta.TipoDeContenido;

        byte[] bytes = Encoding.UTF8.GetBytes(respuesta.Cuerpo);
        salida.ContentLength = bytes.Length;

        await salida.Body.WriteAsync(bytes).ConfigureAwait(false);
    }
}
