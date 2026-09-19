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
        Guid almacenId,
        DateOnly fechaDeOperacion,
        string motivo,
        DateTimeOffset momento)
        : base(EstadoDeAjuste.Borrador, momento)
    {
        Id = id;
        EmpresaId = empresaId;
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

    /// <summary>Las líneas del documento.</summary>
    public IReadOnlyList<LineaDeAjuste> Lineas => _lineas;

    /// <summary>Abre un ajuste en borrador.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8).</param>
    /// <param name="almacenId">Almacén contra el que se ajusta.</param>
    /// <param name="fechaDeOperacion">Día al que se imputa.</param>
    /// <param name="motivo">Por qué se ajusta.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>El ajuste en borrador, sin líneas.</returns>
    public static Ajuste Abrir(
        Guid empresaId,
        Guid almacenId,
        DateOnly fechaDeOperacion,
        string motivo,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Un ajuste sin empresa no existe (R8).", nameof(empresaId));
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

        return new Ajuste(Guid.CreateVersion7(), empresaId, almacenId, fechaDeOperacion, limpio, momento);
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
    /// </remarks>
    /// <param name="evento">Lo que se cuenta de la confirmación.</param>
    /// <param name="momento">Ahora.</param>
    /// <returns>Una fila del libro por línea del documento.</returns>
    public IReadOnlyList<MovimientoStock> Confirmar(EventoDeIntegracion evento, DateTimeOffset momento)
    {
        if (_lineas.Count == 0)
        {
            throw new InvalidOperationException(
                "Un ajuste sin líneas no se confirma: no movería el libro, y un documento " +
                "confirmado que no mueve nada rompe la vuelta de la R13.");
        }

        Transitar(EstadoDeAjuste.Borrador, EstadoDeAjuste.Confirmado, evento);

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

    /// <summary>Marca el ajuste como anulado por un inverso (R2).</summary>
    /// <remarks>
    /// Aquí solo se mueve el estado del original. <b>Quien crea el documento inverso es el 2.5</b>,
    /// y por eso este método no lo construye: adelantarlo dejaría media regla escrita —un ajuste
    /// anulado sin nada que lo compense—, que es peor que no tenerla, porque parece que está.
    /// </remarks>
    /// <param name="evento">Lo que se cuenta de la anulación.</param>
    public void Anular(EventoDeIntegracion evento) =>
        Transitar(EstadoDeAjuste.Confirmado, EstadoDeAjuste.Anulado, evento);
}
