using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Contracts.Paginacion;

namespace Bastion.Inventario.Contracts.Ajustes;

/// <summary>Lo que hace falta para abrir un ajuste en borrador.</summary>
/// <remarks>
/// <para>
/// <b>No lleva empresa</b>, y la ausencia es la regla: la empresa sale del <i>claim</i> del usuario
/// y no de la petición (R8). Si el campo existiera, alguien podría ajustar las existencias de otra
/// sociedad de la misma instalación escribiendo su identificador.
/// </para>
/// <para>
/// <b>Como mucho doscientas líneas</b>, el tope de una página del servidor (ADR-0065 §6): la ficha
/// las devuelve todas sin paginar, porque el alta las trae en una petición. Una más es un
/// <c>400</c> <c>datos-no-validos</c> con este motivo.
/// </para>
/// <para>
/// <b>La clave de ese campo la pone el enlace de modelo</b>, como la de todo <c>400</c> que sale de
/// una anotación, y no es la ruta de <c>zod</c> que llevan los rechazos de una línea (§3): esos los
/// escribe el caso de uso. La pantalla no llega a mandar la línea doscientas una, porque su rejilla
/// para en doscientas, así que no tiene que casar esta clave con ningún campo.
/// </para>
/// </remarks>
/// <param name="SerieId">Serie que lo numerará al confirmarlo (R5).</param>
/// <param name="AlmacenId">Almacén contra el que se ajusta.</param>
/// <param name="FechaDeOperacion">Día al que se imputa (R14: es un día, no un instante).</param>
/// <param name="Motivo">Por qué se ajusta.</param>
/// <param name="Lineas">Qué se mueve: de una a doscientas líneas.</param>
public sealed record AbrirAjusteDto(
    Guid SerieId,
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    string Motivo,
    [MaxLength(Paginacion.TamanioMaximo, ErrorMessage = "Un ajuste lleva como mucho {1} líneas.")]
    IReadOnlyList<LineaDeAjusteDto> Lineas);

/// <summary>Una línea de la petición de alta.</summary>
/// <remarks>
/// <para>
/// <b>La cantidad va con signo</b> y no hay un campo «entrada o salida»: dos sitios donde guardar
/// el mismo signo acaban contradiciéndose.
/// </para>
/// <para>
/// <b>No lleva factor: lo resuelve el servidor</b> (ADR-0065 §2). Si la unidad es la base del
/// artículo, el factor es 1; si no, es el de la conversión de Organización de esa unidad a la base,
/// y sin ella el alta contesta <c>ajuste-sin-conversion</c>. Hasta el 2.17 lo traía la petición, y
/// quien la escribía podía decir que una caja tenía las unidades que quisiera.
/// </para>
/// <para>
/// <b>No lleva divisa</b>: la del documento es la divisa base de la empresa, y la pone el servidor
/// al abrirlo (ADR-0046 §7). Con una por línea, un documento podría mezclarlas.
/// </para>
/// </remarks>
/// <param name="UbicacionId">Hueco del almacén.</param>
/// <param name="ArticuloId">Artículo que se mueve.</param>
/// <param name="CantidadIntroducida">Cantidad con signo, tal como se escribe.</param>
/// <param name="UnidadIntroducidaId">
/// Unidad en la que se escribe: la base del artículo, o una que tenga conversión a ella.
/// </param>
/// <param name="CosteUnitario">
/// Coste de una unidad base, en la divisa base de la empresa, o nada. Solo lo lleva una línea que
/// sube, y nunca negativo: si no, <c>ajuste-coste-no-valido</c>. Sin él, la entrada se valora al
/// precio medio de su artículo en el almacén; y sin precio medio, porque no hay existencias,
/// confirmar contesta <c>ajuste-entrada-sin-coste-ni-precio-medio</c>.
/// </param>
/// <param name="CodigoDeLote">
/// El lote, si el artículo va por lote: de 1 a 20 caracteres del conjunto 82 de GS1, con la caja
/// que tenga. Los espacios de los extremos se recortan. Si no, <c>ajuste-lote-no-valido</c>.
/// </param>
/// <param name="NumeroDeSerie">
/// El número de serie, si el artículo va por serie, con la misma forma que el lote. La línea mueve
/// entonces una unidad base, y el número no se repite en el documento.
/// </param>
public sealed record LineaDeAjusteDto(
    Guid UbicacionId,
    Guid ArticuloId,
    decimal CantidadIntroducida,
    Guid UnidadIntroducidaId,
    decimal? CosteUnitario,
    string? CodigoDeLote = null,
    string? NumeroDeSerie = null);

/// <summary>Un ajuste, como se enseña.</summary>
/// <remarks>
/// <b><c>Numero</c> es nulo mientras sea un borrador</b>, y el tipo lo dice: quien pinte este DTO
/// tiene que decidir qué enseñar en ese hueco, en vez de encontrarse un cero que parece un número.
/// Se rellena al confirmar y no vuelve a cambiar.
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="SerieId">Serie que lo numera (R5).</param>
/// <param name="Numero">El correlativo, o <c>null</c> si todavía es un borrador.</param>
/// <param name="AlmacenId">Almacén contra el que se ajusta.</param>
/// <param name="FechaDeOperacion">Día al que se imputa.</param>
/// <param name="Motivo">Por qué se ajusta.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="Lineas">Cuántas líneas tiene.</param>
/// <param name="AnulaAId">El ajuste que este compensa, o <c>null</c> si no es un inverso.</param>
/// <param name="Divisa">La de todos sus importes: la divisa base de la empresa al abrirlo.</param>
public sealed record AjusteDto(
    Guid Id,
    Guid SerieId,
    long? Numero,
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    string Motivo,
    string Estado,
    int Lineas,
    Guid? AnulaAId,
    string Divisa);

/// <summary>Un ajuste, como se enseña en su ficha: la cabecera y todas sus líneas (ADR-0065 §8).</summary>
/// <remarks>
/// <para>
/// <b>Las líneas van enteras y sin paginar</b>, porque el alta las trae en una sola petición y no
/// admite más de doscientas (ADR-0065 §6).
/// </para>
/// <para>
/// <b>Lleva lo que la pantalla necesita para decidir sus acciones</b>: el estado, si es el inverso de
/// otro (<see cref="AnulaAId"/>) y si es el ajuste de un recuento (<see cref="RecuentoId"/>). Ni el
/// uno ni el otro se anulan desde aquí.
/// </para>
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="SerieId">Serie que lo numera (R5).</param>
/// <param name="Numero">El correlativo, o <c>null</c> si todavía es un borrador.</param>
/// <param name="AlmacenId">Almacén contra el que se ajusta.</param>
/// <param name="FechaDeOperacion">Día al que se imputa.</param>
/// <param name="Motivo">Por qué se ajusta.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="Divisa">La de todos sus importes: la divisa base de la empresa al abrirlo.</param>
/// <param name="AnulaAId">El ajuste que este compensa, o <c>null</c> si no es un inverso.</param>
/// <param name="RecuentoId">El recuento cuya diferencia mueve, o <c>null</c> si no viene de uno.</param>
/// <param name="Lineas">Sus líneas, en su orden.</param>
public sealed record FichaDeAjusteDto(
    Guid Id,
    Guid SerieId,
    long? Numero,
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    string Motivo,
    string Estado,
    string Divisa,
    Guid? AnulaAId,
    Guid? RecuentoId,
    IReadOnlyList<LineaDeFichaDeAjusteDto> Lineas);

/// <summary>Una línea de la ficha del ajuste.</summary>
/// <remarks>
/// <b>Con el factor y la cantidad en unidad base</b>, que pone el servidor (ADR-0065 §2): quien
/// escribió tres cajas ve las unidades que mueven, que son las que suma el libro.
/// </remarks>
/// <param name="Numero">Su posición en el documento, desde uno.</param>
/// <param name="UbicacionId">Hueco del almacén.</param>
/// <param name="ArticuloId">Artículo que se mueve.</param>
/// <param name="CantidadIntroducida">Cantidad con signo, tal como se escribió.</param>
/// <param name="UnidadIntroducidaId">Unidad en la que se escribió.</param>
/// <param name="FactorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
/// <param name="CantidadEnUnidadBase">Cantidad con signo, en la unidad en la que se suma.</param>
/// <param name="CosteUnitario">Coste de una unidad base, o nada.</param>
/// <param name="CodigoDeLote">El lote, si el artículo va por lote.</param>
/// <param name="NumeroDeSerie">El número de serie, si el artículo va por serie.</param>
/// <param name="Valor">
/// Lo que la línea sumó a la valoración, o lo que le restó, o <c>null</c> mientras es borrador.
/// </param>
public sealed record LineaDeFichaDeAjusteDto(
    int Numero,
    Guid UbicacionId,
    Guid ArticuloId,
    decimal CantidadIntroducida,
    Guid UnidadIntroducidaId,
    decimal FactorAUnidadBase,
    decimal CantidadEnUnidadBase,
    decimal? CosteUnitario,
    string? CodigoDeLote,
    string? NumeroDeSerie,
    decimal? Valor);

/// <summary>Un ajuste, como sale en el listado: sin líneas.</summary>
/// <remarks>
/// <b>Un borrador descartado no sale</b> (ADR-0065 §5): lo esconde el filtro del contexto.
/// </remarks>
/// <param name="Id">Identificador.</param>
/// <param name="Numero">El correlativo, o <c>null</c> si todavía es un borrador.</param>
/// <param name="AlmacenId">Almacén contra el que se ajusta.</param>
/// <param name="FechaDeOperacion">Día al que se imputa.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="Motivo">Por qué se ajusta.</param>
public sealed record AjusteResumenDto(
    Guid Id,
    long? Numero,
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    string Estado,
    string Motivo);

/// <summary>
/// La línea de la petición que paró el alta, en la extensión <c>actual</c> del problema
/// (ADR-0065 §3).
/// </summary>
/// <remarks>
/// <b>La llevan los <c>409</c> y los <c>422</c> de una línea</b>: la ubicación bloqueada, el artículo
/// que no se almacena, la unidad retirada y la trazabilidad que no casa. Un <c>400</c> dice lo mismo
/// en <c>errors</c>, con la ruta del campo, porque solo una validación tiene campos.
/// </remarks>
/// <param name="Linea">Su número, desde uno, que es el que enseña la ficha.</param>
public sealed record LineaQueFallaDto(int Linea);

/// <summary>
/// La conversión que le falta a una línea, o que está retirada, en la extensión <c>actual</c> del
/// problema (ADR-0065 §2).
/// </summary>
/// <remarks>
/// <b>Con las dos unidades</b>: quien lo lee sabe qué conversión dar de alta, o cuál está retirada,
/// sin volver a preguntar por el artículo.
/// </remarks>
/// <param name="Linea">Su número, desde uno.</param>
/// <param name="UnidadIntroducidaId">La unidad en la que se escribió la línea.</param>
/// <param name="UnidadBaseId">La unidad base de su artículo.</param>
public sealed record ConversionDeLaLineaDto(int Linea, Guid UnidadIntroducidaId, Guid UnidadBaseId);

/// <summary>Lo que hace falta para anular un ajuste confirmado.</summary>
/// <remarks>
/// <para>
/// <b>Solo el motivo, y es todo lo que hay que decidir.</b> Las líneas del inverso salen del
/// original —cambiadas de signo— y no se piden: dejarlas escribir sería admitir una anulación
/// parcial, que no es una anulación sino otro ajuste, y ese ya se puede dar de alta.
/// </para>
/// <para>
/// <b>Y no lleva fecha</b>, aunque el inverso tenga la suya. Es la de hoy, la pone el servidor, y
/// no se acepta de fuera: una anulación con fecha elegida sería la forma de asentar en un periodo
/// cerrado sin que la R9 del 2.6 tuviera nada que decir, porque el documento que llegara a la
/// comprobación ya vendría con la fecha buena escrita.
/// </para>
/// </remarks>
/// <param name="Motivo">Por qué se anula, escrito por quien lo hace.</param>
public sealed record AnularAjusteDto(string Motivo);

/// <summary>El par que deja una anulación: el documento anulado y el que lo compensa.</summary>
/// <remarks>
/// <b>Se devuelven los dos y no solo el inverso</b>, porque lo que el llamante tiene que poder ver
/// es la flecha entera: el original con su estado ya en <c>Anulado</c> y el inverso con su número
/// y su <c>AnulaAId</c> apuntando al primero. Con uno solo habría que volver a preguntar por el
/// otro, y entre las dos peticiones cabe justo la duda que la respuesta existe para quitar.
/// </remarks>
/// <param name="Original">El ajuste que queda anulado.</param>
/// <param name="Inverso">El contra-documento, ya confirmado y numerado.</param>
public sealed record AnulacionDto(AjusteDto Original, AjusteDto Inverso);

/// <summary>Una fila del libro, como se enseña.</summary>
/// <remarks>
/// <b>Lleva el estado del almacén al que apunta</b>, resuelto por el mismo puerto que usa el alta,
/// y ahí se ve la otra mitad del ADR-0037: un movimiento contra un almacén bloqueado se lee sin
/// problema y dice que su almacén está bloqueado. Lo que el bloqueo impide es operar, no existir.
/// </remarks>
/// <param name="Id">Identificador de la fila.</param>
/// <param name="FechaDeOperacion">Día al que se imputó.</param>
/// <param name="AlmacenId">Almacén.</param>
/// <param name="AlmacenSeOfreceParaLoNuevo">Si hoy se admitirían movimientos nuevos contra él.</param>
/// <param name="UbicacionId">Hueco del almacén.</param>
/// <param name="ArticuloId">Artículo.</param>
/// <param name="LoteId">El lote que movió, o nada.</param>
/// <param name="NumeroDeSerieId">El número de serie que movió, o nada. Nunca con lote.</param>
/// <param name="CantidadEnUnidadBase">Cantidad con signo, en la unidad en la que se suma.</param>
/// <param name="CantidadIntroducida">Cantidad tal como se escribió.</param>
/// <param name="UnidadIntroducidaId">Unidad en la que se escribió.</param>
/// <param name="FactorAUnidadBase">El puente entre las dos.</param>
/// <param name="CosteUnitario">Coste de una unidad base, o nada si la fila no lo trajo.</param>
/// <param name="Valor">
/// Lo que la fila sumó a la valoración de su artículo en su almacén, o lo que le restó, con el
/// signo de la cantidad. Cero en las filas de antes de la valoración (ADR-0046 §8).
/// </param>
/// <param name="PrecioMedio">
/// El precio medio que la fila congeló: el de después en lo que entra, el de antes en lo que sale.
/// </param>
/// <param name="Divisa">Código ISO-4217 de todos los importes de la fila (R6).</param>
/// <param name="DocumentoOrigenTipo">Qué clase de documento la escribió (R13).</param>
/// <param name="DocumentoOrigenId">Cuál (R13).</param>
public sealed record MovimientoDto(
    Guid Id,
    DateOnly FechaDeOperacion,
    Guid AlmacenId,
    bool AlmacenSeOfreceParaLoNuevo,
    Guid UbicacionId,
    Guid ArticuloId,
    Guid? LoteId,
    Guid? NumeroDeSerieId,
    decimal CantidadEnUnidadBase,
    decimal CantidadIntroducida,
    Guid UnidadIntroducidaId,
    decimal FactorAUnidadBase,
    decimal? CosteUnitario,
    decimal Valor,
    decimal PrecioMedio,
    string Divisa,
    string DocumentoOrigenTipo,
    Guid DocumentoOrigenId);
