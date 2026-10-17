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

    /// <summary>
    /// El corte de cada empresa con existencias, para saber cuánto se ha quedado atrás el más viejo
    /// (ADR-0064 §3).
    /// </summary>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Uno por empresa, en orden de identificador.</returns>
    Task<IReadOnlyList<CorteDeUnaEmpresa>> CortesAsync(CancellationToken cancelacion);
}

/// <summary>Lo que deja el avance de una empresa.</summary>
/// <param name="Repuestas">Cuántas instantáneas ha rehecho desde el libro.</param>
/// <param name="DuracionDelAvance">
/// Lo que ha tardado en rehacerlas. Es la cifra del disparador: si pasa de 10 s, el avance se
/// cambia por uno que solo lea el libro del mes (ADR-0064 §2).
/// </param>
/// <param name="DuracionDelCuadre">Lo que ha tardado el cuadre de después.</param>
/// <param name="Cuadre">Las cifras del cuadre y lo que no cuadra.</param>
internal sealed record AvanceHecho(
    int Repuestas,
    TimeSpan DuracionDelAvance,
    TimeSpan DuracionDelCuadre,
    CuadreDeLasExistencias Cuadre);

/// <summary>Dónde está el corte de una empresa con existencias.</summary>
/// <param name="EmpresaId">La empresa.</param>
/// <param name="HastaElMes">
/// El primer día del último mes con instantánea, o <c>null</c> si su corte no ha avanzado nunca.
/// </param>
/// <param name="PrimerMovimiento">
/// La fecha de su primer movimiento, <b>solo si no tiene corte</b>: es desde donde le faltan meses.
/// Con corte no se lee, porque buscarla es recorrer el libro de la empresa cada hora.
/// </param>
internal sealed record CorteDeUnaEmpresa(Guid EmpresaId, DateOnly? HastaElMes, DateOnly? PrimerMovimiento);
