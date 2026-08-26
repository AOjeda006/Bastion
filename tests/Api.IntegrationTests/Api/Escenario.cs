using System.Net;
using System.Net.Http.Json;
using Bastion.Identidad.Contracts.Roles;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Shouldly;

// Los dos módulos tienen su propio PaginaDe<T> porque Contracts no referencia nada, ni siquiera
// los bloques comunes (§4). Aquí, que ve a los dos, hay que decir de cuál se habla.
using PaginaDeRoles = Bastion.Identidad.Contracts.Comun.PaginaDe<Bastion.Identidad.Contracts.Roles.RolDto>;

namespace Bastion.Api.IntegrationTests.Api;

/// <summary>
/// El punto de partida de los tests que necesitan una empresa para ellos solos.
/// </summary>
/// <remarks>
/// <para>
/// Los tests comparten base y semilla, así que trabajar todos sobre la empresa sembrada haría que
/// el segundo que creara el ejercicio de 2026 chocara contra el índice único del primero, y el
/// fallo se leería como un error de la API. Cada uno se lleva su empresa.
/// </para>
/// <para>
/// Y se la lleva <b>cambiando de empresa por donde se cambia</b>: pertenencia, rol y
/// <c>PUT /sesiones/actual/empresa</c>. No hay una puerta de atrás para el test, porque si la
/// hubiera, el camino que el test ejercita no sería el que usa un cliente (R8).
/// </para>
/// </remarks>
public static class Escenario
{
    private const string RutaDeEmpresas = "/api/v1/organizacion/empresas";
    private const string RutaDeUsuarios = "/api/v1/identidad/usuarios";
    private const string RutaDeRoles = "/api/v1/identidad/roles";
    private const string RutaDeLaEmpresaActiva = "/api/v1/identidad/sesiones/actual/empresa";

    /// <summary>Crea una empresa y devuelve un cliente que ya está operando dentro de ella.</summary>
    /// <param name="api">La fábrica del host.</param>
    /// <param name="nif">NIF de la empresa nueva. Tiene que ser válido y no estar dado de alta.</param>
    public static async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(
        this ApiDeVerdad api,
        string nif)
    {
        ArgumentNullException.ThrowIfNull(api);

        (HttpClient cliente, SesionDto sesion) = await api.AbrirComoAdministradorAsync()
            .ConfigureAwait(false);

        EmpresaDto empresa = await CrearEmpresaAsync(cliente, nif).ConfigureAwait(false);

        await EntrarEnAsync(cliente, sesion.UsuarioId, empresa.Id).ConfigureAwait(false);

        return (cliente, empresa);
    }

    /// <summary>Da de alta una empresa con el NIF pedido.</summary>
    /// <param name="cliente">Cliente con permiso para crearla.</param>
    /// <param name="nif">NIF válido y libre.</param>
    public static async Task<EmpresaDto> CrearEmpresaAsync(HttpClient cliente, string nif)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        HttpResponseMessage respuesta = await cliente
            .PostAsJsonAsync(RutaDeEmpresas, NuevaEmpresa(nif))
            .ConfigureAwait(false);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Detalle(respuesta).ConfigureAwait(false));

        return (await respuesta.Content.ReadFromJsonAsync<EmpresaDto>().ConfigureAwait(false))!;
    }

    /// <summary>Concede la pertenencia, asigna el rol de administración y cambia la empresa activa.</summary>
    /// <param name="cliente">Cliente autenticado, que se queda operando en la empresa nueva.</param>
    /// <param name="usuarioId">Quién entra.</param>
    /// <param name="empresaId">Dónde entra.</param>
    public static async Task EntrarEnAsync(HttpClient cliente, Guid usuarioId, Guid empresaId)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        HttpResponseMessage pertenencia = await cliente.PostAsJsonAsync(
            $"{RutaDeUsuarios}/{usuarioId}/pertenencias",
            new ConcederPertenenciaDto { EmpresaId = empresaId }).ConfigureAwait(false);

        pertenencia.StatusCode.ShouldBe(
            HttpStatusCode.NoContent, await Detalle(pertenencia).ConfigureAwait(false));

        HttpResponseMessage asignacion = await cliente.PostAsJsonAsync(
            $"{RutaDeUsuarios}/{usuarioId}/roles",
            new AsignarRolDto { EmpresaId = empresaId, RolId = await RolDeAdministracionAsync(cliente).ConfigureAwait(false) })
            .ConfigureAwait(false);

        asignacion.StatusCode.ShouldBe(
            HttpStatusCode.NoContent, await Detalle(asignacion).ConfigureAwait(false));

        HttpResponseMessage cambio = await cliente.PutAsJsonAsync(
            RutaDeLaEmpresaActiva,
            new CambiarEmpresaDto { EmpresaId = empresaId }).ConfigureAwait(false);

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Detalle(cambio).ConfigureAwait(false));

        SesionDto sesion = (await cambio.Content.ReadFromJsonAsync<SesionDto>().ConfigureAwait(false))!;
        sesion.EmpresaActivaId.ShouldBe(empresaId);

        Sesiones.Llevar(cliente, sesion);
    }

    /// <summary>Un cuerpo de alta de empresa que es válido de arriba abajo.</summary>
    /// <param name="nif">NIF válido y libre.</param>
    public static CrearEmpresaDto NuevaEmpresa(string nif) => new()
    {
        Nif = nif,
        RazonSocial = $"Empresa {nif}",
        DomicilioFiscal = Domicilio(),
        DivisaBase = "EUR",
        RegimenDeIva = "General",
    };

    /// <summary>Un domicilio con los seis campos de R17.</summary>
    public static DireccionDto Domicilio() => new()
    {
        Calle = "Gran Vía",
        Numero = "31",
        CodigoPostal = "28013",
        Poblacion = "Madrid",
        Subdivision = "Madrid",
        Pais = "ES",
    };

    private static async Task<Guid> RolDeAdministracionAsync(HttpClient cliente)
    {
        PaginaDeRoles? roles = await cliente
            .GetFromJsonAsync<PaginaDeRoles>($"{RutaDeRoles}?page=1&size=200")
            .ConfigureAwait(false);

        roles.ShouldNotBeNull();

        // El código lo fija la semilla. Buscarlo por él, y no quedarse con el primero de la lista,
        // es lo que hace que este ayudante siga dando permisos de verdad el día que haya más roles.
        return roles.Elementos.Single(rol => rol.Codigo == "administracion").Id;
    }

    /// <summary>El cuerpo de la respuesta y, si la API dejo un error escrito, ese error.</summary>
    /// <remarks>
    /// El cuerpo de un <c>500</c> no dice nada a proposito, asi que sin la segunda mitad un rojo de
    /// la CI se lee como «esperaba 204 y llego 500» y ahi se acaba. Ver <see cref="RegistroDeFallos"/>.
    /// </remarks>
    /// <param name="respuesta">La respuesta que ha llegado.</param>
    internal static async Task<string> Detalle(HttpResponseMessage respuesta)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        string cuerpo = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);

        return cuerpo + RegistroDeFallos.Ultimos;
    }
}
