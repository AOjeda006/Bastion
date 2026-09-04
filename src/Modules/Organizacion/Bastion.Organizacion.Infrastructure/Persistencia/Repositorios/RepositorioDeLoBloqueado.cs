using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Bloqueos;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeLoBloqueado"/>
internal sealed class RepositorioDeLoBloqueado(OrganizacionDbContext contexto) : IRepositorioDeLoBloqueado
{
    private static readonly CriteriosDe<RecursoBloqueado> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["tipo"] = (Expression<Func<RecursoBloqueado, TipoDeRecursoBloqueado>>)(fila => fila.Tipo),
            ["codigo"] = (Expression<Func<RecursoBloqueado, string?>>)(fila => fila.Codigo),
            ["nombre"] = (Expression<Func<RecursoBloqueado, string>>)(fila => fila.Nombre),
            ["fecha"] = (Expression<Func<RecursoBloqueado, DateTimeOffset>>)(fila => fila.BloqueadoEn),
        },

        // Por fecha y de la más reciente a la más antigua: quien abre esta pantalla suele venir de
        // un bloqueo que acaba de hacerse por error, no de uno de hace seis años.
        PorOmision = "fecha",
        DescendentePorOmision = true,

        // Desempata por identificador y no por tipo: aquí conviven tres tablas, y dos filas de
        // tablas distintas pueden compartir fecha al milisegundo si se bloquearon en la misma
        // petición. Sin desempate único, la página 2 repetiría filas de la 1.
        Desempate = ordenada => ordenada.ThenBy(fila => fila.Id),

        // El `?q=` mira código y nombre, que es lo mismo que miran los demás listados y por el
        // mismo motivo: son los dos campos por los que una persona reconoce una ficha, y ninguno
        // de los dos está en la lista de lo que no puede viajar en una URL (ADR-0025). El NIF de
        // una empresa bloqueada NO se busca desde aquí: para eso está `POST /empresas/buscar`.
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return fila => EF.Functions.ILike(fila.Codigo!, patron, Filtros.Escape)
                || EF.Functions.ILike(fila.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<RecursoBloqueado>> ListarAsync(
        Paginacion paginacion, CancellationToken cancelacion) =>
        LoBloqueado(contexto).PaginarAsync(paginacion, s_criterios, cancelacion);

    /// <summary>
    /// Las tres tablas bloqueables del módulo, proyectadas a la misma forma y unidas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Se une en SQL y no en memoria.</b> Tres consultas paginadas por separado no se pueden
    /// juntar en una página: no hay forma de saber cuántas filas traer de cada una sin traerlas
    /// todas, y traerlas todas es exactamente lo que la paginación existe para evitar. Unidas, el
    /// orden, el filtro, el recuento y el corte los hace PostgreSQL sobre el conjunto — un
    /// <c>UNION ALL</c> de tres <c>SELECT</c> con un <c>ORDER BY</c> por fuera.
    /// </para>
    /// <para>
    /// <b>El paso por un tipo anónimo no es un rodeo: es la única forma que se traduce.</b>
    /// Proyectar cada rama directamente a <see cref="RecursoBloqueado"/> y unir después compila
    /// igual de bien y revienta en ejecución con «unable to translate set operation after client
    /// projection has been applied»: construir un tipo propio es una proyección de cliente, y EF
    /// Core no sabe poner un <c>UNION</c> detrás de una. Con la forma anónima la unión ocurre en
    /// SQL y el tipo propio se construye al final, sobre lo que ya viene unido. Que esto no vuelva
    /// a romperse en silencio lo comprueba <c>LaTraduccionASqlTests</c>, sin contenedor.
    /// </para>
    /// <para>
    /// <b>El <c>Where</c> sobre <c>EstaBloqueado</c> hace falta y no sobra.</b> Dentro del ámbito
    /// abierto, el filtro global deja pasar TODO —bloqueado y no bloqueado—, así que sin esta
    /// condición este listado sería el listado de todo el módulo con otro nombre.
    /// </para>
    /// <para>
    /// <b>El filtro de empresa sigue puesto</b>, y eso es lo que distingue esta consulta de un
    /// <c>IgnoreQueryFilters</c>: el ámbito de bloqueo apaga el filtro del bloqueo y ningún otro,
    /// así que desde dentro de una empresa se ve lo bloqueado <i>de esa empresa</i> (R8).
    /// </para>
    /// <para>
    /// <b>Estática y visible al ensamblado de pruebas, y no privada.</b> Los demás listados del
    /// módulo ordenan sobre una entidad del modelo, así que el barrido que comprueba que todo
    /// orden declarado se traduce a SQL saca su consulta de partida de <c>contexto.Set&lt;T&gt;()</c>.
    /// Este ordena sobre una proyección, que no es una entidad y no tiene <c>Set</c>: sin una
    /// puerta por donde el barrido pueda pedir la consulta, este listado se quedaría fuera del
    /// único sitio donde se comprueba que su <c>?sort=</c> no es un 500.
    /// </para>
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
                Nombre = almacen.Nombre,
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

                // Una ubicación puede no tener descripción; entonces su nombre es su código, que
                // es como se la nombra de viva voz («la A-01-3»).
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
