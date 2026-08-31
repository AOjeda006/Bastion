namespace Bastion.BuildingBlocks.Domain.Bloqueos;

/// <summary>
/// Lo implementa la entidad a la que le puede pasar lo del artículo 32: que sus datos se
/// identifiquen y se reserven en vez de borrarse (R16).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una interfaz y no un miembro de <see cref="Entidades.EntidadBase"/></b> porque
/// bloquearse no le pasa a todas las entidades. Un ejercicio contable se cierra y una serie de
/// facturación se cierra; ninguno de los dos se bloquea, y heredar un bloqueo que nunca se usa
/// obligaría a explicar en cada consulta por qué ese filtro ahí no aplica. También porque C# tiene
/// herencia simple: la empresa ya es una raíz de agregado, y no podría ser además una
/// «entidad bloqueable».
/// </para>
/// <para>
/// <b>Es la marca que hace de lista.</b> Igual que <c>IDeInquilino</c> con el multiempresa, esta
/// interfaz es lo que un barrido del modelo puede preguntar: toda entidad que la implementa lleva
/// su filtro de repositorio, y ninguna que no la implementa lo lleva. Sin ella, «cuáles se
/// bloquean» sería una lista escrita en un test, que es una lista que se queda vieja.
/// </para>
/// <para>
/// <b>Las dos transiciones están aquí y no son opcionales.</b> Declararse bloqueable sin ofrecer
/// cómo bloquearse dejaría el estado a merced de quien pudiera escribirlo desde fuera. Cada
/// entidad las implementa en una línea que delega en <see cref="Bloqueo"/>, que es donde viven las
/// invariantes: la lógica está escrita una vez, y lo que se repite es solo el reenvío.
/// </para>
/// </remarks>
public interface IBloqueable
{
    /// <summary>Su estado de bloqueo. Nunca es nulo: sin bloquear vale <see cref="Bloqueos.Bloqueo.Ninguno"/>.</summary>
    Bloqueo Bloqueo { get; }

    /// <summary>Bloquea la entidad (R16). Suprimir no es borrar.</summary>
    /// <param name="motivo">Por qué se bloquea.</param>
    /// <param name="momento">Ahora.</param>
    void Bloquear(MotivoDeBloqueo motivo, DateTimeOffset momento);

    /// <summary>Levanta el bloqueo.</summary>
    void Desbloquear();
}
