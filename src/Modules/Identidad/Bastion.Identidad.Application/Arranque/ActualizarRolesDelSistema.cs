using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Domain.Roles;

namespace Bastion.Identidad.Application.Arranque;

/// <summary>Lo que el despliegue ha hecho con un rol del sistema.</summary>
/// <param name="Codigo">Código del rol.</param>
/// <param name="Concedidos">Los permisos que la versión desplegada le ha traído.</param>
/// <param name="Retirados">Los que tenía y el catálogo ya no declara.</param>
public sealed record RolDelSistemaActualizado(
    string Codigo,
    IReadOnlyList<string> Concedidos,
    IReadOnlyList<string> Retirados);

/// <summary>
/// Da a cada rol del sistema el catálogo de permisos de la versión desplegada. Lo ejecuta el
/// migrador, en cada despliegue.
/// </summary>
/// <remarks>
/// <para>
/// <b>El hueco que cierra</b> (ADR-0035): <see cref="ISembrarAdministrador"/> solo entra mientras no
/// hay ningún usuario, así que en la segunda versión que se despliega sobre una instalación en
/// marcha no llega a mirar el rol. Los permisos que esa versión declara no los tenía nadie, y nada
/// avisaba: el administrador recibía un <c>403</c> en las pantallas nuevas y podía arreglarlo a
/// mano, porque crear y modificar roles valida contra el catálogo y no contra lo que tiene quien
/// modifica. Una regresión de privilegios silenciosa y recuperable, y aun así una avería.
/// </para>
/// <para>
/// <b>Siempre, y con la edición cerrada.</b> Alinear en cada despliegue deshace cualquier recorte
/// que alguien hiciera al rol del sistema, y por eso <see cref="IModificarRol"/> ya no admite
/// cambiarle los permisos: un recorte que el siguiente despliegue borra sin avisar es peor que no
/// poder hacerlo. Quien quiera un administrador con menos poderes crea un rol propio.
/// </para>
/// <para>
/// <b>Devuelve también lo que no ha cambiado</b>, con las dos listas vacías: el migrador lo escribe
/// igual, porque un paso que solo habla cuando hace algo no distingue «estaba al día» de «no ha
/// mirado».
/// </para>
/// </remarks>
public interface IActualizarRolesDelSistema
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="cancelacion">Cancelación de la operación en curso.</param>
    /// <returns>Un elemento por rol del sistema, en orden de código.</returns>
    Task<IReadOnlyList<RolDelSistemaActualizado>> EjecutarAsync(CancellationToken cancelacion);
}

/// <inheritdoc cref="IActualizarRolesDelSistema"/>
internal sealed class ActualizarRolesDelSistema(
    IRepositorioDeRoles roles,
    ICatalogoDePermisos catalogo,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    IInquilinoActual inquilino) : IActualizarRolesDelSistema
{
    public async Task<IReadOnlyList<RolDelSistemaActualizado>> EjecutarAsync(CancellationToken cancelacion)
    {
        // Sin petición y sin empresa: los roles son de la instalación. El ámbito lo exige la
        // traza, que no escribe una fila sin empresa si nadie ha dicho por qué.
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.ActualizacionDeRolesDelSistema);

        IReadOnlyList<Rol> delSistema = await roles.DelSistemaAsync(cancelacion).ConfigureAwait(false);
        List<RolDelSistemaActualizado> resultado = [];

        foreach (Rol rol in delSistema)
        {
            CambioDePermisos cambio = rol.Alinear(catalogo.Todos);
            resultado.Add(new RolDelSistemaActualizado(rol.Codigo, cambio.Concedidos, cambio.Retirados));
        }

        if (resultado.Exists(actualizado => actualizado.Concedidos.Count > 0 || actualizado.Retirados.Count > 0))
        {
            await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);
        }

        return resultado;
    }
}
