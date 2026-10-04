using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Bastion.Inventario.Application.Transferencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.LotesYSeries;
using Bastion.Inventario.Infrastructure.Persistencia.Transferencias;
using Bastion.Inventario.Infrastructure.Persistencia.Valoraciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeTransferencias"/>
/// <param name="contexto">El contexto del módulo.</param>
/// <param name="inquilino">De donde sale la empresa cuyas existencias se mueven.</param>
internal sealed class RepositorioDeTransferencias(InventarioDbContext contexto, IInquilinoActual inquilino)
    : IRepositorioDeTransferencias
{
    /// <inheritdoc/>
    /// <remarks>Las líneas vienen con el documento, como en el ajuste: la navegación es <c>AutoInclude</c>.</remarks>
    public Task<Transferencia?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Transferencias.FirstOrDefaultAsync(transferencia => transferencia.Id == id, cancelacion);

    /// <inheritdoc/>
    public void Agregar(Transferencia transferencia) => contexto.Transferencias.Add(transferencia);

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado>> BloquearLasValoracionesAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves,
        string divisa,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(claves);

        return LaValoracionDelLibro.BloquearYLeerAsync(contexto, LaEmpresa(), claves, divisa, cancelacion);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>La versión leída es el valor original del testigo</b>, el que el <c>UPDATE</c> llevará en su
    /// <c>WHERE</c>, y la de ahora se proyecta sin rastreo: si se preguntara al rastreador, contestaría
    /// lo mismo que se leyó, y adjuntar la fila daría un testigo a cero (lo cuenta
    /// <c>Versiones</c>). En <c>READ COMMITTED</c>, cada sentencia ve lo confirmado antes de empezar,
    /// así que esta ve lo que dejó quien tenía el cerrojo.
    /// </remarks>
    public async Task<bool> SigueComoSeLeyoAsync(Transferencia transferencia, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(transferencia);

        EntityEntry<Transferencia> entrada = contexto.Entry(transferencia);

        if (entrada.State == EntityState.Detached)
        {
            throw new InvalidOperationException(
                "La transferencia no se leyó en este contexto, así que no hay versión leída con la que " +
                "comparar: el testigo de una fila adjuntada ahora nace a cero.");
        }

        uint leida = (uint)entrada.Property(TestigoDeConcurrencia.Nombre).OriginalValue!;

        uint ahora = await contexto.Transferencias
            .AsNoTracking()
            .Where(fila => fila.Id == transferencia.Id)
            .Select(fila => EF.Property<uint>(fila, TestigoDeConcurrencia.Nombre))
            .SingleAsync(cancelacion)
            .ConfigureAwait(false);

        return leida == ahora;
    }

    /// <inheritdoc/>
    public Task<LotesYSeriesResueltos> ResolverLotesYSeriesAsync(
        IReadOnlyList<CodigoDeUnArticulo> lotes,
        IReadOnlyList<CodigoDeUnArticulo> series,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(lotes);
        ArgumentNullException.ThrowIfNull(series);

        return LosLotesYLasSeries.ResolverAsync(contexto, LaEmpresa(), lotes, series, cancelacion);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>El documento va primero</b>, por lo mismo que en el ajuste (ADR-0053 §10): quien pierde una
    /// carrera sobre la misma transferencia —recibir y anular a la vez— choca con el testigo de su
    /// fila, o con el índice del inverso, y recibe el <c>412</c>, en vez de un <c>422</c> de stock
    /// que no le corresponde.
    /// </para>
    /// <para>
    /// <b>Después, lo que baja antes que lo que sube</b> (ADR-0053 §7). El índice de la serie se
    /// comprueba fila a fila, así que una serie que pasa de una fila a otra tiene que salir de la
    /// primera antes de entrar en la segunda: al enviar, la salida del origen y después el tránsito;
    /// al recibir, el tránsito y después la entrada; en el inverso, lo que niega y después lo que
    /// devuelve al origen. En las cuatro formas la regla es la misma: las filas del libro que restan,
    /// lo que deja de volar, lo que empieza a volar y las filas que suman.
    /// </para>
    /// <para>
    /// <b>Y en cada fila del libro, la existencia antes que la valoración</b>, como en el ajuste
    /// (ADR-0046 §2): una salida sin stock choca con la guarda que el borde traduce.
    /// </para>
    /// </remarks>
    public async Task MoverAsync(LoQueMueveLaTransferencia movido, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(movido);

        Guid empresaId = LaEmpresa();

        // LAS GUARDAS, ANTES DE ESCRIBIR NADA, como en el ajuste.
        LaProyeccionDelLibro.ExigirLoQueLaSentenciaNecesita(contexto, empresaId, movido.Movimientos);

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        MovimientoStock[] restan = [.. movido.Movimientos.Where(fila => fila.CantidadEnUnidadBase < 0m)];
        MovimientoStock[] suman = [.. movido.Movimientos.Where(fila => fila.CantidadEnUnidadBase > 0m)];

        await AnotarAsync(empresaId, restan, cancelacion).ConfigureAwait(false);

        await ElTransito
            .MoverAsync(contexto, empresaId, [.. movido.Transito.Where(vuela => vuela.Cantidad < 0m)], cancelacion)
            .ConfigureAwait(false);

        await ElTransito
            .MoverAsync(contexto, empresaId, [.. movido.Transito.Where(vuela => vuela.Cantidad > 0m)], cancelacion)
            .ConfigureAwait(false);

        await AnotarAsync(empresaId, suman, cancelacion).ConfigureAwait(false);

        contexto.Movimientos.AddRange(movido.Movimientos);
    }

    private async Task AnotarAsync(
        Guid empresaId,
        IReadOnlyCollection<MovimientoStock> movimientos,
        CancellationToken cancelacion)
    {
        await LaProyeccionDelLibro.MoverAsync(contexto, empresaId, movimientos, cancelacion)
            .ConfigureAwait(false);

        await LaValoracionDelLibro.SumarAsync(contexto, empresaId, movimientos, cancelacion)
            .ConfigureAwait(false);
    }

    // LA EMPRESA SALE DEL INQUILINO, como en el ajuste: las sentencias crudas no pasan por el
    // filtro global, y moverían las existencias de cualquiera.
    private Guid LaEmpresa() =>
        inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está moviendo una transferencia dentro de un ámbito sin inquilino, y una existencia " +
            "es siempre de una empresa: sin ella las sentencias tocarían las de cualquiera.");
}
