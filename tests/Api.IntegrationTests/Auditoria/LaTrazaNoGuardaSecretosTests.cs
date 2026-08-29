using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Identidad.Contracts.Usuarios;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Auditoria;

/// <summary>
/// Lo que la traza <b>no</b> puede llegar a contener nunca.
/// </summary>
/// <remarks>
/// <para>
/// Una tabla de solo añadido que registra el valor viejo y el nuevo de cada propiedad es, sin
/// darse cuenta, el historial completo de resúmenes de contraseña de todo el mundo — en una tabla
/// que por diseño no se puede limpiar. La forma que lo impide es una lista de PERMITIDOS que falla
/// cerrado: cada propiedad dice si se audita, si no se audita o si es secreta, y una sin clasificar
/// pone en rojo el barrido de <c>CadaEntidadDeclaraSuAuditoriaTests</c>.
/// </para>
/// <para>
/// Eso es la forma. Esto es el efecto.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaTrazaNoGuardaSecretosTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Usuarios = "/api/v1/identidad/usuarios";

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Cambiar_la_contrasena_no_deja_ni_el_resumen_viejo_ni_el_nuevo()
    {
        using HttpClient administrador = await _api.ComoAdministradorAsync();

        string sufijo = Guid.CreateVersion7().ToString("N")[^12..];
        string correo = $"secreto-{sufijo}@bastion.pruebas";
        string primera = Guid.CreateVersion7().ToString("N") + "aA1!";
        string segunda = Guid.CreateVersion7().ToString("N") + "bB2!";

        HttpResponseMessage alta = await administrador.PostAsJsonAsync(
            Usuarios,
            new CrearUsuarioDto { Correo = correo, Nombre = "Cuenta con secreto", Contrasena = primera });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
        UsuarioDto usuario = (await alta.Content.ReadFromJsonAsync<UsuarioDto>())!;

        string resumenViejo = await ResumenAsync(usuario.Id);

        using HttpClient suyo = _api.CrearCliente();
        SesionDto _ = await Sesiones.AutenticarAsync(suyo, correo, primera);

        HttpResponseMessage cambio = await suyo.PutAsJsonAsync(
            $"{Usuarios}/actual/contrasena",
            new CambiarContrasenaDto { Actual = primera, Nueva = segunda });

        cambio.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cambio));

        string resumenNuevo = await ResumenAsync(usuario.Id);
        resumenNuevo.ShouldNotBe(resumenViejo, "la contraseña no ha llegado a cambiar: no hay nada que comprobar");

        // Que el cambio SÍ dejó traza. Sin esto, un interceptor que no auditara al usuario en
        // absoluto pasaría este test entero, y lo que se estaría comprobando es que no hay
        // auditoría — no que la haya sin secretos.
        IReadOnlyList<RegistroDeAuditoria> suyas = await Trazas.DeAsync(postgres, "Usuario", usuario.Id);
        suyas.ShouldNotBeEmpty("el alta y el cambio de contraseña son cambios de un maestro");

        IReadOnlyList<RegistroDeAuditoria> todas = await Trazas.TodasAsync(postgres);

        todas.ShouldAllBe(fila => !fila.Valores.Contains(resumenViejo, StringComparison.Ordinal));
        todas.ShouldAllBe(fila => !fila.Valores.Contains(resumenNuevo, StringComparison.Ordinal));

        // Y la contraseña en claro tampoco, que ni siquiera llega a la entidad pero conviene
        // dejarlo dicho: lo que no está escrito no lo comprueba nadie.
        todas.ShouldAllBe(fila => !fila.Valores.Contains(primera, StringComparison.Ordinal));
        todas.ShouldAllBe(fila => !fila.Valores.Contains(segunda, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ningun_valor_de_ninguna_propiedad_secreta_esta_en_ninguna_traza()
    {
        // LA FORMA FUERTE, y la que sigue valiendo cuando alguien marque como secreta una
        // propiedad que hoy no existe: no se nombra ninguna columna aquí. Se le pregunta al modelo
        // cuáles ha declarado secretas, se leen TODOS sus valores de la base y se exige que
        // ninguno aparezca en ninguna fila de traza.
        IReadOnlyList<(string Esquema, string Tabla, string Columna)> secretas = [.. Secretas()];

        secretas.ShouldNotBeEmpty(
            "no hay ni una propiedad declarada secreta: o el barrido no está leyendo el modelo, " +
            "o alguien ha quitado la clasificación de los resúmenes de credencial");

        IReadOnlyList<RegistroDeAuditoria> todas = await Trazas.TodasAsync(postgres);
        todas.ShouldNotBeEmpty("sin trazas no hay nada que comprobar");

        List<string> filtrados = [];

        foreach ((string esquema, string tabla, string columna) in secretas)
        {
            foreach (string valor in await ValoresAsync(esquema, tabla, columna))
            {
                if (todas.Any(fila => fila.Valores.Contains(valor, StringComparison.Ordinal)))
                {
                    filtrados.Add($"{esquema}.{tabla}.{columna}");
                }
            }
        }

        filtrados.ShouldBeEmpty(
            "estos valores secretos han acabado escritos en la tabla de traza: " +
            string.Join(", ", filtrados.Distinct(StringComparer.Ordinal)));
    }

    private IEnumerable<(string Esquema, string Tabla, string Columna)> Secretas()
    {
        using IdentidadDbContext identidad = postgres.AbrirIdentidadParaMigrar();
        using OrganizacionDbContext organizacion = postgres.AbrirOrganizacionParaMigrar();

        return [.. De(identidad), .. De(organizacion)];
    }

    private static IEnumerable<(string Esquema, string Tabla, string Columna)> De(DbContext contexto)
    {
        foreach (IEntityType tipo in contexto.Model.GetEntityTypes())
        {
            if (StoreObjectIdentifier.Create(tipo, StoreObjectType.Table) is not { } tabla)
            {
                continue;
            }

            foreach (IProperty propiedad in tipo.GetProperties())
            {
                if (propiedad.Auditoria().Que == ClasificacionDeAuditoria.Secreta)
                {
                    yield return (tabla.Schema ?? "public", tabla.Name, propiedad.GetColumnName(tabla)!);
                }
            }
        }
    }

    private async Task<string> ResumenAsync(Guid usuarioId)
    {
        IReadOnlyList<string> resumenes = await ValoresAsync(
            IdentidadDbContext.Esquema, "usuarios", "hash_de_contrasena", $"WHERE id = '{usuarioId}'");

        return resumenes.ShouldHaveSingleItem();
    }

    private async Task<IReadOnlyList<string>> ValoresAsync(
        string esquema,
        string tabla,
        string columna,
        string filtro = "")
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            $"SELECT {columna} FROM {esquema}.{tabla} {filtro}", conexion);
        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        List<string> valores = [];

        while (await lector.ReadAsync())
        {
            if (!await lector.IsDBNullAsync(0))
            {
                valores.Add(lector.GetValue(0).ToString()!);
            }
        }

        return valores;
    }
}
