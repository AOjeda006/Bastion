using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Domain.Sesiones;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>Cierra la sesión: revoca la cadena de refrescos que la sostenía.</summary>
public interface ICerrarSesion
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="refrescoPresentado">El token tal como venía en la cookie.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(string? refrescoPresentado, CancellationToken cancelacion);
}

/// <inheritdoc cref="ICerrarSesion"/>
/// <remarks>
/// <para>
/// <b>Sale bien siempre</b>, haya cookie o no, valga el token o no. Cerrar sesión es una petición
/// de un estado, no de una transición: el que la hace quiere quedarse fuera, y ya está fuera si el
/// token no valía. Devolver un error distinto según si el token existía convertiría el cierre de
/// sesión en un oráculo para saber si un token capturado sigue vivo.
/// </para>
/// <para>
/// <b>Lo que no puede hacer es revocar el token de acceso</b>, que se valida sin tocar la base de
/// datos y sigue valiendo hasta que caduque. Eso es el precio del JWT, y la respuesta es que dure
/// quince minutos. Quien cierra sesión deja de poder renovar; el borde además borra la cookie.
/// </para>
/// </remarks>
internal sealed class CerrarSesion(
    IRepositorioDeTokensDeRefresco tokens,
    IEmisorDeTokens emisor,
    IInquilinoActual inquilino,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    TimeProvider reloj) : ICerrarSesion
{
    public async Task<Resultado> EjecutarAsync(string? refrescoPresentado, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(refrescoPresentado))
        {
            return Resultado.Correcto();
        }

        // El mismo ámbito que abren `IniciarSesion` y `RenovarSesion`, y por el mismo motivo:
        // cerrar sesión es una operación de autenticación, y quien la pide puede no tener empresa
        // activa ninguna —la cookie basta, no hace falta token de acceso—. Faltaba aquí desde el
        // 0.5 y no se notaba porque nada preguntaba; desde el 0.7 sí pregunta alguien: la traza de
        // cada cambio necesita saber en nombre de qué empresa se escribe, o de por qué no hay.
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.AutenticacionYSesion);

        TokenDeRefresco? presentada = await tokens
            .ObtenerPorHashAsync(emisor.HashearRefresco(refrescoPresentado), cancelacion)
            .ConfigureAwait(false);

        if (presentada is null)
        {
            return Resultado.Correcto();
        }

        // La familia entera, no solo la emisión presentada. Si se revocara solo esta, el token
        // que la sustituyó —el que de verdad tiene el navegador— seguiría renovando, y el cierre
        // de sesión no cerraría nada.
        DateTimeOffset ahora = reloj.GetUtcNow();

        IReadOnlyList<TokenDeRefresco> familia = await tokens
            .DeLaFamiliaAsync(presentada.FamiliaId, cancelacion)
            .ConfigureAwait(false);

        foreach (TokenDeRefresco emision in familia)
        {
            emision.Revocar(MotivoDeRevocacion.CierreDeSesion, ahora);
        }

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
