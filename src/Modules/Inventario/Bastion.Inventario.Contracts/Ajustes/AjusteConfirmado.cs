using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Inventario.Contracts.Ajustes;

/// <summary>Un ajuste de existencias se confirmó y sus líneas ya son filas del libro.</summary>
/// <remarks>
/// <para>
/// <b>Va por DOCUMENTO, no por movimiento</b>, y la diferencia se nota en volumen. Un evento por
/// fila del libro convertiría la bandeja en una segunda copia de la tabla que más crece del
/// sistema, con su coste de reproceso, para contar algo que ya está contado. Lo que le interesa a
/// quien escucha es que un ajuste se confirmó y cuántas líneas movió; el detalle está en el libro,
/// que es la verdad (R3).
/// </para>
/// <para>
/// <b>Y no lleva las líneas dentro.</b> Un evento de integración es un hecho consumado y un
/// contrato público: cuantos más campos lleve, más cosas hay que no se pueden cambiar sin romper a
/// alguien. Quien necesite el detalle lo pide por el identificador.
/// </para>
/// </remarks>
/// <param name="AjusteId">El documento que se confirmó.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="AlmacenId">Almacén contra el que se ajustó.</param>
/// <param name="FechaDeOperacion">Día al que se imputa.</param>
/// <param name="Lineas">Cuántas filas del libro escribió.</param>
public sealed record AjusteConfirmado(
    Guid AjusteId,
    Guid EmpresaId,
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    int Lineas) : EventoDeIntegracion
{
    /// <summary>Nombre con el que viaja por la bandeja de salida.</summary>
    public const string Nombre = "inventario.ajuste-confirmado";
}
