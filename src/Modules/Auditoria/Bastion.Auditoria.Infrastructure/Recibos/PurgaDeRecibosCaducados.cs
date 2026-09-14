using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Auditoria.Infrastructure.Recibos;

/// <summary>
/// Borra los recibos de idempotencia cuyo plazo ha vencido, de todas las empresas (ADR-0034 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Vive en Auditoría porque es la dueña de la tabla</b>: la crea y la migra. Los contextos de los
/// demás módulos escriben en ella dentro de su transacción, pero ninguno decide cuánto dura lo que
/// escribe.
/// </para>
/// <para>
/// <b>Vencido se borra, no se anonimiza.</b> Un recibo sin cuerpo no sirve para repetir la respuesta,
/// que es para lo único que existe, y lo que hay en el cuerpo puede ser la ficha de una persona.
/// </para>
/// <para>
/// <b>Es un borrado masivo, y está en la lista de excepciones con su motivo</b>
/// (<c>ElFiltroNoSeSaltaPorAhiTests</c>): salta el rastreador y la unidad de trabajo, y aquí no hay
/// nada que esos dos debieran ver. La tabla no se audita —es el recibo de una petición, no un cambio
/// de datos— y no lleva versión. Cargar las filas para borrarlas una a una solo serviría para traer a
/// memoria las respuestas guardadas, que es justo el dato que se quiere quitar de en medio.
/// </para>
/// <para>
/// <b>El instante lo recibe, no lo lee.</b> Quien la llama cada hora le pasa el del reloj; un test le
/// pasa el que necesita para comprobar el borde del plazo sin esperar un día.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Auditoría, que es el que mapea la tabla como propia.</param>
/// <param name="inquilino">Para abrir el ámbito sin empresa: la purga es de la instalación entera.</param>
public sealed class PurgaDeRecibosCaducados(AuditoriaDbContext contexto, IInquilinoActual inquilino)
{
    /// <summary>Borra los recibos que caducan en <paramref name="ahora"/> o antes.</summary>
    /// <param name="ahora">El instante contra el que se compara la caducidad de cada fila.</param>
    /// <param name="cancelacion">Cancelación.</param>
    /// <returns>Cuántos recibos se han borrado.</returns>
    public async Task<int> PurgarAsync(DateTimeOffset ahora, CancellationToken cancelacion)
    {
        using IDisposable sinInquilino = inquilino.SinInquilino(MotivoSinInquilino.CaducidadDeRecibos);

        return await contexto.Set<RegistroDeIdempotencia>()
            .Where(recibo => recibo.CaducaEn <= ahora)
            .ExecuteDeleteAsync(cancelacion)
            .ConfigureAwait(false);
    }
}
