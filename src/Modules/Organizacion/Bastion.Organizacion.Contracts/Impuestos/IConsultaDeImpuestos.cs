using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Impuestos;

/// <summary>
/// Lo que otros módulos pueden preguntar sobre los tramos de impuesto.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>organizacion.impuestos</c> ni una llamada HTTP.
/// </para>
/// <para>
/// <b>Existe para que un identificador guardado en otro módulo no sea un <c>uuid</c> y nada más.</b>
/// Catálogo le da al artículo un impuesto por defecto (§7.3): sin este puerto, esa columna aceptaría
/// cualquier valor, compilaría, migraría y pasaría los tests — y el fallo aparecería en la factura
/// que lo usa, tres fases después (ADR-0024).
/// </para>
/// </remarks>
public interface IConsultaDeImpuestos
{
    /// <summary>En qué estado está ese tramo para una operación con esa fecha de devengo.</summary>
    /// <remarks>
    /// <para>
    /// <b>La fecha es un parámetro y no «hoy»</b>, y no por comodidad: lo que se da de alta es un
    /// <b>tramo</b>, no un impuesto, y un tramo rige entre dos fechas. El IVA general pasó del 18 %
    /// al 21 % el 1 de septiembre de 2012, así que la pregunta «¿se puede usar este tramo?» no
    /// tiene respuesta sin decir para qué día. Quien pregunta sabe la fecha de devengo de su
    /// operación; este puerto no tiene por qué suponerla.
    /// </para>
    /// <para>
    /// Un tramo que existe y no rige ese día contesta <see cref="EstadoDeMaestro.SoloResuelveLoViejo"/>:
    /// sigue resolviendo la cuota de una factura antigua, y no se ofrece para una nueva.
    /// </para>
    /// </remarks>
    /// <param name="impuestoId">Identificador del tramo.</param>
    /// <param name="enLaFechaDeDevengo">Día de devengo de la operación que quiere usarlo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDeMaestro> EstadoDeAsync(
        Guid impuestoId,
        DateOnly enLaFechaDeDevengo,
        CancellationToken cancelacion);
}
