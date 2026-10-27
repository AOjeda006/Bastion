using Bastion.BuildingBlocks.Contracts.Maestros;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Unidades;

/// <summary>
/// Lo que otros módulos pueden preguntar sobre las unidades de medida.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>organizacion.unidades_de_medida</c> ni una llamada
/// HTTP.
/// </para>
/// <para>
/// <b>Es el puerto del caso que abrió el ADR-0024.</b> El §7.3 le da al artículo una <i>unidad
/// base</i>, y esa propiedad se llamará <c>UnidadBaseId</c>: un nombre que no casa con
/// <c>UnidadMedida</c> y que ninguna heurística por nombre va a delatar. Lo que impide que ahí acabe
/// un identificador inventado no es el motor —no hay clave ajena entre esquemas— sino esta
/// pregunta.
/// </para>
/// </remarks>
public interface IConsultaDeUnidadesDeMedida
{
    /// <summary>En qué estado está esa unidad de medida.</summary>
    /// <remarks>
    /// <see cref="EstadoDeMaestro.SoloResuelveLoViejo"/> es el estado de una unidad <b>retirada</b>
    /// (ADR-0023). Hoy ninguna fila puede estar ahí porque la retirada llega en el ítem 1.7, y se
    /// dice en voz alta para que nadie lea ese hueco como que el estado sobra: la alternativa
    /// —añadirlo cuando haga falta— es cambiar este contrato con Catálogo ya construido encima.
    /// </remarks>
    /// <param name="unidadId">Identificador de la unidad de medida.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDeMaestro> EstadoDeAsync(Guid unidadId, CancellationToken cancelacion);

    /// <summary>El código y el nombre de cada unidad de medida, en una sola consulta.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nombra lo que enseña su ficha</b>, la de
    /// <c>GET /api/v1/organizacion/unidades-de-medida/{id}</c> (ADR-0074). Una unidad
    /// <b>retirada</b> vuelve, porque sigue resolviendo lo viejo y su ficha la enseña. Es un
    /// maestro de instalación (R8): no hay empresa que filtrar.
    /// </para>
    /// <para>
    /// <b>No mira el permiso de quien pregunta</b>: lo mira quien pregunta, antes de preguntar, como
    /// la pantalla. Lo que no vuelve, se escribe con su identificador.
    /// </para>
    /// </remarks>
    /// <param name="unidades">Las unidades por las que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El código y el nombre de cada una que vuelve.</returns>
    Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombresDeAsync(
        IReadOnlyCollection<Guid> unidades, CancellationToken cancelacion);
}
