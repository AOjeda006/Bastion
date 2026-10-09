using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Series;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El alta del recuento y su lectura (ítem 2.12): las claves del almacén precargadas y sin contar,
/// la ficha con su versión y la huella del teórico, la página de líneas y la lista.
/// </summary>
/// <remarks>
/// <para>
/// <b>La precarga son las claves con físico</b> (ADR-0055 §5): ni las de otro almacén, ni las que se
/// vaciaron, que siguen siendo una fila de existencias a cero. Cada línea lleva su lote o su número
/// de serie por el código, que es lo que se lee en la etiqueta, y la unidad base de su artículo, que
/// se pregunta a Catálogo (§1.2).
/// </para>
/// <para>
/// <b>El teórico es el físico de ahora</b> (§2), y no el tránsito: lo que vuela hacia una clave se
/// enseña aparte y no mueve la huella. Una salida sí la mueve, y la versión de la cabecera no, porque
/// el recuento no se ha escrito.
/// </para>
/// <para>
/// <b>Semillas: del 764 al 772 y el 777</b>, las empresas y, con el mismo número, la unidad y el
/// tramo de impuesto de cada artículo; el 771 es solo una empresa, la ajena. El reparto del bloque
/// del 2.12 está en la cabecera de <c>ElPuertoDelArticuloContraLaBaseTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElAltaDelRecuentoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
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
    /// El alta precarga las claves del almacén con físico, ordenadas por lote, en la unidad base de su
    /// artículo y sin contar; ni las de otro almacén ni la que se vació.
    /// </summary>
    [Fact]
    public async Task Abrir_precarga_las_claves_con_fisico_del_almacen_en_su_unidad_base_y_sin_contar()
    {
        EscenaDeRecuento escena = await MontarAsync(764, "RCA-A", "PorLote");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m, lote: "L-2");
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m, lote: "L-1");
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 2m, lote: "L-3");
        await escena.SalirAsync(postgres, de.AlmacenA, de.UbicacionA, 2m, lote: "L-3");
        await escena.EntrarAsync(postgres, de.AlmacenB, de.UbicacionB, 4m, lote: "L-1");

        using HttpResponseMessage alta = await escena.AbrirPorLaApiAsync(escena.Peticion(de.AlmacenA));

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
        RecuentoDto abierto = (await alta.Content.ReadFromJsonAsync<RecuentoDto>())!;

        alta.Headers.Location.ShouldNotBeNull().ToString()
            .ShouldEndWith($"{EscenaDeRecuento.Recuentos}/{abierto.Id}", Case.Insensitive);

        abierto.Estado.ShouldBe("EnCurso");
        abierto.Numero.ShouldBeNull("se numera al confirmar (ADR-0055 §1.4)");
        abierto.FechaDeConfirmacion.ShouldBeNull();
        abierto.FechaDeApertura.ShouldBe(EscenaDeTransferencia.Hoy);
        abierto.AlmacenId.ShouldBe(de.AlmacenA);
        abierto.SerieId.ShouldBe(escena.SerieDeRecuentos.Id);
        abierto.SerieDelAjusteId.ShouldBe(de.SerieDeAjustes.Id);
        abierto.Motivo.ShouldBe(EscenaDeRecuento.Motivo);
        abierto.AjusteId.ShouldBeNull();
        abierto.MotivoDelDescarte.ShouldBeNull();
        abierto.MotivoDeLaAnulacion.ShouldBeNull();

        abierto.Lineas.ShouldBe(2, "L-1 y L-2 de A: ni el L-1 de B ni el L-3, que se vació");
        abierto.LineasSinContar.ShouldBe(2);
        abierto.LineasConElTeoricoCambiado.ShouldBe(0);
        abierto.LineasConTransito.ShouldBe(0);
        abierto.HuellaDelTeorico.ShouldNotBeNullOrWhiteSpace();

        IReadOnlyList<LineaDeRecuentoDto> lineas = await escena.TodasLasLineasAsync(abierto.Id);

        lineas.Select(linea => (linea.Numero, linea.CodigoDeLote, linea.Teorico))
            .ShouldBe([(1, "L-1", 3m), (2, "L-2", 5m)]);

        foreach (LineaDeRecuentoDto linea in lineas)
        {
            linea.UbicacionId.ShouldBe(de.UbicacionA);
            linea.ArticuloId.ShouldBe(de.ArticuloId);
            linea.UnidadBaseId.ShouldBe(de.UnidadId, "la unidad base, preguntada a Catálogo (ADR-0055 §1.2)");
            linea.NumeroDeSerie.ShouldBeNull();
            linea.Origen.ShouldBe("Precargada");
            linea.CosteUnitario.ShouldBeNull("el coste solo lo lleva una clave añadida (ADR-0055 §6)");
            linea.Contado.ShouldBeNull("una línea precargada no está contada, y no es un cero (ADR-0055 §5)");
            linea.TeoricoAlContar.ShouldBeNull();
            linea.Diferencia.ShouldBeNull();
            linea.EnTransito.ShouldBe(0m);
            linea.TeoricoCambiado.ShouldBeFalse();
            linea.LineaDeAjusteId.ShouldBeNull();
        }

        RecuentoDto ficha = await escena.FichaAsync(abierto.Id);

        ficha.ShouldBe(abierto, "la ficha dice lo mismo que el alta, huella incluida");

        // UNA CLAVE QUE SE VACÍA DESPUÉS SIGUE EN EL RECUENTO, con teórico cero: su fila de
        // existencias ya no viene, y una línea sin fila no desaparece de la huella (ADR-0055 §2).
        await escena.SalirAsync(postgres, de.AlmacenA, de.UbicacionA, 5m, lote: "L-2");

        (await escena.TodasLasLineasAsync(abierto.Id)).Select(linea => (linea.CodigoDeLote, linea.Teorico))
            .ShouldBe([("L-1", 3m), ("L-2", 0m)]);
        (await escena.FichaAsync(abierto.Id)).HuellaDelTeorico.ShouldNotBe(abierto.HuellaDelTeorico);
    }

    /// <summary>
    /// Un artículo con número de serie se precarga una línea por unidad, con su número, y el teórico
    /// de cada una es uno.
    /// </summary>
    [Fact]
    public async Task Abrir_con_numeros_de_serie_precarga_una_linea_por_unidad()
    {
        EscenaDeRecuento escena = await MontarAsync(765, "RCA-S", "PorNumeroSerie");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 1m, serie: "SN-2");
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 1m, serie: "SN-1");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);

        IReadOnlyList<LineaDeRecuentoDto> lineas = await escena.TodasLasLineasAsync(abierto.Id);

        lineas.Select(linea => (linea.Numero, linea.NumeroDeSerie, linea.CodigoDeLote, linea.Teorico))
            .ShouldBe([(1, "SN-1", (string?)null, 1m), (2, "SN-2", (string?)null, 1m)]);

        (await escena.LineasAsync(abierto.Id, "?q=sn-1")).Elementos.ShouldHaveSingleItem().NumeroDeSerie
            .ShouldBe("SN-1", "el texto busca también en el número de serie, sin distinguir mayúsculas");
    }

    /// <summary>
    /// Una clave que solo tiene tránsito hacia ella no se precarga: lo que vuela no está en el
    /// almacén, y su teórico sería cero.
    /// </summary>
    [Fact]
    public async Task Una_clave_que_solo_tiene_transito_no_se_precarga()
    {
        EscenaDeRecuento escena = await MontarAsync(777, "RCA-T");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);
        _ = await de.EnviarAsync(postgres, 2m);

        RecuentoDto deB = await escena.AbrirAsync(de.AlmacenB);

        deB.Lineas.ShouldBe(0, "el hueco de B solo tiene las dos unidades que vuelan hacia él (ADR-0055 §5)");
        deB.LineasConTransito.ShouldBe(0);
        (await escena.TodasLasLineasAsync(deB.Id)).ShouldBeEmpty();
    }

    /// <summary>
    /// La ficha lleva su <c>ETag</c>, el tránsito hacia una clave se cuenta aparte sin mover la huella,
    /// y una salida mueve el teórico y la huella pero no la versión. Con el físico a cero y algo en
    /// vuelo, la clave sigue enseñando su tránsito.
    /// </summary>
    [Fact]
    public async Task La_ficha_lleva_su_version_y_el_teorico_de_ahora_sin_el_transito()
    {
        EscenaDeRecuento escena = await MontarAsync(766, "RCA-F");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 7m);
        await escena.EntrarAsync(postgres, de.AlmacenB, de.UbicacionB, 4m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenB);
        string recurso = $"{EscenaDeRecuento.Recuentos}/{abierto.Id}";
        string version = await escena.Cliente.EtiquetaDeAsync(recurso);

        _ = await de.EnviarAsync(postgres, 2m);

        RecuentoDto conTransito = await escena.FichaAsync(abierto.Id);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        conTransito.LineasConTransito.ShouldBe(1, "dos unidades vuelan de A hacia el hueco de B");
        linea.EnTransito.ShouldBe(2m);
        linea.Teorico.ShouldBe(4m, "el teórico es el físico: lo que vuela no está en el almacén (ADR-0055 §2)");
        conTransito.HuellaDelTeorico.ShouldBe(abierto.HuellaDelTeorico, "el tránsito no mueve la huella");

        await escena.SalirAsync(postgres, de.AlmacenB, de.UbicacionB, 4m);

        RecuentoDto trasLaSalida = await escena.FichaAsync(abierto.Id);
        LineaDeRecuentoDto vacia = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        vacia.Teorico.ShouldBe(0m);
        vacia.EnTransito.ShouldBe(2m, "la fila de un hueco vacío con algo en vuelo hacia él sigue viniendo");
        trasLaSalida.LineasConTransito.ShouldBe(1);
        trasLaSalida.HuellaDelTeorico.ShouldNotBe(abierto.HuellaDelTeorico, "una salida mueve el teórico");
        trasLaSalida.LineasConElTeoricoCambiado.ShouldBe(
            0, "una línea sin contar no tiene teórico de cuando se contó con el que compararlo");

        (await escena.Cliente.EtiquetaDeAsync(recurso)).ShouldBe(
            version, "el recuento no se ha escrito: lo que ha cambiado es el mundo, que lo dice la huella");
    }

    /// <summary>
    /// La página de líneas se corta, se ordena y se acota en el servidor, y rechaza lo que no admite.
    /// </summary>
    [Fact]
    public async Task Las_lineas_se_paginan_se_ordenan_y_se_acotan_en_el_servidor()
    {
        EscenaDeRecuento escena = await MontarAsync(767, "RCA-L", "PorLote");
        EscenaDeTransferencia de = escena.Escena;

        foreach (int lote in Enumerable.Range(1, 5))
        {
            await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, lote, lote: $"LT-{lote}");
        }

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);

        PaginaDe<LineaDeRecuentoDto> primera = await escena.LineasAsync(abierto.Id, "?size=2");

        primera.Total.ShouldBe(5);
        primera.Pagina.ShouldBe(1);
        primera.Tamanio.ShouldBe(2);
        primera.Elementos.Select(linea => linea.Numero).ShouldBe([1, 2]);

        (await escena.LineasAsync(abierto.Id, "?size=2&page=3")).Elementos
            .Select(linea => linea.Numero).ShouldBe([5]);

        (await escena.LineasAsync(abierto.Id, "?size=2&sort=-numero")).Elementos
            .Select(linea => linea.Numero).ShouldBe([5, 4]);

        (await escena.LineasAsync(abierto.Id, "?solo=sin-contar")).Total.ShouldBe(5);
        (await escena.LineasAsync(abierto.Id, "?solo=teorico-cambiado")).Total.ShouldBe(0);

        PaginaDe<LineaDeRecuentoDto> buscada = await escena.LineasAsync(abierto.Id, "?q=lt-3");

        buscada.Total.ShouldBe(1, "el texto busca en el lote, sin distinguir mayúsculas");
        buscada.Elementos.ShouldHaveSingleItem().CodigoDeLote.ShouldBe("LT-3");
        buscada.Elementos.ShouldHaveSingleItem().Teorico.ShouldBe(3m);

        await NoSeAdmiteAsync(escena, $"{abierto.Id}/lineas?solo=contadas", HttpStatusCode.BadRequest, null);
        await NoSeAdmiteAsync(
            escena, $"{abierto.Id}/lineas?sort=lote", HttpStatusCode.BadRequest, "/errors/orden-no-admitido");
        await NoSeAdmiteAsync(
            escena, $"{Guid.NewGuid()}/lineas", HttpStatusCode.NotFound, "/errors/recuento-no-encontrado");
    }

    /// <summary>
    /// Una línea se lee sola con su propia versión, que es la que pedirá contarla, y una línea que no
    /// es de ese recuento no existe.
    /// </summary>
    [Fact]
    public async Task Una_linea_se_lee_con_su_propia_version()
    {
        EscenaDeRecuento escena = await MontarAsync(768, "RCA-U");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);
        await escena.EntrarAsync(postgres, de.AlmacenB, de.UbicacionB, 2m);

        RecuentoDto deA = await escena.AbrirAsync(de.AlmacenA);
        RecuentoDto deB = await escena.AbrirAsync(de.AlmacenB);

        LineaDeRecuentoDto enLaPagina = (await escena.TodasLasLineasAsync(deA.Id)).ShouldHaveSingleItem();
        LineaDeRecuentoDto deLaOtra = (await escena.TodasLasLineasAsync(deB.Id)).ShouldHaveSingleItem();
        string recurso = $"{EscenaDeRecuento.Recuentos}/{deA.Id}/lineas/{enLaPagina.Id}";

        string cabecera = $"{EscenaDeRecuento.Recuentos}/{deA.Id}";
        string versionDeLaLinea = await escena.Cliente.EtiquetaDeAsync(recurso);
        string versionDeLaCabecera = await escena.Cliente.EtiquetaDeAsync(cabecera);

        LineaDeRecuentoDto sola = (await escena.Cliente.GetFromJsonAsync<LineaDeRecuentoDto>(recurso))!;

        sola.ShouldBe(enLaPagina, "la línea sola dice lo mismo que la página");

        // EL ALTA ESCRIBE LA CABECERA Y LAS LÍNEAS EN LA MISMA TRANSACCIÓN, así que nacen con la
        // misma versión, y una línea que devolviera la de su cabecera pasaría por la suya. Se mueve
        // una fila cada vez, por debajo del ORM, y cada etiqueta sigue a la suya.
        await TocarAsync("inventario.lineas_recuento", enLaPagina.Id);

        string lineaTocada = await escena.Cliente.EtiquetaDeAsync(recurso);

        lineaTocada.ShouldNotBe(versionDeLaLinea);
        (await escena.Cliente.EtiquetaDeAsync(cabecera)).ShouldBe(versionDeLaCabecera);

        await TocarAsync("inventario.recuentos", deA.Id);

        (await escena.Cliente.EtiquetaDeAsync(cabecera)).ShouldNotBe(versionDeLaCabecera);
        (await escena.Cliente.EtiquetaDeAsync(recurso)).ShouldBe(lineaTocada);

        await NoSeAdmiteAsync(
            escena,
            $"{deA.Id}/lineas/{deLaOtra.Id}",
            HttpStatusCode.NotFound,
            "/errors/recuento-linea-no-encontrada");
        await NoSeAdmiteAsync(
            escena,
            $"{Guid.NewGuid()}/lineas/{enLaPagina.Id}",
            HttpStatusCode.NotFound,
            "/errors/recuento-no-encontrado");
    }

    /// <summary>
    /// La lista va del día de apertura más reciente al más antiguo, con el desempate por identificador
    /// en los dos sentidos; acota por estado, por almacén y por el texto del motivo, y un estado que no
    /// existe es un <c>400</c>. Un almacén vacío también se cuenta, sin líneas.
    /// </summary>
    [Fact]
    public async Task La_lista_acota_por_estado_por_almacen_y_por_motivo()
    {
        EscenaDeRecuento escena = await MontarAsync(769, "RCA-P");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        RecuentoDto deA = await escena.AbrirAsync(de.AlmacenA);

        using HttpResponseMessage altaDeC =
            await escena.AbrirPorLaApiAsync(escena.Peticion(de.AlmacenC, "Recuento tras la rotura del palé"));

        altaDeC.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(altaDeC));
        RecuentoDto deC = (await altaDeC.Content.ReadFromJsonAsync<RecuentoDto>())!;

        deC.Lineas.ShouldBe(0, "un almacén sin existencias se cuenta igual: lo que aparezca se añade");

        // EL ORDEN DE LA LISTA: del día más reciente al más antiguo, y dentro del día por el
        // identificador, que crece con el tiempo y no se invierte. Todas las altas del caso son de
        // hoy, así que la de A se lleva a ayer por debajo del ORM; la de B, que llega después de la
        // de C, va detrás de ella en los dos sentidos.
        await TocarAsync("inventario.recuentos", deA.Id, "fecha_de_apertura = fecha_de_apertura - 1");

        RecuentoDto deB = await escena.AbrirAsync(de.AlmacenB);

        (await ListarAsync(escena, "")).Select(recuento => recuento.Id).ShouldBe([deC.Id, deB.Id, deA.Id]);
        (await ListarAsync(escena, "?sort=fechaDeApertura")).Select(recuento => recuento.Id)
            .ShouldBe([deA.Id, deC.Id, deB.Id]);
        (await ListarAsync(escena, $"?almacen={de.AlmacenA}")).ShouldHaveSingleItem().Id.ShouldBe(deA.Id);
        (await ListarAsync(escena, "?estado=EnCurso")).Count.ShouldBe(3);
        (await ListarAsync(escena, "?estado=Confirmado")).ShouldBeEmpty();
        (await ListarAsync(escena, "?q=rotura")).ShouldHaveSingleItem().Id.ShouldBe(deC.Id);

        RecuentoResumenDto resumen = (await ListarAsync(escena, $"?almacen={de.AlmacenA}")).Single();

        resumen.ShouldBe(new RecuentoResumenDto(
            deA.Id,
            null,
            de.AlmacenA,
            EscenaDeTransferencia.Hoy.AddDays(-1),
            null,
            "EnCurso",
            EscenaDeRecuento.Motivo));

        await NoSeAdmiteAsync(escena, "?estado=Abierto", HttpStatusCode.BadRequest, null);
        await NoSeAdmiteAsync(escena, "?sort=motivo", HttpStatusCode.BadRequest, "/errors/orden-no-admitido");
    }

    /// <summary>Otra empresa no ve el recuento, ni su ficha, ni sus líneas, ni en la lista (R8).</summary>
    [Fact]
    public async Task Otra_empresa_no_ve_el_recuento()
    {
        EscenaDeRecuento escena = await MontarAsync(770, "RCA-E");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        RecuentoDto propio = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(propio.Id)).ShouldHaveSingleItem();

        (HttpClient ajena, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(771));
        _clientes.Add(ajena);

        foreach (string ruta in new[]
        {
            $"{EscenaDeRecuento.Recuentos}/{propio.Id}",
            $"{EscenaDeRecuento.Recuentos}/{propio.Id}/lineas",
            $"{EscenaDeRecuento.Recuentos}/{propio.Id}/lineas/{linea.Id}",
        })
        {
            using HttpResponseMessage lectura = await ajena.GetAsync(ruta);

            lectura.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"{ruta}: {await Escenario.Detalle(lectura)}");
        }

        PaginaDe<RecuentoResumenDto> lista =
            (await ajena.GetFromJsonAsync<PaginaDe<RecuentoResumenDto>>(EscenaDeRecuento.Recuentos))!;

        lista.Total.ShouldBe(0);
    }

    /// <summary>
    /// El alta rechaza el almacén que no existe o está bloqueado; la serie que no existe, que está
    /// cerrada, que es de otro documento o que es de un ejercicio terminado, en cualquiera de los dos
    /// sitios; el motivo vacío o largo y la empresa que no opera; y no escribe nada.
    /// </summary>
    /// <remarks>
    /// <b>Las dos últimas de la serie, desde el ítem 2.13</b>: antes se descubrían al confirmar,
    /// tras horas de conteo, con una serie que ya no se cambia. Dónde está la frontera del ejercicio
    /// lo dice <c>LaSerieDelRecuentoSeMiraAlAbrirTests</c>, en el carril rápido; aquí, que el alta
    /// pregunta, con las series de verdad.
    /// </remarks>
    [Fact]
    public async Task El_alta_rechaza_lo_que_no_puede_contar_y_no_escribe_nada()
    {
        EscenaDeRecuento escena = await MontarAsync(772, "RCA-G");
        EscenaDeTransferencia de = escena.Escena;

        SerieDto recuentosCerrada = await LosMaestrosPorLaApi.CrearSerieEnAsync(
            escena.Cliente, de.Ejercicio.Id, "RCA-G-RX", TipoDeDocumento.RecuentoDeInventario);
        SerieDto ajustesCerrada =
            await LosMaestrosPorLaApi.CrearSerieEnAsync(escena.Cliente, de.Ejercicio.Id, "RCA-G-AX");
        await de.CerrarLaSerieAsync(postgres, recuentosCerrada.Id);
        await de.CerrarLaSerieAsync(postgres, ajustesCerrada.Id);

        // EL EJERCICIO DEL AÑO PASADO, ABIERTO: sus series están activas y son del documento que toca,
        // así que lo único que las para es que su ejercicio terminó.
        EjercicioDto pasado =
            await LosMaestrosPorLaApi.CrearEjercicioAsync(escena.Cliente, EscenaDeTransferencia.Hoy.Year - 1);
        SerieDto recuentosDelPasado = await LosMaestrosPorLaApi.CrearSerieEnAsync(
            escena.Cliente, pasado.Id, "RCA-G-RP", TipoDeDocumento.RecuentoDeInventario);
        SerieDto ajustesDelPasado =
            await LosMaestrosPorLaApi.CrearSerieEnAsync(escena.Cliente, pasado.Id, "RCA-G-AP");

        using (HttpResponseMessage bloqueo =
            await escena.Cliente.SuprimirAsync($"{LosMaestrosPorLaApi.Almacenes}/{de.AlmacenC}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        AbrirRecuentoDto bien = escena.Peticion(de.AlmacenA);

        (AbrirRecuentoDto Peticion, HttpStatusCode Estado, string Tipo)[] rechazos =
        [
            (bien with { AlmacenId = Guid.NewGuid() }, HttpStatusCode.BadRequest, "recuento-almacen-no-encontrado"),
            (bien with { AlmacenId = de.AlmacenC }, HttpStatusCode.Conflict, "recuento-almacen-bloqueado"),
            (bien with { SerieId = Guid.NewGuid() }, HttpStatusCode.BadRequest, "recuento-serie-no-encontrada"),
            (bien with { SerieDelAjusteId = Guid.NewGuid() }, HttpStatusCode.BadRequest, "recuento-serie-no-encontrada"),
            (bien with { SerieId = recuentosCerrada.Id }, HttpStatusCode.Conflict, "recuento-serie-cerrada"),
            (bien with { SerieDelAjusteId = ajustesCerrada.Id }, HttpStatusCode.Conflict, "recuento-serie-cerrada"),
            (bien with { SerieId = bien.SerieDelAjusteId }, HttpStatusCode.Conflict, "recuento-serie-de-otro-documento"),
            (bien with { SerieDelAjusteId = bien.SerieId }, HttpStatusCode.Conflict, "recuento-serie-de-otro-documento"),
            (bien with { SerieDelAjusteId = de.SerieDeTransferencias.Id }, HttpStatusCode.Conflict, "recuento-serie-de-otro-documento"),
            (bien with { SerieId = recuentosDelPasado.Id }, HttpStatusCode.Conflict, "recuento-serie-de-un-ejercicio-terminado"),
            (bien with { SerieDelAjusteId = ajustesDelPasado.Id }, HttpStatusCode.Conflict, "recuento-serie-de-un-ejercicio-terminado"),
            (bien with { Motivo = "   " }, HttpStatusCode.BadRequest, "recuento-motivo-no-valido"),
            (bien with { Motivo = new string('m', 301) }, HttpStatusCode.BadRequest, "recuento-motivo-no-valido"),
        ];

        foreach ((AbrirRecuentoDto peticion, HttpStatusCode estado, string tipo) in rechazos)
        {
            using HttpResponseMessage alta = await escena.AbrirPorLaApiAsync(peticion);

            alta.StatusCode.ShouldBe(estado, $"{tipo}: {await Escenario.Detalle(alta)}");
            (await EscenaDeTransferencia.TipoDelProblemaAsync(alta)).ShouldBe($"/errors/{tipo}");
        }

        // UNA EMPRESA QUE NO OPERA SE PARA ANTES DE LOS MAESTROS: sin esa pregunta, el alta seguiría
        // y la pararía el almacén, que no es de esa empresa, con otro código.
        await using (ElModuloDeInventario deNinguna = new(postgres, Guid.CreateVersion7()))
        {
            Resultado<RecuentoDto> sinEmpresa =
                await deNinguna.AltaDeRecuento.EjecutarAsync(bien, CancellationToken.None);

            sinEmpresa.Error.ShouldNotBeNull().Codigo.ShouldBe("empresa-activa-no-operativa");
        }

        (await ListarAsync(escena, "")).ShouldBeEmpty("ningún rechazo deja un recuento escrito");

        using HttpResponseMessage elBueno = await escena.AbrirPorLaApiAsync(bien with { Motivo = new string('m', 300) });

        elBueno.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            "el mismo alta con lo que vale sale bien, así que cada rechazo es por lo suyo: " +
            await Escenario.Detalle(elBueno));
    }

    private static async Task<IReadOnlyList<RecuentoResumenDto>> ListarAsync(EscenaDeRecuento escena, string consulta)
    {
        using HttpResponseMessage lectura = await escena.Cliente.GetAsync(EscenaDeRecuento.Recuentos + consulta);

        return (await EscenaDeTransferencia.LeerAsync<PaginaDe<RecuentoResumenDto>>(lectura)).Elementos;
    }

    // UNA ESCRITURA POR DEBAJO DEL ORM, que mueve el `xmin` de la fila y nada más que lo que diga.
    private async Task TocarAsync(string tabla, Guid id, string cambio = "id = id")
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new($"UPDATE {tabla} SET {cambio} WHERE id = @id", conexion);
        orden.Parameters.AddWithValue("id", id);

        (await orden.ExecuteNonQueryAsync()).ShouldBe(1, $"{tabla} {id}");
    }

    private static async Task NoSeAdmiteAsync(
        EscenaDeRecuento escena, string tras, HttpStatusCode estado, string? tipo)
    {
        string ruta = tras.StartsWith('?') ? EscenaDeRecuento.Recuentos + tras : $"{EscenaDeRecuento.Recuentos}/{tras}";

        using HttpResponseMessage lectura = await escena.Cliente.GetAsync(ruta);

        lectura.StatusCode.ShouldBe(estado, $"{ruta}: {await Escenario.Detalle(lectura)}");

        if (tipo is not null)
        {
            (await EscenaDeTransferencia.TipoDelProblemaAsync(lectura)).ShouldBe(tipo);
        }
    }

    private async Task<EscenaDeRecuento> MontarAsync(int semilla, string codigo, string trazabilidad = "Ninguna")
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, semilla, codigo, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
