using Bastion.BuildingBlocks.Contracts.Maestros;

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
/// <b>Son CUATRO valores desde el ítem 3.6, y fueron tres hasta entonces</b>, por la decisión 11
/// del plan terminando su propia frase. Rechaza un cuarto valor en <c>EstadoDeMaestro</c> porque le
/// daría a los puertos de Organización «un valor que ninguno puede producir jamás —la casilla vacía
/// que el 1.10 acaba de quitar—», y en el renglón siguiente pedía cuatro aquí con uno de ellos,
/// «solo resuelve lo viejo», sin productor. Aplicado a sí mismo, su criterio daba tres mientras el
/// artículo no se pudiera retirar. El precedente era el del ítem 1.10 y no el del 1.2:
/// <c>LaMatrizDeLosPuertosDeEstadoTests</c> encontró <c>Bloqueado</c> inalcanzable en
/// <c>EstadoDelTercero</c>, y lo que sobraba era el valor.
/// </para>
/// <para>
/// <b>El cuarto entró con su disparador, que tenía nombre.</b> La cabecera de <c>Articulo</c>
/// decía cuándo tendría motivo para retirarse, «cuando tenga existencias que sostener». Las
/// existencias llegaron en el 2.7, la baja se quedó fuera de la fase 2 (respuesta 4 del repaso, en
/// <c>docs/PLAN.md</c>), y lo nuevo que elige un artículo, en la fase 3. En el 3.6 el artículo se
/// hizo retirable (ADR-0082, que amplía la decisión 1 del ADR-0023), y
/// <see cref="SoloResuelveLoViejo"/> tiene productor: la matriz lo exige contra la base.
/// </para>
/// <para>
/// <b>Y la baja sigue contestando la unidad base</b>, en
/// <see cref="IConsultaDeArticulos.UnidadesBaseDeAsync"/>. El recuento la pide para cada artículo
/// con físico en el almacén, y si falta una, no se abre. Un artículo retirado con existencias que
/// la perdiera dejaría sin recuento a su almacén, que es justo lo que hace falta para sacarlas.
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

    /// <summary>
    /// Existe y se almacena, pero está retirado: lo que ya lo nombra se sigue leyendo, y lo nuevo
    /// no lo elige (ADR-0023, ampliado por el ADR-0082).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el nombre del valor de <c>EstadoDeMaestro</c>, y significa lo mismo</b>: retirar no
    /// borra, así que la ficha, su unidad base y su nombre siguen saliendo. Lo que cambia es la
    /// aptitud para lo nuevo. Cada consumidor decide qué es lo nuevo: el ajuste, la transferencia y
    /// la reserva lo rechazan al abrirse, y el recuento lo admite, porque es la manera de dar de
    /// baja lo que quede.
    /// </para>
    /// <para>
    /// <b>Un servicio retirado sigue contestando <see cref="NoSeAlmacena"/></b>: esa respuesta ya no
    /// autoriza nada, y la retirada no le añade otra razón que el consumidor necesite.
    /// </para>
    /// </remarks>
    SoloResuelveLoViejo = 3,
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
/// <b>Para decidir no publica ni la descripción ni el código</b>, por lo mismo que el puerto de
/// terceros no publica la ficha: lo que hace falta para decidir si un movimiento es legal es una
/// aptitud, y una aptitud es lo que sale.
/// </para>
/// <para>
/// <b>Hoy no tiene consumidor: lo estrena el ítem 2.3</b>, igual que <c>IConsultaDeUnidadesDeMedida</c>
/// fue del 1.2 y lo estrenó el 1.8. No se adelanta por gusto: un identificador de otro módulo
/// obliga a que exista el puerto que lo valida (ADR-0024), y un puerto que no existe no se puede
/// exigir.
/// </para>
/// <para>
/// <b>Desde el 2.12 publica también la unidad base</b>, que es un identificador y no la ficha: el
/// recuento cuenta en ella, y el ajuste que genera mueve en ella con factor 1 (ADR-0055 §1).
/// </para>
/// <para>
/// <b>Y desde el 3.2, el código y la descripción, para enseñarlos</b>: la exportación de un listado
/// los escribe donde la pantalla los pinta (ADR-0074, que enmienda el §12 del ADR-0055). Salen por
/// lotes en <see cref="NombresDeAsync"/>, y con ellos no se decide nada.
/// </para>
/// </remarks>
public interface IConsultaDeArticulos
{
    /// <summary>Si se pueden mover existencias contra ese artículo, y por qué no si no.</summary>
    /// <param name="articuloId">Identificador del artículo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<AptitudParaMoverExistencias> AptitudDeAsync(
        Guid articuloId, CancellationToken cancelacion);

    /// <summary>La unidad base de cada artículo, en una sola consulta.</summary>
    /// <remarks>
    /// <b>Va por lotes</b>, como la marca de trazabilidad: un recuento pregunta por todos los
    /// artículos de un almacén a la vez. Un artículo que no está en la empresa no vuelve, y uno de
    /// otra empresa tampoco: desde fuera de Catálogo, las dos cosas son la misma.
    /// </remarks>
    /// <param name="articulos">Los artículos por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La unidad base de cada artículo que existe en la empresa.</returns>
    Task<IReadOnlyDictionary<Guid, Guid>> UnidadesBaseDeAsync(
        IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion);

    /// <summary>El código y la descripción de cada artículo, en una sola consulta.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nombra lo que enseña su ficha</b>, la de
    /// <c>GET /api/v1/catalogo/articulos/{id}</c> (ADR-0074). Uno de otra empresa no vuelve,
    /// como en <see cref="UnidadesBaseDeAsync"/>: desde fuera de Catálogo, es lo mismo que
    /// uno que no está.
    /// </para>
    /// <para>
    /// <b>No mira el permiso de quien pregunta</b>: lo mira quien pregunta, antes de preguntar, como
    /// la pantalla. Lo que no vuelve, se escribe con su identificador.
    /// </para>
    /// </remarks>
    /// <param name="articulos">Los artículos por los que se pregunta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>El código y el nombre de cada uno que vuelve.</returns>
    Task<IReadOnlyDictionary<Guid, NombreDeMaestro>> NombresDeAsync(
        IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion);
}
