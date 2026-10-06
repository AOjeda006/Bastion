using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Recuentos;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints.Comun;

/// <summary>
/// Los parámetros de la página de líneas de un recuento: los cuatro de siempre, más <c>?solo=</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>?solo=sin-contar</c> y <c>?solo=teorico-cambiado</c></b> son las dos listas que la pantalla
/// necesita antes de confirmar: lo que falta por contar, que da el <c>422</c>, y lo que cambió desde
/// que se contó, que da el <c>409</c> (ADR-0055 §13).
/// </para>
/// <para>
/// <b>No es un criterio sensible</b> (ADR-0025): es una de dos palabras fijas.
/// <c>NingunCriterioSensibleViajaEnLaUrlTests</c> lo lleva en su lista, con ese motivo.
/// </para>
/// <para>
/// <b>El <c>?q=</c> busca en el lote y en el número de serie</b>, que son el único texto de una
/// línea: lo que se lee en la etiqueta de lo que se tiene en la mano.
/// </para>
/// </remarks>
public sealed record ConsultaDeLineasDeRecuento : ConsultaPaginada
{
    /// <summary>Nombre externo del parámetro.</summary>
    public const string NombreDeSolo = "solo";

    /// <summary>Las líneas a las que se acota, o nulo para todas.</summary>
    [FromQuery(Name = NombreDeSolo)]
    [RegularExpression(
        LosFiltrosDelRecuento.PatronDeLasLineas,
        ErrorMessage = "Solo admite sin-contar o teorico-cambiado.")]
    public string? Solo { get; init; }
}
