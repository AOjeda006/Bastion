using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Shouldly;

namespace Bastion.Api.FunctionalTests.BandejaDeSalida;

/// <summary>
/// Las reglas que la fila de la cola y el catálogo defienden por su cuenta, sin base de datos y
/// sin host.
/// </summary>
/// <remarks>
/// Son las invariantes que el resto del mecanismo da por hechas: que una fila diga siempre de
/// quién es —o por qué no es de nadie—, que un evento que no sale deje de intentarse en algún
/// momento, y que dos módulos no puedan pisarse el nombre de un evento.
/// </remarks>
public sealed class LaColaSeDefiendeSolaTests
{
    [Fact]
    public void Una_fila_lleva_empresa_o_lleva_el_motivo_por_el_que_no_la_lleva()
    {
        // La misma invariante que la restricción de la tabla, aquí arriba: sin ella, la cola se
        // llena de filas sin empresa y sin explicación, que es exactamente lo que la auditoría
        // aprendió a no permitir en el 0.7.
        Should.Throw<InvalidOperationException>(() => Fila(empresaId: null, motivo: null));

        Should.Throw<InvalidOperationException>(
            () => Fila(Guid.CreateVersion7(), MotivoSinInquilino.SemillaDeArranque));

        Should.NotThrow(() => Fila(Guid.CreateVersion7(), motivo: null));
        Should.NotThrow(() => Fila(empresaId: null, MotivoSinInquilino.SemillaDeArranque));
    }

    [Fact]
    public void Una_fila_lleva_la_causa_que_se_le_da_y_no_nace_sin_ella()
    {
        // La causa son tres columnas que llena el interceptor (3.4, hueco 3). Sin ella, la fila
        // nacería sin correlación, que es lo que solo tienen las de antes del 3.4.
        Should.Throw<ArgumentNullException>(() => EventoDeLaBandeja.De(
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            Guid.CreateVersion7(),
            sinInquilino: null,
            "pruebas.uno",
            "{}",
            causa: null!));

        Causa causa = new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");

        EventoDeLaBandeja fila = Fila(Guid.CreateVersion7(), motivo: null, causa);

        fila.UsuarioId.ShouldBe(causa.UsuarioId);
        fila.CorrelacionId.ShouldBe(causa.CorrelacionId);
        fila.ContextoDeTraza.ShouldBe(causa.ContextoDeTraza);
    }

    [Fact]
    public void Un_choque_se_reintenta_tres_veces_y_todas_sus_esperas_caben_en_una_vuelta()
    {
        // Las tres de la puerta del 3.4. Y lo más que se espera entre las tres, la suma de sus
        // topes, es menos que el intervalo del sondeo: un evento que choca no retrasa la vuelta más
        // de lo que tardaría en llegar la siguiente.
        EventoDeLaBandeja.ReintentosPorChoque.ShouldBe(3);

        TimeSpan loMasQueSeEspera = TimeSpan.Zero;

        for (int reintento = 1; reintento <= EventoDeLaBandeja.ReintentosPorChoque; reintento++)
        {
            loMasQueSeEspera += EventoDeLaBandeja.EsperaTrasUnChoque * (1 << (reintento - 1));
        }

        loMasQueSeEspera.ShouldBe(TimeSpan.FromMilliseconds(350));
        loMasQueSeEspera.ShouldBeLessThan(new OpcionesDeLaBandeja().Intervalo);
    }

    [Fact]
    public void Un_evento_que_no_sale_se_aparca_al_quinto_intento_y_no_antes()
    {
        EventoDeLaBandeja fila = Fila(Guid.CreateVersion7(), motivo: null);

        for (int intento = 1; intento < EventoDeLaBandeja.IntentosAntesDeAparcar; intento++)
        {
            fila.AnotarFallo("no ha podido ser").ShouldBeFalse("todavía queda por intentar");
            fila.Estado.ShouldBe(EstadoDelEnvio.Pendiente);
        }

        fila.AnotarFallo("no ha podido ser").ShouldBeTrue();
        fila.Estado.ShouldBe(EstadoDelEnvio.Aparcado);
        fila.Intentos.ShouldBe(EventoDeLaBandeja.IntentosAntesDeAparcar);
    }

    [Fact]
    public void El_error_se_recorta_para_que_una_excepcion_enorme_no_reviente_el_guardado()
    {
        // Un mensaje más largo que la columna haría fallar el guardado que estaba apuntando POR
        // QUÉ había fallado algo. El fallo se comería a su propia explicación.
        EventoDeLaBandeja fila = Fila(Guid.CreateVersion7(), motivo: null);

        fila.AnotarFallo(new string('x', 4000));

        fila.UltimoError!.Length.ShouldBe(1024);
    }

    [Fact]
    public void Publicar_limpia_el_error_del_intento_anterior()
    {
        EventoDeLaBandeja fila = Fila(Guid.CreateVersion7(), motivo: null);
        DateTimeOffset ahora = DateTimeOffset.UtcNow;

        fila.AnotarFallo("la primera vez no");
        fila.DarPorPublicado(ahora);

        fila.Estado.ShouldBe(EstadoDelEnvio.Publicado);
        fila.PublicadoEn.ShouldBe(ahora);
        fila.UltimoError.ShouldBeNull("acabó saliendo: dejar el error puesto haría leer un fallo donde no lo hay");
        fila.Intentos.ShouldBe(1, "lo que costó salir SÍ se conserva; es lo que se mira al buscar un patrón");
    }

    [Fact]
    public void Dos_eventos_no_pueden_llamarse_igual_ni_uno_llamarse_de_dos_maneras()
    {
        Should.Throw<InvalidOperationException>(() => new CatalogoDeEventos(
        [
            new DeclaracionDeEvento("pruebas.uno", typeof(UnHecho)),
            new DeclaracionDeEvento("pruebas.uno", typeof(OtroHecho)),
        ]));

        Should.Throw<InvalidOperationException>(() => new CatalogoDeEventos(
        [
            new DeclaracionDeEvento("pruebas.uno", typeof(UnHecho)),
            new DeclaracionDeEvento("pruebas.otro", typeof(UnHecho)),
        ]));
    }

    [Fact]
    public void Un_evento_sin_declarar_lo_dice_al_volcarlo_y_no_al_leerlo()
    {
        CatalogoDeEventos catalogo = new([new DeclaracionDeEvento("pruebas.uno", typeof(UnHecho))]);

        // Al escribir se lanza: es un error de programación —falta una línea en un `Modulo…`— y
        // dejarlo pasar escribiría en la cola una fila que nadie podrá volver a leer.
        Should.Throw<InvalidOperationException>(() => catalogo.NombreDe(typeof(OtroHecho)));

        // Al leer NO se lanza aquí, se devuelve nulo: quien lee es el publicador, y ahí un nombre
        // desconocido es una fila envenenada que se aparca, no un proceso que se muere.
        catalogo.TipoDe("pruebas.el-que-no-esta").ShouldBeNull();
        catalogo.TipoDe("pruebas.uno").ShouldBe(typeof(UnHecho));
    }

    private static EventoDeLaBandeja Fila(Guid? empresaId, MotivoSinInquilino? motivo, Causa? causa = null) =>
        EventoDeLaBandeja.De(
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            empresaId,
            motivo,
            "pruebas.uno",
            "{}",
            causa ?? new Causa(null, Guid.CreateVersion7(), null));

    private sealed record UnHecho : EventoDeIntegracion;

    private sealed record OtroHecho : EventoDeIntegracion;
}
