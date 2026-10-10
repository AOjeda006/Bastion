using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Inventario.Application.Reservas;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Reservas;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Configuraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.LotesYSeries;
using Bastion.Inventario.Infrastructure.Persistencia.Reservas;
using Bastion.Inventario.Infrastructure.Persistencia.Valoraciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeReservas"/>
/// <param name="contexto">El contexto del módulo, con el filtro de la empresa puesto (R8).</param>
/// <param name="inquilino">
/// De dónde sale la empresa del SQL crudo, que no pasa por el filtro global.
/// </param>
internal sealed class RepositorioDeReservas(InventarioDbContext contexto, IInquilinoActual inquilino)
    : IRepositorioDeReservas
{
    /// <summary>
    /// <b>El cerrojo de reservar y liberar</b>: la fila de la valoración de la clave, en
    /// <c>FOR NO KEY UPDATE</c>, que choca con el de una salida y consigo mismo (ADR-0059 §2).
    /// </summary>
    /// <remarks>
    /// <b>No crea la fila</b>, al revés que el cerrojo de una salida: sin fila no hay físico, y no
    /// hay nada que reservar. Sin punto y coma final, como las demás sentencias crudas (ADR-0043).
    /// </remarks>
    internal const string SqlDelCerrojo =
        "SELECT v.cantidad AS \"Value\"" +
        " FROM inventario.valoraciones AS v" +
        " WHERE v.empresa_id = {0}" +
        " AND v.articulo_id = {1}" +
        " AND v.almacen_id = {2}" +
        " FOR NO KEY UPDATE";

    /// <inheritdoc/>
    public async Task<decimal?> BloquearLaValoracionAsync(ClaveDeValoracion clave, CancellationToken cancelacion)
    {
        // REVIENTA SI NO HAY TRANSACCIÓN, como el cerrojo del recuento: sin ella, el cerrojo se
        // soltaría al acabar esta lectura, y lo reservado podría cambiar antes del `COMMIT`.
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Inventario, así que el cerrojo sobre la " +
                "valoración se soltaría al acabar esta lectura. Quien lo pide tiene que ir dentro de " +
                "`EnTransaccionAsync` de la unidad de trabajo del módulo.");
        }

        List<decimal> cantidades = await contexto.Database
            .SqlQueryRaw<decimal>(SqlDelCerrojo, EmpresaDelInquilino(), clave.ArticuloId, clave.AlmacenId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return cantidades.Count > 0 ? cantidades[0] : null;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado>> BloquearLasValoracionesAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves,
        string divisa,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(claves);

        return LaValoracionDelLibro.BloquearYLeerAsync(contexto, EmpresaDelInquilino(), claves, divisa, cancelacion);
    }

    /// <inheritdoc/>
    public async Task<ClaveDeValoracion?> ClaveDelOrigenAsync(
        TipoDeOrigenDeReserva tipo, Guid id, int linea, CancellationToken cancelacion)
    {
        var fila = await contexto.Reservas
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(reserva => reserva.OrigenTipo == tipo && reserva.OrigenId == id && reserva.OrigenLinea == linea)
            .Select(reserva => new { reserva.ArticuloId, reserva.AlmacenId })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return fila is null ? null : new ClaveDeValoracion(fila.ArticuloId, fila.AlmacenId);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>Los consumos vienen con la reserva: la navegación es <c>AutoInclude</c>.</para>
    /// <para>
    /// <b>Es la lectura que decide, así que lee de verdad</b>: si el contexto ya rastreaba esta
    /// reserva, EF la devolvería tal como la cargó, sin pisarla con lo que otra transacción cambió
    /// después. Antes de leer se suelta, con sus consumos. Sin eso, un ámbito que consume dos veces
    /// la misma línea, con una liberación de otro por medio, la consumiría liberada. Lo encontró la
    /// revisión del 2.13, en su hallazgo 8.
    /// </para>
    /// </remarks>
    public Task<Reserva?> ObtenerPorOrigenAsync(
        TipoDeOrigenDeReserva tipo, Guid id, int linea, CancellationToken cancelacion)
    {
        foreach (EntityEntry<Reserva> cargada in contexto.ChangeTracker.Entries<Reserva>()
            .Where(entrada => entrada.Entity.OrigenTipo == tipo
                && entrada.Entity.OrigenId == id
                && entrada.Entity.OrigenLinea == linea)
            .ToList())
        {
            foreach (ConsumoDeReserva consumo in cargada.Entity.Consumos)
            {
                contexto.Entry(consumo).State = EntityState.Detached;
            }

            cargada.State = EntityState.Detached;
        }

        return contexto.Reservas.FirstOrDefaultAsync(
            reserva => reserva.OrigenTipo == tipo && reserva.OrigenId == id && reserva.OrigenLinea == linea,
            cancelacion);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>El estado guardado se lee por su nombre</b>, porque no es público (ADR-0059 §4). Una
    /// reserva que ya está rastreada vuelve la misma instancia, así que la del origen y la caducada
    /// son una sola cuando coinciden.
    /// </remarks>
    public async Task<IReadOnlyList<Reserva>> CaducadasDeLaClaveAsync(
        ClaveDeValoracion clave, DateTimeOffset ahora, CancellationToken cancelacion) =>
        await contexto.Reservas
            .Where(reserva => reserva.ArticuloId == clave.ArticuloId
                && reserva.AlmacenId == clave.AlmacenId
                && EF.Property<EstadoDeReserva>(reserva, ConfiguracionDeReserva.Estado) == EstadoDeReserva.Activa
                && reserva.CaducaEl != null
                && reserva.CaducaEl <= ahora)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<ClaveDeValoracion, decimal>> ReservadoDeAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(claves);

        if (claves.Count == 0)
        {
            return new Dictionary<ClaveDeValoracion, decimal>();
        }

        Guid[] articulos = [.. claves.Select(clave => clave.ArticuloId)];
        Guid[] almacenes = [.. claves.Select(clave => clave.AlmacenId)];

        List<FilaDeLoReservado> filas = await contexto.Database
            .SqlQueryRaw<FilaDeLoReservado>(LoReservado.SqlDeLasClaves, EmpresaDelInquilino(), ahora, articulos, almacenes)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas.ToDictionary(
            fila => new ClaveDeValoracion(fila.ArticuloId, fila.AlmacenId), fila => fila.Reservado);
    }

    /// <inheritdoc/>
    public void Agregar(Reserva reserva) => contexto.Reservas.Add(reserva);

    /// <inheritdoc/>
    public Task<LotesYSeriesResueltos> BuscarLotesYSeriesAsync(
        IReadOnlyList<CodigoDeUnArticulo> lotes,
        IReadOnlyList<CodigoDeUnArticulo> series,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(lotes);
        ArgumentNullException.ThrowIfNull(series);

        return LosLotesYLasSeries.BuscarAsync(contexto, lotes, series, cancelacion);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Por el ORM</b>, con el filtro de la empresa puesto: con la valoración de la clave
    /// bloqueada, ninguna existencia suya se mueve mientras se lee. Una fila viva por hueco, porque la
    /// unicidad de la existencia no distingue nulos.
    /// </remarks>
    public async Task<IReadOnlyDictionary<HuecoDeExistencias, decimal>> FisicoDeLosHuecosAsync(
        ClaveDeValoracion clave, IReadOnlyCollection<HuecoDeExistencias> huecos, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(huecos);

        Guid[] ubicaciones = [.. huecos.Select(hueco => hueco.UbicacionId).Distinct()];
        HashSet<HuecoDeExistencias> pedidos = [.. huecos];

        var filas = await contexto.Existencias
            .AsNoTracking()
            .Where(existencia => existencia.ArticuloId == clave.ArticuloId
                && existencia.AlmacenId == clave.AlmacenId
                && ubicaciones.Contains(existencia.UbicacionId))
            .Select(existencia => new
            {
                existencia.UbicacionId,
                existencia.LoteId,
                existencia.NumeroDeSerieId,
                existencia.Fisico,
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas
            .Select(fila => (Hueco: new HuecoDeExistencias(fila.UbicacionId, fila.LoteId, fila.NumeroDeSerieId), fila.Fisico))
            .Where(par => pedidos.Contains(par.Hueco))
            .ToDictionary(par => par.Hueco, par => par.Fisico);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Lo mismo que anota el ajuste, en el mismo orden</b>: las guardas, lo pendiente —la reserva,
    /// su consumo y las caducadas—, la existencia, la valoración, y las filas del libro para la unidad
    /// de trabajo. La existencia va antes que la valoración por lo mismo que allí (ADR-0046 §2).
    /// </remarks>
    public async Task AnotarEnElLibroAsync(
        IReadOnlyCollection<MovimientoStock> movimientos, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(movimientos);

        Guid empresaId = EmpresaDelInquilino();

        LaProyeccionDelLibro.ExigirLoQueLaSentenciaNecesita(contexto, empresaId, movimientos);

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        await LaProyeccionDelLibro.MoverAsync(contexto, empresaId, movimientos, cancelacion)
            .ConfigureAwait(false);

        await LaValoracionDelLibro.SumarAsync(contexto, empresaId, movimientos, cancelacion)
            .ConfigureAwait(false);

        contexto.Movimientos.AddRange(movimientos);
    }

    private Guid EmpresaDelInquilino() => inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
        "Se está escribiendo o bloqueando una reserva dentro de un ámbito sin inquilino, y una reserva " +
        "es siempre de una empresa: sin ella la sentencia tocaría la de cualquiera.");
}
