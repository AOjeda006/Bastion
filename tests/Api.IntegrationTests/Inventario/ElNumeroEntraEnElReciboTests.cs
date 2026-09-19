using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Idempotencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Idempotencia;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El número está <b>dentro</b> del recibo de idempotencia, y sin la clave la confirmación ni
/// siquiera empieza.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué el cuerpo entero y no el número suelto.</b> Lo que el filtro guarda como recibo son
/// los bytes de la respuesta ya serializada, así que el número solo entra en él si sale en el DTO
/// que la acción devuelve. Un <c>Confirmar</c> que compusiera el correlativo en el borde —o que lo
/// publicara en una cabecera en vez de en el cuerpo— pasaría cualquier afirmación hecha sobre la
/// primera respuesta, y el reintento del cliente que perdió la cobertura recibiría un documento
/// sin número. Comparando los dos cuerpos byte a byte, eso no tiene dónde esconderse.
/// </para>
/// <para>
/// <b>Y el <c>428</c> es la otra mitad de la misma afirmación.</b> Lo que hace atómicos el número y
/// el documento es la transacción, y en este sistema la abre el filtro de idempotencia y nadie más
/// (ADR-0014); sin cabecera, el filtro se aparta en su primera línea. Que la respuesta sin clave
/// sea un <c>428</c> —y no un <c>200</c> silencioso— es lo que impide que exista un camino por el
/// que se confirme fuera de transacción, que es el camino que dejaría un número gastado sin
/// documento o un documento confirmado sin número. Los dos rompen la R5.
/// </para>
/// <para>
/// <b>Semillas: las empresas van por el 313 y los maestros de instalación por el 352.</b> El resto
/// del reparto de este carril está en <c>ElCerrojoDeLaNumeracionTests</c> —del 301 al 308 y del
/// 320 en adelante— y en <c>LaSerieDelAjusteTests</c>, que gasta del 309 al 312 y los maestros 350
/// y 351. Un número de empresa acaba en un NIF único y uno de maestro en el código único de otra
/// tabla, así que son dos cuentas y no una.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElNumeroEntraEnElReciboTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Cabecera = "Idempotency-Key";

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
    public async Task El_reintento_con_la_misma_clave_devuelve_el_numero_y_no_gasta_otro()
    {
        (HttpClient cliente, Guid empresaId, Guid ajusteId, SerieDto serie) =
            await UnBorradorAsync(313, "REC-A", 352);

        string clave = Guid.NewGuid().ToString();

        using HttpResponseMessage primera = await ConfirmarAsync(cliente, ajusteId, clave);
        using HttpResponseMessage segunda = await ConfirmarAsync(cliente, ajusteId, clave);

        primera.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(primera));
        segunda.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(segunda));

        // LOS MISMOS BYTES, que es la afirmación del ítem: si el número se compusiera fuera de lo
        // que se guarda, el reintento contestaría sin él y las dos cadenas no serían iguales.
        string bytesDeLaPrimera = await primera.Content.ReadAsStringAsync();
        string bytesDeLaSegunda = await segunda.Content.ReadAsStringAsync();

        bytesDeLaSegunda.ShouldBe(bytesDeLaPrimera);

        // Y que lo que se ha comparado lleva el número dentro, dicho aparte: dos cuerpos idénticos
        // y los dos sin número habrían pasado igual la comparación de arriba.
        (await primera.Content.ReadFromJsonAsync<AjusteDto>())!.Numero.ShouldBe(1);
        (await segunda.Content.ReadFromJsonAsync<AjusteDto>())!.Numero.ShouldBe(1);

        // La segunda no volvió a hacer el trabajo, y lo dice ella: es la diferencia entre «te la
        // he repetido» y «lo he vuelto a hacer», que sin esto la valdría igual una acción que
        // reconfirmara y acabara devolviendo lo mismo.
        primera.Headers.Contains(RespuestaRepetida.CabeceraDeRepeticion).ShouldBeFalse();
        segunda.Headers.GetValues(RespuestaRepetida.CabeceraDeRepeticion).ShouldContain("true");

        // Y por el efecto, que es lo único que un cliente notaría: la serie gastó UN correlativo.
        // Si el reintento hubiera vuelto a entrar en el mecanismo, aquí habría un dos.
        (await ContadorAsync(cliente, serie.Id)).ShouldBe(1);

        await ElDocumentoAsync(empresaId, ajusteId, EstadoDeAjuste.Confirmado, numero: 1);
    }

    [Fact]
    public async Task Sin_la_cabecera_la_confirmacion_es_428_y_no_toca_nada()
    {
        (HttpClient cliente, Guid empresaId, Guid ajusteId, SerieDto serie) =
            await UnBorradorAsync(314, "REC-B", 353);

        using HttpResponseMessage sinClave = await ConfirmarAsync(cliente, ajusteId, clave: null);

        sinClave.StatusCode.ShouldBe(
            HttpStatusCode.PreconditionRequired,
            "la petición está impecable y lo que falta es una precondición, igual que en el 428 " +
            $"del If-Match. {await Escenario.Detalle(sinClave)}");

        (await sinClave.Content.ReadAsStringAsync())
            .ShouldContain("/errors/" + ErroresDeIdempotencia.CodigoDeObligatoria);

        // NADA se ha movido: ni el documento ni el contador. Es lo que distingue apartarse ANTES
        // de trabajar de apartarse a medias, que es lo que haría una acción que empezara a numerar
        // y luego se encontrara sin transacción que confirmar.
        await ElDocumentoAsync(empresaId, ajusteId, EstadoDeAjuste.Borrador, numero: null);

        (await ContadorAsync(cliente, serie.Id)).ShouldBe(0);

        // Y el 428 no ha quemado nada: con la clave puesta, el MISMO ajuste se confirma y se lleva
        // el PRIMER correlativo de la serie. Sin esta segunda mitad, un rechazo que hubiera
        // consumido el número por el camino saldría verde en todo lo de arriba.
        using HttpResponseMessage conClave =
            await ConfirmarAsync(cliente, ajusteId, Guid.NewGuid().ToString());

        conClave.StatusCode.ShouldBe(HttpStatusCode.OK, await Escenario.Detalle(conClave));

        (await conClave.Content.ReadFromJsonAsync<AjusteDto>())!.Numero.ShouldBe(1);
    }

    /// <summary>La confirmación por HTTP, con la clave o deliberadamente sin ella.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="ajusteId">El documento que confirmar.</param>
    /// <param name="clave">La <c>Idempotency-Key</c>, o <c>null</c> para no mandar la cabecera.</param>
    /// <returns>La respuesta cruda, que es lo que estos casos miran.</returns>
    private static Task<HttpResponseMessage> ConfirmarAsync(
        HttpClient cliente, Guid ajusteId, string? clave)
    {
        HttpRequestMessage peticion =
            new(HttpMethod.Post, $"/api/v1/inventario/ajustes/{ajusteId}/confirmacion");

        if (clave is not null)
        {
            peticion.Headers.TryAddWithoutValidation(Cabecera, clave);
        }

        return cliente.SendAsync(peticion);
    }

    /// <summary>Por dónde va la serie, leído por la API y no por el contexto.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="serieId">La serie.</param>
    /// <returns>Cuántos correlativos ha entregado.</returns>
    private static async Task<long> ContadorAsync(HttpClient cliente, Guid serieId) =>
        (await cliente.GetFromJsonAsync<SerieDto>(
            $"{LosMaestrosPorLaApi.Series}/{serieId}"))!.Contador;

    /// <summary>Cómo quedó el ajuste en la base, que no depende de ninguna respuesta.</summary>
    /// <param name="empresaId">Empresa dueña del documento (R8).</param>
    /// <param name="ajusteId">El documento.</param>
    /// <param name="estado">En qué estado tiene que estar.</param>
    /// <param name="numero">Qué número tiene que llevar, o <c>null</c> si ninguno.</param>
    private async Task ElDocumentoAsync(
        Guid empresaId, Guid ajusteId, EstadoDeAjuste estado, long? numero)
    {
        await using InventarioDbContext inventario = postgres.AbrirInventario(empresaId);

        Ajuste comoQuedo = await inventario.Ajustes.SingleAsync(fila => fila.Id == ajusteId);

        comoQuedo.Estado.ShouldBe(estado);
        comoQuedo.Numero.ShouldBe(numero);
    }

    /// <summary>
    /// Una empresa nueva con sus maestros y un ajuste en borrador dentro, listo para confirmar.
    /// </summary>
    /// <remarks>
    /// El borrador se abre por el caso de uso cableado a mano y no por la API porque el borde del
    /// módulo publica <b>una sola acción</b>, que es la confirmación: el alta va con sus pantallas,
    /// en otro ítem. Lo que estos casos prueban es lo que pasa en esa única acción, así que el
    /// estado de partida puede construirse por donde se pueda.
    /// </remarks>
    /// <param name="semilla">Número de empresa, que acaba en su NIF.</param>
    /// <param name="codigo">Prefijo de los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <returns>El cliente, la empresa, el borrador y la serie que lo numerará.</returns>
    private async Task<(HttpClient Cliente, Guid EmpresaId, Guid AjusteId, SerieDto Serie)>
        UnBorradorAsync(int semilla, string codigo, int semillaDeInstalacion)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        UbicacionDto ubicacion =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01");

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semillaDeInstalacion);

        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, codigo);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> alta = await modulo.Alta.EjecutarAsync(
            new AbrirAjusteDto(
                serie.Id,
                almacen.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Regularización de un recuento",
                [new LineaDeAjusteDto(ubicacion.Id, articuloId, 4m, unidadId, 2m, 1.50m, "EUR")]),
            CancellationToken.None);

        alta.EsCorrecto.ShouldBeTrue($"«{alta.Error?.Codigo}»");

        alta.Valor.Numero.ShouldBeNull(
            "un borrador no ha gastado ningún correlativo, y es lo que hace que el 1 de estos " +
            "casos signifique algo");

        return (cliente, empresa.Id, alta.Valor.Id, serie);
    }
}
