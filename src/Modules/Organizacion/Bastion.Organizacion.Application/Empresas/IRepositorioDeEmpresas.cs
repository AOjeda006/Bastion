using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Empresas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>Acceso a las empresas guardadas.</summary>
/// <remarks>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure
/// (`principios/clean-architecture.md`). Ninguno de sus métodos confirma nada: eso lo decide el
/// caso de uso a través de <c>IUnidadTrabajo</c>.
/// </remarks>
public interface IRepositorioDeEmpresas
{
    /// <summary>La empresa con ese identificador, o nulo si no hay ninguna.</summary>
    Task<Empresa?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si ya hay una empresa con ese NIF.</summary>
    Task<bool> ExisteConNifAsync(string nif, CancellationToken cancelacion);

    /// <summary>Indica si existe la empresa, sin traérsela entera.</summary>
    Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Una página de empresas, con el total.</summary>
    Task<PaginaDe<Empresa>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una empresa nueva. No la graba: eso lo hace la unidad de trabajo.</summary>
    void Agregar(Empresa empresa);
}
