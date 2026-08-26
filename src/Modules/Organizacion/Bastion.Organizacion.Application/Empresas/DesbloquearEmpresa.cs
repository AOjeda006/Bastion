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
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IDesbloquearEmpresa"/>
internal sealed class DesbloquearEmpresa(
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo) : IDesbloquearEmpresa
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
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
