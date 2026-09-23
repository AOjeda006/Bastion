using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Documentos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Domain.Multiempresa;
using Bastion.Inventario.Domain.Movimientos;

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
        DateTimeOffset momento)
        : base(EstadoDeAjuste.Borrador, momento)
    {
        Id = id;
        EmpresaId = empresaId;
        SerieId = serieId;
        AlmacenId = almacenId;
        FechaDeOperacion = fechaDeOperacion;
        Motivo = motivo;
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

    /// <summary>Las líneas del documento.</summary>
    public IReadOnlyList<LineaDeAjuste> Lineas => _lineas;

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
    /// <param name="momento">Ahora.</param>
    /// <returns>El ajuste en borrador, sin líneas ni número.</returns>
    public static Ajuste Abrir(
        Guid empresaId,
        Guid serieId,
        Guid almacenId,
        DateOnly fechaDeOperacion,
        string motivo,
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
            Guid.CreateVersion7(), empresaId, serieId, almacenId, fechaDeOperacion, limpio, momento);
    }

    /// <summary>Añade una línea. Solo en borrador.</summary>
    /// <param name="ubicacionId">Hueco del almacén.</param>
    /// <param name="articuloId">Artículo que se mueve.</param>
    /// <param name="cantidadIntroducida">Cantidad con signo, tal como se escribió.</param>
    /// <param name="unidadIntroducidaId">Unidad en la que se escribió.</param>
    /// <param name="factorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
    /// <param name="costeUnitario">Coste de una unidad base.</param>
    /// <param name="momento">Ahora.</param>
    public void AnadirLinea(
        Guid ubicacionId,
        Guid articuloId,
        decimal cantidadIntroducida,
        Guid unidadIntroducidaId,
        decimal factorAUnidadBase,
        Importe costeUnitario,
        DateTimeOffset momento)
    {
        if (Estado != EstadoDeAjuste.Borrador)
        {
            throw new InvalidOperationException(
                $"Un ajuste en estado «{Estado}» no admite líneas nuevas: sus filas del libro ya " +
                "están escritas y el libro es de solo añadido (R2, R3).");
        }

        _lineas.Add(LineaDeAjuste.Crear(
            Id,
            ubicacionId,
            articuloId,
            cantidadIntroducida,
            unidadIntroducidaId,
            factorAUnidadBase,
            costeUnitario,
            momento));
    }

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
    /// </remarks>
    /// <param name="numero">El correlativo que la serie acaba de dar, en esta misma transacción.</param>
    /// <param name="evento">Lo que se cuenta de la confirmación.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>Una fila del libro por línea del documento.</returns>
    public IReadOnlyList<MovimientoStock> Confirmar(
        long numero, EventoDeIntegracion evento, DateTimeOffset momento)
    {
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

        Transitar(EstadoDeAjuste.Borrador, EstadoDeAjuste.Confirmado, evento);

        // DESPUÉS DE TRANSITAR, y ese orden es el que hace que confirmar dos veces no repinte el
        // número: la transición revienta contra un ajuste que ya no está en borrador, así que la
        // asignación no llega a ejecutarse.
        Numero = numero;

        return
        [
            .. _lineas.Select(linea => MovimientoStock.Registrar(
                EmpresaId,
                FechaDeOperacion,
                AlmacenId,
                linea.UbicacionId,
                linea.ArticuloId,
                linea.CantidadIntroducida,
                linea.UnidadIntroducidaId,
                linea.FactorAUnidadBase,
                linea.CosteUnitario,
                TipoDeDocumentoOrigen.Ajuste,
                Id,
                momento)),
        ];
    }

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
    /// <b>El coste se copia, no se recalcula.</b> El inverso compensa lo que el original escribió,
    /// no lo que costaría hoy: recalcularlo dejaría el par sumando cero en cantidad y distinto de
    /// cero en valor, y entonces anular movería el valor del almacén sin mover una sola unidad.
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

        Ajuste inverso = Abrir(EmpresaId, SerieId, AlmacenId, fechaDeOperacion, motivo, momento);
        inverso.AnulaAId = Id;

        foreach (LineaDeAjuste linea in _lineas)
        {
            inverso.AnadirLinea(
                linea.UbicacionId,
                linea.ArticuloId,
                -linea.CantidadIntroducida,
                linea.UnidadIntroducidaId,
                linea.FactorAUnidadBase,
                linea.CosteUnitario,
                momento);
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
}
