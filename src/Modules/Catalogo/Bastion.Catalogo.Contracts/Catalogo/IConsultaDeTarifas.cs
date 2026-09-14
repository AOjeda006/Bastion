namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>
/// Qué se puede hacer hoy con una tarifa a la que otro módulo apunta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Son dos preguntas y no una</b>, y por eso son tres valores y no un <c>bool</c>: ¿existe?, y
/// ¿rige en la fecha por la que se pregunta? Un puerto que solo contestara «existe» dejaría
/// asignarle a un cliente una tarifa cuya vigencia acabó el año pasado, y el error saldría en la
/// primera venta, lejos de quien lo causó.
/// </para>
/// <para>
/// <b>Es la misma forma que <c>EstadoDeMaestro</c>, y no es el mismo tipo a propósito.</b> Una
/// tarifa no es un maestro de instalación: es de una empresa, no se retira —caduca— y su tercer
/// estado sale de dos fechas y no de una marca. Compartir el tipo obligaría a
/// <c>Catalogo.Contracts</c> a referenciar <c>Organizacion.Contracts</c> o al bloque común, que
/// hoy no referencia nada; y el parecido no es una razón para atarlos, porque nadie sostiene los
/// dos valores a la vez ni los compara. Lo que sí se repite —y a conciencia— es el
/// <b>razonamiento</b>: «existe» y «vale para algo nuevo» son dos preguntas, y las dos hacen falta.
/// </para>
/// <para>
/// <c>NoExiste</c> es el valor cero por lo mismo que allí: un <c>default</c> que llegara por un
/// camino que no pasó por el puerto no autoriza nada.
/// </para>
/// </remarks>
public enum EstadoDeLaTarifa
{
    /// <summary>No hay ninguna tarifa con ese identificador en esta empresa.</summary>
    /// <remarks>
    /// La consulta va por el filtro de inquilinato (R8), así que una tarifa de otra empresa de la
    /// instalación <b>no existe</b> desde aquí, y contesta lo mismo que una inventada.
    /// </remarks>
    NoExiste = 0,

    /// <summary>Existe y su vigencia cubre esa fecha: se puede asignar.</summary>
    RigeEnEsaFecha = 1,

    /// <summary>
    /// Existe, pero su vigencia no cubre esa fecha: sigue resolviendo lo de entonces y no se
    /// ofrece para algo nuevo.
    /// </summary>
    /// <remarks>
    /// Es el <c>SoloResuelveLoViejo</c> del ADR-0023 con un sujeto nuevo. «Lo viejo» es la lectura
    /// habitual y no la única: una tarifa que <b>todavía</b> no ha entrado en vigor también existe
    /// y tampoco se asigna hoy, y cae en este mismo valor. Las dos son la misma respuesta a la
    /// misma pregunta —«¿vale para un alta con esta fecha?»— y tienen un solo sitio donde caber.
    /// </remarks>
    SoloResuelveLoViejo = 2,
}

/// <summary>
/// Lo que otros módulos pueden preguntar sobre las tarifas.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>catalogo.tarifas</c> ni una llamada HTTP.
/// </para>
/// <para>
/// <b>Es la mitad de vuelta del primer cruce MUTUO del proyecto.</b> La otra es
/// <c>IConsultaDeTerceros</c>, en <c>Terceros.Contracts</c>. Que los dos <c>Contracts</c> no se
/// vean entre sí es lo que impide que la mutua sea un ciclo: por aquí cruzan <c>Guid</c>,
/// <c>DateOnly</c> y un enumerado propio, y ni un tipo del otro módulo. Si algún día alguien
/// quisiera devolver aquí un <c>TerceroDto</c>, el compilador hablaría de proyectos y no de esto:
/// queda dicho para que se lea aquí, que es donde se va a mirar.
/// </para>
/// <para>
/// <b>Este proyecto no referencia nada, y sigue sin referenciar nada.</b> Era ya una decisión
/// escrita en su <c>.csproj</c> —una referencia puesta «porque los otros la tienen» es un permiso
/// sin motivo—, y este puerto no la rompe porque no necesita ni un tipo prestado.
/// </para>
/// </remarks>
public interface IConsultaDeTarifas
{
    /// <summary>En qué estado está esa tarifa para esa fecha.</summary>
    /// <remarks>
    /// <b>La fecha es un parámetro y no <c>hoy</c> por dentro</b>, por lo mismo que el tramo de
    /// impuesto pide su fecha de devengo: quien pregunta sabe para cuándo pregunta, y una
    /// implementación que mirara el reloj sería imposible de ejercer en el borde de la vigencia sin
    /// mover el reloj de la máquina.
    /// </remarks>
    /// <param name="tarifaId">Identificador de la tarifa.</param>
    /// <param name="enLaFecha">Día para el que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDeLaTarifa> EstadoDeAsync(
        Guid tarifaId, DateOnly enLaFecha, CancellationToken cancelacion);
}
