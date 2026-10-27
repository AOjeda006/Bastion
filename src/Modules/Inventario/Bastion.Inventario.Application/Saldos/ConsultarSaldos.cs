using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Saldos;

namespace Bastion.Inventario.Application.Saldos;

/// <summary>Los nombres con los que viajan los parámetros de la consulta del saldo.</summary>
/// <remarks>
/// <b>Constantes, para que el borde y el error digan el mismo</b>: el <c>400</c> de una fecha futura
/// nombra el campo, y renombrar el parámetro no puede dejarlo nombrando el viejo.
/// </remarks>
public static class LosParametrosDelSaldo
{
    /// <summary>El día cuyo final se contesta.</summary>
    public const string Fecha = "fecha";

    /// <summary>El almacén al que se acota.</summary>
    public const string Almacen = "almacen";

    /// <summary>El artículo al que se acota.</summary>
    public const string Articulo = "articulo";
}

/// <summary>Lee del libro los saldos a una fecha, con su tránsito y su total.</summary>
/// <remarks>
/// <b>No recibe la empresa</b>: sale del inquilino, como en el resto del SQL crudo del módulo
/// (ADR-0044 §9), y quien llama no la puede elegir.
/// </remarks>
public interface ILecturaDeSaldos
{
    /// <summary>Lee una página de saldos.</summary>
    /// <param name="fecha">El día cuyo final se contesta, que no es posterior a hoy.</param>
    /// <param name="almacenId">El almacén al que se acota, o nulo para todos.</param>
    /// <param name="articuloId">El artículo al que se acota, o nulo para todos.</param>
    /// <param name="paginacion">Qué página y de qué tamaño.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La página, con cuántos pares hay en total.</returns>
    Task<PaginaDe<SaldoDto>> LeerAsync(
        DateOnly fecha,
        Guid? almacenId,
        Guid? articuloId,
        Paginacion paginacion,
        CancellationToken cancelacion);
}

/// <summary>Devuelve una página de saldos a una fecha, con la fecha que contesta.</summary>
public interface IConsultarSaldos : IOrdenaPor
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="fecha">El día cuyo final se pide, o nulo para hoy.</param>
    /// <param name="almacenId">El almacén al que se acota, o nulo para todos.</param>
    /// <param name="articuloId">El artículo al que se acota, o nulo para todos.</param>
    /// <param name="paginacion">Qué página y de qué tamaño.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La página con su fecha, o <c>saldo-con-fecha-futura</c>.</returns>
    Task<Resultado<SaldosAUnaFechaDto>> EjecutarAsync(
        DateOnly? fecha,
        Guid? almacenId,
        Guid? articuloId,
        Paginacion paginacion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IConsultarSaldos"/>
/// <remarks>
/// <para>
/// <b>«Hoy» es el día UTC</b> (ADR-0063 §3), como en el resto del módulo: la fecha de un movimiento
/// futuro se rechaza con el mismo reloj.
/// </para>
/// <para>
/// <b>No ordena por nada que se pueda pedir</b>: el orden es el del artículo y el almacén por su
/// identificador, que es estable entre páginas. El nombre de cada uno está en Catálogo y en
/// Organización, y una consulta no cruza esquemas (§4). Un <c>?sort=</c> es un <c>400</c>, y un
/// <c>?q=</c> con algo escrito también, como en los tramos de una tarifa (ADR-0072).
/// </para>
/// </remarks>
/// <param name="saldos">La lectura del libro.</param>
/// <param name="reloj">De dónde sale hoy.</param>
internal sealed class ConsultarSaldos(ILecturaDeSaldos saldos, TimeProvider reloj) : IConsultarSaldos
{
    private static readonly IReadOnlySet<string> s_ordenables = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlySet<string> CamposOrdenables => s_ordenables;

    public async Task<Resultado<SaldosAUnaFechaDto>> EjecutarAsync(
        DateOnly? fecha,
        Guid? almacenId,
        Guid? articuloId,
        Paginacion paginacion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(paginacion);

        DateOnly hoy = DiaDeNegocio.Hoy(reloj);
        DateOnly dia = fecha ?? hoy;

        if (dia > hoy)
        {
            return Resultado.Fallo<SaldosAUnaFechaDto>(ErroresDeSaldo.ConFechaFutura(dia, hoy));
        }

        PaginaDe<SaldoDto> pagina = await saldos
            .LeerAsync(dia, almacenId, articuloId, paginacion, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(new SaldosAUnaFechaDto(dia, pagina));
    }
}
