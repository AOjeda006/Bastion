using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Documentos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Domain.Multiempresa;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.Domain.Ajustes;

/// <summary>
/// El primer documento del módulo: una corrección de existencias contra un almacén, con sus
/// líneas y su máquina de estados (R1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un ajuste en borrador no ha movido nada.</b> Se escribe, se corrige y se puede tirar. Lo que
/// mueve el libro es <see cref="Confirmar"/>, y lo mueve en la misma transacción: si confirmar no
/// escribiera las filas en el mismo <c>COMMIT</c>, habría un instante con un ajuste confirmado y
/// el stock sin mover, y la R3 —el libro es la verdad— dejaría de serlo durante ese instante.
/// </para>
/// <para>
/// <b>El estado no se asigna, se transita.</b> <see cref="DocumentoBase{TEstado}.Estado"/> tiene
/// el <c>set</c> privado en otro ensamblado, así que asignarlo desde aquí <b>no compila</b>; el
/// único camino son los dos métodos de abajo, y ninguno de los dos puede ejecutarse sin el evento
/// que lo cuenta.
/// </para>
/// </remarks>
public sealed class Ajuste : DocumentoBase<EstadoDeAjuste>, IDeInquilino
{
    /// <summary>Longitud máxima del motivo.</summary>
    public const int LargoDelMotivo = 300;

    private readonly List<LineaDeAjuste> _lineas = [];

    private Ajuste(
        Guid id,
        Guid empresaId,
        Guid serieId,
        Guid almacenId,
        DateOnly fechaDeOperacion,
        string motivo,
        string divisa,
        DateTimeOffset momento)
        : base(EstadoDeAjuste.Borrador, momento)
    {
        Id = id;
        EmpresaId = empresaId;
        SerieId = serieId;
        AlmacenId = almacenId;
        FechaDeOperacion = fechaDeOperacion;
        Motivo = motivo;
        Divisa = divisa;
    }

    /// <summary>Constructor de materialización para EF Core.</summary>
    private Ajuste()
    {
    }

    /// <summary>Identificador del ajuste. Es el documento origen de sus movimientos (R13).</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid EmpresaId { get; private set; }

    /// <summary>La serie que numerará este documento. Se elige al abrirlo y no se cambia.</summary>
    /// <remarks>
    /// <b>No se comprueba aquí, y no es un olvido.</b> Una serie se puede cerrar mientras el
    /// borrador espera, así que una validación en el alta se quedaría vieja: sería una comodidad,
    /// no una garantía. La única guarda es la que corre <b>en el instante de numerar</b>.
    /// </remarks>
    public Guid SerieId { get; private set; }

    /// <summary>El correlativo que la serie le dio, o <c>null</c> mientras sea un borrador.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nulo significa «todavía no», y por eso no es cero.</b> Un cero sería un número, y un
    /// documento con el número cero no se distingue de uno sin numerar en ninguna consulta ni en
    /// ningún listado. El borrador no ha gastado ninguno: si se tira, no deja hueco.
    /// </para>
    /// <para>
    /// <b>Es el correlativo pelado, no el número compuesto.</b> Componerlo con el código de la
    /// serie y su formato es de quien tenga una factura delante; aquí vive lo único que la R5
    /// exige que sea correlativo y sin huecos.
    /// </para>
    /// </remarks>
    public long? Numero { get; private set; }

    /// <summary>Almacén contra el que se ajusta.</summary>
    public Guid AlmacenId { get; private set; }

    /// <summary>Día de calendario al que se imputa el ajuste (R14).</summary>
    /// <remarks>
    /// Es la fecha que acaba en la clave de partición del libro, así que no es un detalle de
    /// presentación: decide en qué partición caen las filas que este documento escriba.
    /// </remarks>
    public DateOnly FechaDeOperacion { get; private set; }

    /// <summary>Por qué se ajusta, escrito por quien lo hace.</summary>
    public string Motivo { get; private set; } = string.Empty;

    /// <summary>La divisa de todos los importes del documento: la divisa base de la empresa.</summary>
    /// <remarks>
    /// <b>Va en la cabecera y no en cada línea</b>, como en una factura (ADR-0046 §7). Con una por
    /// línea, un documento podría mezclar euros y dólares, y su valor no se sumaría sin un tipo de
    /// cambio. Se pone al abrirlo, con la que la empresa tenga ese día, y no cambia.
    /// </remarks>
    public string Divisa { get; private set; } = string.Empty;

    /// <summary>El ajuste que este documento compensa, o <c>null</c> si no es un inverso.</summary>
    /// <remarks>
    /// <para>
    /// <b>Un solo enlace y no dos, a propósito.</b> La tentación es guardar además en el original
    /// un <c>AnuladoPorId</c> que apunte a su inverso, y entonces el par queda escrito en dos
    /// sitios que pueden contradecirse — el mismo motivo por el que una línea lleva la cantidad
    /// <b>con signo</b> y no una cantidad más un «entrada o salida». Con esta columna y el estado
    /// del original la flecha se recorre igual en los dos sentidos: de aquí al original por el
    /// identificador, y del original a aquí buscando quién le apunta.
    /// </para>
    /// <para>
    /// <b>Lo que el motor sostiene y lo que no.</b> Lleva clave ajena —es la misma tabla y el
    /// mismo esquema, así que no cruza ninguna frontera— y con ella el motor garantiza que apunta
    /// a una fila que existe; y lleva índice <b>único</b> filtrado, con el que además garantiza que
    /// no hay dos inversos apuntando al mismo original. Lo que el motor <b>no</b> puede decir es
    /// que esa fila esté <c>Anulado</c>, ni que un anulado se quede sin nadie que le apunte: las
    /// dos son condiciones sobre el estado de otra fila y no caben en ninguna restricción de
    /// columna. Eso lo sostiene <c>LaDobleFlechaDeLaAnulacionTests</c>, con su barrido en los dos
    /// sentidos.
    /// </para>
    /// </remarks>
    public Guid? AnulaAId { get; private set; }

    /// <summary>Las líneas del documento, en el orden en que se escribieron.</summary>
    /// <remarks>
    /// <b>Por su número, y no por cómo estén en la colección</b>: EF Core la llena en el orden en
    /// que el motor devuelve las filas, y la valoración depende del orden (ADR-0046 §3). Todo lo
    /// que recorre las líneas pasa por aquí.
    /// </remarks>
    public IReadOnlyList<LineaDeAjuste> Lineas => [.. _lineas.OrderBy(linea => linea.Numero)];

    /// <summary>Abre un ajuste en borrador.</summary>
    /// <remarks>
    /// <b>La serie se elige aquí y el número no sale hasta confirmar.</b> Son dos instantes
    /// distintos a propósito: elegir serie es una decisión de quien escribe el documento, y gastar
    /// un correlativo es un hecho que la R5 obliga a que no deje huecos. Si el número saliera al
    /// abrir, cada borrador tirado sería un hueco.
    /// </remarks>
    /// <param name="empresaId">Empresa a la que pertenece (R8).</param>
    /// <param name="serieId">Serie que lo numerará al confirmarse (R5).</param>
    /// <param name="almacenId">Almacén contra el que se ajusta.</param>
    /// <param name="fechaDeOperacion">Día al que se imputa.</param>
    /// <param name="motivo">Por qué se ajusta.</param>
    /// <param name="divisa">La divisa base de la empresa, en la que irán todos los importes.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>El ajuste en borrador, sin líneas ni número.</returns>
    public static Ajuste Abrir(
        Guid empresaId,
        Guid serieId,
        Guid almacenId,
        DateOnly fechaDeOperacion,
        string motivo,
        string divisa,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Un ajuste sin empresa no existe (R8).", nameof(empresaId));
        }

        if (serieId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un ajuste sin serie no se podría numerar al confirmarlo, y un documento " +
                "confirmado sin número es lo que la R5 no permite.",
                nameof(serieId));
        }

        if (almacenId == Guid.Empty)
        {
            throw new ArgumentException("Un ajuste ajusta contra un almacén.", nameof(almacenId));
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException(
                "Un ajuste sin motivo escrito es un descuadre sin explicación: el motivo es lo " +
                "único que queda para entender la corrección dentro de dos años.",
                nameof(motivo));
        }

        string limpio = motivo.Trim();

        if (limpio.Length > LargoDelMotivo)
        {
            throw new ArgumentException(
                $"El motivo no puede pasar de {LargoDelMotivo} caracteres.",
                nameof(motivo));
        }

        return new Ajuste(
            Guid.CreateVersion7(),
            empresaId,
            serieId,
            almacenId,
            fechaDeOperacion,
            limpio,
            CatalogoDeDivisas.Normalizar(divisa),
            momento);
    }

    /// <summary>Añade una línea. Solo en borrador.</summary>
    /// <remarks>
    /// <b>Una serie sale una sola vez por documento</b> (ADR-0048 §3), aunque sea en otra ubicación
    /// y con el signo contrario. El índice que la mantiene en un solo sitio se comprueba fila a fila,
    /// así que un documento que la sacara de una estantería y la metiera en otra chocaría o no según
    /// el orden en que el motor recorriera sus filas. Moverla de sitio es la reubicación, que es otro
    /// documento.
    /// </remarks>
    /// <param name="ubicacionId">Hueco del almacén.</param>
    /// <param name="articuloId">Artículo que se mueve.</param>
    /// <param name="cantidadIntroducida">Cantidad con signo, tal como se escribió.</param>
    /// <param name="unidadIntroducidaId">Unidad en la que se escribió.</param>
    /// <param name="factorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
    /// <param name="costeUnitario">
    /// Coste de una unidad base, en <see cref="Divisa"/>, o <c>null</c>. Solo en una línea que sube.
    /// </param>
    /// <param name="momento">Ahora.</param>
    /// <param name="codigoDeLote">El código del lote, o <c>null</c>.</param>
    /// <param name="numeroDeSerie">El número de serie, o <c>null</c>. Nunca con lote.</param>
    public void AnadirLinea(
        Guid ubicacionId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        decimal? costeUnitario,
        DateTimeOffset momento,
        string? codigoDeLote = null,
        string? numeroDeSerie = null)
    {
        if (Estado != EstadoDeAjuste.Borrador)
        {
            throw new InvalidOperationException(
                $"Un ajuste en estado «{Estado}» no admite líneas nuevas: sus filas del libro ya " +
                "están escritas y el libro es de solo añadido (R2, R3).");
        }

        var nueva = LineaDeAjuste.Crear(
            Id,
            _lineas.Count + 1,
            ubicacionId,
            articuloId,
            cantidadIntroducida,
            unidadIntroducidaId,
            factorAUnidadBase,
            costeUnitario,
            codigoDeLote,
            numeroDeSerie,
            momento);

        if (nueva.NumeroDeSerie is { } serie
            && _lineas.Any(linea => linea.ArticuloId == articuloId
                && string.Equals(linea.NumeroDeSerie, serie, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"El número de serie «{serie}» ya está en otra línea de este documento: una serie " +
                "sale una sola vez por documento, y moverla de sitio es la reubicación (ADR-0048 §3).");
        }

        _lineas.Add(nueva);
    }

    /// <summary>Los lotes que nombran las líneas, sin repetir y en el orden en que se crean.</summary>
    /// <remarks>
    /// <b>El orden es el del cerrojo</b>: por artículo y por código, byte a byte. La sentencia que
    /// crea las filas los inserta en ese orden, y dos confirmaciones que nombran los mismos lotes
    /// nuevos esperan una por la otra en vez de bloquearse en cruz (ADR-0048 §2).
    /// </remarks>
    /// <returns>Cada lote, con su artículo.</returns>
    public IReadOnlyList<CodigoDeUnArticulo> LotesQueNombra() => Nombrados(linea => linea.CodigoDeLote);

    /// <summary>Las series que nombran las líneas, sin repetir y en el orden en que se crean.</summary>
    /// <returns>Cada serie, con su artículo.</returns>
    public IReadOnlyList<CodigoDeUnArticulo> SeriesQueNombra() => Nombrados(linea => linea.NumeroDeSerie);

    /// <summary>
    /// Confirma el ajuste y devuelve las filas del libro que hay que escribir <b>en la misma
    /// transacción</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los movimientos salen de aquí pero no viven aquí</b>, y las dos mitades importan. Salen
    /// de aquí porque confirmar sin mover el libro no significa nada, y devolverlos hace que no
    /// exista una forma de confirmar sin obtenerlos. No viven aquí —no son una colección del
    /// agregado— por tres motivos: el libro lo escriben cinco documentos distintos y no es la
    /// tabla de ninguno; la R13 describe dos cosas que se apuntan, no una que contiene a la otra;
    /// y, la que decide el día a día, con los movimientos dentro del agregado EF Core los traería
    /// seguidos del ajuste, y cualquier cambio de estado posterior los llevaría <i>tracked</i>:
    /// bastaría con que uno quedara marcado como modificado para estrellar el <c>SaveChanges</c>
    /// contra el disparador de solo añadido, en tiempo de ejecución.
    /// </para>
    /// <para>
    /// <b>Sin líneas no se confirma</b>, y esa es la mitad de vuelta de la R13: «todo ajuste
    /// confirmado tiene al menos un movimiento» se sostiene aquí, antes de que la fila exista, y
    /// no con una comprobación posterior que encontraría el daño ya hecho.
    /// </para>
    /// <para>
    /// <b>El número entra por parámetro y no se toma aquí</b>, aunque este sea el instante en que
    /// se gasta. Tomarlo exige una sentencia con cerrojo contra la base, y el dominio no habla con
    /// la base; quien la llama es el caso de uso, dentro de la transacción que ya tiene abierta. Lo
    /// que sí se sostiene aquí es que <b>no hay forma de confirmar sin número</b>: no existe una
    /// sobrecarga sin él, y volver a llamar con otro no pasa de la transición de estado.
    /// </para>
    /// <para>
    /// <b>La valoración también entra por parámetro, y por lo mismo</b> (ADR-0046 §10): la calcula
    /// <c>IValoracionDeExistencias</c> contra los saldos que el caso de uso acaba de bloquear, y
    /// el dominio no bloquea nada. Lo que sí se sostiene aquí es que <b>no hay forma de confirmar
    /// sin valorar</b>: se exige una por línea, en el orden de <see cref="LineasAValorar"/>, y cada
    /// línea anota la suya.
    /// </para>
    /// </remarks>
    /// <param name="numero">El correlativo que la serie acaba de dar, en esta misma transacción.</param>
    /// <param name="evento">Lo que se cuenta de la confirmación.</param>
    /// <param name="valoracion">Una línea valorada por cada línea del documento, en su orden.</param>
    /// <param name="resueltos">
    /// La fila de cada lote y de cada serie que nombra el documento (ADR-0048 §2).
    /// </param>
    /// <param name="momento">Ahora.</param>
    /// <returns>Una fila del libro por línea del documento.</returns>
    public IReadOnlyList<MovimientoStock> Confirmar(
        long numero,
        EventoDeIntegracion evento,
        IReadOnlyList<LineaValorada> valoracion,
        LotesYSeriesResueltos resueltos,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(valoracion);
        ArgumentNullException.ThrowIfNull(resueltos);

        if (_lineas.Count == 0)
        {
            throw new InvalidOperationException(
                "Un ajuste sin líneas no se confirma: no movería el libro, y un documento " +
                "confirmado que no mueve nada rompe la vuelta de la R13.");
        }

        if (numero <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numero),
                numero,
                "Los correlativos de una serie empiezan en uno. Un cero o un negativo aquí " +
                "significa que el número no salió del mecanismo de numeración.");
        }

        // ANTES DE TRANSITAR, para que una valoración que no casa no deje el documento a medias.
        // La divisa la vuelve a mirar cada fila del libro, pero entonces el estado ya habría cambiado.
        if (valoracion.Count != _lineas.Count
            || valoracion.Any(valorada => valorada is null
                || valorada.Valor.Divisa != Divisa
                || valorada.PrecioMedio.Divisa != Divisa))
        {
            throw new ArgumentException(
                $"El documento tiene {_lineas.Count} líneas en {Divisa}, y la valoración trae " +
                $"{valoracion.Count}: hace falta una por línea, en la divisa del documento " +
                "(ADR-0046 §10).",
                nameof(valoracion));
        }

        // Y POR LO MISMO: un código sin su fila no deja confirmar, ni a medias.
        if (LotesQueNombra().Any(lote => !resueltos.Lotes.ContainsKey(lote))
            || SeriesQueNombra().Any(serie => !resueltos.Series.ContainsKey(serie)))
        {
            throw new ArgumentException(
                "El documento nombra lotes o series que no llegan resueltos: cada código tiene que " +
                "traer su fila, que la crea o la encuentra la confirmación (ADR-0048 §2).",
                nameof(resueltos));
        }

        Transitar(EstadoDeAjuste.Borrador, EstadoDeAjuste.Confirmado, evento);

        // DESPUÉS DE TRANSITAR, y ese orden es el que hace que confirmar dos veces no repinte el
        // número: la transición revienta contra un ajuste que ya no está en borrador, así que la
        // asignación no llega a ejecutarse.
        Numero = numero;

        IReadOnlyList<LineaDeAjuste> lineas = Lineas;
        List<MovimientoStock> movimientos = new(lineas.Count);

        for (int indice = 0; indice < lineas.Count; indice++)
        {
            LineaDeAjuste linea = lineas[indice];
            LineaValorada valorada = valoracion[indice];

            linea.AnotarValor(valorada.Valor.Cantidad);

            movimientos.Add(MovimientoStock.Registrar(
                EmpresaId,
                FechaDeOperacion,
                AlmacenId,
                linea.UbicacionId,
                linea.ArticuloId,
                linea.CodigoDeLote is { } lote ? resueltos.Lotes[new(linea.ArticuloId, lote)] : null,
                linea.NumeroDeSerie is { } serie ? resueltos.Series[new(linea.ArticuloId, serie)] : null,
                linea.CantidadIntroducida,
                linea.UnidadIntroducidaId,
                linea.FactorAUnidadBase,
                Divisa,
                linea.CosteUnitario is { } coste ? Importe.De(coste, Divisa) : null,
                valorada.Valor,
                valorada.PrecioMedio,
                TipoDeDocumentoOrigen.Ajuste,
                Id,
                momento));
        }

        return movimientos;
    }

    /// <summary>
    /// Lo que el documento pide valorar: una línea por línea, en su orden y en la divisa del
    /// documento.
    /// </summary>
    /// <remarks>
    /// <b>La clave es el artículo y el almacén del documento</b>, sin la ubicación (ADR-0046 §3). La
    /// cantidad es la de la fila del libro, en unidad base y redondeada igual que ella, porque el
    /// coste es por unidad base desde el 2.3. La línea de un inverso no trae coste: trae el valor
    /// que compensa.
    /// </remarks>
    /// <returns>Las líneas a valorar, en el orden de <see cref="Lineas"/>.</returns>
    public IReadOnlyList<LineaAValorar> LineasAValorar() =>
    [
        .. Lineas.Select(linea => new LineaAValorar(
            new ClaveDeValoracion(linea.ArticuloId, AlmacenId),
            MovimientoStock.EnUnidadBase(linea.CantidadIntroducida, linea.FactorAUnidadBase),
            linea.CosteUnitario is { } coste ? PrecioUnitario.De(coste, Divisa) : null,
            linea.ValorQueCompensa is { } compensa ? Importe.De(compensa, Divisa) : null)),
    ];

    /// <summary>
    /// Construye el ajuste <b>inverso</b> que compensará a este: mismo almacén y misma serie, las
    /// mismas líneas con la cantidad cambiada de signo, y todavía en borrador.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hay UNA negación y no dos, y eso no es economía: es la forma de que no puedan
    /// discrepar.</b> La línea guarda la cantidad tal como se escribió y nada más; la cantidad en
    /// unidad base no se guarda aquí, la calcula <c>MovimientoStock</c> al registrar la fila del
    /// libro. Así que negar aquí la introducida niega la base por construcción, y no hay un
    /// segundo sitio donde olvidarse.
    /// </para>
    /// <para>
    /// <b>El coste no se copia: se copia el valor.</b> El inverso de una entrada es una salida, y una
    /// salida no lleva coste, porque se valora al precio medio. Lo que el par necesita para sumar
    /// cero también en valor es el importe exacto que escribió el original, y un coste por unidad
    /// no siempre lo reproduce. Así que cada línea del inverso lleva el valor de la del original,
    /// con el signo cambiado, como valor que compensa (ADR-0046 §6). La divisa sí se hereda: el par
    /// habla la misma.
    /// </para>
    /// <para>
    /// <b>La fecha entra por parámetro y no se hereda</b>, que es lo que decidió el ítem 2.5. Un
    /// inverso con la fecha del original se asentaría en su mismo periodo, y anular un documento
    /// de un ejercicio cerrado sería escribir dentro de él — justo lo que la R9 del 2.6 prohibe.
    /// Con fecha propia, el periodo del original conserva su movimiento y otro lo revierte.
    /// </para>
    /// <para>
    /// <b>Esto solo no anula nada</b>, y el borrador que devuelve todavía no compensa a nadie: lo
    /// hará cuando se confirme y se llame a <see cref="Anular"/>. Un inverso confirmado cuyo
    /// original siguiera <c>Confirmado</c> es exactamente lo que el barrido de la doble flecha
    /// busca, así que la costura entre los dos pasos no queda sin vigilar.
    /// </para>
    /// </remarks>
    /// <param name="fechaDeOperacion">Día al que se imputa el inverso. No es la del original.</param>
    /// <param name="motivo">Por qué se anula, escrito por quien lo hace.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>El inverso en borrador, con sus líneas y sin número.</returns>
    public Ajuste CrearInverso(DateOnly fechaDeOperacion, string motivo, DateTimeOffset momento)
    {
        if (Estado != EstadoDeAjuste.Confirmado)
        {
            throw new InvalidOperationException(
                $"Un ajuste en estado «{Estado}» no se anula: un borrador se tira y un anulado ya " +
                "tiene su inverso (R2).");
        }

        if (AnulaAId is not null)
        {
            throw new InvalidOperationException(
                $"El ajuste {Id} es el inverso de {AnulaAId}, y un inverso no se anula: deshacerlo " +
                "sería volver a hacer el ajuste, y eso es otro ajuste (ADR-0055 §9).");
        }

        Ajuste inverso = Abrir(
            EmpresaId, SerieId, AlmacenId, fechaDeOperacion, motivo, Divisa, momento);
        inverso.AnulaAId = Id;

        foreach (LineaDeAjuste linea in Lineas)
        {
            inverso._lineas.Add(linea.Compensada(inverso.Id, momento));
        }

        return inverso;
    }

    /// <summary>Da por anulado el ajuste, contra el inverso que ya lo compensa (R2).</summary>
    /// <remarks>
    /// <para>
    /// <b>El inverso entra por parámetro y tiene que estar confirmado</b>, y ese orden es la
    /// regla: primero existe el documento que compensa y después el original se da por anulado. Al
    /// revés —mover el estado y crear luego el inverso— habría un instante con un ajuste anulado y
    /// su efecto en el libro sin compensar, que es la media regla que el 2.3 se negó a escribir.
    /// </para>
    /// <para>
    /// <b>Y se comprueba que el inverso es el de ESTE ajuste.</b> Sin esa guarda, anular admitiría
    /// el inverso de otro documento y el par quedaría cruzado: el barrido lo vería después, pero
    /// el daño —dos anulados y un solo inverso— ya estaría escrito en un libro de solo añadido.
    /// </para>
    /// </remarks>
    /// <param name="inverso">El documento que lo compensa, ya confirmado.</param>
    /// <param name="evento">Lo que se cuenta de la anulación.</param>
    public void Anular(Ajuste inverso, EventoDeIntegracion evento)
    {
        ArgumentNullException.ThrowIfNull(inverso);

        if (inverso.AnulaAId != Id)
        {
            throw new InvalidOperationException(
                "El inverso con el que se anula no compensa a este ajuste: lo que llega apunta a " +
                $"«{inverso.AnulaAId}» y esto es «{Id}» (R2).");
        }

        if (inverso.Estado != EstadoDeAjuste.Confirmado)
        {
            throw new InvalidOperationException(
                $"El inverso está en estado «{inverso.Estado}»: un ajuste no se da por anulado " +
                "contra un documento que todavía no ha movido el libro (R2, R3).");
        }

        Transitar(EstadoDeAjuste.Confirmado, EstadoDeAjuste.Anulado, evento);
    }

    private List<CodigoDeUnArticulo> Nombrados(Func<LineaDeAjuste, string?> codigoDe) =>
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
