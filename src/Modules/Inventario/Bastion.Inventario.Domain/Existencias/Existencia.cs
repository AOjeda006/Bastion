using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Inventario.Domain.Existencias;

/// <summary>
/// Lo que hay ahora de un artículo en una ubicación: la <b>fila viva</b> de la proyección del
/// libro (ADR-0044).
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto es un contador, y la R3 dice que el stock no lo es.</b> Las dos cosas se sostienen a la
/// vez porque esta fila no es la verdad: es una copia de la suma del libro que se guarda para no
/// tener que sumarlo entero cada vez que alguien pregunta. La definición del saldo sigue siendo
/// la suma de <c>MovimientoStock</c> con fecha hasta hoy. Que esta fila diga lo mismo no se da por
/// supuesto. Lo comprueban el cuadre, que las compara, y el test de propiedad del 2.7.
/// </para>
/// <para>
/// <b>Aquí no hay forma de moverla</b>, y esa ausencia es la decisión: ni un <c>set</c> accesible
/// ni un método que sume. La única escritura es la sentencia que anota el libro, que suma sobre lo
/// que hay en el motor y en la misma transacción que las filas del libro. Si la aplicación leyera
/// la fila para escribirla, dos confirmaciones simultáneas del mismo artículo leerían el mismo
/// saldo y una de las dos se perdería.
/// </para>
/// <para>
/// <b>No es una entidad del tipo base</b>, por lo mismo que <c>ContadorDeSerie</c>: sus marcas de
/// tiempo las pondría el interceptor al guardar, y a esta fila no la guarda nadie por el
/// rastreador. Unas marcas congeladas mentirían.
/// </para>
/// </remarks>
public sealed class Existencia : IDeInquilino
{
    /// <summary>Constructor de materialización: EF Core rellena la fila.</summary>
    private Existencia()
    {
    }

    /// <summary>Identificador de la fila.</summary>
    /// <remarks>
    /// No es la clave de negocio: la clave es la combinación de abajo, y la sostiene un índice
    /// único. Existe porque el lote puede ser nulo, y una clave primaria no admite nulos. Las
    /// instantáneas mensuales cuelgan de él.
    /// </remarks>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>El artículo. Vive en el esquema de Catálogo.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>El almacén. Vive en el esquema de Organización.</summary>
    public Guid AlmacenId { get; private set; }

    /// <summary>La ubicación dentro del almacén. Vive en el esquema de Organización.</summary>
    public Guid UbicacionId { get; private set; }

    /// <summary>El lote, o <see langword="null"/> si el artículo no lo lleva.</summary>
    /// <remarks>
    /// <b>Estaba en la clave desde el 2.7, aunque nadie lo escribiera hasta el 2.9</b>, que trajo su
    /// tabla y su clave ajena (ADR-0048 §2). Lo que mantiene en una fila por clave las que no llevan
    /// lote es que la unicidad no distingue nulos.
    /// </remarks>
    public Guid? LoteId { get; private set; }

    /// <summary>El número de serie, o <see langword="null"/> si el artículo no lo lleva.</summary>
    /// <remarks>
    /// <b>Una fila con número de serie tiene como mucho una unidad</b>, y un número de serie tiene
    /// existencias en una sola fila. Las dos cosas las sostiene el motor, con un <c>CHECK</c> y un índice único parcial
    /// (ADR-0048 §3), porque dos confirmaciones simultáneas no se ven la una a la otra.
    /// </remarks>
    public Guid? NumeroDeSerieId { get; private set; }

    /// <summary>Lo que hay, en la unidad base del artículo: la suma del libro.</summary>
    /// <remarks>
    /// <b>Lo reservado no está aquí</b> (ADR-0059 §3): es por almacén y no por hueco, se suma al leer
    /// de las reservas de la clave, y el disponible es la cantidad de la valoración menos esa suma.
    /// Las dos columnas que lo guardaban, a cero hasta el 2.13, se fueron con su migración.
    /// </remarks>
    public decimal Fisico { get; private set; }

    /// <summary>
    /// Lo que vuela hacia esta fila, en la unidad base: las líneas de las transferencias enviadas y
    /// todavía no recibidas que llegan aquí (ADR-0053 §1).
    /// </summary>
    /// <remarks>
    /// <b>No es del libro, y por eso no está en el físico ni en el disponible.</b> Lo cuadra su propio
    /// cuadre contra los documentos. Una fila con número de serie cuenta su tránsito como una unidad
    /// más, porque la serie sigue estando en un solo sitio mientras viaja (§7).
    /// </remarks>
    public decimal EnTransito { get; private set; }
}
