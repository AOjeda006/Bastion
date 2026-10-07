using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Fechas;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Numeracion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La confirmación del recuento por la API (ADR-0055 §3 y ADR-0057): numera el recuento y, si hay
/// diferencias, su ajuste, y mueve el libro en la misma transacción.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lleva los dos mecanismos a la vez, y los casos miran los dos</b>: la <c>Idempotency-Key</c>,
/// porque gasta dos correlativos, y el <c>If-Match</c> de la cabecera, porque varias personas escriben
/// en el mismo recuento. La respuesta no lleva <c>ETag</c>, y el <c>412</c> sale como un resultado,
/// antes de escribir nada.
/// </para>
/// <para>
/// <b>Un rechazo no gasta ningún número</b>, y se ve en los contadores de las dos series: lo que falla
/// se deshace entero, con los dos correlativos.
/// </para>
/// <para>
/// <b>Semillas: del 793 al 806</b>, empresas y maestros con el mismo número; el 800 es solo una
/// empresa, la ajena. Las carreras van en <c>LasCarrerasDeLaConfirmacionDelRecuentoTests</c>, con el
/// 807 y el 808. El reparto del bloque del 2.12 está en la cabecera de
/// <c>ElPuertoDelArticuloContraLaBaseTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaConfirmacionDelRecuentoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    // Las cuatro formas de una línea precargada frente a su teórico. La añadida es la quinta, y sube
    // siempre desde cero.
    private enum ClaseDeLinea
    {
        Sube,
        Baja,
        Cuadra,
        SeVacia,
    }

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
    /// Con diferencias, la confirmación numera el recuento y su ajuste, el ajuste mueve justo la
    /// diferencia y cada uno apunta al otro; la respuesta no lleva <c>ETag</c>, y repetirla con la misma
    /// clave devuelve lo mismo sin gastar nada.
    /// </summary>
    [Fact]
    public async Task Confirmar_con_diferencias_numera_los_dos_y_el_ajuste_mueve_justo_la_diferencia()
    {
        EscenaDeRecuento escena = await MontarAsync(793, "RCF-D", "PorLote");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 4m, lote: "L-1");
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 6m, lote: "L-2");
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 2m, lote: "L-3");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        Dictionary<string, LineaDeRecuentoDto> antes = await PorLoteAsync(escena, abierto.Id);

        // UNA SUBE, UNA CUADRA Y UNA SE VACÍA: al ajuste van dos.
        await escena.ContarConSuVersionAsync(abierto.Id, antes["L-1"].Id, 7m);
        await escena.ContarConSuVersionAsync(abierto.Id, antes["L-2"].Id, 6m);
        await escena.ContarConSuVersionAsync(abierto.Id, antes["L-3"].Id, 0m);

        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);
        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);
        string clave = Guid.NewGuid().ToString();
        string cuerpo;

        using (HttpResponseMessage confirmacion = await EscenaDeRecuento.ConfirmarAsync(
            escena.Cliente, abierto.Id, etiqueta, ficha.HuellaDelTeorico, clave))
        {
            cuerpo = await confirmacion.Content.ReadAsStringAsync();

            confirmacion.StatusCode.ShouldBe(HttpStatusCode.OK, cuerpo);

            // SIN ETAG (ADR-0057): la repetición devolvería el de entonces, que ya no sería el de ahora.
            confirmacion.Headers.ETag.ShouldBeNull("la confirmación no lleva ETag: la versión nueva se lee con la ficha");
        }

        RecuentoDto confirmado = JsonSerializer.Deserialize<RecuentoDto>(cuerpo, JsonSerializerOptions.Web)!;

        confirmado.Estado.ShouldBe("Confirmado");
        confirmado.Numero.ShouldBe(1);
        confirmado.FechaDeConfirmacion.ShouldBe(EscenaDeTransferencia.Hoy);
        confirmado.HuellaDelTeorico.ShouldBeNull("ya no está en curso: no queda huella que citar");
        confirmado.AjusteId.ShouldNotBeNull("con diferencias hay ajuste");

        // LA REPETICIÓN, CON LA MISMA CLAVE: lo que se guardó, sin volver a correr nada.
        using (HttpResponseMessage repetida = await EscenaDeRecuento.ConfirmarAsync(
            escena.Cliente, abierto.Id, etiqueta, ficha.HuellaDelTeorico, clave))
        {
            string otraVez = await repetida.Content.ReadAsStringAsync();

            repetida.StatusCode.ShouldBe(HttpStatusCode.OK, otraVez);
            otraVez.ShouldBe(cuerpo, "la repetición devuelve la respuesta que se guardó");
            repetida.Headers.ETag.ShouldBeNull("tampoco la repetición: su ETag sería el de entonces");
        }

        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(1, "la repetición no numera otra vez");
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes + 1);

        // CADA LÍNEA, CON EL TEÓRICO QUE QUEDÓ AL CONFIRMAR, y las que difieren, con su línea de ajuste.
        Dictionary<string, LineaDeRecuentoDto> despues = await PorLoteAsync(escena, abierto.Id);

        despues["L-1"].Teorico.ShouldBe(4m);
        despues["L-1"].Diferencia.ShouldBe(3m);
        despues["L-2"].Diferencia.ShouldBe(0m);
        despues["L-2"].LineaDeAjusteId.ShouldBeNull("lo que cuadra no va al ajuste");
        despues["L-3"].Teorico.ShouldBe(2m);
        despues["L-3"].Diferencia.ShouldBe(-2m);

        // LA DOBLE FLECHA: el recuento nombra su ajuste, el ajuste nombra su recuento, y cada línea que
        // difiere, la línea del ajuste que la movió, con justo su diferencia.
        await using (InventarioDbContext contexto = postgres.AbrirInventario(de.EmpresaId))
        {
            Ajuste ajuste = await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.Id == confirmado.AjusteId);

            ajuste.RecuentoId.ShouldBe(abierto.Id);
            ajuste.Estado.ShouldBe(EstadoDeAjuste.Confirmado);
            ajuste.Numero.ShouldBe(ajustesAntes + 1);
            ajuste.SerieId.ShouldBe(de.SerieDeAjustes.Id);
            ajuste.FechaDeOperacion.ShouldBe(EscenaDeTransferencia.Hoy, "la del ajuste es la de la confirmación");
            ajuste.Motivo.ShouldBe(EscenaDeRecuento.Motivo);

            var delAjuste = ajuste.Lineas.ToDictionary(linea => linea.Id);

            delAjuste.Count.ShouldBe(2, "solo las dos que difieren");

            foreach (string lote in (string[])["L-1", "L-3"])
            {
                LineaDeRecuentoDto delRecuento = despues[lote];
                Guid lineaDeAjusteId = delRecuento.LineaDeAjusteId.ShouldNotBeNull(lote);

                delAjuste[lineaDeAjusteId].CantidadIntroducida.ShouldBe(delRecuento.Diferencia!.Value, lote);
                delAjuste[lineaDeAjusteId].CodigoDeLote.ShouldBe(lote);
            }
        }

        IReadOnlyList<MovimientoStock> filas = await de.FilasDelLibroAsync(postgres, confirmado.AjusteId.Value);

        filas.Select(fila => fila.CantidadEnUnidadBase).ShouldBe([-2m, 3m], "el libro mueve justo la diferencia");
        (await LasExistencias.CuadrarAsync(postgres, de.EmpresaId)).Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// Sin diferencias, el recuento se numera y no hay ajuste: la serie del ajuste no numera y el libro
    /// no se mueve.
    /// </summary>
    [Fact]
    public async Task Sin_diferencias_se_numera_el_recuento_y_no_hay_ajuste_ni_se_toca_su_serie()
    {
        EscenaDeRecuento escena = await MontarAsync(794, "RCF-I");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 5m);

        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);
        int filasAntes = await FilasDelLibroAsync(de.EmpresaId);

        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);

        confirmado.Estado.ShouldBe("Confirmado");
        confirmado.Numero.ShouldBe(1);
        confirmado.AjusteId.ShouldBeNull("si todo cuadra no hay ajuste");

        // SU SERIE NI SE CONSULTA (ADR-0055 §13): tomar el número y no usarlo dejaría un hueco.
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes, "sin ajuste, su serie no numera");
        (await FilasDelLibroAsync(de.EmpresaId)).ShouldBe(filasAntes, "y el libro no se mueve");

        LineaDeRecuentoDto contada = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        contada.Teorico.ShouldBe(5m);
        contada.Diferencia.ShouldBe(0m);
        contada.LineaDeAjusteId.ShouldBeNull();
    }

    /// <summary>
    /// Un recuento sin líneas se confirma con su número y sin ajuste; en curso no cuenta para su
    /// ejercicio, y confirmado sí, por su fecha de confirmación.
    /// </summary>
    [Fact]
    public async Task Un_recuento_vacio_se_confirma_con_su_numero_y_cuenta_para_su_ejercicio_por_la_fecha_de_confirmacion()
    {
        EscenaDeRecuento escena = await MontarAsync(795, "RCF-V");
        EscenaDeTransferencia de = escena.Escena;

        // UN ALMACÉN SIN NADA, en una empresa sin ningún otro documento: la precarga no trae líneas.
        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenB);

        abierto.Lineas.ShouldBe(0);

        string ejercicio = $"{LosMaestrosPorLaApi.Ejercicios}/{de.Ejercicio.Id}";

        // EN CURSO NO CUENTA (ADR-0055 §1.5): no tiene fecha de documento, y mover el ejercicio no lo
        // deja fuera de nada. Se devuelve a su sitio para poder confirmar hoy.
        using (HttpResponseMessage movido = await escena.Cliente.ModificarAsync(ejercicio, DesdeManana()))
        {
            movido.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(movido));
        }

        using (HttpResponseMessage devuelto = await escena.Cliente.ModificarAsync(
            ejercicio,
            new ModificarEjercicioDto { FechaDeInicio = de.Ejercicio.FechaDeInicio, FechaDeFin = de.Ejercicio.FechaDeFin }))
        {
            devuelto.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(devuelto));
        }

        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);

        confirmado.Numero.ShouldBe(1);
        confirmado.FechaDeConfirmacion.ShouldBe(EscenaDeTransferencia.Hoy);
        confirmado.AjusteId.ShouldBeNull("sin líneas no hay diferencia que mover");

        // CONFIRMADO SÍ CUENTA, y es el único documento de la empresa: si el periodo no preguntara por
        // los recuentos, el ejercicio se movería y lo dejaría sin periodo al que pertenecer.
        using HttpResponseMessage fuera = await escena.Cliente.ModificarAsync(ejercicio, DesdeManana());

        await EscenaDeRecuento.ExigirElProblemaAsync(
            fuera, HttpStatusCode.Conflict, "ejercicio-dejaria-documentos-fuera", "mover el ejercicio por encima del recuento");
    }

    /// <summary>
    /// Una línea sin contar no es un cero: la confirmación es un <c>422</c> con las que faltan y su
    /// teórico, y no escribe ni numera nada.
    /// </summary>
    [Fact]
    public async Task Una_linea_sin_contar_no_es_un_cero_y_la_confirmacion_es_un_422_con_las_que_faltan()
    {
        EscenaDeRecuento escena = await MontarAsync(796, "RCF-S", "PorLote");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 2m, lote: "L-1");
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m, lote: "L-2");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        Dictionary<string, LineaDeRecuentoDto> porLote = await PorLoteAsync(escena, abierto.Id);

        await escena.ContarConSuVersionAsync(abierto.Id, porLote["L-1"].Id, 2m);

        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);

        ficha.LineasSinContar.ShouldBe(1);

        using (HttpResponseMessage rechazo = await escena.ConfirmarAsync(abierto.Id, etiqueta, ficha.HuellaDelTeorico!))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                rechazo, HttpStatusCode.UnprocessableEntity, "recuento-con-lineas-sin-contar", "confirmar con una línea sin contar");

            LineasEnConflictoDto faltan = await EscenaDeRecuento.LasLineasEnConflictoAsync(rechazo);

            faltan.Total.ShouldBe(1);
            faltan.HuellaDelTeorico.ShouldBeNull("no es un conflicto del teórico");

            LineaDeRecuentoDto laQueFalta = faltan.Lineas.ShouldHaveSingleItem();

            laQueFalta.Id.ShouldBe(porLote["L-2"].Id);
            laQueFalta.Contado.ShouldBeNull();
            laQueFalta.Teorico.ShouldBe(3m, "con su teórico, para contarla sin volver a leer");
        }

        (await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id))).ShouldBe(etiqueta, "el 422 no escribe nada");
        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(0, "ni gasta el número");
    }

    /// <summary>
    /// Si el stock se mueve mientras se cuenta, la confirmación es un <c>409</c> con la huella de ahora
    /// y las líneas cambiadas; con esa huella confirma, y el ajuste mueve la diferencia contra el
    /// teórico de ahora.
    /// </summary>
    [Fact]
    public async Task Si_el_stock_se_mueve_mientras_se_cuenta_es_un_409_con_la_huella_de_ahora_y_con_ella_confirma()
    {
        EscenaDeRecuento escena = await MontarAsync(797, "RCF-T");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 9m);

        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);
        string vista = ficha.HuellaDelTeorico!;

        // ENTRE MEDIAS, ALGUIEN METE DOS EN EL MISMO HUECO. No toca el recuento, así que su versión
        // sigue valiendo: lo que ha cambiado es el almacén, y eso solo lo dice la huella.
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 2m);

        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);
        LineasEnConflictoDto cambiadas;

        using (HttpResponseMessage conflicto = await escena.ConfirmarAsync(abierto.Id, etiqueta, vista))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                conflicto, HttpStatusCode.Conflict, "recuento-teorico-cambiado", "confirmar con la huella de antes");

            cambiadas = await EscenaDeRecuento.LasLineasEnConflictoAsync(conflicto);
        }

        cambiadas.Total.ShouldBe(1);
        cambiadas.HuellaDelTeorico.ShouldNotBeNull().ShouldNotBe(vista);

        LineaDeRecuentoDto cambiada = cambiadas.Lineas.ShouldHaveSingleItem();

        cambiada.Id.ShouldBe(linea.Id);
        cambiada.TeoricoAlContar.ShouldBe(5m);
        cambiada.Teorico.ShouldBe(7m);
        cambiada.TeoricoCambiado.ShouldBeTrue();

        // EL 409 NO ESCRIBE NI NUMERA, y la ficha enseña la misma huella que el problema.
        (RecuentoDto ahora, string etiquetaDeAhora) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);

        etiquetaDeAhora.ShouldBe(etiqueta, "ni el 409 ni el ajuste de en medio tocan la cabecera");
        ahora.HuellaDelTeorico.ShouldBe(cambiadas.HuellaDelTeorico);
        ahora.LineasConElTeoricoCambiado.ShouldBe(1);
        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(0);
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes);

        // CON LA HUELLA DE AHORA SE CONFIRMA, y el ajuste mueve lo que falta hasta lo contado.
        using HttpResponseMessage confirmacion = await escena.ConfirmarAsync(abierto.Id, etiqueta, ahora.HuellaDelTeorico!);

        RecuentoDto confirmado = await EscenaDeTransferencia.LeerAsync<RecuentoDto>(confirmacion);

        (await de.FilasDelLibroAsync(postgres, confirmado.AjusteId!.Value))
            .ShouldHaveSingleItem().CantidadEnUnidadBase.ShouldBe(2m, "de siete a nueve, no de cinco a nueve");
        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(9m);
    }

    /// <summary>
    /// Lo que sube en una clave con tránsito hacia ella es un <c>409</c> con esas líneas; lo que baja se
    /// confirma, con el tránsito en vuelo.
    /// </summary>
    [Fact]
    public async Task Lo_que_sube_con_transito_hacia_su_clave_es_un_409_y_lo_que_baja_se_confirma()
    {
        EscenaDeRecuento escena = await MontarAsync(798, "RCF-X");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);
        await escena.EntrarAsync(postgres, de.AlmacenB, de.UbicacionB, 3m);

        // DOS EN VUELO HACIA EL HUECO DE B: salieron de A y no se han recibido.
        await de.EnviarAsync(postgres, 2m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenB);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        linea.Teorico.ShouldBe(3m);
        linea.EnTransito.ShouldBe(2m);

        // SE CUENTAN CINCO: los tres de B y los dos que ya llegaron sin recibirse.
        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 5m);

        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);

        ficha.LineasConTransito.ShouldBe(1);

        using (HttpResponseMessage conflicto = await escena.ConfirmarAsync(abierto.Id, etiqueta, ficha.HuellaDelTeorico!))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                conflicto, HttpStatusCode.Conflict, "recuento-sube-con-transito", "subir con tránsito hacia la clave");

            LineasEnConflictoDto conTransito = await EscenaDeRecuento.LasLineasEnConflictoAsync(conflicto);

            conTransito.Total.ShouldBe(1);
            conTransito.Lineas.ShouldHaveSingleItem().EnTransito.ShouldBe(2m);
        }

        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(0, "el 409 no gasta el número");

        // LO QUE BAJA SÍ SE CONFIRMA: el tránsito no se suma a lo que falta, solo a lo que sobra.
        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 2m);

        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);

        (await de.FilasDelLibroAsync(postgres, confirmado.AjusteId!.Value))
            .ShouldHaveSingleItem().CantidadEnUnidadBase.ShouldBe(-1m);

        Bastion.Inventario.Domain.Valoraciones.Valoracion deB = await de.LaValoracionDeAsync(postgres, de.AlmacenB);

        deB.Cantidad.ShouldBe(2m);
        deB.EnTransito.ShouldBe(2m, "el tránsito sigue en vuelo, intacto");
    }

    /// <summary>
    /// Confirmar exige su clave, su versión y una huella, y otra empresa no encuentra el recuento; nada
    /// de eso escribe ni numera, y con las tres cosas en regla confirma.
    /// </summary>
    [Fact]
    public async Task Confirmar_exige_su_clave_su_version_y_una_huella_y_otra_empresa_no_lo_encuentra()
    {
        EscenaDeRecuento escena = await MontarAsync(799, "RCF-P");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();
        string deAntesDeContar = await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id));

        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 4m);

        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);
        string huella = ficha.HuellaDelTeorico!;

        etiqueta.ShouldNotBe(deAntesDeContar, "contar toca la versión de la cabecera");

        (HttpClient ajena, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(800));
        _clientes.Add(ajena);

        (HttpClient Cliente, string? Etiqueta, string? Huella, string? Clave, HttpStatusCode Estado, string Codigo, string Cual)[] rechazos =
        [
            (escena.Cliente, etiqueta, huella, null, HttpStatusCode.PreconditionRequired, "idempotencia-obligatoria", "sin Idempotency-Key"),
            (escena.Cliente, null, huella, NuevaClave(), HttpStatusCode.PreconditionRequired, "falta-if-match", "sin If-Match"),
            (escena.Cliente, "*", huella, NuevaClave(), HttpStatusCode.BadRequest, "if-match-no-valido", "con el comodín"),
            (escena.Cliente, deAntesDeContar, huella, NuevaClave(), HttpStatusCode.PreconditionFailed, "version-obsoleta", "con la versión de antes de contar"),
            (escena.Cliente, etiqueta, null, NuevaClave(), HttpStatusCode.BadRequest, "datos-no-validos", "sin huella"),
            (escena.Cliente, etiqueta, huella.ToUpperInvariant(), NuevaClave(), HttpStatusCode.BadRequest, "datos-no-validos", "con la huella en mayúsculas"),
            (ajena, etiqueta, huella, NuevaClave(), HttpStatusCode.NotFound, "recuento-no-encontrado", "desde otra empresa"),
        ];

        foreach ((HttpClient cliente, string? deLaCabecera, string? laHuella, string? clave, HttpStatusCode estado, string codigo, string cual) in rechazos)
        {
            using HttpResponseMessage rechazo = await EscenaDeRecuento.ConfirmarAsync(
                cliente, abierto.Id, deLaCabecera, laHuella, clave);

            await EscenaDeRecuento.ExigirElProblemaAsync(rechazo, estado, codigo, $"confirmar {cual}");
        }

        (await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id))).ShouldBe(etiqueta, "ningún rechazo escribe");
        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(0, "ni numera");

        // CON LAS TRES COSAS EN REGLA, confirma.
        using HttpResponseMessage confirmacion = await escena.ConfirmarAsync(abierto.Id, etiqueta, huella);

        (await EscenaDeTransferencia.LeerAsync<RecuentoDto>(confirmacion)).Numero.ShouldBe(1);
    }

    /// <summary>
    /// Confirmar otra vez con la misma versión es un <c>412</c>, porque la versión va antes que el
    /// estado; con la versión de ahora, es el <c>409</c> de un recuento que ya no está en curso.
    /// </summary>
    [Fact]
    public async Task Confirmar_dos_veces_con_la_misma_version_es_un_412_y_con_la_de_ahora_un_409()
    {
        EscenaDeRecuento escena = await MontarAsync(801, "RCF-2");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 6m);

        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);
        string huella = ficha.HuellaDelTeorico!;
        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);

        using (HttpResponseMessage primera = await escena.ConfirmarAsync(abierto.Id, etiqueta, huella))
        {
            (await EscenaDeTransferencia.LeerAsync<RecuentoDto>(primera)).Numero.ShouldBe(1);
        }

        // LA MISMA VERSIÓN CON OTRA CLAVE es otra petición, y cita lo que ya no es: su causa es el 412.
        using (HttpResponseMessage segunda = await escena.ConfirmarAsync(abierto.Id, etiqueta, huella))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                segunda, HttpStatusCode.PreconditionFailed, "version-obsoleta", "confirmar otra vez con la versión de antes");
        }

        // CON LA DE AHORA, quien confirma ha visto que ya está confirmado: es el 409 del estado.
        string deAhora = await escena.Cliente.EtiquetaDeAsync(Cabecera(abierto.Id));

        using (HttpResponseMessage tercera = await escena.ConfirmarAsync(abierto.Id, deAhora, huella))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                tercera, HttpStatusCode.Conflict, "recuento-no-esta-en-curso", "confirmar otra vez con la versión de ahora");
        }

        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(1, "un recuento, un número");
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes + 1, "y un ajuste");
    }

    /// <summary>
    /// Sin ejercicio para hoy, o con el de hoy cerrado, no se confirma y no se gasta nada; y un recuento
    /// en curso no impide cerrar el ejercicio en el que se abrió.
    /// </summary>
    [Fact]
    public async Task Sin_ejercicio_para_hoy_o_con_el_de_hoy_cerrado_no_se_confirma_y_no_se_gasta_nada()
    {
        EscenaDeRecuento escena = await MontarAsync(802, "RCF-E");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, 7m);

        (RecuentoDto ficha, string etiqueta) = await escena.LoQueVeQuienConfirmaAsync(abierto.Id);
        long ajustesAntes = await de.ContadorAsync(de.SerieDeAjustes.Id);

        // DENTRO DE DOS AÑOS NO HAY EJERCICIO, y solo el reloj del módulo deja confirmar ese día.
        DateTimeOffset dentroDeDos = new(EscenaDeTransferencia.Hoy.Year + 2, 1, 2, 12, 0, 0, TimeSpan.Zero);

        await using (ElModuloDeInventario modulo = new(postgres, de.EmpresaId, new RelojParado(dentroDeDos)))
        {
            Resultado<RecuentoDto> sinEjercicio =
                await modulo.ConfirmarElRecuentoAsync(abierto.Id, etiqueta, ficha.HuellaDelTeorico!);

            sinEjercicio.EsCorrecto.ShouldBeFalse("el 2 de enero de dentro de dos años no tiene ejercicio");
            sinEjercicio.Error!.Codigo.ShouldBe("recuento-sin-ejercicio");
        }

        // EN CURSO NO IMPIDE CERRAR: no es un borrador del ejercicio en el que se abrió (ADR-0055 §1.5).
        using (HttpResponseMessage cierre = await de.CerrarElEjercicioAsync())
        {
            cierre.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(cierre));
        }

        using (HttpResponseMessage cerrado = await escena.ConfirmarAsync(abierto.Id, etiqueta, ficha.HuellaDelTeorico!))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                cerrado, HttpStatusCode.Conflict, "recuento-en-ejercicio-cerrado", "confirmar con el ejercicio de hoy cerrado");
        }

        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(0);
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesAntes);
        (await escena.FichaAsync(abierto.Id)).Estado.ShouldBe("EnCurso");
    }

    /// <summary>
    /// Al cambiar de año, el recuento abierto con las series del año nuevo se confirma en él, con sus dos
    /// números; el abierto con las del viejo no, porque su fecha ya no cae en el ejercicio de su serie.
    /// </summary>
    [Fact]
    public async Task Al_cambiar_de_anio_confirma_el_abierto_con_las_series_del_nuevo_y_no_el_de_las_del_viejo()
    {
        EscenaDeRecuento escena = await MontarAsync(803, "RCF-Y");
        EscenaDeTransferencia de = escena.Escena;
        int anio = EscenaDeTransferencia.Hoy.Year;

        EjercicioDto siguiente = await LosMaestrosPorLaApi.CrearEjercicioAsync(escena.Cliente, anio + 1);
        SerieDto recuentosDelSiguiente = await LosMaestrosPorLaApi.CrearSerieEnAsync(
            escena.Cliente, siguiente.Id, "RCF-Y-RC2", TipoDeDocumento.RecuentoDeInventario);
        SerieDto ajustesDelSiguiente = await LosMaestrosPorLaApi.CrearSerieEnAsync(escena.Cliente, siguiente.Id, "RCF-Y-AJ2");

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);
        await escena.EntrarAsync(postgres, de.AlmacenB, de.UbicacionB, 4m);

        // EL DE A, CON LAS SERIES DE HOY; EL DE B, CON LAS DEL AÑO QUE VIENE, que es lo que se elige
        // el 31 de diciembre para un recuento que se confirmará en enero (ADR-0055 §1.3).
        RecuentoDto conLasDeHoy = await escena.AbrirAsync(de.AlmacenA);
        RecuentoDto conLasDelSiguiente;

        using (HttpResponseMessage alta = await escena.AbrirPorLaApiAsync(
            new AbrirRecuentoDto(recuentosDelSiguiente.Id, ajustesDelSiguiente.Id, de.AlmacenB, EscenaDeRecuento.Motivo)))
        {
            alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
            conLasDelSiguiente = (await alta.Content.ReadFromJsonAsync<RecuentoDto>())!;
        }

        await escena.ContarConSuVersionAsync(
            conLasDeHoy.Id, (await escena.TodasLasLineasAsync(conLasDeHoy.Id)).ShouldHaveSingleItem().Id, 7m);
        await escena.ContarConSuVersionAsync(
            conLasDelSiguiente.Id, (await escena.TodasLasLineasAsync(conLasDelSiguiente.Id)).ShouldHaveSingleItem().Id, 6m);

        (RecuentoDto fichaDeHoy, string etiquetaDeHoy) = await escena.LoQueVeQuienConfirmaAsync(conLasDeHoy.Id);
        (RecuentoDto fichaDelSiguiente, string etiquetaDelSiguiente) =
            await escena.LoQueVeQuienConfirmaAsync(conLasDelSiguiente.Id);
        long ajustesDeHoy = await de.ContadorAsync(de.SerieDeAjustes.Id);

        // EL 2 DE ENERO DEL AÑO QUE VIENE, con el reloj del módulo.
        DateOnly dosDeEnero = new(anio + 1, 1, 2);

        await using ElModuloDeInventario modulo =
            new(postgres, de.EmpresaId, new RelojParado(new DateTimeOffset(anio + 1, 1, 2, 12, 0, 0, TimeSpan.Zero)));

        Resultado<RecuentoDto> fuera = await modulo.ConfirmarElRecuentoAsync(
            conLasDeHoy.Id, etiquetaDeHoy, fichaDeHoy.HuellaDelTeorico!);

        fuera.EsCorrecto.ShouldBeFalse("el 2 de enero no cae en el ejercicio de la serie de este año");
        fuera.Error!.Codigo.ShouldBe(ErroresDeNumeracion.CodigoDeFechaFueraDelEjercicioDeLaSerie);

        Resultado<RecuentoDto> dentro = await modulo.ConfirmarElRecuentoAsync(
            conLasDelSiguiente.Id, etiquetaDelSiguiente, fichaDelSiguiente.HuellaDelTeorico!);

        dentro.EsCorrecto.ShouldBeTrue($"«{dentro.Error?.Codigo}»");
        dentro.Valor.Numero.ShouldBe(1);
        dentro.Valor.FechaDeConfirmacion.ShouldBe(dosDeEnero);

        await using (InventarioDbContext contexto = postgres.AbrirInventario(de.EmpresaId))
        {
            Ajuste ajuste = await contexto.Ajustes.AsNoTracking().SingleAsync(fila => fila.Id == dentro.Valor.AjusteId);

            ajuste.SerieId.ShouldBe(ajustesDelSiguiente.Id);
            ajuste.Numero.ShouldBe(1);
            ajuste.FechaDeOperacion.ShouldBe(dosDeEnero);
        }

        (await de.ContadorAsync(escena.SerieDeRecuentos.Id)).ShouldBe(0, "el de las series de hoy no numeró");
        (await de.ContadorAsync(de.SerieDeAjustes.Id)).ShouldBe(ajustesDeHoy);
        (await de.ContadorAsync(recuentosDelSiguiente.Id)).ShouldBe(1);
        (await de.ContadorAsync(ajustesDelSiguiente.Id)).ShouldBe(1);
        (await escena.FichaAsync(conLasDeHoy.Id)).Estado.ShouldBe("EnCurso");
    }

    /// <summary>
    /// Tras confirmar, el físico de cada clave es lo contado: un recuento nuevo del mismo almacén
    /// precarga justo eso, y nada de lo que se contó a cero.
    /// </summary>
    /// <remarks>
    /// <b>Cada semilla pasa por las cuatro formas de una línea precargada</b> —sube, baja, cuadra y se
    /// vacía— y por una añadida que sube desde cero, y el caso lo afirma: una semilla que no vaciara
    /// ninguna clave pasaría sin haber probado el vaciado.
    /// </remarks>
    /// <param name="semilla">Empresa, maestros y generador.</param>
    [Theory]
    [InlineData(804)]
    [InlineData(805)]
    [InlineData(806)]
    public async Task Tras_confirmar_el_fisico_de_cada_clave_es_lo_contado(int semilla)
    {
        Random azar = new(semilla);
        EscenaDeRecuento escena = await MontarAsync(semilla, $"RCF-{semilla}", "PorLote");
        EscenaDeTransferencia de = escena.Escena;
        UbicacionDto otroHueco =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, de.AlmacenA, $"RCF-{semilla}-A2");
        Dictionary<Guid, string> huecos = new() { [de.UbicacionA] = "A1", [otroHueco.Id] = "A2" };
        Guid[] losDos = [de.UbicacionA, otroHueco.Id];

        List<ClaseDeLinea> clases = [ClaseDeLinea.Sube, ClaseDeLinea.Baja, ClaseDeLinea.Cuadra, ClaseDeLinea.SeVacia];

        for (int mas = azar.Next(3); mas > 0; mas--)
        {
            clases.Add((ClaseDeLinea)azar.Next(4));
        }

        azar.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(clases));

        Dictionary<string, ClaseDeLinea> claseDe = [];
        Dictionary<(Guid Hueco, string Lote), decimal> entrado = [];

        for (int i = 0; i < clases.Count; i++)
        {
            (Guid Hueco, string Lote) clave = (losDos[azar.Next(2)], $"L-{i + 1}");
            decimal cantidad = 2 + azar.Next(8);

            await escena.EntrarAsync(postgres, de.AlmacenA, clave.Hueco, cantidad, lote: clave.Lote);

            entrado[clave] = cantidad;
            claseDe[clave.Lote] = clases[i];
        }

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);

        // UNA CLAVE QUE LA PRECARGA NO TRAÍA, añadida a mano con su coste.
        (Guid Hueco, string Lote) anadida = (losDos[azar.Next(2)], "N-1");

        using (HttpResponseMessage alta = await EscenaDeRecuento.AnadirAsync(
            escena.Cliente, abierto.Id, new(anadida.Hueco, de.ArticuloId, anadida.Lote, null, 3m), NuevaClave()))
        {
            alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
        }

        Dictionary<(Guid Hueco, string Lote), decimal> contado = [];

        foreach (LineaDeRecuentoDto linea in await escena.TodasLasLineasAsync(abierto.Id))
        {
            (Guid Hueco, string Lote) clave = (linea.UbicacionId, linea.CodigoDeLote!);
            decimal cifra = clave == anadida
                ? 1 + azar.Next(5)
                : LoContado(claseDe[clave.Lote], entrado[clave], azar);

            await escena.ContarConSuVersionAsync(abierto.Id, linea.Id, cifra);

            contado[clave] = cifra;
        }

        contado.Count.ShouldBe(entrado.Count + 1, "las precargadas y la añadida");

        RecuentoDto confirmado = await escena.ConfirmarConLaFichaDeAhoraAsync(abierto.Id);

        // LAS CINCO FORMAS, VISTAS EN LO QUE QUEDÓ: el caso afirma que las ha recorrido.
        IReadOnlyList<LineaDeRecuentoDto> quedaron = await escena.TodasLasLineasAsync(abierto.Id);

        quedaron.ShouldContain(linea => linea.Diferencia > 0m && linea.Teorico > 0m, "una precargada que sube");
        quedaron.ShouldContain(linea => linea.Diferencia < 0m && linea.Contado > 0m, "una que baja");
        quedaron.ShouldContain(linea => linea.Diferencia == 0m, "una que cuadra");
        quedaron.ShouldContain(linea => linea.Contado == 0m && linea.Teorico > 0m, "una que se vacía");
        quedaron.ShouldContain(linea => linea.Teorico == 0m && linea.Contado > 0m, "la añadida, que sube desde cero");

        quedaron.Count(linea => linea.LineaDeAjusteId is not null)
            .ShouldBe(quedaron.Count(linea => linea.Diferencia != 0m), "una línea de ajuste por cada diferencia");
        confirmado.AjusteId.ShouldNotBeNull();

        // LA PROPIEDAD: un recuento nuevo del almacén precarga lo contado, y lo contado a cero ya no existe.
        RecuentoDto otro = await escena.AbrirAsync(de.AlmacenA);

        string[] precargado =
        [
            .. (await escena.TodasLasLineasAsync(otro.Id))
                .Select(linea => $"{huecos[linea.UbicacionId]}/{linea.CodigoDeLote}={linea.Teorico:0.######}")
                .Order(StringComparer.Ordinal),
        ];

        string[] esperado =
        [
            .. contado
                .Where(par => par.Value != 0m)
                .Select(par => $"{huecos[par.Key.Hueco]}/{par.Key.Lote}={par.Value:0.######}")
                .Order(StringComparer.Ordinal),
        ];

        precargado.ShouldBe(esperado, $"semilla {semilla}");
        (await de.LaValoracionDeAsync(postgres, de.AlmacenA)).Cantidad.ShouldBe(contado.Values.Sum());
        (await LasExistencias.CuadrarAsync(postgres, de.EmpresaId)).Descuadres.ShouldBeEmpty();
    }

    private static string Cabecera(Guid recuentoId) => $"{EscenaDeRecuento.Recuentos}/{recuentoId}";

    private static string NuevaClave() => Guid.NewGuid().ToString();

    // LO QUE SE CUENTA EN UNA PRECARGADA, según su forma: lo entrado es de dos a nueve, así que siempre
    // cabe una cifra que baje sin vaciar.
    private static decimal LoContado(ClaseDeLinea clase, decimal entrado, Random azar) => clase switch
    {
        ClaseDeLinea.Sube => entrado + 1 + azar.Next(5),
        ClaseDeLinea.Baja => 1 + azar.Next((int)entrado - 1),
        ClaseDeLinea.Cuadra => entrado,
        ClaseDeLinea.SeVacia => 0m,
        _ => throw new ArgumentOutOfRangeException(nameof(clase), clase, "no es una forma de línea"),
    };

    // Las fechas nuevas de los casos que mueven el ejercicio: empieza mañana, y hoy se queda fuera.
    private static ModificarEjercicioDto DesdeManana()
    {
        DateOnly manana = EscenaDeTransferencia.Hoy.AddDays(1);

        return new ModificarEjercicioDto
        {
            FechaDeInicio = manana,
            FechaDeFin = manana.AddMonths(Ejercicio.MesesMaximos).AddDays(-1),
        };
    }

    private static async Task<Dictionary<string, LineaDeRecuentoDto>> PorLoteAsync(EscenaDeRecuento escena, Guid recuentoId) =>
        (await escena.TodasLasLineasAsync(recuentoId)).ToDictionary(linea => linea.CodigoDeLote!);

    private async Task<int> FilasDelLibroAsync(Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await contexto.Movimientos.CountAsync();
    }

    private async Task<EscenaDeRecuento> MontarAsync(int semilla, string codigo, string trazabilidad = "Ninguna")
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, semilla, codigo, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
