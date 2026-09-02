using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Divisas;

namespace Bastion.Organizacion.Application.Divisas;

/// <summary>Acceso a las divisas guardadas.</summary>
public interface IRepositorioDeDivisas
{
    /// <summary>La divisa con ese identificador, o nula si no hay ninguna.</summary>
    Task<Divisa?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si ya hay una divisa con ese código.</summary>
    /// <param name="codigo">Código ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteElCodigoAsync(string codigo, CancellationToken cancelacion);

    /// <summary>Indica si existen todas las divisas de la lista.</summary>
    /// <remarks>
    /// En una sola consulta y no una por identificador: quien registra una cotización nombra dos,
    /// y dos viajes a la base para comprobar dos claves ajenas es un viaje de más.
    /// </remarks>
    /// <param name="ids">Identificadores que hay que encontrar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExistenAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancelacion);

    /// <summary>Una página de divisas, con el total.</summary>
    Task<PaginaDe<Divisa>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una divisa nueva.</summary>
    void Agregar(Divisa divisa);
}

/// <summary>Acceso a las cotizaciones guardadas.</summary>
public interface IRepositorioDeTiposDeCambio
{
    /// <summary>La cotización con ese identificador, o nula si no hay ninguna.</summary>
    Task<TipoCambio?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>Indica si ese par de divisas ya tiene cotización ese día.</summary>
    Task<bool> ExisteAsync(
        Guid divisaOrigenId,
        Guid divisaDestinoId,
        DateOnly fecha,
        CancellationToken cancelacion);

    /// <summary>Una página de cotizaciones, con el total.</summary>
    Task<PaginaDe<TipoCambio>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Apunta una cotización nueva.</summary>
    void Agregar(TipoCambio tipoCambio);
}
