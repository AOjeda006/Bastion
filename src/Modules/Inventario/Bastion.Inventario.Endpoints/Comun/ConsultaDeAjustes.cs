using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Ajustes;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints.Comun;

/// <summary>
/// Los parámetros del listado de ajustes: los cuatro de siempre, más <c>?estado=</c> y
/// <c>?almacen=</c>, como el del recuento (ADR-0065 §6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Los mismos dos acotados que el recuento, con los mismos nombres</b>: la pantalla pregunta lo
/// mismo, «los borradores que quedan» y «los de este almacén», y un nombre distinto para la misma
/// pregunta sería otra cosa que aprender. Ninguno es un criterio sensible (ADR-0025), y
/// <c>NingunCriterioSensibleViajaEnLaUrlTests</c> ya los lleva en su lista.
/// </para>
/// <para>
/// <b>El estado se valida aquí, contra el nombre de cada estado</b>: uno que no existe es un
/// <c>400</c>, no una página vacía. El descartado no es un estado, y no se puede pedir: no sale en
/// ningún listado (ADR-0065 §5).
/// </para>
/// </remarks>
public sealed record ConsultaDeAjustes : ConsultaPaginada
{
    /// <summary>Nombre externo del parámetro del estado.</summary>
    public const string NombreDelEstado = "estado";

    /// <summary>Nombre externo del parámetro del almacén.</summary>
    public const string NombreDelAlmacen = "almacen";

    /// <summary>El estado al que se acota, por su nombre, o nulo para todos.</summary>
    [FromQuery(Name = NombreDelEstado)]
    [RegularExpression(
        LosFiltrosDelAjuste.PatronDelEstado,
        ErrorMessage = "El estado es Borrador, Confirmado o Anulado.")]
    public string? Estado { get; init; }

    /// <summary>El almacén al que se acota, o nulo para todos.</summary>
    [FromQuery(Name = NombreDelAlmacen)]
    public Guid? Almacen { get; init; }
}
