using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Domain.LotesYSeries;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Infrastructure.Corte;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La R3 en su forma entera: el saldo es la suma del libro, y todo lo demás es una copia que se
/// puede tirar (ítem 2.7, ADR-0044).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tres copias, y cada una con su caso.</b> La fila viva —una por clave— se mueve en la misma
/// sentencia y la misma transacción que escriben el libro. La instantánea mensual es el saldo al
/// acabar cada mes, y se puede borrar y recalcular sin que cambie un número. Y el cuadre las
/// recorre las dos contra el libro, y dice cuántas ha comparado además de lo que no cuadra.
/// </para>
/// <para>
/// <b>Las confirmaciones van por el caso de uso de verdad</b>, con sus adaptadores y su
/// transacción, y no escribiendo el libro a mano: lo que se afirma es que la copia se mueve sola
/// cuando el sistema anota, y un <c>INSERT</c> en el libro no es algo que el sistema haga. Lo único
/// que se escribe por debajo del sistema son las <b>copias estropeadas</b> del caso del cuadre, y
/// van dentro de una transacción que se deshace.
/// </para>
/// <para>
/// <b>Semillas: el fichero entero es del 440 al 459, del 858 al 865, y la 873, la 874, la 875, la
/// 899, la 900 y la 923.</b> Las empresas, del 440 al 449; los maestros de instalación, del 450 al 459. El
/// caso de las dos empresas se lleva la 447 y la 448, y la 457 y la 458; el de las dos salidas del
/// ítem 2.8, la 449 y la 459. Los tres del cerrojo de la empresa, del 2.14, del 858 al 862, empresa
/// y maestros con el mismo número: el bloque estaba libre, y lo dice <c>LasReservasTests</c>. Los
/// dos del avance del corte, también del 2.14, la 863 y la 864 del mismo modo, y la 865, una
/// empresa sin existencias. El del corte de cada empresa, del 2.15, la 873 y la 874, del mismo
/// modo: eran las primeras libres detrás de las de <c>ElSaldoAUnaFechaTests</c>. Los tres del 2.18,
/// la 875, la 899 y la 900, también del mismo modo: la 875 y la 899 son las dos que el bloque del
/// 2.17 dejó libres a sus lados, y la 900, la primera detrás del bloque que <c>LasReservasTests</c>
/// buscó libre, del 830 al 899. Del 901 al 905 son de la importación, que lleva delante 33 000 000.
/// El del JIT del 2.20, la 923, del mismo modo: la primera libre detrás del bloque del 2.19, que
/// acaba en la 922.
/// </para>
/// <para>
/// <b>El cerrojo de las instantáneas es de la empresa desde el 2.14</b> (ADR-0063 §5 y §6), y tiene
/// tres casos además del que ya tenía, cada uno con dos transacciones de verdad: el recálculo de una
/// empresa no frena a otra; sí frena a una confirmación de la suya; y dos confirmaciones de la misma
/// empresa no se frenan entre sí. Para tener un recálculo a medias, el caso le coge antes la fila del
/// corte: el recálculo toma su cerrojo, se para en esa fila, y se queda dentro hasta que el caso la
/// suelta.
/// </para>
/// <para>
/// <b>La instantánea lleva su valor desde el 2.14</b> (ADR-0063 §1 y §2): el caso de la reposición
/// lo compara con una segunda cuenta en C# después de la sentencia del libro y después de
/// recalcular, el del cuadre lo estropea con la cantidad bien, y el de la migración comprueba el
/// relleno de las filas que ya hubiera, en una base propia y sin semilla.
/// </para>
/// <para>
/// <b>Y quien la mantiene en producción, desde el 2.14</b> (ADR-0063 §7), es el trabajo mensual que
/// avanza el corte. Su avance se prueba aquí llamándolo con su empresa, por el contenedor del
/// anfitrión, y su bucle, sin base, en el carril rápido. Que este anfitrión no lo arranque se mira en
/// sus servicios: si lo arrancara, movería el corte de los demás casos.
/// </para>
/// <para>
/// <b>Desde el 2.18 avanza un mes cada vez</b>, y cuadra detrás lo que no esté cuadrado (ADR-0067).
/// Los casos lo llaman como el trabajo: mes a mes hasta que el corte llega, en un solo ámbito, y el
/// cuadre de lo pendiente después. Uno mete una clave nueva entre dos meses de una puesta al día,
/// otro rompe un cuadre a medias y lo sigue en la vuelta siguiente, y el último cruza un recálculo
/// con un cuadre, que no anota lo que comparó. Desde el 2.20, uno más mira que cada mes del cuadre
/// se lea sin el JIT del motor, y que la marca lo encuentre como estaba.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasExistenciasSonLaSumaDelLibroTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
    /// <summary>La migración anterior a la del valor, donde se para el caso del relleno.</summary>
    private const string LaDeAntesDelValor = "20261009150913_LasReservas";

    /// <summary>La migración del valor de la instantánea.</summary>
    private const string LaDelValor = "20261013123740_ElValorDeLaInstantanea";

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

    [Fact]
    public async Task Dos_lineas_de_la_misma_clave_y_otro_documento_dejan_una_sola_fila_viva_con_la_suma()
    {
        ElAlmacenDelCaso caso = await UnAlmacenAsync(440, "EXI-A", 450, factores: [2m, 1.5m]);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        // DOS LÍNEAS DE LA MISMA CLAVE EN EL MISMO DOCUMENTO, que la sentencia agrupa antes de
        // tocar la fila: sin agrupar, el mismo INSERT querría actualizar dos veces la misma fila y
        // PostgreSQL lo rechaza. Y un factor que no es uno, para que se vea que lo que se suma es
        // la cantidad en unidad base y no la que se tecleó.
        await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 4m, 2m), new Linea(0, -1m, 2m));

        // Y UN SEGUNDO DOCUMENTO, que es el que encuentra la fila ya creada: es el camino del
        // ON CONFLICT, el que suma a lo que había en vez de ponerlo.
        await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 3m, 1.5m));

        IReadOnlyList<ApunteDelLibro> libro = await LasExistencias.LibroAsync(postgres, caso.EmpresaId);
        libro.Count.ShouldBe(3, "tres líneas, tres filas del libro: la existencia no se come ninguna");

        Existencia viva = (await LasExistencias.VivasAsync(postgres, caso.EmpresaId)).ShouldHaveSingleItem(
            "una sola fila viva por clave, aunque la muevan tres líneas y dos documentos");

        viva.Fisico.ShouldBe(
            libro.Sum(apunte => apunte.Cantidad),
            "el saldo es la suma del libro: 8 - 2 + 4,5");

        viva.Fisico.ShouldBe(10.5m);
        viva.LoteId.ShouldBeNull("el libro todavía no lleva lote: lo trae el ítem 2.9");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(1, "ha comparado la única clave que hay");
        cuadre.InstantaneasComparadas.ShouldBe(0, "sin corte no hay instantáneas que comparar");
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dos_confirmaciones_a_la_vez_sobre_la_misma_clave_suman_las_dos()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(442);

        // DOS SERIES DEL MISMO EJERCICIO, y es lo que hace que el caso ejerza la fila viva y no
        // otra cosa: con una sola serie, las dos confirmaciones se pararían antes en el contador
        // de la serie, y la segunda llegaría a la existencia con la primera ya confirmada.
        EjercicioDto esteAnio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year);
        SerieDto primera = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-C1");
        SerieDto segunda = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-C2");

        ElAlmacenDelCaso caso = await LosMaestrosDeAsync(cliente, empresa.Id, "EXI-C", 452, primera);

        await using ElModuloDeInventario unos = new(postgres, caso.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, caso.EmpresaId);

        // LA FILA YA EXISTE, y así las dos van por la actualización. Es donde se esconde el fallo
        // caro: leer la fila y escribir lo leído más lo nuevo pierde en silencio lo que la otra
        // sumó, mientras que sin fila previa el mismo fallo revienta con un nulo y se ve solo.
        await ConfirmarAsync(unos, caso, Hoy, new Linea(0, 10m));

        Guid delPrimero = await AbrirAsync(unos, caso, primera.Id, Hoy, [new Linea(0, 3m)]);
        Guid delSegundo = await AbrirAsync(otros, caso, segunda.Id, Hoy, [new Linea(0, 4m)]);

        // TRANSACCIÓN 1: confirma, mueve la fila viva y NO suelta.
        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await unos.ConfirmarYQuedarseDentroAsync(delPrimero);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            // TRANSACCIÓN 2: la otra serie, la misma clave. Se para en la fila viva, que es lo
            // único que las dos comparten, y el caso no suelta a la primera hasta que el motor dice
            // que la segunda ya está esperando.
            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(delSegundo);

            await EsperarAQueLaFreneAsync(unos.ProcesoDeLaBase, laOtra);

            await enVuelo.CommitAsync();

            Resultado<AjusteDto> segundaConfirmacion = await laOtra.WaitAsync(TimeSpan.FromSeconds(30));

            segundaConfirmacion.EsCorrecto.ShouldBeTrue($"«{segundaConfirmacion.Error?.Codigo}»");
        }

        (await LasExistencias.VivasAsync(postgres, caso.EmpresaId))
            .ShouldHaveSingleItem()
            .Fisico.ShouldBe(
                17m,
                "10 de antes, más 3 de la primera, más 4 de la segunda: la segunda suma sobre lo que " +
                "la primera dejó al confirmarse, no sobre lo que había cuando empezó");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(1);
        cuadre.Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// Dos salidas a la vez que caben una a una y no juntas: una confirma, la otra choca con la
    /// restricción del motor, y el físico no baja de cero en ningún momento (ítem 2.8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el caso por el que la guarda está en el motor y no en el dominio</b> (ADR-0046 §4). Con
    /// una comprobación previa, las dos leerían 10, las dos verían que 6 caben y las dos
    /// escribirían: el físico acabaría en −2. El <c>CHECK</c> se evalúa sobre la fila ya bloqueada y
    /// con la cantidad ya sumada, así que la segunda, al despertar, suma sobre el 4 que dejó la
    /// primera y choca.
    /// </para>
    /// <para>
    /// <b>Se afirma el nombre de la restricción y no solo el código</b>, por lo mismo que en la
    /// carrera del inverso: es el nombre que el borde traduce a <c>422</c>
    /// <c>stock-insuficiente</c>, y el carril funcional afirma el mismo, escrito a mano.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Dos_salidas_a_la_vez_que_caben_una_a_una_y_no_juntas_dejan_pasar_solo_una()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(449);

        // DOS SERIES, como en el caso de las dos entradas: con una sola, la segunda se pararía en
        // el contador y llegaría a la existencia con la primera ya confirmada.
        EjercicioDto esteAnio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year);
        SerieDto primera = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-J1");
        SerieDto segunda = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-J2");

        ElAlmacenDelCaso caso = await LosMaestrosDeAsync(cliente, empresa.Id, "EXI-J", 459, primera);

        await using ElModuloDeInventario unos = new(postgres, caso.EmpresaId);
        await using ElModuloDeInventario otros = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(unos, caso, Hoy, new Linea(0, 10m));

        Guid delPrimero = await AbrirAsync(unos, caso, primera.Id, Hoy, [new Linea(0, -6m)]);
        Guid delSegundo = await AbrirAsync(otros, caso, segunda.Id, Hoy, [new Linea(0, -6m)]);

        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await unos.ConfirmarYQuedarseDentroAsync(delPrimero);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(delSegundo);

            await EsperarAQueLaFreneAsync(unos.ProcesoDeLaBase, laOtra);

            await enVuelo.CommitAsync();

            PostgresException rechazo = await Should.ThrowAsync<PostgresException>(
                () => laOtra.WaitAsync(TimeSpan.FromSeconds(30)));

            rechazo.SqlState.ShouldBe("23514", rechazo.MessageText);
            rechazo.ConstraintName.ShouldBe(
                "ck_existencias_fisico_no_negativo",
                "es el nombre que el borde traduce a 422: con otro, la segunda salida saldría 500");
        }

        (await LasExistencias.VivasAsync(postgres, caso.EmpresaId))
            .ShouldHaveSingleItem()
            .Fisico.ShouldBe(4m, "10 menos los 6 de la primera; la segunda no ha movido nada");

        IReadOnlyList<ApunteDelLibro> libro = await LasExistencias.LibroAsync(postgres, caso.EmpresaId);

        libro.Select(apunte => apunte.Cantidad).ShouldBe(
            [10m, -6m],
            ignoreOrder: true,
            "la entrada y la primera salida: de la segunda no queda ni una fila");

        await using (InventarioDbContext inventario = postgres.AbrirInventario(caso.EmpresaId))
        {
            Ajuste rechazado = await inventario.Ajustes.SingleAsync(fila => fila.Id == delSegundo);

            rechazado.Estado.ShouldBe(EstadoDeAjuste.Borrador, "su transacción entera se deshizo");
            rechazado.Numero.ShouldBeNull();
        }

        (await cliente.GetFromJsonAsync<SerieDto>($"{LosMaestrosPorLaApi.Series}/{segunda.Id}"))!
            .Contador.ShouldBe(
                0,
                "el número que tomó volvió a la serie con el rollback: si se hubiera quedado " +
                "gastado, la R5 tendría un hueco");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(1);
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task Una_fecha_futura_no_se_confirma_y_no_deja_nada_en_el_libro_ni_en_la_existencia()
    {
        DateOnly manana = Hoy.AddDays(1);

        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(443);

        // EL EJERCICIO ES EL DE MAÑANA, abierto, y la serie cuelga de él: todo lo demás que mira
        // la confirmación diría que sí. Lo único que el documento tiene mal es la fecha.
        EjercicioDto ejercicio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, manana.Year);
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, ejercicio.Id, "EXI-D");

        ElAlmacenDelCaso caso = await LosMaestrosDeAsync(cliente, empresa.Id, "EXI-D", 453, serie);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        Guid ajusteId = await AbrirAsync(modulo, caso, serie.Id, manana, [new Linea(0, 5m)]);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        confirmacion.EsCorrecto.ShouldBeFalse(
            "una fila del libro con fecha de mañana haría que la existencia contara hoy algo que " +
            "todavía no ha pasado, y el saldo a hoy dejaría de ser la suma del libro hasta hoy");

        confirmacion.Error!.Codigo.ShouldBe("ajuste-con-fecha-futura");

        await using InventarioDbContext inventario = postgres.AbrirInventario(caso.EmpresaId);

        Ajuste comoQuedo = await inventario.Ajustes.SingleAsync(fila => fila.Id == ajusteId);

        comoQuedo.Estado.ShouldBe(EstadoDeAjuste.Borrador, "se confirma el día que llegue su fecha");
        comoQuedo.Numero.ShouldBeNull();

        (await inventario.Movimientos.AnyAsync()).ShouldBeFalse("ni una fila del libro");
        (await inventario.Existencias.AnyAsync()).ShouldBeFalse("ni la fila viva de la clave");

        SerieDto? despues = await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serie.Id}");

        despues!.Contador.ShouldBe(
            0,
            "la guarda va antes del numerador: un documento que no se confirma no gasta número");
    }

    [Fact]
    public async Task Borrar_las_instantaneas_y_recalcularlas_no_cambia_ningun_numero()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(444, "EXI-E", 454, ubicaciones: 3, conElAnioPasado: true);

        int pasado = Hoy.Year - 1;
        DateOnly corte = LasExistencias.MesDe(Hoy);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 3, 10), new Linea(0, 5m));

        Guid delVerano = await ConfirmarAsync(
            modulo, caso, new DateOnly(pasado, 7, 20), new Linea(0, -2m), new Linea(1, 7m));

        // EL PRIMER RECÁLCULO pone el corte en este mes y saca las instantáneas del libro.
        int repuestas = await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, corte);

        IReadOnlyList<FotoDelMes> debidas = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), corte);

        repuestas.ShouldBe(debidas.Count);
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(debidas);

        (await LasExistencias.ValoresDeLasInstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            LasExistencias.ValoresDebidosEnCSharp(
                await LasExistencias.LibroValoradoAsync(postgres, caso.EmpresaId), corte),
            "la reposición saca del libro también el valor (ADR-0063 §1)");

        // DESPUÉS DEL CORTE, TRES CONFIRMACIONES CON FECHA DE UN MES ANTERIOR, UNA DE HOY Y UNA
        // ANULACIÓN. Es la decisión (a) del ítem: quien anota el libro suma también en las
        // instantáneas de ese mes y de todos los que vienen detrás hasta el corte. Una clave nueva
        // —que estrena meses—, una fila en los meses que ya tiene cada una de las otras dos, la de
        // hoy, que suma solo en el mes del corte, y el inverso, también de hoy.
        //
        // NINGUNA VA POR DETRÁS DE SU CLAVE (addendum del 2.8, ADR-0047). Dos de las tres de antes
        // iban por delante del primer mes de su clave, y ya no se confirman: el artículo se había
        // movido después en el mismo almacén. Las fechas van ahora en el orden en que se confirman,
        // y lo que el caso afirma de las instantáneas es lo mismo.
        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 9, 1), new Linea(2, 4m));
        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 11, 5), new Linea(1, 1m));
        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 12, 2), new Linea(0, -1m));
        await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 3m));

        Resultado<AnulacionDto> anulacion =
            await modulo.AnularAsync(delVerano, "Recuento repetido por error");

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»");

        IReadOnlyList<ApunteDelLibro> libro = await LasExistencias.LibroAsync(postgres, caso.EmpresaId);

        IReadOnlyList<FotoDelMes> antes = await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId);

        antes.ShouldBe(
            LasExistencias.DebidasEnCSharp(libro, corte),
            "las instantáneas que dejó la sentencia del libro son las que el libro dice");

        antes.ShouldNotBeEmpty();

        // Y SU VALOR, que la misma sentencia suma con la cantidad (ADR-0063 §1). Las entradas del
        // caso cuestan 1,50 y las salidas salen a su precio medio, así que el valor no es la
        // cantidad, y una sentencia que sumara la una en el sitio del otro no pasaría por buena.
        IReadOnlyList<ValorDelMes> valoresAntes =
            await LasExistencias.ValoresDeLasInstantaneasAsync(postgres, caso.EmpresaId);

        valoresAntes.ShouldBe(
            LasExistencias.ValoresDebidosEnCSharp(
                await LasExistencias.LibroValoradoAsync(postgres, caso.EmpresaId), corte),
            "el valor que dejó la sentencia del libro es el que el libro suma hasta cada fin de mes");

        valoresAntes.ShouldContain(
            valor => valor.Valor != 0m,
            "con todos los valores a cero, una sentencia que no los sumara saldría igual");

        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos = LasExistencias.SaldosDelLibro(libro);
        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, caso.EmpresaId);

        vivas.Count.ShouldBe(saldos.Count, "una fila viva por clave del libro, y ninguna más");

        foreach (Existencia viva in vivas)
        {
            viva.Fisico.ShouldBe(saldos[LasExistencias.ClaveDe(viva)]);
        }

        // LA BORRA, a la vista y por debajo del sistema, para que el caso no dependa de que el
        // recálculo borre: lo que se afirma es que se puede partir de nada.
        int borradas = await BorrarLasInstantaneasAsync(caso.EmpresaId);

        borradas.ShouldBe(antes.Count);
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBeEmpty();

        // LA RECALCULA...
        (await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, corte)).ShouldBe(antes.Count);

        // ...Y COMPARA: ni un número distinto.
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            antes,
            "la instantánea es una optimización: tirada y vuelta a sacar del libro, dice lo mismo");

        (await LasExistencias.ValoresDeLasInstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            valoresAntes,
            "y su valor, también");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(3, "las tres claves que se movieron");
        cuadre.InstantaneasComparadas.ShouldBe(antes.Count, "cada clave, cada mes hasta el corte");
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_cuadre_encuentra_cada_copia_que_no_dice_lo_que_el_libro()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(445, "EXI-F", 455, ubicaciones: 2, conElAnioPasado: true);

        DateOnly junio = new(Hoy.Year - 1, 6, 1);
        DateOnly corte = LasExistencias.MesDe(Hoy);

        await using (ElModuloDeInventario modulo = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(modulo, caso, junio.AddDays(9), new Linea(0, 5m), new Linea(1, 3m));
            await ConfirmarAsync(modulo, caso, Hoy, new Linea(0, 1m));
        }

        await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, corte);

        int debidas = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), corte).Count;

        CuadreDeLasExistencias limpio = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        limpio.ExistenciasComparadas.ShouldBe(2);
        limpio.InstantaneasComparadas.ShouldBe(debidas);
        limpio.Descuadres.ShouldBeEmpty("recién recalculado, todo cuadra: lo de abajo lo pone el caso");

        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, caso.EmpresaId);
        Guid laPrimera = vivas.Single(viva => viva.UbicacionId == caso.Ubicaciones[0]).Id;
        Guid laSegunda = vivas.Single(viva => viva.UbicacionId == caso.Ubicaciones[1]).Id;
        var unaUbicacionSinLibro = Guid.CreateVersion7();

        // OCHO COPIAS ESTROPEADAS, una de cada manera en que una copia puede mentir, y todas en una
        // transacción que se deshace: el cuadre corre dentro de ella, con el mismo contexto.
        await using InventarioDbContext contexto = postgres.AbrirInventario(caso.EmpresaId);
        await using IDbContextTransaction estropeando = await contexto.Database.BeginTransactionAsync();

        // Una fila viva que dice de más.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET fisico = fisico + 1 WHERE id = {0}", laSegunda))
            .ShouldBe(1);

        // Una instantánea que dice otra cosa.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.instantaneas_mensuales SET fisico = fisico + 10 " +
            "WHERE existencia_id = {0} AND mes = {1}",
            laPrimera,
            junio)).ShouldBe(1);

        // Una instantánea que dice otro valor con la cantidad bien (ADR-0063 §2): sin comparar el
        // valor, el saldo a una fecha saldría mal y el cuadre, limpio.
        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.instantaneas_mensuales SET valor = valor + 7 " +
            "WHERE existencia_id = {0} AND mes = {1}",
            laSegunda,
            junio)).ShouldBe(1);

        // Una instantánea que falta: le falta la cantidad y le falta el valor.
        (await contexto.Database.ExecuteSqlRawAsync(
            "DELETE FROM inventario.instantaneas_mensuales WHERE existencia_id = {0} AND mes = {1}",
            laSegunda,
            junio.AddMonths(1))).ShouldBe(1);

        // Una fila viva de una clave que el libro no ha movido nunca.
        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.existencias " +
            "(id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id, fisico, en_transito) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, NULL, 2, 0)",
            Guid.CreateVersion7(),
            caso.EmpresaId,
            caso.ArticuloId,
            caso.AlmacenId,
            unaUbicacionSinLibro)).ShouldBe(1);

        // Una instantánea que sobra: la de después del corte.
        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.instantaneas_mensuales (existencia_id, mes, empresa_id, fisico, valor) " +
            "VALUES ({0}, {1}, {2}, 6, 0)",
            laPrimera,
            corte.AddMonths(1),
            caso.EmpresaId)).ShouldBe(1);

        // Una clave con dos filas vivas que, juntas, dicen lo que el libro. La suma no la delata:
        // solo la cuenta de filas. Para plantarla hay que quitar el índice único, que es justo lo
        // que la cuenta vigila por si un día deja de distinguir el lote nulo; el índice vuelve con
        // el rollback.
        await contexto.Database.ExecuteSqlRawAsync("DROP INDEX inventario.ix_existencias_una_por_clave");

        (await contexto.Database.ExecuteSqlRawAsync(
            "UPDATE inventario.existencias SET fisico = fisico - 2 WHERE id = {0}", laPrimera))
            .ShouldBe(1);

        var laCopia = Guid.CreateVersion7();

        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.existencias " +
            "(id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id, fisico, en_transito) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, NULL, 2, 0)",
            laCopia,
            caso.EmpresaId,
            caso.ArticuloId,
            caso.AlmacenId,
            caso.Ubicaciones[0])).ShouldBe(1);

        // Y una instantánea de más, a cero, para una clave y un mes que ya tenían la suya: tampoco
        // cambia la suma.
        (await contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO inventario.instantaneas_mensuales (existencia_id, mes, empresa_id, fisico, valor) " +
            "VALUES ({0}, {1}, {2}, 0, 0)",
            laCopia,
            junio.AddMonths(1),
            caso.EmpresaId)).ShouldBe(1);

        CuadreDeLasExistencias sucio = await LasExistencias.CuadrarEnAsync(contexto, caso.EmpresaId);

        sucio.ExistenciasComparadas.ShouldBe(3, "las dos de antes y la que no tiene libro");
        sucio.InstantaneasComparadas.ShouldBe(debidas + 1, "las debidas y la que sobra");

        sucio.Descuadres.ShouldAllBe(descuadre =>
            descuadre.ArticuloId == caso.ArticuloId
            && descuadre.AlmacenId == caso.AlmacenId
            && descuadre.LoteId == null);

        sucio.Descuadres
            .Select(descuadre => new Hallado(
                descuadre.Que,
                descuadre.UbicacionId,
                descuadre.Mes,
                descuadre.Esperado,
                descuadre.Guardado,
                descuadre.Filas))
            .ShouldBe(
                [
                    new Hallado("existencia", caso.Ubicaciones[1], null, 3m, 4m, 1),
                    new Hallado("existencia", unaUbicacionSinLibro, null, 0m, 2m, 1),
                    new Hallado("instantanea", caso.Ubicaciones[0], junio, 5m, 15m, 1),
                    new Hallado("instantanea", caso.Ubicaciones[1], junio.AddMonths(1), 3m, 0m, 0),
                    new Hallado("instantanea", caso.Ubicaciones[0], corte.AddMonths(1), 0m, 6m, 1),
                    new Hallado("existencia", caso.Ubicaciones[0], null, 6m, 6m, 2),
                    new Hallado("instantanea", caso.Ubicaciones[0], junio.AddMonths(1), 15m, 5m, 2),
                    new Hallado("instantanea-valor", caso.Ubicaciones[1], junio, 4.5m, 11.5m, 1),
                    new Hallado("instantanea-valor", caso.Ubicaciones[1], junio.AddMonths(1), 11.5m, 0m, 0),
                    new Hallado("instantanea", caso.Ubicaciones[1], junio.AddMonths(2), 0m, 3m, 1),
                    new Hallado("instantanea-valor", caso.Ubicaciones[1], junio.AddMonths(2), 0m, 4.5m, 1),
                ],
                ignoreOrder: true,
                customMessage: "once de ocho copias, y ninguna más: la que falta, falta en sus dos columnas; "
                    + "y como cada mes se compara con el anterior (ADR-0067 §6), la que miente y la que "
                    + "falta descuadran también el mes siguiente");

        await estropeando.RollbackAsync();

        // Y DESHECHO, OTRA VEZ LIMPIO: lo que el cuadre encontró era lo que el caso puso.
        CuadreDeLasExistencias otraVez = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        otraVez.ExistenciasComparadas.ShouldBe(2);
        otraVez.InstantaneasComparadas.ShouldBe(debidas);
        otraVez.Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// La migración del valor rellena cada instantánea que ya había con la suma del valor del libro
    /// de su clave hasta el final de su mes, y se queda sin valor por omisión (ADR-0063 §1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>En una base propia, migrada hasta la de antes</b>, con los lotes, las existencias, el libro
    /// y las instantáneas escritos a mano. En producción no hay ninguna fila que rellenar, porque
    /// ninguna empresa tiene corte: el caso está para que la migración diga la verdad en cualquier
    /// base.
    /// </para>
    /// <para>
    /// <b>Cada fila del libro prueba una parte de la clave o del límite</b>: la del 31 de julio entra
    /// en julio y la del 1 de agosto no; la de otra ubicación, la del lote y la de otra empresa no se
    /// cuelan en la clave sin lote; y una instantánea sin libro queda a cero, y no nula.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_migracion_rellena_el_valor_de_cada_instantanea_con_el_libro_hasta_su_mes()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);

        await PostgresConTodosLosModulos.MigrarInventarioHastaAsync(cadena, LaDeAntesDelValor);

        DateOnly junio = new(Hoy.Year - 1, 6, 1);
        DateOnly julio = junio.AddMonths(1);
        DateOnly agosto = julio.AddMonths(1);

        var empresa = Guid.CreateVersion7();
        var otraEmpresa = Guid.CreateVersion7();
        var articulo = Guid.CreateVersion7();
        var almacen = Guid.CreateVersion7();
        var ubicacion = Guid.CreateVersion7();
        var otraUbicacion = Guid.CreateVersion7();
        var sinLibro = Guid.CreateVersion7();
        var lote = Guid.CreateVersion7();

        var laClave = Guid.CreateVersion7();
        var laDeOtraUbicacion = Guid.CreateVersion7();
        var laDelLote = Guid.CreateVersion7();
        var laDeOtraEmpresa = Guid.CreateVersion7();
        var laSinLibro = Guid.CreateVersion7();

        await using (NpgsqlConnection conexion = new(cadena))
        {
            await conexion.OpenAsync();

            await EscribirAsync(
                conexion,
                "INSERT INTO inventario.lotes (id, empresa_id, articulo_id, codigo) VALUES ($1, $2, $3, 'L-1')",
                lote,
                empresa,
                articulo);

            foreach ((Guid id, Guid deEmpresa, Guid enUbicacion, Guid? deLote, decimal fisico) in new[]
            {
                (laClave, empresa, ubicacion, (Guid?)null, 8m),
                (laDeOtraUbicacion, empresa, otraUbicacion, null, 3m),
                (laDelLote, empresa, ubicacion, lote, 5m),
                (laDeOtraEmpresa, otraEmpresa, ubicacion, null, 7m),
                (laSinLibro, empresa, sinLibro, null, 0m),
            })
            {
                await EscribirAsync(
                    conexion,
                    "INSERT INTO inventario.existencias "
                    + "(id, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id, fisico, en_transito) "
                    + "VALUES ($1, $2, $3, $4, $5, $6::uuid, $7, 0)",
                    id,
                    deEmpresa,
                    articulo,
                    almacen,
                    enUbicacion,
                    deLote is Guid conLote ? conLote : DBNull.Value,
                    fisico);
            }

            foreach ((DateOnly fecha, Guid deEmpresa, Guid enUbicacion, Guid? deLote, decimal cantidad, decimal valor) in new[]
            {
                (junio.AddDays(9), empresa, ubicacion, (Guid?)null, 10m, 15m),
                (julio.AddDays(30), empresa, ubicacion, null, -4m, -6m),
                (agosto, empresa, ubicacion, null, 2m, 3.1234m),
                (junio.AddDays(14), empresa, otraUbicacion, null, 3m, 4.5m),
                (junio.AddDays(19), empresa, ubicacion, lote, 5m, 100m),
                (junio.AddDays(4), otraEmpresa, ubicacion, null, 7m, 1000m),
            })
            {
                await EscribirAsync(
                    conexion,
                    """
                    INSERT INTO inventario.movimiento_stock
                        (id, fecha_de_operacion, empresa_id, articulo_id, almacen_id, ubicacion_id, lote_id,
                         unidad_introducida_id, cantidad_introducida, factor_a_unidad_base,
                         cantidad_en_unidad_base, divisa, documento_origen_tipo, documento_origen_id,
                         precio_medio, valor, creado_en, modificado_en)
                    VALUES
                        (gen_random_uuid(), $1, $2, $3, $4, $5, $6::uuid,
                         gen_random_uuid(), $7, 1, $7, 'EUR', 'Ajuste', gen_random_uuid(),
                         1.5, $8, now(), now())
                    """,
                    fecha,
                    deEmpresa,
                    articulo,
                    almacen,
                    enUbicacion,
                    deLote is Guid conLote ? conLote : DBNull.Value,
                    cantidad,
                    valor);
            }

            foreach ((Guid existencia, Guid deEmpresa, DateOnly mes, decimal fisico) in new[]
            {
                (laClave, empresa, junio, 10m),
                (laClave, empresa, julio, 6m),
                (laClave, empresa, agosto, 8m),
                (laDeOtraUbicacion, empresa, junio, 3m),
                (laDeOtraUbicacion, empresa, julio, 3m),
                (laDelLote, empresa, junio, 5m),
                (laDeOtraEmpresa, otraEmpresa, junio, 7m),
                (laSinLibro, empresa, junio, 0m),
            })
            {
                await EscribirAsync(
                    conexion,
                    "INSERT INTO inventario.instantaneas_mensuales (existencia_id, mes, empresa_id, fisico) "
                    + "VALUES ($1, $2, $3, $4)",
                    existencia,
                    mes,
                    deEmpresa,
                    fisico);
            }
        }

        await PostgresConTodosLosModulos.MigrarInventarioHastaAsync(cadena, LaDelValor);

        Dictionary<(Guid, DateOnly), decimal> valores = [];
        object? porOmision;

        await using (NpgsqlConnection conexion = new(cadena))
        {
            await conexion.OpenAsync();

            await using (NpgsqlCommand lectura = new(
                "SELECT existencia_id, mes, valor FROM inventario.instantaneas_mensuales", conexion))
            await using (NpgsqlDataReader lector = await lectura.ExecuteReaderAsync())
            {
                while (await lector.ReadAsync())
                {
                    valores.Add(
                        (lector.GetGuid(0), lector.GetFieldValue<DateOnly>(1)), lector.GetDecimal(2));
                }
            }

            await using NpgsqlCommand columna = new(
                "SELECT column_default FROM information_schema.columns "
                + "WHERE table_schema = 'inventario' AND table_name = 'instantaneas_mensuales' "
                + "AND column_name = 'valor'",
                conexion);

            porOmision = await columna.ExecuteScalarAsync();
        }

        valores.ShouldBe(
            new Dictionary<(Guid, DateOnly), decimal>
            {
                [(laClave, junio)] = 15m,
                [(laClave, julio)] = 9m,
                [(laClave, agosto)] = 12.1234m,
                [(laDeOtraUbicacion, junio)] = 4.5m,
                [(laDeOtraUbicacion, julio)] = 4.5m,
                [(laDelLote, junio)] = 100m,
                [(laDeOtraEmpresa, junio)] = 1000m,
                [(laSinLibro, junio)] = 0m,
            },
            ignoreOrder: true);

        porOmision.ShouldBe(
            DBNull.Value,
            "el cero por omisión es solo para las filas que ya había: una nueva sin valor no entra");
    }

    [Fact]
    public async Task El_recalculo_espera_a_la_confirmacion_de_una_clave_nueva_que_ya_estaba_dentro()
    {
        ElAlmacenDelCaso caso = await UnAlmacenAsync(446, "EXI-G", 456);

        DateOnly esteMes = LasExistencias.MesDe(Hoy);

        // EL CORTE EMPIEZA EN EL MES PASADO, así que la confirmación de hoy no escribe ninguna
        // instantánea: su mes cae después del corte. Se las tiene que dar el recálculo que lo
        // avanza a este mes, y el recálculo no ve una fila viva que todavía no está confirmada.
        (await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, esteMes.AddMonths(-1)))
            .ShouldBe(0, "no hay libro todavía: solo queda puesto el corte");

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        Guid ajusteId = await AbrirAsync(modulo, caso, caso.Serie.Id, Hoy, [new Linea(0, 5m)]);

        // TRANSACCIÓN 1: confirma una clave NUEVA —su fila viva nace aquí— y no suelta.
        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await modulo.ConfirmarYQuedarseDentroAsync(ajusteId);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            // TRANSACCIÓN 2: el recálculo avanza el corte. SIN SU CERROJO NO ESPERA A NADA: el
            // corte no lo tiene cogido nadie, no hay instantáneas que borrar y la fila viva no se
            // ve, así que acaba enseguida sin darle a la clave nueva la instantánea de este mes.
            Task<int> recalculo = LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, esteMes);

            await EsperarAQueLaFreneAsync(modulo.ProcesoDeLaBase, recalculo);

            await enVuelo.CommitAsync();

            (await recalculo.WaitAsync(TimeSpan.FromSeconds(30))).ShouldBe(
                1,
                "esperó al COMMIT de la confirmación, y con él ya a la vista le dio su instantánea");
        }

        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            [new FotoDelMes(caso.Clave(0), esteMes, 5m)]);

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.ExistenciasComparadas.ShouldBe(1);
        cuadre.InstantaneasComparadas.ShouldBe(1);
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_recalculo_de_una_empresa_no_frena_las_confirmaciones_de_otra()
    {
        ElAlmacenDelCaso deA = await UnAlmacenAsync(858, "EXI-K", 858);
        ElAlmacenDelCaso deB = await UnAlmacenAsync(859, "EXI-L", 859);

        DateOnly esteMes = LasExistencias.MesDe(Hoy);

        // EL CORTE DE A, PUESTO, para que haya una fila que coger.
        (await LasExistencias.RecalcularAsync(postgres, deA.EmpresaId, esteMes.AddMonths(-1))).ShouldBe(0);

        await using ElModuloDeInventario enB = new(postgres, deB.EmpresaId);

        Guid deLaB = await AbrirAsync(enB, deB, deB.Serie.Id, Hoy, [new Linea(0, 5m)]);

        // TRANSACCIÓN 1: el recálculo de A, con su cerrojo y parado en la fila de su corte.
        await using ElCorteCogido corte = await CogerElCorteAsync(deA.EmpresaId);

        Task<int> recalculo = LasExistencias.RecalcularAsync(postgres, deA.EmpresaId, esteMes);

        int delRecalculo = await ElRecalculoParadoDentroAsync(corte, recalculo);

        // TRANSACCIÓN 2: la confirmación de B. Con una clave sin la empresa —lo que hacía el
        // `LOCK TABLE`— se pararía detrás del recálculo de A, y el caso lo dice en el momento.
        Task<Resultado<AjusteDto>> confirmacion = enB.ConfirmarAsync(deLaB);

        await LaEspera.SinQueLaFreneAsync(
            postgres.CadenaDeConexion,
            delRecalculo,
            confirmacion,
            "el recálculo de otra empresa",
            "el cerrojo de las instantáneas no es de la empresa, y un recálculo para a todas");

        Resultado<AjusteDto> confirmadaEnB = await confirmacion;

        confirmadaEnB.EsCorrecto.ShouldBeTrue($"«{confirmadaEnB.Error?.Codigo}»");
        recalculo.IsCompleted.ShouldBeFalse("B ha confirmado con el recálculo de A todavía dentro");

        await corte.SoltarAsync();

        (await recalculo.WaitAsync(TimeSpan.FromSeconds(30))).ShouldBe(0, "A no tiene libro");

        (await LasExistencias.VivasAsync(postgres, deB.EmpresaId))
            .ShouldHaveSingleItem()
            .Fisico.ShouldBe(5m);
    }

    [Fact]
    public async Task La_confirmacion_de_una_clave_nueva_espera_al_recalculo_de_su_empresa()
    {
        ElAlmacenDelCaso caso = await UnAlmacenAsync(860, "EXI-M", 860);

        DateOnly esteMes = LasExistencias.MesDe(Hoy);

        (await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, esteMes.AddMonths(-1))).ShouldBe(0);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        Guid ajusteId = await AbrirAsync(modulo, caso, caso.Serie.Id, Hoy, [new Linea(0, 5m)]);

        // TRANSACCIÓN 1: el recálculo que avanza el corte a este mes, con su cerrojo y parado.
        await using ElCorteCogido corte = await CogerElCorteAsync(caso.EmpresaId);

        Task<int> recalculo = LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, esteMes);

        int delRecalculo = await ElRecalculoParadoDentroAsync(corte, recalculo);

        // TRANSACCIÓN 2: una clave NUEVA de la misma empresa. Si entrara, leería el corte viejo y no
        // se daría la instantánea de este mes; y el recálculo, que no ve su fila viva sin confirmar,
        // tampoco. Con un cerrojo compartido al recalcular, no esperaría.
        Task<Resultado<AjusteDto>> confirmacion = modulo.ConfirmarAsync(ajusteId);

        await EsperarAQueLaFreneAsync(delRecalculo, confirmacion);

        await corte.SoltarAsync();

        // DESDE EL 2.18 EL RECÁLCULO VA MES A MES (ADR-0067 §3), y la confirmación entra entre su
        // vuelta atrás, que quita el corte, y su avance, que lo vuelve a poner. Entra sin corte y no
        // escribe ninguna instantánea; el avance espera a su COMMIT y le da la de este mes.
        (await recalculo.WaitAsync(TimeSpan.FromSeconds(30))).ShouldBe(
            1, "el avance del recálculo esperó a la confirmación, y ya vio su clave");

        Resultado<AjusteDto> confirmada = await confirmacion.WaitAsync(TimeSpan.FromSeconds(30));

        confirmada.EsCorrecto.ShouldBeTrue($"«{confirmada.Error?.Codigo}»");

        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            [new FotoDelMes(caso.Clave(0), esteMes, 5m)],
            "la instantánea de la clave nueva la escribió el mes del recálculo que vino detrás");

        CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId);

        cuadre.InstantaneasComparadas.ShouldBe(1);
        cuadre.Descuadres.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dos_confirmaciones_de_la_misma_empresa_no_se_frenan_por_el_cerrojo_de_las_instantaneas()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(861);

        // DOS SERIES, DOS ARTÍCULOS Y DOS ALMACENES: no comparten ni el contador de la serie, ni la
        // fila viva, ni la valoración. Lo único que comparten es el cerrojo de las instantáneas de su
        // empresa, y es compartido: si fuera exclusivo, la segunda esperaría a la primera.
        EjercicioDto esteAnio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year);
        SerieDto primera = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-N1");
        SerieDto segunda = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, "EXI-N2");

        ElAlmacenDelCaso uno = await LosMaestrosDeAsync(cliente, empresa.Id, "EXI-N", 861, primera);
        ElAlmacenDelCaso otro = await LosMaestrosDeAsync(cliente, empresa.Id, "EXI-O", 862, segunda);

        await using ElModuloDeInventario unos = new(postgres, empresa.Id);
        await using ElModuloDeInventario otros = new(postgres, empresa.Id);

        Guid delPrimero = await AbrirAsync(unos, uno, primera.Id, Hoy, [new Linea(0, 3m)]);
        Guid delSegundo = await AbrirAsync(otros, otro, segunda.Id, Hoy, [new Linea(0, 4m)]);

        // TRANSACCIÓN 1: confirma y no suelta, con el compartido de su empresa tomado.
        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await unos.ConfirmarYQuedarseDentroAsync(delPrimero);

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            (await CerrojosDeLasInstantaneasAsync(unos.ProcesoDeLaBase)).ShouldBe(
                1, "la primera tiene el cerrojo de las instantáneas de su empresa, y no lo suelta");

            // TRANSACCIÓN 2: la otra serie, el otro artículo. Termina con la primera dentro.
            Task<Resultado<AjusteDto>> laOtra = otros.ConfirmarAsync(delSegundo);

            await LaEspera.SinQueLaFreneAsync(
                postgres.CadenaDeConexion,
                unos.ProcesoDeLaBase,
                laOtra,
                "otra confirmación de su empresa",
                "el cerrojo de quien anota no es compartido, y las confirmaciones van de una en una; "
                + "o las dos comparten algo más que la empresa, como la clave o la serie");

            Resultado<AjusteDto> segundaConfirmacion = await laOtra;

            segundaConfirmacion.EsCorrecto.ShouldBeTrue($"«{segundaConfirmacion.Error?.Codigo}»");

            await enVuelo.CommitAsync();
        }

        (await LasExistencias.VivasAsync(postgres, empresa.Id))
            .Select(viva => viva.Fisico)
            .ShouldBe([3m, 4m], ignoreOrder: true);
    }

    [Fact]
    public async Task Las_existencias_y_las_instantaneas_de_dos_empresas_no_se_mezclan()
    {
        // LAS SENTENCIAS DE LA PROYECCIÓN SON SQL CRUDO, y el filtro global de empresa no las
        // alcanza: cada una compara la empresa ella misma. Este caso es el que se pone rojo si una
        // de esas comparaciones desaparece, y por eso las dos empresas tienen de todo —libro, filas
        // vivas, corte e instantáneas—: con una de ellas vacía, casi ninguna fuga se vería.
        ElAlmacenDelCaso deA = await UnAlmacenAsync(447, "EXI-H", 457);
        ElAlmacenDelCaso deB = await UnAlmacenAsync(448, "EXI-I", 458);

        DateOnly esteMes = LasExistencias.MesDe(Hoy);

        await using (ElModuloDeInventario enB = new(postgres, deB.EmpresaId))
        {
            await ConfirmarAsync(enB, deB, Hoy, new Linea(0, 7m));
        }

        await LasExistencias.RecalcularAsync(postgres, deB.EmpresaId, esteMes);

        await using (ElModuloDeInventario enA = new(postgres, deA.EmpresaId))
        {
            await ConfirmarAsync(enA, deA, Hoy, new Linea(0, 5m));
            await LasExistencias.RecalcularAsync(postgres, deA.EmpresaId, esteMes);

            // CON LOS DOS CORTES PUESTOS, la sentencia del libro tiene que leer el de su empresa:
            // si leyera los dos, su subconsulta devolvería dos filas y la confirmación reventaría.
            await ConfirmarAsync(enA, deA, Hoy, new Linea(0, 2m));
        }

        // Y EL RECÁLCULO DE A tira y repone las de A, no las de B.
        (await LasExistencias.RecalcularAsync(postgres, deA.EmpresaId, esteMes)).ShouldBe(1);

        (await LasExistencias.InstantaneasAsync(postgres, deB.EmpresaId)).ShouldBe(
            [new FotoDelMes(deB.Clave(0), esteMes, 7m)],
            "la instantánea de B sigue ahí después de que A recalculara las suyas");

        (await LasExistencias.InstantaneasAsync(postgres, deA.EmpresaId)).ShouldBe(
            [new FotoDelMes(deA.Clave(0), esteMes, 7m)]);

        (await LasExistencias.VivasAsync(postgres, deA.EmpresaId))
            .ShouldHaveSingleItem()
            .Fisico.ShouldBe(7m);

        // Y CADA CUADRE COMPARA LO SUYO, contado: un cuadre que se llevara las claves de la otra
        // empresa compararía dos y no una, y además las daría por descuadradas.
        foreach (Guid empresaId in new[] { deA.EmpresaId, deB.EmpresaId })
        {
            CuadreDeLasExistencias cuadre = await LasExistencias.CuadrarAsync(postgres, empresaId);

            cuadre.ExistenciasComparadas.ShouldBe(1);
            cuadre.InstantaneasComparadas.ShouldBe(1);
            cuadre.Descuadres.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task El_avance_pone_el_corte_un_mes_cada_vez_y_no_rehace_lo_que_ya_llega()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(863, "EXI-AV", 863, ubicaciones: 2, conElAnioPasado: true);

        int pasado = Hoy.Year - 1;
        DateOnly marzo = new(pasado, 3, 1);
        DateOnly esteMes = LasExistencias.MesDe(Hoy);
        DateOnly elQueToca = esteMes.AddMonths(-1);

        await using (ElModuloDeInventario modulo = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 3, 10), new Linea(0, 5m));
            await ConfirmarAsync(
                modulo, caso, new DateOnly(pasado, 7, 20), new Linea(0, -2m), new Linea(1, 7m));
            await ConfirmarAsync(modulo, caso, Hoy, new Linea(1, 1m));
        }

        (await CorteAsync(caso.EmpresaId)).ShouldBeNull("la empresa del caso empieza sin corte");

        // SIN CORTE, EL AVANCE EMPIEZA EN EL MES DEL PRIMER MOVIMIENTO y llega al que toca un mes por
        // llamada, cada uno en su transacción (ADR-0067 §1): es lo que pone al día solo a un trabajo
        // parado, sin tener el exclusivo de la empresa más de un mes. Por el mismo camino que el
        // trabajo: el contenedor del anfitrión, con su avance y su cuadre.
        IReadOnlyList<MesAvanzado> meses = await AvanzarAsync(caso.EmpresaId, elQueToca);

        IReadOnlyList<FotoDelMes> debidas = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), elQueToca);

        debidas.Select(foto => foto.Mes).Distinct().Count().ShouldBeGreaterThan(
            1, "con un solo mes no se vería que van uno a uno");

        // CADA MES, EL SIGUIENTE AL ANTERIOR Y CON LAS SUYAS: la clave de marzo desde marzo, y la de
        // julio desde julio.
        meses.Select(mes => (mes.Mes, mes.Instantaneas)).ShouldBe(
            [.. debidas.GroupBy(foto => foto.Mes).OrderBy(mes => mes.Key).Select(mes => (mes.Key, mes.Count()))],
            "un mes por llamada, del primero del libro al que toca, sin saltarse ninguno");

        meses[0].Mes.ShouldBe(marzo);
        meses.ShouldAllBe(mes => mes.Duracion > TimeSpan.Zero);

        (await CorteAsync(caso.EmpresaId)).ShouldBe(elQueToca);
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(debidas);

        (await LasExistencias.ValoresDeLasInstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            LasExistencias.ValoresDebidosEnCSharp(
                await LasExistencias.LibroValoradoAsync(postgres, caso.EmpresaId), elQueToca));

        // Y CUADRA DETRÁS LO QUE NO ESTÁ CUADRADO, que es todo: de marzo al corte, con sus cifras. Las
        // dos claves se comparan en el mes del corte, y cada instantánea en el suyo; la de hoy queda
        // fuera, porque su mes va después del corte.
        CuadreHecho cuadre = (await CuadrarLoPendienteAsync(caso.EmpresaId)).ShouldNotBeNull(
            "sin nada cuadrado, el cuadre tiene que cuadrar");

        (cuadre.Desde, cuadre.Hasta).ShouldBe((marzo, elQueToca));
        cuadre.Cuadre.ExistenciasComparadas.ShouldBe(2);
        cuadre.Cuadre.InstantaneasComparadas.ShouldBe(debidas.Count);
        cuadre.Cuadre.Descuadres.ShouldBeEmpty();
        cuadre.Duracion.ShouldBeGreaterThan(TimeSpan.Zero);

        (await CuadradoHastaAsync(caso.EmpresaId)).ShouldBe(elQueToca);
        (await CuadrarLoPendienteAsync(caso.EmpresaId)).ShouldBeNull("lo cuadrado se queda anotado, y no se repite");

        // UN SEGUNDO AVANCE AL MISMO MES NO REHACE NADA. Para que se vea, el caso estropea antes una
        // instantánea, a la vista y por debajo del sistema: un avance que rehiciera la arreglaría.
        FotoDelMes estropeada = debidas[^1];

        await using (NpgsqlConnection conexion = new(postgres.CadenaDeConexion))
        {
            await conexion.OpenAsync();

            await EscribirAsync(
                conexion,
                "UPDATE inventario.instantaneas_mensuales AS i SET fisico = i.fisico + 10 "
                + "FROM inventario.existencias AS e "
                + "WHERE e.id = i.existencia_id AND e.empresa_id = $1 AND e.ubicacion_id = $2 "
                + "AND i.mes = $3",
                caso.EmpresaId,
                estropeada.Clave.UbicacionId,
                estropeada.Mes);
        }

        IReadOnlyList<FotoDelMes> conLaEstropeada =
            [.. debidas.Select(foto => foto == estropeada ? foto with { Fisico = foto.Fisico + 10m } : foto)];

        (await AvanzarAsync(caso.EmpresaId, elQueToca)).ShouldBeEmpty("el corte ya llega a ese mes");

        // Y UN MES ANTERIOR TAMPOCO LO MUEVE: llevar el corte atrás es recalcular, y eso no lo
        // decide un trabajo que se despierta cada hora.
        (await AvanzarAsync(caso.EmpresaId, elQueToca.AddMonths(-1))).ShouldBeEmpty(
            "un mes anterior no mueve el corte hacia atrás");

        (await CorteAsync(caso.EmpresaId)).ShouldBe(elQueToca);
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            conLaEstropeada,
            "ni el segundo avance ni el del mes anterior han tocado una instantánea");

        // A UN MES POSTERIOR, SÍ AVANZA, Y SOLO ESE MES: el de hoy, con la confirmación de hoy dentro.
        // Sale del mes anterior más el libro del suyo (ADR-0067 §6), así que la estropeada se
        // arrastra: la de este mes de su clave dice también diez de más.
        IReadOnlyList<FotoDelMes> hastaHoy = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), esteMes);

        MesAvanzado otro = (await AvanzarAsync(caso.EmpresaId, esteMes)).ShouldHaveSingleItem(
            "a un mes posterior, el corte avanza ese mes y ninguno más");

        (otro.Mes, otro.Instantaneas).ShouldBe((esteMes, 2));

        FotoDelMes arrastrada = hastaHoy.Single(foto => foto.Clave == estropeada.Clave && foto.Mes == esteMes);
        decimal deLasDos = hastaHoy.Where(foto => foto.Mes == esteMes).Sum(foto => foto.Fisico);

        (await CorteAsync(caso.EmpresaId)).ShouldBe(esteMes);
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            [.. hastaHoy.Select(foto =>
                foto == estropeada || foto == arrastrada ? foto with { Fisico = foto.Fisico + 10m } : foto)],
            "el mes nuevo sale del anterior, y no del libro entero");

        // EL CUADRE DE LO PENDIENTE MIRA SOLO ESE MES, y en él la cadena cuadra: la de este mes es la
        // del anterior más el libro. Lo que lo delata es lo que se compara con el corte más el libro
        // desde el corte (ADR-0067 §7): la existencia de su clave y la valoración de su artículo.
        CuadreHecho deEsteMes = (await CuadrarLoPendienteAsync(caso.EmpresaId)).ShouldNotBeNull(
            "el corte ha subido un mes, y ese mes queda por cuadrar");

        (deEsteMes.Desde, deEsteMes.Hasta).ShouldBe((esteMes, esteMes));
        deEsteMes.Cuadre.InstantaneasComparadas.ShouldBe(2);

        deEsteMes.Cuadre.Descuadres.Select(AHallado).ShouldBe(
            [
                new Hallado("existencia", estropeada.Clave.UbicacionId, null, arrastrada.Fisico + 10m, arrastrada.Fisico, 1),
                new Hallado("valoracion-cantidad", null, null, deLasDos + 10m, deLasDos, 1),
            ],
            ignoreOrder: true);

        // EL CUADRE ENTERO LA ENCUENTRA, ADEMÁS, DONDE SE ESTROPEÓ: en ese mes la cadena se rompe.
        (await LasExistencias.CuadrarAsync(postgres, caso.EmpresaId)).Descuadres.Select(AHallado).ShouldBe(
            [
                new Hallado("instantanea", estropeada.Clave.UbicacionId, elQueToca, estropeada.Fisico, estropeada.Fisico + 10m, 1),
                new Hallado("existencia", estropeada.Clave.UbicacionId, null, arrastrada.Fisico + 10m, arrastrada.Fisico, 1),
                new Hallado("valoracion-cantidad", null, null, deLasDos + 10m, deLasDos, 1),
            ],
            ignoreOrder: true);

        // Y EL RECÁLCULO LA ARREGLA, mes a mes desde el primero (ADR-0067 §3), y deja todo por cuadrar.
        (await LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, esteMes)).ShouldBe(hastaHoy.Count);
        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(hastaHoy);
        (await CuadradoHastaAsync(caso.EmpresaId)).ShouldBeNull("volver atrás deja lo cuadrado en nada");

        CuadreHecho limpio = (await CuadrarLoPendienteAsync(caso.EmpresaId)).ShouldNotBeNull();

        (limpio.Desde, limpio.Hasta).ShouldBe((marzo, esteMes));
        limpio.Cuadre.InstantaneasComparadas.ShouldBe(hastaHoy.Count);
        limpio.Cuadre.Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// Una confirmación de una clave nueva entre dos meses de una puesta al día: el mes siguiente la
    /// espera, y la lleva hasta el corte (ítem 2.18, ADR-0067 §1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La confirmación entra después del primer mes</b>, con el corte ya en él, y se queda dentro
    /// con el compartido de su empresa tomado: deja la instantánea de su clave en ese mes, que es el
    /// corte que lee. La puesta al día sigue como la sigue el trabajo, un mes por llamada en un solo
    /// ámbito, y el mes siguiente se para en el exclusivo hasta el <c>COMMIT</c> de la confirmación.
    /// </para>
    /// <para>
    /// <b>Sin el exclusivo en el paso, el mes siguiente no esperaría</b>, y no vería ni la fila viva
    /// ni la instantánea de la clave nueva, que todavía no están confirmadas: a la clave le faltarían
    /// todos los meses desde ese hasta el corte. La confirmación escribe antes de que el paso llegue, y
    /// es justo lo que el paso tiene que ver: la carrera es sobre lo que ella escribe (epílogo del
    /// 2.11).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Una_clave_nueva_entre_dos_meses_de_una_puesta_al_dia_la_espera_el_mes_siguiente()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(875, "EXI-AZ", 875, ubicaciones: 2, conElAnioPasado: true);

        int pasado = Hoy.Year - 1;
        DateOnly marzo = new(pasado, 3, 1);
        DateOnly elQueToca = LasExistencias.MesDe(Hoy).AddMonths(-1);

        await using ElModuloDeInventario modulo = new(postgres, caso.EmpresaId);

        await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 3, 10), new Linea(0, 5m));

        // EL PRIMER MES DE LA PUESTA AL DÍA, suelto: sin corte, empieza en el de su primer movimiento.
        MesAvanzado primero = (await AvanzarUnMesAsync(caso.EmpresaId, elQueToca)).ShouldNotBeNull();

        (primero.Mes, primero.Instantaneas).ShouldBe((marzo, 1));

        // TRANSACCIÓN 1: una clave NUEVA, la otra ubicación, con fecha del mes del corte y por detrás de
        // nada (ADR-0047): el artículo se movió en ese almacén el día 10, y esta es del 20. Confirma y
        // no suelta.
        DateOnly delVeinte = new(pasado, 3, 20);
        Guid ajusteId = await AbrirAsync(modulo, caso, caso.SerieDe(delVeinte).Id, delVeinte, [new Linea(1, 4m)]);

        (Resultado<AjusteDto> confirmacion, IDbContextTransaction enVuelo) =
            await modulo.ConfirmarYQuedarseDentroAsync(ajusteId);

        Task<IReadOnlyList<MesAvanzado>> elResto;

        await using (enVuelo)
        {
            confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

            // TRANSACCIÓN 2: el resto de la puesta al día, como la hace el trabajo. Su primer mes se
            // para en el exclusivo, detrás de la confirmación.
            elResto = AvanzarAsync(caso.EmpresaId, elQueToca);

            await EsperarAQueLaFreneAsync(modulo.ProcesoDeLaBase, elResto);

            await enVuelo.CommitAsync();
        }

        IReadOnlyList<MesAvanzado> meses = await elResto.WaitAsync(TimeSpan.FromSeconds(60));
        int cuantos = ((elQueToca.Year - marzo.Year) * 12) + elQueToca.Month - marzo.Month;

        // CADA MES DEL RESTO LLEVA LAS DOS CLAVES, desde el primero: el que esperó ya vio la nueva.
        meses.Select(mes => (mes.Mes, mes.Instantaneas)).ShouldBe(
            [.. Enumerable.Range(1, cuantos).Select(mes => (marzo.AddMonths(mes), 2))],
            "el mes que esperó a la confirmación tiene que haber visto su clave, y los demás arrastrarla");

        IReadOnlyList<FotoDelMes> debidas = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), elQueToca);

        (await CorteAsync(caso.EmpresaId)).ShouldBe(elQueToca);

        (await LasExistencias.InstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            debidas, "la clave nueva tiene su instantánea en cada mes, desde el suyo hasta el corte");

        (await LasExistencias.ValoresDeLasInstantaneasAsync(postgres, caso.EmpresaId)).ShouldBe(
            LasExistencias.ValoresDebidosEnCSharp(
                await LasExistencias.LibroValoradoAsync(postgres, caso.EmpresaId), elQueToca));

        CuadreHecho cuadre = (await CuadrarLoPendienteAsync(caso.EmpresaId)).ShouldNotBeNull();

        (cuadre.Desde, cuadre.Hasta).ShouldBe((marzo, elQueToca));
        cuadre.Cuadre.ExistenciasComparadas.ShouldBe(2);
        cuadre.Cuadre.InstantaneasComparadas.ShouldBe(debidas.Count);
        cuadre.Cuadre.Descuadres.ShouldBeEmpty();
    }

    /// <summary>
    /// Un cuadre que falla a medias deja anotado hasta dónde llegó, y la vuelta siguiente sigue desde
    /// ahí (ítem 2.18, ADR-0067 §5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El fallo lo pone el caso</b>, con un interceptor en el contexto del cuadre que revienta la
    /// lectura de un mes, y solo la de ese: es lo que se ve cuando la orden pasa de su plazo o la
    /// conexión se cae a mitad de una puesta al día. Los meses de antes ya tienen su marca, cada uno
    /// la suya; el del fallo y los de después, no.
    /// </para>
    /// <para>
    /// <b>La vuelta siguiente va por el anfitrión</b>, como el trabajo, y cuadra solo lo que faltaba:
    /// desde el mes del fallo hasta el corte. Lo que compara se cuenta, así que un cuadre que volviera
    /// a empezar, o que se saltara el mes del fallo, no daría las mismas cifras.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_cuadre_que_falla_a_medias_sigue_en_la_vuelta_siguiente_desde_el_ultimo_mes_que_anoto()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(899, "EXI-BA", 899, ubicaciones: 2, conElAnioPasado: true);

        int pasado = Hoy.Year - 1;
        DateOnly junio = new(pasado, 6, 1);
        DateOnly elQueToca = LasExistencias.MesDe(Hoy).AddMonths(-1);

        await using (ElModuloDeInventario modulo = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 3, 10), new Linea(0, 5m));
            await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 7, 20), new Linea(1, 7m));
        }

        (await AvanzarAsync(caso.EmpresaId, elQueToca)).ShouldNotBeEmpty();

        // Lo que falta por cuadrar es lo que lee el trabajo al acabar la vuelta (ítem 2.20): sin nada
        // cuadrado, desde el primer mes con movimiento.
        (await SinCuadrarDesdeAsync(caso.EmpresaId)).ShouldBe(new DateOnly(pasado, 3, 1));

        IReadOnlyList<FotoDelMes> debidas = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), elQueToca);

        // LA PRIMERA VUELTA, CON JUNIO ROTO: marzo, abril y mayo se cuadran y se anotan, y la lectura
        // de junio revienta.
        ElMesQueRevienta rotura = new(junio);

        await using (InventarioDbContext contexto = ElContextoCon(caso.EmpresaId, rotura))
        {
            InvalidOperationException fallo = await Should.ThrowAsync<InvalidOperationException>(
                new ElCuadreDeLasExistencias(contexto, TimeProvider.System)
                    .CuadrarLoPendienteAsync(caso.EmpresaId, CancellationToken.None));

            fallo.InnerException.ShouldBeOfType<TimeoutException>("lo que falló tiene que ser la rotura del caso");
        }

        rotura.Reventadas.ShouldBe(1, "la rotura tiene que haber encontrado la lectura de su mes, y una vez");

        (await CuadradoHastaAsync(caso.EmpresaId)).ShouldBe(
            junio.AddMonths(-1), "lo que se cuadró antes del fallo se queda anotado, mes a mes");

        (await SinCuadrarDesdeAsync(caso.EmpresaId)).ShouldBe(
            junio, "un cuadre parado deja por cuadrar desde el mes en que paró hasta el corte");

        // LA VUELTA SIGUIENTE, por el anfitrión: desde junio hasta el corte, ni un mes antes.
        CuadreHecho siguiente = (await CuadrarLoPendienteAsync(caso.EmpresaId)).ShouldNotBeNull(
            "quedaba por cuadrar desde junio, y la vuelta siguiente tiene que cuadrarlo");

        (siguiente.Desde, siguiente.Hasta).ShouldBe((junio, elQueToca));
        siguiente.Cuadre.ExistenciasComparadas.ShouldBe(2);
        siguiente.Cuadre.InstantaneasComparadas.ShouldBe(debidas.Count(foto => foto.Mes >= junio));
        siguiente.Cuadre.Descuadres.ShouldBeEmpty();

        (await CuadradoHastaAsync(caso.EmpresaId)).ShouldBe(elQueToca);
        (await SinCuadrarDesdeAsync(caso.EmpresaId)).ShouldBeNull();
        (await CuadrarLoPendienteAsync(caso.EmpresaId)).ShouldBeNull("y ya no queda nada");
    }

    /// <summary>
    /// Un cuadre que se cruza con un recálculo no anota el mes que comparó, y la vuelta siguiente lo
    /// compara otra vez sobre lo que el recálculo dejó (ítem 2.18, ADR-0067 §5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El cruce lo pone el caso</b>, con un interceptor en el contexto del cuadre que, cuando la
    /// lectura del primer mes ya ha salido, recalcula la empresa por otra conexión, y solo esa vez. El
    /// recálculo vuelve atrás hasta quitar el corte y avanza otra vez hasta el mismo mes: el corte es
    /// el de antes, pero su fila es otra, y lo que el cuadre comparó era la de antes.
    /// </para>
    /// <para>
    /// <b>Lo que lo protege es la versión de la fila del corte</b>, leída con el mes en la misma foto
    /// (<see cref="ElCuadreDeLasExistencias.SqlDeLaMarca"/>): la marca no toca ninguna fila, y el
    /// cuadre para en ese mes. Sin ella, anotaría marzo en la fila nueva y seguiría hasta el corte
    /// como si nada hubiera pasado.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_cuadre_que_se_cruza_con_un_recalculo_no_anota_lo_que_comparo()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(900, "EXI-BB", 900, ubicaciones: 2, conElAnioPasado: true);

        int pasado = Hoy.Year - 1;
        DateOnly marzo = new(pasado, 3, 1);
        DateOnly elQueToca = LasExistencias.MesDe(Hoy).AddMonths(-1);

        await using (ElModuloDeInventario modulo = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 3, 10), new Linea(0, 5m));
            await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 7, 20), new Linea(1, 7m));
        }

        (await AvanzarAsync(caso.EmpresaId, elQueToca)).ShouldNotBeEmpty();
        (await CuadradoHastaAsync(caso.EmpresaId)).ShouldBeNull("el avance no cuadra, y el caso cuadra después");

        IReadOnlyList<FotoDelMes> debidas = LasExistencias.DebidasEnCSharp(
            await LasExistencias.LibroAsync(postgres, caso.EmpresaId), elQueToca);

        // EL CUADRE, CON EL RECÁLCULO CRUZADO en la lectura de marzo, que es su primer mes.
        ElRecalculoQueSeCruza cruce = new(
            marzo, () => LasExistencias.RecalcularAsync(postgres, caso.EmpresaId, elQueToca));

        CuadreDeLoPendiente? cruzado;

        await using (InventarioDbContext contexto = ElContextoCon(caso.EmpresaId, cruce))
        {
            cruzado = await new ElCuadreDeLasExistencias(contexto, TimeProvider.System)
                .CuadrarLoPendienteAsync(caso.EmpresaId, CancellationToken.None);
        }

        cruce.Cruces.ShouldBe(1, "el cruce tiene que haber encontrado la lectura de su mes, y una vez");
        cruce.Repuestas.ShouldBe(debidas.Count, "y el recálculo, haber repuesto todas las instantáneas");

        CuadreDeLoPendiente cuadre = cruzado.ShouldNotBeNull("marzo se llegó a comparar");

        (cuadre.Desde, cuadre.Hasta).ShouldBe((marzo, marzo), "el cuadre para en el mes en que se cruzó");
        cuadre.Cuadre.InstantaneasComparadas.ShouldBe(debidas.Count(foto => foto.Mes == marzo));
        cuadre.Cuadre.Descuadres.ShouldBeEmpty();

        (await CuadradoHastaAsync(caso.EmpresaId)).ShouldBeNull(
            "lo comparado era el corte de antes del recálculo, y no se anota en el de después");
        (await CorteAsync(caso.EmpresaId)).ShouldBe(elQueToca, "el recálculo deja el corte donde estaba");

        // LA VUELTA SIGUIENTE, por el anfitrión: otra vez desde marzo, sobre lo que dejó el recálculo.
        CuadreHecho siguiente = (await CuadrarLoPendienteAsync(caso.EmpresaId)).ShouldNotBeNull(
            "marzo no quedó anotado, y la vuelta siguiente tiene que cuadrarlo");

        (siguiente.Desde, siguiente.Hasta).ShouldBe((marzo, elQueToca));
        siguiente.Cuadre.ExistenciasComparadas.ShouldBe(2);
        siguiente.Cuadre.InstantaneasComparadas.ShouldBe(debidas.Count);
        siguiente.Cuadre.Descuadres.ShouldBeEmpty();

        (await CuadradoHastaAsync(caso.EmpresaId)).ShouldBe(elQueToca);
    }

    /// <summary>
    /// El cuadre de lo pendiente lee cada mes sin el JIT del motor, y la marca, que va fuera de la
    /// foto, lo encuentra como lo tiene la conexión (ítem 2.20, ADR-0067).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lo mira un interceptor en el contexto del cuadre</b>, que antes de cada orden pregunta al
    /// motor cómo tiene el JIT, por la misma conexión y en la misma transacción: cada lectura, dentro
    /// de la foto de su mes, y cada marca, fuera. Cuenta las dos, porque un selector que no
    /// encontrara nada dejaría las listas vacías, y el caso las afirma enteras.
    /// </para>
    /// <para>
    /// <b>La marca es la pareja</b>: el sitio donde el JIT sigue encendido, porque el motor del
    /// contenedor lo tiene así por omisión. Sin ella, un motor con el JIT apagado de fábrica daría las
    /// lecturas apagadas sin que el cuadre hiciera nada. <b>Y la conexión se abre antes</b>, para que
    /// EF no la cierre entre una orden y la siguiente: cerrada, el grupo de Npgsql la devuelve con
    /// <c>DISCARD ALL</c>, y un <c>SET</c> sin <c>LOCAL</c> que se saliera de la foto no se vería.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_cuadre_lee_cada_mes_sin_jit_y_la_marca_lo_encuentra_como_lo_tiene_la_conexion()
    {
        ElAlmacenDelCaso caso =
            await UnAlmacenAsync(923, "EXI-BC", 923, ubicaciones: 2, conElAnioPasado: true);

        int pasado = Hoy.Year - 1;
        DateOnly marzo = new(pasado, 3, 1);
        DateOnly elQueToca = LasExistencias.MesDe(Hoy).AddMonths(-1);

        await using (ElModuloDeInventario modulo = new(postgres, caso.EmpresaId))
        {
            await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 3, 10), new Linea(0, 5m));
            await ConfirmarAsync(modulo, caso, new DateOnly(pasado, 7, 20), new Linea(1, 7m));
        }

        (await AvanzarAsync(caso.EmpresaId, elQueToca)).ShouldNotBeEmpty();

        ElJitDeCadaOrden jit = new();
        CuadreDeLoPendiente? cuadrado;

        await using (InventarioDbContext contexto = ElContextoCon(caso.EmpresaId, jit))
        {
            await contexto.Database.OpenConnectionAsync();

            cuadrado = await new ElCuadreDeLasExistencias(contexto, TimeProvider.System)
                .CuadrarLoPendienteAsync(caso.EmpresaId, CancellationToken.None);
        }

        CuadreDeLoPendiente cuadre = cuadrado.ShouldNotBeNull("quedaba todo por cuadrar");

        (cuadre.Desde, cuadre.Hasta).ShouldBe((marzo, elQueToca));
        cuadre.Cuadre.Descuadres.ShouldBeEmpty();

        int meses = ((elQueToca.Year - pasado) * 12) + elQueToca.Month - 3 + 1;

        jit.Marcas.ShouldBe(
            Enumerable.Repeat("on", meses), "una marca por mes, fuera de la foto, con el JIT de la conexión");
        jit.Lecturas.Count.ShouldBe(
            (2 * meses) + 1, "el corte y el mes en cada foto, y en la primera, el primer mes por cuadrar");
        jit.Lecturas.ShouldAllBe(ajuste => ajuste == "off", "cada lectura del cuadre va sin JIT");
    }

    [Fact]
    public async Task Las_empresas_del_avance_son_las_que_tienen_existencias_cada_una_una_vez()
    {
        ElAlmacenDelCaso conExistencias = await UnAlmacenAsync(864, "EXI-AW", 864, ubicaciones: 2);
        (_, EmpresaDto sinExistencias) = await EnUnaEmpresaNuevaAsync(865);

        await using (ElModuloDeInventario modulo = new(postgres, conExistencias.EmpresaId))
        {
            await ConfirmarAsync(modulo, conExistencias, Hoy, new Linea(0, 2m), new Linea(1, 3m));
        }

        // Por el contenedor del anfitrión, con el inquilino de verdad y sin petición: sin el ámbito
        // de su motivo, la primera consulta lanzaría porque no hay empresa por la que filtrar.
        IReadOnlyList<Guid> empresas;

        using (IServiceScope ambito = _api.Services.CreateScope())
        {
            empresas = await ambito.ServiceProvider
                .GetRequiredService<IAvanceDelCorte>()
                .EmpresasConExistenciasAsync(CancellationToken.None);
        }

        empresas.Count(empresaId => empresaId == conExistencias.EmpresaId).ShouldBe(
            1, "tiene dos existencias y sale una vez");

        empresas.ShouldNotContain(
            sinExistencias.Id, "una empresa sin existencias no tiene instantáneas que avanzar");

        empresas.ShouldBe([.. empresas.Order()], "en orden de identificador, y sin repetir");
        empresas.Count.ShouldBe(empresas.Distinct().Count());
    }

    [Fact]
    public async Task El_corte_de_cada_empresa_es_su_mes_y_el_primer_movimiento_solo_lo_lleva_la_que_no_tiene()
    {
        ElAlmacenDelCaso sinCorte =
            await UnAlmacenAsync(873, "EXI-AX", 873, ubicaciones: 2, conElAnioPasado: true);
        ElAlmacenDelCaso conCorte = await UnAlmacenAsync(874, "EXI-AY", 874);

        DateOnly primero = new(Hoy.Year - 1, 5, 14);
        DateOnly elQueToca = LasExistencias.MesDe(Hoy).AddMonths(-1);

        // Tres fechas en tres meses, porque el almacén no admite una anterior a su último movimiento:
        // una lectura que tomara el más reciente, o uno cualquiera, saldría con otra fecha.
        await using (ElModuloDeInventario modulo = new(postgres, sinCorte.EmpresaId))
        {
            await ConfirmarAsync(modulo, sinCorte, primero, new Linea(1, 3m));
            await ConfirmarAsync(modulo, sinCorte, new DateOnly(Hoy.Year - 1, 8, 2), new Linea(0, 4m));
            await ConfirmarAsync(modulo, sinCorte, Hoy, new Linea(0, 1m));
        }

        await using (ElModuloDeInventario modulo = new(postgres, conCorte.EmpresaId))
        {
            await ConfirmarAsync(modulo, conCorte, Hoy, new Linea(0, 2m));
        }

        // La otra empresa tiene corte antes de leer: así hay un corte que no es de la del caso, y una
        // empresa con movimientos cuya fecha no se tiene que leer.
        (await AvanzarAsync(conCorte.EmpresaId, elQueToca)).ShouldNotBeEmpty();

        IReadOnlyList<CorteDeUnaEmpresa> antes = await CortesAsync();

        antes.Where(corte => corte.EmpresaId == sinCorte.EmpresaId).ShouldHaveSingleItem().ShouldBe(
            new CorteDeUnaEmpresa(sinCorte.EmpresaId, null, primero),
            "sin corte, la fecha del primer movimiento: es desde donde le faltan los meses");

        antes.Where(corte => corte.EmpresaId == conCorte.EmpresaId).ShouldHaveSingleItem().ShouldBe(
            new CorteDeUnaEmpresa(conCorte.EmpresaId, elQueToca, null, elQueToca),
            "con corte, su mes, y la fecha no se busca; y sin nada cuadrado, le falta desde donde " +
            "empezaría su cuadre, que es el corte mismo: su único movimiento es de después");

        antes.ShouldAllBe(
            corte => corte.HastaElMes == null || corte.PrimerMovimiento == null,
            "ninguna empresa con corte lleva la fecha de su primer movimiento");

        antes.Select(corte => corte.EmpresaId).ShouldBe(
            [.. antes.Select(corte => corte.EmpresaId).Distinct().Order()],
            "una por empresa, en orden de identificador");

        // Y AL AVANZAR, la del caso pasa a tener su corte, y deja de llevar la fecha. Le falta por
        // cuadrar desde el mes de su primer movimiento, que es el de su primera instantánea.
        (await AvanzarAsync(sinCorte.EmpresaId, elQueToca)).ShouldNotBeEmpty();

        (await CortesAsync()).Where(corte => corte.EmpresaId == sinCorte.EmpresaId).ShouldHaveSingleItem()
            .ShouldBe(new CorteDeUnaEmpresa(sinCorte.EmpresaId, elQueToca, null, LasExistencias.MesDe(primero)));

        // Y CUADRADA hasta el corte, ya no le falta nada.
        (await CuadrarLoPendienteAsync(conCorte.EmpresaId)).ShouldNotBeNull();
        (await SinCuadrarDesdeAsync(conCorte.EmpresaId)).ShouldBeNull("cuadrada hasta el corte, no le falta nada");
    }

    [Fact]
    public void El_anfitrion_de_los_casos_no_arranca_el_trabajo_mensual_y_la_purga_si()
    {
        // La purga es la pareja: con base, los trabajos de fondo arrancan, y el único que falta es
        // el que la configuración de este anfitrión apaga (ADR-0063 §7).
        string[] trabajos = [.. _api.Services.GetServices<IHostedService>().Select(trabajo => trabajo.GetType().Name)];

        trabajos.ShouldContain("PurgadorDeRecibos");
        trabajos.ShouldNotContain(
            nameof(AvanzadorDelCorte),
            "los anfitriones de este carril comparten la base, y el trabajo avanzaría el corte de " +
            "las empresas de los demás casos a mitad de su caso");
    }

    [Fact]
    public async Task Anotar_el_libro_sin_transaccion_sin_inquilino_o_con_filas_de_otra_empresa_revienta()
    {
        var empresaId = Guid.CreateVersion7();
        IReadOnlyList<MovimientoStock> movimientos = UnasFilasDelLibro(empresaId);

        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        // SIN TRANSACCIÓN, la sentencia de la proyección se confirmaría sola, y el libro —que se
        // guarda después, en el SaveChanges— podría no llegar a escribirse nunca.
        RepositorioDeAjustes suyo = new(contexto, new InquilinoFijo(empresaId));

        InvalidOperationException sinTransaccion = await Should.ThrowAsync<InvalidOperationException>(
            () => suyo.AnotarEnElLibroAsync(movimientos, CancellationToken.None));

        sinTransaccion.Message.ShouldContain("No hay transacción abierta");

        // CON FILAS DE OTRA EMPRESA, la sentencia sumaría con la empresa del inquilino lo que el
        // libro apunta en otra.
        await using IDbContextTransaction transaccion = await contexto.Database.BeginTransactionAsync();

        // SIN INQUILINO, la sentencia no sabría en qué empresa sumar. Va dentro de la transacción
        // para que la de la transacción no salte antes que la suya. Y se afirma el mensaje, no solo
        // el tipo: si el repositorio sumara en una empresa vacía, la comprobación de la otra
        // empresa lo pararía igual, con otras palabras, y el caso seguiría en verde sin la suya.
        RepositorioDeAjustes deNadie = new(contexto, new InquilinoFijo(null));

        InvalidOperationException sinInquilino = await Should.ThrowAsync<InvalidOperationException>(
            () => deNadie.AnotarEnElLibroAsync(movimientos, CancellationToken.None));

        sinInquilino.Message.ShouldContain("sin inquilino");

        RepositorioDeAjustes deOtra = new(contexto, new InquilinoFijo(Guid.CreateVersion7()));

        InvalidOperationException ajenas = await Should.ThrowAsync<InvalidOperationException>(
            () => deOtra.AnotarEnElLibroAsync(movimientos, CancellationToken.None));

        ajenas.Message.ShouldContain("de otra empresa");

        await transaccion.RollbackAsync();
    }

    /// <summary>La fecha de hoy, en el mismo calendario que usa el caso de uso.</summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Unas filas del libro hechas por el dominio, sin guardar nada.</summary>
    /// <param name="empresaId">La empresa del documento.</param>
    /// <returns>Lo que la confirmación devolvería para anotar.</returns>
    private static IReadOnlyList<MovimientoStock> UnasFilasDelLibro(Guid empresaId)
    {
        DateTimeOffset momento = DateTimeOffset.UtcNow;
        var almacenId = Guid.CreateVersion7();

        var ajuste = Ajuste.Abrir(
            empresaId, Guid.CreateVersion7(), almacenId, Hoy, "Recuento del ítem 2.7", "EUR", momento);

        ajuste.AnadirLinea(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            3m,
            Guid.CreateVersion7(),
            1m,
            1.50m,
            momento);

        return ajuste.Confirmar(
            1,
            new AjusteConfirmado(ajuste.Id, empresaId, almacenId, Hoy, 1),
            ElLibro.ValoracionDesdeCero(ajuste),
            LotesYSeriesResueltos.Ninguno,
            momento);
    }

    /// <summary>
    /// Avanza el corte de una empresa por el contenedor del anfitrión, como lo hace el trabajo: un
    /// mes por llamada, en un solo ámbito, hasta que llega (ADR-0067 §1).
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="hastaElMes">El mes al que tiene que llegar.</param>
    /// <returns>Los meses que ha hecho, en orden; ninguno si el corte ya llegaba.</returns>
    private async Task<IReadOnlyList<MesAvanzado>> AvanzarAsync(Guid empresaId, DateOnly hastaElMes)
    {
        using IServiceScope ambito = _api.Services.CreateScope();

        IAvanceDelCorte avance = ambito.ServiceProvider.GetRequiredService<IAvanceDelCorte>();
        List<MesAvanzado> meses = [];

        while (await avance.AvanzarUnMesAsync(empresaId, hastaElMes, CancellationToken.None) is { } mes)
        {
            meses.Add(mes);

            // CON TOPE: un avance que no moviera el corte no pararía nunca, y el caso se colgaría.
            meses.Count.ShouldBeLessThan(240, "un avance que no mueve el corte no para nunca");
        }

        return meses;
    }

    /// <summary>Un solo mes del avance del corte de una empresa, por el contenedor del anfitrión.</summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="hastaElMes">El mes al que tiene que llegar.</param>
    /// <returns>El mes que ha hecho, o <c>null</c> si el corte ya llegaba.</returns>
    private async Task<MesAvanzado?> AvanzarUnMesAsync(Guid empresaId, DateOnly hastaElMes)
    {
        using IServiceScope ambito = _api.Services.CreateScope();

        return await ambito.ServiceProvider
            .GetRequiredService<IAvanceDelCorte>()
            .AvanzarUnMesAsync(empresaId, hastaElMes, CancellationToken.None);
    }

    /// <summary>
    /// Cuadra lo que la empresa no tiene cuadrado, por el contenedor del anfitrión, como lo hace el
    /// trabajo detrás del avance (ADR-0067 §5).
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>Lo que ha cuadrado, o <c>null</c> si no quedaba nada.</returns>
    private async Task<CuadreHecho?> CuadrarLoPendienteAsync(Guid empresaId)
    {
        using IServiceScope ambito = _api.Services.CreateScope();

        return await ambito.ServiceProvider
            .GetRequiredService<IAvanceDelCorte>()
            .CuadrarLoPendienteAsync(empresaId, CancellationToken.None);
    }

    /// <summary>
    /// El corte de cada empresa con existencias, por el contenedor del anfitrión y sin petición,
    /// como lo lee el trabajo al acabar la vuelta.
    /// </summary>
    /// <returns>Lo que contesta el avance.</returns>
    private async Task<IReadOnlyList<CorteDeUnaEmpresa>> CortesAsync()
    {
        using IServiceScope ambito = _api.Services.CreateScope();

        return await ambito.ServiceProvider
            .GetRequiredService<IAvanceDelCorte>()
            .CortesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Desde dónde le falta por cuadrar a una empresa, como lo lee el trabajo al acabar la vuelta
    /// (ítem 2.20).
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>El primer mes sin cuadrar, o <c>null</c> si lo tiene cuadrado hasta el corte.</returns>
    private async Task<DateOnly?> SinCuadrarDesdeAsync(Guid empresaId) =>
        (await CortesAsync()).Where(corte => corte.EmpresaId == empresaId).ShouldHaveSingleItem().SinCuadrarDesde;

    /// <summary>El corte guardado de una empresa, leído por debajo del sistema.</summary>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>El mes, o <c>null</c> si no tiene.</returns>
    private async Task<DateOnly?> CorteAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand leer = new(
            "SELECT c.hasta_el_mes FROM inventario.cortes_de_la_instantanea AS c WHERE c.empresa_id = @empresa",
            conexion);

        leer.Parameters.AddWithValue("empresa", empresaId);

        return await leer.ExecuteScalarAsync() is DateOnly mes ? mes : null;
    }

    /// <summary>Hasta dónde está cuadrado el corte de una empresa, leído por debajo del sistema.</summary>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>El mes, o <c>null</c> si no tiene nada cuadrado o no tiene corte.</returns>
    private async Task<DateOnly?> CuadradoHastaAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand leer = new(
            "SELECT c.cuadrado_hasta FROM inventario.cortes_de_la_instantanea AS c WHERE c.empresa_id = @empresa",
            conexion);

        leer.Parameters.AddWithValue("empresa", empresaId);

        return await leer.ExecuteScalarAsync() is DateOnly mes ? mes : null;
    }

    /// <summary>
    /// Un contexto de Inventario de la empresa como el de <see cref="PostgresConTodosLosModulos.AbrirInventario"/>,
    /// con el interceptor del caso puesto.
    /// </summary>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="interceptor">El que revienta la lectura de un mes, o el que se cruza con ella.</param>
    /// <returns>El contexto, sin transacción.</returns>
    private InventarioDbContext ElContextoCon(Guid empresaId, DbCommandInterceptor interceptor)
    {
        DbContextOptionsBuilder<InventarioDbContext> opciones = new();
        InventarioDbContext.Configurar(opciones, postgres.CadenaDeConexion);
        opciones.AddInterceptors(interceptor);

        return new InventarioDbContext(opciones.Options, new InquilinoFijo(empresaId), new AccesoCerrado());
    }

    /// <summary>Un descuadre, sin lo que todos comparten.</summary>
    /// <param name="descuadre">Lo que contestó el cuadre.</param>
    /// <returns>Lo que el caso compara.</returns>
    private static Hallado AHallado(Descuadre descuadre) =>
        new(
            descuadre.Que,
            descuadre.UbicacionId,
            descuadre.Mes,
            descuadre.Esperado,
            descuadre.Guardado,
            descuadre.Filas);

    /// <summary>Tira a mano las instantáneas de la empresa, por debajo del sistema.</summary>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>Cuántas ha tirado.</returns>
    private async Task<int> BorrarLasInstantaneasAsync(Guid empresaId)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand borrar = new(
            "DELETE FROM inventario.instantaneas_mensuales WHERE empresa_id = @empresa", conexion);

        borrar.Parameters.AddWithValue("empresa", empresaId);

        return await borrar.ExecuteNonQueryAsync();
    }

    /// <summary>Escribe una fila a mano, con sus parámetros por posición, y exige que entre.</summary>
    /// <param name="conexion">La conexión, abierta.</param>
    /// <param name="sql">La sentencia, con <c>$1</c>, <c>$2</c>…</param>
    /// <param name="valores">Los valores, en ese orden.</param>
    /// <returns>Cuando la fila ha entrado.</returns>
    private static async Task EscribirAsync(NpgsqlConnection conexion, string sql, params object[] valores)
    {
        await using NpgsqlCommand orden = new(sql, conexion);

        foreach (object valor in valores)
        {
            orden.Parameters.Add(new NpgsqlParameter { Value = valor });
        }

        (await orden.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    /// <summary>Abre y confirma un ajuste con la serie del año de su fecha.</summary>
    /// <param name="modulo">El módulo cableado a mano de la empresa.</param>
    /// <param name="caso">Los maestros del caso.</param>
    /// <param name="fecha">La fecha de operación.</param>
    /// <param name="lineas">Las líneas, sobre las ubicaciones del caso.</param>
    /// <returns>El ajuste confirmado.</returns>
    private static async Task<Guid> ConfirmarAsync(
        ElModuloDeInventario modulo, ElAlmacenDelCaso caso, DateOnly fecha, params Linea[] lineas)
    {
        Guid ajusteId = await AbrirAsync(modulo, caso, caso.SerieDe(fecha).Id, fecha, lineas);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»");

        return ajusteId;
    }

    /// <summary>Abre un ajuste en borrador.</summary>
    /// <param name="modulo">El módulo cableado a mano de la empresa.</param>
    /// <param name="caso">Los maestros del caso.</param>
    /// <param name="serieId">La serie que lo numerará.</param>
    /// <param name="fecha">La fecha de operación.</param>
    /// <param name="lineas">Las líneas, sobre las ubicaciones del caso.</param>
    /// <returns>El borrador.</returns>
    private static async Task<Guid> AbrirAsync(
        ElModuloDeInventario modulo,
        ElAlmacenDelCaso caso,
        Guid serieId,
        DateOnly fecha,
        IReadOnlyList<Linea> lineas)
    {
        AbrirAjusteDto peticion = new(
            serieId,
            caso.AlmacenId,
            fecha,
            "Recuento del ítem 2.7",
            [.. lineas.Select(linea => new LineaDeAjusteDto(
                caso.Ubicaciones[linea.Ubicacion],
                caso.ArticuloId,
                linea.Cantidad,
                linea.Factor == 1m ? caso.UnidadId : caso.UnidadDelFactor[linea.Factor],
                linea.Cantidad > 0m ? 1.50m : null))]);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        return alta.Valor.Id;
    }

    /// <summary>
    /// Una empresa con su almacén, sus ubicaciones, un artículo, el ejercicio de este año con su
    /// serie y, si se pide, el del año pasado con la suya.
    /// </summary>
    /// <param name="semilla">La empresa del caso.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <param name="ubicaciones">Cuántas ubicaciones, que son cuántas claves puede mover.</param>
    /// <param name="conElAnioPasado">Si abre también el ejercicio del año pasado.</param>
    /// <param name="factores">Los factores distintos de 1 que van a teclear sus líneas, o ninguno.</param>
    /// <returns>Los maestros del caso.</returns>
    private async Task<ElAlmacenDelCaso> UnAlmacenAsync(
        int semilla,
        string codigo,
        int semillaDeInstalacion,
        int ubicaciones = 1,
        bool conElAnioPasado = false,
        IReadOnlyList<decimal>? factores = null)
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(semilla);

        EjercicioDto esteAnio = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year);
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, esteAnio.Id, codigo);

        SerieDto? delAnioPasado = null;

        if (conElAnioPasado)
        {
            EjercicioDto pasado = await LosMaestrosPorLaApi.CrearEjercicioAsync(cliente, Hoy.Year - 1);
            delAnioPasado = await LosMaestrosPorLaApi.CrearSerieEnAsync(cliente, pasado.Id, codigo + "-P");
        }

        return await LosMaestrosDeAsync(
            cliente, empresa.Id, codigo, semillaDeInstalacion, serie, delAnioPasado, ubicaciones, factores);
    }

    /// <summary>El almacén, las ubicaciones y el artículo, para una empresa y unas series que ya hay.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="empresaId">La empresa.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <param name="serie">La serie de este año.</param>
    /// <param name="delAnioPasado">La del año pasado, si la hay.</param>
    /// <param name="ubicaciones">Cuántas ubicaciones.</param>
    /// <param name="factores">
    /// Los factores distintos de 1 que van a teclear sus líneas: una unidad por cada uno, que
    /// convierte a la base del artículo. Desde el 2.17 el factor lo pone la conversión, no la línea
    /// (ADR-0065 §2).
    /// </param>
    /// <returns>Los maestros del caso.</returns>
    private static async Task<ElAlmacenDelCaso> LosMaestrosDeAsync(
        HttpClient cliente,
        Guid empresaId,
        string codigo,
        int semillaDeInstalacion,
        SerieDto serie,
        SerieDto? delAnioPasado = null,
        int ubicaciones = 1,
        IReadOnlyList<decimal>? factores = null)
    {
        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        List<Guid> huecos = [];

        for (int numero = 1; numero <= ubicaciones; numero++)
        {
            huecos.Add((await LosMaestrosPorLaApi.CrearUbicacionAsync(
                cliente, almacen.Id, $"{codigo}-{numero:00}")).Id);
        }

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semillaDeInstalacion);

        // EL CÓDIGO DE LA UNIDAD, DE LA SEMILLA DE INSTALACIÓN: es la que ya hace única la del
        // artículo, así que la de cada factor tampoco la repite nadie.
        Dictionary<decimal, Guid> unidadDelFactor = [];

        foreach ((decimal factor, int indice) in (factores ?? []).Select((factor, indice) => (factor, indice)))
        {
            unidadDelFactor[factor] = await LosMaestrosPorLaApi.CrearUnidadQueConvierteAAsync(
                cliente,
                string.Create(CultureInfo.InvariantCulture, $"K{semillaDeInstalacion}-{indice}"),
                factor,
                unidadId);
        }

        return new ElAlmacenDelCaso(
            empresaId, almacen.Id, huecos, articuloId, unidadId, unidadDelFactor, serie, delAnioPasado);
    }

    /// <summary>
    /// Coge la fila del corte de la empresa en una transacción aparte, y no la suelta hasta que el
    /// caso lo diga.
    /// </summary>
    /// <remarks>
    /// Es lo que deja un recálculo a medias con su cerrojo tomado: el cerrojo va lo primero, y lo
    /// siguiente es escribir esa fila. Se afirma que la fila estaba: sin ella, el recálculo no se
    /// pararía en ningún sitio y el caso ejercería dos operaciones seguidas.
    /// </remarks>
    /// <param name="empresaId">La empresa, que ya tiene corte.</param>
    /// <returns>La fila cogida, con su proceso.</returns>
    private async Task<ElCorteCogido> CogerElCorteAsync(Guid empresaId)
    {
        NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync();

        await using NpgsqlCommand coger = new(
            "SELECT c.hasta_el_mes FROM inventario.cortes_de_la_instantanea AS c "
            + "WHERE c.empresa_id = @empresa FOR UPDATE",
            conexion,
            transaccion);

        coger.Parameters.AddWithValue("empresa", empresaId);

        (await coger.ExecuteScalarAsync()).ShouldNotBeNull("la empresa del caso tiene que tener corte");

        return new ElCorteCogido(conexion, transaccion);
    }

    /// <summary>
    /// Espera a que el recálculo se pare en la fila del corte, y comprueba que lo hace con el
    /// cerrojo de las instantáneas ya tomado.
    /// </summary>
    /// <param name="corte">La fila cogida.</param>
    /// <param name="recalculo">El recálculo en vuelo.</param>
    /// <returns>El proceso del recálculo.</returns>
    private async Task<int> ElRecalculoParadoDentroAsync(ElCorteCogido corte, Task<int> recalculo)
    {
        await LaEspera.AQueLaFreneAsync(
            postgres.CadenaDeConexion,
            corte.Proceso,
            recalculo,
            "la fila del corte",
            "no ha escrito el corte, o lo ha escrito sin que nadie lo tuviera cogido");

        int delRecalculo = await LaEspera.LaParadaDetrasDeAsync(postgres.CadenaDeConexion, corte.Proceso);

        // EL CERROJO, TOMADO Y EN SU ESPACIO, sin mirar el modo ni la segunda clave: eso lo deciden
        // los casos. Sin esto, un recálculo sin cerrojo dejaría verde el de las dos empresas.
        (await CerrojosDeLasInstantaneasAsync(delRecalculo)).ShouldBe(
            1, "el recálculo está parado en el corte con el cerrojo de las instantáneas tomado");

        return delRecalculo;
    }

    /// <summary>
    /// Cuántos cerrojos consultivos del espacio de las instantáneas tiene concedidos un proceso.
    /// </summary>
    /// <remarks>
    /// Una clave de dos <c>int4</c> sale en <c>pg_locks</c> con la primera en <c>classid</c>, la
    /// segunda en <c>objid</c> y <c>objsubid</c> a 2; una de un <c>int8</c>, como la de la bandeja,
    /// con <c>objsubid</c> a 1.
    /// </remarks>
    /// <param name="proceso">El proceso de PostgreSQL.</param>
    /// <returns>Cuántos, en cualquier modo.</returns>
    private async Task<long> CerrojosDeLasInstantaneasAsync(int proceso)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand cuantos = new(
            "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND granted "
            + "AND pid = @proceso AND classid = @espacio::oid AND objsubid = 2",
            conexion);

        cuantos.Parameters.AddWithValue("proceso", proceso);
        cuantos.Parameters.AddWithValue("espacio", ClaveDelCerrojoDeLasInstantaneas.Espacio);

        return (long)(await cuantos.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Espera a que la operación en vuelo se quede parada detrás del proceso dado, o falla.
    /// </summary>
    /// <remarks>
    /// El mecanismo, y por qué no se suelta por tiempo, están en <see cref="LaEspera"/>. Aquí solo
    /// va a quién se espera y qué habría hecho mal la operación si no esperara.
    /// </remarks>
    /// <param name="procesoQueFrena">El proceso de PostgreSQL de la transacción en vuelo.</param>
    /// <param name="enVuelo">La operación que tiene que quedarse esperando.</param>
    private Task EsperarAQueLaFreneAsync(int procesoQueFrena, Task enVuelo) =>
        LaEspera.AQueLaFreneAsync(
            postgres.CadenaDeConexion,
            procesoQueFrena,
            enVuelo,
            "la transacción en vuelo",
            "ha decidido sin ver lo que la otra estaba a punto de confirmar");

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }

    /// <summary>La fila del corte de una empresa, cogida en una transacción aparte.</summary>
    /// <param name="conexion">Su conexión, abierta.</param>
    /// <param name="transaccion">Su transacción, que tiene la fila.</param>
    private sealed class ElCorteCogido(NpgsqlConnection conexion, NpgsqlTransaction transaccion)
        : IAsyncDisposable
    {
        /// <summary>El proceso de PostgreSQL que tiene la fila.</summary>
        internal int Proceso => conexion.ProcessID;

        /// <summary>Suelta la fila, sin escribir nada.</summary>
        /// <returns>Cuando la ha soltado.</returns>
        internal Task SoltarAsync() => transaccion.RollbackAsync();

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await transaccion.DisposeAsync();
            await conexion.DisposeAsync();
        }
    }

    /// <summary>
    /// Revienta la lectura del cuadre de un mes, y solo esa: la orden que devuelve filas y lleva el mes
    /// entre sus parámetros. Las demás pasan.
    /// </summary>
    /// <remarks>
    /// Revienta con un <see cref="TimeoutException"/>, que es lo que se ve cuando una orden pasa de su
    /// plazo, y la estrategia de ejecución del proveedor lo tiene por pasajero y lo envuelve en un
    /// <see cref="InvalidOperationException"/>, como en producción. Cuenta las que revienta: el caso
    /// afirma que encontró la suya, porque un selector que no encontrara nada dejaría pasar el cuadre
    /// entero, y el caso lo vería como un fallo que no ha ocurrido.
    /// </remarks>
    /// <param name="mes">El primer día del mes que revienta.</param>
    private sealed class ElMesQueRevienta(DateOnly mes) : DbCommandInterceptor
    {
        /// <summary>Cuántas lecturas ha reventado.</summary>
        internal int Reventadas { get; private set; }

        /// <inheritdoc/>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.Parameters.Cast<DbParameter>().Any(parametro => parametro.Value is DateOnly valor && valor == mes))
            {
                Reventadas++;

                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture, $"El caso revienta la lectura del cuadre de {mes:yyyy-MM}."));
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>
    /// Recalcula la empresa cuando la lectura del cuadre de un mes ya ha salido, y solo la primera vez:
    /// la orden que devuelve filas y lleva el mes entre sus parámetros. Las demás pasan.
    /// </summary>
    /// <remarks>
    /// El recálculo va por otra conexión, con su propio contexto, mientras la foto del cuadre sigue
    /// abierta: lo que el cuadre ha leído es de antes, y la fila del corte que anota, de después.
    /// Cuenta las lecturas en que se cruza y lo que repuso el recálculo: el caso afirma que encontró
    /// la suya, porque un selector que no encontrara nada dejaría el cuadre sin cruce, y la marca se
    /// pondría sin que la versión tuviera nada que proteger.
    /// </remarks>
    /// <param name="mes">El primer día del mes en cuya lectura se cruza.</param>
    /// <param name="recalcular">El recálculo, que devuelve cuántas instantáneas ha repuesto.</param>
    private sealed class ElRecalculoQueSeCruza(DateOnly mes, Func<Task<int>> recalcular) : DbCommandInterceptor
    {
        /// <summary>En cuántas lecturas del mes se ha cruzado.</summary>
        internal int Cruces { get; private set; }

        /// <summary>Cuántas instantáneas repuso el recálculo.</summary>
        internal int Repuestas { get; private set; }

        /// <inheritdoc/>
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            bool esLaDelMes = command.Parameters
                .Cast<DbParameter>()
                .Any(parametro => parametro.Value is DateOnly valor && valor == mes);

            if (esLaDelMes && ++Cruces == 1)
            {
                Repuestas = await recalcular();
            }

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>
    /// Pregunta al motor cómo tiene el JIT antes de cada orden del cuadre, por la misma conexión y en
    /// la misma transacción que ella: las lecturas, por un lado, y las marcas, por otro.
    /// </summary>
    /// <remarks>
    /// La pregunta va por una orden de ADO.NET, que no pasa por los interceptores de EF, así que no se
    /// mira a sí misma. Las órdenes que no devuelven filas y no son la marca, como la que apaga el JIT,
    /// no se miran.
    /// </remarks>
    private sealed class ElJitDeCadaOrden : DbCommandInterceptor
    {
        /// <summary>El JIT de cada lectura, en el orden en que llegaron.</summary>
        internal List<string> Lecturas { get; } = [];

        /// <summary>El JIT de cada marca, en el orden en que llegaron.</summary>
        internal List<string> Marcas { get; } = [];

        /// <inheritdoc/>
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Lecturas.Add(await ElJitDeAsync(command, cancellationToken));

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("SET cuadrado_hasta", StringComparison.Ordinal))
            {
                Marcas.Add(await ElJitDeAsync(command, cancellationToken));
            }

            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static async Task<string> ElJitDeAsync(DbCommand orden, CancellationToken cancelacion)
        {
            await using DbCommand pregunta = orden.Connection!.CreateCommand();
            pregunta.Transaction = orden.Transaction;
            pregunta.CommandText = "SELECT current_setting('jit')";

            return (string)(await pregunta.ExecuteScalarAsync(cancelacion))!;
        }
    }

    /// <summary>Una línea de ajuste sobre una de las ubicaciones del caso.</summary>
    /// <param name="Ubicacion">Cuál de las ubicaciones del caso, desde cero.</param>
    /// <param name="Cantidad">La cantidad tecleada, con signo.</param>
    /// <param name="Factor">
    /// El factor a unidad base: 1 es la unidad base del artículo, y otro, la unidad del caso que
    /// convierte a ella con ese factor.
    /// </param>
    private sealed record Linea(int Ubicacion, decimal Cantidad, decimal Factor = 1m);

    /// <summary>Un descuadre, sin lo que todos comparten.</summary>
    private sealed record Hallado(
        string Que, Guid? UbicacionId, DateOnly? Mes, decimal? Esperado, decimal? Guardado, long Filas);

    /// <summary>Los maestros de un caso.</summary>
    /// <param name="EmpresaId">La empresa.</param>
    /// <param name="AlmacenId">El almacén.</param>
    /// <param name="Ubicaciones">Sus ubicaciones: una clave por cada una.</param>
    /// <param name="ArticuloId">El artículo.</param>
    /// <param name="UnidadId">Su unidad base.</param>
    /// <param name="UnidadDelFactor">La unidad de cada factor que no es 1, si el caso los pidió.</param>
    /// <param name="Serie">La serie del ejercicio de este año.</param>
    /// <param name="SerieDelAnioPasado">La del año pasado, si el caso la abrió.</param>
    private sealed record ElAlmacenDelCaso(
        Guid EmpresaId,
        Guid AlmacenId,
        IReadOnlyList<Guid> Ubicaciones,
        Guid ArticuloId,
        Guid UnidadId,
        IReadOnlyDictionary<decimal, Guid> UnidadDelFactor,
        SerieDto Serie,
        SerieDto? SerieDelAnioPasado)
    {
        internal ClaveDeExistencia Clave(int ubicacion) =>
            new(ArticuloId, AlmacenId, Ubicaciones[ubicacion]);

        internal SerieDto SerieDe(DateOnly fecha) =>
            fecha.Year == Hoy.Year
                ? Serie
                : SerieDelAnioPasado ?? throw new InvalidOperationException(
                    "El caso no abrió el ejercicio del año pasado.");
    }
}
