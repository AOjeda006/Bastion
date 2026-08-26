using Bastion.Identidad.Contracts.Sesiones;

namespace Bastion.Identidad.Application.Sesiones;

/// <summary>
/// Lo que produce abrir o renovar una sesión: lo que se devuelve en el cuerpo y lo que se pone en
/// la cookie.
/// </summary>
/// <remarks>
/// <para>
/// Son dos cosas separadas porque van por sitios distintos, y esa separación es la que evita el
/// accidente. <see cref="Sesion"/> es el cuerpo de la respuesta;
/// <see cref="TokenDeRefresco"/> lo escribe el borde en una cookie <c>HttpOnly</c> y no aparece en
/// ningún cuerpo. Si el token de refresco fuera un campo del <see cref="SesionDto"/>, viajaría en
/// el JSON de la respuesta el día que alguien añadiera un <c>[ProducesResponseType]</c> o
/// serializara el objeto en un registro; con dos tipos, para que eso pase hay que escribirlo
/// aposta.
/// </para>
/// <para>
/// Este tipo vive en <c>Application</c> y no en <c>Contracts</c> justamente por eso:
/// <c>Contracts</c> es lo que se publica, y esto no se publica entero.
/// </para>
/// </remarks>
/// <param name="Sesion">Lo que va en el cuerpo de la respuesta.</param>
/// <param name="TokenDeRefresco">Lo que va en la cookie. No se guarda en ninguna parte.</param>
/// <param name="RefrescoExpiraEn">Cuándo caduca la cookie.</param>
public sealed record SesionAbierta(SesionDto Sesion, string TokenDeRefresco, DateTimeOffset RefrescoExpiraEn);
