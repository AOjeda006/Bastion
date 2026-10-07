using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>
/// Cómo empieza y cómo acaba toda escritura en las líneas de un recuento (ADR-0055 §4), y cómo
/// empiezan las acciones de su cabecera (ADR-0055 §3).
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
/// <para>
/// <b>Las acciones de la cabecera comparan su versión con la fila ya bloqueada</b>, y contestan el
/// <c>412</c> como un resultado (ADR-0057). Nadie puede cambiar la fila entre esa comparación y el
/// <c>COMMIT</c>, así que el testigo de EF Core no llega a chocar.
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
        Resultado<Recuento> leido = await BloquearYLeerEnCualquierEstadoAsync(recuentos, recuentoId, cancelacion)
            .ConfigureAwait(false);

        if (!leido.EsCorrecto)
        {
            return leido;
        }

        Recuento recuento = leido.Valor;

        return recuento.Estado == EstadoDeRecuento.EnCurso
            ? Resultado.Correcto(recuento)
            : Resultado.Fallo<Recuento>(ErroresDeRecuento.NoEstaEnCurso(recuentoId, recuento.Estado.ToString()));
    }

    /// <summary>
    /// Bloquea la cabecera, lee el recuento y compara su versión con la del <c>If-Match</c>, sin mirar
    /// su estado.
    /// </summary>
    /// <remarks>
    /// <b>La versión va antes que el estado</b> (ADR-0057): quien confirma dos veces con la misma
    /// versión recibe el <c>412</c>, que es su causa, y no el <c>409</c> de un recuento que ya no está
    /// en curso. El estado lo mira cada acción, porque cada una pide el suyo.
    /// </remarks>
    /// <param name="recuentos">El repositorio.</param>
    /// <param name="versiones">Las versiones del módulo.</param>
    /// <param name="recuentoId">El recuento de la ruta.</param>
    /// <param name="version">La versión de la cabecera que dice tener quien llama.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El recuento, con sus líneas, o el <c>404</c> o el <c>412</c>.</returns>
    internal static async Task<Resultado<Recuento>> BloquearYLeerEnSuVersionAsync(
        IRepositorioDeRecuentos recuentos,
        IVersionesDeInventario versiones,
        Guid recuentoId,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(versiones);

        Resultado<Recuento> leido = await BloquearYLeerEnCualquierEstadoAsync(recuentos, recuentoId, cancelacion)
            .ConfigureAwait(false);

        if (!leido.EsCorrecto)
        {
            return leido;
        }

        VersionDeRecurso actual = versiones.De(leido.Valor);

        return actual == version
            ? leido
            : Resultado.Fallo<Recuento>(ErroresDeConcurrencia.Obsoleta(actual));
    }

    /// <summary>Marca la cabecera para que su versión cambie al guardar.</summary>
    /// <param name="versiones">Las versiones del módulo.</param>
    /// <param name="recuento">El recuento, leído con su fila bloqueada.</param>
    internal static void Tocar(IVersionesDeInventario versiones, Recuento recuento) =>
        versiones.Exigir(recuento, versiones.De(recuento));

    private static async Task<Resultado<Recuento>> BloquearYLeerEnCualquierEstadoAsync(
        IRepositorioDeRecuentos recuentos,
        Guid recuentoId,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(recuentos);

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

        return Resultado.Correcto(recuento);
    }
}
