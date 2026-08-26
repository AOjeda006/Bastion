using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>
/// Los errores del inicio y la renovación de sesión, que son deliberadamente pocos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un solo error para todo lo que puede salir mal al identificarse.</b> «No existe esa cuenta»
/// y «esa no es la contraseña» son dos respuestas distintas para quien pregunta, y con esas dos
/// respuestas se puede recorrer una lista de correos y averiguar cuáles tienen cuenta: un
/// enumerador de usuarios, que es el primer paso de cualquier ataque dirigido. Aquí los dos casos
/// —y también la cuenta dada de baja, y la que está rechazando intentos, y la empresa a la que no
/// pertenece— salen con el <b>mismo código y el mismo texto</b>.
/// </para>
/// <para>
/// Lo que pasó de verdad no se pierde: va al registro, con su identificador de traza. <b>El de
/// fuera necesita saber qué hacer; el de dentro, qué ha pasado.</b> Juntarlos es lo que convierte
/// un mensaje de error en una herramienta de reconocimiento (ADR-0004).
/// </para>
/// <para>
/// El texto tampoco insinúa nada: no dice «revise su contraseña», que ya estaría admitiendo que
/// la cuenta existe.
/// </para>
/// </remarks>
internal static class ErroresDeSesion
{
    /// <summary>Código estable del fallo de identificación. Es contrato publicado.</summary>
    internal const string CodigoDeCredenciales = "credenciales-no-validas";

    /// <summary>Código estable del fallo de renovación.</summary>
    internal const string CodigoDeRefresco = "sesion-no-renovable";

    /// <summary>
    /// El único error que devuelve el inicio de sesión, pase lo que pase.
    /// </summary>
    internal static ErrorDeOperacion Credenciales() => ErrorDeOperacion.NoAutenticado(
        CodigoDeCredenciales,
        "No se ha podido iniciar sesión con esos datos.");

    /// <summary>
    /// El único error que devuelve la renovación, pase lo que pase.
    /// </summary>
    /// <remarks>
    /// Vale igual para «no hay cookie», «ese token no existe», «caducó», «se revocó» y «se ha
    /// detectado que estaba reutilizado». El último es el interesante y es justo el que no se
    /// puede contar: decirle a quien presenta un token robado que se ha detectado el robo es
    /// avisarle de que cambie de método.
    /// </remarks>
    internal static ErrorDeOperacion Refresco() => ErrorDeOperacion.NoAutenticado(
        CodigoDeRefresco,
        "La sesión no se ha podido renovar. Vuelva a iniciar sesión.");
}
