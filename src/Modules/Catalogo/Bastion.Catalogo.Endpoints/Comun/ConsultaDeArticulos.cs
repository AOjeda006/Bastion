using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Catalogo.Endpoints.Comun;

/// <summary>
/// Los parámetros del listado de artículos: el orden, más <c>?categoria=</c> y <c>?retiradas=</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un tipo del módulo y no un campo más en <c>ConsultaDeListado</c>.</b> Es el mismo
/// razonamiento que llevó a <c>ConsultaDeMaestro</c> en el ítem 1.7, y por eso este tipo vive aquí
/// y aquél en el bloque común: <c>?retiradas=</c> lo comparten los cuatro maestros de instalación,
/// y <c>?categoria=</c> no lo comparte nadie. Puesto en el modelo común, los doce listados
/// publicarían en <c>docs/api/openapi.json</c> un parámetro que no miran: un cliente lo pasaría, la
/// API contestaría <c>200</c>, y no pasaría nada de nada. Una mentira del contrato que no falla es
/// la peor clase de mentira, porque nadie la descubre.
/// </para>
/// <para>
/// <b>Desde el 3.6 lleva <c>?retiradas=</c>, y sigue sin heredar de <c>ConsultaDeMaestro</c></b>,
/// que está sellado. El artículo se retira como los cuatro maestros de instalación (ADR-0082), así
/// que el parámetro es el suyo, con el mismo nombre y el mismo efecto: omitido, el listado no los
/// trae. Lo que no comparten es <c>?categoria=</c>, y por eso este tipo sigue siendo del módulo.
/// </para>
/// <para>
/// <b>Y no es un criterio sensible</b> (ADR-0025): lo que viaja en la URL es el identificador de
/// una rama del árbol de clasificación de la propia empresa, no un dato de una persona. Eso no se
/// afirma de palabra —<c>NingunCriterioSensibleViajaEnLaUrlTests</c> compara la lista entera de
/// parámetros de todos los listados contra la declarada allí, así que este nombre <b>tuvo que
/// añadirse</b> a esa lista, que es donde están escritos los motivos de cada uno.
/// </para>
/// </remarks>
public sealed record ConsultaDeArticulos : ConsultaDeListado
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

    /// <summary>
    /// Si el listado trae también los artículos retirados. Omitido, no los trae (ADR-0082).
    /// </summary>
    /// <remarks>
    /// El de <see cref="ConsultaDeMaestro"/>, con su nombre: dos valores y no tres, porque cada
    /// artículo publica su <c>retirada</c> y «solo los retirados» lo compone el cliente.
    /// </remarks>
    [FromQuery(Name = ConsultaDeMaestro.NombreDeLasRetiradas)]
    public bool Retiradas { get; init; }

    /// <inheritdoc />
    public override Resultado<Paginacion> APaginacion(IReadOnlySet<string> camposOrdenables)
    {
        Resultado<Paginacion> pedido = base.APaginacion(camposOrdenables);

        return pedido.EsCorrecto
            ? Resultado.Correcto(pedido.Valor with { IncluyeRetiradas = Retiradas })
            : pedido;
    }
}
