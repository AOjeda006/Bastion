using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// El hueco 2 del 3.4: <b>el manejador corre con la empresa de su fila</b>, y no dentro del ámbito
/// sin inquilino de la vuelta del publicador.
/// </summary>
/// <remarks>
/// <para>
/// Hasta el 3.4, un manejador heredaba el ámbito sin inquilino de la vuelta: leía las filas de
/// todas las empresas, escribía sin ninguna y la guarda de la auditoría no lo miraba. Ahora la
/// empresa activa en su ámbito es la de la fila, que es la empresa desde la que se actuó (ADR-0013
/// §8): lo que lee lo filtra esa empresa, lo que escribe la lleva, y escribir en otra lo para la
/// guarda, como en una petición.
/// </para>
/// <para>
/// <b>La empresa de cada caso es nueva</b>, y no la de prueba de la bandeja: un caso que pasara con
/// la de siempre no distinguiría «la de su fila» de «la de los tests».
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElManejadorVaConLaEmpresaDeSuFilaTests(PostgresConTodosLosModulos postgres)
{
    // Las tres filas que deja un manejador que confirma su documento, cada una con su empresa y el
    // motivo por el que no la lleva: el documento, la traza de su alta y su evento de respuesta.
    private const string LasEmpresasDeSusFilas = """
        SELECT 'efecto', empresa_id, NULL::text FROM pruebas_de_la_bandeja.documentos WHERE marca = @marca
        UNION ALL
        SELECT 'traza', registro.empresa_id, registro.sin_inquilino::text
        FROM auditoria.registros AS registro
        JOIN pruebas_de_la_bandeja.documentos AS documento ON registro.entidad_id = documento.id::text
        WHERE documento.marca = @marca AND registro.entidad = 'DocumentoDePrueba'
        UNION ALL
        SELECT 'respuesta', empresa_id, sin_inquilino::text FROM auditoria.bandeja_de_salida
        WHERE nombre = 'pruebas.respuesta-de-prueba' AND cuerpo ->> 'marca' = @marca
        """;

    [Fact]
    public async Task El_efecto_su_traza_y_su_respuesta_llevan_la_empresa_de_la_fila()
    {
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueEscribe, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());
        var empresa = Guid.CreateVersion7();

        (await bandeja.DespacharAsync(hecho, empresa)).ShouldBe(1);

        List<(string Fila, Guid? Empresa, string? Motivo)> filas = await LasEmpresasAsync(cadena, hecho.Marca);

        // Que estén las tres, antes de mirar su empresa: sobre una lista vacía, la comparación de
        // abajo sale verde sin mirar nada.
        filas.Select(fila => fila.Fila).Order(StringComparer.Ordinal).ShouldBe(["efecto", "respuesta", "traza"]);

        filas.Where(fila => fila.Empresa != empresa || fila.Motivo is not null).ShouldBeEmpty(
            $"las tres tenían que llevar la empresa {empresa} de su fila, y sin motivo: " +
            string.Join(", ", filas.Select(fila => $"{fila.Fila} {fila.Empresa} {fila.Motivo}")));
    }

    [Fact]
    public async Task El_manejador_solo_ve_lo_de_la_empresa_de_su_fila()
    {
        // Dos documentos del caso, uno en la empresa de la fila y otro en otra, puestos a pelo para
        // que el filtro no intervenga al escribirlos. Bajo el ámbito sin inquilino de la vuelta, el
        // manejador vería los dos.
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);
        string marca = Marca();
        var empresa = Guid.CreateVersion7();

        await ContextoDePrueba.SembrarAsync(cadena, empresa, $"{marca}/de-la-suya");
        await ContextoDePrueba.SembrarAsync(cadena, Guid.CreateVersion7(), $"{marca}/de-otra");

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueMiraLoQueVe, ContextoDePrueba>());

        (await bandeja.DespacharAsync(new HechoDePrueba(marca), empresa)).ShouldBe(1);

        // La pareja: los dos están en la base. Sin ella, «no vio el de otra» también lo diría una
        // siembra que no hubiera escrito nada.
        (await ContextoDePrueba.MarcasAsync(cadena, marca)).ShouldBe([$"{marca}/de-la-suya", $"{marca}/de-otra"]);

        ManejadorQueMiraLoQueVe.LoQueVio(marca).ShouldBe(
            [$"{marca}/de-la-suya"],
            "el manejador tenía que ver solo el documento de la empresa de su fila");
    }

    [Fact]
    public async Task Escribir_en_otra_empresa_lo_para_la_guarda_de_la_auditoria()
    {
        // Bajo el ámbito sin inquilino, la guarda no tiene contra qué comparar y no mira: el
        // documento nacería en otra empresa sin dar error.
        string cadena = await ContextoDePrueba.BaseNuevaAsync(postgres);

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueEscribeEnOtraEmpresa, ContextoDePrueba>());

        HechoDePrueba hecho = new(Marca());

        await Should.ThrowAsync<EscrituraEnOtraEmpresaException>(
            () => bandeja.DespacharAsync(hecho, Guid.CreateVersion7()));

        (await ContextoDePrueba.MarcasAsync(cadena, hecho.Marca)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Una_fila_sin_empresa_no_se_entrega_y_dice_su_motivo()
    {
        // La fila que deja la semilla, escrita bajo un ámbito sin inquilino. Entregada sin empresa,
        // su manejador vería las de todas.
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueCuenta, ContextoDeLaBandeja>());

        HechoDePrueba hecho = new(Marca());
        string clave = $"{hecho.Marca}/pruebas.el-que-cuenta";

        (await Should.ThrowAsync<InvalidOperationException>(
                () => bandeja.DespacharAsync(hecho, sinInquilino: MotivoSinInquilino.SemillaDeArranque)))
            .Message.ShouldContain(nameof(MotivoSinInquilino.SemillaDeArranque));

        Efectos.Veces(clave).ShouldBe(0, "el manejador no tenía que recibirlo");

        // La pareja: el mismo hecho, con empresa, sí le llega. El cero de arriba no es de un
        // manejador que no atiende.
        (await bandeja.DespacharAsync(hecho, Guid.CreateVersion7())).ShouldBe(1);

        Efectos.Veces(clave).ShouldBe(1);
    }

    [Fact]
    public async Task El_publicador_aparca_a_la_quinta_una_fila_sin_empresa_con_su_motivo()
    {
        // En una base nueva: la cola es solo la de este caso.
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: true);

        await using BandejaDeVerdad bandeja = new(
            cadena,
            publica: true,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueCuenta, ContextoDeLaBandeja>());

        HechoDePrueba hecho = new(Marca());

        await bandeja.EncolarAsync(hecho, sinInquilino: MotivoSinInquilino.SemillaDeArranque);
        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await bandeja.EnLaColaAsync(hecho.EventoId) is { Estado: EstadoDelEnvio.Aparcado }))
            .ShouldBeTrue("una fila sin empresa con manejadores tiene que acabar aparcada");

        EventoDeLaBandeja fila = (await bandeja.EnLaColaAsync(hecho.EventoId)).ShouldNotBeNull();

        fila.Intentos.ShouldBe(EventoDeLaBandeja.IntentosAntesDeAparcar);
        fila.UltimoError.ShouldNotBeNull().ShouldContain(nameof(MotivoSinInquilino.SemillaDeArranque));
        Efectos.Veces($"{hecho.Marca}/pruebas.el-que-cuenta").ShouldBe(0);
    }

    // Cada caso se lleva su marca, por lo mismo que en los demás de la bandeja.
    private static string Marca() => Guid.CreateVersion7().ToString();

    private static async Task<List<(string Fila, Guid? Empresa, string? Motivo)>> LasEmpresasAsync(
        string cadena,
        string marca)
    {
        await using NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();

        await using NpgsqlCommand consulta = new(LasEmpresasDeSusFilas, conexion);
        consulta.Parameters.AddWithValue("marca", marca);

        List<(string Fila, Guid? Empresa, string? Motivo)> filas = [];

        await using NpgsqlDataReader lector = await consulta.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            filas.Add((
                lector.GetString(0),
                await lector.IsDBNullAsync(1) ? null : lector.GetGuid(1),
                await lector.IsDBNullAsync(2) ? null : lector.GetString(2)));
        }

        return filas;
    }
}
