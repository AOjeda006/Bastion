using System.ComponentModel.DataAnnotations;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Inventario.Application.Transferencias;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Inventario.Endpoints.Comun;

/// <summary>
/// Los parámetros del listado de transferencias: el orden, más <c>?estado=</c>,
/// <c>?origen=</c> y <c>?destino=</c> (ADR-0068 §1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Dos almacenes y no uno</b>, porque una transferencia tiene dos: quien recibe busca lo que llega
/// al suyo, y quien envía, lo que sale del suyo. Juntos dan lo que va de uno a otro. Un solo
/// <c>?almacen=</c> tendría que decidir si es el origen, el destino o cualquiera de los dos, y la
/// pantalla no podría preguntar lo que pregunta. Ninguno es un criterio sensible (ADR-0025): son el
/// <c>Guid</c> de un almacén de la propia empresa, opaco como el del ajuste.
/// </para>
/// <para>
/// <b>El estado se valida aquí, contra el nombre de cada estado</b>: uno que no existe es un
/// <c>400</c>, no una página vacía. La descartada no es un estado, y no se puede pedir: no sale en
/// ningún listado (ADR-0068 §5).
/// </para>
/// <para>
/// <b>No tiene <c>?q=</c></b>, como las líneas de tarifa: el repositorio no declara filtro de
/// texto, porque el alta no tiene ningún campo que escriba una persona. Hasta el ítem 3.2 lo
/// publicaba, llegaba y no filtraba nada (ADR-0069); desde entonces la acción no lo enlaza, y
/// uno con algo escrito es un <c>400</c> <c>filtro-no-admitido</c> (ADR-0072).
/// </para>
/// </remarks>
public sealed record ConsultaDeTransferencias : ConsultaDeListado
{
    /// <summary>Nombre externo del parámetro del estado.</summary>
    public const string NombreDelEstado = "estado";

    /// <summary>Nombre externo del parámetro del almacén de origen.</summary>
    public const string NombreDelOrigen = "origen";

    /// <summary>Nombre externo del parámetro del almacén de destino.</summary>
    public const string NombreDelDestino = "destino";

    /// <summary>El estado al que se acota, por su nombre, o nulo para todos.</summary>
    [FromQuery(Name = NombreDelEstado)]
    [RegularExpression(
        LosFiltrosDeLaTransferencia.PatronDelEstado,
        ErrorMessage = "El estado es Borrador, Enviada, Recibida o Anulada.")]
    public string? Estado { get; init; }

    /// <summary>El almacén del que sale, o nulo para todos.</summary>
    [FromQuery(Name = NombreDelOrigen)]
    public Guid? Origen { get; init; }

    /// <summary>El almacén al que llega, o nulo para todos.</summary>
    [FromQuery(Name = NombreDelDestino)]
    public Guid? Destino { get; init; }
}
