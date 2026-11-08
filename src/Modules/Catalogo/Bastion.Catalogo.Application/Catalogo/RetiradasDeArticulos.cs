using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>
/// Retira un artículo: deja de ofrecerse para lo nuevo y sigue resolviendo lo viejo (ADR-0082).
/// </summary>
/// <remarks>
/// <para>
/// <b>No mira ni existencias, ni reservas, ni borradores</b>, y no es un olvido: retirar no borra
/// nada, así que no hay nada que proteger antes. Lo que pasa después lo decide cada consumidor del
/// puerto, que desde aquí lee <c>SoloResuelveLoViejo</c>. Un ajuste, una transferencia o una reserva
/// nuevos lo rechazan; un recuento lo admite, porque es la manera de dar de baja lo que queda.
/// </para>
/// <para>
/// <b>Sin el cerrojo del artículo</b>, como la retirada de los cuatro maestros de instalación. El
/// cerrojo de <c>ModificarArticulo</c> existe por la marca de trazabilidad, que Inventario lee con
/// uno compartido al confirmar. La retirada no la lee nadie al confirmar, solo al abrir, y una
/// carrera con una modificación la resuelve la versión: la segunda en escribir contesta <c>412</c>.
/// </para>
/// </remarks>
public interface IRetirarArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del artículo.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>Vuelve a ofrecer un artículo retirado.</summary>
public interface IReincorporarArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del artículo.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IRetirarArticulo"/>
internal sealed class RetirarArticulo(
    IRepositorioDeArticulos articulos,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : IRetirarArticulo
{
    public Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        CambioDeRetiradaDelArticulo.AplicarAsync(
            articulos, unidadTrabajo, versiones, id, version, retirar: true, cancelacion);
}

/// <inheritdoc cref="IReincorporarArticulo"/>
internal sealed class ReincorporarArticulo(
    IRepositorioDeArticulos articulos,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : IReincorporarArticulo
{
    public Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion) =>
        CambioDeRetiradaDelArticulo.AplicarAsync(
            articulos, unidadTrabajo, versiones, id, version, retirar: false, cancelacion);
}

/// <summary>Lo que hacen igual las dos puertas: buscar, exigir la versión, cambiar y confirmar.</summary>
/// <remarks>
/// Las cinco líneas del <c>CambioDeRetirada</c> de Organización, que es interno a su módulo. Dos
/// copias en un mismo sitio son las que se pueden comparar de un vistazo; y la que se olvidara de
/// <c>Exigir</c> dejaría una escritura sin control de concurrencia que seguiría contestando
/// <c>204</c>.
/// </remarks>
internal static class CambioDeRetiradaDelArticulo
{
    internal static async Task<Resultado> AplicarAsync(
        IRepositorioDeArticulos articulos,
        IUnidadTrabajoDeCatalogo unidadTrabajo,
        IVersionesDeCatalogo versiones,
        Guid id,
        VersionDeRecurso version,
        bool retirar,
        CancellationToken cancelacion)
    {
        Articulo? articulo = await articulos.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (articulo is null)
        {
            return Resultado.Fallo(ErroresDeArticulo.NoEncontrado(id));
        }

        versiones.Exigir(articulo, version);

        // Las dos transiciones son idempotentes en el dominio, así que repetir el verbo deja el
        // mismo estado y no hay que preguntar antes.
        if (retirar)
        {
            articulo.Retirar();
        }
        else
        {
            articulo.Reincorporar();
        }

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
