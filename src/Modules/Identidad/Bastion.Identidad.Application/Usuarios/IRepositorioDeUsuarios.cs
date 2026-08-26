using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Identidad.Contracts.Comun;
using Bastion.Identidad.Domain.Usuarios;

namespace Bastion.Identidad.Application.Usuarios;

/// <summary>Acceso a los usuarios guardados.</summary>
/// <remarks>
/// El puerto lo declara la capa que lo CONSUME y lo implementa Infrastructure. Ninguno de sus
/// métodos confirma nada: eso lo decide el caso de uso a través de <c>IUnidadTrabajo</c>.
/// </remarks>
public interface IRepositorioDeUsuarios
{
    /// <summary>El usuario con ese identificador, con sus pertenencias, o nulo.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Usuario?> ObtenerAsync(Guid id, CancellationToken cancelacion);

    /// <summary>El usuario con ese correo, con sus pertenencias, o nulo.</summary>
    /// <remarks>
    /// Pide el <see cref="Correo"/> entero y no su cadena a propósito, por lo mismo que
    /// <c>IRepositorioDeEmpresas.ExisteConNifAsync</c>: el correo va con conversor de valor, y
    /// <c>usuario.Correo.Valor == cadena</c> compila igual de bien y revienta en ejecución con
    /// «no se pudo traducir la expresión». Con el tipo en la firma, ese error no se puede escribir.
    /// </remarks>
    /// <param name="correo">Correo ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Usuario?> ObtenerPorCorreoAsync(Correo correo, CancellationToken cancelacion);

    /// <summary>Si ya hay una cuenta con ese correo.</summary>
    /// <param name="correo">Correo ya normalizado.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> ExisteConCorreoAsync(Correo correo, CancellationToken cancelacion);

    /// <summary>Si no hay ningún usuario todavía. Es lo que decide si la semilla se aplica.</summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> NoHayNingunoAsync(CancellationToken cancelacion);

    /// <summary>Si en esa empresa no hay nadie dado de alta, salvo quizá ese usuario.</summary>
    /// <remarks>
    /// Es lo que decide si una empresa recién creada se puede poblar desde fuera. Ver el
    /// arranque en frío que documenta <c>ErroresDePertenencia.PuedeAdministrarAsync</c>.
    /// </remarks>
    /// <param name="empresaId">Empresa que se mira.</param>
    /// <param name="salvoUsuarioId">El usuario que no cuenta como «alguien».</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<bool> SinMiembrosAjenosAsync(Guid empresaId, Guid salvoUsuarioId, CancellationToken cancelacion);

    /// <summary>Una página de usuarios, con el total.</summary>
    /// <param name="paginacion">Qué página se pide.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<Usuario>> ListarAsync(Paginacion paginacion, CancellationToken cancelacion);

    /// <summary>Los usuarios que pertenecen a una empresa, paginados.</summary>
    /// <param name="empresaId">Empresa, que sale del <i>claim</i>.</param>
    /// <param name="paginacion">Qué página se pide.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<PaginaDe<Usuario>> ListarDeEmpresaAsync(
        Guid empresaId,
        Paginacion paginacion,
        CancellationToken cancelacion);

    /// <summary>Apunta un usuario nuevo. No lo graba: eso lo hace la unidad de trabajo.</summary>
    /// <param name="usuario">Usuario que se da de alta.</param>
    void Agregar(Usuario usuario);

    /// <summary>Apunta una pertenencia recién concedida.</summary>
    /// <remarks>
    /// <para>
    /// <b>Hace falta, aunque la pertenencia ya esté colgada de su usuario, y esto costó un rojo
    /// entero de la CI.</b> Cuando el usuario se ha leído de la base —o sea, siempre menos en la
    /// semilla— EF Core lo tiene en estado <c>Unchanged</c>. Al detectar cambios encuentra en su
    /// colección una <see cref="Membresia"/> que no seguía, y decide qué es MIRANDO SI TIENE
    /// CLAVE: la tiene, porque el constructor le pone un <c>Guid</c> v7 el primer día, así que
    /// concluye que ya existía y la marca <c>Modified</c>. Lo que sale no es un <c>INSERT</c>,
    /// es un <c>UPDATE … WHERE id = …</c> que no toca ni una fila, y eso es un
    /// <c>DbUpdateConcurrencyException</c>: un <c>500</c> en cada alta.
    /// </para>
    /// <para>
    /// No pasa con el usuario recién creado —el hijo hereda el <c>Added</c> del padre— ni con
    /// <c>RolDeMembresia</c>, cuya clave es compuesta y sí sale <c>Added</c>. Solo con esta, y
    /// solo por el camino que ningún test de dominio recorre. Está fijado en
    /// <c>LasPertenenciasNuevasSeInsertanTests</c>, que no necesita base de datos.
    /// </para>
    /// </remarks>
    /// <param name="membresia">Pertenencia que devolvió <c>Usuario.Conceder</c>.</param>
    void Registrar(Membresia membresia);
}
