using Bastion.BuildingBlocks.Application.Multiempresa;

namespace Bastion.BuildingBlocks.Infrastructure.Auditoria;

/// <summary>
/// Una fila de la traza: qué cambió, de qué fila, quién lo pidió, desde qué empresa y cuándo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Una fila por entidad cambiada, no por propiedad cambiada.</b> Es la decisión de forma del
/// ítem, y la que los libros de la R3 van a copiar. Con una fila por propiedad, un alta de empresa
/// se convierte en seis filas sueltas que no se pueden leer como «esto fue un cambio» sin
/// inventarse igualmente un identificador de correlación; y preguntar «qué hizo este cambio»
/// obliga a un agrupamiento que la forma no da. Con una fila por entidad, la pregunta por columna
/// se contesta con un índice sobre <see cref="Valores"/> —es <c>jsonb</c>, no texto— y la pregunta
/// por cambio se contesta leyendo una fila. Es además la forma de <c>movimiento_stock</c>: un
/// apunte por hecho, con el detalle dentro.
/// </para>
/// <para>
/// <b>Sin empresa no significa sin explicación.</b> <see cref="EmpresaId"/> es anulable porque hay
/// escrituras legítimas sin inquilino —la semilla, el acceso, las comprobaciones de unicidad
/// global—, y cuando lo es, <see cref="SinInquilino"/> dice cuál de ellas. Exactamente uno de los
/// dos: lo comprueba el constructor y lo vuelve a comprobar una restricción de la propia tabla,
/// porque una invariante que solo vive en C# no protege de un <c>INSERT</c> por otra vía.
/// <see cref="Guid.Empty"/> queda descartado por lo de siempre: es el valor por omisión que
/// rellena el hueco y lo esconde.
/// </para>
/// </remarks>
public sealed class RegistroDeAuditoria
{
    // Constructor para EF Core. Las propiedades se rellenan por reflexión al materializar.
    private RegistroDeAuditoria()
    {
        Entidad = string.Empty;
        EntidadId = string.Empty;
        Valores = string.Empty;
    }

    private RegistroDeAuditoria(
        Guid correlacionId,
        DateTimeOffset ocurridoEn,
        Guid? empresaId,
        MotivoSinInquilino? sinInquilino,
        Guid? usuarioId,
        string entidad,
        string entidadId,
        TipoDeCambio cambio,
        string valores)
    {
        Id = Guid.CreateVersion7();
        CorrelacionId = correlacionId;
        OcurridoEn = ocurridoEn;
        EmpresaId = empresaId;
        SinInquilino = sinInquilino;
        UsuarioId = usuarioId;
        Entidad = entidad;
        EntidadId = entidadId;
        Cambio = cambio;
        Valores = valores;
    }

    /// <summary>Identificador de la fila de traza.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Todas las filas escritas por el mismo <c>SaveChanges</c> comparten este identificador.
    /// </summary>
    /// <remarks>
    /// Es lo que convierte seis filas en «un cambio». Sin él, una operación que toca la empresa y
    /// su domicilio deja dos rastros que solo se pueden juntar por el instante, que es una
    /// coincidencia, no una relación.
    /// </remarks>
    public Guid CorrelacionId { get; private set; }

    /// <summary>Instante en que se confirmó el cambio, con zona (§3).</summary>
    public DateTimeOffset OcurridoEn { get; private set; }

    /// <summary>Empresa desde la que se hizo el cambio, o <c>null</c> si no había ninguna.</summary>
    /// <remarks>
    /// Es la empresa <b>desde la que se actuó</b>, no la empresa «dueña» de la fila cambiada. Para
    /// una entidad de inquilino son la misma; para una global —un rol, que lo es por decisión del
    /// 0.6— no lo son, y la que importa es esta: la auditoría contesta quién hizo qué y desde
    /// dónde. La consecuencia se asume: un mismo rol acumula trazas de varias empresas, y cada una
    /// solo ve las suyas.
    /// </remarks>
    public Guid? EmpresaId { get; private set; }

    /// <summary>Por qué no hay empresa, cuando no la hay.</summary>
    public MotivoSinInquilino? SinInquilino { get; private set; }

    /// <summary>Quién lo pidió, o <c>null</c> si no había nadie autenticado.</summary>
    public Guid? UsuarioId { get; private set; }

    /// <summary>Nombre corto del tipo de la fila cambiada.</summary>
    public string Entidad { get; private set; }

    /// <summary>Clave de la fila cambiada. Compuesta, sus partes van unidas por <c>|</c>.</summary>
    public string EntidadId { get; private set; }

    /// <summary>Alta, modificación o baja.</summary>
    public TipoDeCambio Cambio { get; private set; }

    /// <summary>
    /// Qué cambió, como <c>jsonb</c>: una entrada por propiedad, con <c>antes</c> y <c>despues</c>.
    /// </summary>
    /// <remarks>
    /// Un alta no lleva <c>antes</c> y una baja no lleva <c>despues</c>: el hueco es la
    /// información, y rellenarlo con un nulo lo confundiría con «cambió a nulo».
    /// </remarks>
    public string Valores { get; private set; }

    /// <summary>Arma una fila de traza, comprobando lo que la tabla también comprobará.</summary>
    /// <param name="correlacionId">Identificador común de todas las filas de este cambio.</param>
    /// <param name="ocurridoEn">Instante del cambio.</param>
    /// <param name="empresaId">Empresa desde la que se actúa, si la hay.</param>
    /// <param name="sinInquilino">Motivo, si no la hay.</param>
    /// <param name="usuarioId">Quién lo pide, si hay alguien.</param>
    /// <param name="entidad">Nombre corto del tipo cambiado.</param>
    /// <param name="entidadId">Clave de la fila cambiada.</param>
    /// <param name="cambio">Alta, modificación o baja.</param>
    /// <param name="valores">El detalle, ya serializado.</param>
    /// <returns>La fila de traza.</returns>
    /// <exception cref="InvalidOperationException">
    /// Si lleva empresa y motivo a la vez, o ninguno de los dos.
    /// </exception>
    public static RegistroDeAuditoria De(
        Guid correlacionId,
        DateTimeOffset ocurridoEn,
        Guid? empresaId,
        MotivoSinInquilino? sinInquilino,
        Guid? usuarioId,
        string entidad,
        string entidadId,
        TipoDeCambio cambio,
        string valores)
    {
        if (empresaId.HasValue == sinInquilino.HasValue)
        {
            throw new InvalidOperationException(
                "Una fila de auditoría lleva empresa, o lleva el motivo por el que no la lleva, " +
                "pero nunca las dos cosas ni ninguna de las dos.");
        }

        return new RegistroDeAuditoria(
            correlacionId, ocurridoEn, empresaId, sinInquilino, usuarioId,
            entidad, entidadId, cambio, valores);
    }
}
