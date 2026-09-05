using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Multiempresa;

/// <summary>
/// Los caminos que se saltan el filtro global sin que EF Core diga nada, y la prohibición —de
/// verdad comprobada— de cada uno.
/// </summary>
/// <remarks>
/// <para>
/// Un filtro de consulta protege lo que pasa por el <i>traductor de consultas</i> y nada más. Hay
/// una lista corta y conocida de puertas que lo rodean: pedirle a EF Core que lo ignore, escribir
/// SQL a mano, buscar por clave con <c>Find</c> —que puede contestar desde el rastreador de
/// cambios sin llegar a consultar— y las escrituras masivas <c>ExecuteUpdate</c> y
/// <c>ExecuteDelete</c>, que saltan el rastreador y la unidad de trabajo. Ninguna de ellas
/// <b>falla</b> cuando se cuela: devuelve más filas, o toca las de otro.
/// </para>
/// <para>
/// De ninguna hay test de comportamiento posible mientras no se usen —no se puede ejercitar un
/// camino que no existe—, así que lo que se comprueba es que <b>siguen sin usarse</b>. Es una
/// prohibición, no una preferencia: el día que alguien necesite una de verdad, este fichero le
/// obliga a decirlo aquí y a explicar por qué.
/// </para>
/// <para>
/// <b>Se leen los ficheros de <c>src/</c> con los comentarios quitados</b>: lo que se prohíbe es
/// llamar, no nombrar. Si no fuera así, documentar la regla en el sitio donde importa rompería el
/// test que la defiende.
/// </para>
/// </remarks>
public sealed class ElFiltroNoSeSaltaPorAhiTests
{
    // Las llamadas que rodean el filtro, y por qué cada una lo rodea.
    private static readonly Dictionary<string, string> s_prohibidas = new(StringComparer.Ordinal)
    {
        [".IgnoreQueryFilters("] =
            "apaga TODOS los filtros de esa consulta, y desde el 0.10 son dos: el de empresa y el " +
            "de bloqueo (R16). Quien lo escribiera para ver una fila bloqueada estaría abriendo de " +
            "paso el de empresa sin enterarse. No hace falta: lo que se necesitaba de verdad " +
            "-semilla, acceso, unicidad global, pertenencias- pasa por SinInquilino, y levantar un " +
            "bloqueo pasa por ViendoLoBloqueado. Los dos dejan rastro en el registro y tienen los " +
            "motivos en una lista cerrada. Con dos alternativas auditadas al lado, la prohibición " +
            "puede ser absoluta",

        [".FromSql"] =
            "el SQL escrito a mano no pasa por el traductor, así que el filtro no se le aplica",

        [".ExecuteSql"] =
            "lo mismo, y además escribe",

        [".SqlQuery"] =
            "lo mismo: consulta cruda sobre el contexto",

        [".ExecuteUpdate"] =
            "se traduce a un UPDATE directo. Este SÍ respeta el filtro de la consulta, pero salta " +
            "el rastreador y la unidad de trabajo, así que ni la auditoría (0.7) ni la " +
            "concurrencia (0.9) lo verían pasar",

        [".ExecuteDelete"] =
            "lo mismo, borrando",

        [".Find("] =
            "busca por clave y puede contestar desde el rastreador de cambios SIN consultar. " +
            "Cuando contesta desde ahí no hay consulta que filtrar: devuelve la fila de otra " +
            "empresa si alguien la cargó antes en el mismo contexto",

        [".FindAsync("] =
            "igual que Find",

        ["Set<RolDeMembresia>"] =
            "RolDeMembresia no filtra -depende de la pertenencia, que sí- y solo es seguro " +
            "mientras no se consulte por su cuenta",

        ["Set<PermisoDeRol>"] =
            "PermisoDeRol es parte del rol; consultarlo suelto lo saca de su dueño",
    };

    // Los sitios donde una de esas llamadas SÍ está, con su motivo. La lista nació con una sola
    // línea, y la puso este test: el barrido la encontró y hubo que ir a mirarla.
    private static readonly Dictionary<string, string> s_saltosPermitidos = new(StringComparer.Ordinal)
    {
        ["src/Modules/Identidad/Bastion.Identidad.Infrastructure/Persistencia/Repositorios/RepositorioDeRoles.cs" +
         " usa Set<PermisoDeRol>"] =
            "arma los permisos que van al token a partir de los roles de la pertenencia activa. " +
            "Los identificadores de rol NO vienen de la petición: los pone ConstructorDeSesion a " +
            "partir de la membresía, que sí filtra. Y el rol es global por decisión (ADR-0011), " +
            "así que sus permisos no son de ninguna empresa en particular",

        ["src/BuildingBlocks/Infrastructure/BandejaDeSalida/CerrojoDeLaBandeja.cs usa .SqlQuery"] =
            "toma y suelta el cerrojo con el que un solo publicador vacía la cola, y `pg_try_advisory_lock` " +
            "no tiene traducción en EF Core: no hay forma de pedirlo sin SQL crudo. La excepción es " +
            "estrecha por lo que la hace inútil para cualquier otro caso: esas dos sentencias NO LEEN " +
            "NINGUNA TABLA -devuelven un booleano del gestor de bloqueos-, así que no hay ninguna fila " +
            "que un filtro de empresa hubiera protegido. Quien quiera SQL crudo sobre filas no puede " +
            "acogerse a esto, porque el argumento entero es que aquí no hay filas. Está en un fichero " +
            "propio y en dos métodos, para que la excepción se lea de una vez",

        ["src/BuildingBlocks/Infrastructure/Idempotencia/AlmacenDeIdempotencia.cs usa .ExecuteSql"] =
            "reclama una Idempotency-Key con un INSERT ... ON CONFLICT DO NOTHING, que EF Core no sabe "
            + "traducir. Las dos alternativas sin SQL crudo son peores: mirar-y-luego-insertar deja la "
            + "ventana por la que dos peticiones con la misma clave hacen las dos el trabajo -o sea, "
            + "reintroduce el fallo que el mecanismo viene a impedir-, y atrapar la violacion del indice "
            + "usa una excepcion como flujo de control DENTRO de una transaccion, que en PostgreSQL queda "
            + "abortada y no puede seguir. La excepcion es estrecha por lo que la hace inutil para otro "
            + "caso: esa sentencia NO LEE NINGUNA TABLA -es una escritura de una fila cuya clave primaria "
            + "completa se le entrega, con empresa_id dentro, tomado del claim y nunca de la peticion-, "
            + "asi que no hay ninguna fila que un filtro de empresa hubiera protegido y ella alcance. "
            + "TODO lo que se LEE de esa tabla pasa por EF Core con su filtro puesto, y que la sentencia "
            + "siga nombrando empresa_id en las columnas y en el objetivo del conflicto lo comprueba "
            + "LaClaveDeIdempotenciaEsLaTuplaEnteraTests",
    };

    // Dónde se abre un ámbito sin inquilino, cuántas veces, y por qué ahí. Es la lista blanca del
    // único mecanismo que apaga el filtro a propósito.
    private static readonly Dictionary<string, int> s_ambitosPermitidos = new(StringComparer.Ordinal)
    {
        ["src/Api/Arranque/SemillaDeArranque.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Sesiones/CambiarEmpresaActiva.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Sesiones/CerrarSesion.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Sesiones/IniciarSesion.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Sesiones/RenovarSesion.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Usuarios/CrearUsuario.cs"] = 1,
        ["src/Modules/Identidad/Bastion.Identidad.Application/Usuarios/Pertenencias.cs"] = 4,
        ["src/Modules/Organizacion/Bastion.Organizacion.Application/Empresas/CrearEmpresa.cs"] = 1,

        // El primero que no tiene petición detrás. El publicador corre solo, sin usuario y sin
        // empresa: la cola es de la instalación entera y sus filas son de empresas distintas. Sin
        // este ámbito no es que viera menos eventos — es que reventaría en cada vuelta, porque
        // fuera de un ámbito y sin claim la empresa del filtro no existe.
        ["src/BuildingBlocks/Infrastructure/BandejaDeSalida/PublicadorDeLaBandeja.cs"] = 1,

        // El segundo sin petición detrás, y del 0.15: el migrador carga los maestros de
        // `db/semillas/` —tipos de IVA y unidades— antes de que exista ninguna empresa. El ámbito
        // NO está para poder consultarlos: ni `Impuesto` ni `UnidadMedida` llevan filtro, porque
        // son maestros de la instalación (R8). Está para que la traza de cada alta se pueda
        // escribir: sin empresa y sin ámbito, el interceptor de auditoría lanza. La cuenta es 1
        // porque la apertura cubre los dos ficheros; si se partiera en dos, aquí se vería.
        ["src/Modules/Organizacion/Bastion.Organizacion.Infrastructure/Semillas/" +
         "CargadorDeSemillasDeOrganizacion.cs"] = 1,
    };

    // Lo mismo para el ámbito que ve lo bloqueado. Es una lista aparte y no una más en la de
    // arriba porque son dos mecanismos distintos que se apagan por separado: abrir uno no abre el
    // otro, y ese es medio diseño. Un camino que quiera ver lo bloqueado y no esté aquí, rojo.
    private static readonly Dictionary<string, int> s_aperturasDeBloqueoPermitidas =
        new(StringComparer.Ordinal)
        {
            // Los cuatro desbloqueos y UNA lectura, desde el ítem 1.4. Hasta entonces la razón
            // de estar aquí era siempre la misma y de lógica, no de permisos: para levantar un
            // bloqueo hay que poder leer lo que está bloqueado, y eso -por definición- lo tapa el
            // filtro. Ahora hay una segunda razón, y es la del art. 32 en sí: el acceso reservado
            // tiene que existir para poder ejercerse, porque un bloqueo que no se puede ni mirar
            // no se puede rectificar. Lo que NO ha cambiado es el punto de la lista: ninguna
            // consulta ORDINARIA está aquí, y la que se ha añadido tiene permiso propio.
            ["src/Modules/Organizacion/Bastion.Organizacion.Application/Almacenes/DesbloquearAlmacen.cs"] = 1,
            ["src/Modules/Organizacion/Bastion.Organizacion.Application/Empresas/DesbloquearEmpresa.cs"] = 1,
            ["src/Modules/Identidad/Bastion.Identidad.Application/Usuarios/AdministracionDeUsuarios.cs"] = 1,

            // El cuarto, del 0.15. El fichero lleva las dos mitades -bloquear y desbloquear- y la
            // apertura es UNA: si algún día fueran dos, este recuento se pondría rojo y habría que
            // mirar cuál de las dos ha empezado a ver lo que no debe.
            ["src/Modules/Organizacion/Bastion.Organizacion.Application/Ubicaciones/BloquearUbicacion.cs"] = 1,

            // La quinta, del ítem 1.4, y la primera que NO desbloquea: es el listado del acceso
            // reservado del art. 32 (ADR-0027). Se declara con lo que la distingue de las otras
            // cuatro, porque quien lea esta lista dentro de un año tiene que poder saber por qué
            // una LECTURA está aquí:
            //
            //   - abre el ámbito con su propio motivo (`AccesoReservadoDelArticulo32`) y no con
            //     el de la administración del bloqueo, para que la traza distinga a una persona
            //     mirando datos reservados de un desbloqueo operando sobre ellos;
            //   - exige un permiso propio (`organizacion.bloqueado.ver`), que no viene con el de
            //     ver empresas ni con el de desbloquear;
            //   - y NO emite versión, que es de lo que dependen las cuatro exenciones de
            //     `If-Match` de los desbloqueos. Eso lo afirma la regla de aquí abajo.
            //
            // La apertura es UNA y cubre el listado entero. Si algún día fueran dos, este recuento
            // se pone rojo y hay que mirar qué otro camino ha empezado a ver lo reservado.
            ["src/Modules/Organizacion/Bastion.Organizacion.Application/Bloqueos/ConsultasDeLoBloqueado.cs"] = 1,

            // El sexto desbloqueo, del ítem 1.5: el de un tercero. Misma razón mecánica que los
            // cinco anteriores y ninguna novedad.
            ["src/Modules/Terceros/Bastion.Terceros.Application/Terceros/DesbloquearTercero.cs"] = 1,

            // Y la SÉPTIMA, que es la primera que no desbloquea ni lee lo reservado: un ALTA. Es
            // la que más explicación necesita de toda la lista.
            //
            //   - Por qué mira: la unicidad de (empresa, identificador fiscal) abarca también las
            //     fichas bloqueadas —decisión del ítem 1.5, escrita en `docs/PLAN.md`, y no algo
            //     que decidiera el índice por omisión—. Si el alta no viera lo bloqueado, diría
            //     que el identificador está libre cuando no lo está, y la fila chocaría después
            //     contra la restricción y saldría como un 500 en vez de como el conflicto que es.
            //   - Qué se trae de dentro: un BOOLEANO, y solo uno. El puerto
            //     (`IRepositorioDeTerceros.ExisteLaIdentificacionAsync`) no entrega si el que
            //     estorba estaba activo o bloqueado, así que la respuesta no puede distinguirlos.
            //     Si las dos respuestas se distinguieran, el formulario de alta sería el censo de
            //     las bajas del art. 32, recorrible identificador a identificador.
            //   - Dónde queda escrito cuál era: en el registro, desde la implementación del
            //     repositorio, que es quien lo sabe. En la respuesta, en ninguna parte.
            //   - Cuánto dura: el ámbito envuelve SOLO la pregunta. Ni la creación ni el
            //     `ConfirmarAsync` corren dentro, que es lo que pasaría con un `using` de
            //     declaración en vez del bloque.
            //
            // La apertura es UNA. Si algún día fueran dos, este recuento se pone rojo y hay que
            // mirar qué otra cosa ha empezado a mirar lo bloqueado durante un alta.
            ["src/Modules/Terceros/Bastion.Terceros.Application/Terceros/CrearTercero.cs"] = 1,
        };

    // Los únicos sitios donde se define un filtro global: el `OnModelCreating` de cada contexto de
    // módulo, uno por módulo con persistencia. Repartirlos por otros ficheros no rompería nada;
    // solo haría imposible contestar «qué filtra y qué no» leyendo un sitio.
    private static readonly string[] s_dondeSeDefinenLosFiltros =
    [
        "src/Modules/Auditoria/Bastion.Auditoria.Infrastructure/Persistencia/AuditoriaDbContext.cs",
        "src/Modules/Identidad/Bastion.Identidad.Infrastructure/Persistencia/IdentidadDbContext.cs",
        "src/Modules/Organizacion/Bastion.Organizacion.Infrastructure/Persistencia/OrganizacionDbContext.cs",
        "src/Modules/Terceros/Bastion.Terceros.Infrastructure/Persistencia/TercerosDbContext.cs",

        // No es un módulo, y por eso está aquí abajo y con su línea: es el contexto con el que el
        // trabajo de fondo lee la bandeja. Define el filtro por lo mismo que los otros tres -la
        // cola es un dato de la empresa que lo emitió- y no puede definirlo el mapeo compartido,
        // porque la expresión del filtro lee una propiedad DE LA INSTANCIA del contexto.
        "src/BuildingBlocks/Infrastructure/BandejaDeSalida/ContextoDeLaBandeja.cs",
    ];

    private const string Bloque = @"/\*.*?\*/";

    private const string Linea = @"//.*?$";

    [Fact]
    public void Ninguna_llamada_de_las_que_rodean_el_filtro_aparece_en_el_codigo()
    {
        List<string> hallazgos = [];

        // Solo los ficheros que ven EF Core. En el resto, un `.Find(` es el de `List<T>` —el
        // dominio lo usa, y el dominio no puede tocar EF Core—, así que contarlo sería ruido con
        // forma de aviso de seguridad, que es la clase de aviso que se acaba ignorando.
        foreach ((string ruta, string codigo) in CodigoDeProduccion().Where(fichero => VeEfCore(fichero.Codigo)))
        {
            foreach (string prohibida in s_prohibidas.Keys)
            {
                if (Veces(codigo, prohibida) == 0 || s_saltosPermitidos.ContainsKey($"{ruta} usa {prohibida}"))
                {
                    continue;
                }

                hallazgos.Add($"{ruta} usa {prohibida}");
            }
        }

        hallazgos.ShouldBeEmpty(string.Join("; ", hallazgos));
    }

    [Fact]
    public void La_lista_de_saltos_permitidos_no_nombra_sitios_que_ya_no_existen()
    {
        HashSet<string> presentes = [];

        foreach ((string ruta, string codigo) in CodigoDeProduccion().Where(fichero => VeEfCore(fichero.Codigo)))
        {
            foreach (string prohibida in s_prohibidas.Keys.Where(aguja => Veces(codigo, aguja) > 0))
            {
                presentes.Add($"{ruta} usa {prohibida}");
            }
        }

        List<string> sobran = [.. s_saltosPermitidos.Keys.Where(sitio => !presentes.Contains(sitio))];

        // Un permiso que ya no hace falta es un permiso que sigue concedido, y el siguiente que
        // escriba ahí esa llamada no se encontrará ningún rojo.
        sobran.ShouldBeEmpty(
            "estos saltos están autorizados y ya no están en el código: " + string.Join(", ", sobran));
    }

    [Fact]
    public void El_ambito_sin_inquilino_solo_se_abre_donde_esta_declarado()
    {
        Dictionary<string, int> aperturas = [];

        foreach ((string ruta, string codigo) in CodigoDeProduccion())
        {
            int cuantas = Veces(codigo, ".SinInquilino(");

            if (cuantas > 0)
            {
                aperturas[ruta] = cuantas;
            }
        }

        // Se comparan las dos listas ENTERAS, y no solo los sitios de más: un ámbito que
        // desaparece de donde hacía falta deja ese camino lanzando en cuanto lo pise una petición
        // sin empresa, y eso es un 500 que aquí se ve antes.
        Enumerar(aperturas).ShouldBe(Enumerar(s_ambitosPermitidos));
    }

    [Fact]
    public void El_ambito_que_ve_lo_bloqueado_solo_se_abre_donde_esta_declarado()
    {
        Dictionary<string, int> aperturas = [];

        foreach ((string ruta, string codigo) in CodigoDeProduccion())
        {
            int cuantas = Veces(codigo, ".ViendoLoBloqueado(");

            if (cuantas > 0)
            {
                aperturas[ruta] = cuantas;
            }
        }

        // Las dos listas ENTERAS y en los dos sentidos, igual que con el inquilinato. De más: un
        // camino nuevo mira datos que el art. 32 reserva y nadie lo ha decidido. De menos: el
        // desbloqueo se quedó sin su apertura y dejó de encontrar lo que iba a desbloquear, que
        // es un 404 en una operación que existe justamente para eso.
        Enumerar(aperturas).ShouldBe(Enumerar(s_aperturasDeBloqueoPermitidas));
    }

    /// <summary>
    /// Los caminos que ven lo bloqueado y los que emiten versión son conjuntos <b>disjuntos</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la mitad que sobrevive de las cuatro cláusulas «DEPENDE DE» del ADR-0017, convertida en
    /// algo que se pone rojo. Hasta el ítem 1.4 esas cuatro exenciones de <c>If-Match</c> decían
    /// «ninguna lectura de la API entrega un recurso bloqueado», y ese ítem construye justamente
    /// una. Lo que sigue siendo cierto, y lo que las sostiene ahora, es más estrecho: la lectura de
    /// lo bloqueado <b>no emite versión</b>, así que la llave que <c>If-Match</c> pediría sigue sin
    /// existir.
    /// </para>
    /// <para>
    /// <b>Por fichero y no por método</b>, y es a propósito: la frontera de este proyecto para el
    /// ámbito de bloqueo ya es el fichero —<c>s_aperturasDeBloqueoPermitidas</c> cuenta aperturas
    /// por fichero— y un caso de uso que viera lo bloqueado y produjera un <c>ConVersion&lt;T&gt;</c>
    /// en el mismo fichero es exactamente el cambio que hay que parar, esté o no en el mismo método.
    /// </para>
    /// <para>
    /// Y con <b>las dos mitades afirmadas no vacías</b>: si un día no hubiera ningún camino que ve
    /// lo bloqueado, o ninguno que emite versión, la intersección saldría vacía por no haber nada
    /// que intersecar y esta regla estaría diciendo que sí sin haber mirado (ADR-0020).
    /// </para>
    /// </remarks>
    [Fact]
    public void Ningun_camino_que_ve_lo_bloqueado_emite_un_testigo_de_version()
    {
        SortedSet<string> venLoBloqueado = new(StringComparer.Ordinal);
        SortedSet<string> emitenVersion = new(StringComparer.Ordinal);

        foreach ((string ruta, string codigo) in CodigoDeProduccion())
        {
            if (Veces(codigo, ".ViendoLoBloqueado(") > 0)
            {
                venLoBloqueado.Add(ruta);
            }

            // `ConVersion<` y no `ConVersion` a secas: lo segundo casaría con el propio fichero que
            // declara el tipo y con cualquier `using`, y convertiría la regla en una que se
            // dispara por nombrar el concepto en vez de por usarlo.
            if (Veces(codigo, "ConVersion<") > 0)
            {
                emitenVersion.Add(ruta);
            }
        }

        venLoBloqueado.ShouldNotBeEmpty(
            "no hay ni un camino que abra `ViendoLoBloqueado(...)`: la intersección de abajo " +
            "saldría vacía por no tener nada que intersecar, y esta regla diría que sí sin mirar");

        emitenVersion.ShouldNotBeEmpty(
            "no hay ni un camino que produzca `ConVersion<T>`: igual que arriba, la regla se " +
            "quedaría mirando al vacío y saldría verde");

        List<string> losDos = [.. venLoBloqueado.Intersect(emitenVersion, StringComparer.Ordinal)];

        losDos.ShouldBeEmpty(
            "estos ficheros ven lo bloqueado Y producen un recurso con su versión: " +
            string.Join(", ", losDos) + ". Eso resucita la llave que las cuatro exenciones de " +
            "If-Match de los desbloqueos —empresa, almacén, ubicación y usuario— dan por " +
            "inalcanzable. O deja de emitirse la versión por ese camino, o esas cuatro exenciones " +
            "caducan y hay que volver a exigir If-Match en las cuatro (ADR-0017, ADR-0027)");
    }

    /// <summary>
    /// Cada motivo declarado para ver lo bloqueado tiene un sitio que lo usa, y cada sitio usa uno
    /// declarado.
    /// </summary>
    /// <remarks>
    /// <c>MotivoParaVerLoBloqueado</c> es una lista cerrada, y hasta el ítem 1.4 tenía un solo
    /// valor y ninguna regla encima. Un enumerado de motivos legales se estropea por los dos lados:
    /// un valor declarado <b>que nadie usa</b> es la rama que nadie recorre y nadie prueba —lo dice
    /// el comentario del propio enumerado— y un motivo nuevo que aparece en el código <b>sin estar
    /// declarado</b> ni compilaría, pero uno declarado y usado en un sitio que nadie ha decidido,
    /// sí. Por eso las dos listas, enteras y en los dos sentidos.
    /// </remarks>
    [Fact]
    public void Cada_motivo_para_ver_lo_bloqueado_tiene_su_sitio_y_cada_sitio_su_motivo()
    {
        SortedSet<string> declarados = new(
            Enum.GetNames<MotivoParaVerLoBloqueado>(), StringComparer.Ordinal);

        declarados.ShouldNotBeEmpty("el enumerado de motivos no tiene ni un valor");

        SortedSet<string> usados = new(StringComparer.Ordinal);

        foreach ((_, string codigo) in CodigoDeProduccion())
        {
            foreach (Match cita in Regex.Matches(
                codigo,
                @"MotivoParaVerLoBloqueado\.(\w+)",
                RegexOptions.None,
                TimeSpan.FromSeconds(5)))
            {
                usados.Add(cita.Groups[1].Value);
            }
        }

        usados.ShouldBe(
            declarados,
            customMessage:
            "los motivos que el código usa no son los que el enumerado declara. Usados: " +
            string.Join(", ", usados) + ". Declarados: " + string.Join(", ", declarados) +
            ". Un valor declarado que nadie usa es una rama que nadie prueba; uno usado por un " +
            "camino que nadie ha declarado es una puerta al art. 32 que no ha decidido nadie");
    }

    [Fact]
    public void Los_filtros_globales_se_definen_solo_en_los_contextos_de_modulo()
    {
        List<string> fuera = [.. CodigoDeProduccion()
            .Where(fichero => Veces(fichero.Codigo, ".HasQueryFilter(") > 0)
            .Select(fichero => fichero.Ruta)
            .Where(ruta => !s_dondeSeDefinenLosFiltros.Contains(ruta, StringComparer.Ordinal))];

        fuera.ShouldBeEmpty(
            "estos ficheros definen filtros globales fuera de los contextos de módulo: " +
            string.Join(", ", fuera));
    }

    private static string Enumerar(Dictionary<string, int> cuenta) => string.Join(
        "\n",
        cuenta.OrderBy(par => par.Key, StringComparer.Ordinal)
            .Select(par => $"{par.Key} x {par.Value.ToString(CultureInfo.InvariantCulture)}"));

    // Un fichero que no nombra EF Core no puede llamar a nada de EF Core: ni compilaría. Es el
    // filtro más barato que distingue «esto rodea el filtro global» de «esto es un método de
    // `List<T>` que se llama igual».
    private static bool VeEfCore(string codigo) =>
        codigo.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal);

    private static int Veces(string codigo, string aguja)
    {
        int cuantas = 0;
        int desde = 0;

        while ((desde = codigo.IndexOf(aguja, desde, StringComparison.Ordinal)) >= 0)
        {
            cuantas++;
            desde += aguja.Length;
        }

        return cuantas;
    }

    private static IEnumerable<(string Ruta, string Codigo)> CodigoDeProduccion()
    {
        string raiz = Raiz();
        string separador = Path.DirectorySeparatorChar.ToString();

        foreach (string fichero in Directory.EnumerateFiles(
            Path.Combine(raiz, "src"), "*.cs", SearchOption.AllDirectories))
        {
            // La carpeta de trabajo del compilador guarda copias generadas de los propios
            // fuentes: contarlas duplicaría cada hallazgo y ataría el test a si alguien ha
            // compilado antes de ejecutarlo.
            if (fichero.Contains(separador + "obj" + separador, StringComparison.Ordinal))
            {
                continue;
            }

            yield return (
                Path.GetRelativePath(raiz, fichero).Replace(Path.DirectorySeparatorChar, '/'),
                SinComentarios(File.ReadAllText(fichero)));
        }
    }

    // Basta y sobra para lo que se busca. Se lleva por delante lo que vaya detrás de las dos
    // barras dentro de una cadena -una dirección web, por ejemplo-, y da igual: ninguna de las
    // agujas de este fichero puede aparecer ahí.
    private static string SinComentarios(string codigo) => Regex.Replace(
        Regex.Replace(codigo, Bloque, string.Empty, RegexOptions.Singleline),
        Linea,
        string.Empty,
        RegexOptions.Multiline);

    // El repositorio se encuentra subiendo hasta la solución, y se parte del directorio del
    // ensamblado, NO de este fichero. La primera versión hacía lo contrario y se cayó en la CI
    // estando verde aquí: `Directory.Build.props` pone `ContinuousIntegrationBuild` cuando corre
    // en GitHub Actions, eso activa `DeterministicSourcePaths`, y con él las rutas de los fuentes
    // se reescriben a `/_/tests/…` para que dos máquinas produzcan el mismo binario. Un
    // `[CallerFilePath]` así no apunta a ningún sitio que exista.
    //
    // El fichero del test queda de segundo intento, por si algún día la salida se mueve fuera del
    // árbol. Y si no aparece por ninguno de los dos, esto REVIENTA: un barrido que no encuentra
    // qué barrer no puede dar verde, que es justo lo que hizo bien la versión anterior.
    private static string Raiz([CallerFilePath] string desde = "")
    {
        string? raiz = Subiendo(AppContext.BaseDirectory) ?? Subiendo(Path.GetDirectoryName(desde));

        raiz.ShouldNotBeNull(
            "no se ha encontrado Bastion.sln, ni subiendo desde el ensamblado ni desde el fichero del test");

        return raiz;
    }

    private static string? Subiendo(string? partida)
    {
        DirectoryInfo? carpeta = string.IsNullOrEmpty(partida) ? null : new DirectoryInfo(partida);

        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "Bastion.sln")))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta?.FullName;
    }
}
