namespace Bastion.Identidad.Application.Sesiones;

/// <summary>
/// Calcula y comprueba el resumen de una contraseña.
/// </summary>
/// <remarks>
/// <para>
/// Es un puerto, y su implementación es el <c>PasswordHasher</c> de ASP.NET Core Identity con sus
/// parámetros por defecto. <b>No se inventa criptografía</b>: qué algoritmo, con cuántas
/// iteraciones y por qué, está escrito en el <b>ADR-0008</b>.
/// </para>
/// <para>
/// El puerto existe para que los casos de uso no conozcan ese paquete, y sobre todo para que el
/// día que haya que subir el coste —que lo habrá— se cambie en un sitio.
/// </para>
/// </remarks>
public interface IHasherDeContrasenas
{
    /// <summary>Calcula el resumen de una contraseña.</summary>
    /// <param name="contrasena">La contraseña en claro. No se guarda en ninguna parte.</param>
    string Hashear(string contrasena);

    /// <summary>Comprueba una contraseña contra un resumen.</summary>
    /// <param name="hash">Resumen guardado.</param>
    /// <param name="contrasena">Lo que se ha escrito.</param>
    ResultadoDeComprobacion Comprobar(string hash, string contrasena);

    /// <summary>
    /// Un resumen válido, calculado con los parámetros de ahora, de una contraseña que no conoce
    /// nadie.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Está para que <b>intentar entrar con un correo que no existe cueste lo mismo</b> que
    /// intentarlo con uno que sí. Sin esto, la cuenta inexistente contesta enseguida —no hay nada
    /// contra lo que comparar— y la existente tarda las decenas de milisegundos del algoritmo; esa
    /// diferencia se mide desde fuera con un cronómetro y vuelve a dar el enumerador de usuarios
    /// que <see cref="ErroresDeSesion"/> se molesta en cerrar. Igualar el mensaje y no el tiempo
    /// es cerrar la puerta y dejar la ventana.
    /// </para>
    /// <para>
    /// Tiene que salir del <b>mismo</b> hasher y con los <b>mismos</b> parámetros: un resumen
    /// calculado con un coste distinto tarda distinto, y la defensa se cae sin que nada lo diga.
    /// </para>
    /// </remarks>
    string HashDeRelleno { get; }
}

/// <summary>Desenlace de comprobar una contraseña.</summary>
public enum ResultadoDeComprobacion
{
    /// <summary>No coincide.</summary>
    Incorrecta,

    /// <summary>Coincide.</summary>
    Correcta,

    /// <summary>
    /// Coincide, pero el resumen se calculó con parámetros viejos y conviene recalcularlo.
    /// </summary>
    /// <remarks>
    /// Es el único momento en que se puede: solo aquí está la contraseña en claro. Ignorar este
    /// caso significa que subir el coste del algoritmo no protege a nadie que ya tenga cuenta,
    /// que son justo todos.
    /// </remarks>
    CorrectaPeroConvieneRehashear,
}
