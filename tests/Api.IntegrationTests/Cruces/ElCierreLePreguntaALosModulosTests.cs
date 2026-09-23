using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Cruces;

/// <summary>
/// El puerto <c>IDocumentosDeUnPeriodo</c> cobrado <b>por el efecto</b>: un borrador de Inventario
/// con fecha dentro impide cerrar el ejercicio, y confirmarlo lo desbloquea.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la flecha al revés, y por eso no se puede comprobar en un solo módulo.</b> Los otros
/// puertos del proyecto los implementa el módulo que los publica; éste lo publica Organización y
/// lo implementa Inventario, así que lo que se afirma aquí son dos módulos a la vez: que el cierre
/// pregunta, que Inventario contesta mirando <c>fecha_de_operacion</c>, y que Organización
/// convierte ese <c>true</c> en un 409 que <b>nombra al módulo</b>.
/// </para>
/// <para>
/// <b>La segunda mitad es la que distingue la regla de un «no se puede cerrar nunca».</b> El mismo
/// ajuste, confirmado, deja cerrar: lo que el cierre exige no es que el periodo esté vacío —sería
/// absurdo, un ejercicio con movimientos es lo normal— sino que no quede nada <i>a medias</i>. Sin
/// esta mitad, un <c>HayBorradoresEnAsync</c> que devolviera siempre <c>true</c> pasaría la
/// primera.
/// </para>
/// <para>
/// <b>El ajuste se abre con el módulo real y el cierre entra por la API.</b> Lo primero porque el
/// borde de Inventario no publica el alta con todo lo que este caso necesita montado a mano; lo
/// segundo porque el 409 y su <c>type</c> son un hecho del borde, y cablear el caso de uso se los
/// saltaría enteros.
/// </para>
/// <para>
/// <b>Semillas: la empresa va por la 330 y los maestros de instalación por la 364.</b> El resto
/// del reparto de este carril está en los ficheros de <c>Inventario</c> —del 301 al 328 las
/// empresas, del 349 al 363 los maestros—. Un número de empresa acaba en un NIF único y uno de
/// maestro en el código único de otra tabla, así que son dos cuentas y no una.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElCierreLePreguntaALosModulosTests(PostgresConTodosLosModulos postgres)
    : IDisposable
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

    [Fact]
    public async Task Un_borrador_de_inventario_dentro_del_ejercicio_impide_cerrarlo_y_el_error_lo_nombra()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(330);

        // `CrearSerieAsync` crea el ejercicio del año en curso —1 de enero a 31 de diciembre— y le
        // cuelga la serie. La fecha de operación del ajuste es «hoy», así que cae dentro por
        // construcción: es el caso normal, no uno amañado.
        (AbrirAjusteDto peticion, SerieDto serie) = await UnAjusteCompletoAsync(cliente, "CIE-A", 364);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> borrador = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);

        borrador.EsCorrecto.ShouldBeTrue(
            $"sin borrador no hay nada que impida cerrar, y este caso no diría nada. " +
            $"Contestó «{borrador.Error?.Codigo}»");

        string recurso = $"{LosMaestrosPorLaApi.Ejercicios}/{serie.EjercicioId}";

        using HttpResponseMessage negado = await cliente.AccionarAsync(
            recurso, $"{recurso}/cierre", HttpMethod.Post);

        negado.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "queda un ajuste en borrador con fecha dentro del intervalo: cerrar congelaría un " +
            "periodo en el que todavía hay un documento que puede cambiar de importe");

        JsonElement problema = await LeerProblema(negado);

        problema.GetProperty("type").GetString().ShouldBe("/errors/ejercicio-con-borradores");

        // Y NOMBRA AL MÓDULO, que es para lo que el puerto publica `Modulo`. Sin este dato, quien
        // recibe el 409 sabe que no puede cerrar y no sabe dónde mirar — y el día que haya seis
        // módulos inscritos, eso son seis listados que recorrer a mano.
        problema.GetProperty("detail").GetString()!.ShouldContain(
            "Inventario",
            Case.Sensitive,
            "el error tiene que decir QUÉ módulo se ha negado, no solo que alguien lo hizo");

        // LA OTRA MITAD: el mismo documento, confirmado, deja cerrar. Lo que el cierre exige no es
        // un periodo vacío sino uno sin nada a medias.
        Resultado<AjusteDto> confirmado = await modulo.ConfirmarAsync(borrador.Valor.Id);

        confirmado.EsCorrecto.ShouldBeTrue($"«{confirmado.Error?.Codigo}»");

        (await cliente.AccionarAsync(recurso, $"{recurso}/cierre", HttpMethod.Post)).StatusCode
            .ShouldBe(
                HttpStatusCode.NoContent,
                "ya no queda ningún borrador dentro, así que el cierre tiene que pasar. Si " +
                "siguiera negándose, lo que este caso estaría comprobando es «un ejercicio con " +
                "movimientos no se cierra nunca», que es otra cosa y además falsa");
    }

    /// <summary>Todos los maestros de un ajuste que sí se puede abrir, y su petición.</summary>
    /// <param name="cliente">Cliente autenticado en la empresa del caso.</param>
    /// <param name="codigo">Prefijo para los códigos de este caso.</param>
    /// <param name="semillaDeInstalacion">Número para la unidad y el tramo de impuesto.</param>
    /// <returns>La petición lista para el alta, y la serie que la numerará.</returns>
    private static async Task<(AbrirAjusteDto Peticion, SerieDto Serie)> UnAjusteCompletoAsync(
        HttpClient cliente, string codigo, int semillaDeInstalacion)
    {
        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, codigo);

        UbicacionDto ubicacion =
            await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, codigo + "-01");

        (Guid articuloId, Guid unidadId) =
            await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, semillaDeInstalacion);

        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, codigo);

        AbrirAjusteDto peticion = new(
            serie.Id,
            almacen.Id,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Regularización de un recuento",
            [new LineaDeAjusteDto(ubicacion.Id, articuloId, 4m, unidadId, 2m, 1.50m, "EUR")]);

        return (peticion, serie);
    }

    private static async Task<JsonElement> LeerProblema(HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }
}
