using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Comun;
using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Domain.Sesiones;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Usuarios;

/// <summary>Cambia el nombre de un usuario.</summary>
public interface IModificarUsuario
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Lo que se cambia.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<UsuarioDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarUsuarioDto peticion,
        CancellationToken cancelacion);
}

/// <summary>Bloquea un usuario. Es lo que hace el <c>DELETE</c> del recurso.</summary>
/// <remarks>
/// <para>
/// Un usuario es una <b>persona física</b> sin discusión posible, así que el art. 32 de la LOPDGDD
/// se aplica entero: sus datos se <b>bloquean</b> —se dejan inaccesibles para el tratamiento
/// ordinario y conservados a disposición de jueces y administraciones— y no se destruyen (R16).
/// Además, de un usuario cuelga cada asiento del rastro de auditoría: borrar la fila dejaría el
/// rastro diciendo «lo hizo alguien».
/// </para>
/// <para>
/// Bloquear <b>revoca todas sus sesiones</b>. Sin eso, dar de baja a alguien lo dejaría dentro
/// hasta que caducase su refresco, que son días: la baja se habría anotado y no habría surtido
/// efecto, que es la peor combinación posible.
/// </para>
/// </remarks>
public interface IBloquearUsuario
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>Devuelve a un usuario bloqueado a la actividad.</summary>
/// <remarks>
/// Permiso propio, distinto del de bloquear. Quien puede dar de baja no tiene por qué poder
/// devolver a la actividad una cuenta que otro dio de baja: son dos decisiones distintas y la
/// segunda deshace la primera.
/// </remarks>
public interface IDesbloquearUsuario
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <remarks>
    /// <b>No lleva versión, y desde el 0.10 no puede llevarla.</b> El <c>If-Match</c> se cita
    /// leyendo antes el recurso, y un recurso bloqueado no se puede leer por ningún camino
    /// ordinario: la precondición pediría una llave que no existe (ADR-0017).
    /// </remarks>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarUsuario"/>
internal sealed class ModificarUsuario(
    IRepositorioDeUsuarios usuarios,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    IVersionesDeIdentidad versiones) : IModificarUsuario
{
    public async Task<Resultado<UsuarioDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarUsuarioDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Usuario? usuario = await usuarios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo<UsuarioDto>(ErroresDeUsuario.NoEncontrado(id));
        }

        versiones.Exigir(usuario, version);

        usuario.Renombrar(peticion.Nombre);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(usuario.ADto());
    }
}

/// <inheritdoc cref="IBloquearUsuario"/>
internal sealed class BloquearUsuario(
    IRepositorioDeUsuarios usuarios,
    IRepositorioDeTokensDeRefresco tokens,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    IVersionesDeIdentidad versiones,
    TimeProvider reloj) : IBloquearUsuario
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        Usuario? usuario = await usuarios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo(ErroresDeUsuario.NoEncontrado(id));
        }

        versiones.Exigir(usuario, version);

        DateTimeOffset ahora = reloj.GetUtcNow();
        usuario.Bloquear(MotivoDeBloqueo.SupresionSolicitada, ahora);

        foreach (TokenDeRefresco emision in
            await tokens.DelUsuarioAsync(usuario.Id, cancelacion).ConfigureAwait(false))
        {
            emision.Revocar(MotivoDeRevocacion.CuentaAlterada, ahora);
        }

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}

/// <inheritdoc cref="IDesbloquearUsuario"/>
internal sealed class DesbloquearUsuario(
    IRepositorioDeUsuarios usuarios,
    IUnidadTrabajoDeIdentidad unidadTrabajo,
    IAccesoALoBloqueado bloqueados) : IDesbloquearUsuario
{
    public async Task<Resultado> EjecutarAsync(Guid id, CancellationToken cancelacion)
    {
        // El ÚNICO camino ordinario que necesita ver lo bloqueado, y por una razón de lógica:
        // para levantar un bloqueo hay que poder leer lo que está bloqueado. Es una apertura
        // declarada, con su motivo de la lista cerrada y anotada en el registro — no un
        // `IgnoreQueryFilters`, que además apagaría de paso el filtro de empresa.
        using IDisposable _ = bloqueados.ViendoLoBloqueado(MotivoParaVerLoBloqueado.AdministracionDelBloqueo);

        Usuario? usuario = await usuarios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo(ErroresDeUsuario.NoEncontrado(id));
        }

        usuario.Desbloquear();
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
