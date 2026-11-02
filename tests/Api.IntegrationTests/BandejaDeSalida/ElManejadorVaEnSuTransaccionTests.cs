using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Eventos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// Los huecos 1 y 5 del 3.4: <b>cada manejador en su ámbito y en una transacción del contexto de
/// su módulo</b>, con su efecto, su traza, su huella y su respuesta dentro.
/// </summary>
/// <remarks>
/// <para>
/// <b>La misma transacción se comprueba por el <c>xmin</c></b>, la columna de sistema en la que
/// PostgreSQL apunta qué transacción escribió cada fila. Sin puntos de guardado, todas las filas
/// de una transacción llevan el mismo; dos transacciones, dos distintos. Hasta el 3.4, el efecto
/// que el manejador confirmaba entraba en una y la huella en otra, y este caso lo habría dicho.
/// </para>
/// <para>
/// Cada caso trabaja en <b>una base nueva</b>, con Auditoría migrada y la tabla del módulo de prueba
/// creada a mano: las respuestas que dejan en la cola no las ve el publicador de la API, que no las
/// declara, y la tabla no la ve ningún barrido del esquema.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElManejadorVaEnSuTransaccionTests(PostgresConTodosLosModulos postgres)
{
    // Las cuatro filas que deja un manejador que confirma: su documento, la traza de su alta, su
    // huella y el evento de respuesta que registró el documento. Cada una, con su transacción.
    private const string LasCuatroFilas = """
        SELECT 'efecto', xmin::text FROM pruebas_de_la_bandeja.documentos WHERE marca = @marca
        UNION ALL
        SELECT 'traza', registro.xmin::text
        FROM auditoria.registros AS registro
        JOIN pruebas_de_la_bandeja.documentos AS documento ON registro.entidad_id = documento.id::text
        WHERE documento.marca = @marca AND registro.entidad = 'DocumentoDePrueba'
        UNION ALL
        SELECT 'huella', xmin::text FROM auditoria.eventos_procesados
        WHERE evento_id = @evento AND consumidor = @consumidor
        UNION ALL
        SELECT 'respuesta', xmin::text FROM auditoria.bandeja_de_salida
        WHERE nombre = 'pruebas.respuesta-de-prueba' AND cuerpo ->> 'marca' = @marca
        """;

    [Fact]
    public async Task El_efecto_su_traza_su_huella_y_su_respuesta_son_de_la_misma_transaccion()
    {
        string cadena = await BaseConElModuloDePruebaAsync();

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueEscribe, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());

        (await bandeja.DespacharAsync(hecho)).ShouldBe(1);

        List<(string Fila, string Transaccion)> filas =
            await LasCuatroFilasAsync(cadena, hecho, "pruebas.el-que-escribe");

        // Que estén las cuatro, antes de comparar: una comparación entre filas que no están sale
        // verde sin mirar nada.
        filas.Select(fila => fila.Fila).Order(StringComparer.Ordinal)
            .ShouldBe(["efecto", "huella", "respuesta", "traza"]);

        filas.Select(fila => fila.Transaccion).Distinct(StringComparer.Ordinal).Count().ShouldBe(
            1,
            "las cuatro filas tienen que ser de la misma transacción, y son de varias: " +
            string.Join(", ", filas.Select(fila => $"{fila.Fila} {fila.Transaccion}")));
    }

    [Fact]
    public async Task Dos_confirmaciones_de_un_manejador_son_de_la_misma_transaccion()
    {
        // Lo que ve que no haya puntos de guardado. Con un solo `SaveChanges`, uno solo abriría su
        // subtransacción y las cuatro filas seguirían llevando el mismo `xmin`; con dos, la huella
        // sale con el primero y las otras tres con el segundo.
        string cadena = await BaseConElModuloDePruebaAsync();

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueConfirmaDosVeces, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());

        (await bandeja.DespacharAsync(hecho)).ShouldBe(1);

        List<(string Fila, string Transaccion)> filas =
            await LasCuatroFilasAsync(cadena, hecho, "pruebas.el-que-confirma-dos-veces");

        filas.Select(fila => fila.Fila).Order(StringComparer.Ordinal)
            .ShouldBe(["efecto", "huella", "respuesta", "traza"]);

        filas.Select(fila => fila.Transaccion).Distinct(StringComparer.Ordinal).Count().ShouldBe(
            1,
            "la huella, del primer guardado, y el resto, del segundo, tienen que ser de la misma " +
            "transacción, y son de varias: " +
            string.Join(", ", filas.Select(fila => $"{fila.Fila} {fila.Transaccion}")));
    }

    [Fact]
    public async Task Si_el_manejador_falla_despues_de_confirmar_no_queda_nada_de_lo_confirmado()
    {
        // El caso que la transacción cierra. Fuera de ella, el SaveChanges del manejador se habría
        // confirmado solo: el documento y su respuesta se quedarían, sin huella, y el reintento los
        // escribiría otra vez.
        string cadena = await BaseConElModuloDePruebaAsync();

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueEscribeYFalla, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());

        await Should.ThrowAsync<InvalidOperationException>(() => bandeja.DespacharAsync(hecho));

        (await LasCuatroFilasAsync(cadena, hecho, "pruebas.el-que-escribe-y-falla")).ShouldBeEmpty(
            "lo que el manejador confirmó antes de fallar se ha quedado en la base");
    }

    [Fact]
    public async Task Lo_que_el_manejador_no_confirma_no_lo_guarda_nadie_y_su_huella_si()
    {
        // Como detrás de HTTP: un caso de uso que dice que no, no confirma, y lo que dejó en memoria
        // se va con su ámbito. El evento sí está atendido: el manejador terminó sin lanzar.
        string cadena = await BaseConElModuloDePruebaAsync();

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueNoConfirma, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());

        (await bandeja.DespacharAsync(hecho)).ShouldBe(1);
        (await bandeja.DespacharAsync(hecho)).ShouldBe(0, "la huella tiene que estar");

        (await LasCuatroFilasAsync(cadena, hecho, "pruebas.el-que-no-confirma"))
            .Select(fila => fila.Fila)
            .ShouldBe(["huella"], "solo la huella: el documento no lo confirmó nadie");
    }

    [Fact]
    public async Task Lo_que_deja_a_medias_un_manejador_no_lo_guarda_el_siguiente()
    {
        // El hueco 1. El primero confirma un documento y deja otro en su contexto; el segundo
        // confirma el suyo. Con un ámbito para los dos, el SaveChanges del segundo se llevaría el
        // sobrante del primero.
        string cadena = await BaseConElModuloDePruebaAsync();

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios
                .AgregarManejadorDeEvento<ManejadorQueDejaAlgoAMedias, ContextoDePrueba>()
                .AgregarManejadorDeEvento<ManejadorQueEscribe, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());

        (await bandeja.DespacharAsync(hecho)).ShouldBe(2);

        (await MarcasAsync(cadena, hecho.Marca)).ShouldBe(
            [hecho.Marca, $"{hecho.Marca}/a-medias"],
            "el sobrante del primer manejador lo ha guardado el segundo");
    }

    [Fact]
    public async Task Un_manejador_no_recibe_un_evento_al_que_no_atiende()
    {
        // A qué evento atiende lo dice su base, y el despachador filtra por eso. Sin el filtro, o
        // con la base mal leída, este manejador recibiría la respuesta y reventaría al convertirla.
        string cadena = await BaseConElModuloDePruebaAsync();

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueEscribe, ContextoDePrueba>());

        RespuestaDePrueba otro = new(Marca());

        (await bandeja.DespacharAsync(otro)).ShouldBe(0, "atiende a HechoDePrueba, no a RespuestaDePrueba");

        (await MarcasAsync(cadena, otro.Marca)).ShouldBeEmpty();
    }

    [Fact]
    public void Un_manejador_que_no_hereda_de_la_base_no_llega_a_registrarse()
    {
        // La base es la que dice a qué evento atiende, y se lee al registrar. Uno que implementa la
        // interfaz a pelo no lo dice, y tiene que saberse en el arranque, no en el primer evento.
        Should.Throw<InvalidOperationException>(
                () => new ServiceCollection().AgregarManejadorDeEvento<ManejadorSinBase, ContextoDePrueba>())
            .Message.ShouldContain(nameof(ManejadorSinBase));
    }

    // Cada caso se lleva su marca, por lo mismo que en los demás de la bandeja.
    private static string Marca() => Guid.CreateVersion7().ToString();

    private static async Task<List<(string Fila, string Transaccion)>> LasCuatroFilasAsync(
        string cadena,
        HechoDePrueba hecho,
        string consumidor)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand consulta = new(LasCuatroFilas, conexion);
        consulta.Parameters.AddWithValue("marca", hecho.Marca);
        consulta.Parameters.AddWithValue("evento", hecho.EventoId);
        consulta.Parameters.AddWithValue("consumidor", consumidor);

        List<(string Fila, string Transaccion)> filas = [];

        await using NpgsqlDataReader lector = await consulta.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            filas.Add((lector.GetString(0), lector.GetString(1)));
        }

        return filas;
    }

    private static async Task<List<string>> MarcasAsync(string cadena, string marca)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand consulta = new(
            "SELECT marca FROM pruebas_de_la_bandeja.documentos WHERE marca LIKE @marca || '%' ORDER BY marca",
            conexion);
        consulta.Parameters.AddWithValue("marca", marca);

        List<string> marcas = [];

        await using NpgsqlDataReader lector = await consulta.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            marcas.Add(lector.GetString(0));
        }

        return marcas;
    }

    private async Task<string> BaseConElModuloDePruebaAsync()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: true);

        await ContextoDePrueba.CrearTablaAsync(cadena);

        return cadena;
    }

    // Implementa la interfaz sin la base: no dice a qué evento atiende.
    private sealed class ManejadorSinBase : IManejadorDeEvento
    {
        public string Consumidor => "pruebas.el-que-no-hereda";

        public Task ManejarAsync(EventoDeIntegracion evento, CancellationToken cancelacion) =>
            Task.CompletedTask;
    }
}
