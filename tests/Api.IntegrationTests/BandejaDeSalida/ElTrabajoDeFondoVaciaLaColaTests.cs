using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.IntegrationTests.BandejaDeSalida;

/// <summary>
/// La segunda cláusula del R12 —<b>el trabajo de fondo lo publica</b>— y lo que pasa cuando algo
/// va mal mientras lo hace.
/// </summary>
/// <remarks>
/// <para>
/// El publicador es el único de los tres pedazos del R12 que no tiene una petición detrás: corre
/// solo, cada dos segundos, sin usuario y sin empresa. De ahí salen sus tres formas de fallar en
/// silencio, y hay un test para cada una: entregar y perder el evento si algo revienta después,
/// morirse entero por culpa de un evento envenenado, y quedarse dando vueltas eternamente sobre
/// una fila que no va a salir nunca.
/// </para>
/// <para>
/// Todo esto corre con el cableado de producción (<see cref="BandejaDeVerdad"/>): lo único que se
/// cambia es el intervalo del sondeo y qué manejadores hay puestos.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor compartido, con las migraciones aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElTrabajoDeFondoVaciaLaColaTests(PostgresConTodosLosModulos postgres)
{
    [Fact]
    public async Task Lo_que_esta_pendiente_acaba_publicado()
    {
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: true,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueCuenta>());

        HechoDePrueba hecho = new(Marca());
        await bandeja.EncolarAsync(hecho);

        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await bandeja.EnLaColaAsync(hecho.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("el trabajo de fondo no ha publicado un evento que estaba pendiente");

        Efectos.Veces($"{hecho.Marca}/pruebas.el-que-cuenta").ShouldBe(1);

        EventoDeLaBandeja fila = (await bandeja.EnLaColaAsync(hecho.EventoId)).ShouldNotBeNull();

        fila.PublicadoEn.ShouldNotBeNull();
        fila.Intentos.ShouldBe(0);
        fila.UltimoError.ShouldBeNull();
    }

    [Fact]
    public async Task Un_manejador_que_falla_la_primera_vez_acaba_recibiendo_el_evento()
    {
        // ESTE ES EL QUE DISTINGUE «al menos una vez» de «como mucho una vez». Si la fila se
        // marcara como publicada ANTES de despachar, el fallo del manejador se tragaría el evento:
        // la cola quedaría limpia, nadie volvería a intentarlo y el efecto no ocurriría jamás. El
        // síntoma en producción es el peor de todos, porque no hay ningún error que mirar.
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: true,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueFallaLaPrimeraVez>());

        HechoDePrueba hecho = new(Marca());
        string efecto = $"{hecho.Marca}/pruebas.el-que-falla-una-vez";

        await bandeja.EncolarAsync(hecho);
        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(() => Task.FromResult(Efectos.Veces(efecto) > 0)))
            .ShouldBeTrue("el evento se ha perdido: el manejador falló una vez y no volvió a verlo");

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await bandeja.EnLaColaAsync(hecho.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("después de entregarlo bien, la fila tiene que quedar publicada");

        EventoDeLaBandeja fila = (await bandeja.EnLaColaAsync(hecho.EventoId)).ShouldNotBeNull();

        fila.Intentos.ShouldBe(1, "falló una vez, y eso queda escrito en la fila");
        fila.UltimoError.ShouldBeNull("al publicarse bien se limpia el error del intento anterior");

        Efectos.Veces(efecto).ShouldBe(1, "reintentar no es duplicar");
    }

    [Fact]
    public async Task El_fallo_de_uno_no_es_el_fallo_de_la_vuelta()
    {
        // Una fila con un nombre que no declara nadie: es la que no puede salir de la cola por
        // mucho que se reintente. Si el publicador tratara el lote como una unidad, esta fila
        // bloquearía a todas las que van detrás y el sistema entero dejaría de enterarse de nada.
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: true,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueCuenta>());

        HechoDePrueba envenenado = new(Marca());
        HechoDePrueba bueno = new(Marca());

        await bandeja.EncolarAsync(envenenado, "pruebas.hecho-que-no-declara-nadie");
        await bandeja.EncolarAsync(bueno);

        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await bandeja.EnLaColaAsync(bueno.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("el evento bueno iba DETRÁS del envenenado y no ha salido");

        Efectos.Veces($"{bueno.Marca}/pruebas.el-que-cuenta").ShouldBe(1);

        // Y el envenenado deja de intentarse en algún momento, en vez de dar vueltas para siempre.
        (await BandejaDeVerdad.EsperarAsync(async () =>
            await bandeja.EnLaColaAsync(envenenado.EventoId) is { Estado: EstadoDelEnvio.Aparcado }))
            .ShouldBeTrue("un evento que no puede salir se aparca; reintentarlo eternamente es ruido");

        EventoDeLaBandeja fila = (await bandeja.EnLaColaAsync(envenenado.EventoId)).ShouldNotBeNull();

        fila.Intentos.ShouldBe(EventoDeLaBandeja.IntentosAntesDeAparcar);
        fila.UltimoError.ShouldNotBeNullOrWhiteSpace("aparcar sin decir por qué no ayuda a nadie");
        fila.PublicadoEn.ShouldBeNull();

        // Y no se aparca en silencio: queda dicho al nivel que se mira, y una sola vez.
        bandeja.Registro.Veces(8305).ShouldBe(1);

        // LO QUE DE VERDAD PRUEBA QUE EL TRABAJO DE FONDO SIGUE VIVO: uno nuevo, después del
        // desastre, sale igual. Un publicador muerto en silencio a las tres de la mañana pasa los
        // dos primeros asertos de este test y falla este.
        HechoDePrueba despues = new(Marca());
        await bandeja.EncolarAsync(despues);

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await bandeja.EnLaColaAsync(despues.EventoId) is { Estado: EstadoDelEnvio.Publicado }))
            .ShouldBeTrue("el publicador se ha muerto por el camino");
    }

    [Fact]
    public async Task Un_manejador_que_no_funciona_nunca_acaba_aparcando_su_evento()
    {
        // El otro sabor del envenenamiento: el evento se entiende perfectamente, y lo que no
        // funciona es quien tiene que atenderlo. También se aparca, y también con su motivo.
        await using BandejaDeVerdad bandeja = new(
            postgres.CadenaDeConexion,
            publica: true,
            servicios => servicios.AgregarManejadorDeEvento<ManejadorQueSiempreFalla>());

        HechoDePrueba hecho = new(Marca());

        await bandeja.EncolarAsync(hecho);
        await bandeja.ArrancarAsync();

        (await BandejaDeVerdad.EsperarAsync(async () =>
            await bandeja.EnLaColaAsync(hecho.EventoId) is { Estado: EstadoDelEnvio.Aparcado }))
            .ShouldBeTrue("un consumidor roto tiene que acabar aparcando el evento");

        EventoDeLaBandeja fila = (await bandeja.EnLaColaAsync(hecho.EventoId)).ShouldNotBeNull();

        fila.Intentos.ShouldBe(EventoDeLaBandeja.IntentosAntesDeAparcar);
        fila.UltimoError.ShouldNotBeNull().ShouldContain("InvalidOperationException");
    }

    private static string Marca() => Guid.CreateVersion7().ToString();
}
