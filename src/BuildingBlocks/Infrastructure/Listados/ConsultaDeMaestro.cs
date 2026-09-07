using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Los parámetros de un listado de un maestro que se puede retirar: los cuatro de siempre, más
/// <c>?retiradas=</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un tipo aparte y no un quinto campo en <see cref="ConsultaPaginada"/>.</b> Retirarse
/// solo les pasa a los cuatro maestros de instalación del ADR-0023. Puesto en el modelo común, los
/// doce listados publicarían en <c>docs/api/openapi.json</c> un <c>?retiradas=</c> que no miran:
/// un cliente lo pasaría, la API contestaría <c>200</c>, y no pasaría nada de nada. Una mentira
/// del contrato que no falla es la peor clase de mentira, porque nadie la descubre.
/// </para>
/// <para>
/// <b>Por qué un parámetro y no un extremo aparte.</b> El proyecto ya tiene un listado en un
/// camino propio —el del artículo 32, ADR-0027— y lo tiene por un motivo que aquí no se da: allí
/// lo que se enseña son <b>datos personales reservados</b>, y <c>proteccion-datos.md</c> exige
/// para eso «una vía de acceso separada, nominativa y trazada». Una divisa retirada no tiene datos
/// personales, no le alcanza el artículo 32 y no hay nada que trazar; copiar allí la forma sería
/// pagar cuatro permisos y cuatro rutas por una protección que no hace falta, y además declarar en
/// el contrato que ver una divisa retirada es un acto reservado, que es falso.
/// </para>
/// <para>
/// <b>No es un criterio sensible</b> (ADR-0025): lo que viaja en la URL es un <c>bool</c> sobre el
/// estado operativo de una fila de instalación, no un dato de una persona ni de una empresa. Eso
/// no se afirma de palabra: <c>NingunCriterioSensibleViajaEnLaUrlTests</c> compara la lista entera
/// de parámetros de todos los listados contra la lista declarada, así que este nombre <b>tuvo que
/// añadirse allí</b>, que es donde están escritos los motivos de cada uno.
/// </para>
/// </remarks>
public sealed record ConsultaDeMaestro : ConsultaPaginada
{
    /// <summary>Nombre externo del parámetro.</summary>
    public const string NombreDeLasRetiradas = "retiradas";

    /// <summary>
    /// Si el listado trae también las filas retiradas. Omitido, no las trae.
    /// </summary>
    /// <remarks>
    /// <b>Dos valores y no tres.</b> No hay un «solo las retiradas»: cada elemento publica su
    /// <c>retirada</c>, así que ese listado lo compone el cliente filtrando lo que ya tiene, y un
    /// tercer valor sería una segunda manera de decir lo mismo — con su propia rama en el
    /// repositorio, su propia traducción a SQL y su propio caso que probar.
    /// </remarks>
    [FromQuery(Name = NombreDeLasRetiradas)]
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
