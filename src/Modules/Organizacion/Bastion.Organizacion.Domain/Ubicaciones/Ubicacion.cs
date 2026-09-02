using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Organizacion.Domain.Ubicaciones;

/// <summary>
/// Un hueco concreto dentro de un almacén: dónde está la mercancía, no solo en qué nave.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es opcional para el almacén, no para el sistema.</b> El §7 la marca «(pasillo/estante/hueco,
/// opcional)»: hay almacenes que no se dividen —una nave pequeña donde todo está a la vista—, y
/// obligarles a inventarse una ubicación llenaría la tabla de filas <c>UNICA</c> que no dicen
/// nada. La opcionalidad está en que un almacén pueda no tener ninguna, no en que la ubicación
/// pueda existir a medias.
/// </para>
/// <para>
/// <b>Lleva <c>EmpresaId</c> propio aunque su almacén ya lo tenga</b>, y no es redundancia
/// gratuita. El filtro global de la R8 se escribe por entidad y se evalúa sobre las columnas de
/// la fila: sin la columna, filtrar una ubicación exigiría una subconsulta contra su almacén en
/// cada lectura, y bastaría una consulta que empezara por <c>Ubicaciones</c> —un listado nuevo, un
/// informe— para que las de otra empresa salieran. La columna hace que el filtro sea el mismo que
/// el de todas las demás tablas, que es justo lo que se quiere que nadie tenga que recordar.
/// </para>
/// <para>
/// <b>Se bloquea, no se borra</b>, por el mismo motivo que el almacén y desde el mismo mecanismo
/// (R16, ADR-0016): en cuanto llegue Inventario, cada movimiento apuntará a su ubicación para
/// siempre, y borrar la fila rompería el histórico de valoración, que no se reconstruye. Hoy no
/// hay existencias y borrar sería inocuo; el problema es que el camino de borrado seguiría ahí el
/// día que dejen de serlo, y esa es una decisión de esquema que sale gratis ahora y cuesta una
/// migración después.
/// </para>
/// </remarks>
public sealed class Ubicacion : EntidadBase, IDeInquilino, IBloqueable
{
    /// <summary>Tope del código: cabe en la etiqueta que se pega al estante.</summary>
    public const int LongitudMaximaDeCodigo = 20;

    /// <summary>Tope de cada una de las tres coordenadas.</summary>
    public const int LongitudMaximaDeCoordenada = 20;

    /// <summary>Tope de la descripción.</summary>
    public const int LongitudMaximaDeDescripcion = 120;

    private Ubicacion(
        Guid id,
        Guid empresaId,
        Guid almacenId,
        string codigo,
        string? pasillo,
        string? estante,
        string? hueco,
        string? descripcion,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        AlmacenId = almacenId;
        Codigo = codigo;
        Pasillo = pasillo;
        Estante = estante;
        Hueco = hueco;
        Descripcion = descripcion;
        Bloqueo = Bloqueo.Ninguno();
    }

    private Ubicacion()
    {
        Codigo = null!;
        Bloqueo = null!;
    }

    /// <summary>Identificador de la ubicación.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>Almacén en el que está.</summary>
    public Guid AlmacenId { get; private set; }

    /// <summary>Código que se imprime en la etiqueta, en mayúsculas. No cambia.</summary>
    public string Codigo { get; private set; }

    /// <summary>Pasillo, o nulo si el almacén no se divide así.</summary>
    public string? Pasillo { get; private set; }

    /// <summary>Estante, o nulo.</summary>
    public string? Estante { get; private set; }

    /// <summary>Hueco, o nulo.</summary>
    public string? Hueco { get; private set; }

    /// <summary>Aclaración para quien va a buscarla; nula si el código ya se explica solo.</summary>
    public string? Descripcion { get; private set; }

    /// <inheritdoc/>
    public Bloqueo Bloqueo { get; private set; }

    /// <summary>Da de alta una ubicación dentro de un almacén.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8), la misma que la del almacén.</param>
    /// <param name="almacenId">Almacén en el que está.</param>
    /// <param name="codigo">Código de la etiqueta; se normaliza a mayúsculas.</param>
    /// <param name="pasillo">Pasillo, o nulo.</param>
    /// <param name="estante">Estante, o nulo.</param>
    /// <param name="hueco">Hueco, o nulo.</param>
    /// <param name="descripcion">Aclaración, o nulo.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static Ubicacion Crear(
        Guid empresaId,
        Guid almacenId,
        string codigo,
        string? pasillo,
        string? estante,
        string? hueco,
        string? descripcion,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una ubicación pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        if (almacenId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una ubicación está siempre dentro de un almacén.", nameof(almacenId));
        }

        return new Ubicacion(
            Guid.CreateVersion7(),
            empresaId,
            almacenId,
            CodigoValido(codigo),
            Coordenada(pasillo, nameof(pasillo)),
            Coordenada(estante, nameof(estante)),
            Coordenada(hueco, nameof(hueco)),
            DescripcionValida(descripcion),
            momento);
    }

    /// <summary>
    /// Cambia las coordenadas y la descripción. Ni el código ni el almacén.
    /// </summary>
    /// <remarks>
    /// El código está impreso en una etiqueta que ya está pegada al estante. Y mover una ubicación
    /// de almacén no es un cambio de datos: sería mover la mercancía que hay dentro sin registrar
    /// ni un movimiento, que es exactamente lo que Inventario existe para impedir.
    /// </remarks>
    /// <param name="pasillo">Pasillo, o nulo.</param>
    /// <param name="estante">Estante, o nulo.</param>
    /// <param name="hueco">Hueco, o nulo.</param>
    /// <param name="descripcion">Aclaración, o nulo.</param>
    public void Modificar(string? pasillo, string? estante, string? hueco, string? descripcion)
    {
        Bloqueo.ExigirQueNoEsteBloqueado(
            $"La ubicación {Codigo}, bloqueada,",
            "el histórico de existencias la señala para siempre y la ficha que la describe se " +
            "conserva con ella");

        Pasillo = Coordenada(pasillo, nameof(pasillo));
        Estante = Coordenada(estante, nameof(estante));
        Hueco = Coordenada(hueco, nameof(hueco));
        Descripcion = DescripcionValida(descripcion);
    }

    /// <inheritdoc/>
    /// <remarks>Deja de admitir mercancía, y su histórico se conserva.</remarks>
    public void Bloquear(MotivoDeBloqueo motivo, DateTimeOffset momento) =>
        Bloqueo = Bloqueo.Bloquear(motivo, momento);

    /// <inheritdoc/>
    public void Desbloquear() => Bloqueo = Bloqueo.Desbloquear();

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <param name="codigo">Código tal como lo escribieron.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToUpperInvariant();
    }

    private static string CodigoValido(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string normalizado = NormalizarCodigo(codigo);

        return normalizado.Length <= LongitudMaximaDeCodigo
            ? normalizado
            : throw new ArgumentException(
                $"El código de ubicación admite {LongitudMaximaDeCodigo} caracteres como máximo.",
                nameof(codigo));
    }

    // Vacío y nulo son lo mismo aquí, y se guardan como nulo: dejar conviviendo la cadena vacía
    // con el nulo daría dos formas de decir «este almacén no tiene pasillos», y las consultas
    // tendrían que preguntar por las dos para siempre.
    private static string? Coordenada(string? valor, string parametro)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string limpio = valor.Trim().ToUpperInvariant();

        return limpio.Length <= LongitudMaximaDeCoordenada
            ? limpio
            : throw new ArgumentException(
                $"Una coordenada de ubicación admite {LongitudMaximaDeCoordenada} caracteres como máximo.",
                parametro);
    }

    private static string? DescripcionValida(string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return null;
        }

        string limpia = descripcion.Trim();

        return limpia.Length <= LongitudMaximaDeDescripcion
            ? limpia
            : throw new ArgumentException(
                $"La descripción admite {LongitudMaximaDeDescripcion} caracteres como máximo.",
                nameof(descripcion));
    }
}
