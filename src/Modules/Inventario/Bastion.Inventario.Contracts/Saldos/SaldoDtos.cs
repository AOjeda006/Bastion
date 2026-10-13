using Bastion.BuildingBlocks.Contracts.Paginacion;

namespace Bastion.Inventario.Contracts.Saldos;

/// <summary>El saldo de un artículo en un almacén a una fecha, con lo que viaja hacia él dicho aparte.</summary>
/// <remarks>
/// <para>
/// <b>Por artículo y almacén, y nunca por debajo</b> (ADR-0063 §1): el valor solo significa algo
/// sumado a esa clave. Una ubicación puede quedarse con cero unidades y un valor negativo, porque la
/// salida se valora al precio medio del almacén y no al de la ubicación.
/// </para>
/// <para>
/// <b>El tránsito no está sumado a la cantidad ni al valor</b> (ADR-0063 §4): es lo que salió de otro
/// almacén hacia este y a esa fecha no había llegado. No está en el libro, así que sale de las
/// transferencias, y quien quiera el total lo suma sabiendo que lo suma.
/// </para>
/// </remarks>
/// <param name="ArticuloId">El artículo.</param>
/// <param name="AlmacenId">El almacén.</param>
/// <param name="Cantidad">Las unidades base que había al acabar la fecha, que pueden ser cero.</param>
/// <param name="Valor">Lo que valían, en la divisa de la valoración.</param>
/// <param name="CantidadEnTransito">Las unidades base en vuelo hacia este almacén a esa fecha.</param>
/// <param name="ValorEnTransito">Lo que valían al salir de su origen.</param>
/// <param name="Divisa">La de los dos valores: la de la valoración del par.</param>
public sealed record SaldoDto(
    Guid ArticuloId,
    Guid AlmacenId,
    decimal Cantidad,
    decimal Valor,
    decimal CantidadEnTransito,
    decimal ValorEnTransito,
    string Divisa);

/// <summary>Una página de saldos, con la fecha a la que contestan.</summary>
/// <remarks>
/// <b>La fecha va siempre</b> (ADR-0063 §3), también cuando no se pidió ninguna y contesta la de hoy,
/// y también cuando la página sale vacía: por eso va al lado de la página y no dentro de cada saldo.
/// </remarks>
/// <param name="Fecha">El día cuyo final contestan los saldos.</param>
/// <param name="Saldos">La página.</param>
public sealed record SaldosAUnaFechaDto(DateOnly Fecha, PaginaDe<SaldoDto> Saldos);
