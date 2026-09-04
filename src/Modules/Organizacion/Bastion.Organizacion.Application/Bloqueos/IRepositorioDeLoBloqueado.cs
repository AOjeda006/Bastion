using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;

namespace Bastion.Organizacion.Application.Bloqueos;

/// <summary>
/// La única consulta del módulo que trae filas bloqueadas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un puerto propio y no un método más en los tres repositorios.</b> Repartido, cada uno de los
/// tres tendría un método que solo sirve dentro de un ámbito de bloqueo abierto, y nada impediría
/// llamarlo fuera: la lista de aperturas declaradas seguiría en cinco y la consulta devolvería
/// —correctamente— nada, que es el peor desenlace posible porque parece que funciona. Junto, hay un
/// solo sitio del que se puede decir «esto ve lo bloqueado» y un solo sitio que abre el ámbito.
/// </para>
/// <para>
/// <b>No tiene <c>ObtenerAsync</c>, y esa ausencia es la decisión del ADR-0027.</b> Del listado
/// sale el identificador, y el desbloqueo no pide etiqueta; una ficha individual de lo bloqueado
/// devolvería el recurso con su <c>ETag</c> y haría caducar las cuatro exenciones de
/// <c>If-Match</c> de golpe.
/// </para>
/// </remarks>
public interface IRepositorioDeLoBloqueado : IOrdenaPor
{
    /// <summary>
    /// Trae una página de lo bloqueado: empresas, almacenes y ubicaciones en la misma lista.
    /// </summary>
    /// <remarks>
    /// <b>Devuelve lo bloqueado solo si quien llama ha abierto el ámbito</b> con su motivo. No lo
    /// abre esta consulta: el filtro de repositorio es lo que cumple el art. 32, y un repositorio
    /// que se destapara a sí mismo convertiría una apertura declarada en una puerta.
    /// </remarks>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y con qué filtro.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<RecursoBloqueado>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);
}
