using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Contracts.Impuestos;
using Bastion.Organizacion.Contracts.Unidades;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Cruces;

/// <summary>
/// <c>IConsultaDeArticulos</c> contra PostgreSQL: qué aptitud contesta de un bien, de un servicio,
/// de uno que no está y de uno de otra empresa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué el puerto y no la API.</b> Hoy este puerto no tiene consumidor —lo estrena el ítem
/// 2.3, que es quien valida el artículo de un movimiento—, así que no hay ninguna petición cuya
/// respuesta dependa de él. Lo que decide cada valor es una consulta con el filtro de empresa
/// puesto, y eso solo se ve preguntándole a la consulta.
/// </para>
/// <para>
/// <b>Las fichas se dan de alta por la API.</b> Montarlas con SQL probaría filas que el sistema no
/// produce; y un artículo necesita además una unidad y un tramo de impuesto que existan, porque su
/// alta los valida contra sus puertos (ADR-0024).
/// </para>
/// <para>
/// <b>Y son TRES valores, no cuatro.</b> El enumerado no tiene «solo resuelve lo viejo» porque hoy
/// ningún ítem le da una baja al artículo: sería una casilla que ningún productor produce, que es
/// el defecto del ítem 1.10 y el que la matriz de esta carpeta pone rojo. El motivo entero está en
/// el <c>remarks</c> de <see cref="AptitudParaMoverExistencias"/>, y la pregunta que lo reabriría
/// —qué le pasa al artículo cuando el 2.7 le dé existencias— está anotada como pregunta del cierre
/// de la fase 2.
/// </para>
/// <para>
/// <b>Semillas: las empresas, del bloque 216-218</b> —el 219 ya estaba cogido—; el reparto del
/// resto está en <c>ContratoDeLosCrucesTests</c> (206-215) y en
/// <c>ElPuertoDeTercerosContraLaBaseTests</c> (200-205). <b>Los maestros van por el 316-318</b>,
/// que es otra cuenta: el número de una empresa acaba en un NIF único en la instalación y el de un
/// artículo en el código de una unidad y un tramo, también únicos pero en otra tabla. Mezclarlos en
/// una sola serie haría que reservar un número para lo uno lo gastara para lo otro.
/// </para>
/// </remarks>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class ElPuertoDelArticuloContraLaBaseTests(PostgresConTodosLosModulos postgres)
    : IDisposable
{
    private const string Articulos = "/api/v1/catalogo/articulos";
    private const string Unidades = "/api/v1/organizacion/unidades-de-medida";
    private const string Impuestos = "/api/v1/organizacion/impuestos";

    private static readonly DateOnly s_desde = new(2000, 1, 1);

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
    /// Un bien se ofrece para lo nuevo; un servicio no se almacena, y el tipo no es decorativo.
    /// </summary>
    /// <remarks>
    /// <b>Las dos casillas sobre dos fichas de la MISMA empresa y en el mismo caso</b>, porque
    /// cada una es la contraria de la otra: un adaptador que ignorara el tipo contestaría lo mismo
    /// a las dos, y por separado cualquiera de las dos aserciones lo dejaría pasar. La diferencia
    /// es de negocio y no de matiz — un servicio no tiene existencias que mover, así que un
    /// movimiento contra él no es un error de permisos: es una operación que no significa nada.
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(
        typeof(IConsultaDeArticulos), AptitudParaMoverExistencias.SeOfreceParaLoNuevo)]
    [CubreEstadoDelPuerto(
        typeof(IConsultaDeArticulos), AptitudParaMoverExistencias.NoSeAlmacena)]
    public async Task El_bien_se_ofrece_y_el_servicio_no_se_almacena()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(216);

        ArticuloDto bien = await ArticuloAsync(cliente, 316, "Bien");
        ArticuloDto servicio = await ArticuloAsync(cliente, 317, "Servicio");

        await using CatalogoDbContext contexto = postgres.AbrirCatalogo(empresa.Id);

        // El tipo concreto y no la interfaz: CA1859 está como error en este repositorio, y que la
        // implementación case con su contrato lo comprueba el compilador en `ModuloDeCatalogo`.
        ConsultaDeArticulos puerto = new(contexto);

        (await puerto.AptitudDeAsync(bien.Id, CancellationToken.None))
            .ShouldBe(AptitudParaMoverExistencias.SeOfreceParaLoNuevo);

        (await puerto.AptitudDeAsync(servicio.Id, CancellationToken.None))
            .ShouldBe(
                AptitudParaMoverExistencias.NoSeAlmacena,
                "un servicio no tiene existencias, y se ha dado por bueno para moverlas: el libro " +
                "admitiría una entrada de diez horas de mano de obra en una estantería");
    }

    /// <summary>
    /// Uno inventado y uno de OTRA empresa no existen, y el ajeno sí existe en la suya.
    /// </summary>
    /// <remarks>
    /// <b>La contraria es la que impide el falso verde</b>: sin la última pregunta, un adaptador
    /// que contestara <c>NoExiste</c> a todo pasaría las dos primeras. Y es la R8 dicha como una
    /// aptitud — el filtro de empresa no es una optimización de esta consulta, es la comprobación.
    /// Desde fuera de Catálogo, «no hay ficha» y «la hay pero es de otra empresa» son lo mismo a
    /// propósito: distinguirlas contaría qué vende la empresa de al lado.
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(typeof(IConsultaDeArticulos), AptitudParaMoverExistencias.NoExiste)]
    public async Task Uno_inventado_y_uno_de_otra_empresa_NoExisten_y_el_ajeno_si_en_la_suya()
    {
        (HttpClient _, EmpresaDto propia) = await EnUnaEmpresaNuevaAsync(217);
        (HttpClient enLaAjena, EmpresaDto ajena) = await EnUnaEmpresaNuevaAsync(218);

        ArticuloDto deLaOtra = await ArticuloAsync(enLaAjena, 318, "Bien");

        await using (CatalogoDbContext contexto = postgres.AbrirCatalogo(propia.Id))
        {
            ConsultaDeArticulos puerto = new(contexto);

            (await puerto.AptitudDeAsync(Guid.CreateVersion7(), CancellationToken.None))
                .ShouldBe(AptitudParaMoverExistencias.NoExiste);

            (await puerto.AptitudDeAsync(deLaOtra.Id, CancellationToken.None))
                .ShouldBe(
                    AptitudParaMoverExistencias.NoExiste,
                    "un artículo de otra empresa de la instalación se ve desde esta. Un movimiento " +
                    "podría apuntar a él y la R8 quedaría rota por dentro, sin clave ajena que " +
                    "chistara");
        }

        await using CatalogoDbContext enSuEmpresa = postgres.AbrirCatalogo(ajena.Id);

        (await new ConsultaDeArticulos(enSuEmpresa).AptitudDeAsync(deLaOtra.Id, CancellationToken.None))
            .ShouldBe(
                AptitudParaMoverExistencias.SeOfreceParaLoNuevo, "en su propia empresa sí se ofrece");
    }

    private async Task<(HttpClient Cliente, EmpresaDto Empresa)> EnUnaEmpresaNuevaAsync(int semilla)
    {
        (HttpClient cliente, EmpresaDto empresa) =
            await _api.EnUnaEmpresaNuevaAsync(Escenario.NifInventado(semilla));

        _clientes.Add(cliente);

        return (cliente, empresa);
    }

    /// <summary>Un artículo con su unidad y su tramo de impuesto, propios de este caso.</summary>
    /// <remarks>
    /// La unidad y el tramo llevan el número del caso porque son maestros de instalación: los ve
    /// toda la base, y dos casos con el mismo código chocarían contra el índice único.
    /// </remarks>
    private static async Task<ArticuloDto> ArticuloAsync(
        HttpClient cliente, int semilla, string tipo)
    {
        string sufijo = semilla.ToString(CultureInfo.InvariantCulture);

        using HttpResponseMessage unidad = await cliente.PostAsJsonAsync(
            Unidades,
            new CrearUnidadMedidaDto
            {
                Codigo = "W" + sufijo,
                Nombre = "Unidad " + sufijo,
                Decimales = 0,
            });

        unidad.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(unidad));

        using HttpResponseMessage impuesto = await cliente.PostAsJsonAsync(
            Impuestos,
            new CrearImpuestoDto
            {
                Codigo = "ART" + sufijo,
                Nombre = "Tramo ART" + sufijo,
                Tipo = "Iva",
                Porcentaje = 21m,
                VigenteDesde = s_desde,
                VigenteHasta = null,
            });

        impuesto.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(impuesto));

        using HttpResponseMessage alta = await cliente.PostAsJsonAsync(
            Articulos,
            new CrearArticuloDto
            {
                Codigo = "APT-" + sufijo,
                Descripcion = "Artículo " + sufijo,
                Tipo = tipo,
                UnidadBaseId = (await unidad.Content.ReadFromJsonAsync<UnidadMedidaDto>())!.Id,
                ImpuestoPorDefectoId = (await impuesto.Content.ReadFromJsonAsync<ImpuestoDto>())!.Id,
                CategoriaId = null,
            });

        alta.StatusCode.ShouldBe(HttpStatusCode.Created, await Escenario.Detalle(alta));

        return (await alta.Content.ReadFromJsonAsync<ArticuloDto>())!;
    }
}
