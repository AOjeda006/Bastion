using System.Globalization;
using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Ubicaciones;
using Bastion.Organizacion.Domain.Almacenes;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Domain.Ubicaciones;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// Los dos puertos del ítem 2.2 contra PostgreSQL de verdad: qué contesta un almacén bloqueado,
/// qué contesta una ubicación según el estado de los dos, y qué contesta lo que no está.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué esto no se puede probar con un doble.</b> Lo que decide cada respuesta es un filtro
/// global —el de R16— actuando sobre una consulta, y un doble contestaría lo que se le diga. La
/// pregunta de este ítem es literalmente si la fila llega o no llega hasta el <c>switch</c>, y eso
/// solo lo sabe la base.
/// </para>
/// <para>
/// <b>Y por qué el acceso a lo bloqueado es el DE VERDAD y no un doble abierto.</b> Un doble que
/// contestara <c>Abierto =&gt; true</c> daría verde a estos casos aunque el puerto no abriera
/// ningún ámbito: el filtro estaría levantado por el test, no por el adaptador, y la línea que se
/// quiere comprobar sería decorativa sin que nada lo dijera. Con
/// <see cref="AccesoALoBloqueado"/> el ámbito vale solo dentro del <c>using</c> del puerto, y por
/// eso el caso del almacén bloqueado lleva su contraejemplo: la MISMA consulta, en el MISMO
/// contexto y fuera del puerto, no trae la fila.
/// </para>
/// <para>
/// <b>Las cuatro combinaciones del ADR-0037 §4 van en cuatro casos y no en uno con cuatro
/// aserciones</b>, porque son cuatro reglas distintas y el que decide —ubicación activa dentro de
/// almacén bloqueado— tiene que poder ponerse rojo él solo. Es el que un <c>Math.Min</c> sobre el
/// enumerado habría roto: <c>SoloResuelveLoViejo</c> vale 2 y <c>SeOfreceParaLoNuevo</c> vale 1, y
/// el mínimo de los números es la mejor de las dos respuestas, no la peor.
/// </para>
/// <para>
/// <b>Semillas del bloque 600-607</b>, una empresa por caso: las de este proyecto no se cruzan con
/// las de <c>Api.IntegrationTests</c> —otra base, otro contenedor— pero sí entre ellas, y el NIF de
/// una empresa es único en toda la instalación.
/// </para>
/// </remarks>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class UnaEstanteriaBloqueadaSigueExistiendoTests(PostgresDeVerdad postgres)
    : IAsyncLifetime
{
    private const string Esquema = OrganizacionDbContext.Esquema;

    private const string LetrasDeControl = "TRWAGMYFPDXBNJZSQVHLCKE";

    private static readonly DateTimeOffset s_momento =
        new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    // Las dos tablas ANTES de cada caso, y en este orden: `ubicaciones` apunta a `almacenes` con
    // una clave ajena `Restrict`, así que al revés el DELETE se estrella. Las empresas NO se
    // borran —cada caso se crea la suya, y otras tablas del módulo les apuntan—.
    public async Task InitializeAsync() =>
        await EjecutarAsync($"DELETE FROM {Esquema}.ubicaciones; DELETE FROM {Esquema}.almacenes;");

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Un almacén activo se ofrece; uno inventado y uno de otra empresa no existen, y el ajeno sí
    /// existe en la suya.
    /// </summary>
    /// <remarks>
    /// La última mitad es la que impide el falso verde: sin ella, un puerto que contestara
    /// <c>NoExiste</c> a todo pasaría las dos preguntas de en medio. Y es la R8 dicha como un
    /// estado — lo que el ámbito del ADR-0037 abre es la puerta del art. 32, no la del inquilinato.
    /// </remarks>
    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeAlmacenes), EstadoDeMaestro.SeOfreceParaLoNuevo)]
    [CubreEstadoDeMaestro(typeof(IConsultaDeAlmacenes), EstadoDeMaestro.NoExiste)]
    public async Task El_almacen_activo_se_ofrece_y_el_inventado_y_el_ajeno_no_existen()
    {
        Guid propia = await EmpresaAsync(600);
        Guid ajena = await EmpresaAsync(601);

        Almacen aqui = await AlmacenAsync(propia, "CENTRAL", bloqueado: false);
        Almacen alli = await AlmacenAsync(ajena, "CENTRAL", bloqueado: false);

        (await EstadoDelAlmacenAsync(propia, aqui.Id))
            .ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo);

        (await EstadoDelAlmacenAsync(propia, Guid.CreateVersion7()))
            .ShouldBe(EstadoDeMaestro.NoExiste);

        (await EstadoDelAlmacenAsync(propia, alli.Id))
            .ShouldBe(
                EstadoDeMaestro.NoExiste,
                "un almacén de otra empresa de la instalación se ve desde esta. Un movimiento " +
                "podría apuntar a él y la R8 quedaría rota por dentro, sin clave ajena que chistara");

        (await EstadoDelAlmacenAsync(ajena, alli.Id))
            .ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo, "en su propia empresa sí se ofrece");
    }

    /// <summary>
    /// Un almacén bloqueado <b>solo resuelve lo viejo</b>, y sin el ámbito declarado la fila no
    /// llega.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La casilla del ítem, y la que el ADR-0037 existe para fijar.</b> Un tercero bloqueado
    /// contesta <c>NoExiste</c> —lo que el art. 32 reserva es la privacidad de una persona— y un
    /// almacén bloqueado no puede: el libro de movimientos le apunta para siempre, y la ficha se
    /// conserva por una razón contable.
    /// </para>
    /// <para>
    /// <b>El contraejemplo va aquí dentro y es media prueba.</b> La misma consulta, en el mismo
    /// contexto y fuera del puerto, devuelve nada: eso es lo que dice que la respuesta de arriba la
    /// hace posible el ámbito declarado del adaptador y no una casualidad del filtro. Y la fila se
    /// cuenta además con SQL en crudo, que no depende de ningún filtro, porque «sigue existiendo»
    /// es exactamente lo que hay que comprobar.
    /// </para>
    /// <para>
    /// <b>Y la contraria, sobre la MISMA fila</b>: antes de bloquearla se ofrecía. Sin ella, un
    /// puerto que contestara siempre <c>SoloResuelveLoViejo</c> pasaría este caso.
    /// </para>
    /// </remarks>
    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeAlmacenes), EstadoDeMaestro.SoloResuelveLoViejo)]
    public async Task El_almacen_bloqueado_solo_resuelve_lo_viejo_y_sin_el_ambito_no_se_ve()
    {
        Guid empresa = await EmpresaAsync(602);
        Almacen almacen = await AlmacenAsync(empresa, "NAVE-NORTE", bloqueado: false);

        (await EstadoDelAlmacenAsync(empresa, almacen.Id))
            .ShouldBe(
                EstadoDeMaestro.SeOfreceParaLoNuevo,
                "recién dado de alta, un almacén se ofrece: sin esta mitad, un puerto que " +
                "contestara siempre `SoloResuelveLoViejo` pasaría el resto del caso");

        await BloquearAlmacenAsync(empresa, almacen.Id);

        (await EstadoDelAlmacenAsync(empresa, almacen.Id))
            .ShouldBe(
                EstadoDeMaestro.SoloResuelveLoViejo,
                "bloqueado, el almacén NO se ofrece para un movimiento nuevo y NO desaparece: un " +
                "albarán de hace tres años tiene que poder seguir diciendo de qué nave salió");

        // El filtro sigue puesto para todo el que no abra el ámbito, que es el otro lado de la
        // misma regla: si esta consulta trajera la fila, el `ViendoLoBloqueado` del adaptador no
        // estaría comprando nada y este fichero entero estaría midiendo un filtro apagado.
        AccesoALoBloqueado acceso = Acceso();
        await using OrganizacionDbContext contexto = AbrirContexto(empresa, acceso);

        acceso.Abierto.ShouldBeFalse("fuera del puerto no hay ningún ámbito abierto");

        (await contexto.Almacenes.CountAsync(fila => fila.Id == almacen.Id))
            .ShouldBe(0, "el filtro de R16 tiene que seguir escondiendo la fila bloqueada");

        (await ContarEnCrudoAsync("almacenes", almacen.Id))
            .ShouldBe(1, "y la fila sigue en la tabla: bloquear no es borrar");
    }

    /// <summary>Ubicación activa dentro de almacén activo: se ofrece para lo nuevo.</summary>
    /// <remarks>
    /// La primera de las cuatro combinaciones del ADR-0037 §4, y la única que autoriza. Va suelta
    /// para que las tres siguientes tengan contra qué compararse: sin ella, un puerto que
    /// contestara <c>SoloResuelveLoViejo</c> a toda pareja pasaría las otras tres de una vez.
    /// </remarks>
    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeUbicaciones), EstadoDeMaestro.SeOfreceParaLoNuevo)]
    public async Task Ubicacion_activa_en_almacen_activo_se_ofrece_para_lo_nuevo()
    {
        Guid empresa = await EmpresaAsync(603);
        Almacen almacen = await AlmacenAsync(empresa, "NAVE-A", bloqueado: false);
        Ubicacion ubicacion = await UbicacionAsync(empresa, almacen.Id, "A-01-1", bloqueada: false);

        (await EstadoDeLaUbicacionAsync(empresa, almacen.Id, ubicacion.Id))
            .ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo);
    }

    /// <summary>Ubicación bloqueada dentro de almacén activo: solo resuelve lo viejo.</summary>
    /// <remarks>
    /// La segunda combinación, y la <b>otra</b> que caza el <c>Math.Min</c>: el almacén vale 1 y
    /// la ubicación vale 2, así que el mínimo devuelve la mejor de las dos y deja meter mercancía
    /// en una estantería bloqueada. El estado propio de la ubicación empeora el de su almacén, y
    /// la contraria —la misma fila antes de bloquearla— es la que dice que lo que cambió la
    /// respuesta fue el bloqueo de la ubicación y no otra cosa.
    /// </remarks>
    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeUbicaciones), EstadoDeMaestro.SoloResuelveLoViejo)]
    public async Task Ubicacion_bloqueada_en_almacen_activo_solo_resuelve_lo_viejo()
    {
        Guid empresa = await EmpresaAsync(604);
        Almacen almacen = await AlmacenAsync(empresa, "NAVE-B", bloqueado: false);
        Ubicacion ubicacion = await UbicacionAsync(empresa, almacen.Id, "B-01-1", bloqueada: false);

        (await EstadoDeLaUbicacionAsync(empresa, almacen.Id, ubicacion.Id))
            .ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo);

        await BloquearUbicacionAsync(empresa, ubicacion.Id);

        (await EstadoDeLaUbicacionAsync(empresa, almacen.Id, ubicacion.Id))
            .ShouldBe(
                EstadoDeMaestro.SoloResuelveLoViejo,
                "la estantería bloqueada no admite mercancía nueva y sigue resolviendo la de un " +
                "albarán viejo, igual que su almacén");
    }

    /// <summary>
    /// Ubicación <b>activa</b> dentro de almacén <b>bloqueado</b>: solo resuelve lo viejo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el caso que decide todo el ítem.</b> La fila de la ubicación está activa —bloquear un
    /// almacén no toca sus ubicaciones, a propósito: desbloquear no sabría qué deshacer— así que
    /// aquí hay dos estados de verdad en desacuerdo y alguien tiene que resolverlos. La respuesta
    /// es la peor de las dos: el género tendría que entrar físicamente por la puerta de una nave
    /// cerrada.
    /// </para>
    /// <para>
    /// <b>Y es el que un <c>Math.Min</c> habría roto.</b> El almacén contesta
    /// <c>SoloResuelveLoViejo</c>, que vale 2; la ubicación contesta <c>SeOfreceParaLoNuevo</c>,
    /// que vale 1; el mínimo es 1, o sea justo la respuesta que deja escribir el movimiento.
    /// </para>
    /// <para>
    /// <b>Y eso está medido, no supuesto.</b> Puesto el mínimo en el adaptador, las cuatro
    /// combinaciones se parten en dos mitades exactas: las dos que MEZCLAN estados —esta y la de
    /// la ubicación bloqueada dentro de almacén activo— se ponen rojas, y las dos simétricas
    /// siguen verdes, porque el mínimo de dos valores iguales acierta por casualidad. Es la razón
    /// de que las cuatro estén escritas y no solo las que parecían interesantes: con las dos
    /// simétricas por todo banco de pruebas, el descuido pasaba entero.
    /// </para>
    /// <para>
    /// <b>La contraria es la ubicación gemela sin bloquear el almacén</b>, creada en el mismo caso:
    /// dice que lo que empeora la respuesta es el estado del almacén y no el orden de las altas.
    /// </para>
    /// </remarks>
    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeUbicaciones), EstadoDeMaestro.SoloResuelveLoViejo)]
    public async Task Ubicacion_activa_en_almacen_bloqueado_solo_resuelve_lo_viejo()
    {
        Guid empresa = await EmpresaAsync(605);
        Almacen cerrado = await AlmacenAsync(empresa, "NAVE-CERRADA", bloqueado: false);
        Almacen abierto = await AlmacenAsync(empresa, "NAVE-ABIERTA", bloqueado: false);

        Ubicacion dentroDelCerrado =
            await UbicacionAsync(empresa, cerrado.Id, "C-01-1", bloqueada: false);
        Ubicacion dentroDelAbierto =
            await UbicacionAsync(empresa, abierto.Id, "C-01-1", bloqueada: false);

        await BloquearAlmacenAsync(empresa, cerrado.Id);

        (await EstadoDeLaUbicacionAsync(empresa, cerrado.Id, dentroDelCerrado.Id))
            .ShouldBe(
                EstadoDeMaestro.SoloResuelveLoViejo,
                "la ubicación está activa y su almacén está bloqueado, y ha contestado que se " +
                "ofrece para lo nuevo: se puede escribir un movimiento contra un hueco de una " +
                "nave cerrada. Es exactamente lo que devuelve `Math.Min` sobre este enumerado, " +
                "cuyo orden numérico NO es el de severidad");

        (await EstadoDeLaUbicacionAsync(empresa, abierto.Id, dentroDelAbierto.Id))
            .ShouldBe(
                EstadoDeMaestro.SeOfreceParaLoNuevo,
                "la gemela, en un almacén que no se ha tocado, sí se ofrece: lo que empeoró la " +
                "respuesta de la otra fue el estado de SU almacén");
    }

    /// <summary>Ubicación bloqueada dentro de almacén bloqueado: solo resuelve lo viejo.</summary>
    /// <remarks>
    /// La cuarta combinación. Dos bloqueos no son un bloqueo más grave: la respuesta es la misma
    /// que con uno, porque lo que se contesta es qué se puede hacer y no cuánto se ha bloqueado.
    /// Es una de las dos que el <c>Math.Min</c> NO caza —el mínimo de dos valores iguales acierta
    /// por casualidad—, y se escribe igualmente: lo que descarta es la composición que sumara, o
    /// cualquiera que tratara los dos bloqueos como un tercer estado.
    /// </remarks>
    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeUbicaciones), EstadoDeMaestro.SoloResuelveLoViejo)]
    public async Task Ubicacion_bloqueada_en_almacen_bloqueado_solo_resuelve_lo_viejo()
    {
        Guid empresa = await EmpresaAsync(606);
        Almacen almacen = await AlmacenAsync(empresa, "NAVE-D", bloqueado: false);
        Ubicacion ubicacion = await UbicacionAsync(empresa, almacen.Id, "D-01-1", bloqueada: false);

        await BloquearUbicacionAsync(empresa, ubicacion.Id);
        await BloquearAlmacenAsync(empresa, almacen.Id);

        (await EstadoDeLaUbicacionAsync(empresa, almacen.Id, ubicacion.Id))
            .ShouldBe(EstadoDeMaestro.SoloResuelveLoViejo);

        // Las dos filas siguen en la tabla, que es la mitad que ninguna respuesta del puerto dice.
        (await ContarEnCrudoAsync("almacenes", almacen.Id)).ShouldBe(1);
        (await ContarEnCrudoAsync("ubicaciones", ubicacion.Id)).ShouldBe(1);
    }

    /// <summary>
    /// Una ubicación inventada no existe, y una que existe pero cuelga de OTRO almacén tampoco.
    /// </summary>
    /// <remarks>
    /// <b>Las dos situaciones de <c>NoExiste</c> que el puerto declara</b>, y la segunda es la que
    /// justifica que reciba el almacén: la pregunta es «qué hay en este almacén con este
    /// identificador», y ahí no hay nada. Quien pregunta no puede comprobarlo por su cuenta —los
    /// dos datos viven en un esquema que no es el suyo—, así que o lo contesta este puerto o el
    /// libro admite movimientos con la mercancía en una nave y el hueco en otra. La contraria va
    /// detrás: en su propio almacén, esa misma ubicación sí se ofrece.
    /// </remarks>
    [Fact]
    [CubreEstadoDeMaestro(typeof(IConsultaDeUbicaciones), EstadoDeMaestro.NoExiste)]
    public async Task La_ubicacion_de_otro_almacen_no_existe_aqui_y_si_en_el_suyo()
    {
        Guid empresa = await EmpresaAsync(607);
        Almacen uno = await AlmacenAsync(empresa, "NAVE-E", bloqueado: false);
        Almacen otro = await AlmacenAsync(empresa, "NAVE-F", bloqueado: false);

        Ubicacion enElOtro = await UbicacionAsync(empresa, otro.Id, "E-01-1", bloqueada: false);

        (await EstadoDeLaUbicacionAsync(empresa, uno.Id, Guid.CreateVersion7()))
            .ShouldBe(EstadoDeMaestro.NoExiste);

        (await EstadoDeLaUbicacionAsync(empresa, uno.Id, enElOtro.Id))
            .ShouldBe(
                EstadoDeMaestro.NoExiste,
                "la ubicación existe, pero no en el almacén por el que se ha preguntado. Darla " +
                "por buena es admitir un movimiento que deja la mercancía en una nave y el hueco " +
                "en otra");

        (await EstadoDeLaUbicacionAsync(empresa, otro.Id, enElOtro.Id))
            .ShouldBe(EstadoDeMaestro.SeOfreceParaLoNuevo, "en su propio almacén sí se ofrece");
    }

    private async Task<EstadoDeMaestro> EstadoDelAlmacenAsync(Guid empresaId, Guid almacenId)
    {
        AccesoALoBloqueado acceso = Acceso();

        await using OrganizacionDbContext contexto = AbrirContexto(empresaId, acceso);

        // El tipo concreto y no la interfaz, como en `LosPuertosDeLecturaTests`: CA1859 está como
        // error en este repositorio, y que la implementación case con su contrato lo comprueba el
        // compilador en `ModuloDeOrganizacion`, que la registra bajo él.
        return await new ConsultaDeAlmacenes(contexto, acceso)
            .EstadoDeAsync(almacenId, CancellationToken.None);
    }

    private async Task<EstadoDeMaestro> EstadoDeLaUbicacionAsync(
        Guid empresaId, Guid almacenId, Guid ubicacionId)
    {
        AccesoALoBloqueado acceso = Acceso();

        await using OrganizacionDbContext contexto = AbrirContexto(empresaId, acceso);

        return await new ConsultaDeUbicaciones(contexto, acceso)
            .EstadoDeAsync(almacenId, ubicacionId, CancellationToken.None);
    }

    private async Task<Guid> EmpresaAsync(int semilla)
    {
        var empresa = Empresa.Crear(
            Nif.De(NifInventado(semilla)),
            string.Create(CultureInfo.InvariantCulture, $"Empresa de prueba {semilla}"),
            Domicilio(),
            "EUR",
            RegimenDeIva.General,
            s_momento);

        await GuardarAsync(empresa.Id, contexto => contexto.Empresas.Add(empresa));

        return empresa.Id;
    }

    private async Task<Almacen> AlmacenAsync(Guid empresaId, string codigo, bool bloqueado)
    {
        var almacen = Almacen.Crear(
            empresaId, codigo, "Almacén de prueba", Domicilio(), TipoDeAlmacen.Fisico, s_momento);

        await GuardarAsync(empresaId, contexto => contexto.Almacenes.Add(almacen));

        if (bloqueado)
        {
            await BloquearAlmacenAsync(empresaId, almacen.Id);
        }

        return almacen;
    }

    private async Task<Ubicacion> UbicacionAsync(
        Guid empresaId, Guid almacenId, string codigo, bool bloqueada)
    {
        var ubicacion = Ubicacion.Crear(
            empresaId, almacenId, codigo, "A", "01", "1", "Hueco de prueba", s_momento);

        await GuardarAsync(empresaId, contexto => contexto.Ubicaciones.Add(ubicacion));

        if (bloqueada)
        {
            await BloquearUbicacionAsync(empresaId, ubicacion.Id);
        }

        return ubicacion;
    }

    // Por el dominio y no con un UPDATE: la columna la escribe el objeto de valor `Bloqueo`, y una
    // fila montada a mano probaría un estado que el sistema no produce. El motivo es el del cese
    // de uso y no el del artículo 32: un almacén no guarda ni un dato de una persona.
    private Task BloquearAlmacenAsync(Guid empresaId, Guid almacenId) =>
        GuardarAsync(empresaId, contexto => contexto.Almacenes
            .Single(fila => fila.Id == almacenId)
            .Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento));

    private Task BloquearUbicacionAsync(Guid empresaId, Guid ubicacionId) =>
        GuardarAsync(empresaId, contexto => contexto.Ubicaciones
            .Single(fila => fila.Id == ubicacionId)
            .Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento));

    private async Task GuardarAsync(Guid empresaId, Action<OrganizacionDbContext> cambio)
    {
        await using OrganizacionDbContext contexto = AbrirContexto(empresaId, Acceso());

        cambio(contexto);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    // El contexto se abre a mano y no con `postgres.AbrirContexto()`: el doble de aquel lanza en
    // cuanto alguien le pregunta por la empresa o abre un ámbito, y estos puertos hacen las dos
    // cosas. Es el mismo motivo por el que `LaCargaDeSemillasTests` abre el suyo.
    private OrganizacionDbContext AbrirContexto(Guid empresaId, IAccesoALoBloqueado acceso)
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, postgres.CadenaDeConexion);

        return new OrganizacionDbContext(
            opciones.Options, new InquilinoDeLaEmpresa(empresaId), acceso);
    }

    private static AccesoALoBloqueado Acceso() =>
        new(NullLogger<AccesoALoBloqueado>.Instance, new NadieEnConcreto());

    private static Direccion Domicilio() =>
        Direccion.De("Gran Vía", "31", "28013", "Madrid", "Madrid", "ES");

    private static string NifInventado(int numero) =>
        numero.ToString("D8", CultureInfo.InvariantCulture) + LetrasDeControl[numero % 23];

    /// <summary>Cuenta la fila sin pasar por EF: es la evidencia que no depende de ningún filtro.</summary>
    private async Task<int> ContarEnCrudoAsync(string tabla, Guid id)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden =
            new($"SELECT count(*) FROM {Esquema}.{tabla} WHERE id = @id", conexion);

        orden.Parameters.AddWithValue("id", id);

        return Convert.ToInt32(await orden.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task EjecutarAsync(string sql)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(sql, conexion);
        await orden.ExecuteNonQueryAsync();
    }

    /// <summary>Un inquilino fijo, que es lo que hay dentro de una petición ordinaria.</summary>
    /// <remarks>
    /// No abre ámbitos sin inquilino: si un caso necesitara ver dos empresas, abre un contexto por
    /// empresa y lo dice. Es lo que hacen los casos de aquí arriba.
    /// </remarks>
    /// <param name="empresaId">La empresa activa.</param>
    private sealed class InquilinoDeLaEmpresa(Guid empresaId) : IInquilinoActual
    {
        public bool HayEmpresaActiva => true;

        public Guid? EmpresaDelFiltro => empresaId;

        public MotivoSinInquilino? MotivoDelAmbito => null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
            "Estos casos no suspenden el inquilinato: lo que comprueban es que está puesto.");
    }

    /// <summary>
    /// Nadie autenticado, que es lo que el acceso de verdad espera fuera de una petición.
    /// </summary>
    /// <remarks>
    /// <see cref="AccesoALoBloqueado"/> solo le pregunta si está autenticado, para anotar en el
    /// registro quién miró. Aquí no hay nadie, y el registro es el nulo: lo que este fichero
    /// comprueba es la respuesta del puerto, no la traza —esa tiene su propio carril—.
    /// </remarks>
    private sealed class NadieEnConcreto : IUsuarioActual
    {
        public bool EstaAutenticado => false;

        public Guid UsuarioId => throw new NotSupportedException("No hay nadie autenticado.");

        public Guid EmpresaId => throw new NotSupportedException("No hay nadie autenticado.");

        public bool Tiene(Permiso permiso) => false;
    }
}
