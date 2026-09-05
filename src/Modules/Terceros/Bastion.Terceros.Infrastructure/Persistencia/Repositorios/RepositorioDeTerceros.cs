using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Terceros.Application.Terceros;
using Bastion.Terceros.Domain.Terceros;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bastion.Terceros.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeTerceros"/>
internal sealed partial class RepositorioDeTerceros(
    TercerosDbContext contexto,
    ILogger<RepositorioDeTerceros> registro) : IRepositorioDeTerceros
{
    public Task<Tercero?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Terceros.FirstOrDefaultAsync(tercero => tercero.Id == id, cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// <b>Una consulta, un índice, una fila, y la misma en los dos desenlaces.</b> No hay un
    /// camino que cargue el bloqueo y otro que se lo ahorre: se lee siempre la misma columna de la
    /// misma fila, así que las dos respuestas de conflicto tardan lo mismo <b>por construcción</b>,
    /// y no porque alguien haya igualado los tiempos midiendo. El tiempo es el tercer canal por el
    /// que se filtra si alguien está bloqueado, después del cuerpo y del código de estado.
    /// </para>
    /// <para>
    /// <b>Aquí SÍ se sabe cuál de los dos era, y aquí es donde queda escrito.</b> La proyección
    /// trae el estado del bloqueo para poder anotarlo; lo que sube por el puerto es un booleano.
    /// El art. 32 obliga a saber quién ha mirado datos reservados y cuándo — no a contárselo a
    /// quien rellenó el formulario.
    /// </para>
    /// <para>
    /// Sin cláusula de estado en el <c>Where</c>, y no es un olvido: qué se ve lo decide el filtro
    /// de R16 según haya o no un ámbito abierto. Repetirlo aquí haría creer que la decisión vive
    /// en esta consulta, de donde se puede caer olvidándola en la siguiente.
    /// </para>
    /// </remarks>
    public async Task<bool> ExisteLaIdentificacionAsync(
        Guid empresaId,
        string pais,
        string numero,
        CancellationToken cancelacion)
    {
        bool? bloqueado = await contexto.Terceros
            .Where(tercero => tercero.EmpresaId == empresaId
                && tercero.Identificacion.Pais == pais
                && tercero.Identificacion.Numero == numero
                && !tercero.Bloqueo.EstaBloqueado)
            .Select(tercero => (bool?)tercero.Bloqueo.EstaBloqueado)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (bloqueado is null)
        {
            return false;
        }

        Anotar(registro, empresaId, bloqueado.Value);

        return true;
    }

    // SIN el identificador fiscal dentro, y es deliberado: un NIF es un dato personal y el
    // registro se agrega, se exporta y se conserva con menos ceremonia que la base de datos. Lo
    // que hace falta para responder «se miró una ficha reservada» ya está: la empresa, el
    // instante, y que lo que estorbaba estaba bloqueado. Quién miró lo pone la línea que
    // `AccesoALoBloqueado` escribe al abrir el ámbito, un instante antes y con el mismo usuario.
    [LoggerMessage(
        EventId = 8400,
        Level = LogLevel.Information,
        Message = "Alta de tercero rechazada por identificador ocupado. Empresa: {EmpresaId}. " +
                  "La ficha que lo ocupa está bloqueada: {Bloqueada}.")]
    private static partial void Anotar(ILogger registro, Guid empresaId, bool bloqueada);

    // El identificador fiscal NO está entre los ordenables ni en el filtro del `?q=`, y es la
    // decisión del ADR-0025 escrita donde se puede desobedecer: `?q=` viaja en la URL, o sea en
    // el historial del navegador, en el enlace que se copia y en el registro de acceso del
    // servidor de delante. Aquí muerde más que en empresas, porque el identificador de un cliente
    // es muy a menudo el DNI de una persona. Buscar por él va por cuerpo, con `POST .../buscar`.
    private static readonly CriteriosDe<Tercero> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["razonSocial"] = (Expression<Func<Tercero, string>>)(tercero => tercero.RazonSocial),
        },
        PorOmision = "razonSocial",
        Desempate = ordenada => ordenada.ThenBy(tercero => tercero.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return tercero => EF.Functions.ILike(tercero.RazonSocial, patron, Filtros.Escape)
                || (tercero.NombreComercial != null
                    && EF.Functions.ILike(tercero.NombreComercial, patron, Filtros.Escape));
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Tercero>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Terceros.PaginarAsync(paginacion, s_criterios, cancelacion);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// El recorrido va por el identificador y no por la razón social, y eso decide también el
    /// orden. El identificador es un GUID v7: sus primeros bits son el instante de creación, o sea
    /// que ascendente por identificador ES por antigüedad de alta. Y como clave de recorrido tiene
    /// lo que a la razón social le falta: es única, así que el «después de esto» no necesita
    /// desempate ni comparación de tuplas.
    /// </para>
    /// <para>
    /// Se leen <c>tamanio + 1</c> filas para distinguir «no hay más» de «hay más y el cliente aún
    /// no lo sabe» sin contar el conjunto filtrado entero. La de más se descarta; nunca se entrega.
    /// </para>
    /// </remarks>
    public async Task<TramoDe<Tercero>> BuscarAsync(
        CriterioDeTerceros criterio,
        Guid? desde,
        int tamanio,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(criterio);

        IQueryable<Tercero> consulta = contexto.Terceros;

        // Miembro a miembro, que es lo que EF Core sabe traducir de un tipo complejo — y lo
        // contrario de lo que hay que hacer con el NIF de una empresa, que es un valor convertido
        // y va comparado entero. Los dos casos compilan; el que no toca revienta en ejecución.
        if (criterio.Pais is { } pais && criterio.Numero is { } numero)
        {
            consulta = consulta.Where(tercero =>
                tercero.Identificacion.Pais == pais && tercero.Identificacion.Numero == numero);
        }

        if (criterio.Nombre is { } texto)
        {
            string patron = Filtros.Contiene(texto);

            consulta = consulta.Where(
                tercero => EF.Functions.ILike(tercero.RazonSocial, patron, Filtros.Escape)
                    || (tercero.NombreComercial != null
                        && EF.Functions.ILike(tercero.NombreComercial, patron, Filtros.Escape)));
        }

        if (desde is { } posicion)
        {
            consulta = consulta.Where(tercero => tercero.Id.CompareTo(posicion) > 0);
        }

        List<Tercero> leidos = await consulta
            .OrderBy(tercero => tercero.Id)
            .Take(tamanio + 1)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        bool hayMas = leidos.Count > tamanio;
        List<Tercero> entregados = hayMas ? leidos[..tamanio] : leidos;

        return new TramoDe<Tercero>(
            entregados,
            tamanio,
            hayMas ? Cursores.De(entregados[^1].Id) : null);
    }

    public void Agregar(Tercero tercero) => contexto.Terceros.Add(tercero);
}
