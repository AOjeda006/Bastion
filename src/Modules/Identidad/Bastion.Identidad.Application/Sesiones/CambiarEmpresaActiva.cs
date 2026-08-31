using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Usuarios;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Domain.Sesiones;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>Cambia con qué empresa se está operando.</summary>
/// <remarks>
/// <para>
/// <b>Así es como se cambia de empresa, y es la única manera.</b> La empresa activa vive en el
/// token; cambiarla es <b>emitir un token nuevo</b> (§9: «la activa se selecciona al iniciar
/// sesión y se refleja en el token, nunca en un parámetro manipulable»). No hay una cabecera de
/// empresa, ni un parámetro de consulta, ni un campo en el cuerpo de las demás operaciones: hay
/// esta llamada, que devuelve otra sesión.
/// </para>
/// <para>
/// La consecuencia buena es que R8 deja de depender de que cada caso de uso se acuerde. El
/// identificador de empresa entra al sistema por dos sitios —este y el inicio de sesión— y los dos
/// lo contrastan contra las pertenencias del usuario antes de firmarlo.
/// </para>
/// </remarks>
public interface ICambiarEmpresaActiva
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Empresa que pasa a ser la activa.</param>
    /// <param name="refrescoPresentado">El token tal como venía en la cookie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<SesionAbierta>> EjecutarAsync(
        CambiarEmpresaDto peticion,
        string? refrescoPresentado,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICambiarEmpresaActiva"/>
internal sealed class CambiarEmpresaActiva(
    IUsuarioActual usuarioActual,
    IRepositorioDeUsuarios usuarios,
    IRepositorioDeTokensDeRefresco tokens,
    IEmisorDeTokens emisor,
    IInquilinoActual inquilino,
    ConstructorDeSesion constructor,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    TimeProvider reloj) : ICambiarEmpresaActiva
{
    public async Task<Resultado<SesionAbierta>> EjecutarAsync(
        CambiarEmpresaDto peticion,
        string? refrescoPresentado,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // Esta operación es, literalmente, «deja de operar con la empresa del `claim`». La
        // pertenencia que hay que encontrar es la de la empresa DESTINO, que por definición no es
        // la del filtro: con él puesto, cambiar de empresa devolvería siempre «no pertenece».
        // Que se pueda o no cambiar lo sigue decidiendo `usuario.EnEmpresa`, unas líneas más abajo.
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.AutenticacionYSesion);

        DateTimeOffset ahora = reloj.GetUtcNow();

        // Quién pide el cambio sale del token, no del cuerpo. El cuerpo solo dice A QUÉ empresa.
        Usuario? usuario = await usuarios
            .ObtenerAsync(usuarioActual.UsuarioId, cancelacion)
            .ConfigureAwait(false);

        if (usuario is null || !usuario.PuedeIniciarSesion(ahora))
        {
            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Credenciales());
        }

        // Las mismas empresas que el frontal está pintando en el desplegable, y por la misma
        // consulta. Cambiar a una que no esté en esa lista es cambiar a algo que no se ofreció.
        IReadOnlyList<EmpresaDeSesionDto> selector = await constructor
            .ParaElSelectorAsync(usuario, cancelacion)
            .ConfigureAwait(false);

        // Aquí sí se puede decir qué ha pasado: el usuario ya conoce la lista de empresas a las
        // que pertenece, porque se la devuelve su propia sesión. No se revela nada que no tuviera.
        //
        // Las dos condiciones salen por el MISMO error a propósito. «No pertenece» y «pertenece
        // pero está suprimida» son distinguibles desde dentro y no pueden serlo desde fuera: el
        // segundo mensaje confirmaría la existencia de una empresa bloqueada, que es exactamente
        // lo que el 404 del 0.10 se niega a confirmar. Un código nuevo aquí sería el mismo agujero
        // por otra puerta.
        if (usuario.EnEmpresa(peticion.EmpresaId) is null ||
            !selector.Any(empresa => empresa.Id == peticion.EmpresaId))
        {
            return Resultado.Fallo<SesionAbierta>(ErrorDeOperacion.PermisoDenegado(
                "empresa-no-pertenece",
                "No pertenece a esa empresa."));
        }

        // La cadena anterior se corta. Si sobreviviera, quedaría un refresco capaz de devolver un
        // token con la empresa vieja dentro: dos sesiones con dos empresas activas para el mismo
        // navegador, y R8 dependiendo de cuál de las dos cookies llegue primero.
        if (!string.IsNullOrWhiteSpace(refrescoPresentado))
        {
            TokenDeRefresco? presentada = await tokens
                .ObtenerPorHashAsync(emisor.HashearRefresco(refrescoPresentado), cancelacion)
                .ConfigureAwait(false);

            if (presentada is not null && presentada.UsuarioId == usuario.Id)
            {
                foreach (TokenDeRefresco emision in
                    await tokens.DeLaFamiliaAsync(presentada.FamiliaId, cancelacion).ConfigureAwait(false))
                {
                    emision.Revocar(MotivoDeRevocacion.CambioDeEmpresa, ahora);
                }
            }
        }

        SesionArmada armada = await constructor
            .ArmarAsync(usuario, peticion.EmpresaId, selector, Guid.CreateVersion7(), cancelacion)
            .ConfigureAwait(false);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(armada.Salida);
    }
}
