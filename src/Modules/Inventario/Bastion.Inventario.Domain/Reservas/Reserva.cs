using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Domain.Reservas;

/// <summary>
/// Lo que una línea de un documento de otro módulo aparta de una clave —un artículo en un
/// almacén— para servirlo después (ítem 2.13, ADR-0059).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una máquina de estados explícita, y no un <c>DocumentoBase</c></b> (ADR-0059 §10). Su estado
/// es privado y cambia solo por sus transiciones con nombre —<see cref="Consumir"/>,
/// <see cref="Liberar"/> y <see cref="LiberarSiHaCaducado"/>—, y cada una comprueba de dónde parte.
/// No publica el estado guardado porque nadie debe leerlo a secas: una reserva guardada activa que
/// ya caducó se lee liberada, y eso lo dice <see cref="EstadoEn"/>.
/// </para>
/// <para>
/// <b>Lo reservado no se guarda en ningún sitio más</b> (ADR-0059 §3). Lo que aparta una reserva es
/// <see cref="ApartaEn"/>, y lo reservado de una clave es la suma de eso sobre sus reservas, leída
/// con la valoración de la clave bloqueada. Este tipo no ve el cerrojo: lo toma el caso de uso.
/// </para>
/// </remarks>
public sealed class Reserva : EntidadBase, IDeInquilino
{
    /// <summary>Lo más largo que puede ser el motivo de una liberación a mano.</summary>
    public const int LargoDelMotivo = 300;

    private readonly List<ConsumoDeReserva> _consumos = [];

    // EL ESTADO GUARDADO, que EF mapea por este campo y que solo lee EstadoEn (ADR-0059 §4).
    private EstadoDeReserva _estado;

    private Reserva(
        Guid id,
        Guid empresaId,
        TipoDeOrigenDeReserva origenTipo,
        Guid origenId,
        int origenLinea,
        Guid articuloId,
        Guid almacenId,
        decimal cantidad,
        Guid unidadBaseId,
        DateTimeOffset? caducaEl,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        OrigenTipo = origenTipo;
        OrigenId = origenId;
        OrigenLinea = origenLinea;
        ArticuloId = articuloId;
        AlmacenId = almacenId;
        Cantidad = cantidad;
        UnidadBaseId = unidadBaseId;
        CaducaEl = caducaEl;
        _estado = EstadoDeReserva.Activa;
    }

    private Reserva()
    {
    }

    /// <summary>Identificador de la reserva.</summary>
    public Guid Id { get; private set; }

    /// <summary>La empresa (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>La clase del documento que la pidió.</summary>
    public TipoDeOrigenDeReserva OrigenTipo { get; private set; }

    /// <summary>El documento que la pidió, en su módulo.</summary>
    public Guid OrigenId { get; private set; }

    /// <summary>
    /// La línea de ese documento. El origen entero es único en todos los estados: una línea tiene
    /// una reserva, como mucho (ADR-0059 §1, precisión 6).
    /// </summary>
    public int OrigenLinea { get; private set; }

    /// <summary>El artículo que aparta.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>El almacén del que lo aparta.</summary>
    public Guid AlmacenId { get; private set; }

    /// <summary>Lo que se pidió apartar, en <see cref="UnidadBaseId"/>.</summary>
    public decimal Cantidad { get; private set; }

    /// <summary>
    /// La unidad base del artículo al reservar, copiada de Catálogo. La salida va en ella con factor
    /// uno, aunque el artículo cambie de unidad después (ADR-0059 §12).
    /// </summary>
    public Guid UnidadBaseId { get; private set; }

    /// <summary>
    /// Desde cuándo deja de apartar, en UTC y al microsegundo, o <c>null</c> si no caduca.
    /// </summary>
    public DateTimeOffset? CaducaEl { get; private set; }

    /// <summary>Por qué se liberó, si está guardada liberada.</summary>
    public CausaDeLiberacion? Causa { get; private set; }

    /// <summary>El motivo escrito de una liberación a mano. Nunca en otra.</summary>
    public string? Motivo { get; private set; }

    /// <summary>
    /// Cuándo se liberó, si está guardada liberada. En una caducidad, el instante de
    /// <see cref="CaducaEl"/>.
    /// </summary>
    public DateTimeOffset? LiberadaEl { get; private set; }

    /// <summary>Lo que han sacado los documentos de salida, uno por documento, en el orden en que salieron.</summary>
    public IReadOnlyList<ConsumoDeReserva> Consumos => [.. _consumos.OrderBy(consumo => consumo.CreadoEn)];

    /// <summary>Lo que ya ha salido por el libro.</summary>
    public decimal Consumida => _consumos.Sum(consumo => consumo.Cantidad);

    /// <summary>Lo que falta por salir de lo que se pidió, esté como esté.</summary>
    public decimal Pendiente => Cantidad - Consumida;

    /// <summary>Aparta una cantidad de una clave para una línea de un documento de otro módulo.</summary>
    /// <remarks>
    /// Comprueba la forma de lo que se pide, nada más. Que haya disponible, que el artículo y el
    /// almacén lo admitan y que el origen no tenga ya otra reserva lo mira el caso de uso, con la
    /// valoración de la clave bloqueada (ADR-0059 §5).
    /// </remarks>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="origenTipo">La clase del documento que la pide.</param>
    /// <param name="origenId">El documento que la pide.</param>
    /// <param name="origenLinea">Su línea, desde uno.</param>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="almacenId">El almacén.</param>
    /// <param name="cantidad">Cuánto, en la unidad base, positivo y con seis decimales como mucho.</param>
    /// <param name="unidadBaseId">La unidad base del artículo.</param>
    /// <param name="caducaEl">Desde cuándo deja de apartar, posterior a ahora; o <c>null</c>.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La reserva, activa.</returns>
    public static Reserva Reservar(
        Guid empresaId,
        TipoDeOrigenDeReserva origenTipo,
        Guid origenId,
        int origenLinea,
        Guid articuloId,
        Guid almacenId,
        decimal cantidad,
        Guid unidadBaseId,
        DateTimeOffset? caducaEl,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Una reserva sin empresa no existe (R8).", nameof(empresaId));
        }

        if (!Enum.IsDefined(origenTipo))
        {
            throw new ArgumentOutOfRangeException(
                nameof(origenTipo), origenTipo, "La columna guarda el nombre, y este valor no lo tiene.");
        }

        if (origenId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una reserva sin origen no se podría reconocer en un reintento (ADR-0059 §5).",
                nameof(origenId));
        }

        if (origenLinea <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(origenLinea), origenLinea, "Las líneas de un documento se cuentan desde uno.");
        }

        if (articuloId == Guid.Empty)
        {
            throw new ArgumentException("Una reserva aparta un artículo.", nameof(articuloId));
        }

        if (almacenId == Guid.Empty)
        {
            throw new ArgumentException("Una reserva aparta de un almacén.", nameof(almacenId));
        }

        if (cantidad <= 0m || decimal.Round(cantidad, MovimientoStock.DecimalesDeCantidad) != cantidad)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidad),
                cantidad,
                $"Se reserva una cantidad positiva, con {MovimientoStock.DecimalesDeCantidad} decimales " +
                "como mucho: con más, la base la redondearía al guardarla.");
        }

        if (unidadBaseId == Guid.Empty)
        {
            throw new ArgumentException("Una reserva habla en la unidad base de su artículo.", nameof(unidadBaseId));
        }

        DateTimeOffset? caducidad = ComoSeGuarda(caducaEl);

        if (caducidad <= momento)
        {
            throw new ArgumentOutOfRangeException(
                nameof(caducaEl),
                caducaEl,
                "La caducidad es posterior al momento de reservar: una que ya ha pasado nacería liberada.");
        }

        return new Reserva(
            Guid.CreateVersion7(),
            empresaId,
            origenTipo,
            origenId,
            origenLinea,
            articuloId,
            almacenId,
            cantidad,
            unidadBaseId,
            caducidad,
            momento);
    }

    /// <summary>
    /// El estado de la reserva en un instante: el guardado, salvo que esté guardada activa y su
    /// caducidad no sea posterior a ese instante, que se lee liberada (ADR-0059 §4).
    /// </summary>
    /// <param name="ahora">El instante, el del <c>TimeProvider</c>.</param>
    /// <returns>El estado.</returns>
    public EstadoDeReserva EstadoEn(DateTimeOffset ahora) =>
        _estado == EstadoDeReserva.Activa && HaVencidoEn(ahora) ? EstadoDeReserva.Liberada : _estado;

    /// <summary>
    /// Si en ese instante la reserva está liberada por su caducidad, se haya escrito ya o no. Es lo
    /// que distingue el <c>409</c> de una caducada del de una consumida o liberada a mano.
    /// </summary>
    /// <param name="ahora">El instante.</param>
    /// <returns><c>true</c> si caducó.</returns>
    public bool HaCaducadoEn(DateTimeOffset ahora) =>
        (_estado == EstadoDeReserva.Activa && HaVencidoEn(ahora))
        || (_estado == EstadoDeReserva.Liberada && Causa == CausaDeLiberacion.Caducidad);

    /// <summary>Lo que aparta en ese instante: lo pendiente si está activa, y nada si no.</summary>
    /// <param name="ahora">El instante.</param>
    /// <returns>La cantidad, en su unidad base.</returns>
    public decimal ApartaEn(DateTimeOffset ahora) =>
        EstadoEn(ahora) == EstadoDeReserva.Activa ? Pendiente : 0m;

    /// <summary>
    /// Si una petición pide lo mismo que esta reserva: el mismo artículo, el mismo almacén, la misma
    /// cantidad y la misma caducidad, comparada como se guarda. Es la idempotencia del origen
    /// (ADR-0059 §5): con lo mismo se devuelve esta, y con otra cosa es un <c>409</c>.
    /// </summary>
    /// <param name="articuloId">El artículo pedido.</param>
    /// <param name="almacenId">El almacén pedido.</param>
    /// <param name="cantidad">La cantidad pedida.</param>
    /// <param name="caducaEl">La caducidad pedida.</param>
    /// <returns><c>true</c> si es la misma petición.</returns>
    public bool PideLoMismo(Guid articuloId, Guid almacenId, decimal cantidad, DateTimeOffset? caducaEl) =>
        ArticuloId == articuloId
        && AlmacenId == almacenId
        && Cantidad == cantidad
        && CaducaEl == ComoSeGuarda(caducaEl);

    /// <summary>
    /// Lo que las líneas de un consumo piden valorar: una por línea, en su orden, con la clave de la
    /// reserva y la cantidad que sale, negativa.
    /// </summary>
    /// <param name="lineas">Las líneas del consumo.</param>
    /// <returns>Las líneas a valorar.</returns>
    public IReadOnlyList<LineaAValorar> LineasAValorar(IReadOnlyList<LineaDeConsumo> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);

        return [.. lineas.Select(linea => new LineaAValorar(new ClaveDeValoracion(ArticuloId, AlmacenId), -linea.Cantidad))];
    }

    /// <summary>
    /// Consume una parte o todo lo pendiente con un documento de salida, y devuelve las filas del libro
    /// que hay que escribir <b>en la misma transacción</b> (ADR-0059 §6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dobla la R12</b>: el consumo que baja lo reservado y las filas que bajan el físico salen
    /// juntos de aquí, y van en el mismo <c>COMMIT</c>. Con dos, habría un instante en que lo que sale
    /// contaría dos veces.
    /// </para>
    /// <para>
    /// <b>Todo lo que puede fallar se mira antes de cambiar nada</b>, así que una excepción deja la
    /// reserva como estaba. El caso de uso lo ha mirado antes y lo ha traducido a un rechazo; esto es
    /// la segunda vez.
    /// </para>
    /// </remarks>
    /// <param name="documentoTipo">La clase del documento que sale: en el 2.13, el albarán.</param>
    /// <param name="documentoId">El documento que sale.</param>
    /// <param name="fechaDeOperacion">El día de la salida.</param>
    /// <param name="lineas">Lo que saca, por hueco.</param>
    /// <param name="valoracion">
    /// Una línea valorada por línea, en su orden: la de <see cref="LineasAValorar"/>.
    /// </param>
    /// <param name="resueltos">La fila de cada lote y de cada serie que nombran las líneas.</param>
    /// <param name="divisa">La divisa base de la empresa, la de las filas.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>Una fila del libro por línea.</returns>
    public IReadOnlyList<MovimientoStock> Consumir(
        TipoDeDocumentoOrigen documentoTipo,
        Guid documentoId,
        DateOnly fechaDeOperacion,
        IReadOnlyList<LineaDeConsumo> lineas,
        IReadOnlyList<LineaValorada> valoracion,
        LotesYSeriesResueltos resueltos,
        string divisa,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(valoracion);
        ArgumentNullException.ThrowIfNull(resueltos);

        if (documentoTipo != TipoDeDocumentoOrigen.Albaran)
        {
            throw new ArgumentException(
                $"Una reserva la consume un albarán, y no un documento de tipo {documentoTipo}: el " +
                "ajuste y la transferencia no salen contra una reserva (ADR-0059 §7).",
                nameof(documentoTipo));
        }

        if (documentoId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un consumo sin documento no se ata a sus filas del libro (ADR-0059 §8).", nameof(documentoId));
        }

        if (lineas.Count == 0)
        {
            throw new ArgumentException(
                "Un consumo sin líneas no saca nada, y dejaría un consumo sin su fila del libro.", nameof(lineas));
        }

        if (lineas.Any(linea => linea is null))
        {
            throw new ArgumentException("Una línea de consumo nula no saca nada.", nameof(lineas));
        }

        if (lineas.Where(linea => linea.NumeroDeSerie is not null)
            .GroupBy(linea => linea.NumeroDeSerie, StringComparer.Ordinal)
            .Any(grupo => grupo.Count() > 1))
        {
            throw new ArgumentException(
                "Un número de serie sale una sola vez por documento (ADR-0048 §3).", nameof(lineas));
        }

        if (EstadoEn(momento) != EstadoDeReserva.Activa)
        {
            throw new InvalidOperationException(
                $"La reserva {Id} no está activa: está {EstadoEn(momento)}" +
                (HaCaducadoEn(momento) ? $", porque caducó el {CaducaEl:O}." : "."));
        }

        if (_consumos.Any(consumo => consumo.DocumentoTipo == documentoTipo && consumo.DocumentoId == documentoId))
        {
            throw new InvalidOperationException(
                $"El documento {documentoId} ya consumió la reserva {Id}: un documento la consume una vez.");
        }

        decimal sale = lineas.Sum(linea => linea.Cantidad);

        if (sale > Pendiente)
        {
            throw new InvalidOperationException(
                $"Las líneas sacan {sale} y a la reserva {Id} le quedan {Pendiente}: no se consume " +
                "más de lo que aparta.");
        }

        string laDelLibro = CatalogoDeDivisas.Normalizar(divisa);

        if (valoracion.Count != lineas.Count
            || valoracion.Any(valorada => valorada is null
                || valorada.Valor.Divisa != laDelLibro
                || valorada.PrecioMedio.Divisa != laDelLibro))
        {
            throw new ArgumentException(
                $"El consumo tiene {lineas.Count} líneas en {laDelLibro}, y la valoración trae " +
                $"{valoracion.Count}: hace falta una por línea, en esa divisa (ADR-0046 §10).",
                nameof(valoracion));
        }

        if (lineas.Any(linea =>
            (linea.CodigoDeLote is { } lote && !resueltos.Lotes.ContainsKey(new(ArticuloId, lote)))
            || (linea.NumeroDeSerie is { } serie && !resueltos.Series.ContainsKey(new(ArticuloId, serie)))))
        {
            throw new ArgumentException(
                "El consumo nombra lotes o series que no llegan resueltos: cada código trae su fila, " +
                "que el caso de uso busca sin crearla (ADR-0059 §6).",
                nameof(resueltos));
        }

        // LAS FILAS ANTES QUE EL CONSUMO: si alguna no se deja registrar, la reserva no ha cambiado.
        MovimientoStock[] filas =
        [
            .. lineas.Select((linea, indice) => MovimientoStock.Registrar(
                EmpresaId,
                fechaDeOperacion,
                AlmacenId,
                linea.UbicacionId,
                ArticuloId,
                linea.CodigoDeLote is { } lote ? resueltos.Lotes[new(ArticuloId, lote)] : null,
                linea.NumeroDeSerie is { } serie ? resueltos.Series[new(ArticuloId, serie)] : null,
                -linea.Cantidad,
                UnidadBaseId,
                1m,
                laDelLibro,
                null,
                valoracion[indice].Valor,
                valoracion[indice].PrecioMedio,
                documentoTipo,
                documentoId,
                momento)),
        ];

        _consumos.Add(ConsumoDeReserva.Anotar(Id, documentoTipo, documentoId, fechaDeOperacion, sale, momento));

        if (Pendiente == 0m)
        {
            _estado = EstadoDeReserva.Consumida;
        }

        return filas;
    }

    /// <summary>Suelta a mano lo que le queda, con un motivo escrito. Lo consumido sigue siendo suyo.</summary>
    /// <param name="motivo">Por qué, escrito por quien la libera.</param>
    /// <param name="momento">Ahora.</param>
    public void Liberar(string motivo, DateTimeOffset momento)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException(
                "Una reserva liberada a mano sin motivo escrito es mercancía que vuelve sin explicación.",
                nameof(motivo));
        }

        string limpio = motivo.Trim();

        if (limpio.Length > LargoDelMotivo)
        {
            throw new ArgumentException($"El motivo no puede pasar de {LargoDelMotivo} caracteres.", nameof(motivo));
        }

        if (EstadoEn(momento) != EstadoDeReserva.Activa)
        {
            throw new InvalidOperationException(
                $"La reserva {Id} no está activa, y solo se libera una activa: está {EstadoEn(momento)}" +
                (HaCaducadoEn(momento) ? $", porque caducó el {CaducaEl:O}." : "."));
        }

        _estado = EstadoDeReserva.Liberada;
        Causa = CausaDeLiberacion.AMano;
        Motivo = limpio;
        LiberadaEl = momento;
    }

    /// <summary>
    /// La deja guardada liberada si está guardada activa y ya caducó, con la causa y la fecha de su
    /// caducidad. Es la escritura que hace al pasar por su clave quien reserva, consume o libera
    /// (ADR-0059 §4).
    /// </summary>
    /// <param name="ahora">El instante, el del <c>TimeProvider</c>.</param>
    /// <returns><c>true</c> si la ha liberado ahora; <c>false</c> si no había nada que hacer.</returns>
    public bool LiberarSiHaCaducado(DateTimeOffset ahora)
    {
        if (_estado != EstadoDeReserva.Activa || !HaVencidoEn(ahora))
        {
            return false;
        }

        _estado = EstadoDeReserva.Liberada;
        Causa = CausaDeLiberacion.Caducidad;
        LiberadaEl = CaducaEl;

        return true;
    }

    // UTC Y AL MICROSEGUNDO, que es lo que guarda timestamptz: así la reserva que vuelve de la base
    // es la que se pidió, y el reintento que trae la misma caducidad la reconoce (ADR-0059 §5).
    private static DateTimeOffset? ComoSeGuarda(DateTimeOffset? instante) =>
        instante is { } valor
            ? new DateTimeOffset(valor.UtcTicks - (valor.UtcTicks % TimeSpan.TicksPerMicrosecond), TimeSpan.Zero)
            : null;

    private bool HaVencidoEn(DateTimeOffset ahora) => CaducaEl is { } caducidad && caducidad <= ahora;
}
