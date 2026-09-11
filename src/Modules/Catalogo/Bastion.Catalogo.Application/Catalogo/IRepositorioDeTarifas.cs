using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Acceso a los tramos de tarifa guardados.</summary>
/// <remarks>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure
/// (`principios/clean-architecture.md`). Ninguno de sus métodos confirma nada: eso lo decide el
/// caso de uso a través de <see cref="IUnidadTrabajoDeCatalogo"/>.
/// </remarks>
public interface IRepositorioDeTarifas : IOrdenaPor
{
    /// <summary>El tramo de tarifa con ese identificador, o nulo si no hay ninguno.</summary>
    Task<Tarifa?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>
    /// El tramo de ese código que rige ese día, o nulo si ninguno lo hace.
    /// </summary>
    /// <remarks>
    /// <b>Devuelve uno o ninguno, y que no pueda devolver dos no lo garantiza esta consulta: lo
    /// garantiza la restricción de exclusión de la base.</b> Sin ella, dos tramos solapados del
    /// mismo código harían que esto contestara según el orden del plan de ejecución, y el síntoma
    /// no sería un error sino un precio distinto de un día para otro.
    /// </remarks>
    /// <param name="empresaId">Empresa a la que pertenece (R8).</param>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="dia">Día para el que se quiere el precio.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Tarifa?> VigenteAsync(
        Guid empresaId,
        string codigo,
        DateOnly dia,
        CancellationToken cancelacion);

    /// <summary>
    /// Indica si esa empresa tiene algún tramo con ese código, rija o no.
    /// </summary>
    /// <remarks>
    /// Existe para poder distinguir «esa tarifa no existe» de «esa tarifa existe y no rige hoy»,
    /// que son dos arreglos distintos y por tanto dos <c>type</c> distintos. Solo se pregunta por
    /// el camino de fallo: el que resuelve un precio correctamente nunca llega aquí.
    /// </remarks>
    /// <param name="empresaId">Empresa a la que pertenece (R8).</param>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteElCodigoAsync(Guid empresaId, string codigo, CancellationToken cancelacion);

    /// <summary>
    /// Indica si esa empresa ya tiene un tramo de ese código cuya vigencia se pisa con la que se le
    /// pasa.
    /// </summary>
    /// <remarks>
    /// <b>La restricción de verdad está en la base</b> —un <c>EXCLUDE USING gist</c> sobre la
    /// empresa, el código y el rango de fechas—, y esta consulta <b>no la sustituye</b>: la base es
    /// la única que puede impedirlo cuando dos peticiones llegan a la vez, y la única que cubre los
    /// caminos de escritura que no pasan por aquí. Esto se adelanta para poder contestar un 409 con
    /// el motivo escrito en vez de dejar salir una violación de integridad convertida en 500. Es el
    /// mismo reparto que en los tramos de impuesto del 0.15, y se escribe igual para que se lea
    /// igual.
    /// </remarks>
    /// <param name="empresaId">Empresa a la que pertenece (R8).</param>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="desde">Primer día del tramo que se quiere abrir.</param>
    /// <param name="hasta">Último día, o nulo si queda abierto.</param>
    /// <param name="excepto">Tramo que no cuenta, para poder comprobar uno contra los demás.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> HaySolapeAsync(
        Guid empresaId,
        string codigo,
        DateOnly desde,
        DateOnly? hasta,
        Guid? excepto,
        CancellationToken cancelacion);

    /// <summary>Una página de tramos de tarifa, con el total.</summary>
    /// <param name="paginacion">Qué página, de qué tamaño, con qué orden y qué filtro.</param>
    /// <param name="codigo">Código por el que se acota, ya normalizado, o nulo para no acotar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<Tarifa>> ListarAsync(
        Paginacion paginacion,
        string? codigo,
        CancellationToken cancelacion);

    /// <summary>Apunta un tramo nuevo. No lo graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(Tarifa tarifa);
}

/// <summary>Acceso a las líneas de tarifa guardadas.</summary>
/// <remarks>
/// Puerto aparte del de las tarifas, y no por tamaño: son dos agregados. Una tarifa mediana tiene
/// una línea por referencia, así que cargarlas todas como colección del padre para tocar una sola
/// es exactamente lo que la R12 evita —y el enredo del rastreador que documenta el ADR-0010 sale
/// de ahí—.
/// </remarks>
public interface IRepositorioDeLineasDeTarifa : IOrdenaPor
{
    /// <summary>La línea con ese identificador, o nula si no hay ninguna.</summary>
    Task<LineaTarifa?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>
    /// Todas las líneas de esa tarifa que alcanzan a ese artículo, con su nivel de ascenso.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es EL método caliente del módulo, y su firma es la decisión.</b> Recibe la ascendencia ya
    /// resuelta —<c>IRepositorioDeCategorias.AscendenciaAsync</c>, que la trae en una sola
    /// consulta— y devuelve de una vez las líneas del artículo y las de <b>todas</b> esas
    /// categorías, cada una con cuántos saltos hay hasta ella. <b>Una sola consulta, sea cual sea
    /// la profundidad</b> del artículo en el árbol.
    /// </para>
    /// <para>
    /// Las dos llamadas juntas —la ascendencia y ésta— son <b>dos viajes constantes</b>. Esa es la
    /// afirmación que este ítem sostiene y la que hay que poder poner roja: el número de consultas
    /// de una resolución de precio <b>no crece con la profundidad</b> de la categoría. Una
    /// implementación que subiera nivel a nivel daría el mismo precio en todos los casos y se
    /// notaría en un documento de cuarenta líneas, tres meses después, sin una sola consulta lenta
    /// que mirar.
    /// </para>
    /// <para>
    /// <b>El nivel lo pone quien conoce la ascendencia, no la base.</b> La posición de cada
    /// categoría en la lista que se pasa <i>es</i> su nivel, así que esta consulta no ordena por
    /// profundidad ni sabe qué es una profundidad: solo trae las líneas cuyo destino está en la
    /// lista. Lo que no es antepasado del artículo no entra en la lista, y por tanto <b>no puede
    /// llegar a competir</b> — que es la mitad estructural de la precedencia.
    /// </para>
    /// </remarks>
    /// <param name="tarifaId">Tramo de tarifa en el que se busca.</param>
    /// <param name="articuloId">Artículo al que se le quiere poner precio.</param>
    /// <param name="ascendencia">
    /// La categoría del artículo y sus padres hasta la raíz, en ese orden. Vacía si el artículo no
    /// está clasificado, y entonces solo pueden ganar las líneas de artículo.
    /// </param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<IReadOnlyList<LineaCandidata>> CandidatasAsync(
        Guid tarifaId,
        Guid articuloId,
        IReadOnlyList<Guid> ascendencia,
        CancellationToken cancelacion);

    /// <summary>Cuántas líneas tiene ya ese destino en esa tarifa, y si alguna empieza en esa cantidad.</summary>
    /// <param name="tarifaId">Tramo de tarifa.</param>
    /// <param name="articuloId">Artículo del destino, o nulo.</param>
    /// <param name="categoriaId">Categoría del destino, o nulo.</param>
    /// <param name="cantidadDesde">Cantidad desde la que empezaría el tramo nuevo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<TramosDelDestino> TramosDelDestinoAsync(
        Guid tarifaId,
        Guid? articuloId,
        Guid? categoriaId,
        decimal cantidadDesde,
        CancellationToken cancelacion);

    /// <summary>Una página de líneas de una tarifa, con el total.</summary>
    Task<PaginaDe<LineaTarifa>> ListarAsync(
        Paginacion paginacion,
        Guid tarifaId,
        CancellationToken cancelacion);

    /// <summary>Apunta una línea nueva. No la graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(LineaTarifa linea);
}

/// <summary>Lo que hay que saber del destino antes de añadirle un tramo.</summary>
/// <remarks>
/// Las dos preguntas van juntas en una sola consulta porque se contestan con el mismo barrido, y
/// porque separarlas invitaría a comprobar solo una: son las dos mitades de «ni hueco ni solape».
/// </remarks>
/// <param name="TieneAlguno">Si el destino ya tiene alguna línea en esta tarifa.</param>
/// <param name="YaEmpiezaEnEsaCantidad">Si alguna de sus líneas empieza justo en esa cantidad.</param>
public sealed record TramosDelDestino(bool TieneAlguno, bool YaEmpiezaEnEsaCantidad);
