using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Existencias;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// La R3 como propiedad: después de cualquier secuencia de entradas, salidas, ajustes, anulaciones,
/// recálculos, cierres y fechas prohibidas, el saldo es la suma del libro (ítem 2.7).
/// </summary>
/// <remarks>
/// <para>
/// <b>El generador es propio y con semilla fija</b>, sin biblioteca: un <see cref="Random"/> por
/// caso, con la semilla en el nombre del caso. Lo que importa de un caso de propiedades es que el
/// contraejemplo se pueda repetir, y con la semilla se repite la misma secuencia paso a paso. La
/// secuencia entera va en el mensaje de cada aserción: el rojo dice qué pasos lo produjeron.
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
/// <b>Cada semilla tiene que pasar por todas las clases de paso</b>, y el caso lo afirma: una
/// semilla que no anulara nunca pasaría por prueba de las anulaciones sin haberlas probado.
/// </para>
/// <para>
/// <b>Semillas: del 460 al 465 las empresas</b>, que son también las semillas del generador; del
/// 470 al 475 y del 480 al 485 los maestros de instalación, dos artículos por caso.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElSaldoEsLaSumaDelLibroPorPropiedadTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
    private const int Pasos = 40;
    private const string Anulacion = "anulación";
    private const string Futuro = "futuro";
    private const string Recalculo = "recálculo";
    private const string Cierre = "cierre";
    private const string Reapertura = "reapertura";
    private const string RechazadoPorElCierre = "rechazado por el cierre";

    private static readonly string[] s_clasesDePaso =
    [
        "entrada", "salida", "ajuste", Anulacion, Futuro, Recalculo, Cierre, Reapertura,
        RechazadoPorElCierre,
    ];

    private static readonly decimal[] s_factores = [1m, 2m, 0.5m];

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
        Secuencia secuencia = new(semilla);

        UnaEmpresa empresa = await UnaEmpresaAsync(semilla);

        await using ElModuloDeInventario modulo = new(postgres, empresa.EmpresaId);

        for (int paso = 1; paso <= Pasos; paso++)
        {
            int dado = paso == 1 ? 0 : azar.Next(100);

            if (dado < 40)
            {
                await UnMovimientoAsync(azar, modulo, empresa, secuencia);
            }
            else if (dado < 55)
            {
                await UnaAnulacionAsync(azar, modulo, secuencia);
            }
            else if (dado < 65)
            {
                await UnaFechaFuturaAsync(azar, modulo, empresa, secuencia);
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

        int claves = secuencia.Libro.Select(apunte => apunte.Clave).Distinct().Count();

        claves.ShouldBeGreaterThan(0, secuencia.Relato());
        debidas.Count.ShouldBeGreaterThan(0, secuencia.Relato());

        cuadre.ExistenciasComparadas.ShouldBe(claves, secuencia.Relato());
        cuadre.InstantaneasComparadas.ShouldBe(debidas.Count, secuencia.Relato());
        cuadre.Descuadres.ShouldBeEmpty(secuencia.Relato());

        secuencia.Clases.ShouldBe(s_clasesDePaso, ignoreOrder: true, customMessage: secuencia.Relato());
    }

    /// <summary>Una entrada, una salida o un ajuste de varias líneas, con una fecha de hoy o de atrás.</summary>
    private static async Task UnMovimientoAsync(
        Random azar, ElModuloDeInventario modulo, UnaEmpresa empresa, Secuencia secuencia)
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

        IReadOnlyList<LineaAlAzar> lineas = LineasAlAzar(azar);

        string clase = lineas.Count > 1 ? "ajuste" : lineas[0].Cantidad > 0 ? "entrada" : "salida";

        Guid ajusteId = await AbrirAsync(
            modulo, empresa, fecha.Year == pasado ? empresa.SerieDelAnioPasado : empresa.Serie, fecha, lineas);

        Resultado<AjusteDto> confirmacion = await modulo.ConfirmarAsync(ajusteId);

        if (fecha.Year == pasado && secuencia.PasadoCerrado)
        {
            secuencia.Anotar(RechazadoPorElCierre, fecha, lineas);
            secuencia.BorradoresEnElPasado++;

            confirmacion.EsCorrecto.ShouldBeFalse(secuencia.Relato());
            confirmacion.Error!.Codigo.ShouldBe("ajuste-en-ejercicio-cerrado", secuencia.Relato());

            return;
        }

        secuencia.Anotar(clase, fecha, lineas);

        confirmacion.EsCorrecto.ShouldBeTrue($"«{confirmacion.Error?.Codigo}»\n{secuencia.Relato()}");

        ApunteDelLibro[] apuntes =
        [
            .. lineas.Select(linea => new ApunteDelLibro(
                empresa.Claves[linea.Clave], fecha, linea.Cantidad * linea.Factor)),
        ];

        secuencia.Libro.AddRange(apuntes);
        secuencia.Anulables.Add((ajusteId, apuntes));
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
        (Guid ajusteId, ApunteDelLibro[] apuntes) = secuencia.Anulables[cual];
        secuencia.Anulables.RemoveAt(cual);

        secuencia.Anotar(Anulacion, apuntes);

        Resultado<AnulacionDto> anulacion = await modulo.AnularAsync(ajusteId, "Anulación del generador");

        anulacion.EsCorrecto.ShouldBeTrue($"«{anulacion.Error?.Codigo}»\n{secuencia.Relato()}");

        secuencia.Libro.AddRange(apuntes.Select(apunte => apunte with
        {
            Fecha = Hoy,
            Cantidad = -apunte.Cantidad,
        }));
    }

    /// <summary>Un documento con fecha futura, que tiene que rechazarse sin mover nada.</summary>
    private static async Task UnaFechaFuturaAsync(
        Random azar, ElModuloDeInventario modulo, UnaEmpresa empresa, Secuencia secuencia)
    {
        DateOnly fecha = Hoy.AddDays(azar.Next(1, 40));
        IReadOnlyList<LineaAlAzar> lineas = LineasAlAzar(azar);

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
        Ordenado(await LasExistencias.LibroAsync(postgres, empresa.EmpresaId))
            .ShouldBe(Ordenado(secuencia.Libro), "el libro no es el del modelo\n" + secuencia.Relato());

        IReadOnlyDictionary<ClaveDeExistencia, decimal> saldos =
            LasExistencias.SaldosDelLibro(secuencia.Libro);

        IReadOnlyList<Existencia> vivas = await LasExistencias.VivasAsync(postgres, empresa.EmpresaId);

        vivas.Count.ShouldBe(saldos.Count, "una fila viva por clave\n" + secuencia.Relato());

        foreach (Existencia viva in vivas)
        {
            viva.Fisico.ShouldBe(
                saldos[LasExistencias.ClaveDe(viva)],
                "el saldo no es la suma del libro\n" + secuencia.Relato());

            viva.Disponible.ShouldBe(viva.Fisico, secuencia.Relato());
        }

        (await LasExistencias.InstantaneasAsync(postgres, empresa.EmpresaId)).ShouldBe(
            LasExistencias.DebidasEnCSharp(secuencia.Libro, secuencia.Corte),
            "las instantáneas no son las del libro\n" + secuencia.Relato());
    }

    private static List<LineaAlAzar> LineasAlAzar(Random azar)
    {
        int cuantas = azar.Next(1, 4);
        List<LineaAlAzar> lineas = [];

        for (int numero = 0; numero < cuantas; numero++)
        {
            int signo = azar.Next(2) == 0 ? 1 : -1;

            lineas.Add(new LineaAlAzar(
                azar.Next(4), signo * azar.Next(1, 10), s_factores[azar.Next(s_factores.Length)]));
        }

        return lineas;
    }

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
                    1.50m,
                    "EUR");
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
            .ThenBy(apunte => apunte.Fecha)
            .ThenBy(apunte => apunte.Cantidad)];

    /// <summary>La fecha de hoy, en el mismo calendario que usa el caso de uso.</summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Una empresa con un almacén de dos ubicaciones, dos artículos —cuatro claves— y los
    /// ejercicios de este año y del pasado, cada uno con su serie.
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

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        Guid[] ubicaciones =
        [
            (await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01")).Id,
            (await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-02")).Id,
        ];

        (Guid primero, Guid unidadDelPrimero) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semilla + 10);

        (Guid segundo, Guid unidadDelSegundo) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semilla + 20);

        return new UnaEmpresa(
            cliente,
            empresa.Id,
            almacen.Id,
            [
                new ClaveDeExistencia(primero, almacen.Id, ubicaciones[0]),
                new ClaveDeExistencia(primero, almacen.Id, ubicaciones[1]),
                new ClaveDeExistencia(segundo, almacen.Id, ubicaciones[0]),
                new ClaveDeExistencia(segundo, almacen.Id, ubicaciones[1]),
            ],
            new Dictionary<Guid, Guid> { [primero] = unidadDelPrimero, [segundo] = unidadDelSegundo },
            serie,
            delAnioPasado,
            anioPasado);
    }

    /// <summary>Una línea tal como la saca el generador.</summary>
    /// <param name="Clave">Cuál de las cuatro claves de la empresa.</param>
    /// <param name="Cantidad">La cantidad tecleada, con signo.</param>
    /// <param name="Factor">El factor a unidad base.</param>
    private sealed record LineaAlAzar(int Clave, decimal Cantidad, decimal Factor);

    /// <summary>Los maestros de la empresa del caso.</summary>
    private sealed record UnaEmpresa(
        HttpClient Cliente,
        Guid EmpresaId,
        Guid AlmacenId,
        IReadOnlyList<ClaveDeExistencia> Claves,
        IReadOnlyDictionary<Guid, Guid> UnidadDe,
        SerieDto Serie,
        SerieDto SerieDelAnioPasado,
        EjercicioDto AnioPasado);

    /// <summary>El modelo: lo que el libro debería tener, y el relato de cómo se llegó ahí.</summary>
    /// <param name="semilla">La semilla del generador, que es la primera línea del relato.</param>
    private sealed class Secuencia(int semilla)
    {
        private readonly List<string> _pasos = [];

        internal List<ApunteDelLibro> Libro { get; } = [];

        internal List<(Guid AjusteId, ApunteDelLibro[] Apuntes)> Anulables { get; } = [];

        internal HashSet<string> Clases { get; } = [];

        internal DateOnly? Corte { get; set; }

        internal bool PasadoCerrado { get; set; }

        internal int BorradoresEnElPasado { get; set; }

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

        internal string Relato() =>
            string.Create(CultureInfo.InvariantCulture, $"semilla {semilla}:\n") +
            string.Join("\n", _pasos);

        private static string Describir(LineaAlAzar linea) =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"clave {linea.Clave}: {linea.Cantidad:+0;-0} x {linea.Factor}");
    }
}
