using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Fechas;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Fechas;
using Bastion.BuildingBlocks.Domain.Fechas;
using Bastion.Inventario.Infrastructure.Corte;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Existencias;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Las zonas que el trabajo mensual lee de una empresa, sin petición, y el cuadre que suma el libro
/// hasta el día más adelantado de ellas (ítem 3.5, ADR-0079).
/// </summary>
/// <remarks>
/// <para>
/// <b>Una empresa canaria con un almacén peninsular.</b> El 15 de enero a las 23:30 UTC, en Madrid
/// ya es el 16 y en Canarias todavía el 15, así que una entrada del 16 en el almacén de Madrid ya es
/// de hoy. El reloj del cuadre se para ahí, y el ejercicio es el del año pasado: la entrada la
/// confirma el reloj de verdad, para el que el 16 de enero del año pasado ya pasó.
/// </para>
/// <para>
/// <b>Las zonas, por el contenedor del anfitrión y sin petición</b>, como las lee el trabajo: sin
/// <i>claim</i>, el ámbito sin inquilino del avance del corte es lo único que deja leerlas.
/// </para>
/// <para>
/// <b>Semillas: el 742 y el 743</b>, del bloque de la transferencia que reparte la cabecera de
/// <c>LaTransferenciaTests</c>.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElCuadreEnLasZonasDeLaEmpresaTests(PostgresConTodosLosModulos postgres) : IDisposable
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
    /// Sin petición, las zonas de la empresa son la suya y la de su almacén peninsular, y con las dos
    /// la entrada del 16 cuadra a las 23:30 UTC del 15. Con la de la empresa sola, es un descuadre.
    /// </summary>
    /// <returns>La tarea del caso.</returns>
    [Fact]
    public async Task Sin_peticion_se_leen_todas_las_zonas_y_el_cuadre_llega_al_dia_de_la_mas_adelantada()
    {
        (EscenaDeTransferencia escena, int anio) = await MontarConUnAlmacenEnMadridAsync(742, "CZZ-A");

        IReadOnlyList<ZonaHoraria> zonas = await LasZonasDelTrabajoAsync(escena.EmpresaId);

        zonas.ShouldBe([ZonaHoraria.Canarias, ZonaHoraria.Madrid], "la de la empresa primero, y sin repetir");

        CuadreDeLasExistencias limpio = await CuadrarAsync(escena.EmpresaId, zonas, AlFinalDelQuince(anio));

        limpio.ExistenciasComparadas.ShouldBe(1, "la entrada del almacén de Madrid tiene que haberse comparado");
        limpio.Descuadres.ShouldBeEmpty("en Madrid ya es el 16, y la entrada es de hoy");

        // CON LA ZONA DE LA EMPRESA SOLA, la entrada sería de mañana.
        ExigirElDescuadreDeLaEntrada(
            await CuadrarAsync(escena.EmpresaId, [ZonaHoraria.Canarias], AlFinalDelQuince(anio)), escena);
    }

    /// <summary>
    /// Vuelto el almacén a la zona de la empresa, la entrada del 16 que puso Madrid es un descuadre
    /// hasta que en Canarias también es el 16: la hora que sigue a un cambio de zona, en el cuadre.
    /// </summary>
    /// <remarks>
    /// Las fechas ya escritas no cambian, y el cuadre mira las zonas como están cuando cuadra. La
    /// misma entrada, media hora después, cuadra.
    /// </remarks>
    /// <returns>La tarea del caso.</returns>
    [Fact]
    public async Task Con_el_almacen_de_vuelta_en_Canarias_la_entrada_de_Madrid_descuadra_hasta_que_alli_es_su_dia()
    {
        (EscenaDeTransferencia escena, int anio) = await MontarConUnAlmacenEnMadridAsync(743, "CZZ-B");

        await CambiarLaZonaDelAlmacenAsync(escena, null, ZonaHorariaDto.Canarias);

        IReadOnlyList<ZonaHoraria> zonas = await LasZonasDelTrabajoAsync(escena.EmpresaId);

        zonas.ShouldBe([ZonaHoraria.Canarias], "los dos almacenes heredan la de la empresa");

        ExigirElDescuadreDeLaEntrada(await CuadrarAsync(escena.EmpresaId, zonas, AlFinalDelQuince(anio)), escena);

        CuadreDeLasExistencias despues = await CuadrarAsync(
            escena.EmpresaId, zonas, new DateTimeOffset(anio, 1, 16, 0, 0, 0, TimeSpan.Zero));

        despues.ExistenciasComparadas.ShouldBe(1);
        despues.Descuadres.ShouldBeEmpty("a las 00:00 UTC ya es el 16 en Canarias");
    }

    // Madrid, el 16 a las 00:30; Canarias, el 15 a las 23:30.
    private static DateTimeOffset AlFinalDelQuince(int anio) => new(anio, 1, 15, 23, 30, 0, TimeSpan.Zero);

    private static void ExigirElDescuadreDeLaEntrada(CuadreDeLasExistencias cuadre, EscenaDeTransferencia escena)
    {
        cuadre.Descuadres.ShouldNotBeEmpty("la entrada del 16 es de mañana, y el libro hasta hoy no la cuenta");
        cuadre.Descuadres.ShouldContain(descuadre => descuadre.Que == "existencia");
        cuadre.Descuadres.Select(descuadre => descuadre.AlmacenId).Distinct().ShouldBe([escena.AlmacenA]);
    }

    private static async Task CambiarLaZonaDelAlmacenAsync(
        EscenaDeTransferencia escena, ZonaHorariaDto? zona, ZonaHorariaDto efectiva)
    {
        string ruta = $"{LosMaestrosPorLaApi.Almacenes}/{escena.AlmacenA}";
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
        (await cambio.Content.ReadFromJsonAsync<AlmacenDto>())!.ZonaEfectiva.ShouldBe(efectiva);
    }

    /// <summary>
    /// La escena de siempre, con la empresa en Canarias, el almacén A en Madrid por su cuenta, y una
    /// entrada en A del 16 de enero del año pasado.
    /// </summary>
    private async Task<(EscenaDeTransferencia Escena, int Anio)> MontarConUnAlmacenEnMadridAsync(
        int semilla, string codigo)
    {
        int anio = EscenaDeTransferencia.Hoy.Year - 1;
        EscenaDeTransferencia escena =
            await EscenaDeTransferencia.MontarAsync(_api, semilla, codigo, semilla, anio: anio);

        _clientes.Add(escena.Cliente);

        string ruta = $"{RutaDeEmpresas}/{escena.EmpresaId}";
        EmpresaDto empresa = (await escena.Cliente.GetFromJsonAsync<EmpresaDto>(ruta))!;

        using (HttpResponseMessage cambio = await escena.Cliente.ModificarAsync(
            ruta,
            new ModificarEmpresaDto
            {
                RazonSocial = empresa.RazonSocial,
                DomicilioFiscal = empresa.DomicilioFiscal,
                DivisaBase = empresa.DivisaBase,
                RegimenDeIva = empresa.RegimenDeIva,
                ZonaHoraria = ZonaHorariaDto.Canarias,
            }))
        {
            cambio.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(cambio));
        }

        await CambiarLaZonaDelAlmacenAsync(escena, ZonaHorariaDto.Madrid, ZonaHorariaDto.Madrid);
        await escena.EntrarAsync(postgres, escena.AlmacenA, escena.UbicacionA, 5m, 2m, new DateOnly(anio, 1, 16));

        return (escena, anio);
    }

    private async Task<IReadOnlyList<ZonaHoraria>> LasZonasDelTrabajoAsync(Guid empresaId)
    {
        using IServiceScope ambito = _api.Services.CreateScope();

        return await ambito.ServiceProvider
            .GetRequiredService<IAvanceDelCorte>()
            .ZonasAsync(empresaId, CancellationToken.None);
    }

    private async Task<CuadreDeLasExistencias> CuadrarAsync(
        Guid empresaId, IReadOnlyCollection<ZonaHoraria> zonas, DateTimeOffset ahora)
    {
        await using InventarioDbContext contexto = postgres.AbrirInventario(empresaId);

        return await new ElCuadreDeLasExistencias(contexto, new RelojParado(ahora))
            .CuadrarAsync(empresaId, zonas, CancellationToken.None);
    }
}
