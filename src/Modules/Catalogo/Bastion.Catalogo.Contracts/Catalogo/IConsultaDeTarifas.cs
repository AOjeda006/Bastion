namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>
/// Qué se puede hacer hoy con una tarifa a la que otro módulo apunta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Son tres preguntas y no una</b>, y por eso no es un <c>bool</c>: ¿existe?, ¿es del tipo por
/// el que se pregunta?, y ¿rige en esa fecha? Un puerto que solo contestara «existe» dejaría
/// asignarle a un cliente una tarifa cuya vigencia acabó el año pasado, y el error saldría en la
/// primera venta, lejos de quien lo causó. La del tipo entra en el 3.3, cuando la tarifa pasa a ser
/// de venta o de compra.
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

    /// <summary>
    /// Existe, pero es del otro tipo: de compra si se preguntó por una de venta, o al revés.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Se contesta antes que la vigencia</b> (3.3). El tipo va con el código y no cambia, así que
    /// este no tiene arreglo esperando: elegir el tramo vigente del mismo código daría otra vez una
    /// tarifa del otro tipo. Una caducada de otro tipo contesta esto, que es lo que hay que
    /// corregir primero.
    /// </para>
    /// <para>
    /// Es el <c>NoHaceEseRol</c> de <c>EstadoDelTercero</c>, la otra mitad del cruce, con otro
    /// sujeto: la tarifa no sirve para el papel por el que se pregunta.
    /// </para>
    /// </remarks>
    EsDeOtroTipo = 3,
}

/// <summary>Para qué se pregunta por una tarifa: para vender o para comprar.</summary>
/// <remarks>
/// <para>
/// Es un parámetro del puerto y no un dato que devuelva, por lo mismo que <c>RolDeTercero</c> en
/// la otra mitad del cruce: el estado <b>depende del tipo por el que se pregunta</b>, y quien
/// pregunta no puede olvidarse de comparar lo que no recibe.
/// </para>
/// <para>
/// <b>No es el <c>TipoDeTarifa</c> del dominio</b>, que este proyecto no ve (ADR-0076): son los
/// mismos dos valores con otro nombre, y el adaptador los traduce uno a uno.
/// </para>
/// </remarks>
public enum TipoDeLaTarifa
{
    /// <summary>La lista de precios que se le propone a un cliente.</summary>
    Venta = 0,

    /// <summary>La lista de precios de un proveedor.</summary>
    Compra = 1,
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
    /// <summary>En qué estado está esa tarifa para ese tipo y esa fecha.</summary>
    /// <remarks>
    /// <b>La fecha es un parámetro y no <c>hoy</c> por dentro</b>, por lo mismo que el tramo de
    /// impuesto pide su fecha de devengo: quien pregunta sabe para cuándo pregunta, y una
    /// implementación que mirara el reloj sería imposible de ejercer en el borde de la vigencia sin
    /// mover el reloj de la máquina.
    /// </remarks>
    /// <param name="tarifaId">Identificador de la tarifa.</param>
    /// <param name="tipo">El tipo que tiene que tener: de venta para un cliente, de compra para
    /// un proveedor.</param>
    /// <param name="enLaFecha">Día para el que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDeLaTarifa> EstadoDeAsync(
        Guid tarifaId, TipoDeLaTarifa tipo, DateOnly enLaFecha, CancellationToken cancelacion);
}
