using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Empresas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>
/// Levanta el bloqueo de una empresa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Permiso propio, distinto del de bloquear.</b> Hacer y deshacer no son la misma facultad:
/// bloquear es una medida de contención que puede tomar quien administra el día a día, mientras
/// que devolver a la actividad una ficha bloqueada por el art. 32 de la LOPDGDD es una decisión
/// que se toma sabiendo por qué se bloqueó. Con un único permiso «gestionar empresas», esa
/// distinción no se podría ni expresar.
/// </para>
/// <para>
/// Es idempotente, como el bloqueo: desbloquear lo que ya está activo devuelve <c>204</c>.
/// </para>
/// </remarks>
public interface IDesbloquearEmpresa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <remarks>
    /// <b>No lleva versión, y desde el 0.10 no puede llevarla.</b> El <c>If-Match</c> se cita
    /// leyendo antes el recurso, y un recurso bloqueado no se puede leer por ningún camino
    /// ordinario: la precondición pediría una llave que no existe (ADR-0017).
    /// </remarks>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IDesbloquearEmpresa"/>
internal sealed class DesbloquearEmpresa(
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IAccesoALoBloqueado bloqueados) : IDesbloquearEmpresa
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        // El ÚNICO camino ordinario que necesita ver lo bloqueado, y por una razón de lógica:
        // para levantar un bloqueo hay que poder leer lo que está bloqueado. Es una apertura
        // declarada, con su motivo de la lista cerrada y anotada en el registro — no un
        // `IgnoreQueryFilters`, que además apagaría de paso el filtro de empresa.
        using IDisposable _ = bloqueados.ViendoLoBloqueado(MotivoParaVerLoBloqueado.AdministracionDelBloqueo);

        Empresa? empresa = await empresas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (empresa is null)
        {
            return Resultado.Fallo(ErroresDeEmpresa.NoEncontrada(id));
        }

        empresa.Desbloquear();
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
