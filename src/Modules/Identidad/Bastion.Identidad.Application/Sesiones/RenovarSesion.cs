using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Usuarios;
using Bastion.Identidad.Domain.Sesiones;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>Cambia un token de refresco por una sesión nueva.</summary>
/// <remarks>
/// Tampoco lleva permiso: la autorización es el propio token presentado. Lo que sí lleva es la
/// rotación, que es lo que hace que un refresco robado sirva una vez y delate al ladrón.
/// </remarks>
public interface IRenovarSesion
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="refrescoPresentado">El token tal como venía en la cookie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<SesionAbierta>> EjecutarAsync(string? refrescoPresentado, CancellationToken cancelacion);
}

/// <inheritdoc cref="IRenovarSesion"/>
/// <remarks>
/// <para>
/// <b>Rotación con detección de reutilización.</b> Cada renovación canjea el token presentado y
/// emite otro; el canjeado no vuelve a valer. Si alguien presenta uno <b>ya canjeado</b>, solo
/// caben dos explicaciones y las dos son malas: o el token se copió, o la cadena se duplicó. En
/// cualquiera de las dos hay dos poseedores del mismo secreto y no se puede saber cuál es el
/// legítimo, así que <b>se revoca la familia entera</b> y los dos vuelven a identificarse. El
/// legítimo se lleva la molestia de un inicio de sesión; el otro, la puerta cerrada.
/// </para>
/// <para>
/// A quien lo presenta no se le cuenta nada de esto: sale por
/// <see cref="ErroresDeSesion.Refresco"/>, igual que un token inventado. Decirle «hemos detectado
/// la reutilización» es enseñarle exactamente qué evitar la próxima vez.
/// </para>
/// </remarks>
internal sealed class RenovarSesion(
    IRepositorioDeTokensDeRefresco tokens,
    IRepositorioDeUsuarios usuarios,
    IEmisorDeTokens emisor,
    ConstructorDeSesion constructor,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    TimeProvider reloj) : IRenovarSesion
{
    public async Task<Resultado<SesionAbierta>> EjecutarAsync(
        string? refrescoPresentado,
        CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(refrescoPresentado))
        {
            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Refresco());
        }

        DateTimeOffset ahora = reloj.GetUtcNow();

        TokenDeRefresco? presentada = await tokens
            .ObtenerPorHashAsync(emisor.HashearRefresco(refrescoPresentado), cancelacion)
            .ConfigureAwait(false);

        if (presentada is null)
        {
            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Refresco());
        }

        if (presentada.EstaCanjeado)
        {
            await RevocarFamiliaAsync(
                presentada.FamiliaId, MotivoDeRevocacion.ReutilizacionDetectada, ahora, cancelacion)
                .ConfigureAwait(false);

            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Refresco());
        }

        if (!presentada.EstaVigente(ahora))
        {
            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Refresco());
        }

        Usuario? usuario = await usuarios
            .ObtenerAsync(presentada.UsuarioId, cancelacion)
            .ConfigureAwait(false);

        // Una cuenta bloqueada entre dos renovaciones no puede seguir renovando: si no, dar de
        // baja a alguien lo echaría dentro de quince minutos y no ahora. Se revoca la familia
        // para que tampoco lo intente cada quince minutos hasta que caduque.
        if (usuario is null || !usuario.PuedeIniciarSesion(ahora) ||
            !usuario.PerteneceA(presentada.EmpresaActivaId))
        {
            await RevocarFamiliaAsync(
                presentada.FamiliaId, MotivoDeRevocacion.CuentaAlterada, ahora, cancelacion)
                .ConfigureAwait(false);

            return Resultado.Fallo<SesionAbierta>(ErroresDeSesion.Refresco());
        }

        // Misma familia y misma empresa activa: renovar continúa la sesión, no abre otra ni
        // cambia con qué se está operando.
        SesionArmada armada = await constructor
            .ArmarAsync(usuario, presentada.EmpresaActivaId, presentada.FamiliaId, cancelacion)
            .ConfigureAwait(false);

        presentada.Canjear(armada.Emision.Id, ahora);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(armada.Salida);
    }

    private async Task RevocarFamiliaAsync(
        Guid familiaId,
        MotivoDeRevocacion motivo,
        DateTimeOffset momento,
        CancellationToken cancelacion)
    {
        IReadOnlyList<TokenDeRefresco> familia = await tokens
            .DeLaFamiliaAsync(familiaId, cancelacion)
            .ConfigureAwait(false);

        foreach (TokenDeRefresco emision in familia)
        {
            emision.Revocar(motivo, momento);
        }

        // Se confirma aquí y no lo hace quien llama: la revocación tiene que quedar grabada
        // aunque la operación acabe en error, que es justo como acaba siempre que se llega hasta
        // aquí. Un `return` antes del `ConfirmarAsync` dejaría la detección en el aire.
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);
    }
}
