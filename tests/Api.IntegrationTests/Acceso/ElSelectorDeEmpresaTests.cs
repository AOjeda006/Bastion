using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Empresas;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Acceso;

/// <summary>
/// El selector de empresa: de dónde salen los nombres y en qué empresas se puede estar.
/// </summary>
/// <remarks>
/// <para>
/// Hasta el 0.11 la sesión devolvía una lista de identificadores, y con eso no se pinta un
/// desplegable: nadie elige entre <c>a3f1…</c> y <c>7c02…</c>. El nombre no puede salir de
/// <c>GET /organizacion/empresas</c>, que exige el permiso <c>organizacion.empresa.ver</c>:
/// <b>pertenecer a varias empresas no implica poder ver la ficha de ninguna</b>. Sale de la propia
/// sesión, y lo pone Organización por su interfaz de <c>Contracts</c>.
/// </para>
/// <para>
/// Y el selector es <b>también</b> la lista de empresas en las que se puede abrir sesión. Que lo
/// visible y lo operable sean la misma lista es lo que impide que una empresa suprimida al amparo
/// del art. 32 (R16) se caiga del desplegable y siga, al mismo tiempo, siendo la empresa activa de
/// alguien. Sin eso, el bloqueo taparía la empresa en las pantallas y no echaría a nadie de ella.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElSelectorDeEmpresaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string RutaDeEmpresas = "/api/v1/organizacion/empresas";
    private const string RutaDeUsuarios = "/api/v1/identidad/usuarios";
    private const string RutaDeSesiones = "/api/v1/identidad/sesiones";
    private const string RutaDeLaEmpresaActiva = "/api/v1/identidad/sesiones/actual/empresa";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task El_selector_trae_los_nombres_aunque_no_se_tenga_permiso_para_ver_empresas()
    {
        Reparto reparto = await RepartoAsync(
            "00000081N", "Zeta Suministros SL",
            "00000082J", "Alfa Materiales SL");

        using HttpClient administrador = reparto.Administrador;
        using HttpClient cliente = _api.CrearCliente();

        SesionDto sesion = await Sesiones.AutenticarAsync(cliente, reparto.Correo, reparto.Contrasena);

        // La cuenta se ha creado sin un solo rol. Si tuviera permisos, este test no probaría nada:
        // el nombre podría estar llegando por el camino que se ha descartado.
        sesion.Permisos.ShouldBeEmpty();

        using HttpResponseMessage padron = await cliente.GetAsync(RutaDeEmpresas);
        padron.StatusCode.ShouldBe(
            HttpStatusCode.Forbidden,
            "el padrón de empresas le está cerrado, y aun así el desplegable tiene que tener nombres");

        // Los dos nombres, y en orden alfabético. La empresa que sale PRIMERA es la que se creó y
        // se concedió la SEGUNDA: si el orden fuera el de la base o el de las pertenencias, esta
        // comprobación saldría al revés.
        sesion.Empresas.Select(empresa => empresa.RazonSocial)
            .ShouldBe(["Alfa Materiales SL", "Zeta Suministros SL"]);

        sesion.Empresas.Select(empresa => empresa.Id)
            .ShouldBe([reparto.Segunda.Id, reparto.Primera.Id]);

        // Y con lo que se está operando está en la lista con la que se puede cambiar. Un selector
        // que no contiene a su propia selección es un desplegable con la casilla en blanco.
        sesion.Empresas.ShouldContain(empresa => empresa.Id == sesion.EmpresaActivaId);
    }

    [Fact]
    public async Task Una_empresa_bloqueada_se_cae_del_selector_y_su_pertenencia_sigue_en_la_tabla()
    {
        Reparto reparto = await RepartoAsync(
            "00000083Z", "Gamma Talleres SL",
            "00000084S", "Delta Envases SL");

        using HttpClient administrador = reparto.Administrador;

        using HttpResponseMessage borrado = await administrador
            .SuprimirAsync($"{RutaDeEmpresas}/{reparto.Primera.Id}");

        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(borrado));

        using HttpClient cliente = _api.CrearCliente();
        SesionDto sesion = await Sesiones.AbrirAsync(cliente, reparto.Correo, reparto.Contrasena);

        sesion.Empresas.Select(empresa => empresa.RazonSocial).ShouldBe(["Delta Envases SL"]);

        // Y no se ha caído porque se le haya quitado la pertenencia: la fila sigue exactamente
        // donde estaba. Lo que la esconde es el filtro de R16 en la consulta que puebla el
        // selector, que es la mitad del art. 32 que dice que el dato se conserva.
        //
        // Se cuenta con SQL en crudo por lo mismo que en `LaFilaBloqueadaSigueEnLaBase`: cualquier
        // lectura por EF Core pasaría por el filtro que este test existe para comprobar.
        long pertenencia = await PertenenciasAsync(reparto.UsuarioId, reparto.Primera.Id);
        pertenencia.ShouldBe(1, "la pertenencia se conserva; lo que desaparece es la empresa");
    }

    [Fact]
    public async Task La_sesion_no_se_abre_en_una_empresa_bloqueada_aunque_sea_la_primera_pertenencia()
    {
        Reparto reparto = await RepartoAsync(
            "00000085Q", "Kappa Montajes SL",
            "00000086V", "Lambda Recambios SL");

        using HttpClient administrador = reparto.Administrador;

        using HttpResponseMessage borrado = await administrador
            .SuprimirAsync($"{RutaDeEmpresas}/{reparto.Primera.Id}");

        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(borrado));

        // Sin pedir empresa se entra en la primera pertenencia QUE QUEDE EN PIE, no en la primera
        // a secas. La primera de esta cuenta es justo la que se acaba de suprimir.
        using HttpClient cliente = _api.CrearCliente();
        SesionDto sesion = await Sesiones.AbrirAsync(cliente, reparto.Correo, reparto.Contrasena);

        sesion.EmpresaActivaId.ShouldBe(reparto.Segunda.Id);

        // Y pedirla por su identificador tampoco entra. Sale por donde salen las credenciales
        // malas —mismo 401, mismo texto—: contestar «esa empresa está suprimida» convertiría el
        // formulario de acceso en la consulta que el art. 32 impide, y a quien aún no ha entrado.
        using HttpResponseMessage pedida = await cliente.PostAsJsonAsync(
            RutaDeSesiones,
            new IniciarSesionDto
            {
                Correo = reparto.Correo,
                Contrasena = reparto.Contrasena,
                EmpresaId = reparto.Primera.Id,
            });

        pedida.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await Escenario.Detalle(pedida));
    }

    [Fact]
    public async Task Cambiar_a_una_empresa_bloqueada_se_rechaza_como_si_no_se_perteneciera()
    {
        Reparto reparto = await RepartoAsync(
            "00000087H", "Sigma Herrajes SL",
            "00000088L", "Tau Embalajes SL");

        using HttpClient administrador = reparto.Administrador;

        using HttpResponseMessage borrado = await administrador
            .SuprimirAsync($"{RutaDeEmpresas}/{reparto.Primera.Id}");

        borrado.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(borrado));

        using HttpClient cliente = _api.CrearCliente();
        SesionDto sesion = await Sesiones.AutenticarAsync(cliente, reparto.Correo, reparto.Contrasena);
        sesion.EmpresaActivaId.ShouldBe(reparto.Segunda.Id);

        using HttpResponseMessage cambio = await cliente.PutAsJsonAsync(
            RutaDeLaEmpresaActiva,
            new CambiarEmpresaDto { EmpresaId = reparto.Primera.Id });

        cambio.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await Escenario.Detalle(cambio));

        // El MISMO error que si no perteneciera, y con el mismo código. Uno propio confirmaría que
        // la empresa existe y está bloqueada, que es exactamente lo que el 404 del 0.10 se niega a
        // confirmar: sería el mismo agujero por otra puerta.
        (await cambio.Content.ReadAsStringAsync()).ShouldContain("/errors/empresa-no-pertenece");
    }

    /// <summary>
    /// Un usuario sin ningún permiso que pertenece a dos empresas nuevas, en ese orden.
    /// </summary>
    /// <remarks>
    /// El administrador entra en la primera antes de poblarla: a partir de ahí puede conceder
    /// pertenencias en ella —la excepción de arranque en frío se cierra en cuanto hay alguien
    /// dentro— y puede suprimirla, que es lo que necesitan tres de los cuatro casos. La segunda se
    /// concede por esa misma excepción, porque nace vacía.
    /// </remarks>
    private async Task<Reparto> RepartoAsync(
        string nifPrimera,
        string nombrePrimera,
        string nifSegunda,
        string nombreSegunda)
    {
        (HttpClient administrador, SesionDto sesion) = await _api.AbrirComoAdministradorAsync();

        EmpresaDto primera = await CrearAsync(administrador, nifPrimera, nombrePrimera);
        await Escenario.EntrarEnAsync(administrador, sesion.UsuarioId, primera.Id);

        EmpresaDto segunda = await CrearAsync(administrador, nifSegunda, nombreSegunda);

        string sufijo = Guid.CreateVersion7().ToString("N")[^12..];
        string correo = $"selector-{sufijo}@bastion.pruebas";
        string contrasena = Guid.CreateVersion7().ToString("N") + "aA1!";

        using HttpResponseMessage alta = await administrador.PostAsJsonAsync(
            RutaDeUsuarios,
            new CrearUsuarioDto
            {
                Correo = correo,
                Nombre = "Cuenta de dos empresas",
                Contrasena = contrasena,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
        Guid usuarioId = (await alta.Content.ReadFromJsonAsync<UsuarioDto>())!.Id;

        // Primero la primera. El orden de las pertenencias es el de estas dos llamadas, y de él
        // depende cuál es «la primera» cuando se entra sin pedir empresa.
        await ConcederAsync(administrador, usuarioId, primera.Id);
        await ConcederAsync(administrador, usuarioId, segunda.Id);

        return new Reparto(administrador, primera, segunda, usuarioId, correo, contrasena);
    }

    private static async Task<EmpresaDto> CrearAsync(HttpClient cliente, string nif, string razonSocial)
    {
        using HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(
            RutaDeEmpresas,
            Escenario.NuevaEmpresa(nif) with { RazonSocial = razonSocial });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(respuesta));

        return (await respuesta.Content.ReadFromJsonAsync<EmpresaDto>())!;
    }

    private static async Task ConcederAsync(HttpClient administrador, Guid usuarioId, Guid empresaId)
    {
        using HttpResponseMessage concedida = await administrador.PostAsJsonAsync(
            $"{RutaDeUsuarios}/{usuarioId}/pertenencias",
            new ConcederPertenenciaDto { EmpresaId = empresaId });

        concedida.StatusCode.ShouldBe(
            HttpStatusCode.NoContent, await Escenario.Detalle(concedida));
    }

    private async Task<long> PertenenciasAsync(Guid usuarioId, Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            $"SELECT COUNT(*) FROM {IdentidadDbContext.Esquema}.membresias " +
            $"WHERE usuario_id = '{usuarioId}' AND empresa_id = '{empresaId}'",
            conexion);

        return (long)(await orden.ExecuteScalarAsync())!;
    }

    /// <summary>Lo que deja montado <see cref="RepartoAsync"/>.</summary>
    /// <param name="Administrador">Cliente del administrador, operando dentro de la primera.</param>
    /// <param name="Primera">Empresa creada y concedida en primer lugar.</param>
    /// <param name="Segunda">Empresa creada y concedida en segundo lugar.</param>
    /// <param name="UsuarioId">La cuenta sin permisos que pertenece a las dos.</param>
    /// <param name="Correo">Su correo.</param>
    /// <param name="Contrasena">Su contraseña.</param>
    private sealed record Reparto(
        HttpClient Administrador,
        EmpresaDto Primera,
        EmpresaDto Segunda,
        Guid UsuarioId,
        string Correo,
        string Contrasena);
}
