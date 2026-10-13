using Bastion.Inventario.Infrastructure.Persistencia.Existencias;

namespace Bastion.Inventario.Infrastructure.Corte;

/// <summary>
/// Lo que el trabajo mensual le pide a la base: qué empresas tienen existencias, y el avance del
/// corte de una de ellas, con su cuadre (ADR-0063 §7).
/// </summary>
/// <remarks>
/// <b>Es una interfaz para que el bucle se pruebe sin base.</b> Lo que el trabajo decide —el mes que
/// toca, que una empresa que falla no pare a las demás, qué cuenta y qué anota— no necesita
/// PostgreSQL, y el avance de verdad se prueba aparte, llamándolo con su empresa.
/// </remarks>
internal interface IAvanceDelCorte
{
    /// <summary>Las empresas que tienen alguna existencia, en orden de identificador.</summary>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Sus identificadores, sin repetir.</returns>
    Task<IReadOnlyList<Guid>> EmpresasConExistenciasAsync(CancellationToken cancelacion);

    /// <summary>
    /// Avanza el corte de una empresa hasta un mes, si no llega ya, y la cuadra después.
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="hastaElMes">El primer día del mes al que tiene que llegar el corte.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Lo que ha hecho, o <c>null</c> si el corte ya llegaba y no ha tocado nada.</returns>
    Task<AvanceHecho?> AvanzarAsync(Guid empresaId, DateOnly hastaElMes, CancellationToken cancelacion);
}

/// <summary>Lo que deja el avance de una empresa.</summary>
/// <param name="Repuestas">Cuántas instantáneas ha rehecho desde el libro.</param>
/// <param name="DuracionDelAvance">
/// Lo que ha tardado en rehacerlas. Es la cifra del disparador: si pasa de un minuto, el avance se
/// cambia por uno que solo lea el libro del mes (ADR-0063 §7).
/// </param>
/// <param name="DuracionDelCuadre">Lo que ha tardado el cuadre de después.</param>
/// <param name="Cuadre">Las cifras del cuadre y lo que no cuadra.</param>
internal sealed record AvanceHecho(
    int Repuestas,
    TimeSpan DuracionDelAvance,
    TimeSpan DuracionDelCuadre,
    CuadreDeLasExistencias Cuadre);
