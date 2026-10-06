using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Recuentos;

namespace Bastion.Inventario.Application.Recuentos;

/// <summary>
/// Lo que los casos de uso del recuento necesitan de la persistencia: el documento, las existencias
/// del almacén que cuenta, y el ajuste que lo apunta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las existencias se leen aquí y no por un puerto</b>: son de este módulo, y el recuento es
/// justo la pregunta «qué dice el libro de cada clave de este almacén».
/// </para>
/// <para>
/// <b>El listado ordena por lo que dice <see cref="IOrdenaPor.CamposOrdenables"/></b>, como en los
/// demás módulos, y lo valida el borde antes de llegar aquí.
/// </para>
/// </remarks>
public interface IRepositorioDeRecuentos : IOrdenaPor
{
    /// <summary>El recuento con sus líneas, o <see langword="null"/> si no es de la empresa.</summary>
    /// <param name="id">Identificador del recuento.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El recuento, o <see langword="null"/>.</returns>
    Task<Recuento?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Añade un recuento nuevo, que se escribe al confirmar la unidad de trabajo.</summary>
    /// <param name="recuento">El recuento en curso, con su precarga.</param>
    void Agregar(Recuento recuento);

    /// <summary>Si el almacén tiene ya un recuento en curso.</summary>
    /// <remarks>
    /// <b>Es la cortesía, no la garantía</b>: dos altas a la vez pasan juntas por aquí, y a la segunda
    /// la para el índice único parcial (ADR-0055 §1.7).
    /// </remarks>
    /// <param name="almacenId">El almacén.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Si lo tiene.</returns>
    Task<bool> HayUnoEnCursoAsync(Guid almacenId, CancellationToken cancelacion);

    /// <summary>Las existencias del almacén por clave, con su físico y su tránsito.</summary>
    /// <remarks>
    /// <b>Solo las claves con algo</b>, físico o tránsito: una fila a cero dice lo mismo que una que
    /// no existe, y el teórico de una clave sin fila es cero.
    /// </remarks>
    /// <param name="almacenId">El almacén.</param>
    /// <param name="articulos">
    /// Los artículos a los que se acota, o <see langword="null"/> para el almacén entero.
    /// </param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Una entrada por clave.</returns>
    Task<IReadOnlyList<ExistenciaDeUnaClave>> ExistenciasAsync(
        Guid almacenId,
        IReadOnlyCollection<Guid>? articulos,
        CancellationToken cancelacion);

    /// <summary>El ajuste que movió la diferencia del recuento, o <see langword="null"/> si no movió nada.</summary>
    /// <remarks>
    /// <b>Se encuentra por la flecha contraria</b> (ADR-0055 §8): el ajuste apunta a su recuento, y el
    /// recuento no apunta a su ajuste.
    /// </remarks>
    /// <param name="recuentoId">El recuento.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El ajuste con sus líneas, o <see langword="null"/>.</returns>
    Task<Ajuste?> AjusteDeAsync(Guid recuentoId, CancellationToken cancelacion);

    /// <summary>Una página de recuentos, sin sus líneas.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y qué filtro.</param>
    /// <param name="estado">El estado al que se acota, o <see langword="null"/>.</param>
    /// <param name="almacenId">El almacén al que se acota, o <see langword="null"/>.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>La página, con los recuentos sin rastrear.</returns>
    Task<PaginaDe<Recuento>> ListarAsync(
        Paginacion paginacion,
        EstadoDeRecuento? estado,
        Guid? almacenId,
        CancellationToken cancelacion);
}

/// <summary>Lo que el libro dice de una clave del almacén.</summary>
/// <param name="Clave">La ubicación, el artículo, y el lote o el número de serie.</param>
/// <param name="Fisico">Lo que hay, en unidad base.</param>
/// <param name="EnTransito">Lo que vuela hacia ella, en unidad base (ADR-0053 §1).</param>
public sealed record ExistenciaDeUnaClave(ClaveDelRecuento Clave, decimal Fisico, decimal EnTransito);
