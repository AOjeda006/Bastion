using System.ComponentModel.DataAnnotations;

namespace Bastion.Inventario.Contracts.Recuentos;

/// <summary>Lo que hace falta para abrir un recuento de un almacén.</summary>
/// <remarks>
/// <para>
/// <b>No lleva empresa</b>, como el alta del ajuste: sale del <i>claim</i> del usuario (R8).
/// </para>
/// <para>
/// <b>No lleva líneas</b>: el alta precarga las claves del almacén con existencias, sin contar
/// (ADR-0055 §5). Las que falten se añaden después, una a una.
/// </para>
/// <para>
/// <b>Lleva las dos series desde el alta</b> (ADR-0055 §1.3): la que numerará el recuento y la que
/// numerará su ajuste, si al confirmar hay diferencias.
/// </para>
/// </remarks>
/// <param name="SerieId">Serie que numerará el recuento al confirmarlo (R5).</param>
/// <param name="SerieDelAjusteId">Serie que numerará su ajuste.</param>
/// <param name="AlmacenId">El almacén que se cuenta, entero.</param>
/// <param name="Motivo">Por qué se cuenta. Es el motivo que llevará su ajuste.</param>
public sealed record AbrirRecuentoDto(
    Guid SerieId,
    Guid SerieDelAjusteId,
    Guid AlmacenId,
    string Motivo);

/// <summary>Un recuento, como se enseña en su ficha.</summary>
/// <remarks>
/// <para>
/// <b>Las dos cuentas que miran el teórico de ahora y la huella son nulas cuando el recuento ya no
/// está en curso</b>: el teórico de un recuento cerrado es el que quedó al confirmarlo (ADR-0055 §2).
/// La de las líneas sin contar va siempre, porque habla del recuento y no del almacén.
/// </para>
/// <para>
/// <b>El ajuste se encuentra por la flecha contraria</b> (ADR-0055 §8): es el que apunta a este
/// recuento, y es nulo si el recuento no movió el libro.
/// </para>
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="SerieId">Serie que lo numera.</param>
/// <param name="SerieDelAjusteId">Serie que numera su ajuste.</param>
/// <param name="Numero">El correlativo, o <c>null</c> mientras no se haya confirmado.</param>
/// <param name="AlmacenId">El almacén que se cuenta.</param>
/// <param name="FechaDeApertura">Día del alta.</param>
/// <param name="FechaDeConfirmacion">Día de la confirmación, que es el de su ajuste, o <c>null</c>.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="Motivo">Por qué se cuenta.</param>
/// <param name="Divisa">La de su ajuste: la divisa base de la empresa al abrirlo.</param>
/// <param name="MotivoDelDescarte">Por qué se descartó, si se descartó.</param>
/// <param name="MotivoDeLaAnulacion">Por qué se anuló, si se anuló.</param>
/// <param name="AjusteId">El ajuste que movió la diferencia, o <c>null</c>.</param>
/// <param name="Lineas">Cuántas claves lleva.</param>
/// <param name="LineasSinContar">Cuántas no se han contado todavía.</param>
/// <param name="LineasConElTeoricoCambiado">
/// Cuántas tienen un teórico distinto del de cuando se contaron, o <c>null</c> si ya no está en curso.
/// </param>
/// <param name="LineasConTransito">
/// Cuántas tienen mercancía en tránsito hacia su clave, o <c>null</c> si ya no está en curso.
/// </param>
/// <param name="HuellaDelTeorico">
/// La huella del teórico de todas las líneas, la que lleva la confirmación, o <c>null</c> si ya no
/// está en curso.
/// </param>
public sealed record RecuentoDto(
    Guid Id,
    Guid SerieId,
    Guid SerieDelAjusteId,
    long? Numero,
    Guid AlmacenId,
    DateOnly FechaDeApertura,
    DateOnly? FechaDeConfirmacion,
    string Estado,
    string Motivo,
    string Divisa,
    string? MotivoDelDescarte,
    string? MotivoDeLaAnulacion,
    Guid? AjusteId,
    int Lineas,
    int LineasSinContar,
    int? LineasConElTeoricoCambiado,
    int? LineasConTransito,
    string? HuellaDelTeorico);

/// <summary>Un recuento, como sale en el listado: sin líneas y sin teórico.</summary>
/// <remarks>
/// <b>Sin cuentas</b>: cada una pide leer las existencias del almacén, y una página de veinte
/// recuentos serían veinte almacenes. Las da la ficha.
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="Numero">El correlativo, o <c>null</c> mientras no se haya confirmado.</param>
/// <param name="AlmacenId">El almacén que se cuenta.</param>
/// <param name="FechaDeApertura">Día del alta.</param>
/// <param name="FechaDeConfirmacion">Día de la confirmación, o <c>null</c>.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="Motivo">Por qué se cuenta.</param>
public sealed record RecuentoResumenDto(
    Guid Id,
    long? Numero,
    Guid AlmacenId,
    DateOnly FechaDeApertura,
    DateOnly? FechaDeConfirmacion,
    string Estado,
    string Motivo);

/// <summary>Una línea del recuento: una clave del almacén, en su unidad base.</summary>
/// <remarks>
/// <para>
/// <b>Lo que ve quien cuenta</b> (ADR-0055 §2): lo contado, el teórico de cuando se contó, el de
/// ahora, y la diferencia que movería el ajuste. Mientras el recuento está en curso,
/// <c>Teorico</c> es el físico de la clave en este instante; en uno confirmado o anulado, el que
/// quedó al confirmar, y en uno descartado no hay ninguno.
/// </para>
/// <para>
/// <b>Una línea sin contar no es un cero</b> (ADR-0055 §5): <c>Contado</c> es <c>null</c>, y su
/// diferencia también.
/// </para>
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="Numero">Su orden en el recuento, desde uno.</param>
/// <param name="UbicacionId">El hueco del almacén.</param>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="CodigoDeLote">El lote, si el artículo va por lote.</param>
/// <param name="NumeroDeSerie">El número de serie, si el artículo va por serie.</param>
/// <param name="UnidadBaseId">La unidad en la que se cuenta: la base de su artículo.</param>
/// <param name="Origen">Si la trajo la precarga o se añadió a mano.</param>
/// <param name="CosteUnitario">El coste de una unidad base; solo en una añadida, y si se dijo.</param>
/// <param name="Contado">Lo contado, o <c>null</c> si nadie lo ha contado todavía.</param>
/// <param name="TeoricoAlContar">El físico de la clave cuando se contó, o <c>null</c>.</param>
/// <param name="Teorico">El teórico de ahora, o el que quedó al confirmar; <c>null</c> si se descartó.</param>
/// <param name="EnTransito">Lo que vuela hacia la clave, o <c>null</c> si ya no está en curso.</param>
/// <param name="Diferencia">Lo contado menos el teórico, o <c>null</c> si falta alguno de los dos.</param>
/// <param name="TeoricoCambiado">Si el teórico de ahora ya no es el de cuando se contó.</param>
/// <param name="LineaDeAjusteId">La línea del ajuste que la movió, o <c>null</c>.</param>
public sealed record LineaDeRecuentoDto(
    Guid Id,
    int Numero,
    Guid UbicacionId,
    Guid ArticuloId,
    string? CodigoDeLote,
    string? NumeroDeSerie,
    Guid UnidadBaseId,
    string Origen,
    decimal? CosteUnitario,
    decimal? Contado,
    decimal? TeoricoAlContar,
    decimal? Teorico,
    decimal? EnTransito,
    decimal? Diferencia,
    bool TeoricoCambiado,
    Guid? LineaDeAjusteId);

/// <summary>Lo contado en una línea, en la unidad base de su artículo.</summary>
/// <remarks>
/// <b>Va con el <c>If-Match</c> de la línea</b> (ADR-0055 §4): quien cuenta la ha leído, y si otro la
/// ha contado después, su cifra no pisa la del otro sin verla.
/// </remarks>
public sealed record ContarLineaDeRecuentoDto
{
    /// <summary>Lo contado: no negativo, con seis decimales como mucho, y cero o uno en una serie.</summary>
    /// <remarks>
    /// <b>Anulable en el tipo para que el <c>[Required]</c> muerda</b>: con un <c>decimal</c> a secas,
    /// un cuerpo sin la cifra llegaría como un cero, y una línea sin contar no es un cero
    /// (ADR-0055 §5).
    /// </remarks>
    [Required(ErrorMessage = "Lo contado es obligatorio.")]
    public decimal? Contado { get; init; }
}

/// <summary>Una clave que la precarga no traía, para contarla (ADR-0055 §6).</summary>
/// <remarks>
/// <para>
/// <b>Sin almacén</b>: es el del recuento. La ubicación tiene que ser suya, y el artículo, de los que
/// se almacenan.
/// </para>
/// <para>
/// <b>Sin unidad</b>: la línea se cuenta en la unidad base de su artículo, que dice Catálogo.
/// </para>
/// </remarks>
/// <param name="UbicacionId">El hueco del almacén del recuento.</param>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="CodigoDeLote">El lote, si el artículo va por lote.</param>
/// <param name="NumeroDeSerie">El número de serie, si el artículo va por serie.</param>
/// <param name="CosteUnitario">
/// El coste de una unidad base, o nada. Solo se usa si la línea sube; sin él, entra al precio medio
/// de su clave.
/// </param>
public sealed record AnadirLineaDeRecuentoDto(
    Guid UbicacionId,
    Guid ArticuloId,
    string? CodigoDeLote,
    string? NumeroDeSerie,
    decimal? CosteUnitario);

/// <summary>Lo que lleva la confirmación: la huella del teórico que vio quien confirma (ADR-0055 §2).</summary>
/// <remarks>
/// <b>Va con el <c>If-Match</c> de la cabecera, y cada uno dice una cosa</b>: la versión, si el
/// documento cambió desde que se leyó, y la huella, si cambió el almacén. Y con la
/// <c>Idempotency-Key</c>, obligatoria, porque confirmar gasta dos correlativos (ADR-0057).
/// </remarks>
public sealed record ConfirmarRecuentoDto
{
    /// <summary>La huella del teórico, tal como la dio la ficha: 64 cifras hexadecimales en minúsculas.</summary>
    [Required(ErrorMessage = "La huella del teórico es obligatoria.")]
    [RegularExpression(
        "^[0-9a-f]{64}$",
        ErrorMessage = "La huella del teórico son 64 cifras hexadecimales en minúsculas, tal como las da la ficha.")]
    public string HuellaDelTeorico { get; init; } = string.Empty;
}

/// <summary>Por qué se anula un recuento confirmado (ADR-0055 §9).</summary>
/// <remarks>
/// <b>Va con el <c>If-Match</c> de la cabecera y con la <c>Idempotency-Key</c>, obligatoria</b>, porque
/// el inverso de su ajuste gasta un correlativo (ADR-0057). El motivo es también el del inverso.
/// </remarks>
/// <param name="Motivo">Por qué se anula.</param>
public sealed record AnularRecuentoDto(string Motivo);

/// <summary>Por qué se descarta un recuento en curso (ADR-0055 §1.6).</summary>
/// <remarks>
/// <b>Va con el <c>If-Match</c> de la cabecera</b>, que dice que quien descarta ha visto lo último que
/// se contó. La <c>Idempotency-Key</c> se admite y no se exige: descartar no numera.
/// </remarks>
/// <param name="Motivo">Por qué se descarta.</param>
public sealed record DescartarRecuentoDto(string Motivo);

/// <summary>
/// El estado actual de un conflicto de la confirmación, en la extensión <c>actual</c> del problema
/// (ADR-0055 §11).
/// </summary>
/// <remarks>
/// <b>Lo llevan tres respuestas</b>: el <c>422</c> de las líneas sin contar, el <c>409</c> del teórico,
/// que es el único con la huella de ahora, y el <c>409</c> del tránsito. Las líneas van como las enseña
/// la ficha, las primeras cincuenta por su número, y el total dice cuántas son.
/// </remarks>
/// <param name="Total">Cuántas líneas están en el conflicto.</param>
/// <param name="Lineas">Las primeras, por su número.</param>
/// <param name="HuellaDelTeorico">La huella de ahora, solo en el conflicto del teórico.</param>
public sealed record LineasEnConflictoDto(
    int Total,
    IReadOnlyList<LineaDeRecuentoDto> Lineas,
    string? HuellaDelTeorico);
