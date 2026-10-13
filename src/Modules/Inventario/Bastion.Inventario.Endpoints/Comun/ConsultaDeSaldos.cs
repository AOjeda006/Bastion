using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Saldos;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints.Comun;

/// <summary>
/// Los parámetros de la consulta del saldo: los cuatro de siempre, más <c>?fecha=</c>,
/// <c>?almacen=</c> y <c>?articulo=</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La paginación viaja como en todos los listados</b>, con <c>page</c> y <c>size</c>: el ADR-0063
/// §8 la nombra «página y tamaño», que son los nombres de dentro.
/// </para>
/// <para>
/// <b>Ninguno es un criterio sensible</b> (ADR-0025): un día, y el identificador de un almacén o de
/// un artículo de la propia empresa, no dicen nada de ninguna persona.
/// <c>NingunCriterioSensibleViajaEnLaUrlTests</c> los lleva en su lista, con ese motivo.
/// </para>
/// <para>
/// <b>Una fecha ilegible es un <c>400</c> del enlace del modelo</b>, y una posterior a hoy, un
/// <c>400</c> con su <c>type</c>, <c>saldo-con-fecha-futura</c>, que decide el caso de uso con su
/// reloj.
/// </para>
/// </remarks>
public sealed record ConsultaDeSaldos : ConsultaPaginada
{
    /// <summary>El día cuyo final se pide, o nulo para hoy.</summary>
    [FromQuery(Name = LosParametrosDelSaldo.Fecha)]
    public DateOnly? Fecha { get; init; }

    /// <summary>El almacén al que se acota, o nulo para todos.</summary>
    [FromQuery(Name = LosParametrosDelSaldo.Almacen)]
    public Guid? Almacen { get; init; }

    /// <summary>El artículo al que se acota, o nulo para todos.</summary>
    [FromQuery(Name = LosParametrosDelSaldo.Articulo)]
    public Guid? Articulo { get; init; }
}
