using Bastion.BuildingBlocks.Contracts.Paginacion;

namespace Bastion.BuildingBlocks.Application.Listados;

/// <summary>
/// Lo que declara por qué campos deja ordenarse: la lista de nombres que el borde acepta en
/// <c>?sort=</c>.
/// </summary>
/// <remarks>
/// Es UNA lista y viaja de abajo arriba: la escribe el repositorio, junto al mapa de nombre a
/// columna que la produce, y el caso de uso la reenvía tal cual al borde. Escribirla otra vez en
/// el controlador daría dos listas que dicen lo mismo, y el día que alguien renombre un campo en
/// una sola de ellas la URL seguiría aceptándose y el orden que sale sería otro.
/// </remarks>
public interface IOrdenaPor
{
    /// <summary>Nombres externos por los que se puede ordenar este listado.</summary>
    IReadOnlySet<string> CamposOrdenables { get; }
}

/// <summary>
/// Un caso de uso de listado: devuelve una página y dice por qué campos deja ordenarla.
/// </summary>
/// <remarks>
/// <para>
/// Que los doce listados compartan interfaz no es una comodidad de tecleo: es lo que permite que
/// el borde tenga UNA forma de atender un listado —validar el orden pedido, rechazar con
/// <c>400</c> lo que no está en la lista, y responder— en vez de doce copias donde la número once
/// se olvida de validar y nadie lo nota.
/// </para>
/// <para>
/// Devuelve la página a secas y no un <c>Resultado</c>, a propósito: un listado no tiene desenlace
/// fallido de negocio. Que la paginación pedida sea absurda lo rechaza el borde con sus
/// anotaciones antes de llegar, y una colección vacía es una respuesta correcta, no un error
/// (ADR-0004: el <c>Resultado</c> es para lo que PUEDE fallar de verdad).
/// </para>
/// </remarks>
/// <typeparam name="TDto">Lo que se publica de cada elemento.</typeparam>
public interface IListado<TDto> : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="paginacion">Qué página se pide, de qué tamaño, con qué orden y qué filtro.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<TDto>> EjecutarAsync(Paginacion paginacion, CancellationToken cancelacion);
}
