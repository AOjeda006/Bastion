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
/// La mitad de VUELTA del cruce mutuo del ítem 1.10: Terceros preguntándole a Catálogo, con una
/// tarifa por rol desde el 3.3.
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
/// <para>
/// <b>Casi todos corren dos veces, una por hueco</b>: la de venta, que pide un cliente, y la de
/// compra, que pide un proveedor. El código de los dos es el mismo con un rol distinto, y un
/// caso que solo mirara uno dejaría el otro a merced de una rama cambiada.
/// </para>
/// </remarks>
public sealed class LasTarifasDelTerceroTests
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
    /// Los cuatro estados dan cuatro desenlaces, y los tres noes son DISTINGUIBLES.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es la diferencia con la mitad de ida del mismo cruce, y es deliberada.</b> Al colgar un
    /// proveedor, los estados que no autorizan contestan lo mismo, porque distinguirlos diría si
    /// una ficha de persona existe o está reservada por el art. 32. Aquí lo que hay al otro lado
    /// es una lista de precios de la propia empresa: no hay nada de nadie que reservar, y quien
    /// teclea un identificador que no existe tiene que hacer algo distinto de quien apunta a un
    /// tramo caducado. Tres arreglos, tres <c>type</c>.
    /// </para>
    /// <para>
    /// <b>El del otro tipo es un 409 y no un 400</b>, como el caducado: la tarifa existe y el
    /// identificador está bien escrito; lo que choca es lo que es con el hueco al que se lleva.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(EstadoDeLaTarifa.NoExiste, RolDeTercero.Cliente, TipoDeError.Validacion,
        "tercero-tarifa-no-encontrada")]
    [InlineData(EstadoDeLaTarifa.NoExiste, RolDeTercero.Proveedor, TipoDeError.Validacion,
        "tercero-tarifa-no-encontrada")]
    [InlineData(EstadoDeLaTarifa.SoloResuelveLoViejo, RolDeTercero.Cliente, TipoDeError.Conflicto,
        "tercero-tarifa-no-vigente")]
    [InlineData(EstadoDeLaTarifa.SoloResuelveLoViejo, RolDeTercero.Proveedor, TipoDeError.Conflicto,
        "tercero-tarifa-no-vigente")]
    [InlineData(EstadoDeLaTarifa.EsDeOtroTipo, RolDeTercero.Cliente, TipoDeError.Conflicto,
        "tercero-tarifa-de-otro-tipo")]
    [InlineData(EstadoDeLaTarifa.EsDeOtroTipo, RolDeTercero.Proveedor, TipoDeError.Conflicto,
        "tercero-tarifa-de-otro-tipo")]
    public async Task Los_tres_noes_de_la_tarifa_se_distinguen_entre_si(
        EstadoDeLaTarifa estado,
        RolDeTercero rol,
        TipoDeError tipo,
        string codigo)
    {
        var tarifas = new TarifasEn(estado);

        Resultado<TarifaAsignadaDto> resultado =
            await AsignarAsync(tarifas, rol, Guid.CreateVersion7());

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe(codigo);
        resultado.Error.Tipo.ShouldBe(tipo);
    }

    [Theory]
    [InlineData(RolDeTercero.Cliente)]
    [InlineData(RolDeTercero.Proveedor)]
    public async Task El_unico_estado_que_deja_asignar_es_el_que_rige_ese_dia(RolDeTercero rol)
    {
        var tarifaId = Guid.CreateVersion7();
        var tarifas = new TarifasEn(EstadoDeLaTarifa.RigeEnEsaFecha);
        Tercero ficha = Alta(rol);
        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);
        var confirmaciones = new ConfirmacionesDeTerceros();

        Resultado<TarifaAsignadaDto> resultado =
            await AsignarAsync(tarifas, rol, tarifaId, terceros, confirmaciones);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.TarifaId.ShouldBe(tarifaId);
        Hueco(ficha, rol).ShouldBe(tarifaId);
        confirmaciones.Veces.ShouldBe(1, "la asignación se guarda una vez y no ninguna");
    }

    /// <summary>El no deja la ficha como estaba, y no la guarda a medias.</summary>
    /// <remarks>
    /// Es la mitad que un <c>switch</c> mal ordenado rompe sin que el código de error cambie: se
    /// traduce el estado <b>antes</b> de tocar el agregado, así que un rechazo no llega a
    /// asignar ni a <c>ConfirmarAsync</c>. Si se tradujera después, la respuesta seguiría siendo
    /// un 400 o un 409 y la fila se habría guardado igual.
    /// </remarks>
    [Theory]
    [InlineData(EstadoDeLaTarifa.NoExiste, RolDeTercero.Cliente)]
    [InlineData(EstadoDeLaTarifa.SoloResuelveLoViejo, RolDeTercero.Cliente)]
    [InlineData(EstadoDeLaTarifa.EsDeOtroTipo, RolDeTercero.Cliente)]
    [InlineData(EstadoDeLaTarifa.NoExiste, RolDeTercero.Proveedor)]
    [InlineData(EstadoDeLaTarifa.SoloResuelveLoViejo, RolDeTercero.Proveedor)]
    [InlineData(EstadoDeLaTarifa.EsDeOtroTipo, RolDeTercero.Proveedor)]
    public async Task Un_estado_que_no_autoriza_no_toca_la_ficha_ni_confirma_nada(
        EstadoDeLaTarifa estado,
        RolDeTercero rol)
    {
        Tercero ficha = Alta(rol);
        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);
        var confirmaciones = new ConfirmacionesDeTerceros();

        Resultado<TarifaAsignadaDto> resultado = await AsignarAsync(
            new TarifasEn(estado),
            rol,
            Guid.CreateVersion7(),
            terceros,
            confirmaciones);

        resultado.EsCorrecto.ShouldBeFalse();
        Hueco(ficha, rol).ShouldBeNull("la ficha se queda como estaba");
        confirmaciones.Veces.ShouldBe(0, "un rechazo no confirma nada");
    }

    /// <summary>
    /// Se pregunta por HOY, según el reloj inyectado, y por el tipo del hueco: de venta el del
    /// cliente, de compra el del proveedor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sin este caso, la mitad que decide se queda sin testigo.</b> Una tarifa no está vigente
    /// o no vigente: lo está <i>en un día</i>. Preguntar con <c>DateOnly.MinValue</c>, o con la
    /// fecha del servidor leída de <c>DateTime.UtcNow</c> en vez del <c>TimeProvider</c>,
    /// contestaría lo mismo en todos los casos de arriba y solo se notaría el día que alguien
    /// adelantara el reloj en un test.
    /// </para>
    /// <para>
    /// <b>Y el tipo es la otra mitad (3.3).</b> El doble contesta lo mismo pregunte lo que
    /// pregunte, así que un caso de uso que preguntara siempre por una de venta solo se ve aquí:
    /// en producción, el proveedor recibiría un <c>EsDeOtroTipo</c> con su tarifa de compra bien
    /// puesta, o una de venta como precio de compra si la traducción estuviera cruzada.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(RolDeTercero.Cliente, TipoDeLaTarifa.Venta)]
    [InlineData(RolDeTercero.Proveedor, TipoDeLaTarifa.Compra)]
    public async Task Se_pregunta_por_hoy_segun_el_reloj_y_por_el_tipo_del_hueco(
        RolDeTercero rol,
        TipoDeLaTarifa tipo)
    {
        var tarifaId = Guid.CreateVersion7();
        var tarifas = new TarifasEn(EstadoDeLaTarifa.RigeEnEsaFecha);

        await AsignarAsync(tarifas, rol, tarifaId);

        tarifas.Preguntadas.ShouldHaveSingleItem();
        tarifas.Preguntadas[0].Tarifa.ShouldBe(tarifaId);
        tarifas.Preguntadas[0].Tipo.ShouldBe(tipo);
        tarifas.Preguntadas[0].EnLaFecha.ShouldBe(s_hoy);
    }

    /// <summary>
    /// Un tercero que no hace el rol del hueco es un 409, y no se le pregunta nada a Catálogo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es la invariante del agregado contestada con su <c>type</c>.</b> El dominio lanza si se
    /// le asigna una tarifa de compra a quien no es proveedor; la capa de aplicación lo mira antes,
    /// para que la respuesta sea un 409 con nombre y no un 500. Si dejara de mirarlo, el caso
    /// seguiría rojo, pero por la excepción.
    /// </para>
    /// <para>
    /// <b>Y se mira antes que el puerto</b>: con el rol que falta, la respuesta no depende de cómo
    /// esté la tarifa, y preguntar sería una lectura de otro módulo para nada.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(RolDeTercero.Cliente)]
    [InlineData(RolDeTercero.Proveedor)]
    public async Task Sin_el_rol_del_hueco_es_409_y_no_se_pregunta_ni_se_toca_la_ficha(
        RolDeTercero rol)
    {
        Tercero ficha = Alta(rol == RolDeTercero.Cliente ? RolDeTercero.Proveedor : RolDeTercero.Cliente);
        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);
        var tarifas = new TarifasEn(EstadoDeLaTarifa.RigeEnEsaFecha);
        var confirmaciones = new ConfirmacionesDeTerceros();

        Resultado<TarifaAsignadaDto> resultado =
            await AsignarAsync(tarifas, rol, Guid.CreateVersion7(), terceros, confirmaciones);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe("tercero-tarifa-sin-su-rol");
        resultado.Error.Tipo.ShouldBe(TipoDeError.Conflicto);
        tarifas.Preguntadas.ShouldBeEmpty("sin el rol, la respuesta no depende de la tarifa");
        Hueco(ficha, rol).ShouldBeNull();
        confirmaciones.Veces.ShouldBe(0);
    }

    /// <summary>Los dos huecos no se pisan: asignar o quitar uno deja el otro como estaba.</summary>
    /// <remarks>
    /// Es el motivo del 3.3. Con una sola tarifa, el tercero que es cliente y proveedor recibía su
    /// precio de venta como precio de compra; con dos, una rama que escribiera en el hueco que no
    /// es haría lo mismo con otra forma.
    /// </remarks>
    [Fact]
    public async Task Los_dos_huecos_no_se_pisan()
    {
        var deVenta = Guid.CreateVersion7();
        var deCompra = Guid.CreateVersion7();
        Tercero ficha = AltaConLosDosRoles();
        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);
        var tarifas = new TarifasEn(EstadoDeLaTarifa.RigeEnEsaFecha);

        (await AsignarAsync(tarifas, RolDeTercero.Cliente, deVenta, terceros)).EsCorrecto.ShouldBeTrue();
        (await AsignarAsync(tarifas, RolDeTercero.Proveedor, deCompra, terceros)).EsCorrecto.ShouldBeTrue();

        ficha.TarifaDeVentaId.ShouldBe(deVenta);
        ficha.TarifaDeCompraId.ShouldBe(deCompra);

        (await AsignarAsync(tarifas, RolDeTercero.Cliente, tarifaId: null, terceros)).EsCorrecto.ShouldBeTrue();

        ficha.TarifaDeVentaId.ShouldBeNull();
        ficha.TarifaDeCompraId.ShouldBe(deCompra, "quitar la de venta no toca la de compra");
    }

    /// <summary>
    /// Quitar una tarifa no le pregunta nada a Catálogo, ni pide el rol.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Y esto no es una optimización: es que no hay nada que preguntar.</b> El cuerpo trae
    /// <c>null</c>, no hay identificador con el que preguntar, y un puerto al que se llamara con
    /// <c>Guid.Empty</c> contestaría <c>NoExiste</c> — con lo que retirar la tarifa de un tercero
    /// sería un 400. Quien tenga una tarifa caducada asignada no podría ni quitársela, que es el
    /// callejón sin salida exacto.
    /// </para>
    /// <para>
    /// <b>Y el rol tampoco, por lo mismo.</b> La ficha de este caso tenía el rol al asignarla y lo
    /// ha perdido: un cliente que deja de serlo conserva la suya, y tiene que poder soltarla.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(RolDeTercero.Cliente)]
    [InlineData(RolDeTercero.Proveedor)]
    public async Task Quitar_la_tarifa_no_pregunta_por_ninguna_ni_pide_el_rol(RolDeTercero rol)
    {
        Tercero ficha = AltaConLosDosRoles();
        AsignarEnElDominio(ficha, rol, Guid.CreateVersion7());
        ficha.Modificar(
            "Razón Social",
            nombreComercial: null,
            s_domicilio,
            esCliente: rol != RolDeTercero.Cliente,
            esProveedor: rol != RolDeTercero.Proveedor,
            RegimenFiscal.Comun());

        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);
        var tarifas = new TarifasEn(EstadoDeLaTarifa.NoExiste);

        Resultado<TarifaAsignadaDto> resultado =
            await AsignarAsync(tarifas, rol, tarifaId: null, terceros);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.TarifaId.ShouldBeNull();
        Hueco(ficha, rol).ShouldBeNull();
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
            .EjecutarAsync(
                Guid.CreateVersion7(), RolDeTercero.Cliente, new VersionDeRecurso(0), caso, default);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Tipo.ShouldBe(TipoDeError.NoEncontrado);
        tarifas.Preguntadas.ShouldBeEmpty();
    }

    /// <summary>
    /// La lectura NO pregunta por el estado, y por eso enseña la caducada. Y lee su hueco.
    /// </summary>
    /// <remarks>
    /// Lo que se guardó fue una decisión de la empresa, y sigue siendo verdad aunque la tarifa
    /// haya dejado de regir. Si la lectura la escondiera, la pantalla diría «sin tarifa» y quien
    /// la mirara asignaría otra creyendo que nunca hubo ninguna. La ficha tiene las dos, distintas,
    /// para que leer el hueco que no es salga rojo.
    /// </remarks>
    [Theory]
    [InlineData(RolDeTercero.Cliente)]
    [InlineData(RolDeTercero.Proveedor)]
    public async Task La_lectura_devuelve_la_de_su_hueco_aunque_su_vigencia_haya_terminado(
        RolDeTercero rol)
    {
        var deVenta = Guid.CreateVersion7();
        var deCompra = Guid.CreateVersion7();
        Tercero ficha = AltaConLosDosRoles();
        ficha.AsignarTarifaDeVenta(deVenta);
        ficha.AsignarTarifaDeCompra(deCompra);

        TercerosEnMemoria terceros = new TercerosEnMemoria().Con(ficha);

        Resultado<TarifaAsignadaDto> resultado =
            await new ObtenerTarifaAsignada(terceros).EjecutarAsync(ficha.Id, rol, default);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.TerceroId.ShouldBe(ficha.Id);
        resultado.Valor.TarifaId.ShouldBe(rol == RolDeTercero.Cliente ? deVenta : deCompra);
    }

    private static Task<Resultado<TarifaAsignadaDto>> AsignarAsync(
        TarifasEn tarifas,
        RolDeTercero rol,
        Guid? tarifaId,
        TercerosEnMemoria? terceros = null,
        ConfirmacionesDeTerceros? confirmaciones = null)
    {
        TercerosEnMemoria almacen = terceros ?? new TercerosEnMemoria().Con(Alta(rol));
        Guid terceroId = almacen.Guardados[0].Id;

        return Caso(almacen, tarifas, confirmaciones).EjecutarAsync(
            terceroId,
            rol,
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

    private static Guid? Hueco(Tercero ficha, RolDeTercero rol) =>
        rol == RolDeTercero.Cliente ? ficha.TarifaDeVentaId : ficha.TarifaDeCompraId;

    private static void AsignarEnElDominio(Tercero ficha, RolDeTercero rol, Guid tarifaId)
    {
        if (rol == RolDeTercero.Cliente)
        {
            ficha.AsignarTarifaDeVenta(tarifaId);
        }
        else
        {
            ficha.AsignarTarifaDeCompra(tarifaId);
        }
    }

    private static Tercero Alta(RolDeTercero rol) =>
        Alta(esCliente: rol == RolDeTercero.Cliente, esProveedor: rol == RolDeTercero.Proveedor);

    private static Tercero AltaConLosDosRoles() => Alta(esCliente: true, esProveedor: true);

    private static Tercero Alta(bool esCliente, bool esProveedor) =>
        Tercero.Crear(
            Guid.CreateVersion7(),
            IdentificacionFiscal.Espanola(
                Nif.De(IdentificadoresInventados.PersonaJuridica('B', 1_234_567, comoLetra: false)
                    .Valido)),
            "Razón Social",
            null,
            s_domicilio,
            esCliente,
            esProveedor,
            RegimenFiscal.Comun(),
            s_momento);
}
