using Bastion.BuildingBlocks.Application.Eventos;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>Un manejador apuntado: a qué evento atiende y en qué contexto escribe.</summary>
/// <remarks>
/// <para>
/// <b>El despachador no resuelve los manejadores en su ámbito, sino cada uno en el suyo</b>, y para
/// eso tiene que saber qué pedirle al contenedor antes de tener ningún manejador delante: el tipo
/// del manejador, que se resuelve tal cual —es un tipo cerrado, no uno construido—, y el de su
/// contexto, en el que se abre la transacción.
/// </para>
/// <para>
/// <b>El evento sale de la base al apuntarlo</b>, de <see cref="ManejadorDeEvento{T}"/>, y no del
/// manejador al despachar: así se sabe a quién le toca un evento sin construir a los que no les
/// toca, y un manejador que no hereda de la base no llega a registrarse. Falla en el arranque,
/// que es donde se ve, y no en el primer evento, que es donde no lo mira nadie.
/// </para>
/// </remarks>
/// <param name="Manejador">El tipo del manejador, registrado tal cual en el contenedor.</param>
/// <param name="Evento">El evento al que atiende.</param>
/// <param name="Contexto">El contexto de su módulo, donde van su efecto, su huella y su respuesta.</param>
internal sealed record InscripcionDeManejador(Type Manejador, Type Evento, Type Contexto)
{
    /// <summary>Apunta un manejador, leyendo de su base el evento al que atiende.</summary>
    /// <param name="manejador">El tipo del manejador.</param>
    /// <param name="contexto">El contexto de su módulo.</param>
    /// <returns>La inscripción.</returns>
    /// <exception cref="InvalidOperationException">Si el manejador no hereda de la base.</exception>
    public static InscripcionDeManejador De(Type manejador, Type contexto)
    {
        ArgumentNullException.ThrowIfNull(manejador);
        ArgumentNullException.ThrowIfNull(contexto);

        for (Type? tipo = manejador; tipo is not null; tipo = tipo.BaseType)
        {
            if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(ManejadorDeEvento<>))
            {
                return new InscripcionDeManejador(manejador, tipo.GetGenericArguments()[0], contexto);
            }
        }

        throw new InvalidOperationException(
            $"El manejador {manejador.Name} no hereda de ManejadorDeEvento<T>, así que no dice a qué " +
            "evento atiende. Es la base la que lo dice al registrarlo, sin construirlo.");
    }
}
