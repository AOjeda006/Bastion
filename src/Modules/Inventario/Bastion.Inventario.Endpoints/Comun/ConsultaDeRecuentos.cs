using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Recuentos;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints.Comun;

/// <summary>
/// Los parámetros del listado de recuentos: los cuatro de siempre, más <c>?estado=</c> y
/// <c>?almacen=</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué dos acotados propios y no el <c>?q=</c>.</b> El <c>?q=</c> busca texto en el motivo;
/// estos dos son las dos preguntas que hace la pantalla por igualdad: «los que siguen en curso» y
/// «los de este almacén». La primera es cómo se encuentra el recuento que se está contando.
/// </para>
/// <para>
/// <b>Ninguno es un criterio sensible</b> (ADR-0025): un estado de un documento y el identificador
/// de un almacén de la propia empresa no dicen nada de ninguna persona.
/// <c>NingunCriterioSensibleViajaEnLaUrlTests</c> los lleva en su lista, con ese motivo.
/// </para>
/// <para>
/// <b>El estado se valida aquí, contra el nombre de cada estado</b>, y un valor que no es ninguno
/// es un <c>400</c>, no una página vacía: quien escribió <c>?estado=Abierto</c> creyendo que existe
/// merece enterarse de que no.
/// </para>
/// </remarks>
public sealed record ConsultaDeRecuentos : ConsultaPaginada
{
    /// <summary>Nombre externo del parámetro del estado.</summary>
    public const string NombreDelEstado = "estado";

    /// <summary>Nombre externo del parámetro del almacén.</summary>
    public const string NombreDelAlmacen = "almacen";

    /// <summary>El estado al que se acota, por su nombre, o nulo para todos.</summary>
    [FromQuery(Name = NombreDelEstado)]
    [RegularExpression(
        LosFiltrosDelRecuento.PatronDelEstado,
        ErrorMessage = "El estado es EnCurso, Confirmado, Anulado o Descartado.")]
    public string? Estado { get; init; }

    /// <summary>El almacén al que se acota, o nulo para todos.</summary>
    [FromQuery(Name = NombreDelAlmacen)]
    public Guid? Almacen { get; init; }
}
