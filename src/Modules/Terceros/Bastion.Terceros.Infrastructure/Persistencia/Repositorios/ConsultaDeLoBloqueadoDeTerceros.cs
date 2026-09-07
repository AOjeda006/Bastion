using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;

namespace Bastion.Terceros.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Lo que Terceros tiene bloqueado: fichas de clientes y proveedores.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cierra la nota que el ítem 1.5 dejó abierta.</b> Allí se anotó que un tercero bloqueado
/// desaparecía del camino ordinario —como debe— y no asomaba por el reservado, y se dejó para
/// después porque ampliar el enumerado tocaba un listado de Organización. Con el puerto ya no lo
/// toca: el módulo contesta por lo suyo.
/// </para>
/// <para>
/// <b>Sin código, como la empresa, y a propósito.</b> Un tercero no tiene campo de código: se
/// reconoce por su razón social. Lo que <b>no</b> viaja aquí es el identificador fiscal, y esa
/// ausencia es la decisión: el ítem 1.5 se pasó entero evitando que un NIF apareciera donde no hace
/// falta, y para levantar un bloqueo hecho por error basta con la razón social y el identificador
/// de la fila. Enseñar el NIF sería tratar un dato personal por comodidad.
/// </para>
/// <para>
/// <b>El filtro de empresa sigue puesto</b>: <c>Tercero</c> es <c>IDeInquilino</c>, así que el
/// ámbito de bloqueo apaga el filtro del bloqueo y ningún otro, y desde dentro de una empresa se ve
/// lo bloqueado <i>de esa empresa</i> (R8).
/// </para>
/// </remarks>
/// <param name="contexto">El contexto del módulo.</param>
internal sealed class ConsultaDeLoBloqueadoDeTerceros(TercerosDbContext contexto)
    : IConsultaDeLoBloqueado
{
    /// <inheritdoc/>
    public Task<LoBloqueadoDeUnModulo> PrimerosAsync(
        CriterioDeLoBloqueado criterio, CancellationToken cancelacion) =>
        ConsultasDeLoBloqueado.ResponderAsync(LoBloqueado(contexto), criterio, cancelacion);

    /// <summary>Los terceros bloqueados de la empresa del contexto, proyectados.</summary>
    /// <remarks>
    /// Estática y visible al ensamblado de pruebas por el mismo motivo que sus hermanas: el barrido
    /// que comprueba que todo orden declarado se traduce a SQL necesita una puerta por donde pedir
    /// la consulta, y una proyección no tiene <c>Set</c>.
    /// </remarks>
    /// <param name="contexto">El contexto del módulo.</param>
    internal static IQueryable<RecursoBloqueado> LoBloqueado(TercerosDbContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return contexto.Terceros
            .Where(tercero => tercero.Bloqueo.EstaBloqueado)
            .Select(tercero => new RecursoBloqueado
            {
                Id = tercero.Id,
                Tipo = TipoDeRecursoBloqueado.Tercero,
                Codigo = (string?)null,
                Nombre = tercero.RazonSocial,
                BloqueadoEn = tercero.Bloqueo.Desde!.Value,
                Motivo = tercero.Bloqueo.Motivo!.Value,
            });
    }
}
