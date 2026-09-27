using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Inventario.Domain.Existencias;

/// <summary>
/// Hasta qué mes tiene instantáneas una empresa (ADR-0044).
/// </summary>
/// <remarks>
/// <para>
/// <b>Hace falta escrito y no se puede deducir de las instantáneas.</b> El último mes que haya en
/// la tabla lo diría mientras la empresa tuviera alguna fila. Pero puede no tenerla, si todo lo
/// que se ha movido es posterior al corte. Y entonces un movimiento atrasado no sabría hasta dónde
/// crear las suyas.
/// </para>
/// <para>
/// <b>Sin fila, la empresa no tiene instantáneas</b>, y anotar el libro no crea ninguna. El corte lo
/// pone el recálculo. En el 2.7 solo lo llaman los casos; quien lo avance cada mes llega con el
/// primer lector de la instantánea, el ítem 2.14.
/// </para>
/// </remarks>
public sealed class CorteDeLaInstantanea : IDeInquilino
{
    /// <summary>Constructor de materialización: EF Core rellena la fila.</summary>
    private CorteDeLaInstantanea()
    {
    }

    /// <inheritdoc/>
    /// <remarks>Es también la clave: una empresa tiene un corte y solo uno.</remarks>
    public Guid EmpresaId { get; private set; }

    /// <summary>El primer día del último mes con instantánea, incluido.</summary>
    public DateOnly HastaElMes { get; private set; }
}
