using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Inventario;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Contracts.Maestros;
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
/// <b>Y son CUATRO valores desde el 3.6.</b> Hasta entonces fueron tres, porque «solo resuelve lo
/// viejo» no tenía productor: el artículo no se retiraba, y una casilla que nadie produce es el
/// defecto del ítem 1.10, el que la matriz de esta carpeta pone rojo. El 3.6 hizo retirable el
/// artículo (ADR-0082), y la casilla tiene aquí su caso contra la base. El motivo entero está en el
/// <c>remarks</c> de <see cref="AptitudParaMoverExistencias"/>.
/// </para>
/// <para>
/// <b>Semillas: las empresas, del bloque 216-218</b> —el 219 ya estaba cogido—; el reparto del
/// resto está en <c>ContratoDeLosCrucesTests</c> (206-215) y en
/// <c>ElPuertoDeTercerosContraLaBaseTests</c> (200-205). <b>Los maestros van por el 316-318</b>,
/// que es otra cuenta: el número de una empresa acaba en un NIF único en la instalación y el de un
/// artículo en el código de una unidad y un tramo, también únicos pero en otra tabla. Mezclarlos en
/// una sola serie haría que reservar un número para lo uno lo gastara para lo otro.
/// </para>
/// <para>
/// <b>La unidad base, del 2.12, va del 760 al 762</b>, empresas y maestros con el mismo número,
/// como en los ficheros del inventario. El bloque del 760 al 829 es del 2.12: del 750 al 829 no
/// había ninguna, ni por literal ni por cálculo, y el 750 y el 756 que aparecen son milisegundos y
/// una versión. Del resto del bloque, el 763 es de <c>LasGuardasDelAjusteTests</c>, del 764 al 772
/// y el 777 de <c>ElAltaDelRecuentoTests</c>, del 773 al 776 de
/// <c>UnRecuentoEnCursoPorAlmacenTests</c>, del 778 al 787 de <c>LasLineasDelRecuentoTests</c>, del
/// 788 al 792 de <c>LasCarrerasDeLasLineasDelRecuentoTests</c>, del 793 al 806 de
/// <c>LaConfirmacionDelRecuentoTests</c>, el 807 y el 808 de
/// <c>LasCarrerasDeLaConfirmacionDelRecuentoTests</c> y del 809 al 818 de
/// <c>LaAnulacionYElDescarteDelRecuentoTests</c>; del 819 al 829 quedan libres.
/// </para>
/// <para>
/// <b>Los nombres, del 3.2, van del 931 al 934</b>, en el bloque del 3.2, que va del 930 al 949;
/// el 930 es de <c>ElTextoSoloLoRecibeQuienLoMiraTests</c>.
/// </para>
/// <para>
/// <b>La retirada, del 3.6, va en el 977 y el 978</b>, en el bloque del 3.6, que va del 970 al
/// 998: la empresa y el bien en el 977, y el servicio en el 978.
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

    /// <summary>
    /// La unidad base sale por lotes, cada una la suya, y uno de otra empresa contesta lo mismo que
    /// uno inventado: no vuelve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos artículos con dos unidades distintas</b>, porque una consulta que devolviera la misma
    /// unidad para todos pasaría con uno solo.
    /// </para>
    /// <para>
    /// <b>La contraria, al final</b>: en su propia empresa, el ajeno sí vuelve. Sin ella, una consulta
    /// que no devolviera nada pasaría las dos ausencias (ADR-0055 §1).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_unidad_base_sale_por_lotes_y_la_de_otra_empresa_no_vuelve_como_la_inventada()
    {
        (HttpClient cliente, EmpresaDto propia) = await EnUnaEmpresaNuevaAsync(760);
        (HttpClient enLaAjena, EmpresaDto ajena) = await EnUnaEmpresaNuevaAsync(762);

        ArticuloDto uno = await ArticuloAsync(cliente, 760, "Bien");
        ArticuloDto otro = await ArticuloAsync(cliente, 761, "Bien");
        ArticuloDto deLaOtra = await ArticuloAsync(enLaAjena, 762, "Bien");
        var inventado = Guid.CreateVersion7();

        uno.UnidadBaseId.ShouldNotBe(otro.UnidadBaseId, "el caso necesita dos unidades distintas");

        await using (CatalogoDbContext contexto = postgres.AbrirCatalogo(propia.Id))
        {
            IReadOnlyDictionary<Guid, Guid> unidades = await new ConsultaDeArticulos(contexto)
                .UnidadesBaseDeAsync([uno.Id, otro.Id, deLaOtra.Id, inventado], CancellationToken.None);

            unidades.ShouldBe(
                new Dictionary<Guid, Guid>
                {
                    [uno.Id] = uno.UnidadBaseId,
                    [otro.Id] = otro.UnidadBaseId,
                },
                ignoreOrder: true,
                "cada artículo de la empresa con su unidad, y ni el de otra empresa ni el inventado: " +
                "si el ajeno volviera, un recuento podría contar contra un artículo de la empresa de al " +
                "lado");
        }

        await using CatalogoDbContext enSuEmpresa = postgres.AbrirCatalogo(ajena.Id);

        (await new ConsultaDeArticulos(enSuEmpresa)
            .UnidadesBaseDeAsync([deLaOtra.Id], CancellationToken.None))
            .ShouldBe(
                new Dictionary<Guid, Guid> { [deLaOtra.Id] = deLaOtra.UnidadBaseId },
                ignoreOrder: true,
                "en su propia empresa sí vuelve");
    }

    /// <summary>
    /// El código y la descripción salen por lotes, cada uno los suyos, y uno de otra empresa no
    /// vuelve, como el inventado.
    /// </summary>
    /// <remarks>
    /// <b>La descripción, y no otra columna</b>: es lo que la ficha del artículo enseña como su
    /// nombre, y lo que la exportación de un listado escribe al lado del código (ADR-0074). Con dos
    /// artículos, para que una consulta que nombrara a todos con el mismo no pasara; con un tercero
    /// que está y no se pregunta, para que no pasara una que nombrara a todos los de la empresa; y
    /// la contraria al final, como en la unidad base.
    /// </remarks>
    [Fact]
    public async Task El_codigo_y_la_descripcion_salen_por_lotes_y_los_de_otra_empresa_no_vuelven()
    {
        (HttpClient cliente, EmpresaDto propia) = await EnUnaEmpresaNuevaAsync(931);
        (HttpClient enLaAjena, EmpresaDto ajena) = await EnUnaEmpresaNuevaAsync(933);

        ArticuloDto uno = await ArticuloAsync(cliente, 931, "Bien");
        ArticuloDto otro = await ArticuloAsync(cliente, 932, "Servicio");
        ArticuloDto deLaOtra = await ArticuloAsync(enLaAjena, 933, "Bien");
        await ArticuloAsync(cliente, 934, "Bien");
        var inventado = Guid.CreateVersion7();

        await using (CatalogoDbContext contexto = postgres.AbrirCatalogo(propia.Id))
        {
            (await new ConsultaDeArticulos(contexto)
                .NombresDeAsync([uno.Id, otro.Id, deLaOtra.Id, inventado], CancellationToken.None))
                .ShouldBe(
                    new Dictionary<Guid, NombreDeMaestro>
                    {
                        [uno.Id] = new("APT-931", "Artículo 931"),
                        [otro.Id] = new("APT-932", "Artículo 932"),
                    },
                    ignoreOrder: true,
                    "cada artículo por el que se pregunta con lo suyo, y ni el de otra empresa, ni el " +
                    "inventado, ni el de la empresa por el que no se pregunta: " +
                    "si el ajeno volviera, la exportación contaría qué vende la empresa de al lado");
        }

        await using CatalogoDbContext enSuEmpresa = postgres.AbrirCatalogo(ajena.Id);

        (await new ConsultaDeArticulos(enSuEmpresa)
            .NombresDeAsync([deLaOtra.Id], CancellationToken.None))
            .ShouldBe(
                new Dictionary<Guid, NombreDeMaestro> { [deLaOtra.Id] = new("APT-933", "Artículo 933") },
                ignoreOrder: true,
                "en su propia empresa sí vuelve");
    }

    /// <summary>
    /// Un bien retirado solo resuelve lo viejo, un servicio retirado sigue sin almacenarse, y los
    /// dos siguen dando su unidad base y su nombre.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El cuarto valor contra la base, que es lo que pide la matriz</b> (ADR-0082). Con su
    /// contraria al final: reincorporado, el mismo bien vuelve a ofrecerse, así que un adaptador que
    /// contestara <c>SoloResuelveLoViejo</c> a todo lo que alguna vez se retiró no pasaría.
    /// </para>
    /// <para>
    /// <b>El servicio retirado fija el orden de las dos preguntas</b>: «¿se almacena?» va antes que
    /// «¿está retirado?». Al revés, contestaría <c>SoloResuelveLoViejo</c>, y el recuento, que lo
    /// admite, contaría un servicio.
    /// </para>
    /// <para>
    /// <b>Y lo que ya lo nombra se sigue leyendo</b>: la unidad base, que el recuento pide para cada
    /// artículo con físico, y el nombre, que la exportación escribe. Retirar no borra.
    /// </para>
    /// </remarks>
    [Fact]
    [CubreEstadoDelPuerto(
        typeof(IConsultaDeArticulos), AptitudParaMoverExistencias.SoloResuelveLoViejo)]
    public async Task El_bien_retirado_solo_resuelve_lo_viejo_y_sigue_dando_su_unidad_y_su_nombre()
    {
        (HttpClient cliente, EmpresaDto empresa) = await EnUnaEmpresaNuevaAsync(977);

        ArticuloDto bien = await ArticuloAsync(cliente, 977, "Bien");
        ArticuloDto servicio = await ArticuloAsync(cliente, 978, "Servicio");

        await LosMaestrosPorLaApi.RetirarArticuloAsync(cliente, bien.Id);
        await LosMaestrosPorLaApi.RetirarArticuloAsync(cliente, servicio.Id);

        await using (CatalogoDbContext contexto = postgres.AbrirCatalogo(empresa.Id))
        {
            ConsultaDeArticulos puerto = new(contexto);

            (await puerto.AptitudDeAsync(bien.Id, CancellationToken.None))
                .ShouldBe(
                    AptitudParaMoverExistencias.SoloResuelveLoViejo,
                    "un bien retirado se ha dado por bueno para lo nuevo: un ajuste, una " +
                    "transferencia o una reserva nuevos lo elegirían");

            (await puerto.AptitudDeAsync(servicio.Id, CancellationToken.None))
                .ShouldBe(
                    AptitudParaMoverExistencias.NoSeAlmacena,
                    "un servicio retirado sigue sin almacenarse; si contestara que solo resuelve lo " +
                    "viejo, el recuento, que lo admite, contaría horas de mano de obra");

            (await puerto.UnidadesBaseDeAsync([bien.Id, servicio.Id], CancellationToken.None))
                .ShouldBe(
                    new Dictionary<Guid, Guid>
                    {
                        [bien.Id] = bien.UnidadBaseId,
                        [servicio.Id] = servicio.UnidadBaseId,
                    },
                    ignoreOrder: true,
                    "la unidad base de un retirado sigue saliendo: sin ella, el almacén donde le quedan " +
                    "existencias no se podría contar");

            (await puerto.NombresDeAsync([bien.Id, servicio.Id], CancellationToken.None))
                .ShouldBe(
                    new Dictionary<Guid, NombreDeMaestro>
                    {
                        [bien.Id] = new("APT-977", "Artículo 977"),
                        [servicio.Id] = new("APT-978", "Artículo 978"),
                    },
                    ignoreOrder: true,
                    "el nombre de un retirado sigue saliendo: lo que ya lo nombra se sigue leyendo");
        }

        await LosMaestrosPorLaApi.ReincorporarArticuloAsync(cliente, bien.Id);

        await using CatalogoDbContext despues = postgres.AbrirCatalogo(empresa.Id);

        (await new ConsultaDeArticulos(despues).AptitudDeAsync(bien.Id, CancellationToken.None))
            .ShouldBe(
                AptitudParaMoverExistencias.SeOfreceParaLoNuevo,
                "reincorporado, el mismo bien vuelve a ofrecerse");
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
