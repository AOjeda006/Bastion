using System.Globalization;
using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Contracts;
using Bastion.Inventario.Contracts;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Organizacion.Contracts;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Exportacion;

/// <summary>
/// Las exportaciones de Inventario contra la base: los ajustes, los recuentos y sus líneas, las
/// transferencias y sus líneas, y los saldos a una fecha, cada fichero entero, en el dialecto de
/// la importación, con el orden y los filtros de su listado, y cada maestro con su código y su
/// nombre (ADR-0073, ADR-0074).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un maestro se nombra si quien exporta lo puede ver</b>, y si no, sale su identificador. Se
/// prueba con dos cuentas de la empresa de la escena que se reparten los cuatro permisos, la una
/// pareja de la otra.
/// </para>
/// <para>
/// <b>Las líneas de una transferencia no tienen listado</b>: salen de su ficha, contestan su
/// <c>404</c>, y rechazan un <c>?q=</c>, que no filtraría nada. El orden, como la ficha, no lo
/// leen, igual que los códigos de barras con su listado sin criterios. Las
/// transferencias y los saldos tampoco miran un texto, y lo rechazan como su listado.
/// </para>
/// <para>
/// <b>Semillas: del 950 al 953</b>, del bloque del 3.2 (ver
/// <c>ElTextoSoloLoRecibeQuienLoMiraTests</c>).
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasExportacionesDeInventarioTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Inventario = "/api/v1/inventario";

    private const string Hueco = "Hueco del caso del ADR-0037";

    private const string CabeceraDeAjustes =
        "numero;almacen_codigo;almacen_nombre;fecha_de_operacion;estado;motivo\r\n";

    private const string CabeceraDeRecuentos =
        "numero;almacen_codigo;almacen_nombre;fecha_de_apertura;fecha_de_confirmacion;estado;motivo\r\n";

    private const string CabeceraDeLineasDeRecuento =
        "numero;ubicacion_codigo;ubicacion_nombre;articulo_codigo;articulo_nombre;codigo_de_lote;" +
        "numero_de_serie;unidad_base_codigo;unidad_base_nombre;origen;coste_unitario;contado;" +
        "teorico_al_contar;teorico;en_transito;diferencia;teorico_cambiado\r\n";

    private const string CabeceraDeTransferencias =
        "numero;almacen_origen_codigo;almacen_origen_nombre;almacen_destino_codigo;almacen_destino_nombre;" +
        "fecha_de_envio;fecha_de_recepcion;estado\r\n";

    private const string CabeceraDeLineasDeTransferencia =
        "numero;ubicacion_origen_codigo;ubicacion_origen_nombre;ubicacion_destino_codigo;" +
        "ubicacion_destino_nombre;articulo_codigo;articulo_nombre;cantidad_introducida;" +
        "unidad_introducida_codigo;unidad_introducida_nombre;factor_a_unidad_base;cantidad_en_unidad_base;" +
        "codigo_de_lote;numero_de_serie;valor;divisa\r\n";

    private const string CabeceraDeSaldos =
        "fecha;articulo_codigo;articulo_nombre;almacen_codigo;almacen_nombre;cantidad;valor;" +
        "cantidad_en_transito;valor_en_transito;divisa\r\n";

    private readonly ApiDeVerdad _api = new(postgres);

    private static string Hoy => Dia(EscenaDeTransferencia.Hoy);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Los_ajustes_los_recuentos_y_sus_lineas_salen_con_los_filtros_de_su_listado()
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, 950, "EXR", "PorLote");
        EscenaDeTransferencia de = escena.Escena;
        using HttpClient cliente = de.Cliente;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m, lote: "L-1");
        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m, lote: "L-2");
        await escena.EntrarAsync(postgres, de.AlmacenB, de.UbicacionB, 2m, lote: "L-3");

        await using (ElModuloDeInventario modulo = new(postgres, de.EmpresaId))
        {
            Resultado<AjusteDto> borrador = await modulo.Alta.EjecutarAsync(
                new AbrirAjusteDto(
                    de.SerieDeAjustes.Id,
                    de.AlmacenA,
                    EscenaDeTransferencia.Hoy,
                    "=Rotura en el muelle",
                    [new LineaDeAjusteDto(de.UbicacionA, de.ArticuloId, 1m, de.UnidadId, 2.5m, "L-9", null)]),
                CancellationToken.None);

            borrador.EsCorrecto.ShouldBeTrue($"«{borrador.Error?.Codigo}»");
        }

        string confirmado = $";EXR-A;Almacén EXR-A;{Hoy};Confirmado;Regularización de un recuento\r\n";
        string primero = "1" + confirmado;
        string segundo = "2" + confirmado;
        string tercero = $"3;EXR-B;Almacén EXR-B;{Hoy};Confirmado;Regularización de un recuento\r\n";
        string delMuelle = $";EXR-A;Almacén EXR-A;{Hoy};Borrador;'=Rotura en el muelle\r\n";

        (await ExportarAsync(cliente, "ajustes")).ShouldBe(
            CabeceraDeAjustes + primero + segundo + tercero + delMuelle,
            "del mismo día, por su alta; y el borrador, sin número y con el apóstrofo en el motivo");
        (await ExportarAsync(cliente, "ajustes", "?estado=Confirmado&sort=-numero")).ShouldBe(
            CabeceraDeAjustes + tercero + segundo + primero);
        (await ExportarAsync(cliente, "ajustes", $"?almacen={de.AlmacenB}")).ShouldBe(CabeceraDeAjustes + tercero);
        (await ExportarAsync(cliente, "ajustes", "?q=muelle")).ShouldBe(CabeceraDeAjustes + delMuelle);

        RecuentoDto deA = await escena.AbrirAsync(de.AlmacenA);

        using (HttpResponseMessage altaDeC =
            await escena.AbrirPorLaApiAsync(escena.Peticion(de.AlmacenC, "Recuento tras la rotura del palé")))
        {
            altaDeC.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(altaDeC));
        }

        string recuentoDeA = $";EXR-A;Almacén EXR-A;{Hoy};;EnCurso;{EscenaDeRecuento.Motivo}\r\n";
        string recuentoDeC = $";EXR-C;Almacén EXR-C;{Hoy};;EnCurso;Recuento tras la rotura del palé\r\n";

        (await ExportarAsync(cliente, "recuentos")).ShouldBe(CabeceraDeRecuentos + recuentoDeA + recuentoDeC);
        (await ExportarAsync(cliente, "recuentos", $"?almacen={de.AlmacenC}")).ShouldBe(
            CabeceraDeRecuentos + recuentoDeC);
        (await ExportarAsync(cliente, "recuentos", "?q=rotura")).ShouldBe(CabeceraDeRecuentos + recuentoDeC);
        (await ExportarAsync(cliente, "recuentos", "?estado=Confirmado")).ShouldBe(
            CabeceraDeRecuentos, "un listado vacío es la cabecera sola");

        LineaDeRecuentoDto delPrimerLote = (await escena.TodasLasLineasAsync(deA.Id))
            .Single(linea => linea.CodigoDeLote == "L-1");

        await escena.ContarConSuVersionAsync(deA.Id, delPrimerLote.Id, 2m);

        string lineas = $"{EscenaDeRecuento.Recuentos}/{deA.Id}/lineas";
        string contada = $"1;EXR-A1;{Hueco};APT-950;Artículo 950;L-1;;W950;Unidad 950;Precargada;;2;3;3;0;-1;no\r\n";
        string sinContar = $"2;EXR-A1;{Hueco};APT-950;Artículo 950;L-2;;W950;Unidad 950;Precargada;;;;5;0;;no\r\n";

        (await Exportaciones.ExportarAsync(cliente, lineas, "lineas-de-recuento")).ShouldBe(
            CabeceraDeLineasDeRecuento + contada + sinContar,
            "lo contado, el teórico al contarlo y la diferencia; y sin contar, las tres vacías");
        (await Exportaciones.ExportarAsync(cliente, lineas, "lineas-de-recuento", "?sort=-numero")).ShouldBe(
            CabeceraDeLineasDeRecuento + sinContar + contada);
        (await Exportaciones.ExportarAsync(cliente, lineas, "lineas-de-recuento", "?solo=sin-contar")).ShouldBe(
            CabeceraDeLineasDeRecuento + sinContar);
        (await Exportaciones.ExportarAsync(cliente, lineas, "lineas-de-recuento", "?q=l-1")).ShouldBe(
            CabeceraDeLineasDeRecuento + contada);

        // El recuento que no existe es el 404 del listado de sus líneas, con su problema y sin fichero.
        string inventado = $"{EscenaDeRecuento.Recuentos}/{Guid.CreateVersion7()}/lineas";

        await ExigirElMismoProblemaAsync(cliente, inventado, $"{inventado}/exportacion", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Las_transferencias_y_sus_lineas_salen_con_sus_almacenes_y_sus_huecos_nombrados()
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, 951, "EXT", 951);
        using HttpClient cliente = escena.Cliente;

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2.5m);
        await escena.EntrarAsync(postgres, escena.AlmacenC, escena.UbicacionC, 5m, 2.5m);

        TransferenciaDto recibida = await escena.EnviarAsync(postgres, 4m);

        using (HttpResponseMessage recepcion =
            await escena.RecibirPorLaApiAsync(recibida.Id, EscenaDeTransferencia.Hoy))
        {
            recepcion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(recepcion));
        }

        await escena.EnviarAsync(postgres, 2m);

        Guid borrador;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            borrador = await escena.AbrirAsync(modulo, 1m, desdeC: true);
        }

        string deAaB = "EXT-A;Almacén EXT-A;EXT-B;Almacén EXT-B";
        string laRecibida = $"1;{deAaB};{Hoy};{Hoy};Recibida\r\n";
        string laEnviada = $"2;{deAaB};{Hoy};;Enviada\r\n";
        string elBorrador = $";EXT-C;Almacén EXT-C;EXT-B;Almacén EXT-B;{Hoy};;Borrador\r\n";

        (await ExportarAsync(cliente, "transferencias")).ShouldBe(
            CabeceraDeTransferencias + laRecibida + laEnviada + elBorrador);
        (await ExportarAsync(cliente, "transferencias", "?estado=Enviada")).ShouldBe(
            CabeceraDeTransferencias + laEnviada);
        (await ExportarAsync(cliente, "transferencias", $"?origen={escena.AlmacenC}")).ShouldBe(
            CabeceraDeTransferencias + elBorrador);
        (await ExportarAsync(cliente, "transferencias", $"?origen={escena.AlmacenA}&sort=-numero")).ShouldBe(
            CabeceraDeTransferencias + laEnviada + laRecibida);
        (await ExportarAsync(cliente, "transferencias", $"?destino={escena.AlmacenA}")).ShouldBe(
            CabeceraDeTransferencias);

        // Un texto, que el listado no mira, es su 400; y la caja en blanco no es un texto.
        await ExigirElMismoProblemaAsync(
            cliente,
            $"{EscenaDeTransferencia.Transferencias}?q=EXT",
            $"{EscenaDeTransferencia.Transferencias}/exportacion?q=EXT",
            HttpStatusCode.BadRequest,
            "/errors/filtro-no-admitido");
        (await ExportarAsync(cliente, "transferencias", "?q=%20")).ShouldBe(
            CabeceraDeTransferencias + laRecibida + laEnviada + elBorrador);

        // LAS LÍNEAS, de la ficha: el valor que se fijó al enviar, en la divisa de la transferencia, y
        // el borrador, que todavía no lo tiene.
        string articulo = "APT-951;Artículo 951";
        string unidad = "W951;Unidad 951";

        (await LineasDeAsync(cliente, recibida.Id)).ShouldBe(
            CabeceraDeLineasDeTransferencia +
            $"1;EXT-A1;{Hueco};EXT-B1;{Hueco};{articulo};4;{unidad};1;4;;;10;EUR\r\n");
        (await LineasDeAsync(cliente, borrador)).ShouldBe(
            CabeceraDeLineasDeTransferencia +
            $"1;EXT-C1;{Hueco};EXT-B1;{Hueco};{articulo};1;{unidad};1;1;;;;EUR\r\n");

        // Un texto es el 400 de un listado que no lo mira, y no un fichero que parece filtrado; y la
        // caja en blanco no es un texto.
        string deLaRecibida = $"{EscenaDeTransferencia.Transferencias}/{recibida.Id}/lineas";

        using (HttpResponseMessage conTexto = await cliente.GetAsync($"{deLaRecibida}/exportacion?q=EXT"))
        {
            conTexto.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await Escenario.Detalle(conTexto));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(conTexto)).ShouldBe("/errors/filtro-no-admitido");
        }

        (await Exportaciones.ExportarAsync(cliente, deLaRecibida, "lineas-de-transferencia", "?q=%20"))
            .ShouldBe(await LineasDeAsync(cliente, recibida.Id));

        // La transferencia que no existe es el 404 de su ficha.
        string inventada = $"{EscenaDeTransferencia.Transferencias}/{Guid.CreateVersion7()}";

        await ExigirElMismoProblemaAsync(
            cliente, inventada, $"{inventada}/lineas/exportacion", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Los_saldos_salen_al_final_del_dia_pedido_con_lo_que_vuela_y_sus_maestros_nombrados()
    {
        EscenaDeTransferencia escena = await EscenaDeTransferencia.MontarAsync(_api, 952, "EXS", 952);
        using HttpClient cliente = escena.Cliente;

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 10m, 2.5m);
        await escena.EnviarAsync(postgres, 4m);

        // El orden es el de los identificadores, que son los de la base: el del artículo, que es
        // uno, y dentro, el del almacén.
        (Guid Almacen, string Hoy, string Ayer)[] filas =
        [
            (escena.AlmacenA, "EXS-A;Almacén EXS-A;6;15;0;0;EUR", "EXS-A;Almacén EXS-A;0;0;0;0;EUR"),
            (escena.AlmacenB, "EXS-B;Almacén EXS-B;0;0;4;10;EUR", "EXS-B;Almacén EXS-B;0;0;0;0;EUR"),
        ];
        filas = [.. filas.OrderBy(fila => fila.Almacen.ToString("D"), StringComparer.Ordinal)];

        string articulo = "APT-952;Artículo 952";
        DateOnly ayer = EscenaDeTransferencia.Hoy.AddDays(-1);

        (await ExportarAsync(cliente, "saldos")).ShouldBe(
            CabeceraDeSaldos + string.Concat(filas.Select(fila => $"{Hoy};{articulo};{fila.Hoy}\r\n")),
            "sin fecha, hoy: lo que queda en A, y lo que vuela hacia B");
        (await ExportarAsync(cliente, "saldos", $"?fecha={Dia(ayer)}")).ShouldBe(
            CabeceraDeSaldos + string.Concat(filas.Select(fila => $"{Dia(ayer)};{articulo};{fila.Ayer}\r\n")),
            "el día pedido, en su columna: ayer no se había movido nada");
        (await ExportarAsync(cliente, "saldos", $"?almacen={escena.AlmacenB}&articulo={escena.ArticuloId}"))
            .ShouldBe(CabeceraDeSaldos + $"{Hoy};{articulo};EXS-B;Almacén EXS-B;0;0;4;10;EUR\r\n");

        // Una fecha futura es el 400 de la consulta; y un texto, el de un listado que no lo mira.
        string manana = Dia(EscenaDeTransferencia.Hoy.AddDays(1));

        await ExigirElMismoProblemaAsync(
            cliente,
            $"{Inventario}/saldos?fecha={manana}",
            $"{Inventario}/saldos/exportacion?fecha={manana}",
            HttpStatusCode.BadRequest);
        await ExigirElMismoProblemaAsync(
            cliente,
            $"{Inventario}/saldos?q=EXS",
            $"{Inventario}/saldos/exportacion?q=EXS",
            HttpStatusCode.BadRequest,
            "/errors/filtro-no-admitido");
        (await ExportarAsync(cliente, "saldos", "?q=%20")).ShouldBe(
            CabeceraDeSaldos + string.Concat(filas.Select(fila => $"{Hoy};{articulo};{fila.Hoy}\r\n")));
    }

    /// <summary>
    /// Cada maestro de un recuento se nombra con el permiso de verlo, y sin él sale su
    /// identificador.
    /// </summary>
    [Fact]
    public async Task Cada_maestro_del_recuento_se_nombra_solo_si_quien_exporta_lo_puede_ver()
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, 953, "EXP");
        EscenaDeTransferencia de = escena.Escena;
        using HttpClient administrador = de.Cliente;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        RecuentoDto recuento = await escena.AbrirAsync(de.AlmacenA);
        string lineas = $"{EscenaDeRecuento.Recuentos}/{recuento.Id}/lineas";

        using HttpClient conLoDeOrganizacion = await _api.ConPermisosEnAsync(
            administrador,
            de.EmpresaId,
            PermisosDeInventario.RecuentoVer,
            PermisosDeOrganizacion.AlmacenVer,
            PermisosDeOrganizacion.UbicacionVer);
        using HttpClient conLoDemas = await _api.ConPermisosEnAsync(
            administrador,
            de.EmpresaId,
            PermisosDeInventario.RecuentoVer,
            PermisosDeCatalogo.ArticuloVer,
            PermisosDeOrganizacion.UnidadMedidaVer);

        string motivo = $"{Hoy};;EnCurso;{EscenaDeRecuento.Motivo}\r\n";

        (await ExportarAsync(conLoDeOrganizacion, "recuentos")).ShouldBe(
            CabeceraDeRecuentos + $";EXP-A;Almacén EXP-A;{motivo}");
        (await ExportarAsync(conLoDemas, "recuentos")).ShouldBe(CabeceraDeRecuentos + $";{de.AlmacenA};;{motivo}");

        string cuentas = "Precargada;;;;3;0;;no\r\n";

        (await Exportaciones.ExportarAsync(conLoDeOrganizacion, lineas, "lineas-de-recuento")).ShouldBe(
            CabeceraDeLineasDeRecuento + $"1;EXP-A1;{Hueco};{de.ArticuloId};;;;{de.UnidadId};;{cuentas}");
        (await Exportaciones.ExportarAsync(conLoDemas, lineas, "lineas-de-recuento")).ShouldBe(
            CabeceraDeLineasDeRecuento + $"1;{de.UbicacionA};;APT-953;Artículo 953;;;W953;Unidad 953;{cuentas}");
    }

    private static string Dia(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Task<string> ExportarAsync(HttpClient cliente, string recurso, string consulta = "") =>
        Exportaciones.ExportarAsync(cliente, $"{Inventario}/{recurso}", recurso, consulta);

    private static Task<string> LineasDeAsync(HttpClient cliente, Guid transferenciaId) =>
        Exportaciones.ExportarAsync(
            cliente, $"{EscenaDeTransferencia.Transferencias}/{transferenciaId}/lineas", "lineas-de-transferencia");

    /// <summary>
    /// La exportación contesta el mismo problema que su hermana, antes del primer byte y sin
    /// fichero.
    /// </summary>
    private static async Task ExigirElMismoProblemaAsync(
        HttpClient cliente,
        string hermana,
        string exportacion,
        HttpStatusCode codigo,
        string? tipo = null)
    {
        using HttpResponseMessage suya = await cliente.GetAsync(hermana);
        using HttpResponseMessage exportada = await cliente.GetAsync(exportacion);

        suya.StatusCode.ShouldBe(codigo, $"el arnés: {hermana}");
        exportada.StatusCode.ShouldBe(codigo, await Escenario.Detalle(exportada));
        exportada.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await Exportaciones.ProblemaAsync(exportada)).ShouldBe(await Exportaciones.ProblemaAsync(suya));

        if (tipo is not null)
        {
            (await EscenaDeTransferencia.TipoDelProblemaAsync(exportada)).ShouldBe(tipo);
        }
    }
}
