using Bastion.BuildingBlocks.Infrastructure.Listados;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Catalogo.Endpoints.Comun;

/// <summary>
/// Los parámetros del listado de tarifas: los cuatro de siempre, más <c>?codigo=</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un acotado propio y no el <c>?q=</c>.</b> Son dos preguntas distintas: <c>?q=</c> es
/// «los que digan esto» y busca por texto parcial en el código y el nombre; <c>?codigo=</c> es
/// «los tramos de ESTA tarifa», por igualdad exacta. La segunda es la que de verdad se hace aquí,
/// porque una tarifa son varias filas con el mismo código y verlas juntas —en orden de vigencia—
/// es cómo se comprueba que la sucesión no ha dejado ningún día sin cubrir.
/// </para>
/// <para>
/// <b>No es un criterio sensible</b> (ADR-0025): lo que viaja en la URL es el código de una lista
/// de precios de la propia empresa —<c>PVP</c>, <c>MAYORISTA</c>—, no un dato de ninguna persona.
/// Lo que queda escrito en el registro de acceso es «alguien listó los tramos de la tarifa PVP»,
/// que es exactamente lo que un ERP tiene que poder decir en voz alta. Y eso no se afirma de
/// palabra: <c>NingunCriterioSensibleViajaEnLaUrlTests</c> compara la lista entera de parámetros de
/// todos los listados contra la declarada allí, así que este nombre <b>tuvo que añadirse</b> a esa
/// lista, que es donde están escritos los motivos de cada uno.
/// </para>
/// </remarks>
public sealed record ConsultaDeTarifas : ConsultaPaginada
{
    /// <summary>Nombre externo del parámetro.</summary>
    public const string NombreDelCodigo = "codigo";

    /// <summary>
    /// Código de la tarifa por el que se acota el listado, o nulo para traerlas todas.
    /// </summary>
    /// <remarks>
    /// Se normaliza a mayúsculas antes de consultar, que es la forma en la que está guardado:
    /// acotar por lo que escribió el usuario dejaría fuera sus propias tarifas por haberlas
    /// tecleado en minúscula.
    /// </remarks>
    [FromQuery(Name = NombreDelCodigo)]
    public string? Codigo { get; init; }
}
