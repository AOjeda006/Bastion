namespace Bastion.BuildingBlocks.Domain.Retiradas;

/// <summary>
/// Lo implementa el maestro de instalación que se puede <b>retirar</b>: dejar de ofrecerse para
/// operaciones nuevas sin dejar de resolver lo que ya apunta a él (ADR-0023).
/// </summary>
/// <remarks>
/// <para>
/// <b>Retirada, bloqueo y cierre son tres cosas y el ADR-0023 las separa a propósito.</b> El
/// bloqueo (R16) es la respuesta al artículo 32 de la LOPDGDD y habla de <b>datos personales</b>;
/// una divisa no tiene ninguno, y responde <c>404</c> a su propio <c>GET</c>, que aquí sería
/// exactamente lo contrario de lo que hace falta. El cierre es el final de una <b>línea
/// temporal</b> —un ejercicio, una serie, un tramo de impuesto—, y una unidad de medida no se
/// sucede por otra: deja de usarse. Meter las tres en la misma columna es lo que el glosario del
/// 0.16 dice que no se hace.
/// </para>
/// <para>
/// <b>Es la marca que hace de lista</b>, igual que <see cref="Bloqueos.IBloqueable"/> con el
/// artículo 32 e <c>IDeInquilino</c> con el multiempresa. Sin ella, «cuáles se retiran» sería una
/// lista escrita en un test, y una lista escrita a mano es ciega a lo que llegue después: el
/// quinto maestro retirable entraría sin listado que lo excluya, sin <c>GET</c> que lo siga
/// devolviendo y sin nada rojo. Con la marca, el barrido lo encuentra el día que se declara.
/// </para>
/// <para>
/// <b>Un booleano, sin fecha y sin motivo</b>, al contrario que <see cref="Bloqueos.Bloqueo"/>.
/// Allí las tres piezas van juntas porque el art. 32 las exige: de la fecha cuelga el plazo de
/// prescripción y del motivo cuelga si vence. Aquí no hay plazo que contar ni motivo del que
/// colgarlo, y una fecha tendría además un coste que no se ve: invitaría a preguntar «¿estaba
/// retirada en tal día?», que es convertir la retirada en la sucesión temporal que el ADR dice que
/// <b>no</b> es. Cuándo se retiró y quién lo hizo ya están guardados —<c>ModificadoEn</c> y la
/// traza de auditoría—, así que la columna no sería la única fuente ni la mejor.
/// </para>
/// <para>
/// <b>Las dos transiciones están aquí y no son opcionales</b>, por lo mismo que en
/// <see cref="Bloqueos.IBloqueable"/>: declararse retirable sin ofrecer cómo retirarse dejaría el
/// estado a merced de quien pudiera escribirlo desde fuera. Y las dos, porque una retirada
/// irreversible sería un error nuevo con el mismo radio que el que la retirada viene a arreglar:
/// el de una instalación entera.
/// </para>
/// </remarks>
public interface IRetirable
{
    /// <summary>Si la fila está retirada: no se ofrece para lo nuevo, sigue resolviendo lo viejo.</summary>
    bool EstaRetirada { get; }

    /// <summary>
    /// Retira la fila. Retirar lo que ya está retirado no es un error: devuelve lo mismo.
    /// </summary>
    /// <remarks>
    /// Idempotente por el mismo motivo que <c>Bloquear</c>: es el contrato del verbo que lo
    /// provoca. Un <c>POST</c> repetido sobre el subrecurso tiene que dejar el mismo estado que
    /// uno solo, y no hay nada que mover —no hay fecha— al repetirlo.
    /// </remarks>
    void Retirar();

    /// <summary>Vuelve a ofrecerla para operaciones nuevas.</summary>
    /// <remarks>
    /// Reincorporar lo que no está retirado tampoco es un error: la postcondición ya se cumple, y
    /// lanzar obligaría a todo el que llame a preguntar antes.
    /// </remarks>
    void Reincorporar();
}
