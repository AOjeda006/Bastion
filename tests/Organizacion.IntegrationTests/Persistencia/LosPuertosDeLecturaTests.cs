using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Domain.Divisas;
using Bastion.Organizacion.Domain.Impuestos;
using Bastion.Organizacion.Domain.Unidades;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// Los tres puertos del ítem 1.2 contra PostgreSQL de verdad: qué contestan de una fila que está,
/// de una que no, y —el impuesto— de una que está pero no rige en la fecha por la que preguntan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué esto no se puede probar con un doble.</b> El puerto es lo único que otro módulo va a
/// tener de este maestro: si contesta mal, el módulo que pregunta no tiene forma de enterarse, y
/// un doble contestaría lo que se le diga. Lo que hay que comprobar es que la traducción a SQL
/// existe y devuelve lo que el dominio diría — en particular la del impuesto, que proyecta las dos
/// fechas y decide en memoria justamente porque <c>Impuesto.RigeEl</c> no se puede traducir.
/// </para>
/// <para>
/// <b>Y por qué los casos vienen en pareja.</b> Un puerto que devolviera siempre
/// <c>SeOfreceParaLoNuevo</c> pasaría los tres casos de alta; uno que devolviera siempre
/// <c>NoExiste</c> pasaría los tres de ausencia. Cada afirmación tiene aquí su contraria, y el
/// impuesto tiene además las dos por la MISMA fila, cambiando solo la fecha: es la única forma de
/// que el parámetro de devengo no pueda ser decorativo.
/// </para>
/// <para>
/// <b>Nada de esto se limpia entre casos, y es a propósito.</b> Cada test da de alta sus propias
/// filas y pregunta por el identificador que acaba de crear, así que las semillas del §12 y lo que
/// dejen los demás tests de la colección le dan igual. Un <c>DELETE</c> de tabla aquí borraría las
/// semillas que otro test de esta misma colección da por puestas.
/// </para>
/// </remarks>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class LosPuertosDeLecturaTests(PostgresDeVerdad postgres)
{
    private static readonly DateTimeOffset s_momento = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task El_impuesto_que_rige_en_la_fecha_se_ofrece_para_lo_nuevo()
    {
        Impuesto tramo = ImpuestoConTramo("PTO-VIG", new DateOnly(2012, 9, 1), null);
        await GuardarAsync(contexto => contexto.Impuestos.Add(tramo));

        EstadoDeMaestro estado = await EstadoDelImpuestoAsync(tramo.Id, new DateOnly(2026, 3, 31));

        estado.ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo);
    }

    [Fact]
    public async Task El_tramo_cerrado_sigue_resolviendo_lo_viejo_pero_no_se_ofrece()
    {
        // El caso del ADR-0023 con el ejemplo que de verdad ocurrió: el IVA general al 18 % dejó de
        // regir el 31 de agosto de 2012. Una factura rectificativa de 2011 tiene que poder resolver
        // ese tramo; una factura nueva no puede usarlo. Dos respuestas distintas de la misma fila.
        Impuesto cerrado = ImpuestoConTramo(
            "PTO-CERR", new DateOnly(2010, 7, 1), new DateOnly(2012, 8, 31));

        await GuardarAsync(contexto => contexto.Impuestos.Add(cerrado));

        (await EstadoDelImpuestoAsync(cerrado.Id, new DateOnly(2011, 6, 30)))
            .ShouldBe(
                EstadoDeMaestro.SeOfreceParaLoNuevo,
                "el 30 de junio de 2011 el tramo regía, y el puerto contesta por la fecha de " +
                "devengo, no por hoy");

        (await EstadoDelImpuestoAsync(cerrado.Id, new DateOnly(2026, 3, 31)))
            .ShouldBe(
                EstadoDeMaestro.SoloResuelveLoViejo,
                "el tramo existe —hay que poder resolver lo emitido bajo él— pero no vale para " +
                "una factura de hoy");
    }

    [Fact]
    public async Task El_tramo_que_todavia_no_ha_entrado_tampoco_se_ofrece()
    {
        // La otra mitad de la comparación de fechas. Sin este caso, un puerto que solo mirara
        // `VigenteHasta` —olvidándose de `VigenteDesde`— pasaría todo lo anterior: daría por
        // vigente un tipo aprobado y publicado que entra en vigor el año que viene.
        Impuesto futuro = ImpuestoConTramo("PTO-FUT", new DateOnly(2030, 1, 1), null);
        await GuardarAsync(contexto => contexto.Impuestos.Add(futuro));

        (await EstadoDelImpuestoAsync(futuro.Id, new DateOnly(2026, 3, 31)))
            .ShouldBe(EstadoDeMaestro.SoloResuelveLoViejo);

        (await EstadoDelImpuestoAsync(futuro.Id, new DateOnly(2030, 1, 1)))
            .ShouldBe(
                EstadoDeMaestro.SeOfreceParaLoNuevo,
                "el rango es cerrado por los dos lados, igual que `Impuesto.RigeEl`: el primer " +
                "día cuenta");
    }

    [Fact]
    public async Task La_divisa_dada_de_alta_se_ofrece_y_la_que_no_esta_no_existe()
    {
        // El yen y no un código inventado: `Divisa.Crear` rechaza lo que el catálogo de los
        // bloques comunes no sabe redondear, porque una divisa guardada sin saber con cuántos
        // decimales redondea es una factura mal calculada esperando. Las semillas del §12 no
        // cargan divisas, así que la tabla está vacía y el índice único no tiene con qué chocar.
        var divisa = Divisa.Crear("JPY", "Yen japonés", s_momento);
        await GuardarAsync(contexto => contexto.Divisas.Add(divisa));

        await using OrganizacionDbContext contexto = postgres.AbrirContexto();
        // El tipo concreto y no la interfaz: CA1859 está como error en este repositorio, y de
        // todos modos que la implementación case con su contrato no es cosa de un test —lo
        // comprueba el compilador en `ModuloDeOrganizacion`, que la registra bajo él—.
        ConsultaDeDivisas puerto = new(contexto);

        (await puerto.EstadoDeAsync(divisa.Id, CancellationToken.None))
            .ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo);

        (await puerto.EstadoDeAsync(Guid.CreateVersion7(), CancellationToken.None))
            .ShouldBe(EstadoDeMaestro.NoExiste);
    }

    [Fact]
    public async Task La_unidad_dada_de_alta_se_ofrece_y_la_que_no_esta_no_existe()
    {
        var unidad = UnidadMedida.Crear("PTU", "Unidad de prueba del puerto", 2, s_momento);
        await GuardarAsync(contexto => contexto.UnidadesDeMedida.Add(unidad));

        await using OrganizacionDbContext contexto = postgres.AbrirContexto();
        ConsultaDeUnidadesDeMedida puerto = new(contexto);

        (await puerto.EstadoDeAsync(unidad.Id, CancellationToken.None))
            .ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo);

        (await puerto.EstadoDeAsync(Guid.CreateVersion7(), CancellationToken.None))
            .ShouldBe(EstadoDeMaestro.NoExiste);
    }

    [Fact]
    public async Task El_impuesto_que_no_esta_no_existe()
    {
        (await EstadoDelImpuestoAsync(Guid.CreateVersion7(), new DateOnly(2026, 3, 31)))
            .ShouldBe(EstadoDeMaestro.NoExiste);
    }

    [Fact]
    public async Task Los_tres_puertos_contestan_sin_empresa_activa_y_sin_ver_lo_bloqueado()
    {
        // Esta es la comprobación que faltaba para poder decir que los tres maestros son de
        // INSTALACIÓN, y no se hace leyendo el código: el contexto que abre `PostgresDeVerdad`
        // lleva un inquilino y un acceso que LANZAN en cuanto alguien les pregunta. Que los tres
        // puertos contesten sin excepción es la prueba de que ni R8 ni R16 se les aplican — o sea,
        // de que un `NoExiste` significa «no hay esa fila» y nunca «no es de tu empresa» ni «está
        // bloqueada y no puedes verla», que es lo que el consumidor no sabría distinguir.
        //
        // El día que alguien le ponga a `Impuesto`, `Divisa` o `UnidadMedida` un filtro de empresa
        // o el de bloqueo, este test no se pone rojo por una aserción: revienta con
        // `FaltaLaEmpresaActivaException` o con `NotSupportedException`, que dicen exactamente
        // cuál de las dos ha sido.
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        var inventado = Guid.CreateVersion7();

        EstadoDeMaestro[] respuestas =
        [
            await new ConsultaDeImpuestos(contexto).EstadoDeAsync(
                inventado, new DateOnly(2026, 3, 31), CancellationToken.None),
            await new ConsultaDeDivisas(contexto).EstadoDeAsync(
                inventado, CancellationToken.None),
            await new ConsultaDeUnidadesDeMedida(contexto).EstadoDeAsync(
                inventado, CancellationToken.None),
        ];

        respuestas.ShouldAllBe(estado => estado == EstadoDeMaestro.NoExiste);
    }

    /// <summary>Un tramo con código propio de cada caso, para no chocar con el EXCLUDE de solape.</summary>
    private static Impuesto ImpuestoConTramo(string codigo, DateOnly desde, DateOnly? hasta) =>
        Impuesto.Crear(
            codigo,
            "Impuesto de prueba del puerto",
            TipoDeImpuesto.Iva,
            21.00m,
            desde,
            hasta,
            null,
            null,
            s_momento);

    private async Task<EstadoDeMaestro> EstadoDelImpuestoAsync(Guid impuestoId, DateOnly devengo)
    {
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        return await new ConsultaDeImpuestos(contexto).EstadoDeAsync(
            impuestoId, devengo, CancellationToken.None);
    }

    /// <summary>Da de alta por el mismo camino que la aplicación: el agregado y su contexto.</summary>
    private async Task GuardarAsync(Action<OrganizacionDbContext> alta)
    {
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        alta(contexto);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
