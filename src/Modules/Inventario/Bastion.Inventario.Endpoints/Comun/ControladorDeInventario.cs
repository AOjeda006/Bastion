using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints.Comun;

/// <summary>
/// Lo que comparten los controladores del módulo: la ruta base, y cómo se convierte un
/// <see cref="Resultado"/> en respuesta.
/// </summary>
/// <remarks>
/// <para>
/// La conversión está aquí y no repetida en cada acción porque es donde se decide que un error de
/// negocio salga con su código de estado y su ProblemDetails. Escrita veinte veces, la número
/// diecisiete devolvería un 400 donde tocaba un 409 y nadie lo vería hasta que un cliente
/// ramificara mal.
/// </para>
/// <para>
/// <b>Trae UN ayudante y no los seis de los otros módulos.</b> Inventario estrena su borde en el
/// ítem 2.4 con una sola acción, y los ayudantes que faltan —listado, creación con
/// <c>Location</c>, lectura con <c>ETag</c>, escritura exigiendo versión— no son ayudantes
/// heredables sin más: cada uno fija una forma de respuesta, y copiarlos antes de tener la acción
/// que la use decidiría por adelantado cómo se publica algo que todavía no se ha diseñado.
/// </para>
/// <para>
/// Deriva de <see cref="ControllerBase"/> y no de <c>Controller</c>: esto es una API, no un sitio
/// con vistas, y <c>Controller</c> arrastra todo el aparato de Razor.
/// </para>
/// <para>
/// <b>Sin <c>[Produces("application/json")]</c>, y no por olvido.</b> Ese atributo no documenta:
/// SUSTITUYE los tipos de contenido de cualquier <c>ObjectResult</c>, y el <c>400</c> automático
/// de <c>[ApiController]</c> es uno. Con él puesto, un error de enlace de modelo saldría como
/// <c>application/json</c> en vez de <c>application/problem+json</c>, así que un cliente que
/// ramificara por el tipo de contenido —lo que manda la RFC 9457— no reconocería como problema
/// justo el error más frecuente.
/// </para>
/// </remarks>
[ApiController]
[Route(RutaBase)]
public abstract class ControladorDeInventario : ControllerBase
{
    /// <summary>Prefijo del módulo, con la versión desde el primer día (§9).</summary>
    public const string Prefijo = "api/v1/inventario";

    /// <summary>Ruta base del módulo: el prefijo más el nombre del controlador.</summary>
    public const string RutaBase = Prefijo + "/[controller]";

    /// <summary>Convierte el desenlace de un caso de uso que devuelve valor en respuesta.</summary>
    /// <typeparam name="T">Lo que devuelve el caso de uso.</typeparam>
    /// <param name="resultado">Desenlace del caso de uso.</param>
    protected IActionResult Responder<T>(Resultado<T> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.EsCorrecto ? Ok(resultado.Valor) : resultado.Error!.AResultadoDeAccion();
    }
}
