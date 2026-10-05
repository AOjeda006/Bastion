using System.Net;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Organizacion.Contracts.Series;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las cuatro carreras de la transferencia, con dos transacciones de verdad cada una: tres de dos
/// operaciones sobre la misma transferencia, y una serie en tránsito contra un ajuste en un tercer
/// almacén.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada caso para a la primera transacción con el cerrojo tomado y nada escrito por la segunda</b>
/// (método del 2.8): la ganadora se queda dentro con su trabajo hecho, la perdedora sale lanzada,
/// <c>LaEspera</c> comprueba que está parada detrás de la ganadora y no en otra parte, y solo
/// entonces la ganadora confirma. Sin esa comprobación, una perdedora que llegara tarde por azar
/// daría el mismo verde que una que esperó en el cerrojo.
/// </para>
/// <para>
/// <b>Las dos primeras afirman el <c>412</c> del ADR-0053 §10</b>, que no sale de ningún índice: la
/// recepción y la anulación releen la versión del documento con las valoraciones ya bloqueadas. La
/// segunda está montada para que, sin esa relectura, el código fuera otro —el <c>422</c> de fecha
/// del ADR-0047—, y así el caso distingue las dos causas.
/// </para>
/// <para>
/// <b>La de dos envíos del mismo borrador entró con la tanda del paso 8</b>, y también afirma el
/// <c>412</c>, pero por otra causa: el envío no relee el documento, y lo que lo da es el orden en
/// que se escribe, la cabecera antes que las filas del libro.
/// </para>
/// <para>
/// <b>Semillas: del 730 al 732 y el 735</b>, empresas y maestros de instalación con el mismo número.
/// El reparto del bloque está en la cabecera de <c>LaTransferenciaTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasCarrerasDeLaTransferenciaTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Serie = "SN-TRANSITO-1";

    private static readonly TimeSpan s_plazo = TimeSpan.FromSeconds(30);

    private readonly ApiDeVerdad _api = new(postgres);
    private readonly List<HttpClient> _clientes = [];

    private static DateOnly Hoy => EscenaDeTransferencia.Hoy;

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
    /// Recibir y anular la misma enviada a la vez: la anulación espera en la valoración del destino,
    /// y al entrar ve que el documento ya no es el que leyó.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La anulación ya ha tomado su número cuando se para</b>: el contador de la serie va antes que
    /// las valoraciones (§10). Lo dice la fila del contador, que está bloqueada mientras la anulación
    /// espera —la recepción no numera, así que el cerrojo solo puede ser suyo—. Es la pareja de la
    /// sonda del caso del ejercicio cerrado, en <c>LaTransferenciaTests</c>, que lo encuentra libre
    /// porque allí la guarda va antes del numerador.
    /// </para>
    /// <para>
    /// <b>Que el contador siga en uno después</b> es la otra mitad: el <c>412</c> deshizo la
    /// transacción entera, número incluido. Por sí solo no diría nada del orden, porque una
    /// transacción deshecha devuelve el número tanto si lo tomó como si no.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Recibir_y_anular_a_la_vez_la_que_llega_segunda_sale_con_412()
    {
        EscenaDeTransferencia escena = await MontarAsync(730, "TRC-A");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 6m, 2m);

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 4m);

        await using ElModuloDeInventario unos = new(postgres, escena.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, escena.EmpresaId);

        (Resultado<TransferenciaDto> recepcion, IDbContextTransaction enVuelo) =
            await unos.RecibirYQuedarseDentroAsync(enviada.Id, Hoy);

        await using (enVuelo)
        {
            recepcion.EsCorrecto.ShouldBeTrue($"«{recepcion.Error?.Codigo}»");

            Task<Resultado<AnulacionDeTransferenciaDto>> laOtra =
                otros.AnularLaTransferenciaAsync(enviada.Id, "Se envió al almacén equivocado");

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                unos.ProcesoDeLaBase,
                laOtra,
                "la recepción en vuelo",
                "ha anulado la enviada sin esperar a la recepción que la estaba cambiando");

            (await escena.ElContadorEstaBloqueadoAsync(postgres)).ShouldBeTrue(
                "la anulación numera antes de bloquear las valoraciones (§10): cuando se para, ya " +
                "tiene su número");

            await enVuelo.CommitAsync();

            Resultado<AnulacionDeTransferenciaDto> anulacion = await laOtra.WaitAsync(s_plazo);

            anulacion.EsCorrecto.ShouldBeFalse("la transferencia que leyó ya no existe: ahora está recibida");
            anulacion.Error!.Codigo.ShouldBe(ErroresDeConcurrencia.CodigoDeVersionObsoleta);
        }

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Recibida);
        (await escena.InversosDeAsync(postgres, enviada.Id)).ShouldBe(0);
        (await escena.ContadorAsync()).ShouldBe(1, "el número que tomó la anulación volvió a la serie");
        (await escena.FilasDelLibroAsync(postgres, enviada.Id)).Count.ShouldBe(2);

        await ExigirQueCuadraAsync(escena);
    }

    /// <summary>
    /// Dos recepciones de la misma enviada a la vez, con fechas distintas: la segunda sale con el
    /// <c>412</c>, y no con el <c>422</c> de la fecha.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La que pierde lleva la fecha anterior</b>, el 15 contra el 20. Cuando entra en la
    /// valoración del destino, la ganadora ya la ha movido el día 20, así que la guarda del
    /// ADR-0047 le diría que su fecha es anterior al último movimiento. Ese <c>422</c> sería
    /// mentira: el problema no es la fecha, es que la transferencia ya se recibió. Lo arregló el
    /// paso 5, con la relectura del documento antes de valorar, y es el primer hallazgo de la
    /// revisión del paso 4.
    /// </para>
    /// <para>
    /// <b>En junio del año pasado</b>, para que las tres fechas hayan pasado cualquier día.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Dos_recepciones_a_la_vez_la_segunda_sale_con_412_y_no_con_el_422_de_la_fecha()
    {
        int anio = Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(731, "TRC-B", anio: anio);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 6m, 2m, new DateOnly(anio, 6, 1));

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 4m, new DateOnly(anio, 6, 10));

        var gana = new DateOnly(anio, 6, 20);
        var pierde = new DateOnly(anio, 6, 15);

        await using ElModuloDeInventario unos = new(postgres, escena.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, escena.EmpresaId);

        (Resultado<TransferenciaDto> recepcion, IDbContextTransaction enVuelo) =
            await unos.RecibirYQuedarseDentroAsync(enviada.Id, gana);

        await using (enVuelo)
        {
            recepcion.EsCorrecto.ShouldBeTrue($"«{recepcion.Error?.Codigo}»");

            Task<Resultado<TransferenciaDto>> laOtra = otros.RecibirAsync(enviada.Id, pierde);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                unos.ProcesoDeLaBase,
                laOtra,
                "la recepción en vuelo",
                "ha recibido dos veces la misma transferencia");

            await enVuelo.CommitAsync();

            Resultado<TransferenciaDto> segunda = await laOtra.WaitAsync(s_plazo);

            segunda.EsCorrecto.ShouldBeFalse();
            segunda.Error!.Codigo.ShouldBe(
                ErroresDeConcurrencia.CodigoDeVersionObsoleta,
                "sin releer el documento, la perdedora saldría con el 422 de la fecha del ADR-0047");
        }

        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).FechaDeRecepcion.ShouldBe(gana);
        (await escena.FilasDelLibroAsync(postgres, enviada.Id)).Count.ShouldBe(2, "una salida y una sola entrada");

        await ExigirQueCuadraAsync(escena);
    }

    /// <summary>
    /// Dos envíos del mismo borrador a la vez: el segundo sale con el <c>412</c>, y no con el
    /// <c>422</c> del stock que el primero ya se llevó.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El envío no relee el documento</b>, como hacen la recepción y la anulación. Quien lo para
    /// es el guardado de la cabecera, que va antes que las filas del libro (ADR-0053 §10). El segundo
    /// espera detrás del primero, y cuando entra en el origen solo quedan dos unidades de las seis, y
    /// él quiere sacar cuatro. Si escribiera las filas antes que la cabecera, chocaría con el stock y
    /// saldría con el <c>422</c>. Guardando primero, choca con el testigo de la fila. La mutación 248
    /// cambiaba ese orden y salió verde en los dos carriles: las otras dos carreras no la ven, porque
    /// a ellas las para la relectura antes de llegar a escribir.
    /// </para>
    /// <para>
    /// <b>El segundo va por la API</b>, porque el <c>412</c> y el <c>422</c> los pone el borde. El
    /// caso de uso no traduce ninguno de los dos: los dos suben como excepción.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Dos_envios_a_la_vez_el_segundo_sale_con_412_y_no_con_el_422_del_stock()
    {
        EscenaDeTransferencia escena = await MontarAsync(735, "TRC-D");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 6m, 2m);

        Guid borrador = await escena.AbrirAsync(postgres, 4m);

        await using ElModuloDeInventario unos = new(postgres, escena.EmpresaId);

        (Resultado<TransferenciaDto> envio, IDbContextTransaction enVuelo) =
            await unos.EnviarYQuedarseDentroAsync(borrador);

        await using (enVuelo)
        {
            envio.EsCorrecto.ShouldBeTrue($"«{envio.Error?.Codigo}»");

            Task<HttpResponseMessage> elOtro = escena.EnviarPorLaApiAsync(borrador);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                unos.ProcesoDeLaBase,
                elOtro,
                "el envío en vuelo",
                "ha enviado dos veces el mismo borrador");

            await enVuelo.CommitAsync();

            using HttpResponseMessage segundo = await elOtro.WaitAsync(s_plazo);

            segundo.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed, await Escenario.Detalle(segundo));
            (await EscenaDeTransferencia.TipoDelProblemaAsync(segundo))
                .ShouldBe("/errors/" + ErroresDeConcurrencia.CodigoDeVersionObsoleta);
        }

        (await escena.ContadorAsync()).ShouldBe(1, "el número que tomó el segundo volvió a la serie");
        (await escena.FilasDelLibroAsync(postgres, borrador)).Count.ShouldBe(1, "una sola salida");
        (await escena.LaValoracionDeAsync(postgres, escena.AlmacenB)).EnTransito.ShouldBe(4m);

        await ExigirQueCuadraAsync(escena, enTransito: 1, valoracionesEnTransito: 1);
    }

    /// <summary>
    /// Una serie sale de A hacia B, y a la vez un ajuste la da de alta en C: el ajuste espera en el
    /// índice y recibe su <c>23505</c>, porque la serie en tránsito sigue estando en un sitio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El índice cuenta el tránsito</b> (ADR-0053 §7): una serie que vuela hacia B no está en la
    /// estantería de nadie, pero tampoco puede entrar en otro almacén, porque cuando llegue habría
    /// dos. La revisión del paso 4 temió un interbloqueo <c>40P01</c> aquí, y no lo hay: el árbol B
    /// comprueba la unicidad antes de insertar la entrada, y el ajuste espera sin dejar nada que la
    /// transferencia pueda encontrar.
    /// </para>
    /// <para>
    /// <b>La serie ya existe antes de la carrera</b>, y nada más las cruza: el ajuste numera en otra
    /// serie y valora otro almacén, así que la única espera es la del índice, como en
    /// <c>UnNumeroDeSerieEnUnSoloSitioTests</c>.
    /// </para>
    /// <para>
    /// <b>Y sin carrera, por la API</b>, el mismo ajuste con la serie todavía en vuelo es el
    /// <c>422</c> <c>numero-de-serie-en-existencias</c>: el borde traduce el índice por su nombre.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Una_serie_en_transito_no_entra_a_la_vez_en_un_tercer_almacen()
    {
        EscenaDeTransferencia escena = await MontarAsync(732, "TRC-C", "PorNumeroSerie");

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 1m, 10m, serie: Serie);

        SerieDto otraSerie =
            await LosMaestrosPorLaApi.CrearSerieEnAsync(escena.Cliente, escena.Ejercicio.Id, "TRC-C-AJ2");

        LineaDeAjusteDto enC = new(escena.UbicacionC, escena.ArticuloId, 1m, escena.UnidadId, 1m, 10m, null, Serie);

        await using ElModuloDeInventario unos = new(postgres, escena.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, escena.EmpresaId);

        Guid transferenciaId = await escena.AbrirAsync(unos, 1m, serie: Serie);
        Guid ajusteId = await escena.AbrirUnAjusteAsync(otros, escena.AlmacenC, enC, otraSerie.Id);

        (Resultado<TransferenciaDto> envio, IDbContextTransaction enVuelo) =
            await unos.EnviarYQuedarseDentroAsync(transferenciaId);

        await using (enVuelo)
        {
            envio.EsCorrecto.ShouldBeTrue($"«{envio.Error?.Codigo}»");

            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(ajusteId);

            await LaEspera.AQueLaFreneAsync(
                postgres.CadenaDeConexion,
                unos.ProcesoDeLaBase,
                laOtra,
                "el envío en vuelo",
                "ha metido la serie en C sin ver que el envío la estaba poniendo en camino hacia B");

            await enVuelo.CommitAsync();

            PostgresException choque = await Should.ThrowAsync<PostgresException>(() => laOtra.WaitAsync(s_plazo));

            choque.SqlState.ShouldBe("23505", choque.MessageText);
            choque.ConstraintName.ShouldBe(
                "ix_existencias_numero_de_serie_en_un_sitio",
                "es el nombre que el borde traduce a 422: con otro, el ajuste saldría 500");
        }

        Existencia dondeEsta = (await LasExistencias.VivasAsync(postgres, escena.EmpresaId))
            .Where(fila => fila.Fisico > 0 || fila.EnTransito > 0)
            .ShouldHaveSingleItem("la serie está en un solo sitio, y ese sitio es el camino");

        dondeEsta.AlmacenId.ShouldBe(escena.AlmacenB);
        dondeEsta.Fisico.ShouldBe(0m);
        dondeEsta.EnTransito.ShouldBe(1m);

        (await escena.ContadorAsync(otraSerie.Id)).ShouldBe(0, "el ajuste que perdió no gastó su número");

        using HttpResponseMessage sinCarrera = await escena.ConfirmarElAjustePorLaApiAsync(ajusteId);

        sinCarrera.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(sinCarrera));
        (await sinCarrera.Content.ReadAsStringAsync()).ShouldContain("/errors/numero-de-serie-en-existencias");

        await ExigirQueCuadraAsync(escena, enTransito: 1, valoracionesEnTransito: 1);
    }

    private async Task<EscenaDeTransferencia> MontarAsync(
        int semilla, string codigo, string trazabilidad = "Ninguna", int? anio = null)
    {
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, trazabilidad, anio);

        _clientes.Add(escena.Cliente);

        return escena;
    }

    // LO QUE VUELA, CONTADO: con la serie todavía en el aire, un cuadre que no comparara ninguna
    // clave en tránsito saldría limpio por no mirar.
    private async Task ExigirQueCuadraAsync(
        EscenaDeTransferencia escena, long enTransito = 0, long valoracionesEnTransito = 0)
    {
        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, escena.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBeGreaterThan(0, "sin claves que comparar, el cuadre sale limpio por no mirar");
        cuadre.ExistenciasEnTransitoComparadas.ShouldBe(enTransito);
        cuadre.ValoracionesEnTransitoComparadas.ShouldBe(valoracionesEnTransito);
        cuadre.Descuadres.ShouldBeEmpty();
    }
}
