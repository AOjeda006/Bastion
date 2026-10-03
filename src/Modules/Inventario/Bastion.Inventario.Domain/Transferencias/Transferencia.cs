using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Documentos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Domain.Multiempresa;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Domain.Transferencias;

/// <summary>
/// El segundo documento que escribe en el libro: lleva mercancía de un almacén a otro de la misma
/// empresa, en dos momentos (ADR-0053).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada línea escribe dos filas del libro, cada una en su momento.</b> Al enviar sale del origen,
/// y al recibir entra en el destino. Entre los dos, la mercancía vuela: no está en ningún almacén,
/// pero sigue siendo de la empresa, y el tránsito del destino la cuenta (§1).
/// </para>
/// <para>
/// <b>El valor viaja con la línea</b> (§2): la salida del origen se valora a su precio medio, la
/// línea guarda ese importe, y la entrada en el destino entra con él, sin coste.
/// </para>
/// <para>
/// <b>Se anula con otra transferencia, con las líneas negadas</b> (§5). El inverso niega cada pata
/// que el original escribió, y nace <see cref="EstadoDeTransferencia.Recibida"/>.
/// </para>
/// </remarks>
public sealed class Transferencia : DocumentoBase<EstadoDeTransferencia>, IDeInquilino
{
    /// <summary>Longitud máxima del motivo de una anulación.</summary>
    public const int LargoDelMotivo = 300;

    private readonly List<LineaDeTransferencia> _lineas = [];

    private Transferencia(
        Guid id,
        Guid empresaId,
        Guid serieId,
        Guid almacenOrigenId,
        Guid almacenDestinoId,
        DateOnly fechaDeEnvio,
        string divisa,
        DateTimeOffset momento)
        : base(EstadoDeTransferencia.Borrador, momento)
    {
        Id = id;
        EmpresaId = empresaId;
        SerieId = serieId;
        AlmacenOrigenId = almacenOrigenId;
        AlmacenDestinoId = almacenDestinoId;
        FechaDeEnvio = fechaDeEnvio;
        Divisa = divisa;
    }

    /// <summary>Constructor de materialización para EF Core.</summary>
    private Transferencia()
    {
    }

    /// <summary>Identificador de la transferencia. Es el documento origen de sus movimientos (R13).</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>La serie que la numera al enviarla. Se elige al abrirla y no se cambia.</summary>
    public Guid SerieId { get; private set; }

    /// <summary>El correlativo que la serie le dio al enviarla, o <c>null</c> en borrador.</summary>
    public long? Numero { get; private set; }

    /// <summary>De qué almacén sale.</summary>
    public Guid AlmacenOrigenId { get; private set; }

    /// <summary>A qué almacén llega. Nunca el de origen.</summary>
    public Guid AlmacenDestinoId { get; private set; }

    /// <summary>El día en que sale del origen (R14). Es el de las filas del origen.</summary>
    /// <remarks>
    /// <b>Se pone al abrirla, como la del ajuste</b>, y es la que numera: el número sale en la serie
    /// del ejercicio de esta fecha (ADR-0053 §3).
    /// </remarks>
    public DateOnly FechaDeEnvio { get; private set; }

    /// <summary>
    /// El día en que entra en el destino, o <c>null</c> mientras vuela. Es el de las filas del destino.
    /// </summary>
    public DateOnly? FechaDeRecepcion { get; private set; }

    /// <summary>La divisa de todos los importes del documento: la divisa base de la empresa.</summary>
    public string Divisa { get; private set; } = string.Empty;

    /// <summary>Por qué se anula, en un inverso, o <c>null</c> en cualquier otra.</summary>
    /// <remarks>
    /// <b>Una transferencia no necesita justificarse como un ajuste</b>: mover mercancía de un almacén
    /// a otro no corrige nada. Lo que sí pide un motivo es deshacerla (ADR-0053 §12).
    /// </remarks>
    public string? Motivo { get; private set; }

    /// <summary>La transferencia que este documento compensa, o <c>null</c> si no es un inverso.</summary>
    /// <remarks>Un solo enlace, como en el ajuste, y un índice único que impide un segundo inverso.</remarks>
    public Guid? AnulaAId { get; private set; }

    /// <summary>Las líneas del documento, por su número.</summary>
    public IReadOnlyList<LineaDeTransferencia> Lineas => [.. _lineas.OrderBy(linea => linea.Numero)];

    /// <summary>Abre una transferencia en borrador.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8).</param>
    /// <param name="serieId">Serie que la numerará al enviarla (R5).</param>
    /// <param name="almacenOrigenId">De dónde sale.</param>
    /// <param name="almacenDestinoId">A dónde llega. Distinto del origen.</param>
    /// <param name="fechaDeEnvio">El día en que sale.</param>
    /// <param name="divisa">La divisa base de la empresa, en la que irán todos los importes.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La transferencia en borrador, sin líneas ni número.</returns>
    public static Transferencia Abrir(
        Guid empresaId,
        Guid serieId,
        Guid almacenOrigenId,
        Guid almacenDestinoId,
        DateOnly fechaDeEnvio,
        string divisa,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Una transferencia sin empresa no existe (R8).", nameof(empresaId));
        }

        if (serieId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una transferencia sin serie no se podría numerar al enviarla (R5).", nameof(serieId));
        }

        if (almacenOrigenId == Guid.Empty || almacenDestinoId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una transferencia va de un almacén a otro, y los dos se nombran.", nameof(almacenOrigenId));
        }

        if (almacenOrigenId == almacenDestinoId)
        {
            throw new ArgumentException(
                "El origen y el destino son el mismo almacén: eso no es una transferencia, y moverlo " +
                "de ubicación es la reubicación (ADR-0053 §8).",
                nameof(almacenDestinoId));
        }

        return new Transferencia(
            Guid.CreateVersion7(),
            empresaId,
            serieId,
            almacenOrigenId,
            almacenDestinoId,
            fechaDeEnvio,
            CatalogoDeDivisas.Normalizar(divisa),
            momento);
    }

    /// <summary>Añade una línea. Solo en borrador, y con cantidad positiva.</summary>
    /// <param name="ubicacionOrigenId">Hueco del almacén de origen.</param>
    /// <param name="ubicacionDestinoId">Hueco del almacén de destino.</param>
    /// <param name="articuloId">Artículo que se mueve.</param>
    /// <param name="cantidadIntroducida">Cantidad, positiva, tal como se escribió.</param>
    /// <param name="unidadIntroducidaId">Unidad en la que se escribió.</param>
    /// <param name="factorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
    /// <param name="momento">Ahora.</param>
    /// <param name="codigoDeLote">El código del lote, o <c>null</c>.</param>
    /// <param name="numeroDeSerie">El número de serie, o <c>null</c>. Nunca con lote.</param>
    public void AnadirLinea(
        Guid ubicacionOrigenId,
        Guid ubicacionDestinoId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        DateTimeOffset momento,
        string? codigoDeLote = null,
        string? numeroDeSerie = null)
    {
        if (Estado != EstadoDeTransferencia.Borrador)
        {
            throw new InvalidOperationException(
                $"Una transferencia en estado «{Estado}» no admite líneas nuevas: sus filas del libro " +
                "ya están escritas (R2, R3).");
        }

        if (cantidadIntroducida <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidadIntroducida),
                cantidadIntroducida,
                "Una transferencia lleva mercancía del origen al destino: la cantidad es positiva. La " +
                "negativa es la de su inverso, que se construye anulando.");
        }

        var nueva = LineaDeTransferencia.Crear(
            Id,
            _lineas.Count + 1,
            ubicacionOrigenId,
            ubicacionDestinoId,
            articuloId,
            cantidadIntroducida,
            unidadIntroducidaId,
            factorAUnidadBase,
            codigoDeLote,
            numeroDeSerie,
            momento);

        if (nueva.NumeroDeSerie is { } serie
            && _lineas.Any(linea => linea.ArticuloId == articuloId
                && string.Equals(linea.NumeroDeSerie, serie, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"El número de serie «{serie}» ya está en otra línea de este documento: una serie sale " +
                "una sola vez por documento (ADR-0048 §3).");
        }

        _lineas.Add(nueva);
    }

    /// <summary>Los lotes que nombran las líneas, sin repetir y en el orden del cerrojo.</summary>
    /// <returns>Cada lote, con su artículo.</returns>
    public IReadOnlyList<CodigoDeUnArticulo> LotesQueNombra() => Nombrados(linea => linea.CodigoDeLote);

    /// <summary>Las series que nombran las líneas, sin repetir y en el orden del cerrojo.</summary>
    /// <returns>Cada serie, con su artículo.</returns>
    public IReadOnlyList<CodigoDeUnArticulo> SeriesQueNombra() => Nombrados(linea => linea.NumeroDeSerie);

    /// <summary>Lo que se valora en el origen: una línea por línea, en su orden.</summary>
    /// <remarks>
    /// <para>
    /// <b>La pata del origen mueve la cantidad negada</b>: una salida al enviar, y una entrada en el
    /// inverso.
    /// </para>
    /// <para>
    /// <b>Su valor sale de tres sitios, según quién la pide.</b> Al enviar, del precio medio del
    /// origen, así que no trae nada. En el inverso de una enviada, del valor que compensa: vuelve
    /// exacto lo que estaba en tránsito. Y en el inverso de una recibida, de lo que la salida del
    /// destino se llevó, que entra por parámetro porque se valora antes (ADR-0053 §6).
    /// </para>
    /// </remarks>
    /// <param name="loQueSalioDelDestino">
    /// En el inverso de una recibida, la valoración de la pata del destino. <c>null</c> en otro caso.
    /// </param>
    /// <returns>Las líneas a valorar, en el orden de <see cref="Lineas"/>.</returns>
    public IReadOnlyList<LineaAValorar> LineasAValorarEnElOrigen(
        IReadOnlyList<LineaValorada>? loQueSalioDelDestino = null)
    {
        IReadOnlyList<LineaDeTransferencia> lineas = Lineas;

        if (loQueSalioDelDestino is not null)
        {
            ExigirUnaPorLinea(loQueSalioDelDestino, nameof(loQueSalioDelDestino));
        }

        return
        [
            .. lineas.Select((linea, indice) => new LineaAValorar(
                new ClaveDeValoracion(linea.ArticuloId, AlmacenOrigenId),
                -linea.CantidadEnUnidadBase,
                coste: null,
                loQueSalioDelDestino is { } salio
                    ? Importe.De(-salio[indice].Valor.Cantidad, Divisa)
                    : linea.ValorQueCompensa is { } compensa ? Importe.De(-compensa, Divisa) : null)),
        ];
    }

    /// <summary>Lo que se valora en el destino: una línea por línea, en su orden.</summary>
    /// <remarks>
    /// <b>La pata del destino mueve la cantidad tal cual, y siempre con un valor que compensa.</b> Al
    /// recibir, el que viajó, entero: sin coste y sin el precio medio del destino. En el inverso de
    /// una recibida, el que entró, negado, que el tope deja en lo que quede (ADR-0053 §2 y §5).
    /// </remarks>
    /// <returns>Las líneas a valorar, en el orden de <see cref="Lineas"/>.</returns>
    public IReadOnlyList<LineaAValorar> LineasAValorarEnElDestino() =>
    [
        .. Lineas.Select(linea => new LineaAValorar(
            new ClaveDeValoracion(linea.ArticuloId, AlmacenDestinoId),
            linea.CantidadEnUnidadBase,
            coste: null,
            Importe.De(
                linea.Valor ?? linea.ValorQueCompensa
                    ?? throw new InvalidOperationException(
                        "Una línea que no ha salido del origen no tiene valor que llevar al destino."),
                Divisa))),
    ];

    /// <summary>Envía la transferencia: sale del origen, y lo que sale queda en tránsito.</summary>
    /// <remarks>
    /// <b>Todo entra por parámetro, como al confirmar un ajuste</b>: el número, que lo toma el caso de
    /// uso con cerrojo; la valoración, contra los saldos que acaba de bloquear; y los lotes y las
    /// series, con su fila. No hay forma de enviar sin ninguno de los tres.
    /// </remarks>
    /// <param name="numero">El correlativo que la serie acaba de dar, en esta misma transacción.</param>
    /// <param name="evento">Lo que se cuenta del envío.</param>
    /// <param name="valoracion">
    /// La valoración de <see cref="LineasAValorarEnElOrigen"/>: una por línea, en su orden.
    /// </param>
    /// <param name="resueltos">La fila de cada lote y de cada serie que nombra el documento.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La salida del origen, una fila por línea, y el tránsito del destino.</returns>
    public LoQueMueveLaTransferencia Enviar(
        long numero,
        EventoDeIntegracion evento,
        IReadOnlyList<LineaValorada> valoracion,
        LotesYSeriesResueltos resueltos,
        DateTimeOffset momento)
    {
        if (AnulaAId is not null)
        {
            throw new InvalidOperationException(
                "Un inverso no se envía: se confirma contra su original (ADR-0053 §5).");
        }

        ExigirLineasYNumero(numero);
        ExigirUnaPorLinea(valoracion, nameof(valoracion));
        ExigirLosResueltos(resueltos);

        Transitar(EstadoDeTransferencia.Borrador, EstadoDeTransferencia.Enviada, evento);
        Numero = numero;

        IReadOnlyList<LineaDeTransferencia> lineas = Lineas;
        List<MovimientoStock> movimientos = new(lineas.Count);
        List<MovimientoEnTransito> transito = new(lineas.Count);

        for (int indice = 0; indice < lineas.Count; indice++)
        {
            LineaDeTransferencia linea = lineas[indice];
            LineaValorada valorada = valoracion[indice];

            // La salida resta valor, y lo que viaja es lo que resta.
            linea.AnotarValor(-valorada.Valor.Cantidad);

            movimientos.Add(PataDelOrigen(linea, FechaDeEnvio, valorada.PrecioMedio, resueltos, momento));
            transito.Add(TransitoDe(linea, 1m, resueltos));
        }

        return new LoQueMueveLaTransferencia(movimientos, transito);
    }

    /// <summary>Recibe la transferencia entera: sale del tránsito y entra en el destino.</summary>
    /// <param name="fechaDeRecepcion">El día en que llega. No anterior al envío.</param>
    /// <param name="evento">Lo que se cuenta de la recepción.</param>
    /// <param name="valoracion">
    /// La valoración de <see cref="LineasAValorarEnElDestino"/>: una por línea, en su orden.
    /// </param>
    /// <param name="resueltos">La fila de cada lote y de cada serie que nombra el documento.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La entrada en el destino, una fila por línea, y el tránsito que se descuenta.</returns>
    public LoQueMueveLaTransferencia Recibir(
        DateOnly fechaDeRecepcion,
        EventoDeIntegracion evento,
        IReadOnlyList<LineaValorada> valoracion,
        LotesYSeriesResueltos resueltos,
        DateTimeOffset momento)
    {
        // EL ESTADO VA PRIMERO: en borrador la línea no tiene valor, y la comparación de abajo diría
        // «otro valor» de lo que en realidad es «todavía no ha salido».
        if (Estado != EstadoDeTransferencia.Enviada)
        {
            throw new InvalidOperationException(
                $"Una transferencia en estado «{Estado}» no se recibe: solo llega lo que se envió y " +
                "sigue en vuelo (R1).");
        }

        if (fechaDeRecepcion < FechaDeEnvio)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fechaDeRecepcion),
                fechaDeRecepcion,
                $"La recepción no puede ir antes que el envío, el {FechaDeEnvio:yyyy-MM-dd} (ADR-0053 §3).");
        }

        ExigirUnaPorLinea(valoracion, nameof(valoracion));
        ExigirLosResueltos(resueltos);

        IReadOnlyList<LineaDeTransferencia> lineas = Lineas;

        // EL VALOR VIAJA ENTERO (ADR-0053 §2): una valoración que meta en el destino otra cosa que lo
        // que salió del origen crearía o destruiría valor por el camino.
        if (lineas.Where((linea, indice) => valoracion[indice].Valor.Cantidad != linea.Valor).Any())
        {
            throw new ArgumentException(
                "La valoración del destino no mete el valor que salió del origen: la recepción entra " +
                "con el importe de la línea, entero (ADR-0053 §2).",
                nameof(valoracion));
        }

        Transitar(EstadoDeTransferencia.Enviada, EstadoDeTransferencia.Recibida, evento);
        FechaDeRecepcion = fechaDeRecepcion;

        List<MovimientoStock> movimientos = new(lineas.Count);
        List<MovimientoEnTransito> transito = new(lineas.Count);

        for (int indice = 0; indice < lineas.Count; indice++)
        {
            LineaDeTransferencia linea = lineas[indice];

            movimientos.Add(PataDelDestino(
                linea, fechaDeRecepcion, valoracion[indice].PrecioMedio, resueltos, momento));
            transito.Add(TransitoDe(linea, -1m, resueltos));
        }

        return new LoQueMueveLaTransferencia(movimientos, transito);
    }

    /// <summary>
    /// Construye el inverso que compensará a esta transferencia: mismo origen, destino, serie y
    /// divisa, las mismas líneas con la cantidad negada, y todavía en borrador (ADR-0053 §5).
    /// </summary>
    /// <param name="hoy">El día de la anulación, que es el de sus dos fechas.</param>
    /// <param name="motivo">Por qué se anula.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>El inverso en borrador, con sus líneas y sin número.</returns>
    public Transferencia CrearInverso(DateOnly hoy, string motivo, DateTimeOffset momento)
    {
        if (Estado is not (EstadoDeTransferencia.Enviada or EstadoDeTransferencia.Recibida))
        {
            throw new InvalidOperationException(
                $"Una transferencia en estado «{Estado}» no se anula: un borrador se tira y una " +
                "anulada ya tiene su inverso (R2).");
        }

        if (AnulaAId is not null)
        {
            throw new InvalidOperationException(
                "Un inverso no se anula: deshacerlo sería volver a hacer la transferencia, y eso es " +
                "otra transferencia (R2).");
        }

        if (hoy < (FechaDeRecepcion ?? FechaDeEnvio))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hoy),
                hoy,
                "El inverso no puede ir antes que lo que compensa.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException(
                "Una anulación sin motivo escrito es un movimiento sin explicación.", nameof(motivo));
        }

        string limpio = motivo.Trim();

        if (limpio.Length > LargoDelMotivo)
        {
            throw new ArgumentException(
                $"El motivo no puede pasar de {LargoDelMotivo} caracteres.", nameof(motivo));
        }

        var inverso = new Transferencia(
            Guid.CreateVersion7(),
            EmpresaId,
            SerieId,
            AlmacenOrigenId,
            AlmacenDestinoId,
            hoy,
            Divisa,
            momento)
        {
            Motivo = limpio,
            AnulaAId = Id,
        };

        foreach (LineaDeTransferencia linea in Lineas)
        {
            inverso._lineas.Add(linea.Compensada(inverso.Id, momento));
        }

        return inverso;
    }

    /// <summary>
    /// Confirma el inverso contra su original: niega cada pata que el original escribió, en una sola
    /// transacción, y queda <see cref="EstadoDeTransferencia.Recibida"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>De una enviada</b>, la salida del origen vuelve al origen con el valor exacto que salió, y
    /// el tránsito del destino baja lo mismo, hasta cero.
    /// </para>
    /// <para>
    /// <b>De una recibida</b>, las dos patas. La salida del destino lleva el valor que entró, y como
    /// mucho el que queda; la entrada en el origen lleva lo que esa salida se llevó (ADR-0053 §6).
    /// </para>
    /// </remarks>
    /// <param name="original">La transferencia que compensa, enviada o recibida.</param>
    /// <param name="numero">El correlativo que la serie del original acaba de dar.</param>
    /// <param name="evento">Lo que se cuenta de la confirmación del inverso.</param>
    /// <param name="valoracionEnElDestino">
    /// De una recibida, la valoración de <see cref="LineasAValorarEnElDestino"/>; de una enviada, <c>null</c>.
    /// </param>
    /// <param name="valoracionEnElOrigen">La valoración de <see cref="LineasAValorarEnElOrigen"/>.</param>
    /// <param name="resueltos">La fila de cada lote y de cada serie que nombra el documento.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>Las filas del libro y el tránsito que mueve.</returns>
    public LoQueMueveLaTransferencia ConfirmarComoInverso(
        Transferencia original,
        long numero,
        EventoDeIntegracion evento,
        IReadOnlyList<LineaValorada>? valoracionEnElDestino,
        IReadOnlyList<LineaValorada> valoracionEnElOrigen,
        LotesYSeriesResueltos resueltos,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (AnulaAId != original.Id)
        {
            throw new InvalidOperationException(
                $"Este inverso compensa a «{AnulaAId}», y llega «{original.Id}» (R2).");
        }

        bool niegaLaRecepcion = original.Estado switch
        {
            EstadoDeTransferencia.Enviada => false,
            EstadoDeTransferencia.Recibida => true,
            _ => throw new InvalidOperationException(
                $"El original está en estado «{original.Estado}», y solo se compensa una transferencia " +
                "enviada o recibida (R2)."),
        };

        if (niegaLaRecepcion != valoracionEnElDestino is not null)
        {
            throw new ArgumentException(
                "La valoración del destino va si y solo si el original llegó a recibirse: solo entonces " +
                "hay una entrada en el destino que negar (ADR-0053 §5).",
                nameof(valoracionEnElDestino));
        }

        ExigirLineasYNumero(numero);
        ExigirUnaPorLinea(valoracionEnElOrigen, nameof(valoracionEnElOrigen));
        ExigirLosResueltos(resueltos);

        IReadOnlyList<LineaDeTransferencia> lineas = Lineas;

        if (valoracionEnElDestino is not null)
        {
            ExigirUnaPorLinea(valoracionEnElDestino, nameof(valoracionEnElDestino));
        }

        // LO QUE VUELVE AL ORIGEN ES LO QUE VIAJÓ DE VUELTA, ni un céntimo más (ADR-0053 §6): de una
        // recibida, lo que salió del destino; de una enviada, el valor que compensa.
        for (int indice = 0; indice < lineas.Count; indice++)
        {
            decimal vuelve = valoracionEnElDestino is { } destino
                ? -destino[indice].Valor.Cantidad
                : -lineas[indice].ValorQueCompensa!.Value;

            if (valoracionEnElOrigen[indice].Valor.Cantidad != vuelve)
            {
                throw new ArgumentException(
                    $"La línea {lineas[indice].Numero} devuelve al origen {valoracionEnElOrigen[indice].Valor.Cantidad} " +
                    $"y lo que viajó de vuelta es {vuelve} (ADR-0053 §6).",
                    nameof(valoracionEnElOrigen));
            }
        }

        Transitar(EstadoDeTransferencia.Borrador, EstadoDeTransferencia.Recibida, evento);
        Numero = numero;
        FechaDeRecepcion = FechaDeEnvio;

        List<MovimientoStock> movimientos = new(lineas.Count * 2);
        List<MovimientoEnTransito> transito = [];

        for (int indice = 0; indice < lineas.Count; indice++)
        {
            LineaDeTransferencia linea = lineas[indice];

            // El valor de la línea es el que viajó en el sentido del documento: negativo en un inverso.
            linea.AnotarValor(-valoracionEnElOrigen[indice].Valor.Cantidad);

            if (valoracionEnElDestino is { } destino)
            {
                movimientos.Add(PataDelDestino(linea, FechaDeEnvio, destino[indice].PrecioMedio, resueltos, momento));
            }
            else
            {
                transito.Add(TransitoDe(linea, 1m, resueltos));
            }

            movimientos.Add(PataDelOrigen(
                linea, FechaDeEnvio, valoracionEnElOrigen[indice].PrecioMedio, resueltos, momento));
        }

        return new LoQueMueveLaTransferencia(movimientos, transito);
    }

    /// <summary>Da por anulada la transferencia, contra el inverso que ya la compensa (R2).</summary>
    /// <param name="inverso">El documento que la compensa, ya confirmado.</param>
    /// <param name="evento">Lo que se cuenta de la anulación.</param>
    public void Anular(Transferencia inverso, EventoDeIntegracion evento)
    {
        ArgumentNullException.ThrowIfNull(inverso);

        if (inverso.AnulaAId != Id)
        {
            throw new InvalidOperationException(
                "El inverso con el que se anula no compensa a esta transferencia: apunta a " +
                $"«{inverso.AnulaAId}» y esto es «{Id}» (R2).");
        }

        if (inverso.Estado != EstadoDeTransferencia.Recibida)
        {
            throw new InvalidOperationException(
                $"El inverso está en estado «{inverso.Estado}»: una transferencia no se da por anulada " +
                "contra un documento que todavía no ha movido el libro (R2, R3).");
        }

        EstadoDeTransferencia desde = Estado is EstadoDeTransferencia.Recibida
            ? EstadoDeTransferencia.Recibida
            : EstadoDeTransferencia.Enviada;

        Transitar(desde, EstadoDeTransferencia.Anulada, evento);
    }

    private MovimientoStock PataDelOrigen(
        LineaDeTransferencia linea,
        DateOnly fecha,
        PrecioUnitario precioMedio,
        LotesYSeriesResueltos resueltos,
        DateTimeOffset momento) =>
        MovimientoStock.Registrar(
            EmpresaId,
            fecha,
            AlmacenOrigenId,
            linea.UbicacionOrigenId,
            linea.ArticuloId,
            LoteDe(linea, resueltos),
            NumeroDeSerieDe(linea, resueltos),
            -linea.CantidadIntroducida,
            linea.UnidadIntroducidaId,
            linea.FactorAUnidadBase,
            Divisa,
            costeUnitario: null,
            Importe.De(-linea.Valor!.Value, Divisa),
            precioMedio,
            TipoDeDocumentoOrigen.Transferencia,
            Id,
            momento);

    private MovimientoStock PataDelDestino(
        LineaDeTransferencia linea,
        DateOnly fecha,
        PrecioUnitario precioMedio,
        LotesYSeriesResueltos resueltos,
        DateTimeOffset momento) =>
        MovimientoStock.Registrar(
            EmpresaId,
            fecha,
            AlmacenDestinoId,
            linea.UbicacionDestinoId,
            linea.ArticuloId,
            LoteDe(linea, resueltos),
            NumeroDeSerieDe(linea, resueltos),
            linea.CantidadIntroducida,
            linea.UnidadIntroducidaId,
            linea.FactorAUnidadBase,
            Divisa,
            costeUnitario: null,
            Importe.De(linea.Valor!.Value, Divisa),
            precioMedio,
            TipoDeDocumentoOrigen.Transferencia,
            Id,
            momento);

    private MovimientoEnTransito TransitoDe(
        LineaDeTransferencia linea, decimal signo, LotesYSeriesResueltos resueltos) =>
        new(
            linea.ArticuloId,
            AlmacenDestinoId,
            linea.UbicacionDestinoId,
            LoteDe(linea, resueltos),
            NumeroDeSerieDe(linea, resueltos),
            signo * linea.CantidadEnUnidadBase,
            Importe.De(signo * linea.Valor!.Value, Divisa));

    private static Guid? LoteDe(LineaDeTransferencia linea, LotesYSeriesResueltos resueltos) =>
        linea.CodigoDeLote is { } lote ? resueltos.Lotes[new(linea.ArticuloId, lote)] : null;

    private static Guid? NumeroDeSerieDe(LineaDeTransferencia linea, LotesYSeriesResueltos resueltos) =>
        linea.NumeroDeSerie is { } serie ? resueltos.Series[new(linea.ArticuloId, serie)] : null;

    private void ExigirLineasYNumero(long numero)
    {
        if (_lineas.Count == 0)
        {
            throw new InvalidOperationException(
                "Una transferencia sin líneas no mueve el libro, y un documento que no mueve nada " +
                "rompe la vuelta de la R13.");
        }

        if (numero <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numero),
                numero,
                "Los correlativos de una serie empiezan en uno: este no salió de la numeración.");
        }
    }

    private void ExigirUnaPorLinea(IReadOnlyList<LineaValorada> valoracion, string parametro)
    {
        ArgumentNullException.ThrowIfNull(valoracion, parametro);

        if (valoracion.Count != _lineas.Count
            || valoracion.Any(valorada => valorada is null
                || valorada.Valor.Divisa != Divisa
                || valorada.PrecioMedio.Divisa != Divisa))
        {
            throw new ArgumentException(
                $"El documento tiene {_lineas.Count} líneas en {Divisa}, y la valoración trae " +
                $"{valoracion.Count}: hace falta una por línea, en la divisa del documento (ADR-0046 §10).",
                parametro);
        }
    }

    private void ExigirLosResueltos(LotesYSeriesResueltos resueltos)
    {
        ArgumentNullException.ThrowIfNull(resueltos);

        if (LotesQueNombra().Any(lote => !resueltos.Lotes.ContainsKey(lote))
            || SeriesQueNombra().Any(serie => !resueltos.Series.ContainsKey(serie)))
        {
            throw new ArgumentException(
                "El documento nombra lotes o series que no llegan resueltos: cada código tiene que traer " +
                "su fila (ADR-0048 §2).",
                nameof(resueltos));
        }
    }

    private List<CodigoDeUnArticulo> Nombrados(Func<LineaDeTransferencia, string?> codigoDe) =>
    [
        .. _lineas
            .Select(linea => (linea.ArticuloId, Codigo: codigoDe(linea)))
            .Where(nombrado => nombrado.Codigo is not null)
            .Select(nombrado => new CodigoDeUnArticulo(nombrado.ArticuloId, nombrado.Codigo!))
            .Distinct()
            .OrderBy(nombrado => nombrado.ArticuloId)
            .ThenBy(nombrado => nombrado.Codigo, StringComparer.Ordinal),
    ];
}
