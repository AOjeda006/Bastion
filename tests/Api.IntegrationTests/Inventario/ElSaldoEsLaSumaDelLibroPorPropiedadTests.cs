using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Fechas;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Contracts.Recuentos;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La R3 como propiedad: después de cualquier secuencia de entradas, salidas, ajustes, anulaciones,
/// recálculos, cierres, fechas prohibidas, transferencias y recuentos, el saldo es la suma del libro
/// (ítem 2.7), lo que vuela, la suma de lo enviado sin recibir (ítem 2.11), y lo contado, lo que hay
/// al confirmar (epílogo del 2.12).
/// </summary>
/// <remarks>
/// <para>
/// <b>El generador es propio y con semilla fija</b>, sin biblioteca: un <see cref="Random"/> por
/// caso, con la semilla en el nombre del caso. Lo que importa de un caso de propiedades es que el
/// contraejemplo se pueda repetir, y con la semilla se repite la misma secuencia paso a paso. La
/// secuencia entera va en el mensaje de cada aserción: el rojo dice qué pasos lo produjeron.
/// </para>
/// <para>
/// <b>Y con el reloj parado</b> (ítem 2.9). La secuencia sale de la semilla y de «hoy»: las fechas
/// de este año se sortean hasta el día del año, y la paridad del día decide qué documento toma la
/// fecha del último movimiento. Con el reloj de verdad cada día corría otra secuencia, y el
/// 2026-09-30 la semilla 463 no pasó por ningún rechazo por la fecha. El reloj va a todos los casos
/// de uso, los del ajuste y los de la transferencia, que deciden con él qué fecha es futura y cuál
/// lleva el inverso. El cuadre sigue con el de
/// verdad: mira el libro hasta hoy, y el hoy de verdad nunca va por detrás de este.
/// </para>
/// <para>
/// <b>Tres cuentas que no se miran entre sí.</b> El modelo lleva en memoria lo que el libro debería
/// tener; tras cada paso se compara con el libro de verdad, con las filas vivas y con las
/// instantáneas, que se cuentan en C# desde el modelo sin mirar el SQL que las escribe. Y al final,
/// el recálculo y el cuadre por el camino de producción, afirmando que el cuadre comparó algo.
/// </para>
/// <para>
/// <b>Entradas y salidas son hoy líneas de ajuste con signo</b>: la entrada y la salida como
/// documentos llegan con compras y ventas. Lo que la R3 mira es la fila del libro, y esa es la misma
/// venga del documento que venga.
/// </para>
/// <para>
/// <b>Y el saldo no baja nunca de cero</b> (ítem 2.8, ADR-0046 §4). El modelo sabe, antes de
/// confirmar, si el documento dejaría alguna clave por debajo de cero, y entonces exige el rechazo
/// del motor por el nombre de su restricción, sin una fila más en el libro. Vale igual para una
/// anulación, cuyo inverso es otro documento: si las unidades de la entrada ya salieron, se
/// rechaza, y el original se puede anular más adelante.
/// </para>
/// <para>
/// <b>Y la valoración, con una cuenta propia</b> (ítem 2.8, ADR-0046). El modelo lleva el valor de
/// cada artículo y el precio medio que congela cada fila, calculados aquí con decimales y redondeos
/// a mano, sin el servicio del dominio ni los tipos del dinero; tras cada paso se comparan con la
/// tabla de la valoración y con el valor y el precio medio de cada fila del libro. Para que el
/// precio medio se mueva, cada entrada lleva un coste distinto, y las de un número par de unidades
/// de un artículo que ya tiene existencias van sin coste, al precio medio.
/// </para>
/// <para>
/// <b>Y ningún documento por detrás del último movimiento de su clave</b> (addendum del 2.8,
/// ADR-0047). El modelo sabe la última fecha de cada artículo, y un documento de este año que iría
/// por detrás tiene que rechazarse con su código antes de llegar al stock, porque la valoración va
/// antes que la existencia; la mitad de ellos toman antes la fecha de ese último movimiento, que
/// vale. Uno del año pasado va con fecha de hoy, como el que no tiene stock. Y tras cada paso, el
/// invariante que el contraejemplo rompe, contra el libro de verdad: la suma hasta cada fecha de
/// cada clave no deja cantidad negativa, valor negativo ni valor sin cantidad.
/// </para>
/// <para>
/// <b>Y la clave trazable</b> (ítem 2.9, ADR-0048 §6). Tres artículos, uno por marca: sin
/// trazabilidad, por lote y por número de serie. Las líneas del segundo llevan uno de tres lotes,
/// dos de ellos distintos solo en la caja, y a veces con espacios alrededor, que el sistema recorta.
/// Las del tercero llevan uno de tres números de serie, con una unidad base cada una y sin repetirse
/// en el documento. El modelo sabe, antes de confirmar, si el documento metería una serie donde ya
/// está o en otro sitio mientras sigue en el primero. Si es así, exige el rechazo del motor por el
/// nombre de alguna de las restricciones que rompería. Y tras cada paso comprueba el invariante de
/// la serie contra el libro de verdad: sumada hasta cada fecha, ninguna tiene más de una unidad en
/// un sitio ni está en dos.
/// </para>
/// <para>
/// <b>Y las transferencias entre dos almacenes</b> (ítem 2.11, ADR-0053). Los ajustes siguen en el
/// primero, A; el segundo, B, con un hueco, solo recibe y envía. Un tercer generador, con su propia
/// semilla, decide tras cada paso si viene uno de transferencia: enviar de donde hay, recibir una
/// enviada o anular una enviada o una recibida. Así el primero no tira un dado más por ellas, y
/// hasta la primera la secuencia es la de antes paso a paso; después, lo que una transferencia
/// cambia en A cambia lo que encuentran los pasos que vienen.
/// El modelo lleva lo que vuela por clave del destino y por artículo y almacén, en cantidad y en
/// valor, y valora cada pata como la tabla del ADR: la salida del origen se lleva su valor, la
/// llegada compensa el de la línea, y anular una recibida saca del destino lo que entró —o todo, si
/// lo vacía— y lo devuelve al origen. Tras cada paso compara lo que vuela con las filas vivas y con
/// la valoración, exige que el valor de la empresa sea el que han metido y sacado los ajustes
/// (ADR-0053 §6), y cuadra por el camino de producción, que tiene que comparar lo que el modelo
/// tiene en vuelo y no encontrar nada. Las transferencias van con fechas de este año: el ejercicio
/// de las suyas lo miran los casos de integración del 2.11.
/// </para>
/// <para>
/// <b>Y el recuento</b> (epílogo del 2.12, ADR-0055 y ADR-0057). Un cuarto generador, con su semilla,
/// decide tras cada paso si viene uno del recuento: abrirlo en A o en B, contar sus líneas,
/// confirmarlo, descartarlo o anular uno confirmado. Hay uno en curso como mucho, y entre contar y
/// confirmar pasan los pasos que el dado quiera, así que el almacén se mueve mientras tanto. Quien
/// confirma manda la huella que vio al acabar de contar. Si el modelo sabe que el teórico de alguna
/// línea ha cambiado desde entonces, exige el <c>409</c> del teórico, y confirma otra vez con la
/// huella nueva, como la pantalla. Lo que vuela hacia el almacén que se cuenta ha llegado sin
/// recibirse: si su clave no tiene línea, quien cuenta la añade, y la cuenta con lo que vuela. Si al
/// confirmar sigue volando, el modelo exige el <c>409</c> del tránsito, y la línea se vuelve a
/// contar. Al confirmar, el ajuste es la diferencia entre lo contado y el libro del modelo, y se
/// valora como cualquier otro, al precio medio lo que sube.
/// </para>
/// <para>
/// <b>Y dos invariantes más.</b> Tras confirmar, el físico de cada clave contada es lo contado, en
/// las filas vivas. Y el valor de la empresa solo lo mueven los ajustes, también los de un recuento:
/// además de la cuenta del modelo, la suma del valor de las filas del libro que vienen de un ajuste
/// tiene que ser el de la empresa, y la de las que vienen de una transferencia, menos lo que vuela.
/// </para>
/// <para>
/// <b>El modelo no conoce los identificadores del sistema.</b> Para el modelo, cada lote y cada
/// serie es su código, y les da un identificador propio. Lo que se lee de la base se traduce con las
/// tablas de los lotes y de las series, y esas tablas tienen que tener exactamente lo que el libro
/// del modelo ha confirmado: el lote de un documento rechazado se va con su transacción. Los
/// códigos salen de otro generador, con su propia semilla, para que sortearlos no desplace el de
/// los pasos: cada paso tira los mismos dados que sin ellos, aunque un código cambie su desenlace.
/// </para>
/// <para>
/// <b>Cada semilla tiene que pasar por todas las clases de paso</b>, y el caso lo afirma: una
/// semilla que no anulara nunca pasaría por prueba de las anulaciones sin haberlas probado.
/// </para>
/// <para>
/// <b>Semillas: del 460 al 465 las empresas</b>, que son también las semillas del generador; del
/// 470 al 475, del 480 al 485 y del 590 al 595 los maestros de instalación, tres artículos por caso.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElSaldoEsLaSumaDelLibroPorPropiedadTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
    // CIENTO SESENTA DESDE EL ÍTEM 2.9. En el 2.8 fueron ochenta: con el stock que no baja de cero,
    // cuarenta dejaban semillas sin una salida confirmada —sus anulaciones vaciaban todas las
    // claves— o sin un documento contra el año cerrado. Con tres artículos y seis claves, ochenta
    // dejaban cinco de las seis semillas sin ver a la serie parar un documento por sí sola, y ciento
    // veinte, la 461 sin una entrada al precio medio. Alargar no cambia los primeros pasos: el
    // generador sigue la misma serie.
    private const int Pasos = 160;
    private const string Anulacion = "anulación";
    private const string Futuro = "futuro";
    private const string Recalculo = "recálculo";
    private const string Cierre = "cierre";
    private const string Reapertura = "reapertura";
    private const string RechazadoPorElCierre = "rechazado por el cierre";
    private const string RechazadoSinStock = "rechazado sin stock";
    private const string RechazadoPorLaFecha = "rechazado por la fecha";
    private const string RechazadoPorLaSerie = "rechazado por la serie";
    private const string Envio = "transferencia enviada";
    private const string EnvioSinStock = "envío rechazado sin stock";
    private const string Recepcion = "transferencia recibida";
    private const string AnulacionDeUnaEnviada = "transferencia enviada anulada";
    private const string AnulacionDeUnaRecibida = "transferencia recibida anulada";
    private const string RecuentoConfirmado = "recuento confirmado";
    private const string RecuentoConLaHuellaNueva = "recuento confirmado con la huella nueva";
    private const string RecuentoConElTeoricoCambiado = "recuento con el teórico cambiado";
    private const string RecuentoQueSubeConTransito = "recuento que sube con tránsito";
    private const string RecuentoAnulado = "recuento anulado";
    private const string RecuentoDescartado = "recuento descartado";

    /// <summary>
    /// De cada cien pasos, cuántos traen detrás uno del recuento (epílogo del 2.12). Un recuento
    /// necesita tres por lo menos —abrir, contar y confirmar—, y entre uno y otro pasan unos cuatro
    /// pasos del almacén.
    /// </summary>
    private const int PorcentajeDeRecuentos = 25;

    /// <summary>
    /// Las claves de los ajustes: dos ubicaciones de A por cada uno de los tres artículos. Las tres
    /// de B van detrás, una por artículo, y solo las tocan las transferencias.
    /// </summary>
    private const int ClavesDeLosAjustes = 6;

    /// <summary>
    /// De cada cien pasos, cuántos traen detrás uno de transferencia (ítem 2.11). Con uno de cada
    /// cuatro, unos cuarenta por semilla.
    /// </summary>
    private const int PorcentajeDeTransferencias = 25;

    /// <summary>El código del documento que va por detrás de su clave (ADR-0047).</summary>
    private const string FechaAnterior = "ajuste-fecha-anterior-al-ultimo-movimiento";

    /// <summary>El mismo, en la transferencia: el envío mira el origen y la recepción, el destino.</summary>
    private const string FechaAnteriorDeLaTransferencia = "transferencia-fecha-anterior-al-ultimo-movimiento";

    /// <summary>La restricción que guarda el stock, escrita a mano como en el borde.</summary>
    private const string FisicoNoNegativo = "ck_existencias_fisico_no_negativo";

    /// <summary>La que no deja dos unidades de una serie en una fila (ADR-0048 §3).</summary>
    private const string NumeroDeSerieComoMuchoUna = "ck_existencias_numero_de_serie_como_mucho_una";

    /// <summary>La que no deja una serie en dos filas con unidades (ADR-0048 §3).</summary>
    private const string NumeroDeSerieEnUnSitio = "ix_existencias_numero_de_serie_en_un_sitio";

    private const string PorLote = "PorLote";
    private const string PorNumeroSerie = "PorNumeroSerie";

    private static readonly string[] s_clasesDePaso =
    [
        "entrada", "salida", "ajuste", Anulacion, Futuro, Recalculo, Cierre, Reapertura,
        RechazadoPorElCierre, RechazadoSinStock, RechazadoPorLaFecha, RechazadoPorLaSerie,
        Envio, EnvioSinStock, Recepcion, AnulacionDeUnaEnviada, AnulacionDeUnaRecibida,
        RecuentoConfirmado, RecuentoConLaHuellaNueva, RecuentoConElTeoricoCambiado, RecuentoQueSubeConTransito,
        RecuentoAnulado, RecuentoDescartado,
    ];

    private static readonly decimal[] s_factores = [1m, 2m, 0.5m];

    // DOS LOTES QUE SOLO SE DISTINGUEN POR LA CAJA: el código es el que se teclea, recortado, y la
    // caja cuenta (ADR-0048 §2). Y POCAS SERIES, para que una entre donde ya está.
    private static readonly string[] s_lotes = ["L-1", "l-1", "L-2"];
    private static readonly string[] s_series = ["S-1", "S-2", "S-3"];

    // EL DÍA EN QUE CORREN TODAS LAS SEMILLAS. Con este, las seis pasan por todas las clases de
    // paso; cambiarlo cambia la secuencia de todas, y hay que volver a verlo semilla a semilla.
    private static readonly RelojParado s_reloj = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

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

    [Theory]
    [InlineData(460)]
    [InlineData(461)]
    [InlineData(462)]
    [InlineData(463)]
    [InlineData(464)]
    [InlineData(465)]
    public async Task Tras_cualquier_secuencia_el_saldo_es_la_suma_del_libro(int semilla)
    {
        Random azar = new(semilla);
        Random codigos = new(semilla + 1_000);
        Random traslados = new(semilla + 2_000);
        Random recuentos = new(semilla + 3_000);
        Secuencia secuencia = new(semilla);

        UnaEmpresa empresa = await UnaEmpresaAsync(semilla);

        await using ElModuloDeInventario modulo = new(postgres, empresa.EmpresaId, s_reloj);

        for (int paso = 1; paso <= Pasos; paso++)
        {
            int dado = paso == 1 ? 0 : azar.Next(100);

            if (dado < 40)
            {
                await UnMovimientoAsync(azar, codigos, modulo, empresa, secuencia);
            }
            else if (dado < 55)
            {
                await UnaAnulacionAsync(azar, modulo, secuencia);
            }
            else if (dado < 65)
            {
                await UnaFechaFuturaAsync(azar, codigos, modulo, empresa, secuencia);
            }
            else if (dado < 80)
            {
                await UnRecalculoAsync(azar, empresa, secuencia);
            }
            else
            {
                await UnCierreOUnaReaperturaAsync(empresa, secuencia);
            }

            await ComprobarAsync(empresa, secuencia);

            // LA TRANSFERENCIA, DETRÁS Y CON SUS PROPIOS DADOS (ítem 2.11): el primer generador no
            // sabe de ella, y hasta la primera, la secuencia es la de antes paso a paso.
            if (traslados.Next(100) < PorcentajeDeTransferencias)
            {
                await UnaTransferenciaAsync(traslados, modulo, empresa, secuencia);
                await ComprobarAsync(empresa, secuencia);
            }

            // EL RECUENTO, DETRÁS DE TODO Y CON SUS PROPIOS DADOS (epílogo del 2.12): los otros tres
            // generadores no saben de él, y hasta que un recuento mueve algo, la secuencia es la de
            // antes paso a paso.
            if (recuentos.Next(100) < PorcentajeDeRecuentos)
            {
                await UnPasoDeRecuentoAsync(recuentos, empresa, secuencia);
                await ComprobarAsync(empresa, secuencia);
            }
        }

        // AL FINAL, EL CORTE EN ESTE MES, que es el que da instantánea a todas las claves —el libro
        // no tiene nada después de hoy—, y el cuadre por el camino de producción.
        DateOnly esteMes = LasExistencias.MesDe(Hoy);
        IReadOnlyList<FotoDelMes> debidas = LasExistencias.DebidasEnCSharp(secuencia.Libro, esteMes);

        (await LasExistencias.RecalcularAsync(postgres, empresa.EmpresaId, esteMes))
            .ShouldBe(debidas.Count, secuencia.Relato());

        secuencia.Corte = esteMes;
        await ComprobarAsync(empresa, secuencia);

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, empresa.EmpresaId);

        // CON FILA VIVA, LAS CLAVES DEL LIBRO Y LAS QUE ALGO HA IDO A BUSCAR (ítem 2.11): el envío
        // crea la del destino aunque nada llegue nunca, y la anulación no la borra.
        int claves = secuencia.ClavesConFila().Count;

        claves.ShouldBeGreaterThan(0, secuencia.Relato());
        debidas.Count.ShouldBeGreaterThan(0, secuencia.Relato());

        cuadre.ExistenciasComparadas.ShouldBe(claves, secuencia.Relato());
        cuadre.InstantaneasComparadas.ShouldBe(debidas.Count, secuencia.Relato());
        cuadre.ValoracionesComparadas.ShouldBe(secuencia.ClavesDeValoracion().Count, secuencia.Relato());
        cuadre.Descuadres.ShouldBeEmpty(secuencia.Relato());

        secuencia.Valoracion.Count.ShouldBeGreaterThan(0, secuencia.Relato());

        // EL CUADRE DEL TRÁNSITO, AFIRMADO: tras algún paso comparó claves y valoraciones en vuelo.
        // Sin eso, «ningún descuadre» en cada paso saldría verde por no tener nada que mirar.
        secuencia.MasExistenciasEnVuelo.ShouldBeGreaterThan(
            0L, "el cuadre no ha comparado nunca una existencia en vuelo\n" + secuencia.Relato());

        secuencia.MasValoracionesEnVuelo.ShouldBeGreaterThan(
            0L, "el cuadre no ha comparado nunca una valoración en vuelo\n" + secuencia.Relato());

        (await LosEstadosDelLibroAsync(empresa.EmpresaId)).Fechas.ShouldBeGreaterThan(
            0, "sin fechas que sumar, «ningún estado imposible» sale verde por no mirar\n" + secuencia.Relato());

        secuencia.AlPrecioMedio.ShouldBeGreaterThan(
            0, "ninguna entrada se ha valorado al precio medio\n" + secuencia.Relato());

        // LA CLAVE TRAZABLE, AFIRMADA: sin un lote y una serie en el libro, la traducción y el
        // invariante de la serie salen verdes por no tener nada que mirar.
        secuencia.Libro.ShouldContain(
            apunte => apunte.Clave.LoteId != null, "ningún lote ha llegado al libro\n" + secuencia.Relato());

        (await LasSeriesDelLibroAsync(empresa.EmpresaId)).Fechas.ShouldBeGreaterThan(
            0, "sin series en el libro, «ninguna en dos sitios» sale verde por no mirar\n" + secuencia.Relato());

        // LA SERIE EN VUELO Y EL CÓDIGO CON ESPACIOS EN LA TRANSFERENCIA, AFIRMADOS (revisión del
        // paso 7 del 2.11): sin una serie que vuele, el índice y el CHECK con lo que vuela no se
        // miran; sin un código tecleado con espacios, nada mira que la línea lo recorte.
        secuencia.SeriesQueHanVolado.ShouldBeGreaterThan(
            0, "ninguna serie ha salido en un envío\n" + secuencia.Relato());

        secuencia.CodigosConEspacios.ShouldBeGreaterThan(
            0, "ningún envío ha llevado un código con espacios\n" + secuencia.Relato());

        // LOS DOS INVARIANTES DEL RECUENTO, AFIRMADOS (epílogo del 2.12): sin una clave contada en
        // un recuento confirmado, «el físico es lo contado» sale verde sin mirar; y sin un recuento
        // que genere su ajuste, «solo los ajustes mueven el valor» no ha visto el de un recuento.
        secuencia.ClavesContadasComprobadas.ShouldBeGreaterThan(
            0, "ningún recuento confirmado ha dejado una clave que comprobar\n" + secuencia.Relato());

        secuencia.AjustesDeRecuento.ShouldBeGreaterThan(
            0, "ningún recuento confirmado ha generado su ajuste\n" + secuencia.Relato());

        secuencia.Clases.ShouldBe(s_clasesDePaso, ignoreOrder: true, customMessage: secuencia.Relato());
    }

    /// <summary>Una entrada, una salida o un ajuste de varias líneas, con una fecha de hoy o de atrás.</summary>
    private static async Task UnMovimientoAsync(
        Random azar, Random codigos, ElModuloDeInventario modulo, UnaEmpresa empresa, Secuencia secuencia)
    {
        int pasado = Hoy.Year - 1;

        // CON EL AÑO PASADO CERRADO, LA MITAD DE LAS VECES VA CONTRA ÉL: es el único momento en
        // que un documento con fecha atrás tiene que rechazarse, y con un tercio de las veces
        // ninguna semilla llegaba a verlo.
        DateOnly fecha = azar.Next(secuencia.PasadoCerrado ? 2 : 3) switch
        {
            0 => new DateOnly(pasado, 1, 1).AddDays(azar.Next(365)),
            1 => Hoy,
            _ => new DateOnly(Hoy.Year, 1, 1).AddDays(azar.Next(Hoy.DayOfYear)),
        };

        // LA CLASE SE ELIGE PRIMERO, un tercio cada una, y las líneas después (ítem 2.8). Sacadas
        // las líneas al azar y la clase de ellas, una salida de una sola línea salía una de cada
        // nueve veces, casi todas chocaban con el cero, y había semillas que no confirmaban ninguna.
        string clase = azar.Next(3) switch
        {
            0 => "entrada",
            1 => "salida",
            _ => "ajuste",
        };

        IReadOnlyList<LineaAlAzar> lineas = clase switch
        {
            "entrada" => [UnaLineaAlAzar(azar, 1)],
            "salida" => [UnaSalidaAlAzar(azar, empresa, secuencia)],
            _ => LineasAlAzar(azar, minimo: 2),
        };

        // EL COSTE Y LOS CÓDIGOS SE PONEN DESPUÉS, SIN TOCAR EL AZAR (ítems 2.8 y 2.9): una llamada
        // más al generador cambiaría la secuencia de todas las semillas, que hoy pasan por todas las
        // clases de paso. El coste, antes que los códigos: se decide con lo que se tecleó, y la
        // serie deja después la línea en una unidad. Al revés, ninguna línea de una serie iría
        // nunca al precio medio, porque una unidad no es un número par.
        lineas = [.. lineas.Select(linea => ConCoste(linea, empresa, secuencia))];
        lineas = ConSusCodigos(codigos, empresa, secuencia, lineas);

        bool delAnioCerrado = fecha.Year == pasado && secuencia.PasadoCerrado;

        // UN RECHAZO DEL MOTOR VA CON FECHA DE ESTE AÑO, sea por el stock o por la serie. El motor
        // lo rechaza con cualquier fecha —la fila viva es la suma de todas—, pero el borrador se
        // queda, no hay forma de tirarlo, y uno del año pasado impediría cerrarlo durante el resto
        // de la secuencia: la semilla no llegaría a ver el cierre. El del año cerrado no se toca,
        // porque ese rechazo es otro.
        if (!delAnioCerrado && fecha.Year == pasado && LoQueRomperia(
            secuencia,
            lineas.Select(linea => new ApunteDelLibro(
                secuencia.ClaveDe(empresa, linea), fecha, linea.Cantidad * linea.Factor))).Count > 0)
        {
            fecha = Hoy;
        }

        // Y POR LO MISMO, UNO DEL AÑO PASADO QUE IRÍA POR DETRÁS DE SU CLAVE (ADR-0047): se
        // rechazaría y su borrador impediría el cierre. Hoy no va por detrás de nada, porque el
        // libro no tiene fechas futuras. Sin tocar el azar, como el coste.
        DateOnly? ultima = LaUltimaFechaDeSusClaves(secuencia, lineas.Select(linea => empresa.Claves[linea.Clave]));

        if (!delAnioCerrado && fecha.Year == pasado && fecha < ultima)
        {
            fecha = Hoy;
        }

        // UNO DE ESTE AÑO QUE IRÍA POR DETRÁS, LA MITAD DE LAS VECES TOMA LA FECHA DEL ÚLTIMO
        // MOVIMIENTO DE SUS CLAVES, que vale (ADR-0047 §4), y la otra mitad se queda con la suya y
        // se rechaza. Cada anulación deja su artículo con la fecha de hoy, y con todos rechazados,
        // a partir de ahí solo se confirmaba lo de hoy: había semillas que no valoraban ninguna
        // entrada al precio medio. La paridad del día decide, sin tocar el azar.
        if (fecha.Year != pasado && fecha < ultima && fecha.DayNumber % 2 == 1)
        {
            fecha = ultima.Value;
        }

        Guid ajusteId = await AbrirAsync(
            modulo, empresa, fecha.Year == pasado ? empresa.SerieDelAnioPasado : empresa.Serie, fecha, lineas);

        ApunteDelLibro[] apuntes =
        [
            .. lineas.Select(linea => new ApunteDelLibro(
                secuencia.ClaveDe(empresa, linea), fecha, linea.Cantidad * linea.Factor)),
        ];

        // EL CIERRE SE MIRA ANTES QUE EL STOCK, porque el caso de uso lo mira antes: un documento
        // del año cerrado no llega a tocar la existencia, deje lo que deje.
        if (delAnioCerrado)
        {
            Resultado<AjusteDto> rechazada = await modulo.ConfirmarAsync(ajusteId);

            secuencia.Anotar(RechazadoPorElCierre, fecha, lineas);
            secuencia.BorradoresEnElPasado++;

            rechazada.EsCorrecto.ShouldBeFalse(secuencia.Relato());
            rechazada.Error!.Codigo.ShouldBe("ajuste-en-ejercicio-cerrado", secuencia.Relato());

            return;
        }

        // LA FECHA SE MIRA ANTES QUE EL STOCK, porque el caso de uso valora antes de tocar la
        // existencia: un documento por detrás de su clave no llega a ella (ADR-0047 §2).
        if (fecha < ultima)
        {
            Resultado<AjusteDto> rechazada = await modulo.ConfirmarAsync(ajusteId);

            secuencia.Anotar(RechazadoPorLaFecha, fecha, lineas);

            rechazada.EsCorrecto.ShouldBeFalse(secuencia.Relato());
            rechazada.Error!.Codigo.ShouldBe(FechaAnterior, secuencia.Relato());

            return;
        }

        if (LoQueRomperia(secuencia, apuntes) is { Count: > 0 } rotas)
        {
            secuencia.Anotar(ClaseDelRechazo(rotas), fecha, lineas);

            await ExigirElRechazoDelMotorAsync(() => modulo.ConfirmarAsync(ajusteId), rotas, secuencia);

            return;
        }

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        secuencia.Anotar(clase, fecha, lineas);

        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»\n{secuencia.Relato()}");

        secuencia.Libro.AddRange(apuntes);

        FilaValorada[] valoradas = secuencia.Valorar(
            [.. lineas.Select(linea => new LineaQueSeValora(
                empresa.Claves[linea.Clave].ArticuloId,
                empresa.AlmacenId,
                linea.Cantidad * linea.Factor,
                linea.Coste,
                null))]);

        secuencia.ValorDeLaEmpresa += valoradas.Sum(fila => fila.Valor);
        secuencia.AlPrecioMedio += lineas.Count(linea => linea.Cantidad > 0m && linea.Coste is null);
        secuencia.Anulables.Add((ajusteId, apuntes, valoradas));
    }

    /// <summary>La anulación de un documento confirmado que todavía no se ha anulado.</summary>
    /// <remarks>
    /// El inverso lleva la fecha de hoy y cada fila del original con el signo cambiado, aunque el
    /// original sea del año pasado y ese ejercicio esté cerrado: anular se puede siempre (R2).
    /// </remarks>
    private static async Task UnaAnulacionAsync(
        Random azar, ElModuloDeInventario modulo, Secuencia secuencia)
    {
        if (secuencia.Anulables.Count == 0)
        {
            secuencia.Anotar("anulación que se salta: no hay nada que anular");

            return;
        }

        int cual = azar.Next(secuencia.Anulables.Count);
        (Guid ajusteId, ApunteDelLibro[] apuntes, FilaValorada[] valoradas) = secuencia.Anulables[cual];

        ApunteDelLibro[] inverso =
        [
            .. apuntes.Select(apunte => apunte with
            {
                Fecha = Hoy,
                Cantidad = -apunte.Cantidad,
            }),
        ];

        // EL INVERSO DE UNA ENTRADA ES UNA SALIDA, y si las unidades ya salieron se rechaza
        // (ADR-0046 §1). El de la salida de una serie es una entrada, y si la serie ya está en
        // otro sitio, también (ADR-0048 §5). El original sigue siendo anulable: se puede anular
        // cuando vuelvan, o cuando la serie se vaya de donde está.
        if (LoQueRomperia(secuencia, inverso) is { Count: > 0 } rotas)
        {
            secuencia.Anotar(ClaseDelRechazo(rotas), apuntes);

            await ExigirElRechazoDelMotorAsync(
                () => modulo.AnularAsync(ajusteId, "Anulación del generador"), rotas, secuencia);

            return;
        }

        secuencia.Anulables.RemoveAt(cual);

        secuencia.Anotar(Anulacion, apuntes);

        Resultado<AnulacionDto> anulacion = await modulo.AnularAsync(ajusteId, "Anulación del generador");

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»\n{secuencia.Relato()}");

        secuencia.Libro.AddRange(inverso);

        // EL INVERSO COMPENSA LO QUE CADA LÍNEA MOVIÓ, y no lo que valen hoy sus unidades
        // (ADR-0046 §6): cada línea lleva el valor de la suya con el signo cambiado.
        FilaValorada[] inversas = secuencia.Valorar(
            [.. valoradas.Select(fila => new LineaQueSeValora(
                fila.ArticuloId, fila.AlmacenId, -fila.Cantidad, null, -fila.Valor))]);

        secuencia.ValorDeLaEmpresa += inversas.Sum(fila => fila.Valor);
    }

    /// <summary>
    /// Un paso de transferencia (ítem 2.11): recibir una enviada, anular una enviada o una recibida,
    /// o enviar de donde hay.
    /// </summary>
    /// <remarks>
    /// Recibir, si hay alguna enviada; anular, si hay alguna enviada o recibida; y si no, se envía. Así
    /// que el dado de recibir, sin enviadas, anula una recibida si la hay. Todo con el tercer
    /// generador: el primero no tira un dado más por las transferencias.
    /// </remarks>
    private static async Task UnaTransferenciaAsync(
        Random traslados, ElModuloDeInventario modulo, UnaEmpresa empresa, Secuencia secuencia)
    {
        int dado = traslados.Next(100);

        if (dado < 35 && secuencia.Enviadas.Count > 0)
        {
            await UnaRecepcionAsync(traslados, modulo, empresa, secuencia);
        }
        else if (dado < 60 && secuencia.Enviadas.Count + secuencia.Recibidas.Count > 0)
        {
            await UnaAnulacionDeTransferenciaAsync(traslados, modulo, empresa, secuencia);
        }
        else
        {
            await UnEnvioAsync(traslados, modulo, empresa, secuencia);
        }
    }

    /// <summary>Un envío de una o dos claves con existencias de un almacén al otro.</summary>
    /// <remarks>
    /// <para>
    /// <b>De A a B, y una de cada tres veces de B a A si B tiene algo.</b> Lo que llega a B solo sale
    /// de allí por otra transferencia, y si no volviera nunca, A se iría vaciando.
    /// </para>
    /// <para>
    /// <b>Cada línea, de una clave con existencias</b>, como la salida. Una serie lleva su unidad; lo
    /// demás, todo lo que hay en la clave —que, si es lo último del artículo en el almacén, se lleva
    /// todo su valor—, una parte, o una unidad más de lo que hay, que el motor rechaza. A B va a su
    /// único hueco, y a A, a cualquiera de los dos.
    /// </para>
    /// <para>
    /// <b>La primera línea es una serie la mitad de las veces que el origen la tiene</b>, un empujón
    /// de la revisión del paso: sin él, dos de las seis semillas no hacían volar ninguna, y el índice
    /// y el <c>CHECK</c> con lo que vuela no se miraban en ellas.
    /// </para>
    /// <para>
    /// <b>Los códigos, una de cada cuatro veces con espacios</b>, como en el ajuste: la línea de la
    /// transferencia también guarda el código recortado (ADR-0048 §2).
    /// </para>
    /// <para>
    /// <b>La fecha, hoy o un día de este año.</b> Si va por detrás del último movimiento del origen,
    /// la mitad de las veces toma esa fecha y la otra mitad se rechaza (ADR-0047). El destino no
    /// cuenta: lo que vuela no mueve su fecha (ADR-0053 §3).
    /// </para>
    /// </remarks>
    private static async Task UnEnvioAsync(
        Random traslados, ElModuloDeInventario modulo, UnaEmpresa empresa, Secuencia secuencia)
    {
        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(secuencia.Libro);

        bool deB = saldos.Any(par => par.Key.AlmacenId == empresa.AlmacenB && par.Value > 0m)
            && traslados.Next(3) == 0;

        Guid origen = deB ? empresa.AlmacenB : empresa.AlmacenId;
        Guid destino = deB ? empresa.AlmacenId : empresa.AlmacenB;

        // EN UN ORDEN QUE NO DEPENDE DE LOS IDENTIFICADORES, como en la salida.
        List<(ClaveDeExistencia Clave, decimal Saldo)> conExistencias =
        [
            .. saldos
                .Where(par => par.Key.AlmacenId == origen && par.Value > 0m)
                .Select(par => (Clave: par.Key, Saldo: par.Value, Donde: secuencia.LineaEn(empresa, par.Key)))
                .OrderBy(par => par.Donde.Clave)
                .ThenBy(par => par.Donde.Lote, StringComparer.Ordinal)
                .ThenBy(par => par.Donde.Serie, StringComparer.Ordinal)
                .Select(par => (par.Clave, par.Saldo)),
        ];

        if (conExistencias.Count == 0)
        {
            secuencia.Anotar("envío que se salta: el origen no tiene existencias");

            return;
        }

        int cuantas = Math.Min(conExistencias.Count, 1 + traslados.Next(2));
        List<LineaEnVuelo> lineas = [];

        while (lineas.Count < cuantas)
        {
            int cual = UnaClaveDelEnvio(traslados, conExistencias, primera: lineas.Count == 0);
            (ClaveDeExistencia clave, decimal saldo) = conExistencias[cual];

            conExistencias.RemoveAt(cual);

            decimal cantidad = clave.NumeroDeSerieId is not null
                ? 1m
                : traslados.Next(8) switch
                {
                    0 => saldo + 1m,
                    1 or 2 => saldo,
                    _ => Math.Min(saldo, 1 + traslados.Next((int)decimal.Ceiling(saldo))),
                };

            lineas.Add(new LineaEnVuelo(clave, empresa.DestinoDe(clave, traslados), cantidad, 0m));
        }

        DateOnly fecha = traslados.Next(2) == 0
            ? Hoy
            : new DateOnly(Hoy.Year, 1, 1).AddDays(traslados.Next(Hoy.DayOfYear));

        DateOnly? ultima = LaUltimaFechaDeSusClaves(secuencia, lineas.Select(linea => linea.Origen));

        if (fecha < ultima && fecha.DayNumber % 2 == 1)
        {
            fecha = ultima.Value;
        }

        (string? Lote, string? Serie)[] tecleados =
        [
            .. lineas.Select(linea => (
                linea.Origen.LoteId is { } lote ? Tecleado(traslados, secuencia.CodigoDe(lote)) : null,
                linea.Origen.NumeroDeSerieId is { } serie ? Tecleado(traslados, secuencia.CodigoDe(serie)) : null)),
        ];

        Resultado<TransferenciaDto> alta = await modulo.AltaDeTransferencia.EjecutarAsync(
            new AbrirTransferenciaDto(
                empresa.SerieDeTransferencias.Id,
                origen,
                destino,
                fecha,
                [.. lineas.Select((linea, indice) => new LineaDeTransferenciaDto(
                    linea.Origen.UbicacionId,
                    linea.Destino.UbicacionId,
                    linea.Origen.ArticuloId,
                    linea.Cantidad,
                    empresa.UnidadDe[linea.Origen.ArticuloId],
                    1m,
                    tecleados[indice].Lote,
                    tecleados[indice].Serie))]),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»\n{secuencia.Relato()}");

        string detalle = DescribirElTraslado(empresa, secuencia, fecha, lineas);

        // LA FECHA SE MIRA ANTES QUE EL STOCK, como en el ajuste: el caso de uso valora el origen
        // antes de tocar la existencia.
        if (fecha < ultima)
        {
            Resultado<TransferenciaDto> rechazado = await modulo.EnviarAsync(alta.Valor.Id);

            secuencia.Anotar("envío rechazado por la fecha", detalle);

            rechazado.EsCorrecto.ShouldBeFalse(secuencia.Relato());
            rechazado.Error!.Codigo.ShouldBe(FechaAnteriorDeLaTransferencia, secuencia.Relato());

            return;
        }

        ApunteDelLibro[] salen =
            [.. lineas.Select(linea => new ApunteDelLibro(linea.Origen, fecha, -linea.Cantidad))];

        // EL RECHAZO SIN STOCK ES UNA CLASE DE PASO (tanda del paso 8 del 2.11): sin ella, que las
        // líneas dejaran de pedir de más no lo veía nadie, y el rechazo que no deja tránsito ni
        // consume número se quedaba sin mirar.
        if (LoQueRomperia(secuencia, salen, secuencia.TransitoTras(lineas, 1)) is { Count: > 0 } rotas)
        {
            secuencia.Anotar(
                ClaseDelRechazo(rotas) == RechazadoSinStock ? EnvioSinStock : "envío rechazado por el motor",
                detalle);

            await ExigirElRechazoDelMotorAsync(() => modulo.EnviarAsync(alta.Valor.Id), rotas, secuencia);

            return;
        }

        Resultado<TransferenciaDto> envio = await modulo.EnviarAsync(alta.Valor.Id);

        secuencia.Anotar(Envio, detalle);

        envio.EsCorrecto.ShouldBeTrue($"«{envio.Error?.Codigo}»\n{secuencia.Relato()}");

        secuencia.SeriesQueHanVolado += lineas.Count(linea => linea.Origen.NumeroDeSerieId is not null);
        secuencia.CodigosConEspacios += tecleados.Count(
            codigos => codigos.Lote != codigos.Lote?.Trim() || codigos.Serie != codigos.Serie?.Trim());

        secuencia.Libro.AddRange(salen);

        // LO QUE SALE DEL ORIGEN SE LLEVA SU VALOR, y ese valor es el que vuela (ADR-0053 §2).
        FilaValorada[] valoradas = secuencia.Valorar(
            [.. lineas.Select(linea => new LineaQueSeValora(
                linea.Origen.ArticuloId, origen, -linea.Cantidad, null, null))]);

        LineaEnVuelo[] enVuelo =
            [.. lineas.Select((linea, indice) => linea with { Valor = -valoradas[indice].Valor })];

        secuencia.Volar(enVuelo, 1);
        secuencia.Enviadas.Add(new TransferenciaDelModelo(alta.Valor.Id, origen, destino, fecha, enVuelo));
    }

    /// <summary>La recepción de una enviada, un día entre el de su envío y hoy.</summary>
    /// <remarks>
    /// Si va por detrás del último movimiento del destino, la mitad de las veces toma esa fecha y la
    /// otra mitad se rechaza (ADR-0047), como el envío con el origen. Contra el motor no choca nunca:
    /// lo que suma en cada clave del destino es lo que ya volaba hacia ella, y una serie en vuelo no
    /// está en ningún otro sitio (ADR-0053 §7).
    /// </remarks>
    private static async Task UnaRecepcionAsync(
        Random traslados, ElModuloDeInventario modulo, UnaEmpresa empresa, Secuencia secuencia)
    {
        int cual = traslados.Next(secuencia.Enviadas.Count);
        TransferenciaDelModelo enviada = secuencia.Enviadas[cual];

        DateOnly fecha = enviada.FechaDeEnvio.AddDays(
            traslados.Next(Hoy.DayNumber - enviada.FechaDeEnvio.DayNumber + 1));

        DateOnly? ultima = LaUltimaFechaDeSusClaves(secuencia, enviada.Lineas.Select(linea => linea.Destino));

        if (fecha < ultima && fecha.DayNumber % 2 == 1)
        {
            fecha = ultima.Value;
        }

        string detalle = DescribirElTraslado(empresa, secuencia, fecha, enviada.Lineas);

        Resultado<TransferenciaDto> recepcion = await modulo.RecibirAsync(enviada.Id, fecha);

        if (fecha < ultima)
        {
            secuencia.Anotar("recepción rechazada por la fecha", detalle);

            recepcion.EsCorrecto.ShouldBeFalse(secuencia.Relato());
            recepcion.Error!.Codigo.ShouldBe(FechaAnteriorDeLaTransferencia, secuencia.Relato());

            return;
        }

        secuencia.Anotar(Recepcion, detalle);

        recepcion.EsCorrecto.ShouldBeTrue($"«{recepcion.Error?.Codigo}»\n{secuencia.Relato()}");

        secuencia.Enviadas.RemoveAt(cual);
        secuencia.Libro.AddRange(
            enviada.Lineas.Select(linea => new ApunteDelLibro(linea.Destino, fecha, linea.Cantidad)));
        secuencia.Volar(enviada.Lineas, -1);

        // LA LLEGADA COMPENSA EL VALOR DE LA LÍNEA, y no el precio medio del destino (ADR-0053 §2).
        secuencia.Valorar(
            [.. enviada.Lineas.Select(linea => new LineaQueSeValora(
                linea.Destino.ArticuloId, enviada.Destino, linea.Cantidad, null, linea.Valor))]);

        secuencia.Recibidas.Add(enviada);
    }

    /// <summary>La anulación de una enviada o de una recibida, con su inverso de hoy (ADR-0053 §5).</summary>
    /// <remarks>
    /// <para>
    /// <b>La de una enviada devuelve al origen lo que salió</b>, con su valor, y lo que volaba deja de
    /// volar. No choca nunca: la serie en vuelo no está en ningún otro sitio.
    /// </para>
    /// <para>
    /// <b>La de una recibida saca del destino lo que entró y lo devuelve al origen</b>, con el valor
    /// que sale del destino: el de la línea, o todo el que queda si lo vacía o si vale menos. Si lo que
    /// entró ya no está —salió en otra transferencia—, el motor la rechaza, y la recibida se puede
    /// anular más adelante.
    /// </para>
    /// <para>
    /// <b>La mitad de las veces que la hay, se anula una recibida que deja a cero un artículo del
    /// destino</b>, otro empujón de la revisión del paso: es el borde del ADR-0054, el único sitio
    /// donde el origen puede recibir otra cosa que el valor de la línea, y al azar ninguna semilla
    /// llegaba a él. Con el empujón tampoco llegan todas, así que no se afirma por semilla: el borde
    /// lo fija el caso de <c>LaTransferenciaTests</c> en el que anular una recibida vacía el destino.
    /// </para>
    /// </remarks>
    private static async Task UnaAnulacionDeTransferenciaAsync(
        Random traslados, ElModuloDeInventario modulo, UnaEmpresa empresa, Secuencia secuencia)
    {
        int cual = UnaTransferenciaQueAnular(traslados, secuencia);
        bool recibida = cual >= secuencia.Enviadas.Count;

        List<TransferenciaDelModelo> deDonde = recibida ? secuencia.Recibidas : secuencia.Enviadas;
        int dondeEsta = recibida ? cual - secuencia.Enviadas.Count : cual;
        TransferenciaDelModelo original = deDonde[dondeEsta];

        string detalle = DescribirElTraslado(empresa, secuencia, original.FechaDeEnvio, original.Lineas);

        ApunteDelLibro[] vuelven =
            [.. original.Lineas.Select(linea => new ApunteDelLibro(linea.Origen, Hoy, linea.Cantidad))];

        ApunteDelLibro[] inverso = recibida
            ? [.. original.Lineas.Select(linea => new ApunteDelLibro(linea.Destino, Hoy, -linea.Cantidad)), .. vuelven]
            : vuelven;

        if (LoQueRomperia(secuencia, inverso, recibida ? null : secuencia.TransitoTras(original.Lineas, -1))
            is { Count: > 0 } rotas)
        {
            secuencia.Anotar("anulación de una transferencia rechazada por el motor", detalle);

            await ExigirElRechazoDelMotorAsync(
                () => modulo.AnularLaTransferenciaAsync(original.Id, "Anulación del generador"), rotas, secuencia);

            return;
        }

        Resultado<AnulacionDeTransferenciaDto> anulacion =
            await modulo.AnularLaTransferenciaAsync(original.Id, "Anulación del generador");

        secuencia.Anotar(recibida ? AnulacionDeUnaRecibida : AnulacionDeUnaEnviada, detalle);

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»\n{secuencia.Relato()}");

        deDonde.RemoveAt(dondeEsta);
        secuencia.Libro.AddRange(inverso);

        if (!recibida)
        {
            secuencia.Volar(original.Lineas, -1);

            secuencia.Valorar(
                [.. original.Lineas.Select(linea => new LineaQueSeValora(
                    linea.Origen.ArticuloId, original.Origen, linea.Cantidad, null, linea.Valor))]);

            return;
        }

        // EL DESTINO PRIMERO, y el origen recibe lo que salió de él (ADR-0053 §5).
        FilaValorada[] salen = secuencia.Valorar(
            [.. original.Lineas.Select(linea => new LineaQueSeValora(
                linea.Destino.ArticuloId, original.Destino, -linea.Cantidad, null, -linea.Valor))]);

        secuencia.Valorar(
            [.. original.Lineas.Select((linea, indice) => new LineaQueSeValora(
                linea.Origen.ArticuloId, original.Origen, linea.Cantidad, null, -salen[indice].Valor))]);
    }

    /// <summary>
    /// Qué clave del origen lleva la siguiente línea del envío: la primera, la mitad de las veces una
    /// serie si el origen tiene alguna; las demás, cualquiera.
    /// </summary>
    private static int UnaClaveDelEnvio(
        Random traslados, List<(ClaveDeExistencia Clave, decimal Saldo)> conExistencias, bool primera)
    {
        int[] deSerie =
        [
            .. Enumerable.Range(0, conExistencias.Count)
                .Where(indice => conExistencias[indice].Clave.NumeroDeSerieId is not null),
        ];

        return primera && deSerie.Length > 0 && traslados.Next(2) == 0
            ? deSerie[traslados.Next(deSerie.Length)]
            : traslados.Next(conExistencias.Count);
    }

    /// <summary>
    /// Qué transferencia se anula, contando primero las enviadas y después las recibidas: la mitad de
    /// las veces, una recibida que dejaría a cero alguna clave del destino, si la hay; si no,
    /// cualquiera.
    /// </summary>
    private static int UnaTransferenciaQueAnular(Random traslados, Secuencia secuencia)
    {
        var porArticulo = LasExistencias
            .SaldosDelLibro(secuencia.Libro)
            .GroupBy(par => (par.Key.ArticuloId, par.Key.AlmacenId))
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(par => par.Value));

        int[] queVaciarian =
        [
            .. Enumerable.Range(0, secuencia.Recibidas.Count)
                .Where(indice => secuencia.Recibidas[indice].Lineas
                    .GroupBy(linea => linea.Destino.ArticuloId)
                    .Any(lineas => porArticulo.GetValueOrDefault((lineas.Key, secuencia.Recibidas[indice].Destino))
                        == lineas.Sum(linea => linea.Cantidad))),
        ];

        return queVaciarian.Length > 0 && traslados.Next(2) == 0
            ? secuencia.Enviadas.Count + queVaciarian[traslados.Next(queVaciarian.Length)]
            : traslados.Next(secuencia.Enviadas.Count + secuencia.Recibidas.Count);
    }

    /// <summary>El código como lo teclearía alguien: una de cada cuatro veces, con espacios alrededor.</summary>
    private static string Tecleado(Random traslados, string codigo) =>
        traslados.Next(4) == 0 ? " " + codigo + " " : codigo;

    /// <summary>Una transferencia para el relato: su fecha y, por línea, de qué clave a cuál y cuánto.</summary>
    private static string DescribirElTraslado(
        UnaEmpresa empresa, Secuencia secuencia, DateOnly fecha, IEnumerable<LineaEnVuelo> lineas) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{fecha:yyyy-MM-dd} [{string.Join(", ", lineas.Select(linea => DescribirLaLinea(empresa, secuencia, linea)))}]");

    private static string DescribirLaLinea(UnaEmpresa empresa, Secuencia secuencia, LineaEnVuelo linea)
    {
        LineaAlAzar donde = secuencia.LineaEn(empresa, linea.Origen);

        return string.Create(
                CultureInfo.InvariantCulture,
                $"clave {donde.Clave} a {empresa.IndiceDe(linea.Destino)}: {linea.Cantidad}")
            + (donde.Lote is { } lote ? " lote «" + lote + "»" : string.Empty)
            + (donde.Serie is { } serie ? " serie «" + serie + "»" : string.Empty);
    }

    /// <summary>
    /// Un paso del recuento (epílogo del 2.12): abrir uno o anular uno confirmado si no hay ninguno en
    /// curso; contar lo que falta si lo hay; y si está todo contado, confirmarlo o descartarlo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Todo con el cuarto generador, que es el único que sabe del recuento. Uno de cada cinco que está
    /// listo para confirmar se descarta —con uno de cada seis, la 460 no descartaba ninguno—, y tres de
    /// cada diez pasos sin recuento en curso anulan uno confirmado, si lo hay. Hay que contar mientras
    /// falte una línea por contar o por volver a contar, y mientras llegue algo sin línea.
    /// </para>
    /// <para>
    /// <b>Cada llamada al caso de uso, con su contexto</b> (<see cref="UnaPeticion"/>), y no con el
    /// módulo del caso: el recuento se cuenta por la API, y un contexto que siguiera con el recuento
    /// del alta en memoria lo leería con la versión de antes de contar.
    /// </para>
    /// </remarks>
    private async Task UnPasoDeRecuentoAsync(Random recuentos, UnaEmpresa empresa, Secuencia secuencia)
    {
        if (secuencia.RecuentoEnCurso is not { } enCurso)
        {
            if (secuencia.RecuentosAnulables.Count > 0 && recuentos.Next(10) < 3)
            {
                await UnaAnulacionDeRecuentoAsync(recuentos, empresa, secuencia);
            }
            else
            {
                await UnaAperturaDeRecuentoAsync(recuentos, empresa, secuencia);
            }

            return;
        }

        if (enCurso.Lineas.Any(linea => linea.Contado is null || linea.PorRecontar)
            || LoQueHaLlegadoSinLinea(secuencia, enCurso).Length > 0)
        {
            await UnConteoAsync(recuentos, empresa, secuencia, enCurso);
        }
        else if (recuentos.Next(5) == 0)
        {
            await UnDescarteAsync(empresa, secuencia, enCurso);
        }
        else
        {
            await UnaConfirmacionDeRecuentoAsync(empresa, secuencia, enCurso);
        }
    }

    /// <summary>
    /// Abre el recuento del almacén hacia el que vuela algo; si no lo hay, el de B la mitad de las
    /// veces que B tiene algo, y si no, el de A. Y exige que traiga una línea por cada clave del
    /// almacén con físico, ni una más (ADR-0055 §5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El empujón del tránsito</b>: lo que vuela hacia el almacén que se cuenta es lo que el conteo
    /// encuentra sin línea (<see cref="LoQueHaLlegadoSinLinea"/>), y lo que hace saltar el <c>409</c>
    /// del tránsito. Sin él, la 461 no lo ve nunca, medido con la mutación 370: una clave con físico
    /// y con algo en vuelo hacia ella casi no se da, y la precarga solo trae las que tienen físico.
    /// </para>
    /// <para>
    /// Por el caso de uso, y no por la API, porque la fecha de apertura sale del reloj y el del
    /// generador está parado. Las lecturas van por la API: no dependen del reloj.
    /// </para>
    /// </remarks>
    private async Task UnaAperturaDeRecuentoAsync(Random recuentos, UnaEmpresa empresa, Secuencia secuencia)
    {
        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(secuencia.Libro);

        Guid? haciaDondeVuela = secuencia.Transito
            .Where(par => par.Value > 0m)
            .Select(par => (Guid?)par.Key.AlmacenId)
            .FirstOrDefault();

        Guid almacen = haciaDondeVuela
            ?? (saldos.Any(par => par.Key.AlmacenId == empresa.AlmacenB && par.Value > 0m) && recuentos.Next(2) == 0
                ? empresa.AlmacenB
                : empresa.AlmacenId);

        bool deB = almacen == empresa.AlmacenB;

        Resultado<RecuentoDto> alta;

        await using (ElModuloDeInventario peticion = UnaPeticion(empresa))
        {
            alta = await peticion.AltaDeRecuento.EjecutarAsync(
                new AbrirRecuentoDto(empresa.SerieDeRecuentos.Id, empresa.Serie.Id, almacen, "Recuento del generador"),
                CancellationToken.None);
        }

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»\n{secuencia.Relato()}");

        IReadOnlyList<LineaDelRecuento> lineas = await LasLineasAsync(empresa, secuencia, alta.Valor.Id, almacen);

        secuencia.Anotar(
            "recuento abierto",
            string.Create(CultureInfo.InvariantCulture, $"en {(deB ? "B" : "A")} con {lineas.Count} líneas"));

        lineas.Select(linea => linea.Clave).ShouldBe(
            saldos.Where(par => par.Key.AlmacenId == almacen && par.Value > 0m).Select(par => par.Key),
            ignoreOrder: true,
            customMessage: "la precarga no es una línea por clave con físico\n" + secuencia.Relato());

        RecuentoDelModelo recuento = new(alta.Valor.Id, almacen, lineas);

        await VerLaFichaAsync(empresa, secuencia, recuento);

        secuencia.RecuentoEnCurso = recuento;
    }

    /// <summary>
    /// Añade una línea por cada clave a la que ha llegado algo sin línea, y cuenta las que faltan por
    /// contar y las que hay que volver a contar, cada una con la versión que tiene; y lee después la
    /// ficha: esa huella es la que verá quien confirme.
    /// </summary>
    /// <remarks>
    /// Por la API, porque ni añadir ni contar miran el reloj. Lo que se cuenta lo dice
    /// <see cref="LoQueSeCuenta"/>.
    /// </remarks>
    private static async Task UnConteoAsync(
        Random recuentos, UnaEmpresa empresa, Secuencia secuencia, RecuentoDelModelo recuento)
    {
        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(secuencia.Libro);
        List<string> contadas = [];

        foreach (ClaveDeExistencia clave in LoQueHaLlegadoSinLinea(secuencia, recuento))
        {
            using HttpResponseMessage alta = await EscenaDeRecuento.AnadirAsync(
                empresa.Cliente,
                recuento.Id,
                new AnadirLineaDeRecuentoDto(
                    clave.UbicacionId,
                    clave.ArticuloId,
                    clave.LoteId is { } lote ? secuencia.CodigoDe(lote) : null,
                    clave.NumeroDeSerieId is { } serie ? secuencia.CodigoDe(serie) : null,
                    CosteUnitario: null));

            alta.StatusCode.ShouldBe(HttpStatusCode.Created, $"{await Escenario.Detalle(alta)}\n{secuencia.Relato()}");

            LineaDeRecuentoDto anadida = (await alta.Content.ReadFromJsonAsync<LineaDeRecuentoDto>())!;

            anadida.Teorico.ShouldBe(saldos.GetValueOrDefault(clave), secuencia.Relato());
            anadida.EnTransito.ShouldBe(secuencia.Transito[clave], secuencia.Relato());

            recuento.Lineas.Add(new LineaDelRecuento(anadida.Id, anadida.Numero, clave));
            contadas.Add(string.Create(CultureInfo.InvariantCulture, $"línea {anadida.Numero} añadida"));
        }

        foreach (LineaDelRecuento linea in recuento.Lineas.Where(linea => linea.Contado is null || linea.PorRecontar))
        {
            decimal teorico = saldos.GetValueOrDefault(linea.Clave);
            decimal contado = LoQueSeCuenta(recuentos, linea, teorico, secuencia.Transito.GetValueOrDefault(linea.Clave));

            string ruta = EscenaDeRecuento.RutaDeLaLinea(recuento.Id, linea.Id);
            string etiqueta = await empresa.Cliente.EtiquetaDeAsync(ruta);

            using HttpResponseMessage conteo =
                await EscenaDeRecuento.ContarAsync(empresa.Cliente, recuento.Id, linea.Id, etiqueta, contado);

            LineaDeRecuentoDto contada = await EscenaDeTransferencia.LeerAsync<LineaDeRecuentoDto>(conteo);

            contadas.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"línea {linea.Numero}: {contado} de {teorico}{(linea.PorRecontar ? " otra vez" : string.Empty)}"));

            contada.Contado.ShouldBe(contado, secuencia.Relato());
            contada.TeoricoAlContar.ShouldBe(teorico, secuencia.Relato());

            linea.Contado = contado;
            linea.PorRecontar = false;
        }

        secuencia.Anotar("recuento contado", "[" + string.Join(", ", contadas) + "]");

        await VerLaFichaAsync(empresa, secuencia, recuento);
    }

    /// <summary>
    /// Las claves del almacén del recuento hacia las que vuela algo y que no tienen línea: lo que quien
    /// cuenta encuentra en la estantería sin que nadie lo haya recibido.
    /// </summary>
    /// <remarks>
    /// Sin las series que ya tienen línea en otra ubicación: el recuento no admite una serie en dos
    /// líneas (<c>recuento-serie-repetida</c>), y eso ya lo prueba su caso.
    /// </remarks>
    private static ClaveDeExistencia[] LoQueHaLlegadoSinLinea(Secuencia secuencia, RecuentoDelModelo recuento) =>
    [
        .. secuencia.Transito
            .Where(par => par.Value > 0m && par.Key.AlmacenId == recuento.AlmacenId)
            .Select(par => par.Key)
            .Where(clave => recuento.Lineas.All(linea =>
                linea.Clave != clave
                && (clave.NumeroDeSerieId is null
                    || linea.Clave.ArticuloId != clave.ArticuloId
                    || linea.Clave.NumeroDeSerieId != clave.NumeroDeSerieId))),
    ];

    /// <summary>Lo que se cuenta en una línea, según lo que hay en su clave y lo que vuela hacia ella.</summary>
    /// <remarks>
    /// <para>
    /// <b>Si algo vuela hacia su clave, lo que hay más lo que vuela</b>: la mercancía ha llegado y
    /// nadie la ha recibido, que es justo lo que el <c>409</c> del tránsito impide sumar dos veces
    /// (ADR-0055 §7). Contada al azar, el <c>409</c> dependería de que la cifra saliera de más.
    /// </para>
    /// <para>
    /// <b>La que se vuelve a contar tras un rechazo, lo que hay</b>: quien cuenta ha visto qué era lo
    /// que sobraba. Las demás, la mitad de las veces lo que hay, y si no, de una a tres unidades de
    /// más o de menos, sin bajar de cero. Una serie se cuenta en cero o en uno.
    /// </para>
    /// </remarks>
    private static decimal LoQueSeCuenta(Random recuentos, LineaDelRecuento linea, decimal teorico, decimal transito)
    {
        if (linea.PorRecontar)
        {
            return teorico;
        }

        if (transito > 0m)
        {
            return teorico + transito;
        }

        decimal contado = recuentos.Next(4) switch
        {
            0 or 1 => teorico,
            2 => teorico + recuentos.Next(1, 4),
            _ => Math.Max(0m, teorico - recuentos.Next(1, 4)),
        };

        return linea.Clave.NumeroDeSerieId is null ? contado : Math.Min(contado, 1m);
    }

    /// <summary>
    /// Confirma con la huella que se vio al contar; y si el modelo sabe que el teórico ha cambiado
    /// desde entonces, exige el <c>409</c> y confirma con la huella nueva, como la pantalla.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>En el orden del caso de uso</b> (ADR-0055 §3): la huella, el tránsito de lo que sube, el
    /// valor y, al escribir, las restricciones del motor. Lo que sube va al precio medio de su
    /// artículo en el almacén, y si no lo hay, el <c>422</c> del ajuste. El único choque posible con
    /// el motor es una serie contada donde ya no está y que está en otro sitio. Tras el tránsito o el
    /// valor, se vuelven a contar las líneas que lo causan; tras el motor, todas las que difieren,
    /// porque el motor no dice cuál.
    /// </para>
    /// <para>
    /// <b>Confirmado, el invariante del recuento</b>: el físico de cada clave contada es lo contado,
    /// leído de las filas vivas y no del modelo.
    /// </para>
    /// </remarks>
    private async Task UnaConfirmacionDeRecuentoAsync(UnaEmpresa empresa, Secuencia secuencia, RecuentoDelModelo recuento)
    {
        (RecuentoDto ficha, string etiqueta) = await LaFichaAsync(empresa, recuento.Id);
        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(secuencia.Libro);

        bool haCambiado = recuento.Lineas.Any(
            linea => saldos.GetValueOrDefault(linea.Clave) != recuento.TeoricoVisto[linea.Id]);

        string detalle = DescribirElRecuento(recuento, saldos, secuencia);
        string huella = recuento.HuellaVista!;
        bool conLaHuellaNueva = false;

        // LA HUELLA SE MUEVE SI Y SOLO SI SE MUEVE EL TEÓRICO DE ALGUNA LÍNEA (ADR-0055 §2), dicho por
        // el modelo sin saber cómo se calcula.
        (ficha.HuellaDelTeorico != huella).ShouldBe(
            haCambiado, "la huella no se mueve con el teórico de las líneas\n" + secuencia.Relato());

        if (haCambiado)
        {
            Resultado<RecuentoDto> rechazada = await ConfirmarElRecuentoAsync(empresa, recuento.Id, etiqueta, huella);

            secuencia.Anotar(RecuentoConElTeoricoCambiado, detalle);

            rechazada.EsCorrecto.ShouldBeFalse(secuencia.Relato());
            rechazada.Error!.Codigo.ShouldBe("recuento-teorico-cambiado", secuencia.Relato());

            // CONFIRMAR OTRA VEZ MANDA LA HUELLA NUEVA: la de la ficha, que es la de ahora. El 409
            // no ha escrito nada, así que la versión es la misma.
            huella = ficha.HuellaDelTeorico!;
            recuento.Ver(huella, saldos);
            conLaHuellaNueva = true;
        }

        (LineaDelRecuento Linea, decimal Diferencia)[] difieren =
        [
            .. recuento.Lineas
                .Select(linea => (Linea: linea, Diferencia: linea.Contado!.Value - saldos.GetValueOrDefault(linea.Clave)))
                .Where(par => par.Diferencia != 0m),
        ];

        LineaDelRecuento[] conTransito =
        [
            .. difieren
                .Where(par => par.Diferencia > 0m && secuencia.Transito.GetValueOrDefault(par.Linea.Clave) > 0m)
                .Select(par => par.Linea),
        ];

        if (conTransito.Length > 0)
        {
            await ExigirElRechazoDelRecuentoAsync(
                empresa, recuento, etiqueta, huella, "recuento-sube-con-transito", conTransito, secuencia);

            secuencia.Anotar(RecuentoQueSubeConTransito, detalle);

            return;
        }

        LineaDelRecuento[] sinPrecioMedio =
        [
            .. difieren
                .Where(par => par.Diferencia > 0m && secuencia.Valoracion
                    .GetValueOrDefault((par.Linea.Clave.ArticuloId, recuento.AlmacenId)).Cantidad <= 0m)
                .Select(par => par.Linea),
        ];

        if (sinPrecioMedio.Length > 0)
        {
            await ExigirElRechazoDelRecuentoAsync(
                empresa, recuento, etiqueta, huella, "ajuste-entrada-sin-coste-ni-precio-medio", sinPrecioMedio, secuencia);

            secuencia.Anotar("recuento rechazado sin precio medio", detalle);

            return;
        }

        ApunteDelLibro[] apuntes = [.. difieren.Select(par => new ApunteDelLibro(par.Linea.Clave, Hoy, par.Diferencia))];

        if (LoQueRomperia(secuencia, apuntes) is { Count: > 0 } rotas)
        {
            secuencia.Anotar("recuento rechazado por el motor", detalle);

            await ExigirElRechazoDelMotorAsync(
                () => ConfirmarElRecuentoAsync(empresa, recuento.Id, etiqueta, huella), rotas, secuencia);

            foreach ((LineaDelRecuento linea, _) in difieren)
            {
                linea.PorRecontar = true;
            }

            return;
        }

        Resultado<RecuentoDto> confirmacion = await ConfirmarElRecuentoAsync(empresa, recuento.Id, etiqueta, huella);

        secuencia.Anotar(conLaHuellaNueva ? RecuentoConLaHuellaNueva : RecuentoConfirmado, detalle);

        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»\n{secuencia.Relato()}");
        (confirmacion.Valor.AjusteId is not null).ShouldBe(apuntes.Length > 0, secuencia.Relato());

        secuencia.RecuentoEnCurso = null;
        secuencia.Libro.AddRange(apuntes);

        // LO QUE SUBE, AL PRECIO MEDIO DE SU ARTÍCULO EN EL ALMACÉN; lo que baja, como cualquier
        // salida (ADR-0055 §6 y §8). Es un ajuste más, y mueve el valor de la empresa.
        FilaValorada[] valoradas = secuencia.Valorar(
            [.. difieren.Select(par => new LineaQueSeValora(
                par.Linea.Clave.ArticuloId, recuento.AlmacenId, par.Diferencia, null, null))]);

        secuencia.ValorDeLaEmpresa += valoradas.Sum(fila => fila.Valor);
        secuencia.AjustesDeRecuento += apuntes.Length > 0 ? 1 : 0;
        secuencia.RecuentosAnulables.Add((recuento.Id, apuntes, valoradas));

        // EL INVARIANTE DEL RECUENTO: el físico de cada clave contada es lo contado, en las filas
        // vivas. El modelo ya lo dice con su libro; esto lo dice sin él.
        IReadOnlyDictionary<Guid, Guid> traduccion = await LaTraduccionAsync(empresa.EmpresaId, secuencia);

        var fisicos = (await LasExistencias.VivasAsync(postgres, empresa.EmpresaId))
            .ToDictionary(viva => EnElModelo(LasExistencias.ClaveDe(viva), traduccion), viva => viva.Fisico);

        foreach (LineaDelRecuento linea in recuento.Lineas)
        {
            fisicos.GetValueOrDefault(linea.Clave).ShouldBe(
                linea.Contado!.Value,
                string.Create(CultureInfo.InvariantCulture, $"la línea {linea.Numero} no ha dejado su clave en lo contado\n")
                    + secuencia.Relato());
        }

        secuencia.ClavesContadasComprobadas += recuento.Lineas.Count;
    }

    /// <summary>
    /// Exige que la confirmación se rechace con este código, y deja para volver a contar las líneas
    /// que lo causan. El recuento sigue en curso, y nada se ha movido.
    /// </summary>
    private async Task ExigirElRechazoDelRecuentoAsync(
        UnaEmpresa empresa,
        RecuentoDelModelo recuento,
        string etiqueta,
        string huella,
        string codigo,
        IEnumerable<LineaDelRecuento> causantes,
        Secuencia secuencia)
    {
        Resultado<RecuentoDto> rechazada = await ConfirmarElRecuentoAsync(empresa, recuento.Id, etiqueta, huella);

        rechazada.EsCorrecto.ShouldBeFalse(secuencia.Relato());
        rechazada.Error!.Codigo.ShouldBe(codigo, secuencia.Relato());

        foreach (LineaDelRecuento linea in causantes)
        {
            linea.PorRecontar = true;
        }
    }

    /// <summary>Descarta el recuento en curso por la API: no mueve nada y deja libre el almacén.</summary>
    /// <remarks>Por la API, porque descartar no mira el reloj.</remarks>
    private static async Task UnDescarteAsync(UnaEmpresa empresa, Secuencia secuencia, RecuentoDelModelo recuento)
    {
        (_, string etiqueta) = await LaFichaAsync(empresa, recuento.Id);

        using HttpResponseMessage descarte = await EscenaDeRecuento.DescartarAsync(
            empresa.Cliente, recuento.Id, etiqueta, "Descarte del generador", clave: null);

        RecuentoDto descartado = await EscenaDeTransferencia.LeerAsync<RecuentoDto>(descarte);

        secuencia.Anotar(
            RecuentoDescartado,
            string.Create(CultureInfo.InvariantCulture, $"de {recuento.Lineas.Count} líneas"));

        descartado.Estado.ShouldBe("Descartado", secuencia.Relato());

        secuencia.RecuentoEnCurso = null;
    }

    /// <summary>
    /// La anulación de un recuento confirmado: el inverso de su ajuste, con la fecha de hoy, o solo el
    /// estado si no tuvo ajuste (ADR-0055 §9).
    /// </summary>
    /// <remarks>
    /// Como la de un ajuste: si las unidades que entraron ya salieron, o si la serie que salió está en
    /// otro sitio, el motor la rechaza, y el recuento se puede anular más adelante. Por el caso de uso,
    /// porque el inverso lleva la fecha del reloj.
    /// </remarks>
    private async Task UnaAnulacionDeRecuentoAsync(Random recuentos, UnaEmpresa empresa, Secuencia secuencia)
    {
        int cual = recuentos.Next(secuencia.RecuentosAnulables.Count);
        (Guid recuentoId, ApunteDelLibro[] apuntes, FilaValorada[] valoradas) = secuencia.RecuentosAnulables[cual];

        (_, string etiqueta) = await LaFichaAsync(empresa, recuentoId);

        ApunteDelLibro[] inverso =
            [.. apuntes.Select(apunte => apunte with { Fecha = Hoy, Cantidad = -apunte.Cantidad })];

        string detalle = string.Create(CultureInfo.InvariantCulture, $"con {apuntes.Length} filas de ajuste");

        if (LoQueRomperia(secuencia, inverso) is { Count: > 0 } rotas)
        {
            secuencia.Anotar("anulación de un recuento rechazada por el motor", detalle);

            await ExigirElRechazoDelMotorAsync(() => AnularElRecuentoAsync(empresa, recuentoId, etiqueta), rotas, secuencia);

            return;
        }

        secuencia.RecuentosAnulables.RemoveAt(cual);

        Resultado<RecuentoDto> anulacion = await AnularElRecuentoAsync(empresa, recuentoId, etiqueta);

        secuencia.Anotar(RecuentoAnulado, detalle);

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»\n{secuencia.Relato()}");

        secuencia.Libro.AddRange(inverso);

        // EL INVERSO COMPENSA LO QUE CADA LÍNEA MOVIÓ, como el de cualquier ajuste (ADR-0046 §6).
        FilaValorada[] inversas = secuencia.Valorar(
            [.. valoradas.Select(fila => new LineaQueSeValora(
                fila.ArticuloId, fila.AlmacenId, -fila.Cantidad, null, -fila.Valor))]);

        secuencia.ValorDeLaEmpresa += inversas.Sum(fila => fila.Valor);
    }

    /// <summary>
    /// Un contexto nuevo del módulo con el reloj del generador: lo que tendría una petición de verdad.
    /// </summary>
    private ElModuloDeInventario UnaPeticion(UnaEmpresa empresa) => new(postgres, empresa.EmpresaId, s_reloj);

    /// <summary>La confirmación de un recuento, en su petición.</summary>
    private async Task<Resultado<RecuentoDto>> ConfirmarElRecuentoAsync(
        UnaEmpresa empresa, Guid recuentoId, string etiqueta, string huella)
    {
        await using ElModuloDeInventario peticion = UnaPeticion(empresa);

        return await peticion.ConfirmarElRecuentoAsync(recuentoId, etiqueta, huella);
    }

    /// <summary>La anulación de un recuento, en su petición.</summary>
    private async Task<Resultado<RecuentoDto>> AnularElRecuentoAsync(UnaEmpresa empresa, Guid recuentoId, string etiqueta)
    {
        await using ElModuloDeInventario peticion = UnaPeticion(empresa);

        return await peticion.AnularElRecuentoAsync(recuentoId, etiqueta, "Anulación del generador");
    }

    /// <summary>
    /// Lee la ficha y guarda lo que ve quien va a confirmar: la huella y el teórico de cada línea, que
    /// el modelo saca de su libro.
    /// </summary>
    private static async Task VerLaFichaAsync(UnaEmpresa empresa, Secuencia secuencia, RecuentoDelModelo recuento)
    {
        (RecuentoDto ficha, _) = await LaFichaAsync(empresa, recuento.Id);

        ficha.HuellaDelTeorico.ShouldNotBeNull(secuencia.Relato());

        recuento.Ver(ficha.HuellaDelTeorico, LasExistencias.SaldosDelLibro(secuencia.Libro));
    }

    /// <summary>La ficha de un recuento por la API, con su versión.</summary>
    private static async Task<(RecuentoDto Ficha, string Etiqueta)> LaFichaAsync(UnaEmpresa empresa, Guid recuentoId)
    {
        using HttpResponseMessage lectura = await empresa.Cliente.GetAsync($"{EscenaDeRecuento.Recuentos}/{recuentoId}");

        RecuentoDto ficha = await EscenaDeTransferencia.LeerAsync<RecuentoDto>(lectura);

        lectura.Headers.ETag.ShouldNotBeNull("la ficha del recuento no emite ETag");

        return (ficha, lectura.Headers.ETag.ToString());
    }

    /// <summary>
    /// Las líneas de un recuento recién abierto por la API, con la clave del modelo; y exige que el
    /// teórico y el tránsito de cada una sean los del modelo.
    /// </summary>
    private static async Task<IReadOnlyList<LineaDelRecuento>> LasLineasAsync(
        UnaEmpresa empresa, Secuencia secuencia, Guid recuentoId, Guid almacenId)
    {
        using HttpResponseMessage lectura = await empresa.Cliente.GetAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{EscenaDeRecuento.Recuentos}/{recuentoId}/lineas?size={Paginacion.TamanioMaximo}"));

        PaginaDe<LineaDeRecuentoDto> pagina = await EscenaDeTransferencia.LeerAsync<PaginaDe<LineaDeRecuentoDto>>(lectura);

        pagina.Total.ShouldBeLessThanOrEqualTo(Paginacion.TamanioMaximo, "una página no las trae todas");

        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(secuencia.Libro);
        List<LineaDelRecuento> lineas = [];

        foreach (LineaDeRecuentoDto dto in pagina.Elementos)
        {
            ClaveDeExistencia clave = new(
                dto.ArticuloId,
                almacenId,
                dto.UbicacionId,
                dto.CodigoDeLote is { } lote ? secuencia.LoteDe(dto.ArticuloId, lote) : null,
                dto.NumeroDeSerie is { } serie ? secuencia.SerieDe(dto.ArticuloId, serie) : null);

            dto.Teorico.ShouldBe(saldos.GetValueOrDefault(clave), secuencia.Relato());
            dto.EnTransito.ShouldBe(secuencia.Transito.GetValueOrDefault(clave), secuencia.Relato());

            lineas.Add(new LineaDelRecuento(dto.Id, dto.Numero, clave));
        }

        return lineas;
    }

    /// <summary>Un recuento para el relato: por línea, lo contado y lo que hay ahora.</summary>
    private static string DescribirElRecuento(
        RecuentoDelModelo recuento, IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos, Secuencia secuencia) =>
        "[" + string.Join(", ", recuento.Lineas.Select(linea =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"línea {linea.Numero}: {linea.Contado} de {saldos.GetValueOrDefault(linea.Clave)}")
            + (secuencia.Transito.GetValueOrDefault(linea.Clave) is > 0m and var vuela
                ? string.Create(CultureInfo.InvariantCulture, $" y {vuela} en vuelo")
                : string.Empty))) + "]";

    /// <summary>
    /// Qué restricciones de la existencia rompería sumar estas filas al libro del modelo; ninguna si
    /// el motor tiene que dejarlas pasar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por clave y con las filas del documento sumadas</b>, porque así las suma la sentencia:
    /// agrupa las líneas de la misma clave antes de tocar la fila viva, y el motor mira el
    /// resultado. Una salida y una entrada de la misma clave en el mismo documento no pasan por un
    /// negativo.
    /// </para>
    /// <para>
    /// <b>Una clave que queda en negativo rompe solo la del stock</b>, aunque sea de una serie y
    /// también se salga del <c>BETWEEN 0 AND 1</c>: los <c>CHECK</c> se comprueban en orden
    /// alfabético de nombre (ADR-0048 §3), y el del stock va antes. Una serie con dos unidades en
    /// una fila rompe el <c>CHECK</c> de la serie; y con una, en una fila distinta de otra que ya la
    /// tiene, el índice. En un ajuste, la otra fila no la toca el documento, porque una serie va una
    /// sola vez en cada uno; la anulación de una recibida sí, y por eso se mira como queda (abajo).
    /// </para>
    /// <para>
    /// <b>Con varias rotas, cualquiera puede ser la que salta</b>: la sentencia recorre las filas en
    /// el orden que quiere, y para en la primera.
    /// </para>
    /// <para>
    /// <b>Y con lo que vuela dentro</b> (ítem 2.11, ADR-0053 §7). El <c>CHECK</c> de la serie suma el
    /// físico y el tránsito de la fila, y el índice cuenta en un sitio la fila que tiene la serie en
    /// vuelo. El otro sitio se mira como queda <b>después</b> del documento, con su tránsito: anular
    /// una recibida saca la serie del destino antes de devolverla al origen, y el envío o la anulación
    /// de una enviada mueven el tránsito antes que lo que suma. <paramref name="transito"/> es lo que
    /// el documento deja en vuelo, y si no se dice, lo que ya vuela.
    /// </para>
    /// </remarks>
    private static HashSet<string> LoQueRomperia(
        Secuencia secuencia,
        IEnumerable<ApunteDelLibro> filas,
        IReadOnlyDictionary<ClaveDeExistencia, decimal>? transito = null)
    {
        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(secuencia.Libro);
        IReadOnlyDictionary<ClaveDeExistencia, decimal> enVuelo = transito ?? secuencia.Transito;

        var despues = filas
            .GroupBy(fila => fila.Clave)
            .ToDictionary(
                clave => clave.Key,
                clave => saldos.GetValueOrDefault(clave.Key) + clave.Sum(fila => fila.Cantidad));

        HashSet<string> rotas = [];

        foreach ((ClaveDeExistencia clave, decimal fisico) in despues)
        {
            if (fisico < 0m)
            {
                rotas.Add(FisicoNoNegativo);
            }
            else if (clave.NumeroDeSerieId is not null && fisico + enVuelo.GetValueOrDefault(clave) > 1m)
            {
                rotas.Add(NumeroDeSerieComoMuchoUna);
            }
            else if (clave.NumeroDeSerieId is { } serie && fisico > 0m && saldos.Keys.Union(enVuelo.Keys).Any(otra =>
                otra != clave
                && otra.ArticuloId == clave.ArticuloId
                && otra.NumeroDeSerieId == serie
                && (despues.GetValueOrDefault(otra, saldos.GetValueOrDefault(otra)) > 0m
                    || enVuelo.GetValueOrDefault(otra) > 0m)))
            {
                rotas.Add(NumeroDeSerieEnUnSitio);
            }
        }

        return rotas;
    }

    /// <summary>La clase de paso de un rechazo del motor, según lo que el modelo sabe roto.</summary>
    /// <remarks>
    /// Un rechazo que puede venir de la serie o del stock no cuenta como ninguno de los dos: la
    /// semilla que solo pasara por esos no habría visto a la serie parar un documento.
    /// </remarks>
    private static string ClaseDelRechazo(HashSet<string> rotas) =>
        rotas.Contains(FisicoNoNegativo)
            ? rotas.Count == 1 ? RechazadoSinStock : "rechazado sin stock o por la serie"
            : RechazadoPorLaSerie;

    /// <summary>
    /// La fecha más alta del libro del modelo entre los artículos de estas claves, cada uno en su
    /// almacén, o nada si ninguno se ha movido allí.
    /// </summary>
    /// <remarks>
    /// Un documento lleva una sola fecha, así que va por detrás de alguna de sus claves si y solo si
    /// va por detrás de la más alta. La misma fecha no va por detrás: la regla es «anterior»
    /// (ADR-0047 §4). Comparada con nada, una fecha no es menor, y un artículo sin movimientos no
    /// rechaza ninguna. Lo que vuela no cuenta: no está en el libro (ADR-0053 §1).
    /// </remarks>
    private static DateOnly? LaUltimaFechaDeSusClaves(Secuencia secuencia, IEnumerable<ClaveDeExistencia> claves) =>
        claves.Select(clave => secuencia.UltimaFechaDe(clave.ArticuloId, clave.AlmacenId)).Max();

    /// <summary>
    /// Exige que el motor rechace la operación por una de las restricciones que el modelo sabe
    /// rotas, y no por otra.
    /// </summary>
    private static async Task ExigirElRechazoDelMotorAsync(
        Func<Task> operacion, HashSet<string> rotas, Secuencia secuencia)
    {
        PostgresException rechazo =
            await Should.ThrowAsync<PostgresException>(operacion, secuencia.Relato());

        rechazo.ConstraintName.ShouldBeOneOf([.. rotas], secuencia.Relato());
        rechazo.SqlState.ShouldBe(rechazo.ConstraintName == NumeroDeSerieEnUnSitio ? "23505" : "23514", secuencia.Relato());
    }

    /// <summary>Un documento con fecha futura, que tiene que rechazarse sin mover nada.</summary>
    private static async Task UnaFechaFuturaAsync(
        Random azar, Random codigos, ElModuloDeInventario modulo, UnaEmpresa empresa, Secuencia secuencia)
    {
        DateOnly fecha = Hoy.AddDays(azar.Next(1, 40));
        IReadOnlyList<LineaAlAzar> lineas =
            ConSusCodigos(codigos, empresa, secuencia, [.. LineasAlAzar(azar).Select(ConSuCoste)]);

        secuencia.Anotar(Futuro, fecha, lineas);

        Guid ajusteId = await AbrirAsync(modulo, empresa, empresa.Serie, fecha, lineas);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        confirmacion.EsCorrecto.ShouldBeFalse(secuencia.Relato());
        confirmacion.Error!.Codigo.ShouldBe("ajuste-con-fecha-futura", secuencia.Relato());
    }

    /// <summary>Un recálculo con el corte en un mes cualquiera desde enero del año pasado.</summary>
    /// <remarks>
    /// El corte puede ir hacia atrás: el recálculo lo pone donde se le diga, y lo que tiene que
    /// cumplirse es lo mismo con cualquier corte.
    /// </remarks>
    private async Task UnRecalculoAsync(Random azar, UnaEmpresa empresa, Secuencia secuencia)
    {
        DateOnly corte = new DateOnly(Hoy.Year - 1, 1, 1).AddMonths(azar.Next(12 + Hoy.Month));

        secuencia.Anotar(Recalculo, corte);
        secuencia.Corte = corte;

        (await LasExistencias.RecalcularAsync(postgres, empresa.EmpresaId, corte)).ShouldBe(
            LasExistencias.DebidasEnCSharp(secuencia.Libro, corte).Count, secuencia.Relato());
    }

    /// <summary>Cierra el año pasado si está abierto, o lo reabre si está cerrado.</summary>
    /// <remarks>
    /// Un documento que se rechazó por el cierre se queda en borrador dentro del año pasado, y un
    /// ejercicio con borradores no se cierra. Desde ese momento este paso solo reabre, o se salta.
    /// </remarks>
    private static async Task UnCierreOUnaReaperturaAsync(UnaEmpresa empresa, Secuencia secuencia)
    {
        string recurso = $"{LosMaestrosPorLaApi.Ejercicios}/{empresa.AnioPasado.Id}";

        if (secuencia.PasadoCerrado)
        {
            secuencia.Anotar(Reapertura);

            using HttpResponseMessage reapertura = await empresa.Cliente.AccionarAsync(
                recurso,
                $"{recurso}/reapertura",
                HttpMethod.Post,
                JsonContent.Create(new ReabrirEjercicioDto("Reapertura del generador")));

            reapertura.StatusCode.ShouldBe(
                HttpStatusCode.NoContent, $"{await Escenario.Detalle(reapertura)}\n{secuencia.Relato()}");

            secuencia.PasadoCerrado = false;

            return;
        }

        if (secuencia.BorradoresEnElPasado > 0)
        {
            secuencia.Anotar("cierre que se salta: el año pasado tiene borradores");

            return;
        }

        secuencia.Anotar(Cierre);

        using HttpResponseMessage cierre = await empresa.Cliente.AccionarAsync(
            recurso, $"{recurso}/cierre", HttpMethod.Post);

        cierre.StatusCode.ShouldBe(
            HttpStatusCode.NoContent, $"{await Escenario.Detalle(cierre)}\n{secuencia.Relato()}");

        secuencia.PasadoCerrado = true;
    }

    /// <summary>
    /// El libro de verdad contra el modelo, las filas vivas contra la suma del modelo y las
    /// instantáneas contra las que el modelo dice que tiene que haber.
    /// </summary>
    private async Task ComprobarAsync(UnaEmpresa empresa, Secuencia secuencia)
    {
        IReadOnlyDictionary<Guid, Guid> traduccion = await LaTraduccionAsync(empresa.EmpresaId, secuencia);

        Ordenado((await LasExistencias.LibroAsync(postgres, empresa.EmpresaId))
                .Select(apunte => apunte with { Clave = EnElModelo(apunte.Clave, traduccion) }))
            .ShouldBe(Ordenado(secuencia.Libro), "el libro no es el del modelo\n" + secuencia.Relato());

        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos =
            LasExistencias.SaldosDelLibro(secuencia.Libro);

        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, empresa.EmpresaId);

        // UNA FILA VIVA POR CLAVE DEL LIBRO, Y POR CLAVE A LA QUE ALGO HA VOLADO (ítem 2.11), aunque
        // no haya llegado nunca: el envío crea la fila del destino.
        vivas.Select(viva => EnElModelo(LasExistencias.ClaveDe(viva), traduccion)).ShouldBe(
            secuencia.ClavesConFila(),
            ignoreOrder: true,
            customMessage: "una fila viva por clave\n" + secuencia.Relato());

        foreach (Existencia viva in vivas)
        {
            ClaveDeExistencia clave = EnElModelo(LasExistencias.ClaveDe(viva), traduccion);

            viva.Fisico.ShouldBe(
                saldos.GetValueOrDefault(clave), "el saldo no es la suma del libro\n" + secuencia.Relato());

            viva.EnTransito.ShouldBe(
                secuencia.Transito.GetValueOrDefault(clave),
                "lo que vuela no es lo enviado sin recibir\n" + secuencia.Relato());

            // LO DISPONIBLE NO CUENTA LO QUE VUELA (ADR-0053 §1): es del destino, pero no está.
            viva.Disponible.ShouldBe(viva.Fisico, secuencia.Relato());
        }

        // UNA SERIE, EN UN SOLO SITIO Y CON UNA UNIDAD COMO MUCHO, CONTANDO LO QUE VUELA (ADR-0053
        // §7), en las filas vivas que acaban de cuadrar con el modelo.
        Existencia[] deSerie = [.. vivas.Where(viva => viva.NumeroDeSerieId is not null)];

        deSerie.ShouldAllBe(viva => viva.Fisico + viva.EnTransito <= 1m, secuencia.Relato());

        deSerie
            .Where(viva => viva.Fisico > 0m || viva.EnTransito > 0m)
            .GroupBy(viva => (viva.ArticuloId, viva.NumeroDeSerieId))
            .ShouldAllBe(sitios => sitios.Count() == 1, "una serie en dos sitios\n" + secuencia.Relato());

        // SIN EL ORDEN DE LA LECTURA: se ordena por los identificadores del sistema, y traducidos
        // a los del modelo ya no van en orden.
        (await LasExistencias.InstantaneasAsync(postgres, empresa.EmpresaId))
            .Select(foto => foto with { Clave = EnElModelo(foto.Clave, traduccion) })
            .ShouldBe(
                LasExistencias.DebidasEnCSharp(secuencia.Libro, secuencia.Corte),
                ignoreOrder: true,
                customMessage: "las instantáneas no son las del libro\n" + secuencia.Relato());

        (_, long fueraDeRango, long enDosSitios) = await LasSeriesDelLibroAsync(empresa.EmpresaId);

        (fueraDeRango, enDosSitios).ShouldBe(
            (0L, 0L),
            "el libro sumado hasta alguna fecha deja una serie con más de una unidad en un sitio, " +
            "o en dos\n" + secuencia.Relato());

        ValoracionLeida[] valoraciones = await ValoracionesAsync(empresa.EmpresaId);

        valoraciones.ShouldBe(
            [.. secuencia.ClavesDeValoracion()
                .OrderBy(clave => clave.ArticuloId)
                .ThenBy(clave => clave.AlmacenId)
                .Select(clave => new ValoracionLeida(
                    clave.ArticuloId,
                    clave.AlmacenId,
                    secuencia.Valoracion.GetValueOrDefault(clave).Cantidad,
                    secuencia.Valoracion.GetValueOrDefault(clave).Valor,
                    secuencia.ValoracionEnTransito.GetValueOrDefault(clave).Cantidad,
                    secuencia.ValoracionEnTransito.GetValueOrDefault(clave).Valor,
                    "EUR",
                    secuencia.UltimaFechaDe(clave.ArticuloId, clave.AlmacenId)))],
            "la valoración no es la del modelo\n" + secuencia.Relato());

        // EL VALOR DE LA EMPRESA SOLO LO MUEVEN LOS AJUSTES (ADR-0053 §6), los de un recuento
        // también: la transferencia lo pasa del origen al vuelo y del vuelo al destino, sin crearlo
        // ni perderlo por el camino.
        decimal deLaEmpresa = valoraciones.Sum(fila => fila.Valor + fila.ValorEnTransito);

        deLaEmpresa.ShouldBe(
            secuencia.ValorDeLaEmpresa,
            "el valor de la empresa no es el que han metido y sacado los ajustes\n" + secuencia.Relato());

        // Y LO MISMO DICHO POR EL LIBRO, SIN EL MODELO (epílogo del 2.12): sus filas de ajuste, las de
        // un recuento entre ellas, suman el valor de la empresa; las de transferencia, lo que vuela
        // con el signo cambiado, porque lo que salió de un almacén y no ha llegado a otro no está en
        // ninguno.
        (decimal deAjustes, decimal deTransferencias) = await LoQueValeElLibroPorOrigenAsync(empresa.EmpresaId);

        deAjustes.ShouldBe(
            deLaEmpresa, "las filas de ajuste del libro no suman el valor de la empresa\n" + secuencia.Relato());

        deTransferencias.ShouldBe(
            -valoraciones.Sum(fila => fila.ValorEnTransito),
            "las filas de transferencia del libro no suman lo que vuela\n" + secuencia.Relato());

        (await LosEstadosDelLibroAsync(empresa.EmpresaId)).Imposibles.ShouldBe(
            0, "el libro sumado hasta alguna fecha deja un estado que no existió nunca\n" + secuencia.Relato());

        Ordenado(await FilasValoradasAsync(empresa.EmpresaId)).ShouldBe(
            Ordenado(secuencia.Valorado), "el libro no vale lo que dice el modelo\n" + secuencia.Relato());

        // EL CUADRE DE PRODUCCIÓN, EN CADA PASO (ítem 2.11): compara lo que vuela con las líneas
        // enviadas, que el modelo no ve. Tiene que comparar lo mismo que el modelo tiene en vuelo, y
        // no encontrar nada.
        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, empresa.EmpresaId);

        cuadre.Descuadres.ShouldBeEmpty("el cuadre encuentra lo que el modelo no\n" + secuencia.Relato());

        cuadre.ExistenciasEnTransitoComparadas.ShouldBe(
            secuencia.Transito.Count(par => par.Value != 0m), secuencia.Relato());

        cuadre.ValoracionesEnTransitoComparadas.ShouldBe(
            secuencia.ValoracionEnTransito.Count(par => par.Value.Cantidad != 0m || par.Value.Valor != 0m),
            secuencia.Relato());

        secuencia.MasExistenciasEnVuelo =
            Math.Max(secuencia.MasExistenciasEnVuelo, cuadre.ExistenciasEnTransitoComparadas);

        secuencia.MasValoracionesEnVuelo =
            Math.Max(secuencia.MasValoracionesEnVuelo, cuadre.ValoracionesEnTransitoComparadas);
    }

    /// <summary>La tabla de la valoración de la empresa, leída sin el filtro y sin el mapeo.</summary>
    private async Task<ValoracionLeida[]> ValoracionesAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            "SELECT articulo_id, almacen_id, cantidad, valor, en_transito, valor_en_transito, divisa, ultima_fecha "
            + "FROM inventario.valoraciones WHERE empresa_id = @empresa",
            conexion);

        orden.Parameters.AddWithValue("empresa", empresaId);

        List<ValoracionLeida> filas = [];

        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            filas.Add(new ValoracionLeida(
                lector.GetGuid(0),
                lector.GetGuid(1),
                lector.GetDecimal(2),
                lector.GetDecimal(3),
                lector.GetDecimal(4),
                lector.GetDecimal(5),
                lector.GetString(6),
                lector.IsDBNull(7) ? null : lector.GetFieldValue<DateOnly>(7)));
        }

        // EN C#, Y NO CON UN ORDER BY: el motor y .NET no ordenan los uuid igual.
        return [.. filas.OrderBy(fila => fila.ArticuloId).ThenBy(fila => fila.AlmacenId)];
    }

    /// <summary>
    /// Cuántas fechas tiene el libro por clave de la valoración, y en cuántas la suma hasta esa
    /// fecha deja un estado que no pudo existir (ADR-0047 §5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Contra el libro de verdad, y no contra el modelo</b>: es lo que la regla promete del libro,
    /// y el modelo confirma en el mismo orden que el motor, así que cometería el mismo error. Por
    /// clave de la valoración, que es el artículo en el almacén, y con todas las filas de la misma
    /// fecha sumadas, porque la regla deja confirmar con la misma fecha en cualquier orden.
    /// </para>
    /// <para>
    /// <b>Imposible es cantidad negativa, valor negativo o valor sin cantidad.</b> Con la regla, las
    /// filas hasta cada fecha son las de las confirmaciones hasta una de ellas, y cada confirmación
    /// deja un estado que la valoración admitió. El contraejemplo del ADR deja cero unidades y −450 €
    /// sumado hasta el día 15.
    /// </para>
    /// </remarks>
    private async Task<(long Fechas, long Imposibles)> LosEstadosDelLibroAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            """
            SELECT count(*),
                   count(*) FILTER (WHERE s.cantidad < 0 OR s.valor < 0 OR (s.cantidad = 0 AND s.valor <> 0))
              FROM (SELECT sum(sum(m.cantidad_en_unidad_base)) OVER hasta AS cantidad,
                           sum(sum(m.valor)) OVER hasta AS valor
                      FROM inventario.movimiento_stock AS m
                     WHERE m.empresa_id = @empresa
                     GROUP BY m.articulo_id, m.almacen_id, m.fecha_de_operacion
                    WINDOW hasta AS (PARTITION BY m.articulo_id, m.almacen_id ORDER BY m.fecha_de_operacion)) AS s
            """,
            conexion);

        orden.Parameters.AddWithValue("empresa", empresaId);

        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        await lector.ReadAsync();

        return (lector.GetInt64(0), lector.GetInt64(1));
    }

    /// <summary>
    /// Cuántas fechas tiene el libro por serie, y cuántas veces la suma hasta una de ellas deja la
    /// serie con una cantidad que no es cero ni uno en un sitio, o con unidades en dos (ADR-0048 §6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contra el libro de verdad, como <see cref="LosEstadosDelLibroAsync"/>, y por la misma razón:
    /// las filas de una serie hasta cada fecha son las de las confirmaciones que la movieron hasta una
    /// de ellas. Y con todas las filas de la misma fecha sumadas, porque la regla deja confirmar con
    /// la misma fecha en cualquier orden.
    /// </para>
    /// <para>
    /// <b>Con dos almacenes también</b> (ítem 2.11), aunque la fecha de uno no ordena la del otro. Lo
    /// que se ordena es la serie: dentro de un almacén, ningún documento va por detrás de su último
    /// movimiento (ADR-0047); de uno a otro, solo la mueve una transferencia, que no se recibe antes
    /// de enviarse; y los inversos van con la fecha de hoy. Mientras vuela no está en el libro de
    /// ninguno, y eso no rompe nada de lo que se cuenta aquí.
    /// </para>
    /// </remarks>
    private async Task<(long Fechas, long FueraDeRango, long EnDosSitios)> LasSeriesDelLibroAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            """
            WITH fechas AS (
                SELECT DISTINCT m.articulo_id, m.numero_de_serie_id, m.fecha_de_operacion AS fecha
                  FROM inventario.movimiento_stock AS m
                 WHERE m.empresa_id = @empresa AND m.numero_de_serie_id IS NOT NULL
            ),
            sitios AS (
                SELECT f.articulo_id, f.numero_de_serie_id, f.fecha, sum(m.cantidad_en_unidad_base) AS cantidad
                  FROM fechas AS f
                  JOIN inventario.movimiento_stock AS m
                    ON m.empresa_id = @empresa
                   AND m.articulo_id = f.articulo_id
                   AND m.numero_de_serie_id = f.numero_de_serie_id
                   AND m.fecha_de_operacion <= f.fecha
                 GROUP BY f.articulo_id, f.numero_de_serie_id, f.fecha, m.almacen_id, m.ubicacion_id
            )
            SELECT (SELECT count(*) FROM fechas),
                   (SELECT count(*) FROM sitios WHERE cantidad < 0 OR cantidad > 1),
                   (SELECT count(*)
                      FROM (SELECT 1 FROM sitios WHERE cantidad > 0
                             GROUP BY articulo_id, numero_de_serie_id, fecha HAVING count(*) > 1) AS en_dos)
            """,
            conexion);

        orden.Parameters.AddWithValue("empresa", empresaId);

        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        await lector.ReadAsync();

        return (lector.GetInt64(0), lector.GetInt64(1), lector.GetInt64(2));
    }

    /// <summary>
    /// De cada lote y cada serie de la base, el identificador que el modelo dio a su código; y exige
    /// que sean justo los del libro del modelo.
    /// </summary>
    /// <remarks>
    /// Por las tablas de los lotes y de las series, que son las que dicen qué código es cada uno. Un
    /// lote de más en la base es el de un documento rechazado que no se fue con su transacción, y
    /// uno de menos, dos códigos que el sistema ha juntado en uno.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Guid, Guid>> LaTraduccionAsync(Guid empresaId, Secuencia secuencia)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        var lotes = await contexto.Lotes
            .AsNoTracking()
            .Select(lote => new { lote.Id, lote.ArticuloId, lote.Codigo })
            .ToListAsync();

        var series = await contexto.NumerosDeSerie
            .AsNoTracking()
            .Select(serie => new { serie.Id, serie.ArticuloId, serie.Numero })
            .ToListAsync();

        Dictionary<Guid, Guid> traduccion = [];

        foreach (var lote in lotes)
        {
            traduccion.Add(lote.Id, secuencia.LoteDe(lote.ArticuloId, lote.Codigo));
        }

        foreach (var serie in series)
        {
            traduccion.Add(serie.Id, secuencia.SerieDe(serie.ArticuloId, serie.Numero));
        }

        traduccion.Values.ShouldBe(
            secuencia.Libro
                .SelectMany(apunte => new[] { apunte.Clave.LoteId, apunte.Clave.NumeroDeSerieId })
                .OfType<Guid>()
                .Distinct(),
            ignoreOrder: true,
            customMessage: "los lotes y las series de la base no son los del libro del modelo\n" + secuencia.Relato());

        return traduccion;
    }

    /// <summary>La clave leída de la base, con el lote y la serie que el modelo conoce.</summary>
    private static ClaveDeExistencia EnElModelo(ClaveDeExistencia clave, IReadOnlyDictionary<Guid, Guid> traduccion) =>
        clave with
        {
            LoteId = clave.LoteId is { } lote ? traduccion[lote] : null,
            NumeroDeSerieId = clave.NumeroDeSerieId is { } serie ? traduccion[serie] : null,
        };

    /// <summary>Lo que suman las filas del libro de la empresa de cada clase de documento.</summary>
    /// <remarks>
    /// La clase va como texto, con el nombre del enumerado: así la guarda la conversión del contexto.
    /// </remarks>
    private async Task<(decimal DeAjustes, decimal DeTransferencias)> LoQueValeElLibroPorOrigenAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            "SELECT "
            + "coalesce(sum(valor) FILTER (WHERE documento_origen_tipo = @ajuste), 0), "
            + "coalesce(sum(valor) FILTER (WHERE documento_origen_tipo = @transferencia), 0) "
            + "FROM inventario.movimiento_stock WHERE empresa_id = @empresa",
            conexion);

        orden.Parameters.AddWithValue("empresa", empresaId);
        orden.Parameters.AddWithValue("ajuste", nameof(TipoDeDocumentoOrigen.Ajuste));
        orden.Parameters.AddWithValue("transferencia", nameof(TipoDeDocumentoOrigen.Transferencia));

        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        (await lector.ReadAsync()).ShouldBeTrue();

        return (lector.GetDecimal(0), lector.GetDecimal(1));
    }

    /// <summary>El valor y el precio medio de cada fila del libro de la empresa.</summary>
    private async Task<List<FilaValorada>> FilasValoradasAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(
            "SELECT articulo_id, almacen_id, cantidad_en_unidad_base, valor, precio_medio "
            + "FROM inventario.movimiento_stock WHERE empresa_id = @empresa",
            conexion);

        orden.Parameters.AddWithValue("empresa", empresaId);

        List<FilaValorada> filas = [];

        await using NpgsqlDataReader lector = await orden.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            filas.Add(new FilaValorada(
                lector.GetGuid(0),
                lector.GetGuid(1),
                lector.GetDecimal(2),
                lector.GetDecimal(3),
                lector.GetDecimal(4)));
        }

        return filas;
    }

    private static List<LineaAlAzar> LineasAlAzar(Random azar, int minimo = 1)
    {
        int cuantas = azar.Next(minimo, 4);
        List<LineaAlAzar> lineas = [];

        for (int numero = 0; numero < cuantas; numero++)
        {
            // DOS DE CADA TRES SUMAN (ítem 2.8). Con la mitad, el stock no crece y casi todo
            // documento que saca algo choca con el cero.
            lineas.Add(UnaLineaAlAzar(azar, azar.Next(3) == 0 ? -1 : 1));
        }

        return lineas;
    }

    /// <summary>Una salida de una línea, que saca de donde hay si hay en algún sitio de A.</summary>
    /// <remarks>
    /// <para>
    /// <b>De una clave de A con existencias, si alguna tiene</b> (ítem 2.8). Con la clave al azar, la
    /// mitad de las salidas iban a una clave vacía, chocaban con el cero y había semillas que no
    /// confirmaban ninguna. Si no hay existencias en ninguna, la clave es cualquiera y choca. Desde
    /// el ítem 2.9 la clave lleva su lote o su serie, y la salida se los lleva. Desde el 2.11 hay
    /// otro almacén, B, pero los ajustes son de A, así que lo que haya en B no cuenta: si A se queda
    /// vacío, la salida choca aunque B tenga.
    /// </para>
    /// <para>
    /// <b>Y si no cabe, la mitad de las veces se queda en lo que cabe.</b> La otra mitad se intenta
    /// entera y el motor la rechaza. Cuando lo que hay es múltiplo del factor, la que se queda en lo
    /// que cabe deja la clave justo a cero, que es el borde del <c>CHECK</c> y tiene que admitirse.
    /// </para>
    /// </remarks>
    private static LineaAlAzar UnaSalidaAlAzar(Random azar, UnaEmpresa empresa, Secuencia secuencia)
    {
        LineaAlAzar salida = UnaLineaAlAzar(azar, -1);

        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(secuencia.Libro);

        // EN UN ORDEN QUE NO DEPENDE DE LOS IDENTIFICADORES, que el modelo saca nuevos en cada
        // vuelta: por la clave de la empresa y por el código.
        (LineaAlAzar Donde, decimal Saldo)[] conExistencias =
        [
            .. saldos
                .Where(par => par.Value > 0m && par.Key.AlmacenId == empresa.AlmacenId)
                .Select(par => (Donde: secuencia.LineaEn(empresa, par.Key), Saldo: par.Value))
                .OrderBy(par => par.Donde.Clave)
                .ThenBy(par => par.Donde.Lote, StringComparer.Ordinal)
                .ThenBy(par => par.Donde.Serie, StringComparer.Ordinal),
        ];

        if (conExistencias.Length == 0)
        {
            return salida;
        }

        (LineaAlAzar donde, decimal saldo) = conExistencias[azar.Next(conExistencias.Length)];

        salida = salida with { Clave = donde.Clave, Lote = donde.Lote, Serie = donde.Serie };

        decimal caben = decimal.Floor(saldo / salida.Factor);

        return -salida.Cantidad > caben && caben > 0m && azar.Next(2) == 0
            ? salida with { Cantidad = -caben }
            : salida;
    }

    private static LineaAlAzar UnaLineaAlAzar(Random azar, int signo) => new(
        azar.Next(ClavesDeLosAjustes), signo * azar.Next(1, 10), s_factores[azar.Next(s_factores.Length)]);

    /// <summary>
    /// Las líneas con el lote o la serie que pide la marca de su artículo, sorteados con su propio
    /// generador; la de la serie, además, en una unidad base.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Una línea que ya los lleva —la salida de una clave con existencias— se queda con los suyos.
    /// Una serie no se repite en el documento, porque el alta no lo admite (ADR-0048 §3): con tres
    /// líneas como mucho y tres series, siempre queda una libre. Uno de cada cuatro lotes sorteados
    /// va con espacios alrededor, que el sistema recorta.
    /// </para>
    /// <para>
    /// <b>La línea que resta toma un lote o una serie de su hueco</b>, si hay alguno, igual que la
    /// salida va a una clave con existencias. Sorteados entre los tres, casi nunca coincidían con
    /// lo que había dentro: casi todo ajuste que restaba chocaba con el cero y se llevaba por
    /// delante las líneas que sumaban, y había semillas que no valoraban ninguna entrada al precio
    /// medio. El cero lo siguen viendo la línea que saca más de lo que hay y la de un hueco vacío.
    /// </para>
    /// <para>
    /// <b>Y la mitad de las veces, la serie que entra es una que ya está</b> en cualquier sitio, que
    /// el motor rechaza por la serie. Sin eso, la serie no paraba ningún documento.
    /// </para>
    /// </remarks>
    private static List<LineaAlAzar> ConSusCodigos(
        Random codigos, UnaEmpresa empresa, Secuencia secuencia, IReadOnlyList<LineaAlAzar> lineas)
    {
        HashSet<string> usadas = [.. lineas.Select(linea => linea.Serie).OfType<string>()];
        List<LineaAlAzar> conCodigos = [];

        // EN UN ORDEN QUE NO DEPENDE DE LOS IDENTIFICADORES, como en la salida.
        LineaAlAzar[] conExistencias =
        [
            .. LasExistencias.SaldosDelLibro(secuencia.Libro)
                .Where(par => par.Value > 0m && (par.Key.LoteId is not null || par.Key.NumeroDeSerieId is not null))
                .Select(par => secuencia.LineaEn(empresa, par.Key))
                .OrderBy(donde => donde.Clave)
                .ThenBy(donde => donde.Lote, StringComparer.Ordinal)
                .ThenBy(donde => donde.Serie, StringComparer.Ordinal),
        ];

        foreach (LineaAlAzar linea in lineas)
        {
            string marca = empresa.MarcaDe(linea.Clave);

            if (marca == PorLote && linea.Lote is null)
            {
                string[] enSuHueco =
                [
                    .. conExistencias.Where(donde => donde.Clave == linea.Clave).Select(donde => donde.Lote!),
                ];

                conCodigos.Add(linea with
                {
                    Lote = linea.Cantidad < 0m && enSuHueco.Length > 0
                        ? enSuHueco[codigos.Next(enSuHueco.Length)]
                        : UnLoteAlAzar(codigos),
                });
            }
            else if (marca == PorNumeroSerie)
            {
                string[] yaDentro =
                [
                    .. conExistencias
                        .Where(donde => donde.Serie is { } serie
                            && !usadas.Contains(serie)
                            && (linea.Cantidad > 0m || donde.Clave == linea.Clave))
                        .Select(donde => donde.Serie!),
                ];

                string serie = linea.Serie
                    ?? (yaDentro.Length > 0 && (linea.Cantidad < 0m || codigos.Next(2) == 0)
                        ? yaDentro[codigos.Next(yaDentro.Length)]
                        : UnaSerieLibre(codigos, usadas));

                usadas.Add(serie);

                conCodigos.Add(linea with { Cantidad = Math.Sign(linea.Cantidad), Factor = 1m, Serie = serie });
            }
            else
            {
                conCodigos.Add(linea);
            }
        }

        return conCodigos;
    }

    private static string UnLoteAlAzar(Random codigos)
    {
        string lote = s_lotes[codigos.Next(s_lotes.Length)];

        return codigos.Next(4) == 0 ? " " + lote + " " : lote;
    }

    private static string UnaSerieLibre(Random codigos, HashSet<string> usadas)
    {
        string[] libres = [.. s_series.Where(serie => !usadas.Contains(serie))];

        return libres[codigos.Next(libres.Length)];
    }

    /// <summary>
    /// La línea con su coste si suma, salvo la de un número par de unidades de un artículo que ya
    /// tiene existencias, que va sin él y se valora al precio medio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Solo sin coste si el artículo tiene existencias ANTES del documento: dentro de él, las líneas
    /// que suman se valoran antes que las que restan, así que nada lo vacía antes de llegar a ella.
    /// Una entrada sin coste en un artículo vacío se rechaza, y eso lo mira su caso propio.
    /// </para>
    /// <para>
    /// <b>Par, y no 3, 6 o 9, desde el ítem 2.9.</b> Con tres artículos, las líneas se reparten
    /// entre más claves y hay menos confirmadas en un artículo con existencias. La semilla 460 tiene
    /// ocho —7, 1, 8, 1, 4, 5, 7 y 1 unidades— y ninguna era múltiplo de tres. La regla no toca el
    /// generador ni las clases de paso, porque la existencia no depende del coste: solo cambia qué
    /// líneas se valoran al precio medio.
    /// </para>
    /// </remarks>
    private static LineaAlAzar ConCoste(LineaAlAzar linea, UnaEmpresa empresa, Secuencia secuencia) =>
        linea.Cantidad % 2m == 0m
            && secuencia.Valoracion.GetValueOrDefault((empresa.Claves[linea.Clave].ArticuloId, empresa.AlmacenId))
                .Cantidad > 0m
            ? linea
            : ConSuCoste(linea);

    /// <summary>La línea con su coste si suma; la que resta no lo lleva nunca.</summary>
    /// <remarks>
    /// El coste sale de la propia línea, con cuatro decimales y distinto por clave, cantidad y
    /// factor. Con el de antes, 1,50 para todas, el precio medio no se movía nunca de 1,50 y ninguna
    /// fila pasaba por un redondeo.
    /// </remarks>
    private static LineaAlAzar ConSuCoste(LineaAlAzar linea) =>
        linea.Cantidad > 0m
            ? linea with
            {
                Coste = decimal.Round(
                    0.8125m + (0.3791m * linea.Clave) + (0.0617m * linea.Cantidad / linea.Factor),
                    4,
                    MidpointRounding.AwayFromZero),
            }
            : linea;

    private static async Task<Guid> AbrirAsync(
        ElModuloDeInventario modulo,
        UnaEmpresa empresa,
        SerieDto serie,
        DateOnly fecha,
        IReadOnlyList<LineaAlAzar> lineas)
    {
        AbrirAjusteDto peticion = new(
            serie.Id,
            empresa.AlmacenId,
            fecha,
            "Paso del generador",
            [.. lineas.Select(linea =>
            {
                ClaveDeExistencia clave = empresa.Claves[linea.Clave];

                return new LineaDeAjusteDto(
                    clave.UbicacionId,
                    clave.ArticuloId,
                    linea.Cantidad,
                    empresa.UnidadDe[clave.ArticuloId],
                    linea.Factor,
                    linea.Coste,
                    linea.Lote,
                    linea.Serie);
            })]);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    private static ApunteDelLibro[] Ordenado(IEnumerable<ApunteDelLibro> apuntes) =>
        [.. apuntes
            .OrderBy(apunte => apunte.Clave.ArticuloId)
            .ThenBy(apunte => apunte.Clave.AlmacenId)
            .ThenBy(apunte => apunte.Clave.UbicacionId)
            .ThenBy(apunte => apunte.Clave.LoteId)
            .ThenBy(apunte => apunte.Clave.NumeroDeSerieId)
            .ThenBy(apunte => apunte.Fecha)
            .ThenBy(apunte => apunte.Cantidad)];

    private static FilaValorada[] Ordenado(IEnumerable<FilaValorada> filas) =>
        [.. filas
            .OrderBy(fila => fila.ArticuloId)
            .ThenBy(fila => fila.AlmacenId)
            .ThenBy(fila => fila.Cantidad)
            .ThenBy(fila => fila.Valor)
            .ThenBy(fila => fila.PrecioMedio)];

    /// <summary>La fecha de hoy, en el mismo calendario y con el mismo reloj que el caso de uso.</summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(s_reloj.GetUtcNow().UtcDateTime);

    /// <summary>
    /// Una empresa con un almacén de dos ubicaciones y otro de una, tres artículos —uno por marca,
    /// seis claves en el primero y tres en el segundo— y los ejercicios de este año y del pasado,
    /// cada uno con su serie; el de este año, también con la de las transferencias y la de los
    /// recuentos.
    /// </summary>
    private async Task<UnaEmpresa> UnaEmpresaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        string codigo = string.Create(CultureInfo.InvariantCulture, $"PRO-{semilla}");

        EjercicioDto esteAnio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year);
        EjercicioDto anioPasado = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year - 1);

        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, codigo);
        SerieDto delAnioPasado =
            await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, anioPasado.Id, codigo + "-P");
        SerieDto deTransferencias = await LosMaestrosPorLaApi.CrearSerieEnAsync(
            cliente, esteAnio.Id, codigo + "-TR", TipoDeDocumento.TransferenciaDeInventario);
        SerieDto deRecuentos = await LosMaestrosPorLaApi.CrearSerieEnAsync(
            cliente, esteAnio.Id, codigo + "-RC", TipoDeDocumento.RecuentoDeInventario);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);
        AlmacenDto almacenB = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo + "-B");

        Guid[] ubicaciones =
        [
            (await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01")).Id,
            (await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-02")).Id,
        ];

        Guid b1 = (await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacenB.Id, codigo + "-B1")).Id;

        (Guid primero, Guid unidadDelPrimero) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semilla + 10);

        (Guid segundo, Guid unidadDelSegundo) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semilla + 20, PorLote);

        (Guid tercero, Guid unidadDelTercero) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semilla + 130, PorNumeroSerie);

        return new UnaEmpresa(
            cliente,
            empresa.Id,
            almacen.Id,
            almacenB.Id,
            [
                new ClaveDeExistencia(primero, almacen.Id, ubicaciones[0]),
                new ClaveDeExistencia(primero, almacen.Id, ubicaciones[1]),
                new ClaveDeExistencia(segundo, almacen.Id, ubicaciones[0]),
                new ClaveDeExistencia(segundo, almacen.Id, ubicaciones[1]),
                new ClaveDeExistencia(tercero, almacen.Id, ubicaciones[0]),
                new ClaveDeExistencia(tercero, almacen.Id, ubicaciones[1]),
                new ClaveDeExistencia(primero, almacenB.Id, b1),
                new ClaveDeExistencia(segundo, almacenB.Id, b1),
                new ClaveDeExistencia(tercero, almacenB.Id, b1),
            ],
            new Dictionary<Guid, Guid>
            {
                [primero] = unidadDelPrimero,
                [segundo] = unidadDelSegundo,
                [tercero] = unidadDelTercero,
            },
            new Dictionary<Guid, string> { [primero] = "Ninguna", [segundo] = PorLote, [tercero] = PorNumeroSerie },
            serie,
            delAnioPasado,
            anioPasado,
            deTransferencias,
            deRecuentos);
    }

    /// <summary>Una línea tal como la saca el generador.</summary>
    /// <param name="Clave">
    /// Cuál de las nueve claves de la empresa: de la 0 a la 5, las de A; de la 6 a la 8, las de B.
    /// </param>
    /// <param name="Cantidad">La cantidad tecleada, con signo.</param>
    /// <param name="Factor">El factor a unidad base.</param>
    /// <param name="Coste">El coste por unidad base, o nada si resta o va al precio medio.</param>
    /// <param name="Lote">El lote tal como se teclea, o nada.</param>
    /// <param name="Serie">El número de serie, o nada.</param>
    private sealed record LineaAlAzar(
        int Clave, decimal Cantidad, decimal Factor, decimal? Coste = null, string? Lote = null, string? Serie = null);

    /// <summary>Una línea tal como la valora el modelo.</summary>
    /// <param name="ArticuloId">El artículo.</param>
    /// <param name="AlmacenId">El almacén: la valoración es del artículo en cada uno.</param>
    /// <param name="Cantidad">La cantidad en unidad base, con signo.</param>
    /// <param name="Coste">El coste por unidad base, o nada.</param>
    /// <param name="Compensa">Lo que compensa si es la de un inverso o la de una transferencia, o nada.</param>
    private sealed record LineaQueSeValora(
        Guid ArticuloId, Guid AlmacenId, decimal Cantidad, decimal? Coste, decimal? Compensa);

    /// <summary>Una fila del libro con lo que vale.</summary>
    /// <param name="ArticuloId">El artículo.</param>
    /// <param name="AlmacenId">El almacén.</param>
    /// <param name="Cantidad">La cantidad en unidad base, con signo.</param>
    /// <param name="Valor">Lo que movió, con signo.</param>
    /// <param name="PrecioMedio">El precio medio que congeló.</param>
    private sealed record FilaValorada(
        Guid ArticuloId, Guid AlmacenId, decimal Cantidad, decimal Valor, decimal PrecioMedio);

    /// <summary>Una línea de una transferencia, tal como la lleva el modelo.</summary>
    /// <param name="Origen">La clave de donde sale, con su lote o su serie.</param>
    /// <param name="Destino">La clave a donde va, con el mismo lote o la misma serie.</param>
    /// <param name="Cantidad">Cuánto, en unidad base.</param>
    /// <param name="Valor">Lo que vuela con ella: lo que se llevó del origen.</param>
    private sealed record LineaEnVuelo(
        ClaveDeExistencia Origen, ClaveDeExistencia Destino, decimal Cantidad, decimal Valor);

    /// <summary>Una transferencia enviada o recibida, tal como la lleva el modelo.</summary>
    /// <param name="Id">Su identificador en el sistema.</param>
    /// <param name="Origen">El almacén de donde sale.</param>
    /// <param name="Destino">El almacén a donde va.</param>
    /// <param name="FechaDeEnvio">El día en que salió.</param>
    /// <param name="Lineas">Lo que lleva, con su valor.</param>
    private sealed record TransferenciaDelModelo(
        Guid Id, Guid Origen, Guid Destino, DateOnly FechaDeEnvio, LineaEnVuelo[] Lineas);

    /// <summary>Un recuento en curso, tal como lo lleva el modelo.</summary>
    /// <param name="id">Su identificador en el sistema.</param>
    /// <param name="almacenId">El almacén que se cuenta.</param>
    /// <param name="lineas">Las que trajo la precarga, en su orden.</param>
    private sealed class RecuentoDelModelo(Guid id, Guid almacenId, IReadOnlyList<LineaDelRecuento> lineas)
    {
        internal Guid Id { get; } = id;

        internal Guid AlmacenId { get; } = almacenId;

        /// <summary>Las de la precarga, y detrás las que se añaden a mano.</summary>
        internal List<LineaDelRecuento> Lineas { get; } = [.. lineas];

        /// <summary>La huella de la última vez que se leyó la ficha: la que mandaría quien confirma.</summary>
        internal string? HuellaVista { get; private set; }

        /// <summary>El teórico de cada línea cuando se leyó esa huella, según el libro del modelo.</summary>
        internal Dictionary<Guid, decimal> TeoricoVisto { get; } = [];

        /// <summary>Lo que ve quien lee la ficha: su huella, y el teórico que tienen ahora sus líneas.</summary>
        internal void Ver(string huella, IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos)
        {
            HuellaVista = huella;

            foreach (LineaDelRecuento linea in Lineas)
            {
                TeoricoVisto[linea.Id] = saldos.GetValueOrDefault(linea.Clave);
            }
        }
    }

    /// <summary>Una línea de un recuento en curso, tal como la lleva el modelo.</summary>
    /// <param name="id">Su identificador en el sistema.</param>
    /// <param name="numero">Su orden en el recuento.</param>
    /// <param name="clave">La clave del modelo que cuenta, con su lote o su serie.</param>
    private sealed class LineaDelRecuento(Guid id, int numero, ClaveDeExistencia clave)
    {
        internal Guid Id { get; } = id;

        internal int Numero { get; } = numero;

        internal ClaveDeExistencia Clave { get; } = clave;

        /// <summary>Lo contado, o nada si no se ha contado.</summary>
        internal decimal? Contado { get; set; }

        /// <summary>Si un rechazo de la confirmación pide volver a contarla.</summary>
        internal bool PorRecontar { get; set; }
    }

    /// <summary>Una fila de la valoración, tal como se lee de la tabla.</summary>
    /// <param name="ArticuloId">El artículo.</param>
    /// <param name="AlmacenId">El almacén.</param>
    /// <param name="Cantidad">Lo que hay.</param>
    /// <param name="Valor">Lo que vale lo que hay.</param>
    /// <param name="EnTransito">Lo que vuela hacia el almacén.</param>
    /// <param name="ValorEnTransito">Lo que vale lo que vuela.</param>
    /// <param name="Divisa">La divisa.</param>
    /// <param name="UltimaFecha">La fecha de su último movimiento, o nada.</param>
    private sealed record ValoracionLeida(
        Guid ArticuloId,
        Guid AlmacenId,
        decimal Cantidad,
        decimal Valor,
        decimal EnTransito,
        decimal ValorEnTransito,
        string Divisa,
        DateOnly? UltimaFecha);

    /// <summary>Los maestros de la empresa del caso.</summary>
    private sealed record UnaEmpresa(
        HttpClient Cliente,
        Guid EmpresaId,
        Guid AlmacenId,
        Guid AlmacenB,
        IReadOnlyList<ClaveDeExistencia> Claves,
        IReadOnlyDictionary<Guid, Guid> UnidadDe,
        IReadOnlyDictionary<Guid, string> Marcas,
        SerieDto Serie,
        SerieDto SerieDelAnioPasado,
        EjercicioDto AnioPasado,
        SerieDto SerieDeTransferencias,
        SerieDto SerieDeRecuentos)
    {
        /// <summary>La marca del artículo de una de las claves.</summary>
        internal string MarcaDe(int clave) => Marcas[Claves[clave].ArticuloId];

        /// <summary>Cuál de las nueve claves es esta, sin su lote ni su serie.</summary>
        internal int IndiceDe(ClaveDeExistencia clave)
        {
            ClaveDeExistencia sinCodigos = clave with { LoteId = null, NumeroDeSerieId = null };

            return Enumerable.Range(0, Claves.Count).Single(indice => Claves[indice] == sinCodigos);
        }

        /// <summary>
        /// A qué clave del otro almacén llega lo que sale de esta, con su lote o su serie: al único
        /// hueco de B, o a uno de los dos de A.
        /// </summary>
        internal ClaveDeExistencia DestinoDe(ClaveDeExistencia origen, Random traslados)
        {
            int indice = IndiceDe(origen);
            int articulo = indice < ClavesDeLosAjustes ? indice / 2 : indice - ClavesDeLosAjustes;

            ClaveDeExistencia hueco = origen.AlmacenId == AlmacenId
                ? Claves[ClavesDeLosAjustes + articulo]
                : Claves[(2 * articulo) + traslados.Next(2)];

            return hueco with { LoteId = origen.LoteId, NumeroDeSerieId = origen.NumeroDeSerieId };
        }
    }

    /// <summary>El modelo: lo que el libro debería tener, y el relato de cómo se llegó ahí.</summary>
    /// <param name="semilla">La semilla del generador, que es la primera línea del relato.</param>
    private sealed class Secuencia(int semilla)
    {
        private readonly List<string> _pasos = [];

        private readonly Dictionary<(string Que, Guid ArticuloId, string Codigo), Guid> _identificadores = [];

        private readonly Dictionary<Guid, string> _codigos = [];

        internal List<ApunteDelLibro> Libro { get; } = [];

        internal List<(Guid AjusteId, ApunteDelLibro[] Apuntes, FilaValorada[] Valoradas)> Anulables { get; } = [];

        /// <summary>
        /// La valoración de cada artículo en cada almacén donde se ha movido: su cantidad y su valor.
        /// </summary>
        internal Dictionary<(Guid ArticuloId, Guid AlmacenId), (decimal Cantidad, decimal Valor)> Valoracion { get; } = [];

        /// <summary>Cada fila del libro con lo que vale.</summary>
        internal List<FilaValorada> Valorado { get; } = [];

        /// <summary>
        /// Lo que vuela hacia cada clave del destino (ADR-0053 §1). Una clave a la que algo ha volado
        /// se queda aunque baje a cero: su fila viva tampoco se borra.
        /// </summary>
        internal Dictionary<ClaveDeExistencia, decimal> Transito { get; } = [];

        /// <summary>Lo que vuela hacia cada artículo en cada almacén: su cantidad y su valor.</summary>
        internal Dictionary<(Guid ArticuloId, Guid AlmacenId), (decimal Cantidad, decimal Valor)> ValoracionEnTransito { get; } = [];

        /// <summary>Las transferencias enviadas que no han llegado ni se han anulado.</summary>
        internal List<TransferenciaDelModelo> Enviadas { get; } = [];

        /// <summary>Las recibidas que no se han anulado.</summary>
        internal List<TransferenciaDelModelo> Recibidas { get; } = [];

        /// <summary>El recuento abierto y sin cerrar, o nada: el generador lleva uno a la vez.</summary>
        internal RecuentoDelModelo? RecuentoEnCurso { get; set; }

        /// <summary>
        /// Los recuentos confirmados que no se han anulado, con las filas de su ajuste y lo que valieron;
        /// sin filas si todo cuadraba y no hubo ajuste.
        /// </summary>
        internal List<(Guid RecuentoId, ApunteDelLibro[] Apuntes, FilaValorada[] Valoradas)> RecuentosAnulables { get; } = [];

        /// <summary>Las claves de un recuento confirmado cuyo físico se ha leído igual a lo contado.</summary>
        internal int ClavesContadasComprobadas { get; set; }

        /// <summary>Los recuentos confirmados que han generado su ajuste.</summary>
        internal int AjustesDeRecuento { get; set; }

        /// <summary>
        /// El valor que han metido y sacado los ajustes y sus anulaciones, los de un recuento también:
        /// el de la empresa, porque la transferencia no lo crea ni lo pierde (ADR-0053 §6).
        /// </summary>
        internal decimal ValorDeLaEmpresa { get; set; }

        /// <summary>Las más claves en vuelo que ha comparado el cuadre tras un paso.</summary>
        internal long MasExistenciasEnVuelo { get; set; }

        /// <summary>Las más valoraciones en vuelo que ha comparado el cuadre tras un paso.</summary>
        internal long MasValoracionesEnVuelo { get; set; }

        /// <summary>Las líneas de serie que han salido en un envío confirmado.</summary>
        internal int SeriesQueHanVolado { get; set; }

        /// <summary>Las líneas de un envío confirmado con su código tecleado con espacios.</summary>
        internal int CodigosConEspacios { get; set; }

        internal int AlPrecioMedio { get; set; }

        internal HashSet<string> Clases { get; } = [];

        internal DateOnly? Corte { get; set; }

        internal bool PasadoCerrado { get; set; }

        internal int BorradoresEnElPasado { get; set; }

        /// <summary>
        /// La fecha más alta del libro del modelo para el artículo en el almacén, o nada si no se ha
        /// movido allí: es la que guarda su valoración (ADR-0047 §1). Lo que vuela no la mueve
        /// (ADR-0053 §3).
        /// </summary>
        internal DateOnly? UltimaFechaDe(Guid articuloId, Guid almacenId) =>
            Libro.Where(apunte => apunte.Clave.ArticuloId == articuloId && apunte.Clave.AlmacenId == almacenId)
                .Select(apunte => (DateOnly?)apunte.Fecha)
                .Max();

        /// <summary>
        /// Las claves que tienen fila viva: las del libro y las que algo ha ido a buscar, aunque no
        /// haya llegado nunca. El envío crea la del destino, y nada la borra.
        /// </summary>
        internal HashSet<ClaveDeExistencia> ClavesConFila() =>
            [.. Libro.Select(apunte => apunte.Clave), .. Transito.Keys];

        /// <summary>
        /// Los pares de artículo y almacén que tienen fila en la valoración: los que se han valorado y
        /// los destinos de un envío, que los crea al bloquearlos.
        /// </summary>
        internal HashSet<(Guid ArticuloId, Guid AlmacenId)> ClavesDeValoracion() =>
            [.. Valoracion.Keys, .. ValoracionEnTransito.Keys];

        /// <summary>El código que el modelo dio a un lote o a una serie.</summary>
        internal string CodigoDe(Guid id) => _codigos[id];

        /// <summary>Lo que volaría si estas líneas despegaran (1) o dejaran de volar (−1).</summary>
        internal Dictionary<ClaveDeExistencia, decimal> TransitoTras(IEnumerable<LineaEnVuelo> lineas, int signo)
        {
            Dictionary<ClaveDeExistencia, decimal> tras = new(Transito);

            foreach (LineaEnVuelo linea in lineas)
            {
                tras[linea.Destino] = tras.GetValueOrDefault(linea.Destino) + (signo * linea.Cantidad);
            }

            return tras;
        }

        /// <summary>
        /// Las líneas despegan (1) o dejan de volar (−1), porque llegan o porque se anulan: en la
        /// clave del destino y en su valoración, con su cantidad y su valor.
        /// </summary>
        internal void Volar(IEnumerable<LineaEnVuelo> lineas, int signo)
        {
            foreach (LineaEnVuelo linea in lineas)
            {
                (Guid, Guid) donde = (linea.Destino.ArticuloId, linea.Destino.AlmacenId);
                (decimal cantidad, decimal valor) = ValoracionEnTransito.GetValueOrDefault(donde);

                Transito[linea.Destino] = Transito.GetValueOrDefault(linea.Destino) + (signo * linea.Cantidad);
                ValoracionEnTransito[donde] = (cantidad + (signo * linea.Cantidad), valor + (signo * linea.Valor));
            }
        }

        /// <summary>
        /// El identificador que el modelo da a un lote: uno por artículo y código, recortado como
        /// lo recorta el sistema y con su caja (ADR-0048 §2).
        /// </summary>
        internal Guid LoteDe(Guid articuloId, string codigo) => IdentificadorDe("lote", articuloId, codigo);

        /// <summary>El identificador que el modelo da a un número de serie, igual que al lote.</summary>
        internal Guid SerieDe(Guid articuloId, string numero) => IdentificadorDe("serie", articuloId, numero);

        /// <summary>La clave del modelo para una línea: la de la empresa, con su lote o su serie.</summary>
        internal ClaveDeExistencia ClaveDe(UnaEmpresa empresa, LineaAlAzar linea)
        {
            ClaveDeExistencia clave = empresa.Claves[linea.Clave];

            return clave with
            {
                LoteId = linea.Lote is { } lote ? LoteDe(clave.ArticuloId, lote) : null,
                NumeroDeSerieId = linea.Serie is { } serie ? SerieDe(clave.ArticuloId, serie) : null,
            };
        }

        /// <summary>Una línea vacía en una clave del modelo, con los códigos de su lote o su serie.</summary>
        internal LineaAlAzar LineaEn(UnaEmpresa empresa, ClaveDeExistencia clave) => new(
            empresa.IndiceDe(clave),
            0m,
            1m,
            Lote: clave.LoteId is { } lote ? _codigos[lote] : null,
            Serie: clave.NumeroDeSerieId is { } serie ? _codigos[serie] : null);

        internal void Anotar(string clase, DateOnly fecha, IReadOnlyList<LineaAlAzar> lineas) =>
            Anotar(clase, string.Create(
                CultureInfo.InvariantCulture,
                $"{fecha:yyyy-MM-dd} [{string.Join(", ", lineas.Select(Describir))}]"));

        internal void Anotar(string clase, ApunteDelLibro[] apuntes) =>
            Anotar(clase, string.Create(
                CultureInfo.InvariantCulture,
                $"de {apuntes.Length} filas del {apuntes[0].Fecha:yyyy-MM-dd}"));

        internal void Anotar(string clase, DateOnly corte) =>
            Anotar(clase, string.Create(CultureInfo.InvariantCulture, $"hasta {corte:yyyy-MM}"));

        internal void Anotar(string clase, string detalle = "")
        {
            if (s_clasesDePaso.Contains(clase))
            {
                Clases.Add(clase);
            }

            _pasos.Add(string.Create(
                CultureInfo.InvariantCulture, $"{_pasos.Count + 1,2}. {clase} {detalle}"));
        }

        /// <summary>
        /// La segunda cuenta de la valoración: el precio medio ponderado escrito aquí desde el
        /// ADR-0046, con decimales y redondeos a mano. Apunta las filas y mueve la valoración.
        /// </summary>
        /// <remarks>
        /// Primero las líneas que suman y después las que restan, cada grupo en su orden. La que
        /// suma mueve lo que compensa, o su coste por la cantidad, o el precio medio de antes por la
        /// cantidad, y congela el precio medio de después. La que resta congela el de antes y mueve
        /// lo que compensa, o ese precio por la cantidad; salvo si vacía el artículo o se llevaría
        /// más de lo que hay, y entonces se lleva todo lo que hay.
        /// </remarks>
        internal FilaValorada[] Valorar(IReadOnlyList<LineaQueSeValora> lineas)
        {
            var filas = new FilaValorada[lineas.Count];

            IEnumerable<int> enOrden = Enumerable.Range(0, lineas.Count)
                .Where(indice => lineas[indice].Cantidad > 0m)
                .Concat(Enumerable.Range(0, lineas.Count).Where(indice => lineas[indice].Cantidad <= 0m));

            foreach (int indice in enOrden)
            {
                LineaQueSeValora linea = lineas[indice];
                (Guid, Guid) donde = (linea.ArticuloId, linea.AlmacenId);
                (decimal cantidad, decimal valor) = Valoracion.GetValueOrDefault(donde);
                decimal? antes = cantidad > 0m ? Redondear(valor / cantidad, 6) : null;
                decimal despues = cantidad + linea.Cantidad;

                if (linea.Cantidad > 0m)
                {
                    decimal suma = linea.Compensa ?? Redondear(
                        (linea.Coste ?? antes ?? throw new InvalidOperationException(
                            "El generador ha sacado una entrada sin coste en un artículo vacío.")) * linea.Cantidad,
                        4);

                    filas[indice] = new FilaValorada(
                        linea.ArticuloId,
                        linea.AlmacenId,
                        linea.Cantidad,
                        suma,
                        Redondear((valor + suma) / despues, 6));
                }
                else
                {
                    decimal congelado = antes ?? 0m;
                    decimal dice = linea.Compensa is { } compensa
                        ? -compensa
                        : Redondear(congelado * -linea.Cantidad, 4);

                    filas[indice] = new FilaValorada(
                        linea.ArticuloId,
                        linea.AlmacenId,
                        linea.Cantidad,
                        despues <= 0m || dice > valor ? -valor : -dice,
                        congelado);
                }

                Valoracion[donde] = (despues, valor + filas[indice].Valor);
            }

            Valorado.AddRange(filas);

            return filas;
        }

        internal string Relato() =>
            string.Create(CultureInfo.InvariantCulture, $"semilla {semilla}:\n") +
            string.Join("\n", _pasos);

        private static string Describir(LineaAlAzar linea) =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"clave {linea.Clave}: {linea.Cantidad:+0;-0} x {linea.Factor}")
            + (linea.Coste is { } coste
                ? string.Create(CultureInfo.InvariantCulture, $" a {coste}")
                : linea.Cantidad > 0m ? " al precio medio" : string.Empty)
            + (linea.Lote is { } lote ? " lote «" + lote + "»" : string.Empty)
            + (linea.Serie is { } serie ? " serie «" + serie + "»" : string.Empty);

        private Guid IdentificadorDe(string que, Guid articuloId, string codigo)
        {
            string recortado = codigo.Trim();

            if (!_identificadores.TryGetValue((que, articuloId, recortado), out Guid id))
            {
                id = Guid.CreateVersion7();
                _identificadores.Add((que, articuloId, recortado), id);
                _codigos.Add(id, recortado);
            }

            return id;
        }

        private static decimal Redondear(decimal cantidad, int decimales) =>
            decimal.Round(cantidad, decimales, MidpointRounding.AwayFromZero);
    }
}
