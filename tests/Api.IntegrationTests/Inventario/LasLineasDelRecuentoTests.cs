using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las tres escrituras de una línea del recuento por la API (ítem 2.12): contarla, añadir una clave
/// que la precarga no traía y quitar una que no se va a contar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las tres llevan la versión de la línea, y las tres tocan la de la cabecera</b> (ADR-0055 §4):
/// la de la línea dice que quien escribe ha visto lo que otro contó, y la de la cabecera, que la
/// confirmación ve todo lo que se escribió desde que se leyó la ficha. Por eso cada caso mira las dos.
/// </para>
/// <para>
/// <b>Un rechazo no escribe nada</b>, y se ve por la versión: si un <c>400</c> o un <c>409</c> hubiera
/// tocado la cabecera, la confirmación del usuario que tenía la ficha abierta saldría <c>412</c> sin que
/// nadie hubiera cambiado nada.
/// </para>
/// <para>
/// <b>Semillas: del 778 al 787</b>, empresas y maestros con el mismo número; el 782 es solo una empresa,
/// la ajena. El reparto del bloque del 2.12 está en la cabecera de
/// <c>ElPuertoDelArticuloContraLaBaseTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasLineasDelRecuentoTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    /// Contar anota lo contado con el teórico de ese instante, mueve la versión de la línea y la de la
    /// cabecera, y deja la huella como estaba; la versión de antes ya no vale, y la de ahora sí.
    /// </summary>
    [Fact]
    public async Task Contar_anota_lo_contado_con_su_teorico_y_mueve_la_version_de_la_linea_y_la_de_la_cabecera()
    {
        EscenaDeRecuento escena = await MontarAsync(778, "RCL-C");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        string ruta = EscenaDeRecuento.RutaDeLaLinea(abierto.Id, linea.Id);
        string cabecera = $"{EscenaDeRecuento.Recuentos}/{abierto.Id}";
        string deLaLinea = await escena.Cliente.EtiquetaDeAsync(ruta);
        string deLaCabecera = await escena.Cliente.EtiquetaDeAsync(cabecera);
        RecuentoDto antes = await escena.FichaAsync(abierto.Id);

        using (HttpResponseMessage sinVersion = await escena.ContarAsync(abierto.Id, linea.Id, null, 7m))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                sinVersion, HttpStatusCode.PreconditionRequired, "falta-if-match", "contar sin If-Match");
        }

        LineaDeRecuentoDto contada;

        using (HttpResponseMessage conteo = await escena.ContarAsync(abierto.Id, linea.Id, deLaLinea, 7m))
        {
            contada = await EscenaDeTransferencia.LeerAsync<LineaDeRecuentoDto>(conteo);
        }

        contada.Id.ShouldBe(linea.Id);
        contada.Contado.ShouldBe(7m);
        contada.TeoricoAlContar.ShouldBe(5m, "el teórico de cuando se contó es el físico de ese instante");
        contada.Teorico.ShouldBe(5m);
        contada.Diferencia.ShouldBe(2m, "lo contado menos el teórico: lo que subiría el ajuste");
        contada.TeoricoCambiado.ShouldBeFalse();
        contada.LineaDeAjusteId.ShouldBeNull("contar no mueve el libro");

        string deLaLineaDespues = await escena.Cliente.EtiquetaDeAsync(ruta);

        deLaLineaDespues.ShouldNotBe(deLaLinea, "contar escribe la línea");
        (await escena.Cliente.EtiquetaDeAsync(cabecera)).ShouldNotBe(
            deLaCabecera, "toda escritura en una línea toca la cabecera (ADR-0055 §4)");

        RecuentoDto despues = await escena.FichaAsync(abierto.Id);

        despues.LineasSinContar.ShouldBe(0);
        despues.HuellaDelTeorico.ShouldBe(
            antes.HuellaDelTeorico, "contar no cambia el teórico de ninguna línea, y la huella es eso");

        // LA VERSIÓN DE ANTES YA NO VALE: quien contó sobre lo que vio no pisa sin verlo lo que otro
        // contó después.
        using (HttpResponseMessage vieja = await escena.ContarAsync(abierto.Id, linea.Id, deLaLinea, 9m))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                vieja, HttpStatusCode.PreconditionFailed, "version-obsoleta", "contar con la versión de antes");
        }

        (await escena.LineaAsync(abierto.Id, linea.Id)).Contado.ShouldBe(7m, "el 412 no escribe nada");

        // Y CON LA DE AHORA SE VUELVE A CONTAR, que es como se corrige un conteo.
        using HttpResponseMessage recuento = await escena.ContarAsync(abierto.Id, linea.Id, deLaLineaDespues, 4m);

        LineaDeRecuentoDto recontada = await EscenaDeTransferencia.LeerAsync<LineaDeRecuentoDto>(recuento);

        recontada.Contado.ShouldBe(4m);
        recontada.Diferencia.ShouldBe(-1m);
    }

    /// <summary>
    /// Contar rechaza lo negativo, más de seis decimales, lo que no cabe y un cuerpo sin la cifra, y
    /// ninguno de los rechazos escribe nada.
    /// </summary>
    [Fact]
    public async Task Contar_rechaza_lo_que_no_se_puede_contar_y_no_escribe_nada()
    {
        EscenaDeRecuento escena = await MontarAsync(779, "RCL-R");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 5m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        string ruta = EscenaDeRecuento.RutaDeLaLinea(abierto.Id, linea.Id);
        string cabecera = $"{EscenaDeRecuento.Recuentos}/{abierto.Id}";
        string deLaLinea = await escena.Cliente.EtiquetaDeAsync(ruta);
        string deLaCabecera = await escena.Cliente.EtiquetaDeAsync(cabecera);

        // EL TOPE ES EL DE LA COLUMNA: `numeric(18,6)` deja doce cifras enteras.
        (decimal? Contado, string Codigo, string Cual)[] rechazos =
        [
            (-1m, "recuento-contado-no-valido", "negativo"),
            (0.0000001m, "recuento-contado-no-valido", "con siete decimales"),
            (1_000_000_000_000m, "recuento-contado-no-valido", "con trece cifras enteras"),
            (null, "datos-no-validos", "sin la cifra, que no es un cero"),
        ];

        foreach ((decimal? contado, string codigo, string cual) in rechazos)
        {
            using HttpResponseMessage rechazo = await escena.ContarAsync(abierto.Id, linea.Id, deLaLinea, contado);

            await EscenaDeRecuento.ExigirElProblemaAsync(rechazo, HttpStatusCode.BadRequest, codigo, cual);
        }

        (await escena.Cliente.EtiquetaDeAsync(ruta)).ShouldBe(deLaLinea, "un rechazo no escribe la línea");
        (await escena.Cliente.EtiquetaDeAsync(cabecera)).ShouldBe(deLaCabecera, "ni toca la cabecera");
        (await escena.FichaAsync(abierto.Id)).LineasSinContar.ShouldBe(1);

        // Y EL MISMO CONTEO CON UNA CIFRA QUE CABE SALE BIEN, así que cada rechazo es por la suya. El
        // tope menos una millonésima es lo más alto que se puede contar.
        using HttpResponseMessage elBueno =
            await escena.ContarAsync(abierto.Id, linea.Id, deLaLinea, 999_999_999_999.999999m);

        (await EscenaDeTransferencia.LeerAsync<LineaDeRecuentoDto>(elBueno)).Contado
            .ShouldBe(999_999_999_999.999999m);
    }

    /// <summary>
    /// Un número de serie se cuenta con un cero o un uno, y no entra dos veces en el recuento: ni en
    /// la misma ubicación ni en otra.
    /// </summary>
    [Fact]
    public async Task Una_serie_se_cuenta_con_cero_o_uno_y_no_entra_dos_veces_ni_en_otra_ubicacion()
    {
        EscenaDeRecuento escena = await MontarAsync(780, "RCL-S", "PorNumeroSerie");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 1m, serie: "S-1");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();
        string deLaLinea = await escena.Cliente.EtiquetaDeAsync(EscenaDeRecuento.RutaDeLaLinea(abierto.Id, linea.Id));

        linea.NumeroDeSerie.ShouldBe("S-1");

        foreach (decimal mal in new[] { 2m, 0.5m })
        {
            using HttpResponseMessage rechazo = await escena.ContarAsync(abierto.Id, linea.Id, deLaLinea, mal);

            await EscenaDeRecuento.ExigirElProblemaAsync(
                rechazo, HttpStatusCode.BadRequest, "recuento-contado-no-valido", $"una serie contada con {mal}");
        }

        // UN CERO ES UNA RESPUESTA: la unidad no está donde decía el libro.
        using (HttpResponseMessage cero = await escena.ContarAsync(abierto.Id, linea.Id, deLaLinea, 0m))
        {
            (await EscenaDeTransferencia.LeerAsync<LineaDeRecuentoDto>(cero)).Diferencia.ShouldBe(-1m);
        }

        UbicacionDto otroHueco =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, de.AlmacenA, "RCL-S-A2");

        AnadirLineaDeRecuentoDto laMisma = new(de.UbicacionA, de.ArticuloId, null, "S-1", null);

        using (HttpResponseMessage enLaMisma = await AnadirAsync(escena, abierto.Id, laMisma))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                enLaMisma, HttpStatusCode.Conflict, "recuento-clave-repetida", "la misma serie en su ubicación");
        }

        using (HttpResponseMessage enOtra =
            await AnadirAsync(escena, abierto.Id, laMisma with { UbicacionId = otroHueco.Id }))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                enOtra, HttpStatusCode.Conflict, "recuento-serie-repetida", "la misma serie en otra ubicación");
        }

        // EL HUECO VALE PARA OTRA SERIE, así que el 409 de arriba era por la serie y no por el hueco.
        using HttpResponseMessage otraSerie =
            await AnadirAsync(escena, abierto.Id, laMisma with { UbicacionId = otroHueco.Id, NumeroDeSerie = "S-2" });

        otraSerie.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(otraSerie));
        (await escena.FichaAsync(abierto.Id)).Lineas.ShouldBe(2);
    }

    /// <summary>
    /// Un recuento que no existe, una línea que no existe y una línea de otro recuento son un
    /// <c>404</c> en las tres escrituras, y el recuento de otra empresa también.
    /// </summary>
    [Fact]
    public async Task Lo_que_no_existe_y_lo_de_otra_empresa_es_un_404_en_las_tres_escrituras()
    {
        EscenaDeRecuento escena = await MontarAsync(781, "RCL-N");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);
        await escena.EntrarAsync(postgres, de.AlmacenB, de.UbicacionB, 2m);

        RecuentoDto deA = await escena.AbrirAsync(de.AlmacenA);
        RecuentoDto deB = await escena.AbrirAsync(de.AlmacenB);
        LineaDeRecuentoDto la = (await escena.TodasLasLineasAsync(deA.Id)).ShouldHaveSingleItem();
        LineaDeRecuentoDto lb = (await escena.TodasLasLineasAsync(deB.Id)).ShouldHaveSingleItem();

        string ruta = EscenaDeRecuento.RutaDeLaLinea(deA.Id, la.Id);
        string etiqueta = await escena.Cliente.EtiquetaDeAsync(ruta);
        var ninguno = Guid.NewGuid();

        (Guid Recuento, Guid Linea, string Codigo, string Cual)[] casos =
        [
            (ninguno, la.Id, "recuento-no-encontrado", "un recuento que no existe"),
            (deA.Id, ninguno, "recuento-linea-no-encontrada", "una línea que no existe"),
            (deA.Id, lb.Id, "recuento-linea-no-encontrada", "una línea de otro recuento"),
        ];

        foreach ((Guid recuento, Guid linea, string codigo, string cual) in casos)
        {
            using HttpResponseMessage conteo = await escena.ContarAsync(recuento, linea, etiqueta, 1m);
            using HttpResponseMessage quitada = await EscenaDeRecuento.QuitarAsync(escena.Cliente, recuento, linea, etiqueta);

            await EscenaDeRecuento.ExigirElProblemaAsync(conteo, HttpStatusCode.NotFound, codigo, $"contar {cual}");
            await EscenaDeRecuento.ExigirElProblemaAsync(quitada, HttpStatusCode.NotFound, codigo, $"quitar {cual}");
        }

        AnadirLineaDeRecuentoDto nueva = new(de.UbicacionA, de.ArticuloId, null, null, null);

        using (HttpResponseMessage enNinguno = await AnadirAsync(escena, ninguno, nueva))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                enNinguno, HttpStatusCode.NotFound, "recuento-no-encontrado", "añadir a un recuento que no existe");
        }

        (HttpClient ajena, EmpresaDto _) = await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(782));
        _clientes.Add(ajena);

        using (HttpResponseMessage conteo = await EscenaDeRecuento.ContarAsync(ajena, deA.Id, la.Id, etiqueta, 1m))
        using (HttpResponseMessage quitada = await EscenaDeRecuento.QuitarAsync(ajena, deA.Id, la.Id, etiqueta))
        using (HttpResponseMessage anadida = await EscenaDeRecuento.AnadirAsync(ajena, deA.Id, nueva))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                conteo, HttpStatusCode.NotFound, "recuento-no-encontrado", "contar desde otra empresa");
            await EscenaDeRecuento.ExigirElProblemaAsync(
                quitada, HttpStatusCode.NotFound, "recuento-no-encontrado", "quitar desde otra empresa");
            await EscenaDeRecuento.ExigirElProblemaAsync(
                anadida, HttpStatusCode.NotFound, "recuento-no-encontrado", "añadir desde otra empresa");
        }

        (await escena.Cliente.EtiquetaDeAsync(ruta)).ShouldBe(etiqueta, "ningún 404 escribe la línea");

        RecuentoDto ficha = await escena.FichaAsync(deA.Id);

        ficha.Lineas.ShouldBe(1);
        ficha.LineasSinContar.ShouldBe(1);
    }

    /// <summary>
    /// Añadir una clave la deja sin contar, con el teórico de su clave y el coste que se dijo, y
    /// cambia la huella; una precargada que se quita vuelve como añadida y con el número siguiente.
    /// </summary>
    [Fact]
    public async Task Anadir_una_clave_la_deja_sin_contar_con_su_teorico_y_el_numero_siguiente()
    {
        EscenaDeRecuento escena = await MontarAsync(783, "RCL-A", "PorLote");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m, lote: "L-1");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto precargada = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        string cabecera = $"{EscenaDeRecuento.Recuentos}/{abierto.Id}";
        string deLaCabecera = await escena.Cliente.EtiquetaDeAsync(cabecera);
        RecuentoDto antes = await escena.FichaAsync(abierto.Id);

        LineaDeRecuentoDto nueva;

        using (HttpResponseMessage alta =
            await AnadirAsync(escena, abierto.Id, new(de.UbicacionA, de.ArticuloId, "L-2", null, 1.5m)))
        {
            alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
            nueva = (await alta.Content.ReadFromJsonAsync<LineaDeRecuentoDto>())!;

            alta.Headers.Location.ShouldNotBeNull().ToString().ShouldEndWith(
                EscenaDeRecuento.RutaDeLaLinea(abierto.Id, nueva.Id), Case.Insensitive);
        }

        nueva.Numero.ShouldBe(2);
        nueva.Origen.ShouldBe("Anadida");
        nueva.CodigoDeLote.ShouldBe("L-2");
        nueva.UnidadBaseId.ShouldBe(de.UnidadId, "se cuenta en la unidad base de su artículo");
        nueva.CosteUnitario.ShouldBe(1.5m);
        nueva.Contado.ShouldBeNull("añadir no es contar: una línea sin contar no es un cero");
        nueva.Teorico.ShouldBe(0m, "la clave no tiene nada en el libro");
        nueva.Diferencia.ShouldBeNull();

        (await escena.LineaAsync(abierto.Id, nueva.Id)).ShouldBe(nueva, "la que devuelve el alta es la que se lee");

        RecuentoDto conLaNueva = await escena.FichaAsync(abierto.Id);

        conLaNueva.Lineas.ShouldBe(2);
        conLaNueva.LineasSinContar.ShouldBe(2);
        conLaNueva.HuellaDelTeorico.ShouldNotBe(antes.HuellaDelTeorico, "una línea más es otro teórico");
        (await escena.Cliente.EtiquetaDeAsync(cabecera)).ShouldNotBe(deLaCabecera, "añadir toca la cabecera");

        // QUITAR LA PRECARGADA Y AÑADIRLA OTRA VEZ: vuelve como añadida, con el teórico de su clave y
        // con el mayor número más uno, y no con el que dejó libre.
        string dePrecargada =
            await escena.Cliente.EtiquetaDeAsync(EscenaDeRecuento.RutaDeLaLinea(abierto.Id, precargada.Id));

        using (HttpResponseMessage quitada =
            await EscenaDeRecuento.QuitarAsync(escena.Cliente, abierto.Id, precargada.Id, dePrecargada))
        {
            quitada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(quitada));
        }

        using HttpResponseMessage otraVez =
            await AnadirAsync(escena, abierto.Id, new(de.UbicacionA, de.ArticuloId, "L-1", null, null));

        otraVez.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(otraVez));
        LineaDeRecuentoDto devuelta = (await otraVez.Content.ReadFromJsonAsync<LineaDeRecuentoDto>())!;

        devuelta.Numero.ShouldBe(3);
        devuelta.Origen.ShouldBe("Anadida");
        devuelta.Teorico.ShouldBe(3m);
        devuelta.CosteUnitario.ShouldBeNull("entra al precio medio de su clave");

        (await escena.TodasLasLineasAsync(abierto.Id)).Select(linea => linea.Numero).ShouldBe([2, 3]);
    }

    /// <summary>
    /// Añadir rechaza el coste, la forma de los códigos, los maestros, la marca y la clave repetida,
    /// cada uno con su <c>type</c>, y ningún rechazo escribe nada.
    /// </summary>
    [Fact]
    public async Task Anadir_rechaza_lo_que_no_puede_contar_y_no_escribe_nada()
    {
        EscenaDeRecuento escena = await MontarAsync(784, "RCL-G", "PorLote");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m, lote: "L-1");

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);

        UbicacionDto bloqueada =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, de.AlmacenA, "RCL-G-A2");

        using (HttpResponseMessage bloqueo =
            await escena.Cliente.SuprimirAsync($"{LosMaestrosPorLaApi.Ubicaciones}/{bloqueada.Id}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        string cabecera = $"{EscenaDeRecuento.Recuentos}/{abierto.Id}";
        string deLaCabecera = await escena.Cliente.EtiquetaDeAsync(cabecera);

        AnadirLineaDeRecuentoDto bien = new(de.UbicacionA, de.ArticuloId, "L-2", null, 2m);

        // EL TOPE DEL COSTE ES EL DE LA COLUMNA: `numeric(18,4)` deja catorce cifras enteras.
        (AnadirLineaDeRecuentoDto Peticion, HttpStatusCode Estado, string Codigo, string Cual)[] rechazos =
        [
            (bien with { CosteUnitario = -0.01m }, HttpStatusCode.BadRequest, "recuento-coste-no-valido", "coste negativo"),
            (bien with { CosteUnitario = 100_000_000_000_000m }, HttpStatusCode.BadRequest, "recuento-coste-no-valido", "coste de quince cifras"),
            (bien with { CodigoDeLote = "L 2" }, HttpStatusCode.BadRequest, "recuento-lote-no-valido", "lote con un espacio"),
            (bien with { CodigoDeLote = null, NumeroDeSerie = "S 1" }, HttpStatusCode.BadRequest, "recuento-numero-de-serie-no-valido", "serie con un espacio"),
            (bien with { UbicacionId = Guid.NewGuid() }, HttpStatusCode.BadRequest, "recuento-ubicacion-no-encontrada", "ubicación que no existe"),
            (bien with { UbicacionId = de.UbicacionB }, HttpStatusCode.BadRequest, "recuento-ubicacion-no-encontrada", "ubicación de otro almacén"),
            (bien with { UbicacionId = bloqueada.Id }, HttpStatusCode.Conflict, "recuento-ubicacion-bloqueada", "ubicación bloqueada"),
            (bien with { ArticuloId = Guid.NewGuid() }, HttpStatusCode.BadRequest, "recuento-articulo-no-encontrado", "artículo que no existe"),
            (bien with { CodigoDeLote = null }, HttpStatusCode.Conflict, "recuento-trazabilidad-no-casa", "sin el lote que pide la marca"),
            (bien with { NumeroDeSerie = "S-1" }, HttpStatusCode.Conflict, "recuento-trazabilidad-no-casa", "con lote y serie a la vez"),
            (bien with { CodigoDeLote = "L-1", CosteUnitario = null }, HttpStatusCode.Conflict, "recuento-clave-repetida", "una clave que ya lleva"),
        ];

        foreach ((AnadirLineaDeRecuentoDto peticion, HttpStatusCode estado, string codigo, string cual) in rechazos)
        {
            using HttpResponseMessage rechazo = await AnadirAsync(escena, abierto.Id, peticion);

            await EscenaDeRecuento.ExigirElProblemaAsync(rechazo, estado, codigo, cual);
        }

        (await escena.Cliente.EtiquetaDeAsync(cabecera)).ShouldBe(deLaCabecera, "ningún rechazo toca la cabecera");
        (await escena.FichaAsync(abierto.Id)).Lineas.ShouldBe(1);

        // EL MISMO ALTA CON LO QUE VALE SALE BIEN, así que cada rechazo es por lo suyo.
        using (HttpResponseMessage elBueno = await AnadirAsync(escena, abierto.Id, bien))
        {
            elBueno.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(elBueno));
        }

        // Y UN ARTÍCULO QUE NO SE ALMACENA: otro, porque el de la escena ya tiene movimientos y su
        // tipo no se cambia. Con la unidad y el tramo de la escena, que no gastan otra semilla.
        Guid servicio = await UnServicioAsync(escena);

        using HttpResponseMessage deServicio = await AnadirAsync(
            escena, abierto.Id, bien with { ArticuloId = servicio, CodigoDeLote = null, CosteUnitario = null });

        await EscenaDeRecuento.ExigirElProblemaAsync(
            deServicio, HttpStatusCode.Conflict, "recuento-articulo-no-se-almacena", "un artículo que es un servicio");
    }

    /// <summary>
    /// Quitar una línea exige su versión: con la de antes de contarla es un <c>412</c>, sin ninguna un
    /// <c>428</c>, y con la de ahora la quita, y la línea ya no existe.
    /// </summary>
    [Fact]
    public async Task Quitar_una_linea_exige_su_version_y_despues_ya_no_existe()
    {
        EscenaDeRecuento escena = await MontarAsync(785, "RCL-Q");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        string ruta = EscenaDeRecuento.RutaDeLaLinea(abierto.Id, linea.Id);
        string antesDeContar = await escena.Cliente.EtiquetaDeAsync(ruta);

        using (HttpResponseMessage conteo = await escena.ContarAsync(abierto.Id, linea.Id, antesDeContar, 3m))
        {
            conteo.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(conteo));
        }

        string deAhora = await escena.Cliente.EtiquetaDeAsync(ruta);

        // QUIEN LA QUITA LA HA VISTO: si otro la ha contado después, no se lleva su conteo sin verlo.
        using (HttpResponseMessage vieja =
            await EscenaDeRecuento.QuitarAsync(escena.Cliente, abierto.Id, linea.Id, antesDeContar))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                vieja, HttpStatusCode.PreconditionFailed, "version-obsoleta", "quitar con la versión de antes de contar");
        }

        using (HttpResponseMessage sinVersion =
            await EscenaDeRecuento.QuitarAsync(escena.Cliente, abierto.Id, linea.Id, null))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                sinVersion, HttpStatusCode.PreconditionRequired, "falta-if-match", "quitar sin If-Match");
        }

        (await escena.LineaAsync(abierto.Id, linea.Id)).Contado.ShouldBe(3m, "ni el 412 ni el 428 la quitan");

        string cabecera = $"{EscenaDeRecuento.Recuentos}/{abierto.Id}";
        string deLaCabecera = await escena.Cliente.EtiquetaDeAsync(cabecera);

        using (HttpResponseMessage quitada =
            await EscenaDeRecuento.QuitarAsync(escena.Cliente, abierto.Id, linea.Id, deAhora))
        {
            quitada.StatusCode.ShouldBe(HttpStatusCode.NoContent, await Escenario.Detalle(quitada));
        }

        (await escena.Cliente.EtiquetaDeAsync(cabecera)).ShouldNotBe(deLaCabecera, "quitar toca la cabecera");

        using (HttpResponseMessage lectura = await escena.Cliente.GetAsync(ruta))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                lectura, HttpStatusCode.NotFound, "recuento-linea-no-encontrada", "leer la línea quitada");
        }

        using (HttpResponseMessage otraVez =
            await EscenaDeRecuento.QuitarAsync(escena.Cliente, abierto.Id, linea.Id, deAhora))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                otraVez, HttpStatusCode.NotFound, "recuento-linea-no-encontrada", "quitar la línea quitada");
        }

        RecuentoDto ficha = await escena.FichaAsync(abierto.Id);

        ficha.Lineas.ShouldBe(0);
        ficha.LineasSinContar.ShouldBe(0, "la que se quita no se cuenta, ni como cero");
    }

    /// <summary>
    /// Un recuento que ya no está en curso no admite ninguna de las tres escrituras, y no se escribe
    /// nada.
    /// </summary>
    /// <remarks>
    /// El estado se pone por debajo del ORM porque el descarte y la confirmación todavía no tienen
    /// borde. El caso no depende de cuál de los tres estados sea: los tres paran en la misma guarda.
    /// </remarks>
    [Fact]
    public async Task Un_recuento_que_no_esta_en_curso_no_admite_escrituras_en_sus_lineas()
    {
        EscenaDeRecuento escena = await MontarAsync(786, "RCL-E");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        LineaDeRecuentoDto linea = (await escena.TodasLasLineasAsync(abierto.Id)).ShouldHaveSingleItem();

        string ruta = EscenaDeRecuento.RutaDeLaLinea(abierto.Id, linea.Id);
        string etiqueta = await escena.Cliente.EtiquetaDeAsync(ruta);

        await DescartarPorDebajoAsync(abierto.Id);

        using (HttpResponseMessage conteo = await escena.ContarAsync(abierto.Id, linea.Id, etiqueta, 3m))
        using (HttpResponseMessage quitada = await EscenaDeRecuento.QuitarAsync(escena.Cliente, abierto.Id, linea.Id, etiqueta))
        using (HttpResponseMessage anadida =
            await AnadirAsync(escena, abierto.Id, new(de.UbicacionB, de.ArticuloId, null, null, null)))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                conteo, HttpStatusCode.Conflict, "recuento-no-esta-en-curso", "contar en un descartado");
            await EscenaDeRecuento.ExigirElProblemaAsync(
                quitada, HttpStatusCode.Conflict, "recuento-no-esta-en-curso", "quitar en un descartado");
            await EscenaDeRecuento.ExigirElProblemaAsync(
                anadida, HttpStatusCode.Conflict, "recuento-no-esta-en-curso", "añadir a un descartado");
        }

        (await escena.Cliente.EtiquetaDeAsync(ruta)).ShouldBe(etiqueta, "ningún 409 escribe la línea");
        (await escena.FichaAsync(abierto.Id)).Lineas.ShouldBe(1);
    }

    /// <summary>
    /// Añadir con la misma <c>Idempotency-Key</c> devuelve la misma línea y no la añade dos veces; el
    /// reintento sin clave lo para la clave repetida.
    /// </summary>
    [Fact]
    public async Task Anadir_con_la_misma_clave_de_idempotencia_devuelve_la_misma_linea_y_escribe_una()
    {
        EscenaDeRecuento escena = await MontarAsync(787, "RCL-I");
        EscenaDeTransferencia de = escena.Escena;

        await escena.EntrarAsync(postgres, de.AlmacenA, de.UbicacionA, 3m);

        RecuentoDto abierto = await escena.AbrirAsync(de.AlmacenA);
        UbicacionDto otroHueco =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(escena.Cliente, de.AlmacenA, "RCL-I-A2");

        AnadirLineaDeRecuentoDto peticion = new(otroHueco.Id, de.ArticuloId, null, null, null);
        string clave = Guid.NewGuid().ToString();

        LineaDeRecuentoDto primera;

        using (HttpResponseMessage alta = await EscenaDeRecuento.AnadirAsync(escena.Cliente, abierto.Id, peticion, clave))
        {
            alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));
            primera = (await alta.Content.ReadFromJsonAsync<LineaDeRecuentoDto>())!;
        }

        using (HttpResponseMessage repetida = await EscenaDeRecuento.AnadirAsync(escena.Cliente, abierto.Id, peticion, clave))
        {
            repetida.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(repetida));
            repetida.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
            (await repetida.Content.ReadFromJsonAsync<LineaDeRecuentoDto>())!.Id.ShouldBe(primera.Id);
        }

        using (HttpResponseMessage sinClave = await AnadirAsync(escena, abierto.Id, peticion))
        {
            await EscenaDeRecuento.ExigirElProblemaAsync(
                sinClave, HttpStatusCode.Conflict, "recuento-clave-repetida", "el reintento sin clave");
        }

        (await escena.TodasLasLineasAsync(abierto.Id)).Count.ShouldBe(2, "la precargada y la añadida una vez");
    }

    private static Task<HttpResponseMessage> AnadirAsync(
        EscenaDeRecuento escena, Guid recuentoId, AnadirLineaDeRecuentoDto peticion) =>
        EscenaDeRecuento.AnadirAsync(escena.Cliente, recuentoId, peticion);

    private static async Task<Guid> UnServicioAsync(EscenaDeRecuento escena)
    {
        EscenaDeTransferencia de = escena.Escena;
        ArticuloDto delaEscena =
            (await escena.Cliente.GetFromJsonAsync<ArticuloDto>($"{LosMaestrosPorLaApi.Articulos}/{de.ArticuloId}"))!;

        using HttpResponseMessage alta = await escena.Cliente.PostAsJsonAsync(
            LosMaestrosPorLaApi.Articulos,
            new CrearArticuloDto
            {
                Codigo = "SRV-" + delaEscena.Codigo,
                Descripcion = "Un servicio, que no se almacena",
                Tipo = "Servicio",
                Trazabilidad = "Ninguna",
                UnidadBaseId = de.UnidadId,
                ImpuestoPorDefectoId = delaEscena.ImpuestoPorDefectoId,
                CategoriaId = null,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ArticuloDto>())!.Id;
    }

    // EL DESCARTE POR DEBAJO DEL ORM, con su motivo: la restricción de la tabla solo pide que no lleve
    // número, y un descartado no lo lleva.
    private async Task DescartarPorDebajoAsync(Guid recuentoId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            "UPDATE inventario.recuentos SET estado = 'Descartado', " +
            "motivo_del_descarte = 'Se descarta por debajo para el caso' WHERE id = @id",
            conexion);
        orden.Parameters.AddWithValue("id", recuentoId);

        (await orden.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    private async Task<EscenaDeRecuento> MontarAsync(int semilla, string codigo, string trazabilidad = "Ninguna")
    {
        EscenaDeRecuento escena = await EscenaDeRecuento.MontarAsync(_api, semilla, codigo, trazabilidad);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
