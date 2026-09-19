using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Bastion.Inventario.Application.Ajustes;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Contracts.Unidades;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// El ADR-0037 cobrado <b>por el efecto</b>: contra un almacén bloqueado no se abre un ajuste, y
/// lo que ya estaba escrito contra ese mismo almacén se sigue leyendo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto es lo que el ítem 2.2 no tenía con qué hacerse.</b> Allí el puerto se comprobó por lo
/// que contesta —<c>SoloResuelveLoViejo</c> a un almacén bloqueado— y no se pudo comprobar por lo
/// que eso <i>provoca</i>, porque no había ningún consumidor: el primero es el alta de un ajuste,
/// y el alta es de este ítem. Una respuesta que nadie usa es una respuesta que nadie ha visto
/// funcionar; aquí se ve.
/// </para>
/// <para>
/// <b>Los adaptadores son los de verdad y los maestros se dan de alta por la API.</b> Con dobles,
/// las dos mitades comprobarían que el doble contesta lo que se le programó, que es justo lo que
/// no hace falta saber: lo que decide cada respuesta es una consulta con el filtro del art. 32
/// puesto y un ámbito abierto con su motivo, y eso solo existe contra una base.
/// </para>
/// <para>
/// <b>Y el acceso a lo bloqueado es el real, no el <c>AccesoCerrado</c> de la puerta de atrás.</b>
/// Aquel lanza en cuanto alguien abre un ámbito, y estos dos puertos abren uno —tienen que
/// abrirlo, o el almacén bloqueado no llegaría a la consulta y contestarían <c>NoExiste</c>, que
/// es la respuesta que el ADR-0037 descarta—. Es el mismo montaje que
/// <c>UnaEstanteriaBloqueadaSigueExistiendoTests</c> usa en el carril de Organización.
/// </para>
/// <para>
/// <b>Semillas: las empresas van por el 249 y el 251</b> —el reparto del resto está en
/// <c>ElPuertoDelArticuloContraLaBaseTests</c> y en <c>ContratoDeLosCrucesTests</c>—, y
/// <b>los maestros de instalación por el 349</b>, que es la otra cuenta: el número de una empresa
/// acaba en un NIF único, y el de una unidad o un tramo de impuesto en un código único de otra
/// tabla. Mezclarlas en una sola serie haría que reservar un número para lo uno lo gastara para
/// lo otro.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class UnAlmacenBloqueadoNoAdmiteAjustesTests(PostgresConTodosLosModulos postgres)
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

    /// <summary>
    /// Las dos mitades sobre el MISMO almacén: antes de bloquearlo el alta pasa y el movimiento se
    /// lee ofreciéndose; después, el alta se rechaza y el movimiento <b>sigue leyéndose</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El control es el mismo almacén antes de bloquearlo, y por eso no hay un segundo
    /// almacén.</b> Un alta contra otro almacén abierto demostraría que el alta no rechaza
    /// <i>todo</i>; el mismo almacén antes y después demuestra algo más estrecho y más útil: que
    /// lo único que ha cambiado entre el verde y el rojo es el bloqueo. Lo mismo con la lectura,
    /// donde el <c>true</c> de antes es lo que impide que el <c>false</c> de después se confunda
    /// con una propiedad que nadie rellena.
    /// </para>
    /// <para>
    /// <b>Y los contextos se vuelven a abrir después de bloquear.</b> No porque EF Core cachee la
    /// respuesta —los dos filtros leen propiedades de instancia en cada consulta, y estos puertos
    /// proyectan en vez de traer la entidad—, sino porque lo que se quiere describir es una
    /// petición nueva contra un estado nuevo, que es como llegará de verdad.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Bloquear_el_almacen_cierra_el_alta_y_deja_en_pie_lo_ya_escrito()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(249);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, "ADR37-A");
        UbicacionDto ubicacion = await LosMaestrosPorLaApi.CrearUbicacionAsync(cliente, almacen.Id, "ADR37-A-01");
        (Guid articuloId, Guid unidadId) = await LosMaestrosPorLaApi.CrearArticuloAsync(cliente, 349);

        // LA SERIE ES DE VERDAD, y desde el ítem 2.4 no queda otra: confirmar toma un número con
        // una sentencia que exige que la serie exista, sea de esta empresa y siga activa. Una
        // serie inventada haría fallar la confirmación de abajo por un motivo que no es el de
        // este caso, y el rojo diría «serie-no-numera» donde se habla de almacenes.
        SerieDto serie = await LosMaestrosPorLaApi.CrearSerieAsync(cliente, "ADR37");

        AbrirAjusteDto peticion = new(
            serie.Id,
            almacen.Id,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Regularización de un recuento",
            [new LineaDeAjusteDto(ubicacion.Id, articuloId, 4m, unidadId, 2m, 1.50m, "EUR")]);

        Guid ajusteId;

        await using (ElModuloDeInventario antes = Abrir(empresa.Id))
        {
            Resultado<AjusteDto> alta =
                await antes.Alta.EjecutarAsync(peticion, CancellationToken.None);

            alta.EsCorrecto.ShouldBeTrue(
                "con el almacén sin bloquear el alta tiene que pasar; si no, el rechazo de abajo " +
                $"no diría nada del bloqueo. Contestó «{alta.Error?.Codigo}»");

            ajusteId = alta.Valor.Id;

            Resultado<AjusteDto> confirmacion = await antes.ConfirmarAsync(ajusteId);

            confirmacion.EsCorrecto.ShouldBeTrue(
                "sin confirmar no hay ninguna fila del libro que leer después. Contestó " +
                $"«{confirmacion.Error?.Codigo}»");

            IReadOnlyList<MovimientoDto> conElAbierto =
                (await antes.Lectura.DeUnAjusteAsync(ajusteId, CancellationToken.None)).Valor;

            conElAbierto.Count.ShouldBe(
                1, "el documento tenía una línea y la confirmación escribe una fila por línea");

            conElAbierto[0].AlmacenSeOfreceParaLoNuevo.ShouldBeTrue(
                "antes del bloqueo el almacén se ofrece; sin esta mitad, el `false` de abajo lo " +
                "daría igual una propiedad que nadie rellena");
        }

        using (HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{LosMaestrosPorLaApi.Almacenes}/{almacen.Id}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        await using ElModuloDeInventario despues = Abrir(empresa.Id);

        // Mitad 1 — el alta se cierra. Y se cierra POR EL BLOQUEO: el código lo dice, que es lo
        // que distingue este rechazo del que daría un almacén que no existiera.
        Resultado<AjusteDto> rechazada =
            await despues.Alta.EjecutarAsync(peticion, CancellationToken.None);

        rechazada.EsCorrecto.ShouldBeFalse(
            "el almacén está bloqueado y el alta solo admite `SeOfreceParaLoNuevo`");

        rechazada.Error!.Codigo.ShouldBe(
            "ajuste-almacen-bloqueado",
            "si contesta «no encontrado», el puerto está diciendo `NoExiste` de un almacén que " +
            $"existe, que es lo que el ADR-0037 descarta. Dijo «{rechazada.Error!.Mensaje}»");

        rechazada.Error!.Tipo.ShouldBe(
            TipoDeError.Conflicto,
            "el borde lo publica como 409: los datos enviados son correctos y volver a enviarlos " +
            "igual no arregla nada");

        // Mitad 2 — y lo que ya estaba escrito sigue ahí, y se lee.
        IReadOnlyList<MovimientoDto> conElBloqueado =
            (await despues.Lectura.DeUnAjusteAsync(ajusteId, CancellationToken.None)).Valor;

        conElBloqueado.Count.ShouldBe(
            1,
            "bloquear el almacén ha hecho desaparecer el movimiento que había dentro: lo que el " +
            "art. 32 reserva es la privacidad de una persona, no la existencia de una estantería");

        // Y la fila que se lee es la que está en la base, preguntada en crudo y sin ningún filtro
        // por medio: sin esto, una lectura que devolviera un objeto inventado pasaría igual.
        IReadOnlyList<string> enLaBase = await ElLibro.TextosAsync(
            postgres,
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT id::text FROM inventario.movimiento_stock
                WHERE documento_origen_id = '{ajusteId}'
                """));

        enLaBase.Count.ShouldBe(1, "la confirmación escribió una fila y el libro no se limpia");

        conElBloqueado[0].Id.ShouldBe(Guid.Parse(enLaBase[0], CultureInfo.InvariantCulture));

        conElBloqueado[0].CantidadEnUnidadBase.ShouldBe(
            8m, "cuatro de las introducidas por un factor de dos, tal como se escribió");

        conElBloqueado[0].AlmacenSeOfreceParaLoNuevo.ShouldBeFalse(
            "el movimiento se lee y además CUENTA que su almacén ya no admite nada nuevo: es la " +
            "misma llamada al mismo puerto que el alta, con la respuesta usada como dato en vez " +
            "de como condición");
    }

    /// <summary>
    /// Un almacén bloqueado y uno inventado no contestan lo mismo, y esa diferencia es el
    /// ADR-0037 entero.
    /// </summary>
    /// <remarks>
    /// <b>Las dos respuestas en el mismo caso y con la misma empresa</b>, porque por separado
    /// ninguna de las dos afirma nada: un puerto que contestara <c>NoExiste</c> a lo bloqueado
    /// pasaría la segunda mitad, y uno que contestara <c>SoloResuelveLoViejo</c> a lo que no
    /// existe pasaría la primera. Lo que hay que ver es que <b>se distinguen</b>, porque de esa
    /// distinción depende que un movimiento escrito contra un almacén bloqueado siga teniendo a
    /// qué apuntar (R13).
    /// </remarks>
    [Fact]
    public async Task Un_almacen_bloqueado_y_uno_inventado_no_contestan_lo_mismo()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(251);

        AlmacenDto almacen = await LosMaestrosPorLaApi.CrearAlmacenAsync(cliente, "ADR37-B");

        using (HttpResponseMessage bloqueo = await cliente.SuprimirAsync($"{LosMaestrosPorLaApi.Almacenes}/{almacen.Id}"))
        {
            bloqueo.IsSuccessStatusCode.ShouldBeTrue(await Escenario.Detalle(bloqueo));
        }

        await using ElModuloDeInventario modulo = Abrir(empresa.Id);

        // La línea lleva identificadores inventados a propósito: el alta comprueba el almacén
        // ANTES que las líneas, así que ninguno de los dos casos llega a preguntarlos. Lo que se
        // compara es la respuesta del almacén y nada más.
        Resultado<AjusteDto> contraElBloqueado = await modulo.Alta.EjecutarAsync(
            UnaPeticionContra(almacen.Id), CancellationToken.None);

        Resultado<AjusteDto> contraElInventado = await modulo.Alta.EjecutarAsync(
            UnaPeticionContra(Guid.CreateVersion7()), CancellationToken.None);

        contraElBloqueado.Error!.Codigo.ShouldBe("ajuste-almacen-bloqueado");
        contraElBloqueado.Error!.Tipo.ShouldBe(TipoDeError.Conflicto);

        contraElInventado.Error!.Codigo.ShouldBe(
            "ajuste-almacen-no-encontrado",
            "un almacén que no existe no está bloqueado: son dos estados distintos y el alta los " +
            "tiene que poder decir por separado");

        contraElInventado.Error!.Tipo.ShouldBe(
            TipoDeError.Validacion,
            "el identificador que se envió no es válido, y eso es un 400 y no un 409");
    }

    // La serie va inventada, y aquí sí vale: esta petición solo se usa en casos que el alta
    // rechaza por el almacén, y el alta no mira la serie —lo hace la confirmación, que estos
    // casos no llegan a pedir—.
    private static AbrirAjusteDto UnaPeticionContra(Guid almacenId) => new(
        Guid.CreateVersion7(),
        almacenId,
        DateOnly.FromDateTime(DateTime.UtcNow),
        "Ajuste que no debería llegar a mirar sus líneas",
        [new LineaDeAjusteDto(
            Guid.CreateVersion7(), Guid.CreateVersion7(), 1m, Guid.CreateVersion7(), 1m, 1m, "EUR")]);

    /// <summary>
    /// El módulo de Inventario cableado a mano, con los adaptadores de verdad y contra la base.
    /// </summary>
    /// <remarks>El motivo de cablearlo a mano está en <see cref="ElModuloDeInventario"/>.</remarks>
    /// <param name="empresaId">Empresa en la que opera todo lo de dentro (R8).</param>
    private ElModuloDeInventario Abrir(Guid empresaId) => new(postgres, empresaId);

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }
}
