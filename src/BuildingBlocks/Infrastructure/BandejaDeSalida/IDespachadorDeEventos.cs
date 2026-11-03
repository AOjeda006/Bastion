namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

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
/// <para>
/// <b>Recibe la fila de la cola, y no el evento</b> (3.4, hueco 2). El manejador corre con la
/// empresa de la fila, que es la empresa desde la que se actuó (ADR-0013 §8): con un evento y una
/// empresa sueltos, quien llamara elegiría la empresa. Por eso vive aquí y no en Application, que
/// es donde están los casos de uso: ninguno puede llamarlo, así que ninguno puede cambiar de
/// empresa por este camino.
/// </para>
/// </remarks>
public interface IDespachadorDeEventos
{
    /// <summary>Entrega el evento de la fila a sus manejadores, saltándose los que ya lo procesaron.</summary>
    /// <param name="fila">La fila de la cola, con su evento, su nombre y su empresa.</param>
    /// <param name="cancelacion">Cancelación de la parada del trabajo de fondo.</param>
    /// <returns>Cuántos manejadores han atendido el evento en esta pasada.</returns>
    /// <exception cref="InvalidOperationException">
    /// Si el evento tiene manejadores y la fila no lleva empresa: se escribió sin inquilino, y un
    /// manejador no corre sin empresa.
    /// </exception>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException">
    /// Si un manejador sigue chocando con otra escritura después de los reintentos en el acto.
    /// </exception>
    Task<int> DespacharAsync(EventoDeLaBandeja fila, CancellationToken cancelacion);
}
