using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Terceros.Application.Terceros;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;
using Bastion.Terceros.UnitTests.Dobles;
using Bastion.Terceros.UnitTests.Identificacion;
using Shouldly;

namespace Bastion.Terceros.UnitTests.Terceros;

/// <summary>
/// La mitad de VUELTA del cruce mutuo del ítem 1.10: Terceros preguntándole a Catálogo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que se prueba aquí es la traducción del estado, y no puede probarse en otra capa.</b>
/// <c>EstadoDeLaTarifa</c> es un contrato de Catálogo y el <c>Domain</c> de Terceros no lo ve, a
/// propósito: el agregado recibe un <c>Guid?</c> ya comprobado y no sale a preguntarle nada a
/// nadie. Así que el sitio donde vive «qué se contesta a cada estado» es la capa de aplicación, y
/// el sitio donde se ejerce sin Docker es éste.
/// </para>
/// <para>
/// <b>Y es aritmética de ramas, que es justo lo que el ítem 1.7 enseñó a no dejar solo detrás de
/// Testcontainers</b>: con el motor parado, un <c>switch</c> con una rama mal puesta saldría verde
/// y el aviso llegaría en otra máquina. Estos casos corren siempre, en el carril rápido.
/// </para>
/// </remarks>
public sealed class LaTarifaAsignadaTests
{
    private static readonly DateTimeOffset s_momento =
        new(2026, 9, 13, 7, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly s_hoy = new(2026, 9, 13);

    private static readonly Direccion s_domicilio = Direccion.De(
        calle: "Calle de la Prueba",
        numero: "1",
        codigoPostal: "28001",
        poblacion: "Madrid",
        subdivision: "Madrid",
        pais: "ES");

    /// <summary>
    /// Los tres estados dan tres desenlaces, y dos de ellos son NOES DISTINGUIBLES.
    /// </summary>
    /// <remarks>
    /// <b>Es la diferencia con la mitad de ida del mismo cruce, y es deliberada.</b> Al colgar un
    /// proveedor, los tres estados que no autorizan contestan lo mismo, porque distinguirlos
    /// diría si una ficha de persona existe o está reservada por el art. 32. Aquí lo que hay al
    /// otro lado es una lista de precios de la propia empresa: no hay nada de nadie que reservar,
    /// y quien teclea un identificador que no existe tiene que hacer algo distinto de quien apunta
    /// a un tramo caducado. Dos arreglos, dos <c>type</c>.
    /// </remarks>
    [Theory]
    [InlineData(EstadoDeLaTarifa.NoExiste, TipoDeError.Validacion, "tercero-tarifa-no-encontrada")]
    [InlineData(EstadoDeLaTarifa.SoloResuelveLoViejo, TipoDeError.Conflicto, "tercero-tarifa-no-vigente")]
    public async Task Los_dos_noes_de_la_tarifa_se_distinguen_entre_si(
        EstadoDeLaTarifa estado,
        TipoDeError tipo,
        string codigo)
    {
        var tarifaId = Guid.CreateVersion7();
        var tarifas = new TarifasEn(estado);

        Resultado<TarifaAsignadaDto> resultado = await AsignarAsync(tarifas, tarifaId);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe(codigo);
        resultado.Error.Tipo.ShouldBe(tipo);
    }

    [Fact]
    public async Task El_unico_estado_que_deja_asignar_es_el_que_rige_ese_dia()
    {
        var tarifaId = Guid.CreateVersion7();
        var tarifas = new TarifasEn(EstadoDeLaTarifa.RigeEnEsaFecha);
        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(Alta());
        var confirmaciones = new ConfirmacionesDeTerceros();

        Resultado<TarifaAsignadaDto> resultado =
            await AsignarAsync(tarifas, tarifaId, terceros, confirmaciones);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.TarifaId.ShouldBe(tarifaId);
        terceros.Guardados[0].TarifaAsignadaId.ShouldBe(tarifaId);
        confirmaciones.Veces.ShouldBe(1, "la asignación se guarda una vez y no ninguna");
    }

    /// <summary>El no deja la ficha como estaba, y no la guarda a medias.</summary>
    /// <remarks>
    /// Es la mitad que un <c>switch</c> mal ordenado rompe sin que el código de error cambie: se
    /// traduce el estado <b>antes</b> de tocar el agregado, así que un rechazo no llega a
    /// <c>AsignarTarifa</c> ni a <c>ConfirmarAsync</c>. Si se tradujera después, la respuesta
    /// seguiría siendo un 400 y la fila se habría guardado igual.
    /// </remarks>
    [Fact]
    public async Task Un_estado_que_no_autoriza_no_toca_la_ficha_ni_confirma_nada()
    {
        Tercero ficha = Alta();
        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);
        var confirmaciones = new ConfirmacionesDeTerceros();

        Resultado<TarifaAsignadaDto> resultado = await AsignarAsync(
            new TarifasEn(EstadoDeLaTarifa.SoloResuelveLoViejo),
            Guid.CreateVersion7(),
            terceros,
            confirmaciones);

        resultado.EsCorrecto.ShouldBeFalse();
        ficha.TarifaAsignadaId.ShouldBeNull("la ficha se queda como estaba");
        confirmaciones.Veces.ShouldBe(0, "un rechazo no confirma nada");
    }

    /// <summary>Se pregunta por HOY, y la fecha es la del reloj inyectado.</summary>
    /// <remarks>
    /// <b>Sin este caso, la mitad que decide se queda sin testigo.</b> Una tarifa no está vigente
    /// o no vigente: lo está <i>en un día</i>. Preguntar con <c>DateOnly.MinValue</c>, o con la
    /// fecha del servidor leída de <c>DateTime.UtcNow</c> en vez del <c>TimeProvider</c>,
    /// contestaría lo mismo en todos los casos de arriba y solo se notaría el día que alguien
    /// adelantara el reloj en un test.
    /// </remarks>
    [Fact]
    public async Task La_fecha_con_la_que_se_pregunta_es_la_de_hoy_segun_el_reloj_inyectado()
    {
        var tarifaId = Guid.CreateVersion7();
        var tarifas = new TarifasEn(EstadoDeLaTarifa.RigeEnEsaFecha);

        await AsignarAsync(tarifas, tarifaId);

        tarifas.Preguntadas.ShouldHaveSingleItem();
        tarifas.Preguntadas[0].Tarifa.ShouldBe(tarifaId);
        tarifas.Preguntadas[0].EnLaFecha.ShouldBe(s_hoy);
    }

    /// <summary>Quitar la tarifa no le pregunta nada a Catálogo.</summary>
    /// <remarks>
    /// <b>Y esto no es una optimización: es que no hay nada que preguntar.</b> El cuerpo trae
    /// <c>null</c>, no hay identificador con el que preguntar, y un puerto al que se llamara con
    /// <c>Guid.Empty</c> contestaría <c>NoExiste</c> — con lo que retirar la tarifa de un cliente
    /// sería un 400. Quien tenga una tarifa caducada asignada no podría ni quitársela, que es el
    /// callejón sin salida exacto.
    /// </remarks>
    [Fact]
    public async Task Quitar_la_tarifa_no_pregunta_por_ninguna()
    {
        Tercero ficha = Alta();
        ficha.AsignarTarifa(Guid.CreateVersion7());

        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);
        var tarifas = new TarifasEn(EstadoDeLaTarifa.NoExiste);

        Resultado<TarifaAsignadaDto> resultado =
            await AsignarAsync(tarifas, tarifaId: null, terceros);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.TarifaId.ShouldBeNull();
        ficha.TarifaAsignadaId.ShouldBeNull();
        tarifas.Preguntadas.ShouldBeEmpty(
            "sin identificador no hay nada que preguntar, y preguntar por Guid.Empty haría que " +
            "retirar una tarifa fuera imposible");
    }

    /// <summary>Una ficha que no existe es un 404, y no se pregunta por la tarifa.</summary>
    [Fact]
    public async Task Un_tercero_que_no_existe_es_404_y_no_llega_a_preguntar_por_la_tarifa()
    {
        var tarifas = new TarifasEn(EstadoDeLaTarifa.RigeEnEsaFecha);

        var caso = new AsignarTarifaDto { TarifaId = Guid.CreateVersion7() };

        Resultado<TarifaAsignadaDto> resultado = await Caso(new TercerosEnMemoria(), tarifas)
            .EjecutarAsync(Guid.CreateVersion7(), new VersionDeRecurso(0), caso, default);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Tipo.ShouldBe(TipoDeError.NoEncontrado);
        tarifas.Preguntadas.ShouldBeEmpty();
    }

    /// <summary>La lectura NO pregunta por el estado, y por eso enseña la caducada.</summary>
    /// <remarks>
    /// Lo que se guardó fue una decisión de la empresa, y sigue siendo verdad aunque la tarifa
    /// haya dejado de regir. Si la lectura la escondiera, la pantalla diría «sin tarifa» y quien
    /// la mirara asignaría otra creyendo que nunca hubo ninguna.
    /// </remarks>
    [Fact]
    public async Task La_lectura_devuelve_la_tarifa_aunque_su_vigencia_haya_terminado()
    {
        var tarifaId = Guid.CreateVersion7();
        Tercero ficha = Alta();
        ficha.AsignarTarifa(tarifaId);

        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);

        Resultado<TarifaAsignadaDto> resultado =
            await new ObtenerTarifaAsignada(terceros).EjecutarAsync(ficha.Id, default);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.TerceroId.ShouldBe(ficha.Id);
        resultado.Valor.TarifaId.ShouldBe(tarifaId);
    }

    private static Task<Resultado<TarifaAsignadaDto>> AsignarAsync(
        TarifasEn tarifas,
        Guid? tarifaId,
        TercerosEnMemoria? terceros = null,
        ConfirmacionesDeTerceros? confirmaciones = null)
    {
        TercerosEnMemoria almacen = terceros ?? new TercerosEnMemoria().Con(Alta());
        Guid terceroId = almacen.Guardados[0].Id;

        return Caso(almacen, tarifas, confirmaciones).EjecutarAsync(
            terceroId,
            new VersionDeRecurso(0),
            new AsignarTarifaDto { TarifaId = tarifaId },
            default);
    }

    private static AsignarTarifa Caso(
        TercerosEnMemoria terceros,
        TarifasEn tarifas,
        ConfirmacionesDeTerceros? confirmaciones = null) =>
        new AsignarTarifa(
            terceros,
            tarifas,
            confirmaciones ?? new ConfirmacionesDeTerceros(),
            new VersionesDeTercerosQueDanIgual(),
            new RelojDeTercerosParado(s_momento));

    private static Tercero Alta() =>
        Tercero.Crear(
            Guid.CreateVersion7(),
            IdentificacionFiscal.Espanola(
                Nif.De(IdentificadoresInventados.PersonaJuridica('B', 1_234_567, comoLetra: false)
                    .Valido)),
            "Razón Social",
            null,
            s_domicilio,
            esCliente: true,
            esProveedor: false,
            RegimenFiscal.Comun(),
            s_momento);
}
