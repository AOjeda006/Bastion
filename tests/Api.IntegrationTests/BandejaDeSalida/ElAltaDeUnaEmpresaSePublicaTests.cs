using System.Diagnostics;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    public async Task El_alta_lleva_quien_la_pidio_la_correlacion_de_su_auditoria_y_su_traza()
    {
        // El hueco 3 del 3.4, por el lado del que escribe. La fila lleva el usuario del token, la
        // correlación con la que se auditó la operación y el `traceparent` de su petición, que se
        // manda en una cabecera para saber de qué traza tiene que colgar.
        (HttpClient cliente, SesionDto sesion) = await _api.AbrirComoAdministradorAsync();
        EmpresaDto activa = await Escenario.CrearEmpresaAsync(cliente, Escenario.NifInventado(3_400_001));

        await Escenario.EntrarEnAsync(cliente, sesion.UsuarioId, activa.Id);

        var traza = ActivityTraceId.CreateRandom();
        var tramo = ActivitySpanId.CreateRandom();

        cliente.DefaultRequestHeaders.Add("traceparent", $"00-{traza}-{tramo}-01");

        EmpresaDto nueva = await Escenario.CrearEmpresaAsync(cliente, Escenario.NifInventado(3_400_002));

        EventoDeLaBandeja fila = (await DelAltaDeAsync(nueva.Id)).ShouldNotBeNull();

        fila.UsuarioId.ShouldBe(sesion.UsuarioId);

        // La correlación es la de la traza de auditoría del alta: las dos filas son de la misma
        // operación. Que la traza exista y sea de quien la pidió se afirma antes: sobre una lista
        // vacía, la igualdad de abajo no compara nada.
        List<(Guid Correlacion, Guid? Usuario)> auditadas = await LaAuditoriaDeAsync(nueva.Id);

        auditadas.ShouldNotBeEmpty("el alta no ha dejado su traza de auditoría");
        auditadas.Select(auditada => auditada.Usuario).Distinct().ShouldBe([sesion.UsuarioId]);
        auditadas.Select(auditada => auditada.Correlacion).Distinct().ShouldBe(
            [fila.CorrelacionId.ShouldNotBeNull()],
            "la fila de la bandeja tenía que llevar la correlación con la que se auditó su operación");

        // De la petición, y no de la cabecera tal cual: la misma traza, otro tramo, el del servidor.
        string contexto = fila.ContextoDeTraza.ShouldNotBeNull("la fila no lleva la traza de su petición");

        contexto.ShouldStartWith($"00-{traza}-");
        contexto.ShouldNotContain(
            tramo.ToString(),
            Case.Insensitive,
            "el tramo tenía que ser el de la petición en el servidor");
        contexto.Length.ShouldBe(ConfiguracionDeLaBandeja.LongitudDelContextoDeTraza);
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

    private async Task<List<(Guid Correlacion, Guid? Usuario)>> LaAuditoriaDeAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand consulta = new(
            "SELECT correlacion_id, usuario_id FROM auditoria.registros WHERE entidad_id = @id",
            conexion);
        consulta.Parameters.AddWithValue("id", empresaId.ToString());

        List<(Guid Correlacion, Guid? Usuario)> filas = [];

        await using NpgsqlDataReader lector = await consulta.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            filas.Add((lector.GetGuid(0), await lector.IsDBNullAsync(1) ? null : lector.GetGuid(1)));
        }

        return filas;
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
