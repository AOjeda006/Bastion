using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Identidad.Application.Usuarios;

/// <summary>Los errores de negocio de la administración de usuarios.</summary>
/// <remarks>
/// Estos SÍ dicen lo que ha pasado, al revés que los de <c>ErroresDeSesion</c>, y la diferencia no
/// es un descuido: aquí quien pregunta ya está dentro y ya ha demostrado que tiene el permiso de
/// administrar usuarios. A ese le sobra el disfraz y le hace falta saber por qué no ha podido.
/// </remarks>
internal static class ErroresDeUsuario
{
    /// <summary>No existe ese usuario, o no es visible desde aquí.</summary>
    /// <param name="id">Identificador que se pidió.</param>
    internal static ErrorDeOperacion NoEncontrado(Guid id) => ErrorDeOperacion.NoEncontrado(
        "usuario-no-encontrado",
        $"No hay ningún usuario con el identificador {id}.");

    /// <summary>Ya hay una cuenta con ese correo.</summary>
    /// <param name="correo">Correo ya normalizado.</param>
    internal static ErrorDeOperacion CorreoYaRegistrado(string correo) => ErrorDeOperacion.Conflicto(
        "correo-ya-registrado",
        $"Ya hay una cuenta con el correo {correo}.");

    /// <summary>La empresa no admite altas porque está bloqueada o no existe.</summary>
    /// <remarks>
    /// Lo contesta el módulo Organización a través de su <c>Contracts</c>, porque el motor no
    /// puede: la empresa vive en otro esquema y entre esquemas no hay claves foráneas (§4, regla
    /// de frontera 4). Sin esta comprobación, la pertenencia se guardaría apuntando a un
    /// identificador que no lleva a ninguna parte y nadie se enteraría hasta el primer listado.
    /// </remarks>
    internal static ErrorDeOperacion EmpresaNoOperativa() => ErrorDeOperacion.ReglaDeNegocio(
        "empresa-no-operativa",
        "La empresa no existe o está bloqueada, y no admite altas de usuarios.");

    /// <summary>La contraseña actual que se ha presentado no es la que hay.</summary>
    internal static ErrorDeOperacion ContrasenaActualIncorrecta() => ErrorDeOperacion.Validacion(
        "contrasena-actual-incorrecta",
        "La contraseña actual no es correcta.");
}
