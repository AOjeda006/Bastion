using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Inventario.Domain.Movimientos;

/// <summary>
/// Una línea del libro mayor de existencias (R3): lo que entró o salió, dónde, de qué artículo y
/// contra qué documento.
/// </summary>
/// <remarks>
/// <para>
/// <b>El stock no es un contador, es este libro.</b> No hay ninguna columna «existencias» que se
/// sume y se reste: lo que hay en una ubicación es la suma de sus filas de aquí, y el saldo
/// proyectado del 2.7 tendrá que coincidir siempre con esa suma. De ahí que la fila no cambie
/// nunca —lo que corrige un movimiento es otro movimiento, con el signo contrario— y de ahí que
/// la tabla sea de solo añadido en el motor, con disparadores y no con un permiso que el dueño
/// pueda devolverse a sí mismo.
/// </para>
/// <para>
/// <b>Tres cantidades y no una, que es lo que pide la trazabilidad.</b>
/// <see cref="CantidadEnUnidadBase"/> es en lo que se suma —todo el libro habla la misma unidad
/// por artículo, o sumar no significaría nada—; <see cref="CantidadIntroducida"/> y
/// <see cref="UnidadIntroducidaId"/> son lo que escribió la persona, que es lo que aparece en el
/// papel que firmó; y <see cref="FactorAUnidadBase"/> es el puente, guardado en la fila porque una
/// conversión puede cambiar mañana y la fila de ayer tiene que seguir explicándose sola. La regla
/// que las une está en <see cref="EnUnidadBase"/> y la sostiene además un <c>CHECK</c> del motor.
/// </para>
/// <para>
/// <b>Los tres identificadores de fuera no llevan clave ajena</b> —almacén, ubicación y artículo
/// viven en otros esquemas y ninguna clave ajena cruza de esquema (invariante 5)—. Quien los
/// valida antes de escribir son los tres puertos del ítem 2.2 (ADR-0024), y el almacén o la
/// ubicación pueden estar bloqueados sin que esta fila deje de ser verdad: una estantería
/// bloqueada sigue existiendo (ADR-0037).
/// </para>
/// <para>
/// <b>No es bloqueable y no se audita</b>, y las dos cosas por el mismo motivo: la fila no cambia
/// nunca. Auditar sus cambios duplicaría la tabla más grande del sistema para registrar cero
/// cambios, y bloquearla sería esconder un hecho contable. El motivo se escribe en su
/// configuración, que es donde el barrido lo exige.
/// </para>
/// </remarks>
public sealed class MovimientoStock : EntidadBase, IDeInquilino
{
    /// <summary>Decimales de una cantidad del libro.</summary>
    /// <remarks>
    /// Los mismos seis del factor de una conversión (<c>ConversionUM.DecimalesDelFactor</c>), y no
    /// por gusto: la cantidad en unidad base es un producto por ese factor, así que darle menos
    /// escala que al factor tiraría precisión que el factor sí sabía expresar.
    /// </remarks>
    public const int DecimalesDeCantidad = 6;

    private MovimientoStock(
        Guid id,
        Guid empresaId,
        DateOnly fechaDeOperacion,
        Guid almacenId,
        Guid ubicacionId,
        Guid articuloId,
        Guid? loteId,
        Guid? numeroDeSerieId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        string divisa,
        Importe? costeUnitario,
        Importe valor,
        PrecioUnitario precioMedio,
        TipoDeDocumentoOrigen documentoOrigenTipo,
        Guid documentoOrigenId,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        FechaDeOperacion = fechaDeOperacion;
        AlmacenId = almacenId;
        UbicacionId = ubicacionId;
        ArticuloId = articuloId;
        LoteId = loteId;
        NumeroDeSerieId = numeroDeSerieId;
        CantidadIntroducida = cantidadIntroducida;
        UnidadIntroducidaId = unidadIntroducidaId;
        FactorAUnidadBase = factorAUnidadBase;
        CantidadEnUnidadBase = EnUnidadBase(cantidadIntroducida, factorAUnidadBase);
        Divisa = divisa;
        CosteUnitarioSinDivisa = costeUnitario?.Cantidad;
        ValorSinDivisa = valor.Cantidad;
        PrecioMedioSinDivisa = precioMedio.Cantidad;
        DocumentoOrigenTipo = documentoOrigenTipo;
        DocumentoOrigenId = documentoOrigenId;
    }

    /// <summary>Constructor de materialización: EF Core rellena la fila.</summary>
    private MovimientoStock()
    {
    }

    /// <summary>Identificador de la fila.</summary>
    /// <remarks>
    /// No es la clave primaria él solo: la clave es <c>(id, fecha_de_operacion)</c>, porque
    /// PostgreSQL exige que la clave de partición forme parte de toda clave única de una tabla
    /// particionada. La unicidad efectiva sigue siendo la del identificador, que es un UUID v7.
    /// </remarks>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>Día en que la operación ocurrió, y clave de partición del libro (R14).</summary>
    /// <remarks>
    /// Es una <b>fecha de negocio</b>, no un instante: el día en que se movió la mercancía, sin
    /// hora ni zona. Por eso <see cref="DateOnly"/> y columna <c>date</c>, y por eso no sirve
    /// <c>CreadoEn</c> para particionar — un ajuste de diciembre grabado en enero es una fila de
    /// diciembre, y en la partición de enero estaría mal contada.
    /// </remarks>
    public DateOnly FechaDeOperacion { get; private set; }

    /// <summary>Almacén en el que ocurrió. Vive en el esquema de Organización.</summary>
    public Guid AlmacenId { get; private set; }

    /// <summary>Ubicación concreta dentro del almacén. Vive en el esquema de Organización.</summary>
    public Guid UbicacionId { get; private set; }

    /// <summary>Artículo que se movió. Vive en el esquema de Catálogo.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>El lote que se movió, o <see langword="null"/>.</summary>
    /// <remarks>
    /// <b>Este sí lleva clave ajena</b>: el lote vive en el mismo esquema (ADR-0048 §2). Y es la
    /// fila y no el código, porque el código solo no identifica nada sin su artículo.
    /// </remarks>
    public Guid? LoteId { get; private set; }

    /// <summary>El número de serie que se movió, o <see langword="null"/>. Nunca con lote.</summary>
    public Guid? NumeroDeSerieId { get; private set; }

    /// <summary>Cantidad <b>con signo</b>, en la unidad base del artículo: lo que se suma.</summary>
    /// <remarks>
    /// Positiva es entrada y negativa es salida. Cero no se admite —lo impide la fábrica y lo
    /// impide un <c>CHECK</c>—: una fila que no mueve nada deja constancia de un movimiento que no
    /// ocurrió, y la suma no la distingue de no haberla escrito.
    /// </remarks>
    public decimal CantidadEnUnidadBase { get; private set; }

    /// <summary>Cantidad tal como se introdujo, en la unidad en que se introdujo.</summary>
    public decimal CantidadIntroducida { get; private set; }

    /// <summary>Unidad en la que se introdujo. Vive en el esquema de Organización.</summary>
    public Guid UnidadIntroducidaId { get; private set; }

    /// <summary>Factor por el que se pasó de la unidad introducida a la base.</summary>
    /// <remarks>
    /// Se guarda <b>en la fila</b> y no se vuelve a buscar: una conversión se puede modificar o
    /// retirar (ADR-0023), y si la cantidad base se recalculara al leer, una fila de hace dos años
    /// cambiaría de valor porque alguien corrigió una tabla de maestros hoy. El libro no puede
    /// hacer eso.
    /// </remarks>
    public decimal FactorAUnidadBase { get; private set; }

    /// <summary>La divisa de todos los importes de la fila (R6).</summary>
    /// <remarks>
    /// <b>Una columna por fila, y no una por importe</b> como en el resto del proyecto: con una
    /// sola no se puede escribir una fila que mezcle divisas. Y es la columna que el coste ya
    /// tenía, renombrada, porque una nueva habría que rellenarla en las filas que ya están, y el
    /// libro no admite un <c>UPDATE</c> (ADR-0046 §7).
    /// </remarks>
    public string Divisa { get; private set; } = string.Empty;

    /// <summary>Coste de una unidad base, o <c>null</c> si la fila no lo trajo.</summary>
    /// <remarks>
    /// Lo trae una entrada con coste. Una salida no lo lleva, porque se valora al precio medio
    /// (ADR-0046 §7), y las escritas antes del 2.8 lo llevan y se quedan como están. Se compone al
    /// leer, con la divisa de la fila.
    /// </remarks>
    public Importe? CosteUnitario =>
        CosteUnitarioSinDivisa is { } coste ? Importe.De(coste, Divisa) : null;

    /// <summary>La columna del coste, sin divisa: lo que EF Core lee y escribe.</summary>
    private decimal? CosteUnitarioSinDivisa { get; set; }

    /// <summary>
    /// Lo que la fila sumó a la valoración de su artículo y almacén, o lo que le restó: lleva el
    /// signo de la cantidad (ADR-0046 §5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es un importe redondeado una sola vez</b>, en <c>PrecioUnitario.Por</c>, y la valoración
    /// suma exactamente esto. Así, el valor de una fecha pasada es la suma de esta columna hasta
    /// esa fecha, sin reproducir nada.
    /// </para>
    /// <para>
    /// <b>Las filas de antes del 2.8 valen cero</b>, porque no congelaron ningún precio y el libro
    /// no se reescribe (ADR-0046 §8).
    /// </para>
    /// </remarks>
    public Importe Valor => Importe.De(ValorSinDivisa, Divisa);

    /// <summary>
    /// El precio medio que la fila congela: el de después en lo que entra, el de antes en lo que
    /// sale (ADR-0046 §5).
    /// </summary>
    /// <remarks>
    /// Se guarda aunque se pudiera deducir del libro, por lo mismo que el factor: la fila tiene que
    /// explicarse sola, y reproducir la valoración para saber a qué precio salió una unidad hace dos
    /// años es justo lo que el PMP perpetuo evita.
    /// </remarks>
    public PrecioUnitario PrecioMedio => PrecioUnitario.De(PrecioMedioSinDivisa, Divisa);

    /// <summary>La columna del valor, sin divisa: lo que EF Core lee y escribe.</summary>
    private decimal ValorSinDivisa { get; set; }

    /// <summary>La columna del precio medio, sin divisa: lo que EF Core lee y escribe.</summary>
    private decimal PrecioMedioSinDivisa { get; set; }

    /// <summary>Qué clase de documento lo escribió (R13).</summary>
    public TipoDeDocumentoOrigen DocumentoOrigenTipo { get; private set; }

    /// <summary>Identificador del documento que lo escribió (R13).</summary>
    public Guid DocumentoOrigenId { get; private set; }

    // AQUÍ NO HAY «QUIÉN», Y LA AUSENCIA ES LA DECISIÓN. La primera versión de esta entidad llevaba
    // un `RegistradoPorUsuarioId`, y sobraba por dos motivos que apuntan al mismo sitio: el criterio
    // del 2.3 enumera las columnas del libro y no lo nombra, y el «quién» ya está en el DOCUMENTO,
    // que sí se audita (ADR-0012). Una segunda copia del usuario en la tabla que más crece del
    // sistema no añade una respuesta: añade una que puede discrepar de la otra.
    //
    // Y tenía una consecuencia que la habría dado por buena sin mirarla: un `uuid` de Identidad en
    // el dominio obliga por R7 a nombrar el puerto que lo valida, y validar por un puerto al sujeto
    // que viene firmado en el token no es una comprobación, es un viaje.

    /// <summary>
    /// La regla que une las tres cantidades: la base es la introducida por el factor, redondeada a
    /// la escala del libro.
    /// </summary>
    /// <remarks>
    /// <b>Redondeo <see cref="MidpointRounding.AwayFromZero"/> y escrito</b>, como en todo el
    /// proyecto (R6): el de .NET por omisión es el del banquero y daría 0,0000125 → 0,000012.
    /// El motor sostiene la misma igualdad con un <c>CHECK</c>, y el <c>round</c> de PostgreSQL
    /// sobre <c>numeric</c> redondea también alejándose del cero, así que las dos comprobaciones
    /// dicen lo mismo y no una cada cosa.
    /// </remarks>
    /// <param name="cantidadIntroducida">Cantidad tal como se escribió.</param>
    /// <param name="factorAUnidadBase">Factor hacia la unidad base del artículo.</param>
    /// <returns>La cantidad en unidad base.</returns>
    public static decimal EnUnidadBase(decimal cantidadIntroducida, decimal factorAUnidadBase) =>
        decimal.Round(
            cantidadIntroducida * factorAUnidadBase,
            DecimalesDeCantidad,
            MidpointRounding.AwayFromZero);

    /// <summary>Registra una línea del libro. Es la única forma de que exista.</summary>
    /// <param name="empresaId">Empresa dueña de la fila (R8).</param>
    /// <param name="fechaDeOperacion">Día en que ocurrió.</param>
    /// <param name="almacenId">Almacén.</param>
    /// <param name="ubicacionId">Ubicación dentro del almacén.</param>
    /// <param name="articuloId">Artículo movido.</param>
    /// <param name="loteId">El lote, o <c>null</c>.</param>
    /// <param name="numeroDeSerieId">El número de serie, o <c>null</c>. Nunca con lote.</param>
    /// <param name="cantidadIntroducida">Cantidad tal como se escribió, con signo.</param>
    /// <param name="unidadIntroducidaId">Unidad en la que se escribió.</param>
    /// <param name="factorAUnidadBase">Factor hacia la unidad base del artículo.</param>
    /// <param name="divisa">La divisa de los importes de la fila.</param>
    /// <param name="costeUnitario">Coste de una unidad base, en esa divisa, o <c>null</c>.</param>
    /// <param name="valor">Lo que la fila suma o resta a la valoración, con el signo de la cantidad.</param>
    /// <param name="precioMedio">El precio medio que la fila congela.</param>
    /// <param name="documentoOrigenTipo">Clase del documento que la escribe (R13).</param>
    /// <param name="documentoOrigenId">Identificador de ese documento (R13).</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    /// <returns>La línea, ya con su cantidad base calculada.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// El factor no es positivo, o la cantidad —introducida o en base— es cero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Algún importe va en otra divisa que la fila, el valor no lleva el signo de la cantidad o el
    /// precio medio es negativo. O la fila lleva lote y número de serie, o un número de serie
    /// que no mueve una unidad base.
    /// </exception>
    public static MovimientoStock Registrar(
        Guid empresaId,
        DateOnly fechaDeOperacion,
        Guid almacenId,
        Guid ubicacionId,
        Guid articuloId,
        Guid? loteId,
        Guid? numeroDeSerieId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        string divisa,
        Importe? costeUnitario,
        Importe valor,
        PrecioUnitario precioMedio,
        TipoDeDocumentoOrigen documentoOrigenTipo,
        Guid documentoOrigenId,
        DateTimeOffset momento)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factorAUnidadBase);
        ArgumentNullException.ThrowIfNull(valor);
        ArgumentNullException.ThrowIfNull(precioMedio);

        string laDeLaFila = CatalogoDeDivisas.Normalizar(divisa);

        if (costeUnitario is not null && costeUnitario.Divisa != laDeLaFila)
        {
            throw new ArgumentException(
                $"El coste va en {costeUnitario.Divisa} y la fila en {laDeLaFila}: todos los " +
                "importes de una fila van en su divisa (R6).",
                nameof(costeUnitario));
        }

        if (valor.Divisa != laDeLaFila || precioMedio.Divisa != laDeLaFila)
        {
            throw new ArgumentException(
                $"El valor va en {valor.Divisa}, el precio medio en {precioMedio.Divisa} y la fila " +
                $"en {laDeLaFila}: todos los importes de una fila van en su divisa (R6).",
                nameof(valor));
        }

        // CERO VALE CON CUALQUIER SIGNO: una entrada a coste cero, o la salida de una clave que
        // solo tiene filas de antes del 2.8, no mueven valor.
        if (valor.Cantidad * cantidadIntroducida < 0m)
        {
            throw new ArgumentException(
                "El valor va con el signo de la cantidad: lo que entra suma valor y lo que sale lo " +
                "resta (ADR-0046 §5).",
                nameof(valor));
        }

        if (precioMedio.Cantidad < 0m)
        {
            throw new ArgumentException(
                "Un precio medio negativo no existe: sale de un valor y una cantidad que no lo son.",
                nameof(precioMedio));
        }

        if (cantidadIntroducida == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidadIntroducida), "una línea del libro que no mueve nada no se escribe");
        }

        // Y la base también, por separado: una cantidad introducida diminuta con un factor
        // diminuto se redondea a cero, y entonces la fila diría que movió algo y sumaría nada.
        if (EnUnidadBase(cantidadIntroducida, factorAUnidadBase) == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidadIntroducida),
                "la cantidad en unidad base se redondea a cero: la fila sumaría nada");
        }

        // LO MISMO QUE LA LÍNEA, OTRA VEZ: el libro lo escribirán cinco documentos, y no todos
        // tendrán líneas de ajuste delante.
        if (loteId is not null && numeroDeSerieId is not null)
        {
            throw new ArgumentException(
                "Una fila del libro lleva lote o número de serie, no los dos (ADR-0048 §1).",
                nameof(numeroDeSerieId));
        }

        if (numeroDeSerieId is not null
            && Math.Abs(EnUnidadBase(cantidadIntroducida, factorAUnidadBase)) != 1m)
        {
            throw new ArgumentException(
                "Una fila con número de serie mueve una unidad base, arriba o abajo (ADR-0048 §3).",
                nameof(numeroDeSerieId));
        }

        return new MovimientoStock(
            Guid.CreateVersion7(),
            empresaId,
            fechaDeOperacion,
            almacenId,
            ubicacionId,
            articuloId,
            loteId,
            numeroDeSerieId,
            cantidadIntroducida,
            unidadIntroducidaId,
            factorAUnidadBase,
            laDeLaFila,
            costeUnitario,
            valor,
            precioMedio,
            documentoOrigenTipo,
            documentoOrigenId,
            momento);
    }
}
