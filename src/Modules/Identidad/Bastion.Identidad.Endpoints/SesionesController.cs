using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Bastion.Identidad.Application.Sesiones;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Endpoints.Comun;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Identidad.Endpoints;

/// <summary>
/// Sesiones, bajo <c>/api/v1/identidad/sesiones</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el único controlador con acciones anónimas de todo el sistema</b>, y son exactamente
/// tres: abrir sesión, renovarla y cerrarla. No pueden exigir permiso porque son la manera de
/// conseguir permisos; a cambio, las tres son las que más protecciones llevan encima —error
/// indistinguible, tiempo constante, rotación con detección de reutilización—.
/// </para>
/// <para>
/// El resto del sistema deniega por defecto: la política de respaldo del host exige autenticación
/// donde no hay atributo, así que olvidarse de poner uno cierra la puerta en vez de abrirla.
/// </para>
/// </remarks>
public sealed class SesionesController(
    IIniciarSesion iniciar,
    IRenovarSesion renovar,
    ICerrarSesion cerrar,
    ICambiarEmpresaActiva cambiarEmpresa) : ControladorDeIdentidad
{
    /// <summary>Abre una sesión.</summary>
    /// <param name="peticion">Correo, contraseña y, si acaso, con qué empresa empezar.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Iniciar(
        [FromBody] IniciarSesionDto peticion,
        CancellationToken cancelacion) =>
        Entregar(await iniciar.EjecutarAsync(peticion, cancelacion).ConfigureAwait(false));

    /// <summary>Cambia el token de refresco por una sesión nueva.</summary>
    /// <remarks>
    /// No lleva cuerpo: lo que autoriza es la cookie. Un token de refresco en el cuerpo tendría
    /// que haberlo leído el JavaScript de algún sitio, y ese sitio sería el que no puede existir.
    /// </remarks>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPost("renovacion")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Renovar(CancellationToken cancelacion)
    {
        Resultado<SesionAbierta> resultado = await renovar
            .EjecutarAsync(CookieDeRefresco.Leer(Request), cancelacion)
            .ConfigureAwait(false);

        // Si la renovación falla, la cookie se borra. Da igual por qué haya fallado: si no sirve
        // para renovar, no sirve para nada, y dejarla puesta hace que el frontal siga intentándolo
        // cada quince minutos contra un token muerto.
        if (!resultado.EsCorrecto)
        {
            CookieDeRefresco.Borrar(Response);
        }

        return Entregar(resultado);
    }

    /// <summary>Cierra la sesión.</summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpDelete("actual")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cerrar(CancellationToken cancelacion)
    {
        Resultado resultado = await cerrar
            .EjecutarAsync(CookieDeRefresco.Leer(Request), cancelacion)
            .ConfigureAwait(false);

        CookieDeRefresco.Borrar(Response);

        return ResponderSinContenido(resultado);
    }

    /// <summary>Cambia con qué empresa se está operando (R8).</summary>
    /// <remarks>
    /// Autenticada pero SIN permiso: elegir entre las empresas a las que uno ya pertenece no es una
    /// facultad que se conceda, es lo que significa pertenecer a varias. Lo que sí se comprueba
    /// —dentro— es que pertenezca a la que pide.
    /// </remarks>
    /// <param name="peticion">Empresa que pasa a ser la activa.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    [HttpPut("actual/empresa")]
    [ProducesResponseType(typeof(SesionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CambiarEmpresa(
        [FromBody] CambiarEmpresaDto peticion,
        CancellationToken cancelacion) =>
        Entregar(await cambiarEmpresa
            .EjecutarAsync(peticion, CookieDeRefresco.Leer(Request), cancelacion)
            .ConfigureAwait(false));

    // El único sitio donde la sesión se parte en dos: el cuerpo va al JSON y el refresco a la
    // cookie. Está aquí, una vez, para que ninguna de las tres acciones pueda devolver por
    // descuido el token de refresco dentro del cuerpo.
    private IActionResult Entregar(Resultado<SesionAbierta> resultado)
    {
        if (!resultado.EsCorrecto)
        {
            return resultado.Error!.AResultadoDeAccion();
        }

        SesionAbierta sesion = resultado.Valor;
        CookieDeRefresco.Escribir(Response, sesion.TokenDeRefresco, sesion.RefrescoExpiraEn);

        return Ok(sesion.Sesion);
    }
}
