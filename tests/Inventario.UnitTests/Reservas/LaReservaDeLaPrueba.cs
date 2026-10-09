using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;

namespace Bastion.Inventario.UnitTests.Reservas;

/// <summary>
/// Una reserva de una línea de pedido, y lo que hace el caso de uso para consumirla: valora cada
/// línea al precio medio de la clave y no resuelve ningún lote.
/// </summary>
/// <remarks>
/// <b>No sustituye a la base</b>: el cerrojo de la valoración, el físico de cada hueco y la
/// caducidad que se escribe al pasar los ve el carril de integración. Aquí se ve la máquina de
/// estados y la aritmética de lo pendiente, sin encender nada.
/// </remarks>
internal static class LaReservaDeLaPrueba
{
    internal const string Divisa = "EUR";

    internal const decimal PrecioMedio = 2.5m;

    internal static readonly DateTimeOffset Momento = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    internal static readonly DateOnly Hoy = new(2026, 3, 14);

    internal static readonly Guid Empresa = Guid.Parse("0f6a1c1e-0000-4000-8000-000000000001");

    internal static readonly Guid Pedido = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000e1");

    internal static readonly Guid Almacen = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000a1");

    internal static readonly Guid Ubicacion = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000a2");

    internal static readonly Guid OtraUbicacion = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000a3");

    internal static readonly Guid Articulo = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000c1");

    internal static readonly Guid Unidad = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000d1");

    internal static readonly Guid Albaran = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000f1");

    internal static readonly Guid OtroAlbaran = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000f2");

    /// <summary>Una reserva de la primera línea del pedido, hecha en <see cref="Momento"/>.</summary>
    /// <param name="cantidad">Cuánto aparta, en unidad base.</param>
    /// <param name="caducaEl">Cuándo deja de apartar, o <c>null</c> si no caduca.</param>
    /// <returns>La reserva, activa.</returns>
    internal static Reserva UnaReservaDe(decimal cantidad, DateTimeOffset? caducaEl = null) =>
        Reserva.Reservar(
            Empresa,
            TipoDeOrigenDeReserva.PedidoDeVenta,
            Pedido,
            1,
            Articulo,
            Almacen,
            cantidad,
            Unidad,
            caducaEl,
            Momento);

    /// <summary>Una línea de consumo del hueco de la prueba.</summary>
    /// <param name="cantidad">Cuánto saca, en unidad base.</param>
    /// <returns>La línea.</returns>
    internal static LineaDeConsumo UnaLinea(decimal cantidad) => new(Ubicacion, cantidad, null, null);

    /// <summary>
    /// Valora las líneas al <see cref="PrecioMedio"/>, como lo haría el caso de uso con una clave
    /// que tuviera ese precio: cada una resta lo que saca por el precio.
    /// </summary>
    /// <param name="lineas">Las líneas del consumo.</param>
    /// <returns>Una valoración por línea, en su orden.</returns>
    internal static IReadOnlyList<LineaValorada> AlPrecioMedio(IReadOnlyList<LineaDeConsumo> lineas) =>
    [
        .. lineas.Select(linea => new LineaValorada(
            Importe.De(-linea.Cantidad * PrecioMedio, Divisa),
            PrecioUnitario.De(PrecioMedio, Divisa))),
    ];

    /// <summary>Consume la reserva con un albarán, hoy, sin lotes ni series.</summary>
    /// <param name="reserva">La reserva.</param>
    /// <param name="albaran">El albarán que sale.</param>
    /// <param name="momento">Ahora.</param>
    /// <param name="lineas">Lo que saca, por hueco.</param>
    /// <returns>Las filas del libro que hay que escribir.</returns>
    internal static IReadOnlyList<MovimientoStock> Consumir(
        Reserva reserva,
        Guid albaran,
        DateTimeOffset momento,
        params LineaDeConsumo[] lineas) =>
        reserva.Consumir(
            TipoDeDocumentoOrigen.Albaran,
            albaran,
            DateOnly.FromDateTime(momento.UtcDateTime),
            lineas,
            AlPrecioMedio(lineas),
            LotesYSeriesResueltos.Ninguno,
            Divisa,
            momento);
}
