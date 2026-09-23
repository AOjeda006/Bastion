namespace Bastion.Organizacion.Contracts.Ejercicios;

/// <summary>
/// Qué se puede escribir con una fecha, según el ejercicio al que caiga (R9).
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="SinEjercicio"/> es un valor propio y no un hueco</b>, y esa es la decisión de este
/// enumerado. Una fecha que no cae en ningún ejercicio no es «un ejercicio que no existe»: es un
/// documento que no se podría imputar a ninguna autoliquidación, ni hoy ni cuando alguien lo
/// busque. Contestarlo como si fuera un fallo —nulo, o una excepción— obligaría a cada llamante a
/// decidir qué hacer con eso, y la primera vez que alguien decidiera «pues adelante» el agujero
/// sería permanente. Como valor del enumerado, <b>rechaza la escritura</b> igual que
/// <see cref="Cerrado"/>, y con un error distinto porque se arreglan distinto: uno se arregla
/// abriendo el ejercicio que falta, el otro cambiando la fecha o reabriendo.
/// </para>
/// <para>
/// Es el valor <b>cero</b> a propósito, por lo mismo que <c>EstadoDeMaestro.NoExiste</c>: si
/// alguien recibe un <c>default</c> por un camino que no pasó por el puerto, se encuentra la
/// respuesta que no autoriza nada.
/// </para>
/// </remarks>
public enum EstadoDelEjercicioParaEscribir
{
    /// <summary>Esa fecha no cae dentro de ningún ejercicio de la empresa.</summary>
    SinEjercicio = 0,

    /// <summary>Cae en un ejercicio abierto: se puede escribir.</summary>
    Abierto = 1,

    /// <summary>Cae en un ejercicio cerrado: el periodo ya es definitivo.</summary>
    Cerrado = 2,
}

/// <summary>
/// Lo que otro módulo pregunta antes de hacer definitivo un documento: si su fecha se puede
/// escribir.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>organizacion.ejercicios</c> ni una llamada HTTP.
/// </para>
/// <para>
/// <b>Y es la GUARDA, no una cortesía.</b> La distinción importa y está escrita también al otro
/// lado: lo que el cierre le pregunta a cada módulo —si le quedan borradores dentro— es una
/// <b>cortesía</b>, porque un borrador no está imputado a nada y lo único que se gana con la
/// pregunta es que quien cierra se entere antes en vez de encontrarse el documento colgando
/// después. Esto no: esto es lo que impide que un documento se haga definitivo con fecha en un
/// periodo que ya lo es. Si se quita la cortesía, alguien se lleva un susto; si se quita esta, el
/// libro deja de cuadrar con lo presentado.
/// </para>
/// <para>
/// <b>Por eso la respuesta se lee con la fila bloqueada, y por eso no vale con haber preguntado
/// antes.</b> Entre la pregunta y el <c>COMMIT</c> del documento cabe un cierre entero. Quien la
/// implementa toma un cerrojo <b>compartido</b> sobre la fila del ejercicio y lo suelta al
/// confirmar la transacción, así que el cierre —que toma el exclusivo— espera a que las
/// confirmaciones en vuelo acaben, y las que empiecen después ven el ejercicio cerrado. Las dos
/// cosas —el estado y el cerrojo— salen de la <b>misma lectura</b>: comprobar y luego bloquear
/// deja exactamente la ventana que se viene a cerrar.
/// </para>
/// <para>
/// <b>R11 no sirve aquí</b>, y por eso hace falta el cerrojo: el testigo de concurrencia protege a
/// dos que escriben la misma fila, y quien confirma un documento <b>no escribe la fila del
/// ejercicio</b>. Para la R11, confirmar y cerrar no se pisan.
/// </para>
/// </remarks>
public interface IConsultaDeEjercicios
{
    /// <summary>
    /// En qué estado está, <b>para escribir</b>, el ejercicio al que cae esa fecha.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bloquea, así que se llama dentro de la transacción que va a escribir</b> y lo más tarde
    /// posible: el cerrojo compartido dura hasta el <c>COMMIT</c>, y cuanto antes se tome, más
    /// tiempo tiene esperando quien quiera cerrar. Quien la implementa <b>revienta</b> si no hay
    /// transacción abierta, en vez de contestar un estado que no significaría nada.
    /// </para>
    /// <para>
    /// La empresa no viaja en la firma: sale de donde la toma el filtro global (R8), nunca de
    /// quien llama. Con ella en la firma, «confirmar en mi empresa» sería «confirmar contra el
    /// ejercicio de cualquiera».
    /// </para>
    /// </remarks>
    /// <param name="fecha">La fecha de operación del documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<EstadoDelEjercicioParaEscribir> ParaEscribirEnAsync(
        DateOnly fecha, CancellationToken cancelacion);
}
