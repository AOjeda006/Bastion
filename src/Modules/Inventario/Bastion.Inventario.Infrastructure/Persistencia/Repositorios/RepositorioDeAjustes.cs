using System.Linq.Expressions;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.LotesYSeries;
using Bastion.Inventario.Infrastructure.Persistencia.Valoraciones;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeAjustes"/>
/// <param name="contexto">El contexto del módulo.</param>
/// <param name="inquilino">De donde sale la empresa cuyas existencias se mueven.</param>
internal sealed class RepositorioDeAjustes(InventarioDbContext contexto, IInquilinoActual inquilino)
    : IRepositorioDeAjustes
{
    private static readonly CriteriosDe<Ajuste> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["fechaDeOperacion"] = (Expression<Func<Ajuste, DateOnly>>)(ajuste => ajuste.FechaDeOperacion),
            ["numero"] = (Expression<Func<Ajuste, long?>>)(ajuste => ajuste.Numero),
        },
        PorOmision = "fechaDeOperacion",
        // De la última a la primera (ADR-0065 §6): quien abre la lista busca lo que se movió hace
        // poco, y el borrador que dejó a medias.
        DescendentePorOmision = true,
        Desempate = ordenada => ordenada.ThenBy(ajuste => ajuste.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return ajuste => EF.Functions.ILike(ajuste.Motivo, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    /// <inheritdoc/>
    /// <remarks>
    /// Las líneas vienen con el documento sin pedirlas: la navegación está marcada
    /// <c>AutoInclude</c> en su configuración, porque son su agregado y confirmarlo sin ellas
    /// escribiría un libro vacío.
    /// </remarks>
    public Task<Ajuste?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Ajustes.FirstOrDefaultAsync(ajuste => ajuste.Id == id, cancelacion);

    /// <inheritdoc/>
    public void Agregar(Ajuste ajuste) => contexto.Ajustes.Add(ajuste);

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Sin las líneas</b>, que el resumen no enseña. Los descartados no salen: los esconde el
    /// filtro con nombre del contexto, igual que el de la empresa (ADR-0065 §5).
    /// </remarks>
    public Task<PaginaDe<Ajuste>> ListarAsync(
        Paginacion paginacion,
        EstadoDeAjuste? estado,
        Guid? almacenId,
        CancellationToken cancelacion)
    {
        IQueryable<Ajuste> consulta = contexto.Ajustes.IgnoreAutoIncludes();

        if (estado is { } elEstado)
        {
            consulta = consulta.Where(ajuste => ajuste.Estado == elEstado);
        }

        if (almacenId is { } elAlmacen)
        {
            consulta = consulta.Where(ajuste => ajuste.AlmacenId == elAlmacen);
        }

        return consulta.PaginarAsync(paginacion, s_criterios, cancelacion);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>La empresa sale del inquilino</b>, como al anotar: la sentencia cruda no pasa por el
    /// filtro global, y bloquearía la valoración de cualquiera.
    /// </remarks>
    public Task<IReadOnlyDictionary<ClaveDeValoracion, SaldoValorado>> BloquearLasValoracionesAsync(
        IReadOnlyCollection<ClaveDeValoracion> claves,
        string divisa,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(claves);

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está bloqueando la valoración dentro de un ámbito sin inquilino, y una valoración " +
            "es siempre de una empresa: sin ella la sentencia bloquearía la de cualquiera.");

        return LaValoracionDelLibro.BloquearYLeerAsync(contexto, empresaId, claves, divisa, cancelacion);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>La empresa sale del inquilino</b>, como al bloquear la valoración: la sentencia que crea
    /// no pasa por el filtro global.
    /// </remarks>
    public Task<LotesYSeriesResueltos> ResolverLotesYSeriesAsync(
        IReadOnlyList<CodigoDeUnArticulo> lotes,
        IReadOnlyList<CodigoDeUnArticulo> series,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(lotes);
        ArgumentNullException.ThrowIfNull(series);

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se están resolviendo lotes y series dentro de un ámbito sin inquilino, y un lote es " +
            "siempre de una empresa: sin ella la sentencia los crearía en la de cualquiera.");

        return LosLotesYLasSeries.ResolverAsync(contexto, empresaId, lotes, series, cancelacion);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Primero la existencia y después el libro, y ese orden da igual</b>: los dos van en la
    /// misma transacción, así que nadie ve el uno sin el otro. La existencia se mueve ya, con una
    /// sentencia; las filas del libro se escriben cuando la unidad de trabajo guarda.
    /// </para>
    /// <para>
    /// <b>Lo que no da igual es que el documento vaya antes que la existencia</b> (ítem 2.8,
    /// ADR-0046 §4), y por eso lo pendiente se guarda aquí, antes de la sentencia. Las guardas del
    /// documento en el motor —el testigo de la R11 y el índice único del inverso— separan a dos
    /// que confirman o anulan el mismo documento a la vez. La del stock separa a dos documentos
    /// que no caben juntos. Si la existencia se moviera antes, la perdedora de una carrera sobre el
    /// mismo documento chocaría con el stock que la ganadora ya se llevó, y contestaría
    /// <c>422</c> <c>stock-insuficiente</c> a quien solo llegó tarde, en vez del <c>412</c> que le
    /// dice que recargue. Con el documento primero, la perdedora choca con su guarda y no llega a
    /// la sentencia.
    /// </para>
    /// <para>
    /// <b>Y la valoración va después de la existencia, que tampoco da igual</b> (ADR-0046 §2). Las
    /// dos rechazan una salida sin stock, cada una con su guarda: la de la existencia lleva el
    /// nombre que el borde traduce a <c>422</c>, y la de la valoración sería un defecto. Con la
    /// existencia primero, choca la que tiene que chocar.
    /// </para>
    /// <para>
    /// <b>La empresa sale del inquilino y no de las filas</b>, y la sentencia comprueba que cada
    /// fila es de ella antes de mandar nada (ADR-0044). Un ámbito sin inquilino no puede anotar: la
    /// sentencia cruda no pasa por el filtro global y sumaría en la empresa de cualquiera.
    /// </para>
    /// </remarks>
    public async Task AnotarEnElLibroAsync(
        IReadOnlyCollection<MovimientoStock> movimientos, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(movimientos);

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está anotando el libro dentro de un ámbito sin inquilino, y una existencia es " +
            "siempre de una empresa: sin ella la sentencia sumaría en la de cualquiera.");

        // LAS GUARDAS, ANTES DE ESCRIBIR NADA: sin transacción, guardar el documento lo
        // confirmaría solo, y la sentencia reventaría después con el documento ya escrito.
        LaProyeccionDelLibro.ExigirLoQueLaSentenciaNecesita(contexto, empresaId, movimientos);

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        await LaProyeccionDelLibro.MoverAsync(contexto, empresaId, movimientos, cancelacion)
            .ConfigureAwait(false);

        // LA VALORACIÓN, DESPUÉS DE LA EXISTENCIA (ADR-0046 §2): una salida sin stock ya ha chocado
        // con su restricción, por el nombre, y aquí solo llega lo que cabe.
        await LaValoracionDelLibro.SumarAsync(contexto, empresaId, movimientos, cancelacion)
            .ConfigureAwait(false);

        contexto.Movimientos.AddRange(movimientos);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Sin rastrear</b>: esto es la mitad de lectura de la R13 y nadie modifica lo que trae —no
    /// podría: la tabla no admite <c>UPDATE</c>—, así que meter el libro entero de un documento en
    /// el rastreador solo costaría memoria.
    /// </para>
    /// <para>
    /// <b>El orden es el del identificador</b>, y no es arbitrario: es un UUID v7, cuyo prefijo es
    /// el instante de creación, así que ordenar por él devuelve las filas en el orden en que se
    /// escribieron. No se ordena por <c>fecha_de_operacion</c> porque todas las de un mismo
    /// documento la comparten —es la del documento— y dejaría el desempate al azar del plan.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<MovimientoStock>> MovimientosDeAsync(
        TipoDeDocumentoOrigen tipo,
        Guid documentoId,
        CancellationToken cancelacion) =>
        await contexto.Movimientos
            .Where(movimiento => movimiento.DocumentoOrigenTipo == tipo
                && movimiento.DocumentoOrigenId == documentoId)
            .OrderBy(movimiento => movimiento.Id)
            .AsNoTracking()
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
}
