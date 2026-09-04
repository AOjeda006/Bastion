using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.Organizacion.Contracts.Bloqueos;

namespace Bastion.Organizacion.Application.Bloqueos;

/// <summary>
/// El acceso reservado del artículo 32: la única lectura de la API que entrega filas bloqueadas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque tapar sin poder rectificar también incumple.</b> Una fila bloqueada es
/// invisible para la interfaz, así que no hay desde dónde ofrecer el desbloqueo y un bloqueo hecho
/// por error se convierte en un dato inalcanzable. El art. 32 obliga a reservar, no a perder
/// (ADR-0027).
/// </para>
/// <para>
/// <b>Es un listado y no una ficha, y la diferencia no es de comodidad.</b> Una ficha individual
/// devolvería el recurso con su testigo de versión, y entonces la llave que <c>If-Match</c> pide
/// volvería a existir: las cuatro exenciones de los desbloqueos caducarían a la vez. De un listado
/// sale el identificador, que es lo único que el desbloqueo necesita.
/// </para>
/// </remarks>
public interface IListarLoBloqueado : IListado<BloqueadoDto>
{
}

/// <inheritdoc cref="IListarLoBloqueado"/>
internal sealed class ListarLoBloqueado(
    IRepositorioDeLoBloqueado bloqueados,
    IAccesoALoBloqueado acceso,
    PoliticaDeRetencion retencion) : IListarLoBloqueado
{
    public IReadOnlySet<string> CamposOrdenables => bloqueados.CamposOrdenables;

    public async Task<PaginaDe<BloqueadoDto>> EjecutarAsync(
        Paginacion paginacion, CancellationToken cancelacion)
    {
        // El QUINTO sitio del proyecto que abre el ámbito, y el primero que no es una
        // administración de bloqueo: es el acceso reservado del art. 32, con su motivo propio y su
        // permiso propio. La apertura anota en el registro el motivo Y QUIÉN pregunta, que es lo
        // que convierte esto en una vía trazada y no en una consulta más.
        using IDisposable _ = acceso.ViendoLoBloqueado(MotivoParaVerLoBloqueado.AccesoReservadoDelArticulo32);

        PaginaDe<RecursoBloqueado> pagina =
            await bloqueados.ListarAsync(paginacion, cancelacion).ConfigureAwait(false);

        return new PaginaDe<BloqueadoDto>(
            [.. pagina.Elementos.Select(ADto)],
            pagina.Pagina, pagina.Tamanio, pagina.Total);
    }

    private BloqueadoDto ADto(RecursoBloqueado recurso) =>
        new(recurso.Id,
            recurso.Tipo.ToString(),
            recurso.Codigo,
            recurso.Nombre,
            recurso.BloqueadoEn,
            recurso.Motivo.ToString(),
            // El vencimiento se calcula aquí y no se guarda en una columna, y es a propósito: el
            // plazo es una política de la instalación y puede cambiar por asesoramiento legal. En
            // columna, cambiarlo obligaría a recalcular filas ya escritas y las que nadie tocara se
            // quedarían con el plazo viejo sin que se notara.
            //
            // Se rearma el `Bloqueo` con los dos datos de la fila —que es exactamente el estado que
            // hay guardado— para no tener dos formas de preguntar cuándo vence un bloqueo.
            retencion.VenceEn(Bloqueo.Ninguno().Bloquear(recurso.Motivo, recurso.BloqueadoEn)));
}
