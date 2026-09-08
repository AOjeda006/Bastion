using Bastion.BuildingBlocks.Infrastructure.Listados;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Catalogo.Endpoints.Comun;

/// <summary>
/// Los parámetros del listado de artículos: los cuatro de siempre, más <c>?categoria=</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un tipo del módulo y no un quinto campo en <c>ConsultaPaginada</c>.</b> Es el mismo
/// razonamiento que llevó a <c>ConsultaDeMaestro</c> en el ítem 1.7, y por eso este tipo vive aquí
/// y aquél en el bloque común: <c>?retiradas=</c> lo comparten los cuatro maestros de instalación,
/// y <c>?categoria=</c> no lo comparte nadie. Puesto en el modelo común, los doce listados
/// publicarían en <c>docs/api/openapi.json</c> un parámetro que no miran: un cliente lo pasaría, la
/// API contestaría <c>200</c>, y no pasaría nada de nada. Una mentira del contrato que no falla es
/// la peor clase de mentira, porque nadie la descubre.
/// </para>
/// <para>
/// <b>No hereda de <c>ConsultaDeMaestro</c></b>, y no porque sobre: un artículo <b>no se retira</b>.
/// La retirada del ADR-0023 es de los cuatro maestros de instalación —los que son de todas las
/// empresas a la vez—, y un artículo es de una. Publicar aquí un <c>?retiradas=</c> diría que hay
/// artículos retirados que se pueden pedir, y no los hay.
/// </para>
/// <para>
/// <b>Y no es un criterio sensible</b> (ADR-0025): lo que viaja en la URL es el identificador de
/// una rama del árbol de clasificación de la propia empresa, no un dato de una persona. Eso no se
/// afirma de palabra —<c>NingunCriterioSensibleViajaEnLaUrlTests</c> compara la lista entera de
/// parámetros de todos los listados contra la declarada allí, así que este nombre <b>tuvo que
/// añadirse</b> a esa lista, que es donde están escritos los motivos de cada uno.
/// </para>
/// </remarks>
public sealed record ConsultaDeArticulos : ConsultaPaginada
{
    /// <summary>Nombre externo del parámetro.</summary>
    public const string NombreDeLaCategoria = "categoria";

    /// <summary>
    /// Categoría por la que se acota el listado, o nula para traerlos todos.
    /// </summary>
    /// <remarks>
    /// <b>Acota por la categoría dicha y no por su subárbol</b>, y es la consecuencia directa de
    /// haber modelado el árbol como lista de adyacencia: traer el subárbol es un <b>descenso</b>
    /// —un recorrido recursivo por todos los hijos— y lo que este módulo hace de verdad son
    /// ascensos. El motivo entero, con su alternativa costeada, está en <c>docs/PLAN.md</c>.
    /// </remarks>
    [FromQuery(Name = NombreDeLaCategoria)]
    public Guid? Categoria { get; init; }
}
