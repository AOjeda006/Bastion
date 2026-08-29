using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// Las tres cláusulas del R12 sobre un hecho de verdad, por el camino de verdad: una empresa dada
/// de alta por la API, su evento en la cola, y el trabajo de fondo del propio host vaciándola.
/// </summary>
/// <remarks>
/// <para>
/// Los demás tests de esta carpeta montan la bandeja con manejadores de prueba, que es lo que
/// permite comprobar los caminos de fallo. Este no monta nada: es el host que se despliega, con su
/// cableado, su interceptor enganchado al contexto de Organización y su publicador arrancado.
/// Una maquinaria que solo ha movido un evento inventado demuestra la fontanería y nada del
/// modelo.
/// </para>
/// <para>
/// <b>Y en la fase 0 no lo escucha nadie</b>, a propósito: los módulos que van a reaccionar a un
/// alta —Contabilidad sembrando su plan, Notificaciones dando la bienvenida— no existen todavía.
/// Que el evento salga igual es la mitad del R12 que se olvida: quien decide contar lo que le ha
/// pasado es el emisor, no el receptor.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido, con las migraciones aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElAltaDeUnaEmpresaSePublicaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private readonly ApiDeVerdad _api = new(postgres);

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Dar_de_alta_una_empresa_deja_su_evento_en_la_cola_y_el_host_lo_publica()
    {
        (HttpClient cliente, EmpresaDto activa) = await _api.EnUnaEmpresaNuevaAsync("00000053F");

        EmpresaDto nueva = await Escenario.CrearEmpresaAsync(cliente, "00000054P");

        EventoDeLaBandeja fila = (await DelAltaDeAsync(nueva.Id)).ShouldNotBeNull(
            "dar de alta una empresa por la API no ha dejado su evento en la bandeja");

        fila.Nombre.ShouldBe(EmpresaCreada.Nombre);

        // De quién es la fila: de la empresa que estaba operando, no de la que se acaba de crear.
        // La cola es un dato del inquilino que la escribió, y lo que el evento CUENTA va dentro.
        fila.EmpresaId.ShouldBe(activa.Id);
        fila.SinInquilino.ShouldBeNull();

        // Y lo publica el trabajo de fondo del propio host: aquí no se ha arrancado nada.
        (await BandejaDeVerdad.EsperarAsync(async () =>
            await DelAltaDeAsync(nueva.Id) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("el evento sigue pendiente: el trabajo de fondo del host no lo ha publicado");
    }

    [Fact]
    public async Task El_alta_que_hace_la_semilla_se_publica_igual_y_dice_por_que_no_tiene_empresa()
    {
        // El arranque en frío: la primera empresa la crea la semilla, cuando todavía no hay
        // ninguna empresa activa ni ningún usuario. Ese camino es el que rompería una bandeja que
        // diera por hecho que siempre hay inquilino — y es exactamente el motivo por el que la
        // columna es anulable y lleva al lado el porqué.
        _ = _api.CrearCliente();

        EventoDeLaBandeja fila = (await DeLaSemillaAsync()).ShouldNotBeNull(
            "la empresa de la semilla no ha dejado su evento");

        fila.EmpresaId.ShouldBeNull();
        fila.SinInquilino.ShouldBe(MotivoSinInquilino.SemillaDeArranque);

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await DeLaSemillaAsync() is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("un evento sin inquilino se publica igual que los demás");
    }

    // La cola no tiene endpoint de consulta —eso es de la fase 10—, así que se lee de la base. Y
    // se busca por lo que el evento CUENTA, no por la fila: quien da de alta la empresa es la API,
    // y el identificador del evento no sale por ninguna respuesta.
    private async Task<EventoDeLaBandeja?> DelAltaDeAsync(Guid empresaId)
    {
        foreach ((EventoDeLaBandeja fila, EmpresaCreada evento) in await AltasAsync())
        {
            if (evento.EmpresaId == empresaId)
            {
                return fila;
            }
        }

        return null;
    }

    private async Task<EventoDeLaBandeja?> DeLaSemillaAsync()
    {
        foreach ((EventoDeLaBandeja fila, EmpresaCreada evento) in await AltasAsync())
        {
            if (string.Equals(evento.Nif, ApiDeVerdad.NifDeLaSemilla, StringComparison.Ordinal))
            {
                return fila;
            }
        }

        return null;
    }

    private async Task<List<(EventoDeLaBandeja Fila, EmpresaCreada Evento)>> AltasAsync()
    {
        await using ContextoDeLaBandeja cola = postgres.AbrirBandejaEntera();

        List<EventoDeLaBandeja> filas = await cola.Bandeja
            .AsNoTracking()
            .Where(fila => fila.Nombre == EmpresaCreada.Nombre)
            .ToListAsync();

        return [.. filas.Select(fila => (fila, JsonSerializer.Deserialize<EmpresaCreada>(fila.Cuerpo, s_json)!))];
    }
}
