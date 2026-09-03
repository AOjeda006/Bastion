using System.Linq.Expressions;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Identidad.Application.Usuarios;
using Bastion.Identidad.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Identidad.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeUsuarios"/>
internal sealed class RepositorioDeUsuarios(IdentidadDbContext contexto) : IRepositorioDeUsuarios
{
    // Las pertenencias y sus roles vienen SIEMPRE. No es comodidad: sin ellas, armar la sesión
    // haría una consulta por pertenencia dentro del camino del login, y —peor— `usuario.EnEmpresa`
    // devolvería nulo sobre una colección vacía, o sea, «no perteneces a esa empresa» para alguien
    // que sí pertenece. Un fallo de autorización nacido de una carga perezosa.
    private IQueryable<Usuario> ConPertenencias => contexto.Usuarios
        .Include(usuario => usuario.Membresias)
        .ThenInclude(membresia => membresia.Roles);

    public Task<Usuario?> ObtenerAsync(Guid id, CancellationToken cancelacion) =>
        ConPertenencias.FirstOrDefaultAsync(usuario => usuario.Id == id, cancelacion);

    // Se compara el OBJETO entero y no `usuario.Correo.Valor`, por lo mismo que con el NIF en
    // Organización: el correo va con conversor de valor, así que para EF Core la columna es un
    // escalar y no hay ningún `.Valor` en el que entrar. La versión con la cadena compila y
    // revienta en ejecución con «no se pudo traducir la expresión» — en el login.
    public Task<Usuario?> ObtenerPorCorreoAsync(Correo correo, CancellationToken cancelacion) =>
        ConPertenencias.FirstOrDefaultAsync(usuario => usuario.Correo == correo, cancelacion);

    public Task<bool> ExisteConCorreoAsync(Correo correo, CancellationToken cancelacion) =>
        contexto.Usuarios.AnyAsync(usuario => usuario.Correo == correo, cancelacion);

    public async Task<bool> NoHayNingunoAsync(CancellationToken cancelacion) =>
        !await contexto.Usuarios.AnyAsync(cancelacion).ConfigureAwait(false);

    // La consulta va contra la base y no sobre las pertenencias ya cargadas: quien pregunta
    // esto es el usuario que acaba de crear la empresa, y sus propias pertenencias no dicen
    // nada de las de los demás.
    public async Task<bool> SinMiembrosAjenosAsync(
        Guid empresaId,
        Guid salvoUsuarioId,
        CancellationToken cancelacion) =>
        !await contexto.Usuarios
            .Where(usuario => usuario.Id != salvoUsuarioId)
            .AnyAsync(
                usuario => usuario.Membresias.Any(membresia => membresia.EmpresaId == empresaId),
                cancelacion)
            .ConfigureAwait(false);

    // Ordenar POR correo no pone ningún correo en la URL —`?sort=correo` solo nombra el campo—,
    // pero FILTRAR por correo sí: `?q=ana@ejemplo.es` acaba en el historial del navegador, en el
    // enlace que se copia y en el registro de acceso del servidor de delante. Por eso el filtro
    // de este listado mira el nombre y nada más, y buscar por correo va por cuerpo (ADR-0025).
    private static readonly CriteriosDe<Usuario> s_criterios = new()
    {
        Ordenables = new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["correo"] = (Expression<Func<Usuario, Correo>>)(usuario => usuario.Correo),
            ["nombre"] = (Expression<Func<Usuario, string>>)(usuario => usuario.Nombre),
        },
        PorOmision = "correo",
        Desempate = ordenada => ordenada.ThenBy(usuario => usuario.Id),
        Filtro = texto =>
        {
            string patron = Filtros.Contiene(texto);

            return usuario => EF.Functions.ILike(usuario.Nombre, patron, Filtros.Escape);
        },
    };

    public IReadOnlySet<string> CamposOrdenables => s_criterios.CamposOrdenables;

    public Task<PaginaDe<Usuario>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion) =>
        contexto.Usuarios.PaginarAsync(paginacion, s_criterios, cancelacion);

    // El listado se acota a la empresa activa aquí, dentro de la consulta, y no filtrando en
    // memoria lo que ya se ha traído: filtrar después significa que la página 1 puede venir vacía
    // porque sus veinte filas eran de otra empresa.
    public Task<PaginaDe<Usuario>> ListarDeEmpresaAsync(
        Guid empresaId,
        Paginacion paginacion,
        CancellationToken cancelacion) =>
        contexto.Usuarios
            .Where(usuario => usuario.Membresias.Any(membresia => membresia.EmpresaId == empresaId))
            .PaginarAsync(paginacion, s_criterios, cancelacion);

    public void Agregar(Usuario usuario) => contexto.Usuarios.Add(usuario);

    public void Registrar(Membresia membresia) => contexto.Membresias.Add(membresia);
}
