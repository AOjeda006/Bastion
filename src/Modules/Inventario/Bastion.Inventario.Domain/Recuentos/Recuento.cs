using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Documentos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Domain.Multiempresa;
using Bastion.Inventario.Domain.Ajustes;

namespace Bastion.Inventario.Domain.Recuentos;

/// <summary>
/// El recuento de un almacén entero: lo que una persona cuenta, contra lo que el libro dice que hay
/// (ADR-0055).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el primer documento del inventario que no escribe en el libro.</b> La diferencia entre lo
/// contado y el teórico la mueve un ajuste, que el recuento abre al confirmarse y que se confirma en
/// la misma transacción. Por eso <see cref="Confirmar"/> no devuelve movimientos: los devuelve el
/// ajuste.
/// </para>
/// <para>
/// <b>El teórico no vive aquí.</b> Es el físico de cada clave en el instante en que se pregunta, y lo
/// lee el caso de uso: sin cerrojo para la ficha, y con las valoraciones bloqueadas para confirmar
/// (ADR-0055 §3). Entra por parámetro, como la valoración en el ajuste, y el dominio exige que traiga
/// el de todas sus líneas.
/// </para>
/// </remarks>
public sealed class Recuento : DocumentoBase<EstadoDeRecuento>, IDeInquilino
{
    /// <summary>Longitud máxima de los motivos: el del alta es el de su ajuste, y tiene que caber en él.</summary>
    public const int LargoDelMotivo = Ajuste.LargoDelMotivo;

    private readonly List<LineaDeRecuento> _lineas = [];

    private Recuento(
        Guid id,
        Guid empresaId,
        Guid serieId,
        Guid serieDelAjusteId,
        Guid almacenId,
        DateOnly fechaDeApertura,
        string motivo,
        string divisa,
        DateTimeOffset momento)
        : base(EstadoDeRecuento.EnCurso, momento)
    {
        Id = id;
        EmpresaId = empresaId;
        SerieId = serieId;
        SerieDelAjusteId = serieDelAjusteId;
        AlmacenId = almacenId;
        FechaDeApertura = fechaDeApertura;
        Motivo = motivo;
        Divisa = divisa;
    }

    /// <summary>Constructor de materialización para EF Core.</summary>
    private Recuento()
    {
    }

    /// <summary>Identificador del recuento.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>La serie que lo numerará al confirmarse, de las de recuentos.</summary>
    /// <remarks>
    /// Se elige en el alta y se valida dos veces: al abrir, para que nadie cuente horas contra una
    /// serie que no sirve, y al numerar, que es la guarda (ADR-0055 §1.3).
    /// </remarks>
    public Guid SerieId { get; private set; }

    /// <summary>La serie que numerará su ajuste, si lo hay, de las de ajustes.</summary>
    public Guid SerieDelAjusteId { get; private set; }

    /// <summary>El correlativo que le dio la serie al confirmarse, o <c>null</c> mientras no lo esté.</summary>
    /// <remarks>
    /// Se numera al confirmar (ADR-0055 §1.4): un recuento descartado no deja hueco. Mientras está en
    /// curso, la pantalla lo nombra por su almacén y su fecha de apertura, y no hay otro igual, porque
    /// en un almacén solo hay uno en curso.
    /// </remarks>
    public long? Numero { get; private set; }

    /// <summary>El almacén que se cuenta, entero.</summary>
    public Guid AlmacenId { get; private set; }

    /// <summary>El día UTC del alta.</summary>
    public DateOnly FechaDeApertura { get; private set; }

    /// <summary>El día de la confirmación, que es el de su ajuste, o <c>null</c> si no se ha confirmado.</summary>
    /// <remarks>
    /// <b>Es la que cuenta para el ejercicio</b>: uno en curso no cuenta (ADR-0055 §1.5), y uno
    /// confirmado o anulado cuenta por esta fecha, como documento.
    /// </remarks>
    public DateOnly? FechaDeConfirmacion { get; private set; }

    /// <summary>Por qué se cuenta. Es el motivo de su ajuste.</summary>
    public string Motivo { get; private set; } = string.Empty;

    /// <summary>La divisa base de la empresa al abrirlo: la del coste de sus líneas y la de su ajuste.</summary>
    public string Divisa { get; private set; } = string.Empty;

    /// <summary>Por qué se descartó, o <c>null</c> si no se descartó.</summary>
    public string? MotivoDelDescarte { get; private set; }

    /// <summary>Por qué se anuló, o <c>null</c> si no se anuló.</summary>
    /// <remarks>
    /// Va aquí aunque el inverso de su ajuste lleve el mismo: un recuento que no movió el libro no
    /// tiene ajuste, y su anulación se quedaría sin explicar.
    /// </remarks>
    public string? MotivoDeLaAnulacion { get; private set; }

    /// <summary>Las líneas, por su número.</summary>
    public IReadOnlyList<LineaDeRecuento> Lineas => [.. _lineas.OrderBy(linea => linea.Numero)];

    /// <summary>Si su confirmación movió el libro, es decir, si tiene ajuste.</summary>
    public bool MovioElLibro => _lineas.Any(linea => linea.LineaDeAjusteId is not null);

    /// <summary>Abre un recuento en curso, con las claves del almacén precargadas y sin contar.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8).</param>
    /// <param name="serieId">La serie que lo numerará.</param>
    /// <param name="serieDelAjusteId">La serie que numerará su ajuste.</param>
    /// <param name="almacenId">El almacén que se cuenta.</param>
    /// <param name="fechaDeApertura">El día UTC del alta.</param>
    /// <param name="motivo">Por qué se cuenta.</param>
    /// <param name="divisa">La divisa base de la empresa.</param>
    /// <param name="precarga">Las claves del almacén con físico mayor que cero, en cualquier orden.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>El recuento en curso.</returns>
    public static Recuento Abrir(
        Guid empresaId,
        Guid serieId,
        Guid serieDelAjusteId,
        Guid almacenId,
        DateOnly fechaDeApertura,
        string motivo,
        string divisa,
        IReadOnlyCollection<LineaAPrecargar> precarga,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(precarga);

        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Un recuento sin empresa no existe (R8).", nameof(empresaId));
        }

        if (serieId == Guid.Empty || serieDelAjusteId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un recuento lleva sus dos series desde el alta: la suya y la de su ajuste (ADR-0055 §1.3).",
                serieId == Guid.Empty ? nameof(serieId) : nameof(serieDelAjusteId));
        }

        if (almacenId == Guid.Empty)
        {
            throw new ArgumentException("Un recuento cuenta un almacén.", nameof(almacenId));
        }

        var recuento = new Recuento(
            Guid.CreateVersion7(),
            empresaId,
            serieId,
            serieDelAjusteId,
            almacenId,
            fechaDeApertura,
            LeerMotivo(motivo, nameof(motivo)),
            CatalogoDeDivisas.Normalizar(divisa),
            momento);

        // EL NÚMERO LO REPARTE LA PRECARGA, no quien la entrega: por ubicación, artículo, lote y
        // serie, así que dos altas del mismo almacén numeran igual.
        IEnumerable<LineaAPrecargar> enOrden = precarga
            .OrderBy(linea => linea.Clave.UbicacionId)
            .ThenBy(linea => linea.Clave.ArticuloId)
            .ThenBy(linea => linea.Clave.CodigoDeLote, StringComparer.Ordinal)
            .ThenBy(linea => linea.Clave.NumeroDeSerie, StringComparer.Ordinal);

        foreach (LineaAPrecargar linea in enOrden)
        {
            if (recuento.LlevaLaClave(linea.Clave) || recuento.LlevaLaSerie(linea.Clave))
            {
                throw new ArgumentException(
                    "La precarga trae la misma clave dos veces: cada clave del almacén es una fila de " +
                    "sus existencias, y una sola línea.",
                    nameof(precarga));
            }

            recuento._lineas.Add(LineaDeRecuento.Crear(
                recuento.Id,
                recuento._lineas.Count + 1,
                linea.Clave,
                linea.UnidadBaseId,
                OrigenDeLaLinea.Precargada,
                costeUnitario: null,
                momento));
        }

        return recuento;
    }

    /// <summary>Si lleva ya una línea con esa clave.</summary>
    /// <param name="clave">La clave.</param>
    /// <returns>Si la lleva.</returns>
    public bool LlevaLaClave(ClaveDelRecuento clave) => _lineas.Any(linea => linea.Clave == clave);

    /// <summary>Si lleva ya ese número de serie de ese artículo, en esa ubicación o en otra.</summary>
    /// <remarks>
    /// <b>Una serie va una sola vez por recuento</b>, porque va una sola vez por ajuste (ADR-0048 §3):
    /// en dos ubicaciones, el ajuste la sacaría de una y la metería en otra, y eso es la reubicación.
    /// </remarks>
    /// <param name="clave">La clave, con su número de serie.</param>
    /// <returns>Si la lleva. Una clave sin serie no lleva ninguna.</returns>
    public bool LlevaLaSerie(ClaveDelRecuento clave)
    {
        ArgumentNullException.ThrowIfNull(clave);

        return clave.NumeroDeSerie is { } serie
            && _lineas.Any(linea => linea.ArticuloId == clave.ArticuloId
                && string.Equals(linea.NumeroDeSerie, serie, StringComparison.Ordinal));
    }

    /// <summary>Añade una clave que la precarga no traía, sin contar.</summary>
    /// <param name="clave">La clave.</param>
    /// <param name="unidadBaseId">La unidad base de su artículo.</param>
    /// <param name="costeUnitario">El coste de una unidad base, o <c>null</c>.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>La línea nueva.</returns>
    public LineaDeRecuento AnadirLinea(
        ClaveDelRecuento clave,
        Guid unidadBaseId,
        decimal? costeUnitario,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(clave);
        ExigirEnCurso("no admite claves nuevas");

        if (LlevaLaClave(clave) || LlevaLaSerie(clave))
        {
            throw new InvalidOperationException(
                "El recuento ya lleva esa clave, o ese número de serie en otra ubicación: se contaría " +
                "dos veces (ADR-0055 §13, ADR-0048 §3).");
        }

        // EL MAYOR MÁS UNO, y no cuántas hay más uno: con una línea quitada, contar daría un número
        // que ya tiene otra.
        int numero = _lineas.Count == 0 ? 1 : _lineas.Max(linea => linea.Numero) + 1;

        var nueva = LineaDeRecuento.Crear(
            Id, numero, clave, unidadBaseId, OrigenDeLaLinea.Anadida, costeUnitario, momento);

        _lineas.Add(nueva);

        return nueva;
    }

    /// <summary>Quita una línea, y su clave queda como está: el recuento no dice nada de ella (ADR-0055 §5).</summary>
    /// <param name="lineaId">La línea.</param>
    public void QuitarLinea(Guid lineaId)
    {
        ExigirEnCurso("no admite que se quiten líneas");

        _ = _lineas.Remove(Linea(lineaId));
    }

    /// <summary>Anota lo contado en una línea, y el teórico que había al contarlo.</summary>
    /// <param name="lineaId">La línea.</param>
    /// <param name="contado">Lo contado, en unidad base.</param>
    /// <param name="teoricoAhora">El físico de su clave en este instante.</param>
    public void Contar(Guid lineaId, decimal contado, decimal teoricoAhora)
    {
        ExigirEnCurso("no admite que se cuente");

        Linea(lineaId).Contar(contado, teoricoAhora);
    }

    /// <summary>Las líneas que nadie ha contado todavía, por su número.</summary>
    /// <returns>Las líneas sin contar.</returns>
    public IReadOnlyList<LineaDeRecuento> LineasSinContar() => [.. Lineas.Where(linea => !linea.EstaContada)];

    /// <summary>La huella del teórico de todas sus líneas (ADR-0055 §2).</summary>
    /// <param name="teoricos">El teórico de cada línea, de todas y de ninguna más.</param>
    /// <returns>La huella.</returns>
    public string HuellaDelTeorico(IReadOnlyDictionary<Guid, decimal> teoricos)
    {
        ExigirElTeoricoDeTodas(teoricos);

        return Recuentos.HuellaDelTeorico.De(teoricos);
    }

    /// <summary>Las líneas contadas cuyo teórico ya no es el de cuando se contaron.</summary>
    /// <param name="teoricos">El teórico de cada línea ahora.</param>
    /// <returns>Esas líneas, por su número.</returns>
    public IReadOnlyList<LineaDeRecuento> LineasConElTeoricoCambiado(IReadOnlyDictionary<Guid, decimal> teoricos)
    {
        ExigirElTeoricoDeTodas(teoricos);

        return [.. Lineas.Where(linea => linea.TeoricoAlContar is { } entonces && entonces != teoricos[linea.Id])];
    }

    /// <summary>Las líneas contadas por encima de su teórico: las que el ajuste haría subir.</summary>
    /// <param name="teoricos">El teórico de cada línea ahora.</param>
    /// <returns>Esas líneas, por su número.</returns>
    public IReadOnlyList<LineaDeRecuento> LineasQueSuben(IReadOnlyDictionary<Guid, decimal> teoricos)
    {
        ExigirElTeoricoDeTodas(teoricos);

        return [.. Lineas.Where(linea => linea.Contado is { } contado && contado > teoricos[linea.Id])];
    }

    /// <summary>
    /// El ajuste que movería la diferencia entre lo contado y el teórico, en borrador, o <c>null</c>
    /// si todo cuadra (ADR-0055 §8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Una línea por cada línea que difiere, en el orden del recuento</b>: la cantidad es lo
    /// contado menos el teórico, en la unidad base y con factor uno. La línea añadida que sube lleva
    /// su coste, y todas las demás entran o salen al precio medio de su clave.
    /// </para>
    /// <para>
    /// <b>No cambia el recuento.</b> Lo confirma el caso de uso, que es quien valora, y después
    /// <see cref="Confirmar"/> comprueba que el ajuste es justo esta diferencia.
    /// </para>
    /// </remarks>
    /// <param name="teoricos">El teórico de cada línea, leído con las valoraciones bloqueadas.</param>
    /// <param name="fechaDeConfirmacion">El día de la confirmación, que es el del ajuste.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>El ajuste en borrador, o <c>null</c>.</returns>
    public Ajuste? AjusteDeLaDiferencia(
        IReadOnlyDictionary<Guid, decimal> teoricos,
        DateOnly fechaDeConfirmacion,
        DateTimeOffset momento)
    {
        ExigirEnCurso("no genera ajuste");
        ExigirTodasContadas();
        ExigirElTeoricoDeTodas(teoricos);

        List<(LineaDeRecuento Linea, decimal Diferencia)> difieren = Diferencias(teoricos);

        if (difieren.Count == 0)
        {
            return null;
        }

        var ajuste = Ajuste.AbrirParaUnRecuento(
            Id, EmpresaId, SerieDelAjusteId, AlmacenId, fechaDeConfirmacion, Motivo, Divisa, momento);

        foreach ((LineaDeRecuento linea, decimal diferencia) in difieren)
        {
            ajuste.AnadirLinea(
                linea.UbicacionId,
                linea.ArticuloId,
                diferencia,
                linea.UnidadBaseId,
                factorAUnidadBase: 1m,
                costeUnitario: diferencia > 0m ? linea.CosteUnitario : null,
                momento,
                linea.CodigoDeLote,
                linea.NumeroDeSerie);
        }

        return ajuste;
    }

    /// <summary>Confirma el recuento, con el ajuste de su diferencia ya confirmado.</summary>
    /// <remarks>
    /// <b>Comprueba que el ajuste es justo la diferencia</b> de este teórico —ni una línea de más ni
    /// una de menos, cada una con su cantidad—, que es suyo y que ya movió el libro. Y lo comprueba
    /// antes de transitar, para que un ajuste que no casa no deje el recuento a medias.
    /// </remarks>
    /// <param name="numero">El correlativo que acaba de dar su serie.</param>
    /// <param name="fechaDeConfirmacion">El día de la confirmación.</param>
    /// <param name="teoricos">El teórico con el que se generó el ajuste.</param>
    /// <param name="ajuste">El ajuste de la diferencia, confirmado, o <c>null</c> si todo cuadra.</param>
    /// <param name="evento">Lo que se cuenta de la confirmación.</param>
    public void Confirmar(
        long numero,
        DateOnly fechaDeConfirmacion,
        IReadOnlyDictionary<Guid, decimal> teoricos,
        Ajuste? ajuste,
        EventoDeIntegracion evento)
    {
        ExigirEnCurso("no se confirma");
        ExigirTodasContadas();
        ExigirElTeoricoDeTodas(teoricos);

        if (numero <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numero), numero, "Los correlativos de una serie empiezan en uno.");
        }

        if (fechaDeConfirmacion < FechaDeApertura)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fechaDeConfirmacion),
                fechaDeConfirmacion,
                $"El recuento se abrió el {FechaDeApertura:yyyy-MM-dd}, y no se confirma antes de abrirse.");
        }

        List<(LineaDeRecuento Linea, decimal Diferencia)> difieren = Diferencias(teoricos);
        List<(LineaDeRecuento Linea, Guid LineaDeAjusteId)> pares = Emparejar(difieren, ajuste, fechaDeConfirmacion);

        Transitar(EstadoDeRecuento.EnCurso, EstadoDeRecuento.Confirmado, evento);

        Numero = numero;
        FechaDeConfirmacion = fechaDeConfirmacion;

        foreach ((LineaDeRecuento linea, Guid lineaDeAjusteId) in pares)
        {
            linea.AnotarLineaDeAjuste(lineaDeAjusteId);
        }
    }

    /// <summary>El inverso de su ajuste, para anularlo con el recuento (ADR-0055 §9).</summary>
    /// <param name="ajuste">Su ajuste, confirmado.</param>
    /// <param name="fechaDeOperacion">El día al que se imputa el inverso.</param>
    /// <param name="motivo">Por qué se anula.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>El inverso en borrador.</returns>
    public Ajuste CrearInversoDeSuAjuste(Ajuste ajuste, DateOnly fechaDeOperacion, string motivo, DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(ajuste);

        if (Estado != EstadoDeRecuento.Confirmado)
        {
            throw new InvalidOperationException(
                $"Un recuento en estado «{Estado}» no tiene ajuste que anular: solo lo tiene uno confirmado.");
        }

        if (ajuste.RecuentoId != Id)
        {
            throw new InvalidOperationException(
                $"El ajuste {ajuste.Id} no es el de este recuento: apunta a «{ajuste.RecuentoId}» y esto es «{Id}».");
        }

        return ajuste.CrearInversoDeSuRecuento(fechaDeOperacion, motivo, momento);
    }

    /// <summary>Anula el recuento, con su ajuste ya anulado si lo tenía (ADR-0055 §9).</summary>
    /// <param name="motivo">Por qué se anula.</param>
    /// <param name="ajusteAnulado">Su ajuste, ya anulado, o <c>null</c> si no movió el libro.</param>
    /// <param name="evento">Lo que se cuenta de la anulación.</param>
    public void Anular(string motivo, Ajuste? ajusteAnulado, EventoDeIntegracion evento)
    {
        string limpio = LeerMotivo(motivo, nameof(motivo));

        if (Estado != EstadoDeRecuento.Confirmado)
        {
            throw new InvalidOperationException(
                $"Un recuento en estado «{Estado}» no se anula: uno en curso se descarta, y uno anulado " +
                "o descartado ya está cerrado.");
        }

        if (MovioElLibro
            ? ajusteAnulado is null || ajusteAnulado.RecuentoId != Id || ajusteAnulado.Estado != EstadoDeAjuste.Anulado
            : ajusteAnulado is not null)
        {
            throw new ArgumentException(
                MovioElLibro
                    ? "El recuento movió el libro, y se anula con su ajuste ya anulado: si no, su " +
                      "diferencia seguiría en el libro (ADR-0055 §9)."
                    : "El recuento no movió el libro, así que no tiene ajuste con el que anularse.",
                nameof(ajusteAnulado));
        }

        Transitar(EstadoDeRecuento.Confirmado, EstadoDeRecuento.Anulado, evento);
        MotivoDeLaAnulacion = limpio;
    }

    /// <summary>Descarta un recuento en curso, con su motivo (ADR-0055 §1.6).</summary>
    /// <param name="motivo">Por qué se descarta.</param>
    /// <param name="evento">Lo que se cuenta del descarte.</param>
    public void Descartar(string motivo, EventoDeIntegracion evento)
    {
        string limpio = LeerMotivo(motivo, nameof(motivo));

        Transitar(EstadoDeRecuento.EnCurso, EstadoDeRecuento.Descartado, evento);
        MotivoDelDescarte = limpio;
    }

    private static string LeerMotivo(string motivo, string parametro)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException(
                "Un recuento lleva su motivo escrito: el del alta es el de su ajuste, y el del " +
                "descarte o la anulación es lo único que queda para entender por qué.",
                parametro);
        }

        string limpio = motivo.Trim();

        return limpio.Length <= LargoDelMotivo
            ? limpio
            : throw new ArgumentException($"El motivo no puede pasar de {LargoDelMotivo} caracteres.", parametro);
    }

    private List<(LineaDeRecuento Linea, decimal Diferencia)> Diferencias(IReadOnlyDictionary<Guid, decimal> teoricos) =>
    [
        .. Lineas
            .Select(linea => (Linea: linea, Diferencia: linea.Contado!.Value - teoricos[linea.Id]))
            .Where(par => par.Diferencia != 0m),
    ];

    private List<(LineaDeRecuento Linea, Guid LineaDeAjusteId)> Emparejar(
        List<(LineaDeRecuento Linea, decimal Diferencia)> difieren,
        Ajuste? ajuste,
        DateOnly fechaDeConfirmacion)
    {
        if (difieren.Count == 0)
        {
            return ajuste is null
                ? []
                : throw new ArgumentException(
                    "Todo cuadra, así que no hay ajuste: uno que no mueve ninguna diferencia no es de este recuento.",
                    nameof(ajuste));
        }

        if (ajuste is null
            || ajuste.RecuentoId != Id
            || ajuste.Estado != EstadoDeAjuste.Confirmado
            || ajuste.FechaDeOperacion != fechaDeConfirmacion
            || ajuste.Lineas.Count != difieren.Count)
        {
            throw new ArgumentException(
                "El recuento tiene diferencias, y se confirma con su ajuste: el suyo, ya confirmado, con " +
                "la fecha de la confirmación y una línea por diferencia (ADR-0055 §8).",
                nameof(ajuste));
        }

        List<(LineaDeRecuento Linea, Guid LineaDeAjusteId)> pares = new(difieren.Count);

        foreach ((LineaDeRecuento linea, decimal diferencia) in difieren)
        {
            LineaDeAjuste? suya = ajuste.Lineas.SingleOrDefault(deAjuste =>
                deAjuste.UbicacionId == linea.UbicacionId
                && deAjuste.ArticuloId == linea.ArticuloId
                && string.Equals(deAjuste.CodigoDeLote, linea.CodigoDeLote, StringComparison.Ordinal)
                && string.Equals(deAjuste.NumeroDeSerie, linea.NumeroDeSerie, StringComparison.Ordinal));

            if (suya is null
                || suya.CantidadIntroducida != diferencia
                || suya.FactorAUnidadBase != 1m
                || suya.UnidadIntroducidaId != linea.UnidadBaseId)
            {
                throw new ArgumentException(
                    $"La línea {linea.Numero} difiere en {diferencia}, y el ajuste no la mueve justo en " +
                    "eso, en la unidad base: no es la diferencia de este teórico.",
                    nameof(ajuste));
            }

            pares.Add((linea, suya.Id));
        }

        return pares;
    }

    private LineaDeRecuento Linea(Guid lineaId) =>
        _lineas.SingleOrDefault(linea => linea.Id == lineaId)
            ?? throw new InvalidOperationException($"La línea {lineaId} no es de este recuento.");

    private void ExigirEnCurso(string queNo)
    {
        if (Estado != EstadoDeRecuento.EnCurso)
        {
            throw new InvalidOperationException($"Un recuento en estado «{Estado}» {queNo}: ya no está en curso.");
        }
    }

    private void ExigirTodasContadas()
    {
        if (_lineas.Any(linea => !linea.EstaContada))
        {
            throw new InvalidOperationException(
                "El recuento tiene líneas sin contar, y una línea sin contar no es un cero: se cuenta o " +
                "se quita (ADR-0055 §5).");
        }
    }

    private void ExigirElTeoricoDeTodas(IReadOnlyDictionary<Guid, decimal> teoricos)
    {
        ArgumentNullException.ThrowIfNull(teoricos);

        if (teoricos.Count != _lineas.Count || _lineas.Any(linea => !teoricos.ContainsKey(linea.Id)))
        {
            throw new ArgumentException(
                $"El recuento tiene {_lineas.Count} líneas y llegan {teoricos.Count} teóricos: hace falta " +
                "el de todas, y el de ninguna más (ADR-0055 §2).",
                nameof(teoricos));
        }
    }
}
