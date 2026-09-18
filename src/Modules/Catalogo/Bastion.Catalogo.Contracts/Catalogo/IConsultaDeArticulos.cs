namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>
/// La respuesta a «¿puedo mover existencias contra este artículo?».
/// </summary>
/// <remarks>
/// <para>
/// <b>Se llama por la pregunta que contesta y no <c>EstadoDelArticulo</c></b>, que es el nombre que
/// pedía a gritos ser un <c>EstadoDeMaestro</c> recortado. No lo es: aquí no hay ciclo de vida del
/// artículo, hay aptitud para una operación. El día que el artículo tenga final de vida —y lo
/// tendrá, más abajo está cuándo— el valor que entre se leerá como lo que será, una
/// <b>incorporación al ciclo de vida</b>, y no como la redefinición de un estado que se quedó
/// corto.
/// </para>
/// <para>
/// <b>Son TRES valores y la decisión 11 del plan pedía cuatro</b>, y esto no es una desviación: es
/// esa decisión terminando su propia frase. Rechaza un cuarto valor en <c>EstadoDeMaestro</c>
/// porque le daría a los puertos de Organización «un valor que ninguno puede producir jamás —la
/// casilla vacía que el 1.10 acaba de quitar—», y en el renglón siguiente pide cuatro aquí con uno
/// de ellos, «solo resuelve lo viejo», sin productor. Aplicado a sí mismo, su criterio da tres.
/// </para>
/// <para>
/// <b>El precedente es el del ítem 1.10, no el del 1.2.</b> En el 1.2 un puerto sí llevó un valor
/// sin productor, pero el hueco tenía fecha —cinco ítems— y el consumidor llegaba en el 1.8. Aquí
/// no hay fecha. Lo que hay es el 1.10 exacto: <c>LaMatrizDeLosPuertosDeEstadoTests</c> encontró
/// <c>Bloqueado</c> inalcanzable en <c>EstadoDelTercero</c> y la conclusión fue que <b>lo que
/// sobraba era el valor</b>. Mismo test, misma conclusión, once ítems antes.
/// </para>
/// <para>
/// <b>Y el disparador del cuarto valor, con nombre.</b> <c>Articulo</c> no es bloqueable —no
/// guarda ni un dato de una persona— y tampoco se retira; su propia cabecera dice <b>cuándo</b>
/// tendrá motivo: «qué le pasa cuando deja de venderse es una pregunta de la fase 2, <b>cuando
/// tenga existencias que sostener</b>. Hoy no tiene ninguna». Las existencias llegan en el ítem
/// 2.7. Así que el valor que falta entra con <b>el primer ítem posterior al 2.7 que le dé una
/// baja</b>, y hoy ninguno de los catorce se la da: eso está anotado como pregunta del cierre de
/// la fase 2 en <c>docs/PLAN.md</c> → <i>Notas / riesgos</i>, y no se contesta aquí.
/// </para>
/// <para>
/// <see cref="NoExiste"/> es el valor cero a propósito, como en <c>EstadoDeMaestro</c> y en
/// <c>EstadoDelTercero</c>: si alguien recibe un <c>default</c> por un camino que no pasó por el
/// puerto, lo que se encuentra es la respuesta que no autoriza nada.
/// </para>
/// </remarks>
public enum AptitudParaMoverExistencias
{
    /// <summary>No hay en esta empresa ningún artículo con ese identificador.</summary>
    /// <remarks>
    /// Caben aquí dos situaciones y caben a propósito: no hay ficha con ese identificador, o la hay
    /// pero es de otra empresa (R8). Desde fuera de Catálogo las dos son lo mismo.
    /// </remarks>
    NoExiste = 0,

    /// <summary>Existe, se almacena, y se puede mover contra él.</summary>
    SeOfreceParaLoNuevo = 1,

    /// <summary>
    /// Existe, pero es un <c>Servicio</c>: no se almacena, así que no hay existencias que mover.
    /// </summary>
    /// <remarks>
    /// <b>No es un valor de <c>EstadoDeMaestro</c> ni podría serlo</b>, y por eso este enumerado es
    /// propio. Los tres valores de aquél son <b>dos preguntas</b> colapsadas —¿existe?, ¿vale para
    /// un alta?— y «¿se almacena?» es una tercera pregunta, no una tercera respuesta. Metida allí
    /// le daría a los cuatro puertos de Organización una casilla que ninguno puede producir.
    /// </remarks>
    NoSeAlmacena = 2,
}

/// <summary>
/// Lo que otros módulos pueden preguntar sobre los artículos.
/// </summary>
/// <remarks>
/// <para>
/// Es la lectura entre módulos del §4: <b>interfaz del <c>Contracts</c> del módulo dueño, resuelta
/// en proceso</b>. Ni un <c>JOIN</c> contra <c>catalogo.articulos</c> ni una llamada HTTP.
/// </para>
/// <para>
/// <b>No publica ni la descripción ni el código</b>, por lo mismo que el puerto de terceros no
/// publica la ficha: lo que hace falta para decidir si un movimiento es legal es una aptitud, y una
/// aptitud es lo que sale.
/// </para>
/// <para>
/// <b>Hoy no tiene consumidor: lo estrena el ítem 2.3</b>, igual que <c>IConsultaDeUnidadesDeMedida</c>
/// fue del 1.2 y lo estrenó el 1.8. No se adelanta por gusto: un identificador de otro módulo
/// obliga a que exista el puerto que lo valida (ADR-0024), y un puerto que no existe no se puede
/// exigir.
/// </para>
/// </remarks>
public interface IConsultaDeArticulos
{
    /// <summary>Si se pueden mover existencias contra ese artículo, y por qué no si no.</summary>
    /// <param name="articuloId">Identificador del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<AptitudParaMoverExistencias> AptitudDeAsync(
        Guid articuloId, CancellationToken cancelacion);
}
