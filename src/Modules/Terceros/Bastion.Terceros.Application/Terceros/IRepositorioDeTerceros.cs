using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Terceros;

/// <summary>Acceso a los terceros guardados.</summary>
/// <remarks>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure
/// (`principios/clean-architecture.md`). Ninguno de sus métodos confirma nada: eso lo decide el
/// caso de uso a través de <see cref="IUnidadTrabajoDeTerceros"/>.
/// </remarks>
public interface IRepositorioDeTerceros : IOrdenaPor
{
    /// <summary>El tercero con ese identificador, o nulo si no hay ninguno.</summary>
    Task<Tercero?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>
    /// Indica si esa empresa ya tiene un tercero con ese identificador fiscal, <b>esté activo o
    /// esté bloqueado</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Devuelve un booleano, y ahí está la propiedad de este ítem.</b> Quien pregunta no puede
    /// enterarse de si el que estorba está activo o bloqueado, porque esa información no cruza el
    /// puerto: no es que el caso de uso decida no mirarla, es que no la tiene. Un enumerado con
    /// tres valores habría dejado la indistinguibilidad del conflicto en manos de que nadie
    /// escribiera un <c>if</c>, y la primera vez que alguien quisiera «un mensaje más útil» lo
    /// escribiría. Con un <c>bool</c>, el mensaje más útil no se puede escribir.
    /// </para>
    /// <para>
    /// <b>Cuál de los dos era sí queda escrito</b>, pero en el registro y desde la implementación
    /// —que es quien lo sabe—, no en la respuesta. El art. 32 obliga a saber quién miró datos
    /// reservados; no obliga a contárselo a quien rellenó el formulario, y contárselo es
    /// exactamente lo que convierte un formulario de alta en un censo de bajas.
    /// </para>
    /// <para>
    /// <b>Ver lo bloqueado no lo abre este método</b>: lo abre el caso de uso, con su motivo de la
    /// lista cerrada. Aquí solo se consulta; si nadie ha abierto el ámbito, el filtro de R16 deja
    /// fuera lo bloqueado y esta pregunta responde que no — que es justo el fallo que
    /// <c>ElFiltroNoSeSaltaPorAhiTests</c> impide que ocurra en silencio.
    /// </para>
    /// </remarks>
    /// <param name="empresaId">Empresa a la que pertenecería la ficha (R8).</param>
    /// <param name="pais">País emisor, en ISO 3166-1 alfa-2.</param>
    /// <param name="numero">Identificador ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteLaIdentificacionAsync(
        Guid empresaId,
        string pais,
        string numero,
        CancellationToken cancelacion);

    /// <summary>Una página de terceros, con el total.</summary>
    Task<PaginaDe<Tercero>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Un tramo de terceros que cumplen el criterio, y por dónde seguir.</summary>
    /// <remarks>
    /// <para>
    /// <b>No devuelve total</b>, por lo mismo que la búsqueda de empresas: contar un conjunto
    /// filtrado cuesta un recorrido entero en cada tramo, que es lo que un cursor viene a evitar.
    /// </para>
    /// <para>
    /// <b>Y NO abre el ámbito de bloqueo</b>, que es la diferencia importante con
    /// <see cref="ExisteLaIdentificacionAsync"/>. Buscar por identificador fiscal es una consulta
    /// ordinaria, y una consulta ordinaria no ve lo bloqueado: si lo viera, la pantalla de
    /// búsqueda sería la puerta trasera del art. 32 con el criterio más cómodo posible.
    /// </para>
    /// </remarks>
    /// <param name="criterio">Lo que se busca, ya comprobado.</param>
    /// <param name="desde">Último tercero entregado, o nulo para empezar por el principio.</param>
    /// <param name="tamanio">Cuántos se piden.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<TramoDe<Tercero>> BuscarAsync(
        CriterioDeTerceros criterio,
        Guid? desde,
        int tamanio,
        CancellationToken cancelacion);

    /// <summary>Apunta un tercero nuevo. No lo graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(Tercero tercero);
}

/// <summary>
/// El criterio ya comprobado con el que se busca un tercero, tal como lo recibe el repositorio.
/// </summary>
/// <remarks>
/// <para>
/// <b>El identificador viaja en dos cadenas y no en un <see cref="IdentificacionFiscal"/>, y es lo
/// contrario de lo que hace <c>CriterioDeEmpresas</c>.</b> Allí el NIF va como objeto porque en la
/// base es un <b>valor convertido</b> —una sola columna—, y EF Core sabe comparar el objeto entero
/// pero no sabe entrar en su <c>.Valor</c>. Aquí la identificación es un <b>tipo poseído</b>: son
/// tres columnas, y lo que EF Core sabe traducir es justo lo contrario, la comparación miembro a
/// miembro. Pasar el objeto entero obligaría además a arrastrar su estado de verificación hasta el
/// repositorio para no compararlo, que es un campo que se lleva para ignorarlo.
/// </para>
/// <para>
/// Las dos cadenas llegan <b>ya normalizadas</b> por el mismo camino que el alta
/// (<c>IdentificacionFiscal.NormalizarNumero</c>, y para <c>ES</c> además validadas como NIF), así
/// que buscar «B-12345678» y «b12345678» encuentra lo mismo que se guardó.
/// </para>
/// </remarks>
/// <param name="Pais">País emisor en ISO 3166-1 alfa-2, o nulo si no se busca por identificador.</param>
/// <param name="Numero">Identificador exacto ya normalizado, o nulo.</param>
/// <param name="Nombre">Trozo de razón social o de nombre comercial, ya recortado, o nulo.</param>
public sealed record CriterioDeTerceros(string? Pais, string? Numero, string? Nombre);
