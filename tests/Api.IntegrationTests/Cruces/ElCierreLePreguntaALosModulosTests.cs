using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
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
/// <b>Semillas: las empresas van de la 330 a la 332 y los maestros de instalación de la 364 a
/// la 366.</b> El resto
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

    /// <summary>El año del ejercicio que monta <c>CrearSerieAsync</c>: el en curso.</summary>
    private static int ElAnio => DateTime.UtcNow.Year;

    /// <summary>
    /// El día del ajuste: el 15 de junio, y <b>no «hoy»</b>.
    /// </summary>
    /// <remarks>
    /// Los casos de abajo encogen el ejercicio por delante y por detrás de esta fecha, así que
    /// tienen que poder sumarle y restarle días sin salirse del año. Con «hoy», el 31 de diciembre
    /// el intervalo recortado acabaría antes de empezar y el caso saldría rojo una vez al año, en
    /// la máquina de quien tocara ese día. Un día fijo de dentro del ejercicio no tiene ese
    /// problema y no pierde nada: lo que se comprueba es la aritmética de los extremos.
    /// </remarks>
    private static DateOnly ElDiaDelDocumento => new(ElAnio, 6, 15);

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

    /// <summary>
    /// Encoger el ejercicio por encima de un documento es <b>409</b>; encogerlo por el lado en el
    /// que no hay nada, <b>no</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el agujero silencioso del que venía este ítem.</b> Hasta ahora, mover las fechas de un
    /// ejercicio abierto con movimientos dentro estaba permitido, y los que se quedaban fuera
    /// pasaban a no pertenecer a ningún ejercicio sin que nadie los tocara: ni cambiaban de fecha,
    /// ni de importe, ni dejaban rastro. Se enteraba quien cuadrara el año.
    /// </para>
    /// <para>
    /// <b>La segunda mitad es la que distingue la regla de un «un ejercicio con documentos no se
    /// mueve».</b> El mismo ejercicio, encogido por el otro lado —donde no hay nada—, se mueve sin
    /// problema. Sin ella, preguntar por el intervalo ENTERO en vez de por los trozos que quedan
    /// fuera pasaría igual, y eso dejaría inmóvil cualquier ejercicio con un solo movimiento
    /// dentro, que es todos.
    /// </para>
    /// <para>
    /// Y el ajuste va <b>confirmado</b>, no en borrador: aquí no se pregunta por lo que está a
    /// medias sino por lo que ya está contado, que es precisamente lo que peor se lleva con un
    /// cambio de periodo.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Encoger_el_ejercicio_por_encima_de_un_documento_es_409_y_por_el_otro_lado_no()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(331);

        (AbrirAjusteDto peticion, SerieDto serie) = await UnAjusteCompletoAsync(cliente, "MOV-A", 365);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> borrador = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        borrador.EsCorrecto.ShouldBeTrue($"«{borrador.Error?.Codigo}»");

        Resultado<AjusteDto> confirmado = await modulo.ConfirmarAsync(borrador.Valor.Id);
        confirmado.EsCorrecto.ShouldBeTrue($"«{confirmado.Error?.Codigo}»");

        string recurso = $"{LosMaestrosPorLaApi.Ejercicios}/{serie.EjercicioId}";

        // El documento es del 15 de junio: empezar el 1 de julio lo deja fuera.
        using HttpResponseMessage encogido = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            recurso,
            await cliente.EtiquetaDeAsync(recurso),
            JsonContent.Create(new ModificarEjercicioDto
            {
                FechaDeInicio = new DateOnly(ElAnio, 7, 1),
                FechaDeFin = new DateOnly(ElAnio, 12, 31),
            }));

        encogido.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "el intervalo nuevo deja fuera al ajuste del 15 de junio, que pasaría a no pertenecer " +
            "a ningún ejercicio sin que nadie lo tocara");

        JsonElement problema = await LeerProblema(encogido);

        problema.GetProperty("type").GetString().ShouldBe("/errors/ejercicio-dejaria-documentos-fuera");

        problema.GetProperty("detail").GetString()!.ShouldContain(
            "Inventario",
            Case.Sensitive,
            "el error tiene que decir QUÉ módulo se queda con documentos fuera");

        // Y AHORA POR EL OTRO LADO: se queda fuera de julio a diciembre, donde no hay nada.
        using HttpResponseMessage permitido = await cliente.EnviarConVersionAsync(
            HttpMethod.Put,
            recurso,
            await cliente.EtiquetaDeAsync(recurso),
            JsonContent.Create(new ModificarEjercicioDto
            {
                FechaDeInicio = new DateOnly(ElAnio, 1, 1),
                FechaDeFin = new DateOnly(ElAnio, 6, 30),
            }));

        permitido.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "por este lado no se queda ningún documento fuera. Si esto saliera 409, la " +
            "comprobación estaría preguntando por el intervalo entero en vez de por los trozos " +
            "que quedan fuera, y entonces un ejercicio con un solo movimiento dentro ya no se " +
            "podría mover nunca");
    }

    /// <summary>
    /// Un ejercicio sin series pero <b>con documentos dentro</b> tampoco se borra.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Y por eso la clave ajena de las series no basta.</b> Borrar un ejercicio ya se negaba
    /// cuando tenía series colgando, porque hay una clave ajena que lo impide. Este caso quita esa
    /// red: la serie se suprime por su puerta —puede, porque todavía no ha numerado— y lo que
    /// queda es un ejercicio sin series y con un ajuste con fecha dentro. Ninguna clave ajena
    /// puede pararlo: el ajuste vive en otro esquema y entre esquemas no se cruza (regla 4). Lo
    /// único que hay entre el borrado y medio año de movimientos huérfanos es la pregunta al
    /// puerto.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_ejercicio_sin_series_pero_con_un_documento_dentro_tampoco_se_borra()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(332);

        (AbrirAjusteDto peticion, SerieDto serie) = await UnAjusteCompletoAsync(cliente, "BOR-A", 366);

        await using ElModuloDeInventario modulo = new(postgres, empresa.Id);

        Resultado<AjusteDto> borrador = await modulo.Alta.EjecutarAsync(peticion, CancellationToken.None);
        borrador.EsCorrecto.ShouldBeTrue($"«{borrador.Error?.Codigo}»");

        // La serie se va por su puerta: no ha numerado, así que se puede suprimir. Con esto cae la
        // red que hasta ahora impedía el borrado por otro motivo.
        (await cliente.SuprimirAsync($"{LosMaestrosPorLaApi.Series}/{serie.Id}")).StatusCode
            .ShouldBe(
                HttpStatusCode.NoContent,
                "sin esto, el 409 de abajo sería el de las series y este caso no diría nada nuevo");

        string recurso = $"{LosMaestrosPorLaApi.Ejercicios}/{serie.EjercicioId}";

        using HttpResponseMessage borrado = await cliente.SuprimirAsync(recurso);

        borrado.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        JsonElement problema = await LeerProblema(borrado);

        problema.GetProperty("type").GetString().ShouldBe("/errors/ejercicio-con-documentos");

        problema.GetProperty("detail").GetString()!.ShouldContain("Inventario", Case.Sensitive);

        // Y el ejercicio sigue ahí, leído por su puerta y no por lo que dijo el 409.
        (await cliente.GetAsync(recurso)).StatusCode.ShouldBe(HttpStatusCode.OK);
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
            ElDiaDelDocumento,
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
