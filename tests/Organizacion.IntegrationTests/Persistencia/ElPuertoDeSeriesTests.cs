using System.Globalization;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Series;
using Bastion.Organizacion.Domain.Ejercicios;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Series;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// El puerto del ítem 2.4 contra PostgreSQL de verdad: qué contesta de una serie activa, de una
/// cerrada, de una que no está y de una que es de otra empresa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Este puerto no es el que sostiene la R5, y eso cambia lo que hay que comprobar aquí.</b> Lo
/// que garantiza que un documento confirmado lleve un correlativo sin huecos es el <c>WHERE</c> de
/// la sentencia que toma el número, dentro de la transacción del documento y con la fila del
/// contador bloqueada; eso se comprueba en <c>ElCerrojoDeLaNumeracionTests</c>, en el carril de la
/// API. Lo que se comprueba aquí es lo otro: que el alta de un documento pueda rechazar una serie
/// que no sirve <b>antes</b> de que alguien rellene sus líneas.
/// </para>
/// <para>
/// <b>Y la serie de otra empresa tiene caso propio aunque ya haya uno de «no existe».</b> Las dos
/// contestan lo mismo —<c>NoExiste</c>— y esa coincidencia <i>es</i> la afirmación: distinguirlas
/// convertiría el puerto en un detector de series ajenas para quien probara identificadores al
/// azar. Con solo el caso de la inventada, un puerto que se saltara el filtro de la R8 saldría
/// verde y contestaría <c>SeOfreceParaLoNuevo</c> sobre la serie de otra sociedad.
/// </para>
/// <para>
/// <b>Las semillas de NIF van por el 620</b>, que es la banda libre de este carril: la del 600 al
/// 607 la gasta <c>UnaEstanteriaBloqueadaSigueExistiendoTests</c>, y <c>empresas.nif</c> es único
/// en un contenedor que toda la colección comparte.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones puestas.</param>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class ElPuertoDeSeriesTests(PostgresDeVerdad postgres)
{
    private const string LetrasDeControl = "TRWAGMYFPDXBNJZSQVHLCKE";

    private static readonly DateTimeOffset s_momento = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeSeries), EstadoDeMaestro.SeOfreceParaLoNuevo)]
    [CubreEstadoDeMaestro(typeof(IConsultaDeSeries), EstadoDeMaestro.NoExiste)]
    public async Task La_serie_activa_se_ofrece_y_la_que_no_esta_no_existe()
    {
        Guid empresa = await EmpresaAsync(620);
        Serie serie = await SerieAsync(empresa, "PSE-ACT", cerrada: false);

        (await EstadoAsync(empresa, serie.Id)).ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo);

        (await EstadoAsync(empresa, Guid.CreateVersion7())).ShouldBe(EstadoDeMaestro.NoExiste);
    }

    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeSeries), EstadoDeMaestro.SoloResuelveLoViejo)]
    public async Task Una_serie_cerrada_sigue_resolviendo_lo_viejo_y_no_se_ofrece()
    {
        // La misma frase del ADR-0023 aplicada a una serie, y encaja sin forzarla: los documentos
        // que esta serie numeró siguen resolviéndola —es lo que demuestra que su numeración fue
        // correlativa— y ninguno nuevo se apunta a ella.
        Guid empresa = await EmpresaAsync(621);
        Serie cerrada = await SerieAsync(empresa, "PSE-CERR", cerrada: true);

        (await EstadoAsync(empresa, cerrada.Id)).ShouldBe(
            EstadoDeMaestro.SoloResuelveLoViejo,
            "cerrada NO es lo mismo que borrada: si contestara `NoExiste`, el documento que la " +
            "lleva escrita no podría ni enseñar de qué serie salió");
    }

    [Fact]
    public async Task Una_serie_de_otra_empresa_contesta_lo_mismo_que_una_que_no_existe()
    {
        Guid propia = await EmpresaAsync(622);
        Guid ajena = await EmpresaAsync(623);

        Serie deLaAjena = await SerieAsync(ajena, "PSE-AJENA", cerrada: false);

        (await EstadoAsync(ajena, deLaAjena.Id)).ShouldBe(
            EstadoDeMaestro.SeOfreceParaLoNuevo,
            "desde su propia empresa la serie se ofrece; si no, el «no existe» de abajo no diría " +
            "nada del filtro y saldría verde con una serie que nunca llegó a guardarse");

        (await EstadoAsync(propia, deLaAjena.Id)).ShouldBe(
            EstadoDeMaestro.NoExiste,
            "y desde otra empresa contesta EXACTAMENTE lo mismo que una inventada: una respuesta " +
            "distinta diría cuáles de los identificadores que alguien pruebe existen en otras " +
            "sociedades de la misma instalación (R8)");
    }

    private async Task<EstadoDeMaestro> EstadoAsync(Guid empresaId, Guid serieId)
    {
        await using OrganizacionDbContext contexto = AbrirContexto(empresaId);

        // El tipo concreto y no la interfaz, igual que en `LosPuertosDeLecturaTests`: CA1859 está
        // como error en este repositorio, y que la implementación case con su contrato lo
        // comprueba el compilador en `ModuloDeOrganizacion`, que la registra bajo él.
        ConsultaDeSeries puerto = new(contexto);

        return await puerto.EstadoDeAsync(serieId, CancellationToken.None);
    }

    private async Task<Guid> EmpresaAsync(int semilla)
    {
        var empresa = Empresa.Crear(
            Nif.De(NifInventado(semilla)),
            string.Create(CultureInfo.InvariantCulture, $"Empresa de prueba {semilla}"),
            Direccion.De("Gran Vía", "31", "28013", "Madrid", "Madrid", "ES"),
            "EUR",
            RegimenDeIva.General,
            s_momento);

        await GuardarAsync(empresa.Id, contexto => contexto.Empresas.Add(empresa));

        return empresa.Id;
    }

    /// <summary>Una serie con su ejercicio, porque una serie numera por serie y ejercicio.</summary>
    /// <param name="empresaId">Empresa dueña de las dos filas.</param>
    /// <param name="codigo">Código de la serie, único dentro de empresa y ejercicio.</param>
    /// <param name="cerrada">Si se cierra nada más crearla.</param>
    /// <returns>La serie guardada.</returns>
    private async Task<Serie> SerieAsync(Guid empresaId, string codigo, bool cerrada)
    {
        var ejercicio = Ejercicio.Crear(
            empresaId,
            s_momento.Year,
            new DateOnly(s_momento.Year, 1, 1),
            new DateOnly(s_momento.Year, 12, 31),
            s_momento);

        await GuardarAsync(empresaId, contexto => contexto.Ejercicios.Add(ejercicio));

        var serie = Serie.Crear(
            empresaId,
            ejercicio.Id,
            TipoDeDocumento.AjusteDeInventario,
            codigo,
            "{serie}-{numero:0000}",
            s_momento);

        if (cerrada)
        {
            serie.Cerrar();
        }

        await GuardarAsync(empresaId, contexto => contexto.Series.Add(serie));

        return serie;
    }

    private async Task GuardarAsync(Guid empresaId, Action<OrganizacionDbContext> cambio)
    {
        await using OrganizacionDbContext contexto = AbrirContexto(empresaId);

        cambio(contexto);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    // El contexto se abre a mano y no con `postgres.AbrirContexto()`: el doble de aquel lanza en
    // cuanto alguien le pregunta por la empresa, y el filtro de la R8 se la pregunta en cada
    // consulta de este fichero — que es justamente lo que el último caso comprueba.
    private OrganizacionDbContext AbrirContexto(Guid empresaId)
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, postgres.CadenaDeConexion);

        return new OrganizacionDbContext(
            opciones.Options, new InquilinoDeEstaEmpresa(empresaId), new AccesoQueNadieDebeAbrir());
    }

    private static string NifInventado(int numero) =>
        numero.ToString("D8", CultureInfo.InvariantCulture) + LetrasDeControl[numero % 23];

    /// <summary>Un inquilino fijo, que es lo que hay dentro de una petición ordinaria.</summary>
    /// <remarks>
    /// No suspende el inquilinato: lo que estos casos comprueban es que el filtro está puesto, y
    /// un doble que supiera abrir el ámbito sin empresa podría taparlo.
    /// </remarks>
    /// <param name="empresaId">La empresa activa.</param>
    private sealed class InquilinoDeEstaEmpresa(Guid empresaId) : IInquilinoActual
    {
        public bool HayEmpresaActiva => true;

        public Guid? EmpresaDelFiltro => empresaId;

        public MotivoSinInquilino? MotivoDelAmbito => null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
            "Estos casos no suspenden el inquilinato: lo que comprueban es que está puesto.");
    }
}
