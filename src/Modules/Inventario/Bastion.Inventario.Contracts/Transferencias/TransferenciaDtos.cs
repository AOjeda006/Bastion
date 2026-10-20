using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Contracts.Paginacion;

namespace Bastion.Inventario.Contracts.Transferencias;

/// <summary>Lo que hace falta para abrir una transferencia en borrador.</summary>
/// <remarks>
/// <para>
/// <b>No lleva empresa</b>, como el alta del ajuste: sale del <i>claim</i> del usuario y no de la
/// petición (R8). Y los dos almacenes tienen que ser de esa empresa, que es lo que comprueba el
/// puerto, sin decir si el otro existe en otra (ADR-0053 §8).
/// </para>
/// <para>
/// <b>Como mucho doscientas líneas</b>, el tope de una página del servidor, como en el ajuste
/// (ADR-0068 §1): la ficha las devuelve todas sin paginar. Una más es un <c>400</c>
/// <c>datos-no-validos</c> con este motivo, bajo la clave que pone el enlace de modelo.
/// </para>
/// </remarks>
/// <param name="SerieId">Serie que la numerará al enviarla (R5).</param>
/// <param name="AlmacenOrigenId">De dónde sale.</param>
/// <param name="AlmacenDestinoId">A dónde llega. Distinto del origen.</param>
/// <param name="FechaDeEnvio">Día en que sale, que es al que se imputa la salida (R14).</param>
/// <param name="Lineas">Qué se mueve: de una a doscientas líneas.</param>
public sealed record AbrirTransferenciaDto(
    Guid SerieId,
    Guid AlmacenOrigenId,
    Guid AlmacenDestinoId,
    DateOnly FechaDeEnvio,
    [MaxLength(Paginacion.TamanioMaximo, ErrorMessage = "Una transferencia lleva como mucho {1} líneas.")]
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
/// <para>
/// <b>No lleva factor: lo resuelve el servidor</b> (ADR-0068 §2), como en el ajuste. Si la unidad es
/// la base del artículo, el factor es 1; si no, es el de la conversión de Organización de esa unidad
/// a la base, y sin ella el alta contesta <c>transferencia-sin-conversion</c>. Hasta el 2.19 lo traía
/// la petición.
/// </para>
/// </remarks>
/// <param name="UbicacionOrigenId">Hueco del almacén de origen.</param>
/// <param name="UbicacionDestinoId">Hueco del almacén de destino.</param>
/// <param name="ArticuloId">Artículo que se mueve.</param>
/// <param name="CantidadIntroducida">
/// Cantidad positiva, tal como se escribe: hasta seis decimales y menos de un billón, y lo mismo lo
/// que mueve en la unidad base. Si no, <c>transferencia-cantidad-no-valida</c>.
/// </param>
/// <param name="UnidadIntroducidaId">
/// Unidad en la que se escribe: la base del artículo, o una que tenga conversión a ella.
/// </param>
/// <param name="CodigoDeLote">El lote, si el artículo va por lote, con la forma del ajuste.</param>
/// <param name="NumeroDeSerie">El número de serie, si el artículo va por serie: una unidad base.</param>
public sealed record LineaDeTransferenciaDto(
    Guid UbicacionOrigenId,
    Guid UbicacionDestinoId,
    Guid ArticuloId,
    decimal CantidadIntroducida,
    Guid UnidadIntroducidaId,
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

/// <summary>Una transferencia, como se enseña en su ficha: la cabecera y todas sus líneas (ADR-0068 §8).</summary>
/// <remarks>
/// <para>
/// <b>Las líneas van enteras y sin paginar</b>, porque el alta las trae en una sola petición y no
/// admite más de doscientas (ADR-0068 §1).
/// </para>
/// <para>
/// <b>Lleva lo que la pantalla necesita para decidir sus acciones</b>: el estado y si es el inverso
/// de otra (<see cref="AnulaAId"/>), que no se anula desde aquí.
/// </para>
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="SerieId">Serie que la numera (R5).</param>
/// <param name="Numero">El correlativo, o <c>null</c> si todavía es un borrador.</param>
/// <param name="AlmacenOrigenId">De dónde sale.</param>
/// <param name="AlmacenDestinoId">A dónde llega.</param>
/// <param name="FechaDeEnvio">Día al que se imputa la salida.</param>
/// <param name="FechaDeRecepcion">Día al que se imputa la entrada, o <c>null</c> si no ha llegado.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="Divisa">La de todos sus importes: la divisa base de la empresa al abrirla.</param>
/// <param name="AnulaAId">La transferencia que esta compensa, o <c>null</c> si no es un inverso.</param>
/// <param name="Motivo">Por qué se anuló lo que compensa; solo lo lleva un inverso.</param>
/// <param name="Lineas">Sus líneas, en su orden.</param>
public sealed record FichaDeTransferenciaDto(
    Guid Id,
    Guid SerieId,
    long? Numero,
    Guid AlmacenOrigenId,
    Guid AlmacenDestinoId,
    DateOnly FechaDeEnvio,
    DateOnly? FechaDeRecepcion,
    string Estado,
    string Divisa,
    Guid? AnulaAId,
    string? Motivo,
    IReadOnlyList<LineaDeFichaDeTransferenciaDto> Lineas);

/// <summary>Una línea de la ficha de la transferencia.</summary>
/// <remarks>
/// <b>Con el factor y la cantidad en unidad base</b>, que pone el servidor (ADR-0068 §2), y con las
/// dos ubicaciones, la de cada punta. Quien escribió tres cajas ve las unidades que salen del origen
/// y llegan al destino, que son las que suma el libro.
/// </remarks>
/// <param name="Numero">Su posición en el documento, desde uno.</param>
/// <param name="UbicacionOrigenId">Hueco del almacén de origen.</param>
/// <param name="UbicacionDestinoId">Hueco del almacén de destino.</param>
/// <param name="ArticuloId">Artículo que se mueve.</param>
/// <param name="CantidadIntroducida">
/// Cantidad tal como se escribió: positiva, y negada en un inverso.
/// </param>
/// <param name="UnidadIntroducidaId">Unidad en la que se escribió.</param>
/// <param name="FactorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
/// <param name="CantidadEnUnidadBase">La misma cantidad, en la unidad en la que se suma.</param>
/// <param name="CodigoDeLote">El lote, si el artículo va por lote.</param>
/// <param name="NumeroDeSerie">El número de serie, si el artículo va por serie.</param>
/// <param name="Valor">
/// Lo que salió del origen a su precio medio, que es lo que entra en el destino, o <c>null</c>
/// mientras es un borrador (ADR-0053 §2).
/// </param>
public sealed record LineaDeFichaDeTransferenciaDto(
    int Numero,
    Guid UbicacionOrigenId,
    Guid UbicacionDestinoId,
    Guid ArticuloId,
    decimal CantidadIntroducida,
    Guid UnidadIntroducidaId,
    decimal FactorAUnidadBase,
    decimal CantidadEnUnidadBase,
    string? CodigoDeLote,
    string? NumeroDeSerie,
    decimal? Valor);

/// <summary>Una transferencia, como sale en el listado: sin líneas.</summary>
/// <remarks>
/// <b>Una descartada no sale</b> (ADR-0068 §5): la esconde el filtro del contexto. <b>Y un inverso se
/// reconoce en la fila</b> por <see cref="AnulaAId"/>, porque va de los mismos almacenes que la que
/// compensa y, sin eso, la lista enseñaría dos veces el mismo viaje.
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="Numero">El correlativo, o <c>null</c> si todavía es un borrador.</param>
/// <param name="AlmacenOrigenId">De dónde sale.</param>
/// <param name="AlmacenDestinoId">A dónde llega.</param>
/// <param name="FechaDeEnvio">Día al que se imputa la salida.</param>
/// <param name="FechaDeRecepcion">Día al que se imputa la entrada, o <c>null</c> si no ha llegado.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="AnulaAId">La transferencia que esta compensa, o <c>null</c> si no es un inverso.</param>
public sealed record TransferenciaResumenDto(
    Guid Id,
    long? Numero,
    Guid AlmacenOrigenId,
    Guid AlmacenDestinoId,
    DateOnly FechaDeEnvio,
    DateOnly? FechaDeRecepcion,
    string Estado,
    Guid? AnulaAId);

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
