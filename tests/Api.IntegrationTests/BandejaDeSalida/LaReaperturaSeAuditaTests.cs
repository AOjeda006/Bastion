using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// Reabrir un ejercicio <b>exige un motivo</b> y <b>deja su evento</b>, por el camino de verdad.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué hace falta un evento y no basta con la fila.</b> El estado del ejercicio dice cómo
/// está <b>ahora</b>, y ese estado no distingue un ejercicio que nunca se cerró de uno que se cerró
/// y se reabrió: los dos ponen «Abierto». Quien revise las cuentas dentro de dos años necesita
/// saber que ocurrió y por qué, y esa pregunta no se le puede hacer a la fila. Por eso es el primer
/// evento de integración que emite este agregado — y por eso <c>Ejercicio</c> pasó a heredar de
/// <c>RaizAgregado</c> en el 2.6 y no antes: cerrar no necesitaba contarse.
/// </para>
/// <para>
/// <b>El motivo se comprueba en los dos sentidos.</b> Sin motivo, 400 con su código <b>y el
/// ejercicio sigue cerrado</b>: sin esa segunda mitad, una validación puesta DESPUÉS de
/// <c>Reabrir()</c> pasaría el 400 dejando el ejercicio abierto y sin nada en la cola, que es el
/// peor de los desenlaces posibles. Con motivo, 204, abierto, y el evento en la bandeja con el
/// motivo dentro.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido, con las migraciones aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaReaperturaSeAuditaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private readonly ApiDeVerdad _api = new(postgres);

    // Las semillas van por `Escenario.NifInventado` y no por un NIF escrito a mano, que es la
    // convención del carril: la letra de control calculada a mano y mal puesta se lee como «el
    // alta de este test da 400» y cuesta un rato ver que el equivocado era el test. Y son la 380
    // y la 381 porque este carril COMPARTE la base con los demás ficheros de la colección: una
    // semilla repetida no falla aquí, falla en el caso de OTRO fichero que la pedía primero.

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Reabrir_sin_motivo_es_400_y_deja_el_ejercicio_cerrado()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(380));
        using HttpClient suyo = cliente;

        (string recurso, _) = await UnEjercicioCerradoAsync(cliente, 2032);

        // Tres espacios, no la cadena vacía: un `[Required]` del borde da «   » por buena, así que
        // si esto pasara, la comprobación estaría en el sitio equivocado.
        using HttpResponseMessage sinMotivo = await ReabrirAsync(cliente, recurso, "   ");

        sinMotivo.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await LeerProblema(sinMotivo)).GetProperty("type").GetString()
            .ShouldBe("/errors/ejercicio-motivo-no-valido");

        // LA SEGUNDA MITAD: la negativa no ha reabierto nada. Con la validación puesta después de
        // `Reabrir()`, el 400 de arriba llegaría igual y el ejercicio se quedaría abierto sin que
        // nadie hubiera escrito por qué.
        (await EstadoDeAsync(cliente, recurso)).ShouldBe(
            "Cerrado",
            "la reapertura se rechazó, así que el ejercicio no puede haber cambiado de estado");
    }

    [Fact]
    public async Task Reabrir_con_motivo_lo_abre_y_deja_el_evento_con_el_motivo_dentro()
    {
        (HttpClient cliente, _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(381));
        using HttpClient suyo = cliente;

        (string recurso, EjercicioDto ejercicio) = await UnEjercicioCerradoAsync(cliente, 2033);

        const string Motivo = "Subsanación: faltaba la factura de diciembre";

        using HttpResponseMessage reapertura = await ReabrirAsync(cliente, recurso, Motivo);

        reapertura.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(reapertura));

        (await EstadoDeAsync(cliente, recurso)).ShouldBe("Abierto");

        (EventoDeLaBandeja fila, EjercicioReabierto evento) = (await ReaperturaDeAsync(ejercicio.Id))
            .ShouldNotBeNull("reabrir por la API no ha dejado su evento en la bandeja");

        fila.Nombre.ShouldBe(EjercicioReabierto.Nombre);

        evento.Motivo.ShouldBe(
            Motivo,
            "el motivo es lo único que queda para entender la reapertura, así que viaja DENTRO " +
            "del evento y no se queda en el cuerpo de la petición");

        evento.Anio.ShouldBe(ejercicio.Anio);
        evento.EmpresaId.ShouldBe(ejercicio.EmpresaId);

        // Y lo publica el trabajo de fondo del propio host, como cualquier otro evento: aquí no se
        // ha arrancado nada a mano.
        (await BandejaDeVerdad.EsperarAsync(async () =>
            await ReaperturaDeAsync(ejercicio.Id) is { Fila.Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("el evento de la reapertura sigue pendiente en la cola");
    }

    private static Task<HttpResponseMessage> ReabrirAsync(
        HttpClient cliente, string recurso, string motivo) =>
        cliente.AccionarAsync(
            recurso,
            $"{recurso}/reapertura",
            HttpMethod.Post,
            JsonContent.Create(new ReabrirEjercicioDto(motivo)));

    private static async Task<string> EstadoDeAsync(HttpClient cliente, string recurso)
    {
        EjercicioDto? leido = await cliente.GetFromJsonAsync<EjercicioDto>(recurso);

        return leido.ShouldNotBeNull().Estado;
    }

    private static async Task<JsonElement> LeerProblema(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

    private static async Task<(string Recurso, EjercicioDto Ejercicio)> UnEjercicioCerradoAsync(
        HttpClient cliente, int anio)
    {
        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            "/api/v1/organizacion/ejercicios",
            new CrearEjercicioDto
            {
                Anio = anio,
                FechaDeInicio = new DateOnly(anio, 1, 1),
                FechaDeFin = new DateOnly(anio, 12, 31),
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        EjercicioDto ejercicio = (await alta.Content.ReadFromJsonAsync<EjercicioDto>()).ShouldNotBeNull();

        string recurso = $"/api/v1/organizacion/ejercicios/{ejercicio.Id}";

        (await cliente.AccionarAsync(recurso, $"{recurso}/cierre", HttpMethod.Post)).StatusCode
            .ShouldBe(
                HttpStatusCode.NoContent,
                "si el cierre no cierra, lo de abajo no está reabriendo nada");

        return (recurso, ejercicio);
    }

    // La cola no tiene endpoint de consulta —eso es de la fase 10—, así que se lee de la base, y se
    // busca por lo que el evento CUENTA: el identificador de la fila no sale por ninguna respuesta.
    private async Task<(EventoDeLaBandeja Fila, EjercicioReabierto Evento)?> ReaperturaDeAsync(
        Guid ejercicioId)
    {
        await using ContextoDeLaBandeja cola = postgres.AbrirBandejaEntera();

        List<EventoDeLaBandeja> filas = await cola.Bandeja
            .AsNoTracking()
            .Where(fila => fila.Nombre == EjercicioReabierto.Nombre)
            .ToListAsync();

        foreach (EventoDeLaBandeja fila in filas)
        {
            EjercicioReabierto evento = JsonSerializer.Deserialize<EjercicioReabierto>(fila.Cuerpo, s_json)!;

            if (evento.EjercicioId == ejercicioId)
            {
                return (fila, evento);
            }
        }

        return null;
    }
}
