using System.Collections.Concurrent;
using System.Diagnostics;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// El hueco 3 del 3.4, detrás del despachador: <b>el manejador corre con la causa de su fila</b>,
/// y lo que escribe la hereda.
/// </summary>
/// <remarks>
/// <para>
/// La fila lleva quién causó el evento, la correlación de su operación y el <c>traceparent</c> de
/// su traza. En el ámbito de cada manejador, el despachador restaura las tres: su efecto se audita
/// a nombre de ese usuario y con esa correlación, su respuesta lleva las tres, y su actividad es
/// hija de la traza de la fila. Que la fila las lleve al escribirse lo prueba
/// <see cref="ElAltaDeUnaEmpresaSePublicaTests"/>, por la API.
/// </para>
/// <para>
/// <b>La causa de cada caso es inventada a propósito</b>, con un usuario y una correlación que no
/// tiene nadie más: una que coincidiera con la del ámbito del manejador no distinguiría «la de su
/// fila» de «la suya».
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElManejadorVaConQuienLoCausoTests(PostgresConTodosLosModulos postgres)
{
    // Lo que deja un manejador que escribe, con la causa de cada fila: la traza de auditoría de
    // cada documento con la marca del caso, y cada evento de respuesta.
    private const string LasCausasDeSusFilas = """
        SELECT 'traza ' || documento.marca, registro.usuario_id, registro.correlacion_id, NULL::text
        FROM auditoria.registros AS registro
        JOIN pruebas_de_la_bandeja.documentos AS documento ON registro.entidad_id = documento.id::text
        WHERE documento.marca LIKE @marca || '%' AND registro.entidad = 'DocumentoDePrueba'
        UNION ALL
        SELECT 'respuesta ' || (cuerpo ->> 'marca'), usuario_id, correlacion_id, contexto_de_traza
        FROM auditoria.bandeja_de_salida
        WHERE nombre = 'pruebas.respuesta-de-prueba' AND cuerpo ->> 'marca' LIKE @marca || '%'
        """;

    [Fact]
    public async Task El_efecto_y_su_respuesta_llevan_el_usuario_la_correlacion_y_la_traza_de_la_fila()
    {
        // Lo que se guarda SIN nadie escuchando la fuente: la traza de la respuesta es la de la fila.
        // El caso lo afirma antes de empezar en vez de heredarlo: un host de la API vivo en este
        // proceso registraría su oyente, y con él la traza sería la de la actividad del manejador.
        using (ActivitySource sonda = new(TrazasDeLaBandeja.Fuente))
        {
            sonda.HasListeners().ShouldBeFalse("este caso mide lo que se guarda sin oyente, y alguien escucha");
        }

        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueEscribe, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());
        Causa causa = Inventada();

        (await bandeja.DespacharAsync(hecho, causa: causa)).ShouldBe(1);

        List<Fila> filas = await LasCausasAsync(cadena, hecho.Marca);

        // Que estén las dos antes de mirar su causa: sobre una lista vacía, lo de abajo no mira nada.
        filas.Select(fila => fila.Nombre).Order(StringComparer.Ordinal)
            .ShouldBe([$"respuesta {hecho.Marca}", $"traza {hecho.Marca}"]);

        filas.Where(fila => fila.Usuario != causa.UsuarioId || fila.Correlacion != causa.CorrelacionId)
            .ShouldBeEmpty(
                $"las dos tenían que llevar el usuario {causa.UsuarioId} y la correlación " +
                $"{causa.CorrelacionId} de su fila: {Describir(filas)}");

        filas.Single(fila => fila.Nombre.StartsWith("respuesta", StringComparison.Ordinal))
            .Traza.ShouldBe(causa.ContextoDeTraza);
    }

    [Fact]
    public async Task Una_fila_sin_correlacion_recibe_una_nueva_y_la_misma_para_todos_sus_manejadores()
    {
        // La fila escrita antes del 3.4, que no la guardaba: se encola y se le quita a pelo, porque
        // la factoría ya no deja escribir una fila sin causa.
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios
                .AgregarManejadorDeEvento<ManejadorQueEscribe, ContextoDePrueba>()
                .AgregarManejadorDeEvento<ManejadorQueConfirmaDosVeces, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());

        await bandeja.EncolarAsync(hecho, causa: Inventada());
        await QuitarLaCausaAsync(cadena, hecho.EventoId);

        EventoDeLaBandeja sinCausa = (await bandeja.EnLaColaAsync(hecho.EventoId)).ShouldNotBeNull();

        sinCausa.CorrelacionId.ShouldBeNull("la fila tenía que haberse quedado sin correlación, como las de antes");

        (await bandeja.DespacharLaFilaAsync(sinCausa)).ShouldBe(2);

        List<Fila> filas = await LasCausasAsync(cadena, hecho.Marca);

        // Tres documentos —uno del primero, dos del segundo— con su traza y su respuesta. El segundo
        // confirma dos veces en su ámbito, y el primero, en otro.
        filas.Count.ShouldBe(6, Describir(filas));

        List<Guid?> correlaciones = [.. filas.Select(fila => fila.Correlacion).Distinct()];

        correlaciones.Count.ShouldBe(
            1,
            $"los dos manejadores tenían que compartir la correlación: {Describir(filas)}");
        correlaciones[0].ShouldNotBeNull().ShouldNotBe(Guid.Empty);

        filas.Where(fila => fila.Usuario is not null || fila.Traza is not null).ShouldBeEmpty(
            $"de una fila sin causa no se inventa ni el usuario ni la traza: {Describir(filas)}");
    }

    [Fact]
    public async Task La_actividad_del_manejador_es_hija_de_la_traza_de_su_fila()
    {
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueEscribe, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());
        Causa causa = Inventada();

        using Oyente oyente = new();

        (await bandeja.DespacharAsync(hecho, causa: causa)).ShouldBe(1);

        Activity actividad = oyente.DelEvento(hecho.EventoId).ShouldHaveSingleItem();
        var deLaFila = ActivityContext.Parse(causa.ContextoDeTraza!, traceState: null);

        actividad.Kind.ShouldBe(ActivityKind.Consumer);
        actividad.DisplayName.ShouldBe($"process {HechoDePrueba.Nombre}");
        actividad.TraceId.ShouldBe(deLaFila.TraceId, "la actividad tenía que ir en la traza de la fila");
        actividad.ParentSpanId.ShouldBe(deLaFila.SpanId, "y colgar del tramo de la fila, como hija");
        actividad.HasRemoteParent.ShouldBeTrue();
        actividad.GetTagItem("messaging.consumer.group.name").ShouldBe("pruebas.el-que-escribe");
        actividad.Status.ShouldBe(ActivityStatusCode.Unset);

        // Y lo que escribe el manejador cuelga de su actividad, no de la fila: una cadena de
        // eventos se lee como una escalera, y no como hermanos de la petición.
        (await LasCausasAsync(cadena, hecho.Marca))
            .Single(fila => fila.Nombre.StartsWith("respuesta", StringComparison.Ordinal))
            .Traza.ShouldBe(actividad.Id);
    }

    [Fact]
    public async Task La_actividad_de_un_manejador_que_falla_queda_en_error()
    {
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueSiempreFalla, ContextoDeLaBandeja>());

        HechoDePrueba hecho = new(Marca());

        using Oyente oyente = new();

        await Should.ThrowAsync<InvalidOperationException>(() => bandeja.DespacharAsync(hecho, causa: Inventada()));

        Activity actividad = oyente.DelEvento(hecho.EventoId).ShouldHaveSingleItem();

        actividad.Status.ShouldBe(ActivityStatusCode.Error);
        actividad.StatusDescription.ShouldNotBeNull().ShouldContain("no va a funcionar");
    }

    // Cada caso se lleva su marca, por lo mismo que en los demás de la bandeja.
    private static string Marca() => Guid.CreateVersion7().ToString();

    // Una causa que no tiene nadie más, con su traza de verdad: formato W3C, tramo muestreado.
    private static Causa Inventada() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        $"00-{ActivityTraceId.CreateRandom()}-{ActivitySpanId.CreateRandom()}-01");

    private static string Describir(List<Fila> filas) =>
        string.Join(", ", filas.Select(fila => $"{fila.Nombre} {fila.Usuario} {fila.Correlacion} {fila.Traza}"));

    private static async Task QuitarLaCausaAsync(string cadena, Guid eventoId)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            """
            UPDATE auditoria.bandeja_de_salida
            SET usuario_id = NULL, correlacion_id = NULL, contexto_de_traza = NULL
            WHERE evento_id = @evento
            """,
            conexion);
        orden.Parameters.AddWithValue("evento", eventoId);

        (await orden.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    private static async Task<List<Fila>> LasCausasAsync(string cadena, string marca)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand consulta = new(LasCausasDeSusFilas, conexion);
        consulta.Parameters.AddWithValue("marca", marca);

        List<Fila> filas = [];

        await using NpgsqlDataReader lector = await consulta.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            filas.Add(new Fila(
                lector.GetString(0),
                await lector.IsDBNullAsync(1) ? null : lector.GetGuid(1),
                await lector.IsDBNullAsync(2) ? null : lector.GetGuid(2),
                await lector.IsDBNullAsync(3) ? null : lector.GetString(3)));
        }

        return filas;
    }

    private sealed record Fila(string Nombre, Guid? Usuario, Guid? Correlacion, string? Traza);

    // Escucha la fuente de la bandeja mientras vive. El oyente es del proceso entero, así que lo que
    // ve se separa por el evento de cada caso, que es una de las etiquetas de la actividad.
    private sealed class Oyente : IDisposable
    {
        private readonly ConcurrentQueue<Activity> _terminadas = new();
        private readonly ActivityListener _oyente;

        public Oyente()
        {
            _oyente = new ActivityListener
            {
                ShouldListenTo = fuente => fuente.Name == TrazasDeLaBandeja.Fuente,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = _terminadas.Enqueue,
            };

            ActivitySource.AddActivityListener(_oyente);
        }

        public List<Activity> DelEvento(Guid eventoId) =>
        [
            .. _terminadas.Where(actividad => Equals(
                actividad.GetTagItem("messaging.message.id"),
                eventoId.ToString())),
        ];

        public void Dispose() => _oyente.Dispose();
    }
}
