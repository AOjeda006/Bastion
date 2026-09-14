using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Catalogo;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Catalogo.UnitTests.Dobles;
using Bastion.Terceros.Contracts.Terceros;
using Shouldly;

namespace Bastion.Catalogo.UnitTests.Catalogo;

/// <summary>
/// La mitad de IDA del cruce mutuo del ítem 1.10: Catálogo preguntándole a Terceros.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí se prueban las decisiones del cruce que no son del dominio.</b> La primera: los tres
/// estados que contesta <c>IConsultaDeTerceros</c> se juntan en DOS respuestas, porque distinguir
/// los noes convertiría el formulario de «añadir proveedor» en el censo de las bajas del artículo
/// 32. La segunda: el estado se pregunta ANTES que el duplicado. La tercera: el listado esconde las
/// filas cuyo tercero el puerto no deja tratar, y ese filtro no lo pone la pantalla.
/// </para>
/// <para>
/// <b>Un bloqueado, aquí, es <c>NoExiste</c></b>, porque es lo que contesta el puerto de verdad:
/// el filtro del art. 32 esconde la ficha antes de la consulta. Que lo contesta así lo afirma
/// <c>ElPuertoDeTercerosContraLaBaseTests</c> contra PostgreSQL; lo que se prueba en este fichero
/// es qué hace el caso de uso con cada valor.
/// </para>
/// <para>
/// <b>Ninguna de las dos es un invariante del agregado</b>, así que no se pueden probar en el
/// dominio: <c>ArticuloProveedor</c> recibe un <c>Guid</c> ya preguntado y no sale a buscar nada.
/// Y ninguna abre una conexión, así que tampoco tienen que vivir detrás de Testcontainers — que es
/// donde se quedarían sin ejercer el día que Docker no arranque.
/// </para>
/// </remarks>
public sealed class ProveedoresDelArticuloTests
{
    private static readonly DateTimeOffset s_momento =
        new(2026, 9, 13, 7, 30, 0, TimeSpan.Zero);

    private static readonly Guid s_empresaId = Guid.CreateVersion7();

    /// <summary>
    /// Los estados que no autorizan contestan EXACTAMENTE lo mismo: mismo código, mismo tipo y
    /// mismo mensaje, y sin un solo parámetro dentro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La comparación es entre ellos y no contra una constante</b>, y esa es la forma que tiene
    /// este caso de cazar lo que se busca. Fijar el texto a mano dejaría pasar la versión que
    /// devuelve el mismo <c>type</c> con dos mensajes distintos —«ese tercero no existe» y «ese
    /// tercero no es proveedor»—, que filtra casi igual de bien: quien sabe que una ficha existe
    /// y no la ve en el maestro, sabe que está bloqueada.
    /// </para>
    /// <para>
    /// <b>Y el TIPO también se compara, que es la mitad que se olvida.</b> Un 400 para «no existe»
    /// y un 409 para «no es proveedor» separarían los dos casos sin escribir una palabra distinta:
    /// quien recorriera identificadores leería el código de estado y tendría el censo igual.
    /// </para>
    /// <para>
    /// Es el mismo razonamiento que <c>tercero-duplicado</c> del ítem 1.5, y la diferencia con la
    /// mitad de VUELTA de este mismo cruce —donde los dos noes de la tarifa SÍ se distinguen— es
    /// deliberada: allí lo que hay al otro lado es una lista de precios, no la ficha de alguien.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Los_estados_que_no_autorizan_contestan_lo_MISMO()
    {
        List<ErrorDeOperacion> respuestas = [];

        // Todos los valores menos el que autoriza, sacados del enumerado y no escritos: si mañana
        // vuelve a haber un tercer no, entra aquí solo.
        foreach (EstadoDelTercero estado in Enum.GetValues<EstadoDelTercero>()
            .Where(uno => uno != EstadoDelTercero.Disponible))
        {
            Articulo articulo = Alta();
            var terceros = new TercerosEn(estado);

            Resultado<ArticuloProveedorDto> resultado = await Agregar(articulo, terceros)
                .EjecutarAsync(
                    articulo.Id,
                    new AgregarProveedorDto { TerceroId = Guid.CreateVersion7() },
                    CancellationToken.None);

            resultado.EsCorrecto.ShouldBeFalse($"el estado {estado} no autoriza");
            respuestas.Add(resultado.Error!);
        }

        respuestas.Select(error => error.Codigo).Distinct().Count().ShouldBe(
            1,
            "los noes van por un solo `type`: dos códigos distintos son dos preguntas que " +
            "se pueden hacer a un identificador cualquiera");

        respuestas.Select(error => error.Tipo).Distinct().Count().ShouldBe(
            1,
            "y por un solo código de estado, que es la mitad que se olvida: un 400 y un 409 " +
            "separan los casos sin escribir una palabra distinta");

        respuestas.Select(error => error.Mensaje).Distinct().Count().ShouldBe(
            1,
            "y con el mismo texto, sin el identificador dentro: un mensaje que nombrara la ficha " +
            "confirmaría que existe");

        respuestas[0].Campos.ShouldBeEmpty(
            "ni un campo con detalle: el detalle es justo lo que no se puede dar");
    }

    [Fact]
    public async Task El_unico_estado_que_deja_colgar_el_suministro_es_Disponible()
    {
        Articulo articulo = Alta();
        var terceroId = Guid.CreateVersion7();
        var proveedores = new ProveedoresEnMemoria();
        var confirmaciones = new ConfirmacionesContadas();

        Resultado<ArticuloProveedorDto> resultado =
            await Agregar(articulo, new TercerosEn(EstadoDelTercero.Disponible), proveedores, confirmaciones)
                .EjecutarAsync(
                    articulo.Id,
                    new AgregarProveedorDto { TerceroId = terceroId, ReferenciaDelProveedor = " AB-12 " },
                    CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.TerceroId.ShouldBe(terceroId);
        resultado.Valor.ArticuloId.ShouldBe(articulo.Id);
        resultado.Valor.EmpresaId.ShouldBe(s_empresaId, "la empresa sale del claim y no del cuerpo (R8)");
        resultado.Valor.ReferenciaDelProveedor.ShouldBe("AB-12");
        proveedores.Guardados.Count.ShouldBe(1);
        confirmaciones.Veces.ShouldBe(1);
    }

    /// <summary>Se pregunta por el papel de PROVEEDOR, y no por el de cliente.</summary>
    /// <remarks>
    /// El estado depende del papel: el mismo tercero está disponible para comprarle y no para
    /// venderle. Preguntar por el papel equivocado contestaría <c>Disponible</c> para alguien a
    /// quien solo se le vende, y todos los demás casos de este fichero seguirían verdes.
    /// </remarks>
    [Fact]
    public async Task Se_pregunta_por_el_papel_de_proveedor()
    {
        Articulo articulo = Alta();
        var terceroId = Guid.CreateVersion7();
        var terceros = new TercerosEn(EstadoDelTercero.Disponible);

        await Agregar(articulo, terceros).EjecutarAsync(
            articulo.Id,
            new AgregarProveedorDto { TerceroId = terceroId },
            CancellationToken.None);

        terceros.Preguntados.ShouldHaveSingleItem();
        terceros.Preguntados[0].Tercero.ShouldBe(terceroId);
        terceros.Preguntados[0].Rol.ShouldBe(RolDeTercero.Proveedor);
    }

    /// <summary>
    /// El estado se pregunta ANTES que el duplicado: un suministro que ya existía, de un tercero que
    /// el puerto no deja tratar, contesta el mismo 400 que uno inventado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El orden es una decisión y por eso tiene su caso, y estuvo al revés.</b> El listado
    /// esconde el suministro de un tercero bloqueado; si volver a añadirlo contestara «ya está»,
    /// quien comparase lo que el listado enseña con lo que el alta contesta sabría quién está
    /// bloqueado. Lo que se afirma es la mitad que eso rompe: el <c>type</c> del 400, y que el
    /// almacén no llega a mirarse para contestar.
    /// </para>
    /// <para>
    /// Con un tercero disponible el duplicado sí sale como 409, y es el caso de al lado: sin él, un
    /// caso de uso que contestara siempre 400 pasaría este.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_estado_se_pregunta_antes_que_el_duplicado_y_lo_escondido_no_es_un_409()
    {
        Articulo articulo = Alta();
        var terceroId = Guid.CreateVersion7();
        ProveedoresEnMemoria proveedores = new ProveedoresEnMemoria().Con(Suministro(articulo.Id, terceroId));
        var terceros = new TercerosEn(EstadoDelTercero.NoExiste);

        Resultado<ArticuloProveedorDto> resultado =
            await Agregar(articulo, terceros, proveedores).EjecutarAsync(
                articulo.Id,
                new AgregarProveedorDto { TerceroId = terceroId },
                CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Codigo.ShouldBe(
            ErroresDeArticuloProveedor.CodigoDeTerceroNoValido,
            "ha contestado el duplicado. El listado no enseña ese suministro, así que «ya está» " +
            "diría que el tercero existe y está reservado");
        terceros.Preguntados.ShouldHaveSingleItem();
    }

    /// <summary>Con un tercero que se puede tratar, el duplicado es un 409.</summary>
    [Fact]
    public async Task Con_un_tercero_disponible_el_duplicado_es_409()
    {
        Articulo articulo = Alta();
        var terceroId = Guid.CreateVersion7();
        ProveedoresEnMemoria proveedores = new ProveedoresEnMemoria().Con(Suministro(articulo.Id, terceroId));
        var confirmaciones = new ConfirmacionesContadas();

        Resultado<ArticuloProveedorDto> resultado =
            await Agregar(
                    articulo, new TercerosEn(EstadoDelTercero.Disponible), proveedores, confirmaciones)
                .EjecutarAsync(
                    articulo.Id,
                    new AgregarProveedorDto { TerceroId = terceroId },
                    CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Tipo.ShouldBe(TipoDeError.Conflicto);
        resultado.Error.Codigo.ShouldBe("articulo-proveedor-duplicado");
        proveedores.Guardados.Count.ShouldBe(1, "el duplicado no se guarda");
        confirmaciones.Veces.ShouldBe(0);
    }

    [Fact]
    public async Task Un_articulo_que_no_existe_es_404_y_no_se_pregunta_por_el_tercero()
    {
        var terceros = new TercerosEn(EstadoDelTercero.Disponible);

        Resultado<ArticuloProveedorDto> resultado =
            await Agregar(Alta(), terceros).EjecutarAsync(
                Guid.CreateVersion7(),
                new AgregarProveedorDto { TerceroId = Guid.CreateVersion7() },
                CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Tipo.ShouldBe(TipoDeError.NoEncontrado);
        terceros.Preguntados.ShouldBeEmpty();
    }

    /// <summary>
    /// LA RESPUESTA DEL ART. 32 PARA UNA LECTURA: el suministro de un tercero que el puerto no deja
    /// tratar no sale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La lista lleva los tres estados mezclados a propósito</b> —y el que no se puede tratar
    /// es, contra la base, el bloqueado: lo comprueba <c>ContratoDeLosCrucesTests</c>—. Con una
    /// lista de un elemento, un filtro que devolviera siempre vacío y otro que devolviera siempre
    /// todo se distinguirían del correcto por un solo caso cada uno; con tres, el único que pasa es
    /// el que mira ficha a ficha. Y el que no hace el papel SÍ sale: dejó de ser proveedor, pero lo fue, y esconderlo
    /// haría imposible quitar la fila.
    /// </para>
    /// <para>
    /// <b>Lo que sale no lleva total, y ésa es la otra mitad.</b> Un listado paginado con un total
    /// que contara las filas escondidas permitiría restar y saber cuántos hay bloqueados.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_listado_esconde_al_que_no_se_puede_tratar_y_deja_al_que_ya_no_hace_el_papel()
    {
        Articulo articulo = Alta();
        var disponible = Guid.CreateVersion7();
        var bloqueado = Guid.CreateVersion7();
        var sinPapel = Guid.CreateVersion7();

        ProveedoresEnMemoria proveedores = new ProveedoresEnMemoria()
            .Con(Suministro(articulo.Id, disponible))
            .Con(Suministro(articulo.Id, bloqueado))
            .Con(Suministro(articulo.Id, sinPapel));

        TercerosEn terceros = new TercerosEn(EstadoDelTercero.Disponible)
            .Con(bloqueado, EstadoDelTercero.NoExiste)
            .Con(sinPapel, EstadoDelTercero.NoHaceEseRol);

        Resultado<IReadOnlyList<ArticuloProveedorDto>> resultado =
            await new ListarProveedoresDelArticulo(
                    new ArticulosEnMemoria { Guardados = { articulo } }, proveedores, terceros)
                .EjecutarAsync(articulo.Id, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.Select(suministro => suministro.TerceroId)
            .ShouldBe([disponible, sinPapel], ignoreOrder: true);
    }

    /// <summary>Se pregunta UNA vez por el conjunto, y no una vez por fila.</summary>
    /// <remarks>
    /// Es el motivo de que el puerto reciba un conjunto y no un identificador. Una pregunta por
    /// fila convertiría un artículo con veinte proveedores en veintiuna idas a la base — y lo haría
    /// sin ponerse rojo, porque el resultado sería el mismo.
    /// </remarks>
    [Fact]
    public async Task El_listado_pregunta_una_sola_vez_con_todos_los_identificadores()
    {
        Articulo articulo = Alta();
        var uno = Guid.CreateVersion7();
        var otro = Guid.CreateVersion7();

        ProveedoresEnMemoria proveedores = new ProveedoresEnMemoria()
            .Con(Suministro(articulo.Id, uno))
            .Con(Suministro(articulo.Id, otro));

        var terceros = new TercerosEn(EstadoDelTercero.Disponible);

        await new ListarProveedoresDelArticulo(
                new ArticulosEnMemoria { Guardados = { articulo } }, proveedores, terceros)
            .EjecutarAsync(articulo.Id, CancellationToken.None);

        terceros.ConjuntosPreguntados.ShouldHaveSingleItem();
        terceros.ConjuntosPreguntados[0].ShouldBe([uno, otro], ignoreOrder: true);
    }

    /// <summary>Un artículo sin proveedores no le pregunta nada a Terceros.</summary>
    /// <remarks>
    /// Preguntar por el conjunto vacío es una ida a otro módulo para que conteste el vacío. Y no es
    /// una hipótesis: la mayoría de los artículos de un catálogo no tienen proveedor declarado.
    /// </remarks>
    [Fact]
    public async Task Un_articulo_sin_proveedores_no_pregunta_nada()
    {
        Articulo articulo = Alta();
        var terceros = new TercerosEn(EstadoDelTercero.Disponible);

        Resultado<IReadOnlyList<ArticuloProveedorDto>> resultado =
            await new ListarProveedoresDelArticulo(
                    new ArticulosEnMemoria { Guardados = { articulo } },
                    new ProveedoresEnMemoria(),
                    terceros)
                .EjecutarAsync(articulo.Id, CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.ShouldBeEmpty();
        terceros.ConjuntosPreguntados.ShouldBeEmpty();
    }

    /// <summary>Un artículo que no existe es 404, y no una lista vacía.</summary>
    /// <remarks>
    /// Un listado que cuelga de otro recurso tiene dos «no hay nada» distintos, y contestarlos
    /// igual haría indistinguible un identificador equivocado de un artículo sin proveedores.
    /// </remarks>
    [Fact]
    public async Task El_listado_de_un_articulo_que_no_existe_es_404_y_no_una_lista_vacia()
    {
        Resultado<IReadOnlyList<ArticuloProveedorDto>> resultado =
            await new ListarProveedoresDelArticulo(
                    new ArticulosEnMemoria(),
                    new ProveedoresEnMemoria(),
                    new TercerosEn(EstadoDelTercero.Disponible))
                .EjecutarAsync(Guid.CreateVersion7(), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeFalse();
        resultado.Error!.Tipo.ShouldBe(TipoDeError.NoEncontrado);
    }

    /// <summary>
    /// Corregir la referencia de un suministro cuyo tercero está bloqueado SIGUE funcionando.
    /// </summary>
    /// <remarks>
    /// <b>Reservar no es borrar, y es la mitad del art. 32 que se olvida.</b> Si la modificación
    /// volviera a preguntar por el tercero, bloquear a alguien congelaría todas las filas que ya
    /// apuntaban a él: no se podría ni corregir una errata. Lo que aquí se cambia es un código
    /// nuestro para un hecho que ya existía, y no se publica ni un dato de la ficha.
    /// </remarks>
    [Fact]
    public async Task Modificar_la_referencia_no_vuelve_a_preguntar_por_el_tercero()
    {
        Articulo articulo = Alta();
        ArticuloProveedor suministro = Suministro(articulo.Id, Guid.CreateVersion7());
        ProveedoresEnMemoria proveedores = new ProveedoresEnMemoria().Con(suministro);

        Resultado<ArticuloProveedorDto> resultado = await new ModificarProveedorDelArticulo(
                proveedores, new ConfirmacionesContadas(), new VersionesQueDanIgual())
            .EjecutarAsync(
                suministro.Id,
                new VersionDeRecurso(0),
                new ModificarProveedorDto { ReferenciaDelProveedor = "NUEVA" },
                CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        resultado.Valor.ReferenciaDelProveedor.ShouldBe("NUEVA");
    }

    /// <summary>Y quitarlo también, por el mismo motivo.</summary>
    [Fact]
    public async Task Quitar_un_suministro_lo_borra_de_verdad()
    {
        Articulo articulo = Alta();
        ArticuloProveedor suministro = Suministro(articulo.Id, Guid.CreateVersion7());
        ProveedoresEnMemoria proveedores = new ProveedoresEnMemoria().Con(suministro);

        Resultado resultado = await new QuitarProveedorDelArticulo(
                proveedores, new ConfirmacionesContadas(), new VersionesQueDanIgual())
            .EjecutarAsync(
                suministro.Id, new VersionDeRecurso(0), CancellationToken.None);

        resultado.EsCorrecto.ShouldBeTrue();
        proveedores.Eliminados.ShouldHaveSingleItem();
        proveedores.Guardados.ShouldBeEmpty(
            "lo que desaparece no es la ficha de nadie: es un hecho entre dos que ha dejado de " +
            "ser verdad, y su rastro está en la traza");
    }

    private static AgregarProveedorAlArticulo Agregar(
        Articulo articulo,
        TercerosEn terceros,
        ProveedoresEnMemoria? proveedores = null,
        ConfirmacionesContadas? confirmaciones = null) =>
        new(
            new UsuarioDe(s_empresaId),
            new ArticulosEnMemoria { Guardados = { articulo } },
            proveedores ?? new ProveedoresEnMemoria(),
            terceros,
            confirmaciones ?? new ConfirmacionesContadas(),
            new RelojParado(s_momento));

    private static ArticuloProveedor Suministro(Guid articuloId, Guid terceroId) =>
        ArticuloProveedor.Nuevo(s_empresaId, articuloId, terceroId, null, s_momento);

    private static Articulo Alta() =>
        Articulo.Crear(
            s_empresaId,
            "ART-1",
            "Artículo de prueba",
            TipoDeArticulo.Bien,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            s_momento);
}
