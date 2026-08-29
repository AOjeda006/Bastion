using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Eventos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// La tercera cláusula del R12: <b>reprocesar no duplica</b>.
/// </summary>
/// <remarks>
/// <para>
/// La entrega es «al menos una vez» por decisión (ADR-0013): la fila se marca DESPUÉS de que el
/// manejador haya terminado, así que un proceso que se caiga en medio deja el evento pendiente y
/// alguien lo vuelve a entregar. Eso convierte la segunda entrega en algo normal, no en un
/// accidente — y lo que no puede ser normal es que el efecto ocurra dos veces.
/// </para>
/// <para>
/// <b>Se comprueba contra el efecto, no contra la huella.</b> Mirar si está la fila de
/// <c>eventos_procesados</c> sería mirar el reflejo: esa fila la escribe justo el código que se
/// está poniendo a prueba. Lo que dice si la idempotencia funciona es cuántas veces se ejecutó el
/// manejador, y eso lo cuenta el manejador.
/// </para>
/// <para>
/// Y se ejercita el <b>despachador</b>, que es la capa que decide, y no el publicador, que es la
/// que refleja: la segunda entrega no es la primera otra vez, es otro camino de código.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido, con las migraciones aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ReprocesarNoDuplicaTests(PostgresConTodosLosModulos postgres)
{
    [Fact]
    public async Task El_mismo_evento_dos_veces_deja_su_efecto_una_sola()
    {
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueCuenta>());

        HechoDePrueba hecho = new(Marca());
        string efecto = $"{hecho.Marca}/pruebas.el-que-cuenta";

        (await DespacharAsync(bandeja, hecho)).ShouldBe(1, "la primera vez sí lo atiende");
        (await DespacharAsync(bandeja, hecho)).ShouldBe(0, "la segunda ya no");

        Efectos.Veces(efecto).ShouldBe(
            1, "el consumidor ha hecho su trabajo dos veces: reprocesar está duplicando");
    }

    [Fact]
    public async Task Cada_consumidor_tiene_su_turno_aunque_sea_el_mismo_evento()
    {
        // Por qué la clave de la huella es (evento, consumidor) y no solo el evento: con la clave
        // corta, el primer consumidor en terminar dejaría a los demás sin su turno para siempre, y
        // el síntoma sería «este módulo no se entera de nada» meses después.
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: false,
            servicios => servicios
                .AgregarManejadorDeEvento<ManejadorQueCuenta>()
                .AgregarManejadorDeEvento<OtroManejadorQueCuenta>());

        HechoDePrueba hecho = new(Marca());

        (await DespacharAsync(bandeja, hecho)).ShouldBe(2, "son dos consumidores del mismo hecho");
        (await DespacharAsync(bandeja, hecho)).ShouldBe(0);

        Efectos.Veces($"{hecho.Marca}/pruebas.el-que-cuenta").ShouldBe(1);
        Efectos.Veces($"{hecho.Marca}/pruebas.el-otro-que-cuenta").ShouldBe(1);
    }

    [Fact]
    public async Task Dos_hechos_distintos_se_atienden_los_dos()
    {
        // El control negativo de los dos de arriba: una idempotencia escrita al revés —«si ya he
        // atendido algo de este tipo, no vuelvo»— los pasaría los dos y rompería el sistema
        // entero. Lo que no se repite es EL MISMO hecho, no el mismo tipo de hecho.
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: false,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueCuenta>());

        HechoDePrueba uno = new(Marca());
        HechoDePrueba otro = new(Marca());

        (await DespacharAsync(bandeja, uno)).ShouldBe(1);
        (await DespacharAsync(bandeja, otro)).ShouldBe(1);

        Efectos.Veces($"{uno.Marca}/pruebas.el-que-cuenta").ShouldBe(1);
        Efectos.Veces($"{otro.Marca}/pruebas.el-que-cuenta").ShouldBe(1);
    }

    [Fact]
    public async Task Un_hecho_que_no_escucha_nadie_no_es_un_error()
    {
        // En la fase 0 esto es lo normal: `EmpresaCreada` se publica y no lo escucha nadie, porque
        // los módulos que van a reaccionar no existen todavía. Quien decide contar lo que le ha
        // pasado es el emisor; que haya oyentes o no es cosa de otro día.
        await using BandejaDeVerdad bandeja = new(postgres.CadenaDeConexion, publica: false);

        (await DespacharAsync(bandeja, new HechoDePrueba(Marca()))).ShouldBe(0);
    }

    // Cada test se lleva su marca: los manejadores cuentan sus efectos en una tabla estática del
    // proceso, y sin esto un test contaría lo que hizo otro.
    private static string Marca() => Guid.CreateVersion7().ToString();

    // Como lo hace el publicador: un ámbito por vuelta, y dentro el ámbito sin inquilino con su
    // motivo. La cola no es de ninguna empresa en particular, y el despachador escribe en ella.
    private static async Task<int> DespacharAsync(BandejaDeVerdad bandeja, HechoDePrueba hecho)
    {
        using IServiceScope ambito = bandeja.Servicios.CreateScope();

        IInquilinoActual inquilino = ambito.ServiceProvider.GetRequiredService<IInquilinoActual>();

        using IDisposable sinInquilino = inquilino.SinInquilino(MotivoSinInquilino.PublicacionDeEventos);

        return await ambito.ServiceProvider
            .GetRequiredService<IDespachadorDeEventos>()
            .DespacharAsync(hecho, CancellationToken.None);
    }
}
