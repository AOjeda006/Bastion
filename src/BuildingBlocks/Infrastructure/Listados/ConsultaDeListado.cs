using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Mvc;

// Alias porque la propiedad `Orden` de este tipo y el tipo `Orden` se llaman igual, y en
// posicion de tipo gana el miembro. Renombrar la propiedad la alejaria de `Paginacion.Orden`,
// que es lo que produce; el alias deja los dos nombres donde tienen que estar.
using OrdenPedido = Bastion.BuildingBlocks.Contracts.Paginacion.Orden;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Lo que pide un listado por la URL además de su página: el orden, <c>?sort=</c>, y lo que añada
/// cada recurso.
/// </summary>
/// <remarks>
/// <para>
/// Vive en el borde y no en <c>Contracts</c> por dos motivos: los nombres externos solo importan
/// aquí, y las anotaciones solo sirven en lo que MVC enlaza. Un objeto construido a mano en el
/// controlador se saltaría la validación entera.
/// </para>
/// <para>
/// <b>Una acción de listado enlaza tres piezas y no una</b>: la página, <see cref="PaginaPedida"/>
/// (<c>?page=</c> y <c>?size=</c>); el texto, <see cref="FiltroDeTexto"/> (<c>?q=</c>), solo si el
/// recurso lo mira; y esto. Hasta el ítem 3.2 eran un solo tipo, <c>ConsultaPaginada</c>, con los
/// cuatro parámetros, y eso publicaba dos mentiras del contrato de las que no fallan. Los seis
/// listados sin filtro de texto publicaban un <c>?q=</c> que llegaba y no filtraba nada
/// (ADR-0069). Y la exportación de un listado, que saca todas sus páginas, habría publicado un
/// <c>?page=</c> y un <c>?size=</c> que tampoco miraría. Con las piezas separadas, cada acción
/// publica justo lo que lee, y lo dice su firma (ADR-0072).
/// </para>
/// <para>
/// <b>La lista completa de lo que la API acepta por la URL en un listado la vigila una regla del
/// carril funcional</b>, no una costumbre: <c>NingunCriterioSensibleViajaEnLaUrlTests</c> compara
/// los parámetros de todas las acciones de listado contra los nombres declarados allí, y ninguno
/// es un campo de la lista de sensibles (ADR-0025). El día que alguien añada un <c>?nif=</c>
/// «porque es cómodo», ese NIF acabaría en el historial del navegador, en el enlace que se copia
/// y en el registro de acceso del servidor de delante — y esa regla se pone roja antes.
/// </para>
/// <para>
/// <b>No es <c>sealed</c>, y solo para esto:</b> los listados con un criterio propio —la
/// categoría de los artículos, el estado de un documento, el <c>?retiradas=</c> de los cuatro
/// maestros de instalación (<see cref="ConsultaDeMaestro"/>)— lo añaden heredando, para que el
/// parámetro exista solo donde significa algo, sin duplicar la validación del orden.
/// </para>
/// <para>
/// Vive en el bloque común desde el ítem 1.3; estaba duplicada en Identidad y en Organización.
/// </para>
/// </remarks>
public record ConsultaDeListado
{
    /// <summary>Código del error con el que se rechaza un <c>?sort=</c> que no se admite.</summary>
    /// <remarks>
    /// Público porque es contrato: acaba publicado como <c>/errors/orden-no-admitido</c> en el
    /// <c>type</c> del ProblemDetails, y un cliente puede ramificar sobre él.
    /// </remarks>
    public const string CodigoDeOrdenNoAdmitido = "orden-no-admitido";

    /// <summary>Nombre externo del parámetro de orden.</summary>
    public const string NombreDelOrden = "sort";

    /// <summary>Por qué campo se ordena: <c>?sort=codigo</c> o <c>?sort=-codigo</c>.</summary>
    /// <remarks>
    /// Sin anotación de validación, y no por olvido: qué nombres valen depende del recurso, y una
    /// anotación no puede saberlo. Lo comprueba <see cref="APaginacion"/> contra la lista que
    /// declara el propio recurso.
    /// </remarks>
    [FromQuery(Name = NombreDelOrden)]
    public string? Orden { get; init; }

    /// <summary>
    /// La paginación que entiende la capa de aplicación, con el orden y lo que añada el recurso, o
    /// el error si el orden pedido no se admite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El rechazo es un <c>400</c> con la lista de lo que sí vale, no un orden por omisión
    /// silencioso: quien escribió <c>?sort=nombre</c> creyendo que existe merece enterarse de que
    /// no, en vez de recibir una página bien formada ordenada por otra cosa.
    /// </para>
    /// <para>
    /// La página y el texto no salen de aquí: los pone quien compone las piezas,
    /// <see cref="RespuestasDeListado"/>.
    /// </para>
    /// </remarks>
    /// <param name="camposOrdenables">Los nombres que este recurso admite en <c>?sort=</c>.</param>
    public virtual Resultado<Paginacion> APaginacion(IReadOnlySet<string> camposOrdenables)
    {
        ArgumentNullException.ThrowIfNull(camposOrdenables);

        var orden = OrdenPedido.Leer(Orden);

        if (orden is not null && !camposOrdenables.Contains(orden.Campo))
        {
            return Resultado.Fallo<Paginacion>(ErrorDeOperacion.Validacion(
                CodigoDeOrdenNoAdmitido,
                $"Por ese campo no se puede ordenar. Los que valen: {string.Join(", ", camposOrdenables.Order(StringComparer.Ordinal))}.",
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    [NombreDelOrden] = [$"El campo {orden.Campo} no está entre los ordenables."],
                }));
        }

        return Resultado.Correcto(new Paginacion { Orden = orden });
    }
}
