using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Organizacion.Contracts.Series;

/// <summary>
/// Lo que otros módulos pueden preguntar sobre las series de numeración.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>organizacion.series</c> ni una llamada HTTP.
/// </para>
/// <para>
/// <b>Existe porque desde el ítem 2.4 hay documentos de otro módulo que guardan una
/// <c>SerieId</c></b>, y un <c>uuid</c> guardado sin nadie que lo compruebe es la cuarta vía del
/// ADR-0024: no hay clave ajena que cruce de esquema, así que lo único que puede impedir que ahí
/// acabe un identificador inventado es esta pregunta.
/// </para>
/// <para>
/// <b>No es lo que garantiza la R5, y decirlo importa.</b> Quien confirma un documento vuelve a
/// comprobar la serie entera —que existe, que es de esta empresa y que sigue activa— dentro del
/// <c>WHERE</c> de la sentencia que toma el número, en la misma transacción y con la fila
/// bloqueada. Esa es la garantía. Lo que este puerto da es que el borrador no se escriba apuntando
/// a nada, y que quien se equivoca de serie lo sepa al darlo de alta y no después de rellenarlo.
/// </para>
/// <para>
/// <b>Y no contesta de qué documentos es la serie ni de qué ejercicio cuelga.</b> Las dos cosas las
/// comprueba desde el ADR-0043 la sentencia que numera, que es donde está la garantía: una serie de
/// facturas no le da número a un ajuste (<c>serie-de-otro-documento</c>) y una serie del año pasado
/// no se lo da a un documento de este (<c>fecha-fuera-del-ejercicio-de-la-serie</c>). Quien abre un
/// borrador contra la serie equivocada se entera al confirmarlo, y no antes; preguntarlo también
/// aquí sería la cortesía, no la regla, y hoy no se hace.
/// </para>
/// </remarks>
public interface IConsultaDeSeries
{
    /// <summary>En qué estado está esa serie.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="EstadoDeMaestro.SoloResuelveLoViejo"/> es el estado de una serie <b>cerrada</b>,
    /// y encaja sin forzarlo: una serie cerrada sigue resolviendo los documentos que ya numeró
    /// —es lo que demuestra que su numeración fue correlativa— y no entrega ni un número más.
    /// </para>
    /// <para>
    /// <b>Una serie de otra empresa contesta <see cref="EstadoDeMaestro.NoExiste"/></b>, igual
    /// que una que no existe y por el mismo criterio con el que lo hace la sentencia que numera:
    /// distinguirlas convertiría esta pregunta en un detector de series ajenas para quien probara
    /// identificadores al azar. Aquí no hace falta escribir nada para que ocurra —el filtro de la
    /// R8 lo aplica el contexto—, y por eso está dicho: es una garantía que se hereda, y las que
    /// se heredan son las que alguien retira sin darse cuenta.
    /// </para>
    /// </remarks>
    /// <param name="serieId">Identificador de la serie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDeMaestro> EstadoDeAsync(Guid serieId, CancellationToken cancelacion);
}
