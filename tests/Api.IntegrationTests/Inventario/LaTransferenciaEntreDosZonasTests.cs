using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Fechas;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Fechas;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Transferencias;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Inventario.Domain.Transferencias;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La transferencia entre dos almacenes de zonas distintas (ítem 3.5, ADR-0079): cada fecha es la de
/// la zona de su almacén, y la guarda compara instantes.
/// </summary>
/// <remarks>
/// <para>
/// <b>La península y Canarias, y el cambio de hora de octubre.</b> El 25 de octubre de 2026 a las
/// 23:30 UTC, en Madrid ya es el 26 y en Canarias todavía el 25. Los dos casos de la anulación paran
/// el reloj ahí, así que su ejercicio es el de 2026 y no el del año en curso: son días que ya
/// pasaron cualquier día que corra el carril.
/// </para>
/// <para>
/// <b>La zona se cambia por la API, con movimientos ya escritos</b>, que es lo que la puerta del 3.5
/// dejó hacer siempre. Las fechas que ya estaban no cambian, y la guarda mira las zonas como están
/// cuando comprueba.
/// </para>
/// <para>
/// <b>Semillas: del 739 al 741, y el 744</b>, del bloque de la transferencia que reparte la cabecera
/// de <c>LaTransferenciaTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaTransferenciaEntreDosZonasTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Motivo = "Se envió al almacén equivocado";

    // Madrid, el 26 a las 00:30; Canarias, el 25 a las 23:30.
    private static readonly DateTimeOffset s_madridYaEsElVeintiseis = new(2026, 10, 25, 23, 30, 0, TimeSpan.Zero);

    // Madrid, el 26 a la 01:00; Canarias, el 26 a las 00:00.
    private static readonly DateTimeOffset s_losDosEnElVeintiseis = new(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);

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
    /// La misma recepción, un día antes que su envío: con el destino en Madrid es un <c>422</c>, y
    /// con el destino en Canarias entra, y la base la guarda. Dos días antes, nunca. Y la ficha dice
    /// en cada momento el primer día que admite, que es el que el formulario pone de mínimo.
    /// </summary>
    /// <remarks>
    /// El 10 de junio empieza en Madrid a las 22:00 UTC del 9, y el 9 de Canarias acaba a las 23:00
    /// UTC: no acaba antes de que empiece el otro. El 8 de Canarias, sí. La base no sabe de zonas y
    /// admite un día de holgura, y el <c>UPDATE</c> a mano es lo que dice que no admite dos.
    /// </remarks>
    /// <returns>La tarea del caso.</returns>
    [Fact]
    public async Task La_recepcion_un_dia_antes_entra_solo_si_la_zona_del_destino_lo_admite_y_la_base_no_admite_dos()
    {
        int anio = EscenaDeTransferencia.Hoy.Year - 1;
        EscenaDeTransferencia escena = await MontarAsync(739, "TRZ-A", anio);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m, new DateOnly(anio, 6, 1));
        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 2m, new DateOnly(anio, 6, 10));

        // EN LA MISMA ZONA, un día antes es antes.
        (await LaPrimeraRecepcionAsync(escena, enviada.Id)).ShouldBe(new DateOnly(anio, 6, 10));
        await ExigirQueVaAntesDelEnvioAsync(escena, enviada.Id, new DateOnly(anio, 6, 9));

        await CambiarLaZonaAsync(escena, escena.AlmacenB, ZonaHorariaDto.Canarias);

        // CON EL DESTINO EN CANARIAS, la misma transferencia: dos días antes, sigue siendo antes.
        (await LaPrimeraRecepcionAsync(escena, enviada.Id)).ShouldBe(new DateOnly(anio, 6, 9));
        await ExigirQueVaAntesDelEnvioAsync(escena, enviada.Id, new DateOnly(anio, 6, 8));

        using (HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(enviada.Id, new DateOnly(anio, 6, 9)))
        {
            (await EscenaDeTransferencia.LeerAsync<TransferenciaDto>(recepcion)).Estado
                .ShouldBe(nameof(EstadoDeTransferencia.Recibida));
        }

        Transferencia recibida = await escena.LaTransferenciaAsync(postgres, enviada.Id);
        recibida.FechaDeEnvio.ShouldBe(new DateOnly(anio, 6, 10));
        recibida.FechaDeRecepcion.ShouldBe(new DateOnly(anio, 6, 9));

        // Recibida, ya no admite ninguna.
        (await LaPrimeraRecepcionAsync(escena, enviada.Id)).ShouldBeNull();

        // LA BASE, POR UNA VÍA QUE NO PASA POR C#: el día de holgura, y ni uno más.
        await using InventarioDbContext contexto = postgres.AbrirInventario(escena.EmpresaId);

        PostgresException choque = await Should.ThrowAsync<PostgresException>(
            () => contexto.Database.ExecuteSqlRawAsync(
                "UPDATE inventario.transferencias SET fecha_de_recepcion = fecha_de_envio - 2 WHERE id = {0}",
                enviada.Id));

        // `check_violation`.
        choque.SqlState.ShouldBe("23514");
        choque.ConstraintName.ShouldBe("ck_transferencias_recepcion_no_antes_del_envio");
    }

    /// <summary>
    /// Anulada a las 23:30 UTC del 25 de octubre, la salida del destino va el 25, el día de Canarias,
    /// y la vuelta al origen el 26, el de Madrid: el inverso y su libro, cada fecha en su zona.
    /// </summary>
    /// <returns>La tarea del caso.</returns>
    [Fact]
    public async Task La_anulacion_lleva_cada_fecha_y_cada_fila_del_libro_a_la_zona_de_su_almacen()
    {
        EscenaDeTransferencia escena = await MontarAsync(740, "TRZ-B", 2026);

        await CambiarLaZonaAsync(escena, escena.AlmacenB, ZonaHorariaDto.Canarias);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m, new DateOnly(2026, 10, 1));

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 2m, new DateOnly(2026, 10, 26));

        using (HttpResponseMessage recepcion =
            await escena.RecibirPorLaApiAsync(enviada.Id, new DateOnly(2026, 10, 25)))
        {
            recepcion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(recepcion));
        }

        ExigirLasFilas(
            await escena.FilasDelLibroAsync(postgres, enviada.Id),
            (escena.AlmacenA, -2m, new DateOnly(2026, 10, 26)),
            (escena.AlmacenB, 2m, new DateOnly(2026, 10, 25)));

        AnulacionDeTransferenciaDto anulacion = await AnularAsync(escena, enviada.Id, s_madridYaEsElVeintiseis);

        Transferencia inverso = await escena.LaTransferenciaAsync(postgres, anulacion.Inverso.Id);
        inverso.FechaDeEnvio.ShouldBe(new DateOnly(2026, 10, 26), "la vuelta sale del origen, en Madrid");
        inverso.FechaDeRecepcion.ShouldBe(new DateOnly(2026, 10, 25), "y sale del destino, en Canarias");

        ExigirLasFilas(
            await escena.FilasDelLibroAsync(postgres, inverso.Id),
            (escena.AlmacenB, -2m, new DateOnly(2026, 10, 25)),
            (escena.AlmacenA, 2m, new DateOnly(2026, 10, 26)));
    }

    /// <summary>
    /// Recibida el 26 en Madrid y pasado el destino a Canarias, el inverso de las 23:30 UTC del 25
    /// iría antes que la recepción que compensa: es el <c>422</c> del último movimiento, sin inverso.
    /// Media hora después, a las 00:00 UTC, ya es el 26 en las dos puntas, y se anula.
    /// </summary>
    /// <returns>La tarea del caso.</returns>
    [Fact]
    public async Task En_la_hora_que_sigue_al_cambio_de_zona_el_inverso_espera_con_el_422_del_ultimo_movimiento()
    {
        EscenaDeTransferencia escena = await MontarAsync(741, "TRZ-C", 2026);

        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m, new DateOnly(2026, 10, 1));

        TransferenciaDto enviada = await escena.EnviarAsync(postgres, 2m, new DateOnly(2026, 10, 26));

        using (HttpResponseMessage recepcion =
            await escena.RecibirPorLaApiAsync(enviada.Id, new DateOnly(2026, 10, 26)))
        {
            recepcion.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(recepcion));
        }

        await CambiarLaZonaAsync(escena, escena.AlmacenB, ZonaHorariaDto.Canarias);

        await using (ElModuloDeInventario modulo =
            new(postgres, escena.EmpresaId, new RelojParado(s_madridYaEsElVeintiseis)))
        {
            Resultado<AnulacionDeTransferenciaDto> antes = await modulo.AnularLaTransferenciaAsync(enviada.Id, Motivo);

            antes.EsCorrecto.ShouldBeFalse();
            antes.Error!.Codigo.ShouldBe("transferencia-fecha-anterior-al-ultimo-movimiento");
        }

        (await escena.InversosDeAsync(postgres, enviada.Id)).ShouldBe(0);
        (await escena.LaTransferenciaAsync(postgres, enviada.Id)).Estado.ShouldBe(EstadoDeTransferencia.Recibida);

        AnulacionDeTransferenciaDto anulacion = await AnularAsync(escena, enviada.Id, s_losDosEnElVeintiseis);

        Transferencia inverso = await escena.LaTransferenciaAsync(postgres, anulacion.Inverso.Id);
        inverso.FechaDeEnvio.ShouldBe(new DateOnly(2026, 10, 26));
        inverso.FechaDeRecepcion.ShouldBe(new DateOnly(2026, 10, 26));
    }

    /// <summary>
    /// A las 23:30 UTC del 25 de octubre, el 26 ya es hoy en Madrid y todavía mañana en Canarias. De
    /// Madrid a Canarias, el envío del 26 sale, y su recepción del 26 es futura hasta las 00:00 UTC.
    /// De Canarias a Madrid, lo futuro es el envío. Cada punta mira el futuro en la zona de su almacén.
    /// </summary>
    /// <remarks>
    /// Lo piden dos mutaciones de la F2 del 3.5 que salieron verdes en los dos carriles: la 1160, la
    /// recepción con el «hoy» del origen, y la 1172, el envío con el de la península.
    /// </remarks>
    /// <returns>La tarea del caso.</returns>
    [Fact]
    public async Task El_futuro_de_cada_punta_es_el_de_la_zona_de_su_almacen()
    {
        EscenaDeTransferencia escena = await MontarAsync(744, "TRZ-D", 2026);
        DateOnly veintiseis = new(2026, 10, 26);

        await CambiarLaZonaAsync(escena, escena.AlmacenB, ZonaHorariaDto.Canarias);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m, new DateOnly(2026, 10, 1));

        // DE MADRID A CANARIAS: el envío del 26 sale, que en Madrid ya es hoy, y su recepción del 26
        // espera a que también lo sea en Canarias.
        Guid haciaCanarias = await escena.AbrirAsync(postgres, 2m, veintiseis);

        await using (ElModuloDeInventario modulo = Parado(escena, s_madridYaEsElVeintiseis))
        {
            ExigirQueSale(await modulo.EnviarAsync(haciaCanarias));
            ExigirQueEsFutura(await modulo.RecibirAsync(haciaCanarias, veintiseis));
        }

        await using (ElModuloDeInventario modulo = Parado(escena, s_losDosEnElVeintiseis))
        {
            ExigirQueSale(await modulo.RecibirAsync(haciaCanarias, veintiseis));
        }

        // DE CANARIAS A MADRID: el envío del 26 es futuro en el origen.
        Guid haciaMadrid;

        await using (ElModuloDeInventario modulo = new(postgres, escena.EmpresaId))
        {
            Resultado<TransferenciaDto> alta = await escena.IntentarAbrirAsync(
                modulo,
                escena.AlmacenB,
                escena.AlmacenA,
                [escena.Linea(escena.UbicacionB, escena.UbicacionA, 2m)],
                veintiseis);

            ExigirQueSale(alta);
            haciaMadrid = alta.Valor.Id;
        }

        await using (ElModuloDeInventario modulo = Parado(escena, s_madridYaEsElVeintiseis))
        {
            ExigirQueEsFutura(await modulo.EnviarAsync(haciaMadrid));
        }

        await using (ElModuloDeInventario modulo = Parado(escena, s_losDosEnElVeintiseis))
        {
            ExigirQueSale(await modulo.EnviarAsync(haciaMadrid));
        }
    }

    private static void ExigirQueSale(Resultado<TransferenciaDto> resultado) =>
        resultado.EsCorrecto.ShouldBeTrue($"«{resultado.Error?.Codigo}»");

    private static void ExigirQueEsFutura(Resultado<TransferenciaDto> resultado)
    {
        resultado.EsCorrecto.ShouldBeFalse("la fecha es de mañana en la zona de su almacén");
        resultado.Error!.Codigo.ShouldBe("transferencia-con-fecha-futura");
    }

    private static async Task ExigirQueVaAntesDelEnvioAsync(
        EscenaDeTransferencia escena, Guid transferenciaId, DateOnly fecha)
    {
        using HttpResponseMessage recepcion = await escena.RecibirPorLaApiAsync(transferenciaId, fecha);

        recepcion.StatusCode.ShouldBe(HttpStatusCode.UnprocessableContent, await Escenario.Detalle(recepcion));
        (await EscenaDeTransferencia.TipoDelProblemaAsync(recepcion))
            .ShouldBe("/errors/transferencia-recepcion-antes-del-envio");
    }

    private static async Task<DateOnly?> LaPrimeraRecepcionAsync(EscenaDeTransferencia escena, Guid transferenciaId) =>
        (await escena.Cliente.GetFromJsonAsync<FichaDeTransferenciaDto>(
            $"{EscenaDeTransferencia.Transferencias}/{transferenciaId}"))!.PrimeraRecepcion;

    private static void ExigirLasFilas(
        IReadOnlyList<MovimientoStock> filas, params (Guid Almacen, decimal Cantidad, DateOnly Fecha)[] esperadas) =>
        filas.Select(fila => (fila.AlmacenId, fila.CantidadEnUnidadBase, fila.FechaDeOperacion))
            .ShouldBe(esperadas.Select(esperada => (esperada.Almacen, esperada.Cantidad, esperada.Fecha)));

    private static async Task CambiarLaZonaAsync(EscenaDeTransferencia escena, Guid almacenId, ZonaHorariaDto zona)
    {
        string ruta = $"{LosMaestrosPorLaApi.Almacenes}/{almacenId}";
        AlmacenDto almacen = (await escena.Cliente.GetFromJsonAsync<AlmacenDto>(ruta))!;

        using HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            ruta,
            new ModificarAlmacenDto
            {
                Nombre = almacen.Nombre,
                Direccion = almacen.Direccion,
                Tipo = almacen.Tipo,
                ZonaHoraria = zona,
            });

        cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        (await cambio.Content.ReadFromJsonAsync<AlmacenDto>())!.ZonaEfectiva.ShouldBe(zona);
    }

    private ElModuloDeInventario Parado(EscenaDeTransferencia escena, DateTimeOffset ahora) =>
        new(postgres, escena.EmpresaId, new RelojParado(ahora));

    private async Task<AnulacionDeTransferenciaDto> AnularAsync(
        EscenaDeTransferencia escena, Guid transferenciaId, DateTimeOffset ahora)
    {
        await using ElModuloDeInventario modulo = new(postgres, escena.EmpresaId, new RelojParado(ahora));

        Resultado<AnulacionDeTransferenciaDto> anulacion =
            await modulo.AnularLaTransferenciaAsync(transferenciaId, Motivo);

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»");

        return anulacion.Valor;
    }

    private async Task<EscenaDeTransferencia> MontarAsync(int semilla, string codigo, int anio)
    {
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, anio: anio);

        _clientes.Add(escena.Cliente);

        return escena;
    }
}
