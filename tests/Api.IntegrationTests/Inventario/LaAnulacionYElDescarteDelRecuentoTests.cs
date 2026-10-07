using System.Net;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Fechas;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La anulación del recuento confirmado y el descarte del que está en curso, por la API (ADR-0055 §1.6
/// y §9, y ADR-0057).
/// </summary>
/// <remarks>
/// <para>
/// <b>Anular el recuento es anular su ajuste</b>, con el inverso del 2.5: el mismo número en la serie
/// del ajuste, la fecha de hoy y el libro compensado. Y el ajuste de un recuento no se anula por su
/// camino: los dos caminos van en el mismo caso, el público con su <c>409</c> y el del recuento, que
/// pasa.
/// </para>
/// <para>
/// <b>Las dos acciones piden los dos mecanismos</b>, como la confirmación: responden sin <c>ETag</c>, y
/// su <c>412</c> sale antes de escribir. La anulación exige la clave, porque su inverso numera; el
/// descarte la admite y no la exige.
/// </para>
/// <para>
/// <b>Semillas: del 809 al 818</b>, empresas y maestros con el mismo número; el 813 y el 817 son solo
/// empresas, las ajenas. El reparto del bloque del 2.12 está en la cabecera de
/// <c>ElPuertoDelArticuloContraLaBaseTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaAnulacionYElDescarteDelRecuentoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private static readonly TimeSpan s_plazo = TimeSpan.FromSeconds(30);

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (HttpClient cliente in _clientes)
        {
            cliente.Dispose();
        }

        _api.Dispose();
    }

    /// <summary>
    /// Anular un recuento con ajuste anula el ajuste con un inverso de hoy, numerado en la serie del
    /// ajuste, y el físico vuelve al de antes; la respuesta no lleva <c>ETag</c>, y repetirla con la misma
    /// clave devuelve lo mismo sin gastar nada.
    /// </summary>
    [Fact]
    public async Task Anular_con_ajuste_lo_anula_con_un_inverso_de_hoy_en_su_serie_y_el_fisico_vuelve_al_de_antes()
    {
        EscenaDeRecuento escena = await MontarAsync(809, "RCA-A");
        EscenaDeTransferencia de = escena.Escena;

        (RecuentoDto confirmado, Guid ajusteId) = await ConfirmarConDiferenciaAsync(escena, 5m, 7m);

        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);
        string etiqueta = await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id));
        string clave = Guid.NewGuid().ToString();
        string cuerpo;

        using (HttpResponseMessage anulacion = await EscenaDeRecuento.AnularAsync(
            escena.Cliente, confirmado.Id, etiqueta, EscenaDeRecuento.MotivoDeLaAnulacion, clave))
        {
            cuerpo = await anulacion.Content.ReadAsStringAsync();

            anulacion.StatusCode.ShouldBe(HttpStatusCode.OK, cuerpo);
            anulacion.Headers.ETag.ShouldBeNull("la anulación no lleva ETag: la versión nueva se lee con la ficha");
        }

        RecuentoDto anulado = JsonSerializer.Deserialize<RecuentoDto>(cuerpo, JsonSerializerOptions.Web)!;

        anulado.Estado.ShouldBe("Anulado");
        anulado.MotivoDeLaAnulacion.ShouldBe(EscenaDeRecuento.MotivoDeLaAnulacion);
        anulado.Numero.ShouldBe(1, "anulado sigue siendo el documento que se numeró");
        anulado.AjusteId.ShouldBe(ajusteId, "y sigue nombrando el ajuste que movió su diferencia");

        // LA REPETICIÓN, CON LA MISMA CLAVE: lo que se guardó, sin volver a correr nada.
        using (HttpResponseMessage repetida = await EscenaDeRecuento.AnularAsync(
            escena.Cliente, confirmado.Id, etiqueta, EscenaDeRecuento.MotivoDeLaAnulacion, clave))
        {
            string otraVez = await repetida.Content.ReadAsStringAsync();

            repetida.StatusCode.ShouldBe(HttpStatusCode.OK, otraVez);
            otraVez.ShouldBe(cuerpo, "la repetición devuelve la respuesta que se guardó");
            repetida.Headers.ETag.ShouldBeNull("tampoco la repetición");
        }

        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes + 1, "un inverso, un número");
        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(1, "el recuento no numera otra vez");

        // EL TEÓRICO ES EL QUE QUEDÓ AL CONFIRMAR: anular no reescribe lo que se contó.
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(confirmado.Id)).ShouldHaveSingleItem();

        linea.Teorico.ShouldBe(5m);
        linea.Diferencia.ShouldBe(2m);

        Ajuste inverso;

        await using (InventarioDbContext contexto = postgres.AbrirInventario(de.EmpresaId))
        {
            Ajuste original = await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.Id == ajusteId);

            original.Estado.ShouldBe(EstadoDeAjuste.Anulado);

            inverso = await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.AnulaAId == ajusteId);
        }

        inverso.Estado.ShouldBe(EstadoDeAjuste.Confirmado);
        inverso.SerieId.ShouldBe(de.SerieDeAjustes.Id, "el inverso numera en la serie de su original");
        inverso.Numero.ShouldBe(ajustesAntes + 1);
        inverso.FechaDeOperacion.ShouldBe(EscenaDeTransferencia.Hoy, "y lleva la fecha de hoy");
        inverso.Motivo.ShouldBe(EscenaDeRecuento.MotivoDeLaAnulacion);
        inverso.RecuentoId.ShouldBeNull("el recuento tiene un ajuste, el de su diferencia, y no dos");

        IReadOnlyList<MovimientoStock> filas = await de.FilasDelLibroAsync(postgres, inverso.Id);

        filas.Select(fila => fila.CantidadEnUnidadBase).ShouldBe([-2m], "el inverso deshace justo la diferencia");
        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(5m, "el físico vuelve al de antes");
        (await LasExistencias.CuadrarAsync(postgres, de.EmpresaId)).Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// El ajuste de un recuento no se anula por su camino: es un <c>409</c> que no toca nada. Por el del
    /// recuento, sí.
    /// </summary>
    [Fact]
    public async Task El_ajuste_de_un_recuento_no_se_anula_por_su_camino_y_por_el_del_recuento_si()
    {
        EscenaDeRecuento escena = await MontarAsync(810, "RCA-P");
        EscenaDeTransferencia de = escena.Escena;

        (RecuentoDto confirmado, Guid ajusteId) = await ConfirmarConDiferenciaAsync(escena, 5m, 3m);

        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);

        // EL CAMINO PÚBLICO: su recuento seguiría confirmado con la diferencia deshecha. Sin la guarda,
        // el dominio lanzaría y esto sería un 500.
        using (HttpResponseMessage publica = await de.AnularElAjustePorLaApiAsync(ajusteId))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                publica, HttpStatusCode.Conflict, "ajuste-de-un-recuento-no-se-anula", "anular el ajuste de un recuento");
        }

        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes, "el 409 no numera");
        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(3m, "ni mueve nada");
        (await escena.FichaAsync(confirmado.Id)).Estado.ShouldBe("Confirmado");

        await using (InventarioDbContext contexto = postgres.AbrirInventario(de.EmpresaId))
        {
            (await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.Id == ajusteId))
                .Estado.ShouldBe(EstadoDeAjuste.Confirmado);
        }

        // EL CAMINO DEL RECUENTO, que comparte el núcleo con el público y pasa.
        string etiqueta = await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id));

        using HttpResponseMessage delRecuento = await escena.AnularAsync(confirmado.Id, etiqueta);

        (await EscenaDeTransferencia.LeerAsync<RecuentoDto>(delRecuento)).Estado.ShouldBe("Anulado");
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes + 1);
        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(5m);
    }

    /// <summary>
    /// Sin ejercicio para hoy, o con el de hoy cerrado, el recuento que movió el libro no se anula y no
    /// gasta nada, porque su inverso lleva la fecha de hoy; el que no lo movió se anula igual, porque no
    /// escribe nada con esa fecha.
    /// </summary>
    [Fact]
    public async Task Sin_ejercicio_para_hoy_o_con_el_de_hoy_cerrado_solo_se_anula_el_que_no_movio_el_libro()
    {
        EscenaDeRecuento escena = await MontarAsync(811, "RCA-E");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        // EL QUE CUADRA, sin ajuste; y después, en el mismo almacén ya libre, EL QUE SUBE, con ajuste.
        RecuentoDto cuadra = await AbrirYContarAsync(escena, 5m);
        RecuentoDto sinAjuste = await escena.ConfirmarConLaFichaDeAhoraAsync(cuadra.Id);

        sinAjuste.AjusteId.ShouldBeNull();

        RecuentoDto sube = await AbrirYContarAsync(escena, 7m);
        RecuentoDto conAjuste = await escena.ConfirmarConLaFichaDeAhoraAsync(sube.Id);
        Guid ajusteId = conAjuste.AjusteId.ShouldNotBeNull();

        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);
        string deSinAjuste = await escena.Cliente.EtiquetaDeAsync(Cabecera(sinAjuste.Id));
        string deConAjuste = await escena.Cliente.EtiquetaDeAsync(Cabecera(conAjuste.Id));

        // DENTRO DE DOS AÑOS NO HAY EJERCICIO, y solo el reloj del módulo deja anular ese día.
        DateTimeOffset dentroDeDos = new(EscenaDeTransferencia.Hoy.Year + 2, 1, 2, 12, 0, 0, TimeSpan.Zero);

        await using (ElModuloDeInventario modulo = new(postgres, de.EmpresaId, new RelojParado(dentroDeDos)))
        {
            Resultado<RecuentoDto> sinEjercicio =
                await modulo.AnularElRecuentoAsync(conAjuste.Id, deConAjuste, EscenaDeRecuento.MotivoDeLaAnulacion);

            sinEjercicio.EsCorrecto.ShouldBeFalse("el 2 de enero de dentro de dos años no tiene ejercicio");
            sinEjercicio.Error!.Codigo.ShouldBe("recuento-sin-ejercicio");
        }

        using (HttpResponseMessage cierre = await de.CerrarElEjercicioAsync())
        {
            cierre.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cierre));
        }

        using (HttpResponseMessage cerrado = await escena.AnularAsync(conAjuste.Id, deConAjuste))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                cerrado, HttpStatusCode.Conflict, "recuento-en-ejercicio-cerrado", "anular con el ejercicio de hoy cerrado");
        }

        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes, "ningún rechazo numera");
        (await escena.FichaAsync(conAjuste.Id)).Estado.ShouldBe("Confirmado");
        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(7m);

        await using (InventarioDbContext contexto = postgres.AbrirInventario(de.EmpresaId))
        {
            (await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.Id == ajusteId))
                .Estado.ShouldBe(EstadoDeAjuste.Confirmado);
        }

        // EL QUE NO MOVIÓ EL LIBRO SE ANULA, con el ejercicio cerrado: solo cambia de estado.
        using HttpResponseMessage anulacion = await escena.AnularAsync(sinAjuste.Id, deSinAjuste);

        (await EscenaDeTransferencia.LeerAsync<RecuentoDto>(anulacion)).Estado.ShouldBe("Anulado");
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes, "sin ajuste no hay inverso");
    }

    /// <summary>
    /// Anular exige su clave, su versión y un motivo, y otra empresa no encuentra el recuento; nada de
    /// eso escribe ni numera, y con todo en regla anula.
    /// </summary>
    [Fact]
    public async Task Anular_exige_su_clave_su_version_y_un_motivo_y_otra_empresa_no_lo_encuentra()
    {
        EscenaDeRecuento escena = await MontarAsync(812, "RCA-V");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await AbrirYContarAsync(escena, 6m);
        string deAntesDeConfirmar = await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id));
        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);
        string etiqueta = await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id));
        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);
        string motivo = EscenaDeRecuento.MotivoDeLaAnulacion;

        (HttpClient ajena, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(813));
        _clientes.Add(ajena);

        (HttpClient Cliente, string? Etiqueta, string Motivo, string? Clave, HttpStatusCode Estado, string Codigo, string Cual)[] rechazos =
        [
            (escena.Cliente, etiqueta, motivo, null, HttpStatusCode.PreconditionRequired, "idempotencia-obligatoria", "sin Idempotency-Key"),
            (escena.Cliente, null, motivo, NuevaClave(), HttpStatusCode.PreconditionRequired, "falta-if-match", "sin If-Match"),
            (escena.Cliente, deAntesDeConfirmar, motivo, NuevaClave(), HttpStatusCode.PreconditionFailed, "version-obsoleta", "con la versión de antes de confirmar"),
            (escena.Cliente, etiqueta, "   ", NuevaClave(), HttpStatusCode.BadRequest, "recuento-motivo-no-valido", "sin motivo"),
            (escena.Cliente, etiqueta, new string('m', 301), NuevaClave(), HttpStatusCode.BadRequest, "recuento-motivo-no-valido", "con un motivo de 301"),
            (ajena, etiqueta, motivo, NuevaClave(), HttpStatusCode.NotFound, "recuento-no-encontrado", "desde otra empresa"),
        ];

        foreach ((HttpClient cliente, string? deLaCabecera, string elMotivo, string? clave, HttpStatusCode estado, string codigo, string cual) in rechazos)
        {
            using HttpResponseMessage rechazo = await EscenaDeRecuento.AnularAsync(
                cliente, confirmado.Id, deLaCabecera, elMotivo, clave);

            await EscenaDeRecuento.ExigirElProblemaAsync(rechazo, estado, codigo, $"anular {cual}");
        }

        (await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id))).ShouldBe(etiqueta, "ningún rechazo escribe");
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes, "ni numera");

        // CON TODO EN REGLA, anula.
        using HttpResponseMessage anulacion = await escena.AnularAsync(confirmado.Id, etiqueta);

        (await EscenaDeTransferencia.LeerAsync<RecuentoDto>(anulacion)).Estado.ShouldBe("Anulado");
    }

    /// <summary>
    /// Uno en curso no se anula: se descarta. Anular otra vez con la misma versión es un <c>412</c>,
    /// porque la versión va antes que el estado; con la de ahora, el <c>409</c> de uno que no está
    /// confirmado.
    /// </summary>
    [Fact]
    public async Task Uno_en_curso_no_se_anula_y_anular_dos_veces_es_un_412_con_la_misma_version_y_un_409_con_la_de_ahora()
    {
        EscenaDeRecuento escena = await MontarAsync(814, "RCA-2");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await AbrirYContarAsync(escena, 6m);

        using (HttpResponseMessage enCurso = await escena.AnularAsync(
            abierto.Id, await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id))))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                enCurso, HttpStatusCode.Conflict, "recuento-no-esta-confirmado", "anular uno en curso");
        }

        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);
        string etiqueta = await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id));
        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);

        using (HttpResponseMessage primera = await escena.AnularAsync(confirmado.Id, etiqueta))
        {
            (await EscenaDeTransferencia.LeerAsync<RecuentoDto>(primera)).Estado.ShouldBe("Anulado");
        }

        // LA MISMA VERSIÓN CON OTRA CLAVE es otra petición, y cita lo que ya no es: su causa es el 412.
        using (HttpResponseMessage segunda = await escena.AnularAsync(confirmado.Id, etiqueta))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                segunda, HttpStatusCode.PreconditionFailed, "version-obsoleta", "anular otra vez con la versión de antes");
        }

        // CON LA DE AHORA, quien anula ha visto que ya está anulado: es el 409 del estado.
        string deAhora = await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id));

        using (HttpResponseMessage tercera = await escena.AnularAsync(confirmado.Id, deAhora))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                tercera, HttpStatusCode.Conflict, "recuento-no-esta-confirmado", "anular otra vez con la versión de ahora");
        }

        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes + 1, "un inverso y no dos");
    }

    /// <summary>
    /// Descartar deja el recuento sin número, con su motivo y con lo contado, no mueve nada y deja el
    /// almacén libre para otro; sin clave responde sin <c>ETag</c>, y con clave, la repetición devuelve lo
    /// mismo.
    /// </summary>
    [Fact]
    public async Task Descartar_lo_deja_sin_numero_con_su_motivo_y_el_almacen_libre_para_otro()
    {
        EscenaDeRecuento escena = await MontarAsync(815, "RCA-D");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await AbrirYContarAsync(escena, 7m);
        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);
        RecuentoDto descartado;

        using (HttpResponseMessage descarte = await escena.DescartarAsync(
            abierto.Id, await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id))))
        {
            descarte.Headers.ETag.ShouldBeNull("el descarte no lleva ETag, como la confirmación");
            descartado = await EscenaDeTransferencia.LeerAsync<RecuentoDto>(descarte);
        }

        descartado.Estado.ShouldBe("Descartado");
        descartado.Numero.ShouldBeNull("descartar no numera");
        descartado.MotivoDelDescarte.ShouldBe(EscenaDeRecuento.MotivoDelDescarte);
        descartado.AjusteId.ShouldBeNull();
        descartado.HuellaDelTeorico.ShouldBeNull("ya no está en curso: no queda huella que citar");

        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        linea.Contado.ShouldBe(7m, "lo contado se queda en el documento");
        linea.Teorico.ShouldBeNull("uno descartado no tiene teórico: ni el de ahora ni el de una confirmación");

        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(0);
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes);
        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(5m, "y no mueve nada");

        // EL ALMACÉN QUEDA LIBRE: el índice de uno en curso ya no lo ve.
        RecuentoDto otro = await escena.AbrirAsync(de.AlmacenA);
        string etiqueta = await escena.Cliente.EtiquetaDeAsync(Cabecera(otro.Id));
        string clave = Guid.NewGuid().ToString();
        string cuerpo;

        // CON CLAVE, que se admite: la repetición devuelve lo que se guardó.
        using (HttpResponseMessage conClave = await EscenaDeRecuento.DescartarAsync(
            escena.Cliente, otro.Id, etiqueta, EscenaDeRecuento.MotivoDelDescarte, clave))
        {
            cuerpo = await conClave.Content.ReadAsStringAsync();

            conClave.StatusCode.ShouldBe(HttpStatusCode.OK, cuerpo);
        }

        using HttpResponseMessage repetido = await EscenaDeRecuento.DescartarAsync(
            escena.Cliente, otro.Id, etiqueta, EscenaDeRecuento.MotivoDelDescarte, clave);

        repetido.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await repetido.Content.ReadAsStringAsync()).ShouldBe(cuerpo, "la repetición devuelve la respuesta que se guardó");
    }

    /// <summary>
    /// Descartar con la versión de antes de un conteo es un <c>412</c> que no escribe; exige su versión y
    /// un motivo, otra empresa no lo encuentra, y uno confirmado ya no se descarta.
    /// </summary>
    [Fact]
    public async Task Descartar_con_la_version_de_antes_es_un_412_y_uno_confirmado_no_se_descarta()
    {
        EscenaDeRecuento escena = await MontarAsync(816, "RCA-X");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        string deAntesDeContar = await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id));
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        // UN CONTEO QUE LLEGA ANTES cambia lo que se perdería: quien descarta tiene que haberlo visto.
        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 6m);

        string etiqueta = await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id));
        string motivo = EscenaDeRecuento.MotivoDelDescarte;

        (HttpClient ajena, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(817));
        _clientes.Add(ajena);

        (HttpClient Cliente, string? Etiqueta, string Motivo, HttpStatusCode Estado, string Codigo, string Cual)[] rechazos =
        [
            (escena.Cliente, deAntesDeContar, motivo, HttpStatusCode.PreconditionFailed, "version-obsoleta", "con la versión de antes de contar"),
            (escena.Cliente, null, motivo, HttpStatusCode.PreconditionRequired, "falta-if-match", "sin If-Match"),
            (escena.Cliente, etiqueta, string.Empty, HttpStatusCode.BadRequest, "recuento-motivo-no-valido", "sin motivo"),
            (ajena, etiqueta, motivo, HttpStatusCode.NotFound, "recuento-no-encontrado", "desde otra empresa"),
        ];

        foreach ((HttpClient cliente, string? deLaCabecera, string elMotivo, HttpStatusCode estado, string codigo, string cual) in rechazos)
        {
            using HttpResponseMessage rechazo = await EscenaDeRecuento.DescartarAsync(
                cliente, abierto.Id, deLaCabecera, elMotivo, clave: null);

            await EscenaDeRecuento.ExigirElProblemaAsync(rechazo, estado, codigo, $"descartar {cual}");
        }

        (await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id))).ShouldBe(etiqueta, "ningún rechazo escribe");
        (await escena.FichaAsync(abierto.Id)).Estado.ShouldBe("EnCurso");

        // CONFIRMADO, ya no se descarta: lo que movió se anula.
        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);

        using HttpResponseMessage tarde = await escena.DescartarAsync(
            confirmado.Id, await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id)));

        await EscenaDeRecuento.ExigirElProblemaAsync(
            tarde, HttpStatusCode.Conflict, "recuento-no-esta-en-curso", "descartar uno confirmado");
    }

    /// <summary>
    /// Dos anulaciones del mismo recuento a la vez: la segunda espera en la cabecera, lee la versión que
    /// dejó la primera y es el <c>412</c> de la versión; hay un inverso, no dos.
    /// </summary>
    /// <remarks>
    /// <b>La primera ya ha escrito, y se admite</b> con las dos condiciones de la regla de las carreras.
    /// El código distingue la causa: sin el cerrojo de la cabecera, la segunda leería la versión de antes,
    /// esperaría en el contador de la serie y la pararía el índice de <c>anula_a_id</c>, que también es un
    /// <c>412</c>, pero sin la versión de ahora en su <c>detail</c>. Por eso el caso mira el
    /// <c>detail</c>, y la mutación que quita el cerrojo, sola, lo pone rojo.
    /// </remarks>
    [Fact]
    public async Task Dos_anulaciones_a_la_vez_la_segunda_espera_en_la_cabecera_y_es_el_412_de_la_version()
    {
        EscenaDeRecuento escena = await MontarAsync(818, "RCA-C");
        EscenaDeTransferencia de = escena.Escena;

        (RecuentoDto confirmado, Guid ajusteId) = await ConfirmarConDiferenciaAsync(escena, 5m, 7m);

        string etiqueta = await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id));
        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);

        await using ElModuloDeInventario modulo = new(postgres, de.EmpresaId);

        // TRANSACCIÓN 1: anula, con la cabecera bloqueada, el inverso numerado y su libro escrito, y NO
        // suelta.
        (Resultado<RecuentoDto> primera, IDbContextTransaction enVuelo) = await modulo.AnularElRecuentoYQuedarseDentroAsync(
            confirmado.Id, etiqueta, EscenaDeRecuento.MotivoDeLaAnulacion);

        string detalle;

        await using (enVuelo)
        {
            primera.EsCorrecto.ShouldBeTrue($"«{primera.Error?.Codigo}»");

            // TRANSACCIÓN 2: la misma anulación por la API, con la misma versión.
            Task<HttpResponseMessage> segunda = escena.AnularAsync(confirmado.Id, etiqueta);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                modulo.ProcesoDeLaBase,
                segunda,
                "la anulación en vuelo",
                "ha anulado sin esperar al cerrojo de la cabecera");

            await enVuelo.CommitAsync();

            using HttpResponseMessage respuesta = await segunda.WaitAsync(s_plazo);

            await EscenaDeRecuento.ExigirElProblemaAsync(
                respuesta, HttpStatusCode.PreconditionFailed, "version-obsoleta", "la segunda anulación");

            using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

            detalle = problema.RootElement.GetProperty("detail").GetString()!;
        }

        // EL 412 DE LA VERSIÓN, Y NO EL DEL ÍNDICE: nombra la versión que dejó la primera.
        string deAhora = await escena.Cliente.EtiquetaDeAsync(Cabecera(confirmado.Id));

        detalle.ShouldContain(deAhora.Trim('"'), Case.Sensitive, "el 412 tiene que ser el de la versión, con la de ahora");

        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes + 1, "un inverso, un número");

        await using (InventarioDbContext contexto = postgres.AbrirInventario(de.EmpresaId))
        {
            (await contexto.Ajustes.CountAsync(fila => fila.AnulaAId == ajusteId)).ShouldBe(1);
        }

        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(5m, "la diferencia, deshecha una vez");
    }

    private static string Cabecera(Guid recuentoId) => $"{EscenaDeRecuento.Recuentos}/{recuentoId}";

    private static string NuevaClave() => Guid.NewGuid().ToString();

    /// <summary>Abre el recuento del almacén A de la escena y cuenta su única línea.</summary>
    private static async Task<RecuentoDto> AbrirYContarAsync(EscenaDeRecuento escena, decimal contado)
    {
        RecuentoDto abierto = await escena.AbrirAsync(escena.Escena.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, contado);

        return abierto;
    }

    /// <summary>Mete existencias, abre, cuenta otra cifra y confirma, con su ajuste.</summary>
    private async Task<(RecuentoDto Confirmado, Guid AjusteId)> ConfirmarConDiferenciaAsync(
        EscenaDeRecuento escena, decimal teorico, decimal contado)
    {
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, teorico);

        RecuentoDto abierto = await AbrirYContarAsync(escena, contado);
        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);

        return (confirmado, confirmado.AjusteId.ShouldNotBeNull("con diferencias hay ajuste"));
    }

    private async Task<EscenaDeRecuento> MontarAsync(int semilla, string codigo)
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, semilla, codigo);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
