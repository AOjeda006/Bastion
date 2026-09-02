using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Organizacion.Application;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Semillas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// El cargador de <c>db/semillas/</c> contra PostgreSQL de verdad: que mete lo que trae el
/// fichero, que meterlo dos veces no duplica nada, y que lo que mete cabe donde va.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los ficheros son los DE VERDAD</b> y no unos de prueba: es la única forma de que este test
/// diga algo sobre lo que se despliega. Un <c>impuestos.json</c> inventado probaría el cargador y
/// dejaría sin probar la semilla, que es la mitad que se edita a mano y la que puede traer un
/// porcentaje con una coma de más o dos tramos que se pisan.
/// </para>
/// <para>
/// <b>Y por eso la restricción de exclusión participa de verdad.</b> Los doce tramos entran contra
/// una tabla que prohíbe el solape: si alguien edita el <c>.json</c> y deja dos tramos de
/// <c>IVA-GENERAL</c> pisándose, este test se cae con un <c>23P01</c> —el mismo que se caería el
/// migrador—, no con una comparación de números.
/// </para>
/// <para>
/// Lo que aquí NO se prueba es que el migrador lo llame: eso es cableado del <i>composition
/// root</i>, y quien lo comprueba —por el efecto y dentro de la imagen— es el job <c>Humo</c>.
/// </para>
/// </remarks>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class LaCargaDeSemillasTests(PostgresDeVerdad postgres) : IAsyncLifetime
{
    private const string Esquema = OrganizacionDbContext.Esquema;

    /// <inheritdoc/>
    // Las dos tablas ANTES de cada caso, y no después, por lo mismo que en el test de los
    // maestros: un caso que se cae a mitad no puede dejar al siguiente contando filas suyas.
    public async Task InitializeAsync() =>
        await EjecutarAsync(
            $"DELETE FROM {Esquema}.impuestos; DELETE FROM {Esquema}.unidades_de_medida;");

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task La_carga_deja_dentro_todo_lo_que_trae_el_fichero()
    {
        int impuestosDelFichero = SemillasDeOrganizacion
            .Leer<FilaDeImpuesto>(SemillasDeOrganizacion.CarpetaPublicada, SemillasDeOrganizacion.Impuestos)
            .Count;
        int unidadesDelFichero = SemillasDeOrganizacion
            .Leer<FilaDeUnidad>(SemillasDeOrganizacion.CarpetaPublicada, SemillasDeOrganizacion.UnidadesDeMedida)
            .Count;

        // La afirmación de conjunto no vacío, dicha antes de empezar: si el fichero llegara vacío,
        // todo lo de abajo compararía cero con cero y saldría verde sin haber cargado nada.
        impuestosDelFichero.ShouldBeGreaterThan(0);
        unidadesDelFichero.ShouldBeGreaterThan(0);

        await CargarAsync();

        (await ContarAsync("impuestos")).ShouldBe(impuestosDelFichero);
        (await ContarAsync("unidades_de_medida")).ShouldBe(unidadesDelFichero);
    }

    [Fact]
    public async Task Cargar_dos_veces_no_duplica_nada()
    {
        // El migrador corre en CADA despliegue, no solo en el primero. Sin esta propiedad, la
        // segunda vuelta o duplicaría los maestros o se estrellaría contra el índice único, y el
        // segundo despliegue de una instalación en marcha saldría con 1.
        await CargarAsync();

        int impuestos = await ContarAsync("impuestos");
        int unidades = await ContarAsync("unidades_de_medida");

        await CargarAsync();

        (await ContarAsync("impuestos")).ShouldBe(impuestos);
        (await ContarAsync("unidades_de_medida")).ShouldBe(unidades);
    }

    [Fact]
    public async Task Los_tramos_del_IVA_general_entran_los_tres_y_solo_uno_queda_abierto()
    {
        await CargarAsync();

        await using OrganizacionDbContext contexto = AbrirContexto();

        // Tres filas del mismo código, que es lo que un índice único habría impedido y la
        // restricción de exclusión permite: un impuesto no se edita, se sucede.
        List<DateOnly?> tramos =
        [
            .. await contexto.Impuestos
                .Where(impuesto => impuesto.Codigo == "IVA-GENERAL")
                .OrderBy(impuesto => impuesto.VigenteDesde)
                .Select(impuesto => impuesto.VigenteHasta)
                .ToListAsync(),
        ];

        tramos.Count.ShouldBe(3);

        // Y exactamente uno abierto: dos abiertos harían que «el IVA general del día D» tuviera
        // dos respuestas. No es una preferencia de este test —lo prohíbe la base—, es la
        // comprobación de que lo prohibido de verdad no está en el fichero.
        tramos.Count(hasta => hasta is null).ShouldBe(1);
        tramos[^1].ShouldBeNull("el tramo abierto tiene que ser el último, no uno de en medio");
    }

    [Fact]
    public async Task El_porcentaje_se_guarda_con_los_decimales_que_tiene_la_columna()
    {
        await CargarAsync();

        await using OrganizacionDbContext contexto = AbrirContexto();

        decimal vigente = await contexto.Impuestos
            .Where(impuesto => impuesto.Codigo == "IVA-GENERAL" && impuesto.VigenteHasta == null)
            .Select(impuesto => impuesto.Porcentaje)
            .SingleAsync();

        // R6: el porcentaje es decimal, y va como aparece en el BOE. Un 21 que volviera como
        // 20,999999 sería la señal de que alguien lo pasó por coma flotante en algún tramo del
        // camino, y esa diferencia acaba en la casilla de un modelo 303.
        vigente.ShouldBe(21m);
    }

    [Fact]
    public async Task La_carga_declara_por_que_no_tiene_empresa()
    {
        InquilinoDeLaCarga inquilino = new();

        await CargarAsync(inquilino);

        // Los maestros son de la instalación (R8) y aquí no hay petición: el ámbito se abre a
        // propósito y con su motivo, que es el que acaba en la columna de la traza. Comprobarlo
        // aquí es lo que impide que alguien lo cambie por otro cualquiera para que compile.
        inquilino.Motivos.ShouldBe([MotivoSinInquilino.CargaDeMaestros]);
    }

    private async Task CargarAsync(InquilinoDeLaCarga? inquilino = null)
    {
        await using OrganizacionDbContext contexto = AbrirContexto();

        await new CargadorDeSemillasDeOrganizacion(
                contexto,
                new UnidadDeTrabajoDelTest(contexto),
                inquilino ?? new InquilinoDeLaCarga(),
                TimeProvider.System,
                NullLogger<CargadorDeSemillasDeOrganizacion>.Instance)
            .CargarAsync(CancellationToken.None);
    }

    // El contexto se abre a mano y no con `postgres.AbrirContexto()`: el doble de aquel lanza en
    // cuanto alguien abre un ámbito sin inquilino, y eso es exactamente lo que el cargador hace.
    private OrganizacionDbContext AbrirContexto()
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, postgres.CadenaDeConexion);

        return new OrganizacionDbContext(
            opciones.Options, new InquilinoDeLaCarga(), new AccesoQueNadieDebeAbrir());
    }

    private async Task<int> ContarAsync(string tabla)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new($"SELECT count(*) FROM {Esquema}.{tabla}", conexion);

        return Convert.ToInt32(await orden.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task EjecutarAsync(string sql)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(sql, conexion);
        await orden.ExecuteNonQueryAsync();
    }

    /// <summary>La unidad de trabajo del módulo, que aquí es lo que es: confirmar el contexto.</summary>
    private sealed class UnidadDeTrabajoDelTest(OrganizacionDbContext contexto) : IUnidadTrabajoDeOrganizacion
    {
        public Task<int> ConfirmarAsync(CancellationToken cancelacion) =>
            contexto.SaveChangesAsync(cancelacion);
    }

    /// <summary>
    /// Un inquilino sin empresa que <b>apunta con qué motivo se le abrió el ámbito</b>.
    /// </summary>
    /// <remarks>
    /// No devuelve una empresa inventada: los maestros que carga la semilla no llevan
    /// <c>EmpresaId</c>, así que una empresa aquí sería un dato que no significa nada. Lo que sí
    /// hace, y es su único trabajo además de dejar pasar, es dejar constancia del motivo para que
    /// un test pueda mirarlo.
    /// </remarks>
    private sealed class InquilinoDeLaCarga : IInquilinoActual
    {
        private readonly List<MotivoSinInquilino> _motivos = [];

        public IReadOnlyList<MotivoSinInquilino> Motivos => _motivos;

        public bool HayEmpresaActiva => false;

        public Guid? EmpresaDelFiltro => null;

        public MotivoSinInquilino? MotivoDelAmbito => _motivos.Count > 0 ? _motivos[^1] : null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo)
        {
            _motivos.Add(motivo);

            return new Cierre();
        }

        private sealed class Cierre : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
