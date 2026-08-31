using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Domain.Empresas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>
/// Bloquea una empresa. Es lo que hace el <c>DELETE</c> del recurso.
/// </summary>
/// <remarks>
/// <para>
/// Borrar de verdad no es una opción, y no por prudencia. Una empresa puede ser un empresario
/// INDIVIDUAL, que es persona física: el art. 32 de la LOPDGDD obliga a BLOQUEAR sus datos —a
/// dejarlos inaccesibles para el tratamiento ordinario y conservados a disposición de jueces y
/// administraciones mientras no prescriban las responsabilidades— y no a destruirlos (R16).
/// </para>
/// <para>
/// Y aunque no hubiera personas de por medio: de una empresa cuelga cada factura emitida con su
/// NIF. Borrar la fila dejaría huérfano un libro registro que hay que conservar cuatro años.
/// </para>
/// </remarks>
public interface IBloquearEmpresa
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <inheritdoc cref="IBloquearEmpresa"/>
internal sealed class BloquearEmpresa(
    IRepositorioDeEmpresas empresas,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones,
    TimeProvider reloj) : IBloquearEmpresa
{
    public async Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion)
    {
        Empresa? empresa = await empresas.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (empresa is null)
        {
            return Resultado.Fallo(ErroresDeEmpresa.NoEncontrada(id));
        }

        versiones.Exigir(empresa, version);

        // Bloquear lo ya bloqueado no es un error: el dominio lo trata como idempotente y no
        // mueve la fecha del primer bloqueo, de la que cuelga el plazo de prescripción. Así, un
        // cliente que reintenta un DELETE que ya había llegado obtiene el mismo 204 y no un 409
        // que le haría pensar que algo va mal.
        empresa.Bloquear(reloj.GetUtcNow());
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
