using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Auditoria;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Identidad.Application.Arranque;
using Bastion.Identidad.Contracts.Roles;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Autorizacion;

/// <summary>
/// El rol del sistema tiene el catálogo entero de la versión desplegada, y nadie se lo recorta
/// (ADR-0035).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que este fichero NO prueba es el despliegue.</b> El caso de uso lo llama el migrador, y
/// el migrador no corre aquí: la base de estos tests la migra la fixture. Que el segundo arranque de
/// verdad —imágenes, compose, el mismo volumen— deje el rol al día lo prueba
/// <c>scripts/ci/segundo-arranque.sh</c> en el Humo. Aquí se prueba lo que ese guion no puede ver
/// sin coste: qué dice el caso de uso, qué deja en la traza y que la API cierra la puerta.
/// </para>
/// <para>
/// <b>El estado viejo se monta con SQL, y es a propósito:</b> es el estado que deja una versión
/// anterior, y ninguna operación de esta versión sabe producirlo —justo lo que el caso de uso
/// existe para corregir—.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElRolDelSistemaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Roles = "/api/v1/identidad/roles";

    // Con forma de permiso y sin nadie que lo declare: lo que queda en la base cuando una versión
    // retira uno.
    private const string Retirado = "organizacion.permiso-de-una-version-anterior.ver";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Recortado_y_con_un_permiso_retirado_el_despliegue_lo_deja_con_el_catalogo_y_lo_dice()
    {
        IReadOnlyList<string> catalogo = Catalogo();
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        RolDto rol = await RolDelSistemaAsync(cliente);

        // Tres del principio, del medio y del final: un recorte que solo quitara los primeros
        // pasaría con un caso de uso que mirara solo los primeros.
        string[] quitados = [catalogo[0], catalogo[catalogo.Count / 2], catalogo[^1]];

        await RecortarAsync(rol.Id, quitados);

        try
        {
            (await PermisosEnLaBaseAsync(rol.Id)).Count.ShouldBe(catalogo.Count - quitados.Length + 1);

            RolDelSistemaActualizado primera = (await ActualizarAsync()).ShouldHaveSingleItem();

            primera.Codigo.ShouldBe(rol.Codigo);
            primera.Concedidos.ShouldBe([.. quitados.Order(StringComparer.Ordinal)]);
            primera.Retirados.ShouldBe([Retirado]);
            (await PermisosEnLaBaseAsync(rol.Id)).ShouldBe(catalogo);

            // Una traza por fila tocada, sin empresa y con su motivo propio: el día que el rol
            // amanezca distinto, la tabla tiene que decir que fue el despliegue y no la semilla.
            IReadOnlyList<RegistroDeAuditoria> trazas = [.. (await Trazas.TodasAsync(postgres))
                .Where(fila => fila.SinInquilino == MotivoSinInquilino.ActualizacionDeRolesDelSistema)];

            trazas.Count.ShouldBe(quitados.Length + 1);
            trazas.ShouldAllBe(fila => fila.EmpresaId == null && fila.Entidad == "PermisoDeRol");

            // Y la segunda vez no hay nada que hacer, y lo dice con las dos listas vacías.
            RolDelSistemaActualizado segunda = (await ActualizarAsync()).ShouldHaveSingleItem();

            segunda.Concedidos.ShouldBeEmpty();
            segunda.Retirados.ShouldBeEmpty();
        }
        finally
        {
            // Si una afirmación de arriba cae, el rol no se queda recortado para los demás tests
            // de la colección, que entran todos como este administrador.
            await ActualizarAsync();
        }
    }

    [Fact]
    public async Task Cambiarle_la_lista_al_rol_del_sistema_es_409_y_no_toca_nada()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        RolDto rol = await RolDelSistemaAsync(cliente);

        using HttpResponseMessage respuesta = await cliente.ModificarAsync(
            $"{Roles}/{rol.Id}",
            new ModificarRolDto { Nombre = rol.Nombre, Permisos = [.. rol.Permisos.Skip(1)] });

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Conflict, await Escenario.Detalle(respuesta));

        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("type").GetString().ShouldBe("/errors/permisos-de-rol-del-sistema");

        (await LeerAsync(cliente, rol.Id)).Permisos.ShouldBe(rol.Permisos);
    }

    [Fact]
    public async Task Renombrarlo_con_la_misma_lista_en_otro_orden_vale()
    {
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        RolDto rol = await RolDelSistemaAsync(cliente);

        try
        {
            // La lista se compara como conjunto: un formulario que la reenvía en el orden en que
            // pinta las casillas no tiene por qué recibir un 409.
            using HttpResponseMessage respuesta = await cliente.ModificarAsync(
                $"{Roles}/{rol.Id}",
                new ModificarRolDto { Nombre = "Administración general", Permisos = [.. rol.Permisos.Reverse()] });

            respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(respuesta));
            (await LeerAsync(cliente, rol.Id)).Nombre.ShouldBe("Administración general");
        }
        finally
        {
            using HttpResponseMessage vuelta = await cliente.ModificarAsync(
                $"{Roles}/{rol.Id}",
                new ModificarRolDto { Nombre = rol.Nombre, Permisos = rol.Permisos });

            vuelta.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(vuelta));
        }
    }

    [Fact]
    public async Task Un_rol_propio_sigue_cambiando_de_permisos()
    {
        IReadOnlyList<string> catalogo = Catalogo();
        using HttpClient cliente = await _api.ComoAdministradorAsync();
        string sufijo = Guid.CreateVersion7().ToString("N")[^12..];

        HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Roles,
            new CrearRolDto { Codigo = $"propio-{sufijo}", Nombre = "Rol propio", Permisos = [catalogo[0]] });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
        RolDto rol = (await alta.Content.ReadFromJsonAsync<RolDto>())!;

        using HttpResponseMessage cambio = await cliente.ModificarAsync(
            $"{Roles}/{rol.Id}",
            new ModificarRolDto { Nombre = rol.Nombre, Permisos = [catalogo[^1]] });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        (await LeerAsync(cliente, rol.Id)).Permisos.ShouldBe([catalogo[^1]]);
    }

    private IReadOnlyList<string> Catalogo() =>
        [.. _api.Services.GetRequiredService<ICatalogoDePermisos>().Todos
            .Select(permiso => permiso.Valor)
            .Order(StringComparer.Ordinal)];

    private async Task<IReadOnlyList<RolDelSistemaActualizado>> ActualizarAsync()
    {
        using IServiceScope ambito = _api.Services.CreateScope();

        return await ambito.ServiceProvider
            .GetRequiredService<IActualizarRolesDelSistema>()
            .EjecutarAsync(CancellationToken.None);
    }

    private static async Task<RolDto> RolDelSistemaAsync(HttpClient cliente)
    {
        PaginaDe<RolDto>? roles = await cliente.GetFromJsonAsync<PaginaDe<RolDto>>($"{Roles}?page=1&size=200");

        roles.ShouldNotBeNull();

        return roles.Elementos.Single(rol => rol.EsDelSistema);
    }

    private static async Task<RolDto> LeerAsync(HttpClient cliente, Guid id) =>
        (await cliente.GetFromJsonAsync<RolDto>($"{Roles}/{id}"))!;

    private async Task RecortarAsync(Guid rolId, string[] quitados)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();
        await using NpgsqlCommand orden = new(
            "DELETE FROM identidad.permisos_de_rol WHERE rol_id = @rol AND permiso = ANY(@quitados);" +
            "INSERT INTO identidad.permisos_de_rol (rol_id, permiso) VALUES (@rol, @retirado);",
            conexion);
        orden.Parameters.AddWithValue("rol", rolId);
        orden.Parameters.AddWithValue("quitados", quitados);
        orden.Parameters.AddWithValue("retirado", Retirado);
        await orden.ExecuteNonQueryAsync();
    }

    private async Task<IReadOnlyList<string>> PermisosEnLaBaseAsync(Guid rolId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();
        await using NpgsqlCommand orden = new(
            "SELECT permiso FROM identidad.permisos_de_rol WHERE rol_id = @rol ORDER BY permiso COLLATE \"C\"",
            conexion);
        orden.Parameters.AddWithValue("rol", rolId);

        List<string> permisos = [];
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            permisos.Add(lector.GetString(0));
        }

        return permisos;
    }
}
