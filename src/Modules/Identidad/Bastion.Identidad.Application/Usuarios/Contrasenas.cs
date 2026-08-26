using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Domain.Sesiones;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Usuarios;

/// <summary>Cambia la contraseña PROPIA, presentando la actual.</summary>
/// <remarks>
/// No lleva permiso, y es la única excepción de la fase junto al inicio y la renovación de sesión:
/// la autorización es saber la contraseña de ahora. Sobre qué cuenta se opera no se pregunta —sale
/// del <i>claim</i>—, así que no hay manera de escribir aquí el identificador de otro.
/// </remarks>
public interface ICambiarContrasenaPropia
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">La contraseña de ahora y la nueva.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(CambiarContrasenaDto peticion, CancellationToken cancelacion);
}

/// <summary>Le cambia la contraseña a OTRO usuario.</summary>
/// <remarks>
/// Esto es tomar la cuenta de alguien, así que lleva su propio permiso
/// (<c>identidad.usuario.cambiar-contrasena</c>) y no se puede hacer presentando nada: quien lo
/// hace no sabe la contraseña anterior, por eso la cambia.
/// </remarks>
public interface IRestablecerContrasena
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del usuario al que se le cambia.</param>
    /// <param name="peticion">La contraseña nueva.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(
        Guid id,
        RestablecerContrasenaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICambiarContrasenaPropia"/>
internal sealed class CambiarContrasenaPropia(
    IUsuarioActual usuarioActual,
    IRepositorioDeUsuarios usuarios,
    IHasherDeContrasenas hasher,
    IRepositorioDeTokensDeRefresco tokens,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    TimeProvider reloj) : ICambiarContrasenaPropia
{
    public async Task<Resultado> EjecutarAsync(CambiarContrasenaDto peticion, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Usuario? usuario = await usuarios
            .ObtenerAsync(usuarioActual.UsuarioId, cancelacion)
            .ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo(ErroresDeSesion.Credenciales());
        }

        if (hasher.Comprobar(usuario.HashDeContrasena, peticion.Actual) == ResultadoDeComprobacion.Incorrecta)
        {
            return Resultado.Fallo(ErroresDeUsuario.ContrasenaActualIncorrecta());
        }

        usuario.CambiarContrasena(hasher.Hashear(peticion.Nueva));
        await RevocarSesionesAsync(tokens, usuario.Id, reloj.GetUtcNow(), cancelacion).ConfigureAwait(false);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }

    /// <summary>Revoca todas las cadenas de refresco vivas de un usuario.</summary>
    /// <remarks>
    /// La contraseña se cambia justamente cuando se sospecha que alguien más la sabía. Dejar vivos
    /// los refrescos anteriores haría que el cambio no echase a nadie: el intruso seguiría
    /// renovando su sesión con una contraseña ya inservible, que es exactamente lo que se quería
    /// evitar. Incluida la sesión de quien la cambia: volver a entrar es barato, y la alternativa
    /// es tener que distinguir cuál de las cadenas vivas es «la buena», que no se puede.
    /// </remarks>
    /// <param name="tokens">Repositorio de emisiones.</param>
    /// <param name="usuarioId">Usuario cuyas sesiones se cortan.</param>
    /// <param name="momento">Instante que queda anotado en la revocación.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal static async Task RevocarSesionesAsync(
        IRepositorioDeTokensDeRefresco tokens,
        Guid usuarioId,
        DateTimeOffset momento,
        CancellationToken cancelacion)
    {
        foreach (TokenDeRefresco emision in
            await tokens.DelUsuarioAsync(usuarioId, cancelacion).ConfigureAwait(false))
        {
            emision.Revocar(MotivoDeRevocacion.CuentaAlterada, momento);
        }
    }
}

/// <inheritdoc cref="IRestablecerContrasena"/>
internal sealed class RestablecerContrasena(
    IRepositorioDeUsuarios usuarios,
    IHasherDeContrasenas hasher,
    IRepositorioDeTokensDeRefresco tokens,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    TimeProvider reloj) : IRestablecerContrasena
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        RestablecerContrasenaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Usuario? usuario = await usuarios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo(ErroresDeUsuario.NoEncontrado(id));
        }

        // `CambiarContrasena` reinicia además el contador de intentos fallidos y levanta el
        // rechazo temporal: restablecer es justamente la manera de sacar a alguien de un bloqueo
        // por intentos.
        usuario.CambiarContrasena(hasher.Hashear(peticion.Nueva));

        await CambiarContrasenaPropia
            .RevocarSesionesAsync(tokens, usuario.Id, reloj.GetUtcNow(), cancelacion)
            .ConfigureAwait(false);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
