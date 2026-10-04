using System.ComponentModel.DataAnnotations;

namespace Bastion.Inventario.Contracts.Transferencias;

/// <summary>Lo que hace falta para abrir una transferencia en borrador.</summary>
/// <remarks>
/// <b>No lleva empresa</b>, como el alta del ajuste: sale del <i>claim</i> del usuario y no de la
/// petición (R8). Y los dos almacenes tienen que ser de esa empresa, que es lo que comprueba el
/// puerto, sin decir si el otro existe en otra (ADR-0053 §8).
/// </remarks>
/// <param name="SerieId">Serie que la numerará al enviarla (R5).</param>
/// <param name="AlmacenOrigenId">De dónde sale.</param>
/// <param name="AlmacenDestinoId">A dónde llega. Distinto del origen.</param>
/// <param name="FechaDeEnvio">Día en que sale, que es al que se imputa la salida (R14).</param>
/// <param name="Lineas">Qué se mueve.</param>
public sealed record AbrirTransferenciaDto(
    Guid SerieId,
    Guid AlmacenOrigenId,
    Guid AlmacenDestinoId,
    DateOnly FechaDeEnvio,
    IReadOnlyList<LineaDeTransferenciaDto> Lineas);

/// <summary>Una línea de la petición de alta.</summary>
/// <remarks>
/// <para>
/// <b>La cantidad es positiva</b>: una transferencia lleva mercancía del origen al destino, y la
/// negada es la de su inverso, que se construye anulando.
/// </para>
/// <para>
/// <b>Lleva las dos ubicaciones desde el alta</b> (ADR-0053 §1): la del origen, de donde sale, y la
/// del destino, donde se espera. La recepción es entera y no pregunta nada.
/// </para>
/// <para>
/// <b>No lleva coste</b>: lo que sale del origen se valora a su precio medio, y ese importe es el
/// que entra en el destino (ADR-0053 §2).
/// </para>
/// </remarks>
/// <param name="UbicacionOrigenId">Hueco del almacén de origen.</param>
/// <param name="UbicacionDestinoId">Hueco del almacén de destino.</param>
/// <param name="ArticuloId">Artículo que se mueve.</param>
/// <param name="CantidadIntroducida">Cantidad positiva, tal como se escribe.</param>
/// <param name="UnidadIntroducidaId">Unidad en la que se escribe.</param>
/// <param name="FactorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
/// <param name="CodigoDeLote">El lote, si el artículo va por lote, con la forma del ajuste.</param>
/// <param name="NumeroDeSerie">El número de serie, si el artículo va por serie: una unidad base.</param>
public sealed record LineaDeTransferenciaDto(
    Guid UbicacionOrigenId,
    Guid UbicacionDestinoId,
    Guid ArticuloId,
    decimal CantidadIntroducida,
    Guid UnidadIntroducidaId,
    decimal FactorAUnidadBase,
    string? CodigoDeLote = null,
    string? NumeroDeSerie = null);

/// <summary>Una transferencia, como se enseña.</summary>
/// <remarks>
/// <b><c>Numero</c> es nulo mientras sea un borrador, y <c>FechaDeRecepcion</c> mientras no haya
/// llegado</b>: el tipo lo dice, como en el ajuste, en vez de un cero o una fecha mínima que parezcan
/// datos.
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="SerieId">Serie que la numera (R5).</param>
/// <param name="Numero">El correlativo, o <c>null</c> si todavía es un borrador.</param>
/// <param name="AlmacenOrigenId">De dónde sale.</param>
/// <param name="AlmacenDestinoId">A dónde llega.</param>
/// <param name="FechaDeEnvio">Día al que se imputa la salida.</param>
/// <param name="FechaDeRecepcion">Día al que se imputa la entrada, o <c>null</c> si no ha llegado.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="Lineas">Cuántas líneas tiene.</param>
/// <param name="AnulaAId">La transferencia que esta compensa, o <c>null</c> si no es un inverso.</param>
/// <param name="Motivo">Por qué se anuló lo que compensa; solo lo lleva un inverso.</param>
/// <param name="Divisa">La de todos sus importes: la divisa base de la empresa al abrirla.</param>
public sealed record TransferenciaDto(
    Guid Id,
    Guid SerieId,
    long? Numero,
    Guid AlmacenOrigenId,
    Guid AlmacenDestinoId,
    DateOnly FechaDeEnvio,
    DateOnly? FechaDeRecepcion,
    string Estado,
    int Lineas,
    Guid? AnulaAId,
    string? Motivo,
    string Divisa);

/// <summary>Lo que hace falta para recibir una transferencia enviada.</summary>
/// <remarks>
/// <b>Solo la fecha</b>: la recepción es entera (ADR-0053 §4), así que no hay líneas que elegir, y
/// una diferencia se regulariza después con un ajuste en el destino.
/// </remarks>
public sealed record RecibirTransferenciaDto
{
    /// <summary>Día en que llega. No futuro, y no anterior al envío (ADR-0053 §3).</summary>
    /// <remarks>
    /// <b>Anulable en el tipo para que el <c>[Required]</c> muerda</b>: con un <c>DateOnly</c> a
    /// secas, un cuerpo sin la fecha llegaría como el 1 de enero del año uno, y se contestaría con
    /// otra regla que no es la que falla.
    /// </remarks>
    [Required(ErrorMessage = "La fecha de recepción es obligatoria.")]
    public DateOnly? FechaDeRecepcion { get; init; }
}

/// <summary>Lo que hace falta para anular una transferencia enviada o recibida.</summary>
/// <remarks>
/// <b>Solo el motivo, y sin fecha</b>, por lo mismo que en el ajuste: las líneas del inverso salen
/// del original, negadas, y su fecha es la de hoy, que pone el servidor (ADR-0053 §5).
/// </remarks>
/// <param name="Motivo">Por qué se anula, escrito por quien lo hace.</param>
public sealed record AnularTransferenciaDto(string Motivo);

/// <summary>El par que deja una anulación: la transferencia anulada y la que la compensa.</summary>
/// <param name="Original">La transferencia que queda anulada.</param>
/// <param name="Inverso">El contra-documento, ya numerado y recibido.</param>
public sealed record AnulacionDeTransferenciaDto(TransferenciaDto Original, TransferenciaDto Inverso);
