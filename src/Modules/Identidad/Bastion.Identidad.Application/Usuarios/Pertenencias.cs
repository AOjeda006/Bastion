using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Roles;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Domain.Usuarios;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Identidad.Application.Usuarios;

/// <summary>Da de alta a un usuario en una empresa.</summary>
public interface IConcederPertenencia
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="peticion">Empresa a la que se le da de alta.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(
        Guid usuarioId,
        ConcederPertenenciaDto peticion,
        CancellationToken cancelacion);
}

/// <summary>Da de baja a un usuario de una empresa.</summary>
public interface IRetirarPertenencia
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="empresaId">Empresa de la que se le da de baja.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid usuarioId, Guid empresaId, CancellationToken cancelacion);
}

/// <summary>Asigna un rol a un usuario en una empresa.</summary>
public interface IAsignarRol
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="peticion">Empresa y rol.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid usuarioId, AsignarRolDto peticion, CancellationToken cancelacion);
}

/// <summary>Le retira un rol a un usuario en una empresa.</summary>
public interface IRetirarRol
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="peticion">Empresa y rol.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid usuarioId, AsignarRolDto peticion, CancellationToken cancelacion);
}

/// <inheritdoc cref="IConcederPertenencia"/>
/// <remarks>
/// <para>
/// Este es el sitio donde se guarda un identificador de <b>otro esquema</b>, y por tanto donde se
/// paga el precio de no tener claves foráneas entre módulos (§4, regla de frontera 4). El motor no
/// va a comprobar que esa empresa existe: lo comprueba
/// <see cref="IConsultaDeEmpresas"/>, que es la interfaz del <c>Contracts</c> del módulo dueño,
/// resuelta en proceso. Es una llamada a método, no una petición HTTP.
/// </para>
/// <para>
/// Y la empresa que se puede nombrar es la del <i>claim</i> —con la única excepción de la empresa
/// todavía vacía, que es el arranque en frío que documenta
/// <see cref="ErroresDePertenencia.PuedeAdministrarAsync"/>—.
/// </para>
/// </remarks>
internal sealed class ConcederPertenencia(
    IUsuarioActual usuarioActual,
    IRepositorioDeUsuarios usuarios,
    IConsultaDeEmpresas empresas,
    IInquilinoActual inquilino,
    IUnidadTrabajoDeIdentidad unidadTrabajo) : IConcederPertenencia
{
    public async Task<Resultado> EjecutarAsync(
        Guid usuarioId,
        ConcederPertenenciaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // Administrar pertenencias habla POR DEFINICIÓN de una empresa que no tiene por qué ser la
        // activa —el arranque en frío de la que uno acaba de crear—, y necesita ver al usuario con
        // TODAS sus pertenencias: con el filtro puesto, `PerteneceA` diría «no» sobre una que sí
        // existe y el alta acabaría contra su propio índice único. Quién puede nombrar qué empresa
        // no lo decide el filtro, lo decide `PuedeAdministrarAsync` — que está justo aquí abajo.
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.AdministracionDePertenencias);

        if (!await ErroresDePertenencia
            .PuedeAdministrarAsync(usuarioActual, usuarios, peticion.EmpresaId, cancelacion)
            .ConfigureAwait(false))
        {
            return Resultado.Fallo(ErroresDePertenencia.EmpresaAjena());
        }

        Usuario? usuario = await usuarios.ObtenerAsync(usuarioId, cancelacion).ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo(ErroresDeUsuario.NoEncontrado(usuarioId));
        }

        if (!await empresas.EstaActivaAsync(peticion.EmpresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo(ErroresDeUsuario.EmpresaNoOperativa());
        }

        // Conceder dos veces la misma pertenencia no es un error: es la misma petición repetida.
        // Se sale ANTES de tocar nada, y no confiando en que `Conceder` devuelva la que ya había,
        // porque lo que viene después —`Registrar`— sí distingue: apuntar como nueva una que ya
        // existe acabaría en un INSERT contra su propio índice único.
        if (usuario.PerteneceA(peticion.EmpresaId))
        {
            return Resultado.Correcto();
        }

        usuarios.Registrar(usuario.Conceder(peticion.EmpresaId));
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}

/// <inheritdoc cref="IRetirarPertenencia"/>
internal sealed class RetirarPertenencia(
    IUsuarioActual usuarioActual,
    IRepositorioDeUsuarios usuarios,
    IInquilinoActual inquilino,
    IUnidadTrabajoDeIdentidad unidadTrabajo) : IRetirarPertenencia
{
    public async Task<Resultado> EjecutarAsync(
        Guid usuarioId,
        Guid empresaId,
        CancellationToken cancelacion)
    {
        // Igual que al conceder: la pertenencia que se retira puede ser la de otra empresa, y
        // retirar la última de un usuario ajeno tiene que poder verse para poder negarse.
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.AdministracionDePertenencias);

        if (!await ErroresDePertenencia
            .PuedeAdministrarAsync(usuarioActual, usuarios, empresaId, cancelacion)
            .ConfigureAwait(false))
        {
            return Resultado.Fallo(ErroresDePertenencia.EmpresaAjena());
        }

        Usuario? usuario = await usuarios.ObtenerAsync(usuarioId, cancelacion).ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo(ErroresDeUsuario.NoEncontrado(usuarioId));
        }

        if (!usuario.Retirar(empresaId))
        {
            return Resultado.Fallo(ErroresDePertenencia.NoPertenece());
        }

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}

/// <inheritdoc cref="IAsignarRol"/>
internal sealed class AsignarRol(
    IUsuarioActual usuarioActual,
    IRepositorioDeUsuarios usuarios,
    IRepositorioDeRoles roles,
    IInquilinoActual inquilino,
    IUnidadTrabajoDeIdentidad unidadTrabajo) : IAsignarRol
{
    public async Task<Resultado> EjecutarAsync(
        Guid usuarioId,
        AsignarRolDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // La pertenencia sobre la que se asigna el rol es la de la empresa que nombra la petición,
        // que en el arranque en frío no es la activa. Sin ámbito, `ResolverAsync` no la encuentra.
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.AdministracionDePertenencias);

        Resultado<Membresia> membresia = await ErroresDePertenencia
            .ResolverAsync(usuarioActual, usuarios, usuarioId, peticion.EmpresaId, cancelacion)
            .ConfigureAwait(false);

        if (!membresia.EsCorrecto)
        {
            return Resultado.Fallo(membresia.Error!);
        }

        if (!await roles.ExisteAsync(peticion.RolId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo(ErroresDeRol.NoEncontrado(peticion.RolId));
        }

        membresia.Valor.AsignarRol(peticion.RolId);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}

/// <inheritdoc cref="IRetirarRol"/>
internal sealed class RetirarRol(
    IUsuarioActual usuarioActual,
    IRepositorioDeUsuarios usuarios,
    IInquilinoActual inquilino,
    IUnidadTrabajoDeIdentidad unidadTrabajo) : IRetirarRol
{
    public async Task<Resultado> EjecutarAsync(
        Guid usuarioId,
        AsignarRolDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // Lo mismo que al asignarlo, y por lo mismo.
        using IDisposable ambito = inquilino.SinInquilino(MotivoSinInquilino.AdministracionDePertenencias);

        Resultado<Membresia> membresia = await ErroresDePertenencia
            .ResolverAsync(usuarioActual, usuarios, usuarioId, peticion.EmpresaId, cancelacion)
            .ConfigureAwait(false);

        if (!membresia.EsCorrecto)
        {
            return Resultado.Fallo(membresia.Error!);
        }

        // No comprueba que el rol exista: retirar uno que ya no existe es exactamente lo que hay
        // que poder hacer para limpiar una asignación huérfana.
        membresia.Valor.RetirarRol(peticion.RolId);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}

/// <summary>Los errores de negocio de las pertenencias, y la resolución que comparten.</summary>
internal static class ErroresDePertenencia
{
    /// <summary>Se ha nombrado una empresa que no es la del <i>claim</i>.</summary>
    internal static ErrorDeOperacion EmpresaAjena() => ErrorDeOperacion.PermisoDenegado(
        "empresa-ajena",
        "Solo se pueden administrar pertenencias de la empresa con la que se está operando.");

    /// <summary>
    /// Si se pueden administrar las pertenencias de esa empresa desde la sesión de ahora.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La regla es la del <i>claim</i>: se administra la empresa con la que se está operando y
    /// ninguna otra. Si se pudiera nombrar otra, tener <c>identidad.pertenencia.conceder</c> en
    /// una empresa cualquiera equivaldría a tenerlo en todas, y R8 se caería por la puerta de
    /// atrás.
    /// </para>
    /// <para>
    /// <b>Con una excepción, y por necesidad: el arranque en frío.</b> Una empresa recién creada
    /// no tiene a nadie dentro; para entrar en ella hay que pertenecer a ella, y para que alguien
    /// pertenezca hay que estar dentro. Escrita solo con la regla de arriba, la segunda empresa
    /// del sistema es <b>inalcanzable para siempre</b> —lo descubrió el primer test que intentó
    /// crear una—. Así que se admite nombrar otra empresa <b>mientras no haya nadie más dentro</b>:
    /// la que uno acaba de crear y todavía está vacía. En cuanto entra un segundo, se acabó.
    /// </para>
    /// <para>
    /// Lo que NO abre esa excepción: una empresa vacía no tiene datos que ver, así que colarse en
    /// ella no enseña nada de nadie; y quien lo haga necesita ya el permiso de conceder, con el
    /// que podría crearse una empresa propia igualmente. La alternativa —que crear la empresa dé
    /// de alta a quien la crea— es una escritura de Organización sobre Identidad, y eso solo se
    /// hace por eventos (§4, regla 5), que son el ítem 0.8.
    /// </para>
    /// </remarks>
    /// <param name="usuarioActual">De dónde sale la empresa con la que se opera.</param>
    /// <param name="usuarios">Repositorio de usuarios.</param>
    /// <param name="empresaId">Empresa que nombra la petición.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal static async Task<bool> PuedeAdministrarAsync(
        IUsuarioActual usuarioActual,
        IRepositorioDeUsuarios usuarios,
        Guid empresaId,
        CancellationToken cancelacion) =>
        empresaId == usuarioActual.EmpresaId
        || await usuarios
            .SinMiembrosAjenosAsync(empresaId, usuarioActual.UsuarioId, cancelacion)
            .ConfigureAwait(false);

    /// <summary>El usuario no pertenece a esa empresa.</summary>
    internal static ErrorDeOperacion NoPertenece() => ErrorDeOperacion.NoEncontrado(
        "pertenencia-no-encontrada",
        "Ese usuario no pertenece a esa empresa.");

    /// <summary>
    /// Comprueba la empresa contra el <i>claim</i> y devuelve la pertenencia sobre la que operar.
    /// </summary>
    /// <remarks>
    /// Está compartida porque asignar y retirar un rol hacen la misma comprobación, y una
    /// comprobación repetida es una comprobación que un día se copia mal. Que sean dos permisos
    /// distintos no obliga a que sean dos validaciones distintas: lo que se separa es quién puede
    /// hacer cada cosa, no cómo se averigua sobre qué se hace.
    /// </remarks>
    /// <param name="usuarioActual">De dónde sale la empresa con la que se opera.</param>
    /// <param name="usuarios">Repositorio de usuarios.</param>
    /// <param name="usuarioId">Usuario sobre el que se opera.</param>
    /// <param name="empresaId">Empresa que nombra la petición.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal static async Task<Resultado<Membresia>> ResolverAsync(
        IUsuarioActual usuarioActual,
        IRepositorioDeUsuarios usuarios,
        Guid usuarioId,
        Guid empresaId,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(usuarioActual);
        ArgumentNullException.ThrowIfNull(usuarios);

        if (!await PuedeAdministrarAsync(usuarioActual, usuarios, empresaId, cancelacion)
            .ConfigureAwait(false))
        {
            return Resultado.Fallo<Membresia>(EmpresaAjena());
        }

        Usuario? usuario = await usuarios.ObtenerAsync(usuarioId, cancelacion).ConfigureAwait(false);

        if (usuario is null)
        {
            return Resultado.Fallo<Membresia>(ErroresDeUsuario.NoEncontrado(usuarioId));
        }

        Membresia? membresia = usuario.EnEmpresa(empresaId);

        return membresia is null
            ? Resultado.Fallo<Membresia>(NoPertenece())
            : Resultado.Correcto(membresia);
    }
}
