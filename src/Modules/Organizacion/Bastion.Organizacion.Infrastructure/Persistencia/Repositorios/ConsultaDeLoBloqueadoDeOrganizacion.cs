using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Lo que Organización tiene bloqueado: empresas, almacenes y ubicaciones.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sigue uniendo en SQL, y ahí el repositorio anterior tenía razón.</b> Las tres tablas viven en
/// el mismo esquema, así que unirlas en la base es correcto y barato: el filtro, el orden y el
/// recuento los hace PostgreSQL sobre el conjunto. Lo que cambió en el 1.6 es el nivel al que se
/// une — dentro de un módulo sí, entre módulos no— porque cruzar esquemas es la frontera que dejaba
/// fuera del art. 32 a los usuarios y a los terceros bloqueados.
/// </para>
/// <para>
/// <b>El paso por un tipo anónimo no es un rodeo: es la única forma que se traduce.</b> Proyectar
/// cada rama directamente a <see cref="RecursoBloqueado"/> y unir después compila igual de bien y
/// revienta en ejecución con «unable to translate set operation after client projection has been
/// applied»: construir un tipo propio es una proyección de cliente, y EF Core no sabe poner un
/// <c>UNION</c> detrás de una. Con la forma anónima la unión ocurre en SQL y el tipo propio se
/// construye al final. Que esto no vuelva a romperse en silencio lo comprueba
/// <c>LaTraduccionASqlTests</c>, sin contenedor.
/// </para>
/// <para>
/// <b>El <c>Where</c> sobre <c>EstaBloqueado</c> hace falta y no sobra.</b> Dentro del ámbito
/// abierto, el filtro global deja pasar TODO —bloqueado y no bloqueado—, así que sin esta condición
/// esto sería el listado de todo el módulo con otro nombre.
/// </para>
/// <para>
/// <b>El filtro de empresa sigue puesto</b>, y eso es lo que distingue esta consulta de un
/// <c>IgnoreQueryFilters</c>: el ámbito de bloqueo apaga el filtro del bloqueo y ningún otro, así
/// que desde dentro de una empresa se ve lo bloqueado <i>de esa empresa</i> (R8).
/// </para>
/// </remarks>
/// <param name="contexto">El contexto del módulo.</param>
internal sealed class ConsultaDeLoBloqueadoDeOrganizacion(OrganizacionDbContext contexto)
    : IConsultaDeLoBloqueado
{
    /// <inheritdoc/>
    public Task<LoBloqueadoDeUnModulo> PrimerosAsync(
        CriterioDeLoBloqueado criterio, CancellationToken cancelacion) =>
        ConsultasDeLoBloqueado.ResponderAsync(LoBloqueado(contexto), criterio, cancelacion);

    /// <summary>
    /// Las tres tablas bloqueables del módulo, proyectadas a la misma forma y unidas.
    /// </summary>
    /// <remarks>
    /// <b>Estática y visible al ensamblado de pruebas, y no privada.</b> Los demás listados del
    /// módulo ordenan sobre una entidad del modelo, así que el barrido que comprueba que todo orden
    /// declarado se traduce a SQL saca su consulta de partida de <c>contexto.Set&lt;T&gt;()</c>.
    /// Este ordena sobre una proyección, que no es una entidad y no tiene <c>Set</c>: sin una
    /// puerta por donde el barrido pueda pedir la consulta, este listado se quedaría fuera del
    /// único sitio donde se comprueba que su <c>?sort=</c> no es un 500.
    /// </remarks>
    /// <param name="contexto">El contexto del módulo.</param>
    internal static IQueryable<RecursoBloqueado> LoBloqueado(OrganizacionDbContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var empresas = contexto.Empresas
            .Where(empresa => empresa.Bloqueo.EstaBloqueado)
            .Select(empresa => new
            {
                empresa.Id,
                Tipo = TipoDeRecursoBloqueado.Empresa,

                // Una empresa no tiene código: se reconoce por su razón social. Va nulo y no una
                // cadena vacía, que sería «tiene código y está en blanco».
                Codigo = (string?)null,
                Nombre = empresa.RazonSocial,
                Desde = empresa.Bloqueo.Desde!.Value,
                Motivo = empresa.Bloqueo.Motivo!.Value,
            });

        var almacenes = contexto.Almacenes
            .Where(almacen => almacen.Bloqueo.EstaBloqueado)
            .Select(almacen => new
            {
                almacen.Id,
                Tipo = TipoDeRecursoBloqueado.Almacen,
                Codigo = (string?)almacen.Codigo,
                almacen.Nombre,
                Desde = almacen.Bloqueo.Desde!.Value,
                Motivo = almacen.Bloqueo.Motivo!.Value,
            });

        var ubicaciones = contexto.Ubicaciones
            .Where(ubicacion => ubicacion.Bloqueo.EstaBloqueado)
            .Select(ubicacion => new
            {
                ubicacion.Id,
                Tipo = TipoDeRecursoBloqueado.Ubicacion,
                Codigo = (string?)ubicacion.Codigo,

                // Una ubicación puede no tener descripción; entonces su nombre es su código, que es
                // como se la nombra de viva voz («la A-01-3»).
                Nombre = ubicacion.Descripcion ?? ubicacion.Codigo,
                Desde = ubicacion.Bloqueo.Desde!.Value,
                Motivo = ubicacion.Bloqueo.Motivo!.Value,
            });

        return empresas.Concat(almacenes).Concat(ubicaciones)
            .Select(fila => new RecursoBloqueado
            {
                Id = fila.Id,
                Tipo = fila.Tipo,
                Codigo = fila.Codigo,
                Nombre = fila.Nombre,
                BloqueadoEn = fila.Desde,
                Motivo = fila.Motivo,
            });
    }
}
