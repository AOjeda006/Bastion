using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.BuildingBlocks.Application.Eventos;

/// <summary>
/// Entrega un evento a quien lo escuche, <b>una sola vez por consumidor</b>.
/// </summary>
/// <remarks>
/// <para>
/// Es un despachador propio de unas pocas decenas de líneas y no un bus en memoria (§4): en un
/// monolito modular, un bus no aporta nada sobre una interfaz y sí quita algo —quién atiende deja
/// de decirlo el compilador—. Además, las bibliotecas clásicas de este hueco pasaron a licencia
/// comercial en 2025 (nota de licencias del plan maestro).
/// </para>
/// <para>
/// <b>Aquí es donde vive «reprocesar no duplica».</b> El publicador entrega al menos una vez, a
/// propósito (ver <c>PublicadorDeLaBandeja</c>); lo que convierte «al menos una» en «exactamente
/// una» es esta capa, que apunta el par (evento, consumidor) y no vuelve a llamar al manejador que
/// ya lo procesó.
/// </para>
/// </remarks>
public interface IDespachadorDeEventos
{
    /// <summary>Entrega el evento a sus manejadores, saltándose los que ya lo procesaron.</summary>
    /// <param name="evento">El hecho ocurrido.</param>
    /// <param name="cancelacion">Cancelación de la parada del trabajo de fondo.</param>
    /// <returns>Cuántos manejadores han atendido el evento en esta pasada.</returns>
    Task<int> DespacharAsync(EventoDeIntegracion evento, CancellationToken cancelacion);
}
