using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Domain.Valoraciones;
using Shouldly;

namespace Bastion.Inventario.UnitTests.Transferencias;

/// <summary>
/// Los cuatro estados de la transferencia y lo que cada uno no deja hacer (R1, ADR-0053 §12):
/// <c>Borrador → Enviada → Recibida</c>, y <c>Enviada</c> o <c>Recibida → Anulada</c>.
/// </summary>
/// <remarks>
/// <b>Cada prohibición va con su pareja</b>: el mismo paso desde el estado que sí lo admite sale
/// bien en otro caso de este fichero. Sin ella, un documento que lo rechazara todo pondría verdes
/// todas las prohibiciones.
/// </remarks>
public sealed class LaMaquinaDeEstadosDeLaTransferenciaTests
{
    private static readonly DateOnly s_diaDePartida = new(2026, 3, 2);

    private static readonly DateOnly s_diaDeEnvio = new(2026, 3, 14);

    private static readonly DateOnly s_diaDeLlegada = new(2026, 3, 16);

    private static readonly DateOnly s_diaDeLaAnulacion = new(2026, 4, 2);

    /// <summary>Se abre en borrador, sin número y sin fecha de llegada.</summary>
    [Fact]
    public void Se_abre_en_borrador_sin_numero_ni_llegada()
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferencia(s_diaDeEnvio);

        transferencia.Estado.ShouldBe(EstadoDeTransferencia.Borrador);
        transferencia.Numero.ShouldBeNull("el número lo da la serie al enviar, no al abrir");
        transferencia.FechaDeRecepcion.ShouldBeNull();
        transferencia.AnulaAId.ShouldBeNull();
        transferencia.Motivo.ShouldBeNull("una transferencia no se justifica: solo su anulación");
        transferencia.AlmacenOrigenId.ShouldBe(ElLibroDeLaPrueba.Origen);
        transferencia.AlmacenDestinoId.ShouldBe(ElLibroDeLaPrueba.Destino);
    }

    /// <summary>El origen y el destino no pueden ser el mismo almacén (ADR-0053 §8).</summary>
    [Fact]
    public void El_origen_y_el_destino_no_son_el_mismo_almacen()
    {
        Should.Throw<ArgumentException>(() => Transferencia.Abrir(
            ElLibroDeLaPrueba.Empresa,
            ElLibroDeLaPrueba.Serie,
            ElLibroDeLaPrueba.Origen,
            ElLibroDeLaPrueba.Origen,
            s_diaDeEnvio,
            ElLibroDeLaPrueba.Divisa,
            ElLibroDeLaPrueba.Momento));
    }

    /// <summary>Una transferencia sin empresa no existe (R8): el filtro global no la vería nunca.</summary>
    [Fact]
    public void Una_transferencia_sin_empresa_no_existe()
    {
        Should.Throw<ArgumentException>(() => Transferencia.Abrir(
            Guid.Empty,
            ElLibroDeLaPrueba.Serie,
            ElLibroDeLaPrueba.Origen,
            ElLibroDeLaPrueba.Destino,
            s_diaDeEnvio,
            ElLibroDeLaPrueba.Divisa,
            ElLibroDeLaPrueba.Momento)).ParamName.ShouldBe("empresaId");
    }

    /// <summary>Una línea lleva cantidad positiva: la negativa es la del inverso.</summary>
    /// <param name="cantidad">Lo que no vale.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Una_linea_lleva_cantidad_positiva(int cantidad)
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferencia(s_diaDeEnvio);

        Should.Throw<ArgumentOutOfRangeException>(() => transferencia.AnadirLinea(
            ElLibroDeLaPrueba.UbicacionDelOrigen,
            ElLibroDeLaPrueba.UbicacionDelDestino,
            ElLibroDeLaPrueba.Articulo,
            cantidad,
            ElLibroDeLaPrueba.Unidad,
            1m,
            ElLibroDeLaPrueba.Momento));

        transferencia.Lineas.ShouldBeEmpty();
    }

    /// <summary>
    /// El factor a unidad base es positivo: con uno negativo, la línea de una entrega sacaría
    /// mercancía del destino y la metería en el origen.
    /// </summary>
    /// <param name="factor">Lo que no vale.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void El_factor_a_unidad_base_es_positivo(int factor)
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferencia(s_diaDeEnvio);

        Should.Throw<ArgumentOutOfRangeException>(() => transferencia.AnadirLinea(
            ElLibroDeLaPrueba.UbicacionDelOrigen,
            ElLibroDeLaPrueba.UbicacionDelDestino,
            ElLibroDeLaPrueba.Articulo,
            1m,
            ElLibroDeLaPrueba.Unidad,
            factor,
            ElLibroDeLaPrueba.Momento)).ParamName.ShouldBe("factorAUnidadBase");

        transferencia.Lineas.ShouldBeEmpty();
    }

    /// <summary>Un número de serie sale una sola vez por documento (ADR-0048 §3).</summary>
    [Fact]
    public void Un_numero_de_serie_va_una_sola_vez_en_el_documento()
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferencia(s_diaDeEnvio);
        transferencia.AnadirLinea(
            ElLibroDeLaPrueba.UbicacionDelOrigen,
            ElLibroDeLaPrueba.UbicacionDelDestino,
            ElLibroDeLaPrueba.Articulo,
            1m,
            ElLibroDeLaPrueba.Unidad,
            1m,
            ElLibroDeLaPrueba.Momento,
            numeroDeSerie: "SN-0001");

        Should.Throw<InvalidOperationException>(() => transferencia.AnadirLinea(
            ElLibroDeLaPrueba.UbicacionDelOrigen,
            ElLibroDeLaPrueba.UbicacionDelDestino,
            ElLibroDeLaPrueba.Articulo,
            1m,
            ElLibroDeLaPrueba.Unidad,
            1m,
            ElLibroDeLaPrueba.Momento,
            numeroDeSerie: "SN-0001"));

        transferencia.Lineas.ShouldHaveSingleItem();
        transferencia.SeriesQueNombra().ShouldHaveSingleItem().Codigo.ShouldBe("SN-0001");
    }

    /// <summary>
    /// El mismo código de serie en otro artículo es otra pieza, y va en su propia línea: la serie es
    /// única por artículo, no en toda la empresa (ADR-0048 §3).
    /// </summary>
    [Fact]
    public void La_misma_serie_de_otro_articulo_es_otra_pieza()
    {
        var otroArticulo = Guid.Parse("0f6a1c1e-0000-4000-8000-0000000000c2");
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferencia(s_diaDeEnvio);
        transferencia.AnadirLinea(
            ElLibroDeLaPrueba.UbicacionDelOrigen,
            ElLibroDeLaPrueba.UbicacionDelDestino,
            ElLibroDeLaPrueba.Articulo,
            1m,
            ElLibroDeLaPrueba.Unidad,
            1m,
            ElLibroDeLaPrueba.Momento,
            numeroDeSerie: "SN-0001");

        transferencia.AnadirLinea(
            ElLibroDeLaPrueba.UbicacionDelOrigen,
            ElLibroDeLaPrueba.UbicacionDelDestino,
            otroArticulo,
            1m,
            ElLibroDeLaPrueba.Unidad,
            1m,
            ElLibroDeLaPrueba.Momento,
            numeroDeSerie: "SN-0001");

        transferencia.Lineas.Count.ShouldBe(2);
        transferencia.SeriesQueNombra().Select(serie => serie.ArticuloId)
            .ShouldBe([ElLibroDeLaPrueba.Articulo, otroArticulo], ignoreOrder: true);
    }

    /// <summary>Enviarla la pasa a enviada y le da número, y deja de admitir líneas.</summary>
    [Fact]
    public void Enviarla_la_numera_y_la_cierra_a_lineas_nuevas()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);

        libro.Enviar(transferencia);

        transferencia.Estado.ShouldBe(EstadoDeTransferencia.Enviada);
        transferencia.Numero.ShouldNotBeNull();
        transferencia.FechaDeRecepcion.ShouldBeNull("mientras vuela, no ha llegado");

        Should.Throw<InvalidOperationException>(() => transferencia.AnadirLinea(
            ElLibroDeLaPrueba.UbicacionDelOrigen,
            ElLibroDeLaPrueba.UbicacionDelDestino,
            ElLibroDeLaPrueba.Articulo,
            1m,
            ElLibroDeLaPrueba.Unidad,
            1m,
            ElLibroDeLaPrueba.Momento));
    }

    /// <summary>Una enviada no se envía otra vez: su salida ya está escrita.</summary>
    [Fact]
    public void Una_enviada_no_se_envia_otra_vez()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(transferencia);
        long? numero = transferencia.Numero;

        Should.Throw<InvalidOperationException>(() => transferencia.Enviar(
            numero!.Value + 1,
            ElLibroDeLaPrueba.ElEnvioDe(transferencia),
            [UnaValoracion(8m)],
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));

        transferencia.Numero.ShouldBe(numero, "un envío rechazado no gasta otro número");
    }

    /// <summary>Recibirla la pasa a recibida con su fecha de llegada.</summary>
    [Fact]
    public void Recibirla_la_pasa_a_recibida_con_su_fecha()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(transferencia);

        libro.Recibir(transferencia, s_diaDeLlegada);

        transferencia.Estado.ShouldBe(EstadoDeTransferencia.Recibida);
        transferencia.FechaDeRecepcion.ShouldBe(s_diaDeLlegada);
        transferencia.FechaDeEnvio.ShouldBe(s_diaDeEnvio, "la llegada no mueve la fecha de la salida");
    }

    /// <summary>La recepción puede ser el mismo día que el envío, pero no antes (ADR-0053 §3).</summary>
    [Fact]
    public void La_recepcion_no_va_antes_que_el_envio()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(transferencia);

        Should.Throw<ArgumentOutOfRangeException>(() => transferencia.Recibir(
            s_diaDeEnvio.AddDays(-1),
            ElLibroDeLaPrueba.LaRecepcionDe(transferencia, s_diaDeEnvio.AddDays(-1)),
            [UnaValoracion(8m)],
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));

        transferencia.Estado.ShouldBe(EstadoDeTransferencia.Enviada);

        libro.Recibir(transferencia, s_diaDeEnvio);

        transferencia.FechaDeRecepcion.ShouldBe(s_diaDeEnvio, "el mismo día sí: es la pareja de la prohibición");
    }

    /// <summary>Un borrador no se recibe: no ha salido nada que pueda llegar.</summary>
    [Fact]
    public void Un_borrador_no_se_recibe()
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);

        Should.Throw<InvalidOperationException>(() => transferencia.Recibir(
            s_diaDeLlegada,
            ElLibroDeLaPrueba.LaRecepcionDe(transferencia, s_diaDeLlegada),
            [UnaValoracion(8m)],
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));

        transferencia.Estado.ShouldBe(EstadoDeTransferencia.Borrador);
    }

    /// <summary>Una recibida no se recibe otra vez: la recepción es entera (ADR-0053 §4).</summary>
    [Fact]
    public void Una_recibida_no_se_recibe_otra_vez()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(transferencia);
        libro.Recibir(transferencia, s_diaDeLlegada);

        Should.Throw<InvalidOperationException>(() => transferencia.Recibir(
            s_diaDeLlegada,
            ElLibroDeLaPrueba.LaRecepcionDe(transferencia, s_diaDeLlegada),
            [UnaValoracion(8m)],
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));
    }

    /// <summary>Un borrador no se anula: se tira.</summary>
    [Fact]
    public void Un_borrador_no_se_anula()
    {
        Transferencia transferencia = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);

        Should.Throw<InvalidOperationException>(() => transferencia.CrearInverso(
            s_diaDeLaAnulacion, "Se mandó al almacén equivocado", ElLibroDeLaPrueba.Momento));
    }

    /// <summary>
    /// Se anula enviada o recibida, y las dos dejan un inverso recibido y el original anulado.
    /// </summary>
    /// <param name="recibirAntes">Si se recibe antes de anularla.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Anularla_deja_el_inverso_recibido_y_el_original_anulado(bool recibirAntes)
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(original);

        if (recibirAntes)
        {
            libro.Recibir(original, s_diaDeLlegada);
        }

        (Transferencia inverso, _) = libro.Anular(original, s_diaDeLaAnulacion);

        original.Estado.ShouldBe(EstadoDeTransferencia.Anulada);
        inverso.Estado.ShouldBe(
            EstadoDeTransferencia.Recibida, "el inverso mueve lo que mueve de una vez, y no deja nada en vuelo");
        inverso.AnulaAId.ShouldBe(original.Id);
        inverso.Numero.ShouldNotBeNull();
        inverso.Numero.ShouldNotBe(original.Numero, "el inverso gasta número propio");
        inverso.SerieId.ShouldBe(original.SerieId, "y lo gasta en la serie del original");
        inverso.FechaDeEnvio.ShouldBe(s_diaDeLaAnulacion);
        inverso.FechaDeRecepcion.ShouldBe(s_diaDeLaAnulacion, "sus dos fechas son la de la anulación");
        inverso.Motivo.ShouldBe("Se mandó al almacén equivocado");
    }

    /// <summary>Una anulada no se anula otra vez, y un inverso no se anula ni se envía.</summary>
    [Fact]
    public void Ni_una_anulada_ni_un_inverso_se_vuelven_a_anular()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(original);
        (Transferencia inverso, _) = libro.Anular(original, s_diaDeLaAnulacion);

        Should.Throw<InvalidOperationException>(() => original.CrearInverso(
            s_diaDeLaAnulacion, "Otra vez", ElLibroDeLaPrueba.Momento));
        Should.Throw<InvalidOperationException>(() => inverso.CrearInverso(
            s_diaDeLaAnulacion, "Deshacer la anulación", ElLibroDeLaPrueba.Momento));
    }

    /// <summary>Un inverso no se envía: se confirma contra su original.</summary>
    [Fact]
    public void Un_inverso_no_se_envia()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(original);
        Transferencia inverso = original.CrearInverso(s_diaDeLaAnulacion, "Por error", ElLibroDeLaPrueba.Momento);

        Should.Throw<InvalidOperationException>(() => inverso.Enviar(
            999,
            ElLibroDeLaPrueba.ElEnvioDe(inverso),
            [UnaValoracion(8m)],
            LotesYSeriesResueltos.Ninguno,
            ElLibroDeLaPrueba.Momento));

        inverso.Estado.ShouldBe(EstadoDeTransferencia.Borrador);
    }

    /// <summary>El inverso no va antes que lo que compensa: de una recibida, antes que su llegada.</summary>
    [Fact]
    public void El_inverso_no_va_antes_que_la_llegada_que_compensa()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(original);
        libro.Recibir(original, s_diaDeLlegada);

        Should.Throw<ArgumentOutOfRangeException>(() => original.CrearInverso(
            s_diaDeLlegada.AddDays(-1), "Por error", ElLibroDeLaPrueba.Momento));

        original.CrearInverso(s_diaDeLlegada, "Por error", ElLibroDeLaPrueba.Momento)
            .FechaDeEnvio.ShouldBe(s_diaDeLlegada, "el mismo día de la llegada sí");
    }

    /// <summary>Una anulación lleva motivo, y de 300 caracteres como mucho.</summary>
    /// <param name="motivo">Lo que no vale.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Una_anulacion_lleva_motivo(string? motivo)
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(original);

        Should.Throw<ArgumentException>(() => original.CrearInverso(
            s_diaDeLaAnulacion, motivo!, ElLibroDeLaPrueba.Momento));
        Should.Throw<ArgumentException>(() => original.CrearInverso(
            s_diaDeLaAnulacion, new string('x', Transferencia.LargoDelMotivo + 1), ElLibroDeLaPrueba.Momento));

        original.CrearInverso(
            s_diaDeLaAnulacion, new string('x', Transferencia.LargoDelMotivo), ElLibroDeLaPrueba.Momento)
            .Motivo.ShouldNotBeNull("trescientos justos sí");
        original.Estado.ShouldBe(EstadoDeTransferencia.Enviada, "construir el inverso no anula nada");
    }

    /// <summary>No se da por anulada contra un inverso que todavía no ha movido el libro.</summary>
    [Fact]
    public void No_se_anula_contra_un_inverso_sin_confirmar()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(original);
        Transferencia inverso = original.CrearInverso(s_diaDeLaAnulacion, "Por error", ElLibroDeLaPrueba.Momento);

        Should.Throw<InvalidOperationException>(() => original.Anular(
            inverso, new TransferenciaAnulada(original.Id, ElLibroDeLaPrueba.Empresa)));

        original.Estado.ShouldBe(EstadoDeTransferencia.Enviada);
    }

    /// <summary>
    /// No se da por anulada contra el inverso de otra: ese inverso ya está confirmado, pero lo que
    /// compensa en el libro es la otra, y esta seguiría con su mercancía en vuelo.
    /// </summary>
    [Fact]
    public void No_se_anula_contra_el_inverso_de_otra()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia esta = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        Transferencia otra = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(esta);
        libro.Enviar(otra);
        (Transferencia inversoDeLaOtra, _) = libro.Anular(otra, s_diaDeLaAnulacion);

        Should.Throw<InvalidOperationException>(() => esta.Anular(
            inversoDeLaOtra, new TransferenciaAnulada(esta.Id, ElLibroDeLaPrueba.Empresa)));

        esta.Estado.ShouldBe(EstadoDeTransferencia.Enviada);
        otra.Estado.ShouldBe(EstadoDeTransferencia.Anulada, "la pareja: con el suyo, la otra sí se anula");
    }

    /// <summary>
    /// Una anulada no se da por anulada otra vez, ni siquiera contra su propio inverso: contaría la
    /// anulación dos veces.
    /// </summary>
    [Fact]
    public void Una_anulada_no_se_da_por_anulada_otra_vez()
    {
        ElLibroDeLaPrueba libro = UnLibroConExistenciasEnElOrigen();
        Transferencia original = ElLibroDeLaPrueba.UnaTransferenciaDe(4m, s_diaDeEnvio);
        libro.Enviar(original);
        (Transferencia inverso, _) = libro.Anular(original, s_diaDeLaAnulacion);
        int eventos = original.EventosPendientes.Count;

        Should.Throw<InvalidOperationException>(() => original.Anular(
            inverso, new TransferenciaAnulada(original.Id, ElLibroDeLaPrueba.Empresa)));

        original.Estado.ShouldBe(EstadoDeTransferencia.Anulada);
        original.EventosPendientes.Count.ShouldBe(eventos, "la anulación se cuenta una sola vez");
    }

    private static ElLibroDeLaPrueba UnLibroConExistenciasEnElOrigen()
    {
        ElLibroDeLaPrueba libro = new();
        libro.Ajustar(ElLibroDeLaPrueba.Origen, ElLibroDeLaPrueba.UbicacionDelOrigen, 10m, 2m, s_diaDePartida);

        return libro;
    }

    private static LineaValorada UnaValoracion(decimal valor) => new(
        Importe.De(valor, ElLibroDeLaPrueba.Divisa), PrecioUnitario.De(2m, ElLibroDeLaPrueba.Divisa));
}
