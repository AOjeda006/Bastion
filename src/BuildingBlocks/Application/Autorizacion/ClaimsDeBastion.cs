namespace Bastion.BuildingBlocks.Application.Autorizacion;

/// <summary>
/// Los nombres de los <i>claims</i> que lleva el token de acceso.
/// </summary>
/// <remarks>
/// <para>
/// <b>Están aquí, en el bloque común, porque los escribe un sitio y los lee otro.</b> Quien los
/// escribe es el emisor de tokens del módulo Identidad; quien los lee es el borde, al reconstruir
/// quién opera y con qué empresa. Escritos como literales en los dos sitios, basta con que uno
/// diga <c>permiso</c> y el otro <c>permisos</c> para que la cadena entera se construya sin error,
/// deje el registro correcto y autorice a cualquiera: el emparejador no encuentra ningún permiso,
/// pero tampoco encuentra nada de lo que quejarse.
/// </para>
/// <para>
/// Con una constante compartida, esa discrepancia no se puede escribir.
/// </para>
/// </remarks>
public static class ClaimsDeBastion
{
    /// <summary>Quién es: el identificador del usuario.</summary>
    /// <remarks>
    /// <c>sub</c> es el nombre registrado en el RFC 7519 y no uno inventado. El borde tiene que
    /// pedir además que no se traduzcan los nombres entrantes: por omisión, la pila de Microsoft
    /// convierte <c>sub</c> en la URI larga de <c>ClaimTypes.NameIdentifier</c>, y buscar
    /// <c>sub</c> después de esa traducción no encuentra nada.
    /// </remarks>
    public const string Sujeto = "sub";

    /// <summary>Su nombre, para que la interfaz no necesite otra consulta.</summary>
    public const string Nombre = "name";

    /// <summary>La empresa con la que se está operando (R8).</summary>
    /// <remarks>
    /// Va en el token y en ningún otro sitio: ni cabecera, ni parámetro de consulta, ni campo del
    /// cuerpo. Cambiarla es pedir otro token.
    /// </remarks>
    public const string Empresa = "empresa";

    /// <summary>
    /// Un permiso concedido en esa empresa. Aparece tantas veces como permisos haya.
    /// </summary>
    /// <remarks>
    /// Repetido y no una lista separada por comas: así el emparejador compara valores enteros y no
    /// tiene que partir cadenas, que es donde se cuelan los espacios de más.
    /// </remarks>
    public const string Permiso = "permiso";
}
