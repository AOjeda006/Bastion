using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>
/// Cómo empieza y cómo acaba toda escritura en las líneas de un recuento (ADR-0055 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Empieza bloqueando la cabecera, antes de leerla</b>. Leer primero y bloquear después dejaría
/// mirar el estado de una fila que otra transacción ya podía haber cambiado: una confirmación que
/// llega entre medias cerraría el recuento, y la línea se contaría en uno confirmado.
/// </para>
/// <para>
/// <b>Y acaba tocando la cabecera</b>, para que su versión cambie: quien confirma mira la ficha y la
/// huella, y si alguien ha contado después, su <c>If-Match</c> ya no vale. Se toca con la versión
/// que se acaba de leer con la fila bloqueada, así que esto nunca da un <c>412</c> por sí solo.
/// </para>
/// </remarks>
internal static class ElRecuentoEnCurso
{
    /// <summary>Bloquea la cabecera, lee el recuento y mira que siga en curso.</summary>
    /// <param name="recuentos">El repositorio.</param>
    /// <param name="recuentoId">El recuento de la ruta.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El recuento, con sus líneas, o el <c>404</c> o el <c>409</c>.</returns>
    internal static async Task<Resultado<Recuento>> BloquearYLeerAsync(
        IRepositorioDeRecuentos recuentos,
        Guid recuentoId,
        CancellationToken cancelacion)
    {
        if (!await recuentos.BloquearAsync(recuentoId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<Recuento>(ErroresDeRecuento.NoEncontrado(recuentoId));
        }

        // CON LA FILA BLOQUEADA NO PUEDE FALTAR: un recuento no se borra, y el cerrojo ya ha mirado la
        // empresa. Si falta, el filtro y el cerrojo no dicen lo mismo, y eso es un defecto.
        Recuento recuento = await recuentos.ObtenerAsync(recuentoId, cancelacion).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"El recuento {recuentoId} está bloqueado y no se puede leer: el cerrojo y el filtro " +
                "de la empresa no están diciendo lo mismo.");

        return recuento.Estado == EstadoDeRecuento.EnCurso
            ? Resultado.Correcto(recuento)
            : Resultado.Fallo<Recuento>(ErroresDeRecuento.NoEstaEnCurso(recuentoId, recuento.Estado.ToString()));
    }

    /// <summary>Marca la cabecera para que su versión cambie al guardar.</summary>
    /// <param name="versiones">Las versiones del módulo.</param>
    /// <param name="recuento">El recuento, leído con su fila bloqueada.</param>
    internal static void Tocar(IVersionesDeInventario versiones, Recuento recuento) =>
        versiones.Exigir(recuento, versiones.De(recuento));
}
