using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Domain.Sesiones;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>
/// Arma una sesión: reúne los permisos del usuario en la empresa activa, emite el token de acceso
/// y apunta la emisión del de refresco.
/// </summary>
/// <remarks>
/// Está aquí y no repetido en el login, la renovación y el cambio de empresa porque las tres cosas
/// tienen que producir <b>exactamente</b> el mismo token. Escrito tres veces, la tercera copia
/// olvidaría un <i>claim</i> —el de empresa, seguramente— y el resultado sería una sesión que
/// funciona para casi todo: la peor clase de fallo de autorización, porque no se nota.
/// </remarks>
internal sealed class ConstructorDeSesion(
    IRepositorioDeRoles roles,
    IRepositorioDeTokensDeRefresco tokens,
    IEmisorDeTokens emisor,
    TimeProvider reloj)
{
    /// <summary>Arma la sesión y apunta la emisión del token de refresco.</summary>
    /// <param name="usuario">Quién.</param>
    /// <param name="empresaActivaId">Empresa activa, ya comprobada contra sus pertenencias.</param>
    /// <param name="familiaId">Cadena de rotaciones: una nueva en el login, la misma al renovar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal async Task<SesionArmada> ArmarAsync(
        Usuario usuario,
        Guid empresaActivaId,
        Guid familiaId,
        CancellationToken cancelacion)
    {
        Membresia membresia = usuario.EnEmpresa(empresaActivaId)
            ?? throw new InvalidOperationException(
                "Se ha intentado armar una sesión sobre una empresa a la que el usuario no " +
                "pertenece. Quien llama tiene que haberlo comprobado antes: llegar hasta aquí " +
                "significa que la comprobación se ha caído de algún camino.");

        IReadOnlyList<string> permisos = await roles
            .PermisosDeAsync([.. membresia.Roles.Select(rol => rol.RolId)], cancelacion)
            .ConfigureAwait(false);

        TokenDeAcceso acceso = emisor.EmitirAcceso(usuario.Id, usuario.Nombre, empresaActivaId, permisos);
        RefrescoGenerado refresco = emisor.GenerarRefresco();

        var emision = TokenDeRefresco.Emitir(
            usuario.Id,
            familiaId,
            empresaActivaId,
            refresco.Hash,
            reloj.GetUtcNow(),
            emisor.DuracionDelRefresco);

        tokens.Agregar(emision);

        var sesion = new SesionDto(
            acceso.Valor,
            acceso.ExpiraEn,
            usuario.Id,
            usuario.Nombre,
            empresaActivaId,
            [.. usuario.Membresias.Select(pertenencia => pertenencia.EmpresaId)],
            permisos);

        return new SesionArmada(
            new SesionAbierta(sesion, refresco.Valor, emision.ExpiraEn),
            emision);
    }
}

/// <summary>La sesión armada y la emisión que la respalda.</summary>
/// <remarks>
/// La emisión se devuelve aparte porque quien renueva la necesita para enlazar la rotación
/// (<c>presentada.Canjear(emision.Id, ahora)</c>). Sin ese enlace, la cadena se rompe y la
/// detección de reutilización se queda sin familia a la que acudir.
/// </remarks>
/// <param name="Salida">Lo que va al borde: cuerpo y cookie.</param>
/// <param name="Emision">La fila del token de refresco recién apuntada.</param>
internal sealed record SesionArmada(SesionAbierta Salida, TokenDeRefresco Emision);
