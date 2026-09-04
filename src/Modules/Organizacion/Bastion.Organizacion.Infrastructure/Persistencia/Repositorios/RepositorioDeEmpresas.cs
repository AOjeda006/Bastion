using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Empresas;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeEmpresas"/>
internal sealed class RepositorioDeEmpresas(OrganizacionDbContext contexto) : IRepositorioDeEmpresas
{
    public Task<Empresa?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Empresas.FirstOrDefaultAsync(empresa => empresa.Id == id, cancelacion);

    // Se compara el OBJETO entero, no `empresa.Nif.Valor`. El NIF está mapeado con un conversor
    // de valor, así que para EF Core la columna es un escalar y no hay ningún `.Valor` en el que
    // entrar: la versión con la cadena compilaba igual de bien y reventaba en ejecución con «no
    // se pudo traducir la expresión», o sea, un 500 en cada alta de empresa. Un doble en memoria
    // habría evaluado ese `.Valor` en LINQ-to-Objects y habría dado verde.
    //
    // Comparar el objeto también usa el índice único, porque el conversor produce exactamente el
    // valor normalizado que está en la columna.
    public Task<bool> ExisteConNifAsync(Nif nif, CancellationToken cancelacion) =>
        contexto.Empresas.AnyAsync(empresa => empresa.Nif == nif, cancelacion);

    // Sin cláusula de estado: el filtro de R16 ya deja fuera lo bloqueado, y repetirlo aquí
    // haría creer que la protección vive en esta consulta — de donde se puede caer olvidándola
    // en la siguiente.
    public Task<bool> EstaActivaAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Empresas.AnyAsync(empresa => empresa.Id == id, cancelacion);

    // El NIF NO está aquí, ni entre los ordenables ni en el filtro, y es la decisión del
    // ADR-0025 escrita en el sitio donde se puede desobedecer: `?q=` viaja en la URL, o sea en el
    // historial del navegador, en el enlace que se copia y en el registro de acceso del servidor
    // de delante. Buscar empresas por NIF va por cuerpo, con `POST .../buscar`.
    private static readonly CriteriosDe<Empresa> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["razonSocial"] = (Expression<Func<Empresa, string>>)(empresa => empresa.RazonSocial),
        },
        PorOmision = "razonSocial",
        Desempate = ordenada => ordenada.ThenBy(empresa => empresa.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return empresa => EF.Functions.ILike(empresa.RazonSocial, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    // Orden estable y explícito. Sin `ORDER BY`, PostgreSQL no promete ningún orden entre
    // consultas, así que la página 2 podría repetir o saltarse filas de la 1 sin que nadie
    // hubiera tocado nada; el desempate por identificador lo pone el paginador común.
    public Task<PaginaDe<Empresa>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Empresas.PaginarAsync(paginacion, s_criterios, cancelacion);

    /// <summary>
    /// La búsqueda del ADR-0025: criterio por cuerpo y recorrido por cursor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El recorrido va por el identificador y no por la razón social</b>, y eso decide también
    /// el orden. El identificador es un GUID v7: sus primeros bits son el instante de creación, o
    /// sea que ascendente por identificador ES por antigüedad de alta, que es un orden con
    /// sentido y no un capricho. Y como clave de recorrido tiene lo que a la razón social le
    /// falta: es única, así que el «después de esto» no necesita desempate ni comparación de
    /// tuplas, y cae sobre la clave primaria.
    /// </para>
    /// <para>
    /// <b>Se leen `tamanio + 1` filas</b>. Es lo que distingue «no hay más» de «hay más y el
    /// cliente aún no lo sabe» sin contar el conjunto filtrado entero — que es precisamente el
    /// recorrido que un cursor viene a evitar. La de más se descarta; nunca se entrega.
    /// </para>
    /// </remarks>
    public async Task<TramoDe<Empresa>> BuscarAsync(
        CriterioDeEmpresas criterio,
        Guid? desde,
        int tamanio,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(criterio);

        IQueryable<Empresa> consulta = contexto.Empresas;

        // El objeto entero contra la columna, por lo mismo que en `ExisteConNifAsync`.
        if (criterio.Nif is { } nif)
        {
            consulta = consulta.Where(empresa => empresa.Nif == nif);
        }

        if (criterio.RazonSocial is { } texto)
        {
            string patron = Filtros.Contiene(texto);

            consulta = consulta.Where(
                empresa => EF.Functions.ILike(empresa.RazonSocial, patron, Filtros.Escape));
        }

        if (desde is { } posicion)
        {
            consulta = consulta.Where(empresa => empresa.Id.CompareTo(posicion) > 0);
        }

        List<Empresa> leidas = await consulta
            .OrderBy(empresa => empresa.Id)
            .Take(tamanio + 1)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        bool hayMas = leidas.Count > tamanio;
        List<Empresa> entregadas = hayMas ? leidas[..tamanio] : leidas;

        return new TramoDe<Empresa>(
            entregadas,
            tamanio,
            hayMas ? Cursores.De(entregadas[^1].Id) : null);
    }

    public void Agregar(Empresa empresa) => contexto.Empresas.Add(empresa);
}
