using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Contracts.Sesiones;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Valoraciones;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La valoración del ajuste contra la base de verdad (ADR-0046): la divisa del documento, el coste
/// que admite cada línea y lo que el precio medio deja en el libro.
/// </summary>
/// <remarks>
/// <para>
/// <b>La divisa sale de la empresa, y el caso lo ve con una empresa en dólares.</b> Con una en
/// euros, un documento que escribiera <c>EUR</c> a fuego saldría verde, y la pregunta al puerto no
/// estaría comprobada por nadie.
/// </para>
/// <para>
/// <b>Los importes se eligen para que el redondeo se vea.</b> Con precios redondos, un precio medio
/// que se redondeara dos veces, o ninguna, daría lo mismo que el bueno. Aquí 11 entre 7 no cabe en
/// seis decimales, y 100 entre 300 tampoco, así que cada valor afirmado dice por dónde pasó.
/// </para>
/// <para>
/// <b>Las confirmaciones van por el caso de uso</b>, con su cerrojo y su transacción, salvo en dos
/// casos: el de las dos empresas y el del cuadre. Esos necesitan la MISMA clave en dos empresas, o
/// escribir por debajo del sistema, y los identificadores de la API no se pueden repetir; van por el
/// repositorio de verdad, con identificadores inventados, igual que <see cref="ElLibro"/>.
/// </para>
/// <para>
/// <b>Semillas: el fichero entero es del 500 al 519.</b> Las empresas, del 500 al 509; los maestros
/// de instalación, del 510 al 519. Este carril comparte la base entre todos sus ficheros, así que
/// una semilla repetida no falla aquí: falla en el fichero de otro que la pedía primero. Del 500 y el
/// 501 son los casos de la divisa; del 502 al 508, los del precio medio, con sus maestros del 511 al
/// 518.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaValoracionDelAjusteTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string RutaDeEmpresas = "/api/v1/organizacion/empresas";

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
    /// Una salida con coste y un coste negativo se rechazan con el mismo <c>400</c>, y no queda
    /// ningún borrador.
    /// </summary>
    /// <remarks>
    /// <b>Los maestros son inventados, y eso también se afirma.</b> El coste se mira antes que el
    /// almacén, la serie y las líneas, porque es un cuerpo mal escrito y no hace falta preguntar a
    /// nadie para saberlo. Si se mirara después, el código sería el del almacén que no existe.
    /// </remarks>
    [Fact]
    public async Task Una_salida_con_coste_o_un_coste_negativo_no_abren_el_borrador()
    {
        (_, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(Escenario.NuevaEmpresa(Escenario.NifInventado(500)));

        await using (ElModuloDeInventario modulo = new(postgres, empresa.Id))
        {
            foreach ((decimal cantidad, decimal coste) in new[] { (-2m, 1.50m), (2m, -1.50m) })
            {
                Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
                    new AbrirAjusteDto(
                        Guid.CreateVersion7(),
                        Guid.CreateVersion7(),
                        DateOnly.FromDateTime(DateTime.UtcNow),
                        "Ajuste que no debería pasar del coste",
                        [new LineaDeAjusteDto(
                            Guid.CreateVersion7(), Guid.CreateVersion7(), cantidad, Guid.CreateVersion7(), 1m, coste)]),
                    CancellationToken.None);

                alta.Error.ShouldNotBeNull($"{cantidad} a {coste} no tendría que abrir un borrador");
                alta.Error.Codigo.ShouldBe("ajuste-coste-no-valido");
                alta.Error.Tipo.ShouldBe(TipoDeError.Validacion);
            }
        }

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresa.Id);

        (await contexto.Ajustes.CountAsync()).ShouldBe(0, "un alta rechazada no deja un borrador a medias");
    }

    /// <summary>El borrador toma la divisa base de la empresa, que aquí es el dólar.</summary>
    [Fact]
    public async Task El_borrador_toma_la_divisa_base_de_la_empresa()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(
            Escenario.NuevaEmpresa(Escenario.NifInventado(501)) with { DivisaBase = "USD" });

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, "V28-A");
        UbicacionDto ubicacion = await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, "V28-A-01");
        (Guid articuloId, Guid unidadId) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 510);
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, "V28");

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                serie.Id,
                almacen.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Recuento del ítem 2.8",
                [new LineaDeAjusteDto(ubicacion.Id, articuloId, 4m, unidadId, 2m, 1.50m)]),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");
        alta.Valor.Divisa.ShouldBe("USD", "la divisa del documento es la base de la empresa, no el euro");
    }

    /// <summary>
    /// Una entrada sin coste en una clave sin existencias es un <c>422</c>, y la confirmación no deja
    /// nada: ni libro, ni valoración, ni documento confirmado.
    /// </summary>
    /// <remarks>
    /// <b>Y la valoración tampoco, aunque el cerrojo la había creado.</b> Bloquear es crear (ADR-0046
    /// §2): la fila nace a cero antes de que el dominio diga que no se puede valorar, y es el rechazo
    /// el que la deshace con todo lo demás. Una fila que se quedara ahí no descuadraría nada, porque
    /// está a cero, pero diría que la clave se movió.
    /// </remarks>
    [Fact]
    public async Task Una_entrada_sin_coste_en_una_clave_vacia_es_422_y_no_deja_nada()
    {
        ElCaso caso = await UnCasoAsync(502, "V28-B", 511);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        Guid ajusteId = await AbrirAsync(modulo, caso, caso.Serie.Id, new Linea(0, 4m, null));

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        confirmacion.Error.ShouldNotBeNull(
            "sin existencias no hay precio medio, y sin coste no hay con qué valorar la entrada");
        confirmacion.Error.Codigo.ShouldBe("ajuste-entrada-sin-coste-ni-precio-medio");
        confirmacion.Error.Tipo.ShouldBe(TipoDeError.ReglaDeNegocio);

        await using InventarioDbContext contexto = postgres.AbrirInventario(caso.EmpresaId);

        (await contexto.Movimientos.CountAsync()).ShouldBe(0, "el rechazo no escribe el libro");
        (await contexto.Valoraciones.CountAsync()).ShouldBe(
            0, "el cerrojo creó la valoración a cero y el rechazo la deshizo con todo lo demás");
        (await contexto.Ajustes.SingleAsync()).Estado.ShouldBe(EstadoDeAjuste.Borrador);
    }

    /// <summary>
    /// Una entrada sin coste en una clave con existencias se valora a su precio medio, redondeado una
    /// sola vez, y congela el de después.
    /// </summary>
    [Fact]
    public async Task Una_entrada_sin_coste_se_valora_al_precio_medio_de_la_clave()
    {
        ElCaso caso = await UnCasoAsync(503, "V28-P", 512);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, new Linea(0, 3m, 1m));
        await ConfirmarAsync(modulo, caso, new Linea(0, 4m, 2m));
        Guid sinCoste = await ConfirmarAsync(modulo, caso, new Linea(0, 2m, null));

        MovimientoStock fila = (await FilasDeAsync(caso.EmpresaId, sinCoste)).ShouldHaveSingleItem();

        fila.Valor.ShouldBe(
            Importe.De(3.1429m, "EUR"),
            "11 entre 7 es 1,571429 con seis decimales, por 2 es 3,142858, y con cuatro 3,1429: el " +
            "precio medio se redondea una vez y el importe otra (R6)");
        fila.PrecioMedio.ShouldBe(
            PrecioUnitario.De(1.571433m, "EUR"), "el de después: 14,1429 entre 9 unidades");

        Valoracion valoracion = (await LasValoracionesAsync(caso.EmpresaId)).ShouldHaveSingleItem();

        valoracion.Cantidad.ShouldBe(9m);
        valoracion.Valor.ShouldBe(Importe.De(14.1429m, "EUR"));

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>
    /// Una salida se valora al precio medio de antes de salir, y lo congela; la que vacía la clave se
    /// lleva todo el valor, aunque el precio por la cantidad diga una diezmilésima menos.
    /// </summary>
    [Fact]
    public async Task Una_salida_congela_el_precio_medio_y_la_que_vacia_se_lleva_todo_el_valor()
    {
        ElCaso caso = await UnCasoAsync(504, "V28-S", 513);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, new Linea(0, 100m, 0.50m), new Linea(0, 200m, 0.25m));
        Guid primera = await ConfirmarAsync(modulo, caso, new Linea(0, -120m, null));
        Guid laQueVacia = await ConfirmarAsync(modulo, caso, new Linea(0, -180m, null));

        MovimientoStock deLaPrimera = (await FilasDeAsync(caso.EmpresaId, primera)).ShouldHaveSingleItem();

        deLaPrimera.PrecioMedio.ShouldBe(
            PrecioUnitario.De(0.333333m, "EUR"), "100 entre 300, el de antes de salir");
        deLaPrimera.Valor.ShouldBe(Importe.De(-40m, "EUR"), "0,333333 por 120 es 39,99996, y con cuatro 40");

        MovimientoStock deLaQueVacia = (await FilasDeAsync(caso.EmpresaId, laQueVacia)).ShouldHaveSingleItem();

        deLaQueVacia.PrecioMedio.ShouldBe(PrecioUnitario.De(0.333333m, "EUR"));
        deLaQueVacia.Valor.ShouldBe(
            Importe.De(-60m, "EUR"),
            "0,333333 por 180 es 59,9999, y la diezmilésima que faltara se quedaría en una clave sin " +
            "unidades, que es lo que la tabla prohíbe: vaciar se lleva todo");

        Valoracion valoracion = (await LasValoracionesAsync(caso.EmpresaId)).ShouldHaveSingleItem();

        valoracion.Cantidad.ShouldBe(0m);
        valoracion.Valor.ShouldBe(Importe.Cero("EUR"));

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>
    /// Dos confirmaciones a la vez sobre la misma clave: la segunda se valora con lo que la primera
    /// dejó, no con lo que había cuando empezó.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el caso por el que la valoración se lee bajo cerrojo</b> (ADR-0046 §2), y la mutación del
    /// ítem va contra él. La segunda es una entrada sin coste, que se valora al precio medio: sin el
    /// cerrojo leería 10 unidades a 1,00 y valoraría a 1,00; con él espera, y cuando la primera
    /// confirma lee 20 unidades por 40 y valora a 2,00. La suma de la tabla saldría bien en los dos
    /// casos, porque la sentencia que suma espera igual: lo que se equivoca es el valor de la fila.
    /// </para>
    /// <para>
    /// <b>La primera se para con la valoración bloqueada y nada escrito</b>, y confirma después de ver
    /// a la segunda esperando. Parada ya confirmada, como en la carrera de la existencia, el caso no
    /// ve el cerrojo: el <c>INSERT … ON CONFLICT</c> de la segunda espera en el índice único a la
    /// transacción que cambió la fila, bloquee lo que ya está o no. Lo midió la mutación que cambia
    /// <c>DO UPDATE</c> por <c>DO NOTHING</c>, que con aquella forma dejó el carril entero en verde.
    /// </para>
    /// <para>
    /// <b>La clave ya existe, y a propósito.</b> Con una clave nueva, las dos chocarían en el índice
    /// único al crearla, y la segunda esperaría aunque el cerrojo no bloqueara nada de lo que ya hay:
    /// el caso saldría verde con la mitad del mecanismo.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_segunda_de_dos_confirmaciones_a_la_vez_se_valora_con_lo_que_dejo_la_primera()
    {
        ElCaso caso = await UnCasoAsync(505, "V28-C", 514);

        // DOS SERIES, como en la carrera de la existencia: con una sola, las dos se pararían antes en
        // el contador, y la segunda llegaría a la valoración con la primera ya confirmada.
        SerieDto otra = await LosMaestrosPorLaApi.CrearSerieEnAsync(caso.Cliente, caso.Serie.EjercicioId, "V28-C2");

        await using ElModuloDeInventario unos = new(postgres, caso.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(unos, caso, new Linea(0, 10m, 1m));

        Guid laPrimera = await AbrirAsync(unos, caso, caso.Serie.Id, new Linea(0, 10m, 3m));
        Guid laSegunda = await AbrirAsync(otros, caso, otra.Id, new Linea(0, 5m, null));

        await using (IDbContextTransaction enVuelo = await unos.AbrirTransaccionAsync())
        {
            (await unos.BloquearLasValoracionesAsync(
                    [new ClaveDeValoracion(caso.Articulos[0].ArticuloId, caso.AlmacenId)], "EUR"))
                .ShouldHaveSingleItem().Value
                .ShouldBe(new SaldoValorado(10m, Importe.De(10m, "EUR"), Hoy));

            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(laSegunda);

            await EsperarAQueLaFreneAsync(unos.ProcesoDeLaBase, laOtra);

            Resultado<AjusteDto> confirmacion = await unos.ConfirmarSinAbrirTransaccionAsync(laPrimera);

            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            await enVuelo.CommitAsync();

            Resultado<AjusteDto> segunda = await laOtra.WaitAsync(TimeSpan.FromSeconds(30));

            segunda.EsCorrecto.ShouldBeTrue($"«{segunda.Error?.Codigo}»");
        }

        MovimientoStock fila = (await FilasDeAsync(caso.EmpresaId, laSegunda)).ShouldHaveSingleItem();

        fila.Valor.ShouldBe(
            Importe.De(10m, "EUR"),
            "5 al precio medio de lo que dejó la primera, 40 entre 20; a 1,00 serían 5, que es el " +
            "precio de antes de que la primera confirmara");

        Valoracion valoracion = (await LasValoracionesAsync(caso.EmpresaId)).ShouldHaveSingleItem();

        valoracion.Cantidad.ShouldBe(25m);
        valoracion.Valor.ShouldBe(Importe.De(50m, "EUR"));

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>
    /// Con existencias valoradas en euros, un documento en dólares de la misma clave es un
    /// <c>422</c>; la clave que se había vaciado empieza de nuevo en dólares.
    /// </summary>
    /// <remarks>
    /// <b>La divisa cambia porque cambia la de la empresa</b>, que es la única forma de que un
    /// documento hable otra: su divisa es la base de la empresa al abrirlo. Las filas en euros de la
    /// clave que vuelve suman cero en cantidad y en valor, así que el cuadre las junta con las de
    /// dólares sin convertir nada.
    /// </remarks>
    [Fact]
    public async Task Un_documento_en_otra_divisa_que_la_de_la_valoracion_es_422_salvo_en_una_clave_vacia()
    {
        ElCaso caso = await UnCasoAsync(506, "V28-D", 515, 516);

        await using (ElModuloDeInventario enEuros = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(enEuros, caso, new Linea(0, 5m, 1m));
            await ConfirmarAsync(enEuros, caso, new Linea(1, 2m, 1m));
            await ConfirmarAsync(enEuros, caso, new Linea(1, -2m, null));
        }

        using HttpResponseMessage cambio = await caso.Cliente.ModificarAsync(
            $"{RutaDeEmpresas}/{caso.EmpresaId}",
            new ModificarEmpresaDto
            {
                RazonSocial = "Empresa que pasa a llevar las cuentas en dólares",
                DomicilioFiscal = Escenario.Domicilio(),
                DivisaBase = "USD",
                RegimenDeIva = "General",
            });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));

        await using ElModuloDeInventario enDolares = new(postgres, caso.EmpresaId);

        Guid laQueVuelve = await ConfirmarAsync(enDolares, caso, new Linea(1, 3m, 2m));
        Guid laQueNo = await AbrirAsync(enDolares, caso, caso.Serie.Id, new Linea(0, 1m, 2m));

        Resultado<AjusteDto> rechazo = await enDolares.ConfirmarAsync(laQueNo);

        rechazo.Error.ShouldNotBeNull("sumar dólares a euros pediría un tipo de cambio con fecha");
        rechazo.Error.Codigo.ShouldBe("ajuste-valoracion-en-otra-divisa");
        rechazo.Error.Tipo.ShouldBe(TipoDeError.ReglaDeNegocio);

        (await FilasDeAsync(caso.EmpresaId, laQueNo)).ShouldBeEmpty();
        (await FilasDeAsync(caso.EmpresaId, laQueVuelve)).ShouldHaveSingleItem().Valor
            .ShouldBe(Importe.De(6m, "USD"));

        IReadOnlyList<Valoracion> valoraciones = await LasValoracionesAsync(caso.EmpresaId);

        valoraciones.Single(fila => fila.ArticuloId == caso.Articulos[0].ArticuloId).Saldo
            .ShouldBe(new SaldoValorado(5m, Importe.De(5m, "EUR"), Hoy), "la de euros sigue como estaba");
        valoraciones.Single(fila => fila.ArticuloId == caso.Articulos[1].ArticuloId).Saldo
            .ShouldBe(new SaldoValorado(3m, Importe.De(6m, "USD"), Hoy), "la vacía ha empezado en dólares");

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 2);
    }

    /// <summary>
    /// El inverso resta el valor que sumó la entrada que anula, y no lo que valen hoy sus unidades.
    /// </summary>
    /// <remarks>
    /// <b>Entre la entrada y su anulación hay una salida</b>, y el precio medio ya no es el de la
    /// entrada. Al precio medio de hoy, 4,00, el inverso restaría 40 y dejaría 20 en cinco unidades
    /// que costaron 15; restando los 50 que la entrada sumó, deja 10, que es lo que valdrían si la
    /// entrada no hubiera existido y la salida se hubiera valorado igual.
    /// </remarks>
    [Fact]
    public async Task El_inverso_resta_el_valor_que_sumo_la_entrada_y_el_par_suma_cero()
    {
        ElCaso caso = await UnCasoAsync(507, "V28-I", 517);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, new Linea(0, 10m, 3m));
        Guid laQueSeAnula = await ConfirmarAsync(modulo, caso, new Linea(0, 10m, 5m));
        await ConfirmarAsync(modulo, caso, new Linea(0, -5m, null));

        Resultado<AnulacionDto> anulacion =
            await modulo.AnularAsync(laQueSeAnula, "El proveedor facturó a otro precio");

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»");

        MovimientoStock original = (await FilasDeAsync(caso.EmpresaId, laQueSeAnula)).ShouldHaveSingleItem();
        MovimientoStock inversa =
            (await FilasDeAsync(caso.EmpresaId, anulacion.Valor.Inverso.Id)).ShouldHaveSingleItem();

        inversa.Valor.ShouldBe(Importe.De(-50m, "EUR"), "resta lo que la entrada sumó");
        inversa.PrecioMedio.ShouldBe(
            PrecioUnitario.De(4m, "EUR"), "como toda salida, congela el precio medio de antes de salir");
        (original.Valor + inversa.Valor).ShouldBe(Importe.Cero("EUR"), "el par suma cero en valor");

        Valoracion valoracion = (await LasValoracionesAsync(caso.EmpresaId)).ShouldHaveSingleItem();

        valoracion.Saldo.ShouldBe(new SaldoValorado(5m, Importe.De(10m, "EUR"), Hoy));

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>
    /// Las líneas se valoran en el orden en que se escribieron, aunque la base devuelva sus filas en
    /// otro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El orden decide el valor</b>, y no solo el precio que se congela: con 10 unidades a 1,00,
    /// una entrada de 10 a 3,00 seguida de una de 5 sin coste valora esa a 2,00 y deja 50; al revés,
    /// la de 5 entra a 1,00 y deja 45. Por eso cada línea guarda su número (ADR-0046 §3).
    /// </para>
    /// <para>
    /// <b>El caso desordena la base antes de confirmar</b>, y lo comprueba. La primera línea se
    /// reescribe sin cambiar nada, y su versión nueva queda guardada detrás de la segunda; y la
    /// confirmación lee sin índices, con otro contexto que no la tiene en memoria. Sin eso, la base
    /// devolvería las líneas en el orden bueno por casualidad —el índice único de
    /// <c>(ajuste_id, numero)</c> ya las da ordenadas— y el caso no vería un agregado que no las
    /// ordenara.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Las_lineas_se_valoran_en_el_orden_en_que_se_escribieron_aunque_la_base_las_devuelva_en_otro()
    {
        ElCaso caso = await UnCasoAsync(508, "V28-O", 518);

        Guid ajusteId;

        await using (ElModuloDeInventario quienEscribe = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(quienEscribe, caso, new Linea(0, 10m, 1m));
            ajusteId = await AbrirAsync(
                quienEscribe, caso, caso.Serie.Id, new Linea(0, 10m, 3m), new Linea(0, 5m, null));
        }

        (await ElLibro.EscalarAsync<int>(
            postgres,
            string.Create(
                CultureInfo.InvariantCulture,
                $"UPDATE inventario.lineas_ajuste SET numero = numero WHERE ajuste_id = '{ajusteId}' AND numero = 1 RETURNING numero")))
            .ShouldBe(1);

        (await ElOrdenGuardadoAsync(ajusteId)).ShouldBe(
            "2,1", "sin índices, la base tiene que devolver la segunda línea antes que la primera");

        await using (ElModuloDeInventario quienConfirma = new(postgres, caso.EmpresaId))
        {
            await using IDbContextTransaction transaccion = await quienConfirma.AbrirTransaccionAsync();

            await quienConfirma.LeerSinIndicesAsync();

            Resultado<AjusteDto> confirmacion = await quienConfirma.ConfirmarSinAbrirTransaccionAsync(ajusteId);

            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            await transaccion.CommitAsync();
        }

        IReadOnlyList<MovimientoStock> filas = await FilasDeAsync(caso.EmpresaId, ajusteId);

        filas.Single(fila => fila.CantidadEnUnidadBase == 10m).Valor.ShouldBe(Importe.De(30m, "EUR"));
        filas.Single(fila => fila.CantidadEnUnidadBase == 5m).Valor.ShouldBe(
            Importe.De(10m, "EUR"),
            "5 al precio medio que dejó la primera línea, 40 entre 20; a 1,00 serían 5, que es lo que " +
            "valdría si se valorara en el orden en que la base devolvió las filas");

        (await LasValoracionesAsync(caso.EmpresaId)).ShouldHaveSingleItem().Saldo
            .ShouldBe(new SaldoValorado(25m, Importe.De(50m, "EUR"), Hoy));

        await ExigirQueCuadraAsync(caso.EmpresaId, valoraciones: 1);
    }

    /// <summary>
    /// Dos empresas con el mismo artículo y el mismo almacén no comparten valoración: cada una se
    /// valora con su precio medio.
    /// </summary>
    /// <remarks>
    /// <b>Las sentencias de la valoración son SQL crudo</b>, el filtro global no las alcanza, y cada
    /// una compara la empresa ella misma. Con claves distintas, una comparación que faltara no se
    /// vería: por eso las dos empresas tienen EXACTAMENTE la misma clave, que solo se puede tener
    /// con identificadores inventados. Si la lectura se llevara las dos filas, el recuento del
    /// cerrojo reventaría; si la suma tocara las dos, reventaría el suyo; y si el precio medio fuera
    /// el de las dos juntas, 40 entre 14, la salida de A no valdría 10.
    /// </remarks>
    [Fact]
    public async Task Dos_empresas_con_la_misma_clave_no_comparten_valoracion()
    {
        var deA = Guid.CreateVersion7();
        var deB = Guid.CreateVersion7();
        var almacenId = Guid.CreateVersion7();
        var ubicacionId = Guid.CreateVersion7();
        var articuloId = Guid.CreateVersion7();

        await ConfirmarSinLaApiAsync(deA, almacenId, ubicacionId, articuloId, 10m, 2m);
        await ConfirmarSinLaApiAsync(deB, almacenId, ubicacionId, articuloId, 4m, 5m);
        MovimientoStock salidaDeA = await ConfirmarSinLaApiAsync(deA, almacenId, ubicacionId, articuloId, -5m, null);
        MovimientoStock entradaDeB = await ConfirmarSinLaApiAsync(deB, almacenId, ubicacionId, articuloId, 1m, null);

        salidaDeA.Valor.ShouldBe(Importe.De(-10m, "EUR"), "5 al precio medio de A, que es 2,00");
        entradaDeB.Valor.ShouldBe(Importe.De(5m, "EUR"), "1 al precio medio de B, que es 5,00");

        (await ElLibro.EscalarAsync<long>(
            postgres,
            string.Create(
                CultureInfo.InvariantCulture,
                $"SELECT count(*) FROM inventario.valoraciones WHERE articulo_id = '{articuloId}'")))
            .ShouldBe(2, "una fila por empresa: la empresa es parte de la clave");

        (await LasValoracionesAsync(deA)).ShouldHaveSingleItem().Saldo
            .ShouldBe(new SaldoValorado(5m, Importe.De(10m, "EUR"), Hoy));
        (await LasValoracionesAsync(deB)).ShouldHaveSingleItem().Saldo
            .ShouldBe(new SaldoValorado(5m, Importe.De(25m, "EUR"), Hoy));

        await ExigirQueCuadraAsync(deA, valoraciones: 1);
        await ExigirQueCuadraAsync(deB, valoraciones: 1);
    }

    /// <summary>El cuadre encuentra cada valoración que no dice lo que el libro, en sus dos columnas.</summary>
    /// <remarks>
    /// <b>El arnés del cuadre</b>: un cuadre que no mirara la valoración saldría limpio en todos los
    /// demás casos de este carril, igual que uno que sí. Las cuatro mentiras van en una transacción
    /// que se deshace, y el cuadre corre dentro de ella.
    /// </remarks>
    [Fact]
    public async Task El_cuadre_encuentra_cada_valoracion_que_no_dice_lo_que_el_libro()
    {
        var empresaId = Guid.CreateVersion7();
        var almacenId = Guid.CreateVersion7();
        var ubicacionId = Guid.CreateVersion7();
        var queVale = Guid.CreateVersion7();
        var queCuenta = Guid.CreateVersion7();
        var queFalta = Guid.CreateVersion7();
        var queSobra = Guid.CreateVersion7();

        await ConfirmarSinLaApiAsync(empresaId, almacenId, ubicacionId, queVale, 6m, 1.25m);
        await ConfirmarSinLaApiAsync(empresaId, almacenId, ubicacionId, queCuenta, 4m, 2m);
        await ConfirmarSinLaApiAsync(empresaId, almacenId, ubicacionId, queFalta, 2m, 3m);

        await ExigirQueCuadraAsync(empresaId, valoraciones: 3);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction estropeando = await contexto.Database.BeginTransactionAsync();

        // Una que vale de más.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.valoraciones SET valor = valor + 1 WHERE empresa_id = {0} AND articulo_id = {1}",
            empresaId,
            queVale)).ShouldBe(1);

        // Una que cuenta de más.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.valoraciones SET cantidad = cantidad + 2 WHERE empresa_id = {0} AND articulo_id = {1}",
            empresaId,
            queCuenta)).ShouldBe(1);

        // Una que falta, y descuadra en las dos columnas.
        (await contexto.Database.ExecuteSqlRawAsync(
            "DELETE FROM inventario.valoraciones WHERE empresa_id = {0} AND articulo_id = {1}",
            empresaId,
            queFalta)).ShouldBe(1);

        // Y una que sobra: unidades que el libro no ha movido nunca.
        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.valoraciones (empresa_id, articulo_id, almacen_id, cantidad, valor, divisa) " +
            "VALUES ({0}, {1}, {2}, 3, 0, 'EUR')",
            empresaId,
            queSobra,
            almacenId)).ShouldBe(1);

        CuadreDeLasExistencias sucio = await LasExistencias.CuadrarEnAsync(contexto, empresaId);

        sucio.ValoracionesComparadas.ShouldBe(4, "las tres del libro y la que sobra");

        sucio.Descuadres.ShouldAllBe(descuadre =>
            descuadre.AlmacenId == almacenId && descuadre.UbicacionId == null && descuadre.Mes == null);

        sucio.Descuadres
            .Select(descuadre => (descuadre.Que, descuadre.ArticuloId, descuadre.Esperado, descuadre.Guardado, descuadre.Filas))
            .ShouldBe(
                [
                    ("valoracion-valor", queVale, 7.5m, 8.5m, 1L),
                    ("valoracion-cantidad", queCuenta, 4m, 6m, 1L),
                    ("valoracion-cantidad", queFalta, 2m, 0m, 0L),
                    ("valoracion-valor", queFalta, 6m, 0m, 0L),
                    ("valoracion-cantidad", queSobra, 0m, 3m, 1L),
                ],
                ignoreOrder: true,
                customMessage: "las cinco, y ninguna más: cada una dice qué esperaba y qué encontró");

        await estropeando.RollbackAsync();

        await ExigirQueCuadraAsync(empresaId, valoraciones: 3);
    }

    /// <summary>Da de alta una empresa con ese cuerpo y deja un cliente operando dentro de ella.</summary>
    /// <param name="alta">El cuerpo del alta.</param>
    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(CrearEmpresaDto alta)
    {
        (HttpClient cliente, SesionDto sesion) = await _api.AbrirComoAdministradorAsync();
        _clientes.Add(cliente);

        HttpResponseMessage respuesta = await cliente.PostAsJsonAsync(RutaDeEmpresas, alta);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Created);
        EmpresaDto empresa = (await respuesta.Content.ReadFromJsonAsync<EmpresaDto>())!;

        await Escenario.EntrarEnAsync(cliente, sesion.UsuarioId, empresa.Id);

        return (cliente, empresa);
    }

    /// <summary>
    /// Una empresa en euros con un almacén de una ubicación, sus artículos y una serie del ejercicio
    /// de este año.
    /// </summary>
    /// <param name="semilla">La empresa del caso.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="articulos">El número de instalación de cada artículo.</param>
    /// <returns>Los maestros del caso.</returns>
    private async Task<ElCaso> UnCasoAsync(int semilla, string codigo, params int[] articulos)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await EnUnaEmpresaNuevaAsync(Escenario.NuevaEmpresa(Escenario.NifInventado(semilla)));

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);
        UbicacionDto ubicacion = await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01");

        List<(Guid ArticuloId, Guid UnidadId)> deInstalacion = [];

        foreach (int numero in articulos)
        {
            deInstalacion.Add(await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, numero));
        }

        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, codigo);

        return new ElCaso(cliente, empresa.Id, almacen.Id, ubicacion.Id, deInstalacion, serie);
    }

    /// <summary>Abre un borrador con fecha de hoy.</summary>
    /// <param name="modulo">El módulo cableado a mano de la empresa.</param>
    /// <param name="caso">Los maestros del caso.</param>
    /// <param name="serieId">La serie que lo numerará.</param>
    /// <param name="lineas">Las líneas.</param>
    /// <returns>El borrador.</returns>
    private static async Task<Guid> AbrirAsync(
        ElModuloDeInventario modulo, ElCaso caso, Guid serieId, params Linea[] lineas)
    {
        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                serieId,
                caso.AlmacenId,
                Hoy,
                "Recuento del ítem 2.8",
                [.. lineas.Select(linea => new LineaDeAjusteDto(
                    caso.UbicacionId,
                    caso.Articulos[linea.Articulo].ArticuloId,
                    linea.Cantidad,
                    caso.Articulos[linea.Articulo].UnidadId,
                    1m,
                    linea.Coste))]),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    /// <summary>Abre y confirma un ajuste con la serie del caso, y exige que salga bien.</summary>
    /// <param name="modulo">El módulo cableado a mano de la empresa.</param>
    /// <param name="caso">Los maestros del caso.</param>
    /// <param name="lineas">Las líneas.</param>
    /// <returns>El ajuste confirmado.</returns>
    private static async Task<Guid> ConfirmarAsync(ElModuloDeInventario modulo, ElCaso caso, params Linea[] lineas)
    {
        Guid ajusteId = await AbrirAsync(modulo, caso, caso.Serie.Id, lineas);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

        return ajusteId;
    }

    /// <summary>
    /// Confirma un ajuste de una línea por el repositorio de verdad, sin la API, con los
    /// identificadores que se le den.
    /// </summary>
    /// <remarks>
    /// El mismo camino que <see cref="ElLibro"/>: el agregado, el cerrojo de la valoración, el
    /// repositorio y la unidad de trabajo, en una transacción que se confirma. Lo que se salta es el
    /// alta, que es la que pregunta a los maestros, y por eso admite claves repetidas entre empresas.
    /// </remarks>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="almacenId">El almacén.</param>
    /// <param name="ubicacionId">La ubicación.</param>
    /// <param name="articuloId">El artículo.</param>
    /// <param name="cantidad">La cantidad, en unidad base y con signo.</param>
    /// <param name="coste">El coste, o nada.</param>
    /// <returns>La fila del libro que escribió.</returns>
    private async Task<MovimientoStock> ConfirmarSinLaApiAsync(
        Guid empresaId, Guid almacenId, Guid ubicacionId, Guid articuloId, decimal cantidad, decimal? coste)
    {
        DateTimeOffset momento = DateTimeOffset.UtcNow;

        var ajuste = Ajuste.Abrir(
            empresaId, Guid.CreateVersion7(), almacenId, Hoy, "Recuento del ítem 2.8", "EUR", momento);

        ajuste.AnadirLinea(ubicacionId, articuloId, cantidad, Guid.CreateVersion7(), 1m, coste, momento);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);
        await using IDbContextTransaction transaccion = await contexto.Database.BeginTransactionAsync();

        RepositorioDeAjustes repositorio = new(contexto, new InquilinoFijo(empresaId));

        IReadOnlyList<MovimientoStock> movimientos = await ElLibro.ConfirmarBajoCerrojoAsync(
            repositorio,
            ajuste,
            1,
            new AjusteConfirmado(ajuste.Id, empresaId, almacenId, Hoy, 1),
            momento);

        repositorio.Agregar(ajuste);
        await repositorio.AnotarEnElLibroAsync(movimientos, CancellationToken.None);

        await new UnidadDeTrabajoDeInventario(contexto).ConfirmarAsync(CancellationToken.None);
        await transaccion.CommitAsync();

        return movimientos.ShouldHaveSingleItem();
    }

    /// <summary>Las filas del libro de un documento, leídas con el filtro de la empresa puesto.</summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="documentoId">El documento.</param>
    /// <returns>Sus filas.</returns>
    private async Task<IReadOnlyList<MovimientoStock>> FilasDeAsync(Guid empresaId, Guid documentoId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await contexto.Movimientos
            .AsNoTracking()
            .Where(fila => fila.DocumentoOrigenId == documentoId)
            .ToListAsync();
    }

    /// <summary>Las valoraciones de una empresa, leídas con el filtro de la empresa puesto.</summary>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>Sus valoraciones.</returns>
    private async Task<IReadOnlyList<Valoracion>> LasValoracionesAsync(Guid empresaId)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await contexto.Valoraciones.AsNoTracking().ToListAsync();
    }

    /// <summary>
    /// Cuadra la empresa y exige que no haya descuadres, después de haber comparado las valoraciones
    /// que se dicen.
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="valoraciones">Cuántas claves de valoración tenía que comparar.</param>
    private async Task ExigirQueCuadraAsync(Guid empresaId, int valoraciones)
    {
        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, empresaId);

        cuadre.ValoracionesComparadas.ShouldBe(
            valoraciones, "sin valoraciones que mirar, «no hay descuadres» sale verde por no mirar");
        cuadre.Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// Los números de las líneas de un documento en el orden en que la base las devuelve sin índices,
    /// que es el orden en que están guardadas.
    /// </summary>
    /// <param name="ajusteId">El documento.</param>
    /// <returns>Los números, separados por comas.</returns>
    private async Task<string> ElOrdenGuardadoAsync(Guid ajusteId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();
        await using NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync();

        await using (NpgsqlCommand sinIndices = new(ElModuloDeInventario.SinIndices, conexion, transaccion))
        {
            await sinIndices.ExecuteNonQueryAsync();
        }

        await using NpgsqlCommand orden = new(
            "SELECT string_agg(numero::text, ',') "
            + "FROM (SELECT numero FROM inventario.lineas_ajuste WHERE ajuste_id = @ajuste) AS l",
            conexion,
            transaccion);

        orden.Parameters.AddWithValue("ajuste", ajusteId);

        return (string)(await orden.ExecuteScalarAsync())!;
    }

    /// <summary>Espera a que la operación en vuelo se quede parada detrás del proceso dado, o falla.</summary>
    /// <remarks>
    /// El mismo mecanismo que en <c>LasExistenciasSonLaSumaDelLibroTests</c>, y por lo mismo: soltar
    /// por tiempo convertiría la carrera en dos operaciones seguidas, que salen bien con cerrojo o sin
    /// él.
    /// </remarks>
    /// <param name="procesoQueFrena">El proceso de PostgreSQL de la transacción en vuelo.</param>
    /// <param name="enVuelo">La operación que tiene que quedarse esperando.</param>
    private async Task EsperarAQueLaFreneAsync(int procesoQueFrena, Task enVuelo)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand quienEspera = new(
            "SELECT count(*) FROM pg_stat_activity WHERE @frena = ANY(pg_blocking_pids(pid))",
            conexion);

        quienEspera.Parameters.AddWithValue("frena", procesoQueFrena);

        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < limite)
        {
            enVuelo.IsCompleted.ShouldBeFalse(
                "la operación ha terminado sin esperar a la transacción en vuelo: ha valorado sin " +
                "ver lo que la otra estaba a punto de confirmar");

            if ((long)(await quienEspera.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new ShouldAssertException(
            "en treinta segundos nadie se ha puesto a esperar a la transacción en vuelo");
    }

    /// <summary>La fecha de hoy, en el mismo calendario que usa el caso de uso.</summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Una línea del caso.</summary>
    /// <param name="Articulo">Cuál de los artículos del caso, desde cero.</param>
    /// <param name="Cantidad">La cantidad, con signo; el factor es siempre uno.</param>
    /// <param name="Coste">El coste por unidad base, o nada.</param>
    private sealed record Linea(int Articulo, decimal Cantidad, decimal? Coste);

    /// <summary>Los maestros de un caso.</summary>
    /// <param name="Cliente">Cliente autenticado en la empresa.</param>
    /// <param name="EmpresaId">La empresa.</param>
    /// <param name="AlmacenId">El almacén.</param>
    /// <param name="UbicacionId">Su única ubicación.</param>
    /// <param name="Articulos">Los artículos, con su unidad base.</param>
    /// <param name="Serie">La serie del ejercicio de este año.</param>
    private sealed record ElCaso(
        HttpClient Cliente,
        Guid EmpresaId,
        Guid AlmacenId,
        Guid UbicacionId,
        IReadOnlyList<(Guid ArticuloId, Guid UnidadId)> Articulos,
        SerieDto Serie);
}
