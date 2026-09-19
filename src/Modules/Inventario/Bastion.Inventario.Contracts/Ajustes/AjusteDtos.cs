namespace Bastion.Inventario.Contracts.Ajustes;

/// <summary>Lo que hace falta para abrir un ajuste en borrador.</summary>
/// <remarks>
/// <b>No lleva empresa</b>, y la ausencia es la regla: la empresa sale del <i>claim</i> del usuario
/// y no de la petición (R8). Si el campo existiera, alguien podría ajustar las existencias de otra
/// sociedad de la misma instalación escribiendo su identificador.
/// </remarks>
/// <param name="AlmacenId">Almacén contra el que se ajusta.</param>
/// <param name="FechaDeOperacion">Día al que se imputa (R14: es un día, no un instante).</param>
/// <param name="Motivo">Por qué se ajusta.</param>
/// <param name="Lineas">Qué se mueve.</param>
public sealed record AbrirAjusteDto(
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    string Motivo,
    IReadOnlyList<LineaDeAjusteDto> Lineas);

/// <summary>Una línea de la petición de alta.</summary>
/// <remarks>
/// <para>
/// <b>La cantidad va con signo</b> y no hay un campo «entrada o salida»: dos sitios donde guardar
/// el mismo signo acaban contradiciéndose.
/// </para>
/// <para>
/// <b>El factor viaja en la petición, y eso tiene fecha de caducidad.</b> Lo que debería decir
/// cuántas unidades base hay en una caja es el catálogo de conversiones de Organización
/// (<c>ConversionUM</c>), pero <b>ningún ítem de la fase 2 le pone puerto</b> y el 2.2 fijó en tres
/// los que Inventario pregunta. Mientras tanto lo aporta quien da el alta, y lo que sí queda
/// garantizado es la coherencia interna de la fila: la cantidad en unidad base la calcula el
/// dominio a partir de estas dos y ningún camino permite enviarla. Está anotado como pregunta
/// abierta del cierre de fase en <c>docs/PLAN.md</c>.
/// </para>
/// </remarks>
/// <param name="UbicacionId">Hueco del almacén.</param>
/// <param name="ArticuloId">Artículo que se mueve.</param>
/// <param name="CantidadIntroducida">Cantidad con signo, tal como se escribe.</param>
/// <param name="UnidadIntroducidaId">Unidad en la que se escribe.</param>
/// <param name="FactorAUnidadBase">Cuántas unidades base hay en una de las introducidas.</param>
/// <param name="CosteUnitario">Coste de una unidad base.</param>
/// <param name="Divisa">Código ISO-4217 del coste (R6).</param>
public sealed record LineaDeAjusteDto(
    Guid UbicacionId,
    Guid ArticuloId,
    decimal CantidadIntroducida,
    Guid UnidadIntroducidaId,
    decimal FactorAUnidadBase,
    decimal CosteUnitario,
    string Divisa);

/// <summary>Un ajuste, como se enseña.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="AlmacenId">Almacén contra el que se ajusta.</param>
/// <param name="FechaDeOperacion">Día al que se imputa.</param>
/// <param name="Motivo">Por qué se ajusta.</param>
/// <param name="Estado">En qué punto de su vida está.</param>
/// <param name="Lineas">Cuántas líneas tiene.</param>
public sealed record AjusteDto(
    Guid Id,
    Guid AlmacenId,
    DateOnly FechaDeOperacion,
    string Motivo,
    string Estado,
    int Lineas);

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
/// <param name="CantidadEnUnidadBase">Cantidad con signo, en la unidad en la que se suma.</param>
/// <param name="CantidadIntroducida">Cantidad tal como se escribió.</param>
/// <param name="UnidadIntroducidaId">Unidad en la que se escribió.</param>
/// <param name="FactorAUnidadBase">El puente entre las dos.</param>
/// <param name="CosteUnitario">Coste de una unidad base.</param>
/// <param name="Divisa">Código ISO-4217 del coste (R6).</param>
/// <param name="DocumentoOrigenTipo">Qué clase de documento la escribió (R13).</param>
/// <param name="DocumentoOrigenId">Cuál (R13).</param>
public sealed record MovimientoDto(
    Guid Id,
    DateOnly FechaDeOperacion,
    Guid AlmacenId,
    bool AlmacenSeOfreceParaLoNuevo,
    Guid UbicacionId,
    Guid ArticuloId,
    decimal CantidadEnUnidadBase,
    decimal CantidadIntroducida,
    Guid UnidadIntroducidaId,
    decimal FactorAUnidadBase,
    decimal CosteUnitario,
    string Divisa,
    string DocumentoOrigenTipo,
    Guid DocumentoOrigenId);
