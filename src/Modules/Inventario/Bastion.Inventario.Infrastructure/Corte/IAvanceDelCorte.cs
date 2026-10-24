using Bastion.Inventario.Infrastructure.Persistencia.Existencias;

namespace Bastion.Inventario.Infrastructure.Corte;

/// <summary>
/// Lo que el trabajo mensual le pide a la base: qué empresas tienen existencias, el avance del
/// corte de una de ellas, un mes cada vez, y su cuadre (ADR-0063 §7, ADR-0067).
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
    /// Avanza el corte de una empresa un mes hacia el que se le diga, si no llega ya, en su propia
    /// transacción (ADR-0067 §1).
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="hastaElMes">El primer día del mes al que tiene que llegar el corte.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>El mes que ha hecho, o <c>null</c> si el corte ya llegaba y no ha tocado nada.</returns>
    Task<MesAvanzado?> AvanzarUnMesAsync(Guid empresaId, DateOnly hastaElMes, CancellationToken cancelacion);

    /// <summary>
    /// Cuadra lo que le quede por cuadrar a una empresa, desde donde se quedó hasta su corte, y lo
    /// anota (ADR-0067 §5).
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Lo que ha cuadrado, o <c>null</c> si no tenía nada pendiente.</returns>
    Task<CuadreHecho?> CuadrarLoPendienteAsync(Guid empresaId, CancellationToken cancelacion);

    /// <summary>
    /// El corte de cada empresa con existencias, para saber cuánto se ha quedado atrás el más viejo
    /// (ADR-0064 §3), y cuánto le falta por cuadrar al cuadre más atrasado (ítem 2.20).
    /// </summary>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Uno por empresa, en orden de identificador.</returns>
    Task<IReadOnlyList<CorteDeUnaEmpresa>> CortesAsync(CancellationToken cancelacion);
}

/// <summary>Lo que deja un paso del avance de una empresa.</summary>
/// <param name="Mes">El primer día del mes en que ha puesto el corte.</param>
/// <param name="Instantaneas">Cuántas instantáneas ha escrito.</param>
/// <param name="Duracion">
/// Lo que ha tardado, de abrir su transacción al <c>COMMIT</c>, que es también lo que ha tenido el
/// exclusivo de la empresa. Es la cifra que se mide contra el disparador de 10 s (ADR-0064 §2).
/// </param>
internal sealed record MesAvanzado(DateOnly Mes, int Instantaneas, TimeSpan Duracion);

/// <summary>Lo que deja el cuadre de lo pendiente de una empresa.</summary>
/// <param name="Desde">El primer día del primer mes que ha comparado.</param>
/// <param name="Hasta">El primer día del último.</param>
/// <param name="Duracion">Lo que ha tardado, todos los meses juntos.</param>
/// <param name="Cuadre">Las cifras del cuadre y lo que no cuadra.</param>
internal sealed record CuadreHecho(DateOnly Desde, DateOnly Hasta, TimeSpan Duracion, CuadreDeLasExistencias Cuadre);

/// <summary>Dónde está el corte de una empresa con existencias.</summary>
/// <param name="EmpresaId">La empresa.</param>
/// <param name="HastaElMes">
/// El primer día del último mes con instantánea, o <c>null</c> si su corte no ha avanzado nunca.
/// </param>
/// <param name="PrimerMovimiento">
/// La fecha de su primer movimiento, <b>solo si no tiene corte</b>: es desde donde le faltan meses.
/// Con corte no se lee, porque buscarla es recorrer el libro de la empresa cada hora.
/// </param>
/// <param name="SinCuadrarDesde">
/// El primer día del primer mes que le falta por cuadrar, que es el mismo por el que empezaría su
/// cuadre; o <c>null</c> si lo tiene cuadrado hasta el corte, o si no tiene corte.
/// </param>
internal sealed record CorteDeUnaEmpresa(
    Guid EmpresaId, DateOnly? HastaElMes, DateOnly? PrimerMovimiento, DateOnly? SinCuadrarDesde = null);
