using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bastion.Identidad.Contracts.Roles;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Api;

/// <summary>
/// Cómo consigue un test las credenciales con las que llama.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por el mismo camino que un cliente</b>: iniciando sesión contra el endpoint de verdad y
/// usando el token que devuelve. Nada de fabricar un <c>ClaimsPrincipal</c> a mano ni de firmar un
/// token en el test. Un token fabricado por el test prueba que el manejador de permisos hace lo
/// que se le pide; no prueba que el emisor escriba los <i>claims</i> que ese manejador lee, que es
/// precisamente donde la cadena se rompe sin dar error.
/// </para>
/// <para>
/// Y por eso el usuario con permisos recortados también se crea por la API, con el rol y la
/// asignación que usaría un administrador de verdad.
/// </para>
/// </remarks>
public static class Sesiones
{
    private const string RutaDeSesiones = "/api/v1/identidad/sesiones";
    private const string RutaDeUsuarios = "/api/v1/identidad/usuarios";
    private const string RutaDeRoles = "/api/v1/identidad/roles";

    /// <summary>Un cliente ya autenticado como la cuenta de la semilla, que lo puede todo.</summary>
    /// <param name="api">La fábrica del host.</param>
    public static async Task<HttpClient> ComoAdministradorAsync(this ApiDeVerdad api)
    {
        (HttpClient cliente, _) = await api.AbrirComoAdministradorAsync().ConfigureAwait(false);

        return cliente;
    }

    /// <summary>Lo mismo, pero devolviendo también lo que dijo la sesión.</summary>
    /// <remarks>
    /// Quien necesita el identificador del usuario o el de la empresa activa lo saca de aquí, no
    /// de una constante del test: son los que ha puesto el emisor en el token, y contrastarlos
    /// contra los de la sesión es lo que detecta que emisor y manejador no hablan de lo mismo.
    /// </remarks>
    /// <param name="api">La fábrica del host.</param>
    public static async Task<(HttpClient Cliente, SesionDto Sesion)> AbrirComoAdministradorAsync(
        this ApiDeVerdad api)
    {
        ArgumentNullException.ThrowIfNull(api);

        HttpClient cliente = api.CrearCliente();
        SesionDto sesion = await AutenticarAsync(
            cliente,
            ApiDeVerdad.CorreoDelAdministrador,
            ApiDeVerdad.ContrasenaDelAdministrador).ConfigureAwait(false);

        return (cliente, sesion);
    }

    /// <summary>
    /// Un cliente autenticado como una cuenta nueva a la que se le conceden EXACTAMENTE esos
    /// permisos, ni uno más.
    /// </summary>
    /// <param name="api">La fábrica del host.</param>
    /// <param name="permisos">Los permisos del rol que se le asigna. Vacío = ninguno.</param>
    public static async Task<HttpClient> ConPermisosAsync(this ApiDeVerdad api, params string[] permisos)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(permisos);

        using HttpClient administrador = api.CrearCliente();
        SesionDto sesionDelAdministrador = await AbrirAsync(
            administrador,
            ApiDeVerdad.CorreoDelAdministrador,
            ApiDeVerdad.ContrasenaDelAdministrador).ConfigureAwait(false);

        administrador.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sesionDelAdministrador.TokenDeAcceso);

        Guid empresaId = sesionDelAdministrador.EmpresaActivaId;

        // Correo y código irrepetibles: los tests comparten base y se ejecutan en el mismo
        // proceso, así que reutilizar un correo haría que el segundo chocara con un 409 y el
        // fallo se leería como un problema de permisos.
        // Los doce ULTIMOS caracteres, no los doce primeros: en un GUID de versión 7 los
        // primeros son la marca de tiempo, y dos roles creados en el mismo segundo tendrían
        // el mismo código. Y doce, no treinta y dos, porque el código de un rol admite
        // cuarenta posiciones y «recortado-» ya gasta diez: con el GUID entero salía un 400
        // que se leía como un problema de permisos.
        string sufijo = Guid.CreateVersion7().ToString("N")[^12..];
        string correo = $"recortado-{sufijo}@bastion.pruebas";
        string contrasena = Guid.CreateVersion7().ToString("N") + "aA1!";

        HttpResponseMessage alta = await administrador.PostAsJsonAsync(
            RutaDeUsuarios,
            new CrearUsuarioDto { Correo = correo, Nombre = "Cuenta recortada", Contrasena = contrasena })
            .ConfigureAwait(false);

        alta.StatusCode.ShouldBe(HttpStatusCode.Created);
        UsuarioDto usuario = (await alta.Content.ReadFromJsonAsync<UsuarioDto>().ConfigureAwait(false))!;

        if (permisos.Length > 0)
        {
            HttpResponseMessage creacionDelRol = await administrador.PostAsJsonAsync(
                RutaDeRoles,
                new CrearRolDto
                {
                    Codigo = $"recortado-{sufijo}",
                    Nombre = "Rol recortado",
                    Permisos = permisos,
                })
                .ConfigureAwait(false);

            creacionDelRol.StatusCode.ShouldBe(HttpStatusCode.Created);
            RolDto rol = (await creacionDelRol.Content.ReadFromJsonAsync<RolDto>().ConfigureAwait(false))!;

            HttpResponseMessage asignacion = await administrador.PostAsJsonAsync(
                $"{RutaDeUsuarios}/{usuario.Id}/roles",
                new AsignarRolDto { EmpresaId = empresaId, RolId = rol.Id })
                .ConfigureAwait(false);

            asignacion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        HttpClient cliente = api.CrearCliente();
        await AutenticarAsync(cliente, correo, contrasena).ConfigureAwait(false);

        return cliente;
    }

    /// <summary>Abre una sesión y devuelve lo que sale en el cuerpo.</summary>
    /// <param name="cliente">Cliente sobre el que se abre.</param>
    /// <param name="correo">Correo.</param>
    /// <param name="contrasena">Contraseña.</param>
    public static async Task<SesionDto> AbrirAsync(HttpClient cliente, string correo, string contrasena)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        HttpResponseMessage respuesta = await cliente
            .PostAsJsonAsync(RutaDeSesiones, new IniciarSesionDto { Correo = correo, Contrasena = contrasena })
            .ConfigureAwait(false);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, "la sesión de partida del test no se ha abierto");

        return (await respuesta.Content.ReadFromJsonAsync<SesionDto>().ConfigureAwait(false))!;
    }

    /// <summary>Deja en el cliente el token que acaba de emitir el servidor.</summary>
    /// <param name="cliente">Cliente sobre el que se abre.</param>
    /// <param name="correo">Correo.</param>
    /// <param name="contrasena">Contraseña.</param>
    public static async Task<SesionDto> AutenticarAsync(
        HttpClient cliente,
        string correo,
        string contrasena)
    {
        SesionDto sesion = await AbrirAsync(cliente, correo, contrasena).ConfigureAwait(false);

        Llevar(cliente, sesion);

        return sesion;
    }

    /// <summary>Pone el token de una sesión en la cabecera del cliente.</summary>
    /// <param name="cliente">Cliente que pasa a llevarlo.</param>
    /// <param name="sesion">Sesión recién emitida.</param>
    public static void Llevar(HttpClient cliente, SesionDto sesion)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(sesion);

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sesion.TokenDeAcceso);
    }
}
