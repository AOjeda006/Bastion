namespace Bastion.Arquitectura.Tests;

/// <summary>
/// En qué estado está cada módulo del §5: si tiene carpeta, y si esa carpeta lleva ya código.
/// </summary>
internal enum Presencia
{
    /// <summary>Ni siquiera hay carpeta en <c>src/Modules/</c>. Decidido, no olvidado.</summary>
    SinCarpeta,

    /// <summary>Carpeta y cinco proyectos vacíos, esperando a su fase.</summary>
    Andamio,

    /// <summary>Montado: al menos una de sus cinco capas lleva tipos.</summary>
    Montado,
}

/// <summary>
/// Lo que el proyecto DECLARA que hay: los dieciséis módulos del §5, sus cinco capas y qué capas
/// llevan tipos hoy. Es la mitad declarada de todas las comparaciones de este carril; la otra
/// mitad la descubre <see cref="Ensamblados"/>, y los tests comparan las dos ENTERAS.
/// </summary>
/// <remarks>
/// <para>
/// Por qué existe este fichero, y no reglas escritas módulo a módulo: trece de los dieciséis
/// módulos todavía no existen. Una regla por nombre —«Bastion.Ventas.Domain no referencia EF
/// Core»— nacería sobre un conjunto vacío, y en NetArchTest un conjunto vacío CUMPLE: no hay
/// ningún tipo que incumpla, así que la regla sale verde y sigue verde el día que el módulo se
/// monte mal. Trece reglas verdes que no miran nada son peores que ninguna, porque el informe
/// dice dieciséis fronteras comprobadas.
/// </para>
/// <para>
/// De ahí la regla del carril: <b>toda regla afirma también que su conjunto no está vacío, y ese
/// conteo se compara</b> contra lo que este inventario declara. Una regla sin esa afirmación no
/// cuenta como escrita.
/// </para>
/// </remarks>
internal static class Inventario
{
    /// <summary>La raíz de espacios de nombres y de ensamblados (Anexo A.1). Una sola grafía.</summary>
    internal const string Raiz = "Bastion";

    /// <summary>
    /// El bloque común. NO es un módulo: sus ensamblados se llaman igual —
    /// <c>Bastion.BuildingBlocks.Domain</c> casa con el patrón <c>Bastion.&lt;X&gt;.&lt;Capa&gt;</c>—
    /// así que hay que excluirlo del descubrimiento a mano, y aquí está dicho por qué en vez de
    /// escondido dentro de un <c>Where</c>.
    /// </summary>
    internal const string BloqueComun = "BuildingBlocks";

    /// <summary>
    /// Las cinco capas del §4, de dentro afuera. El ORDEN es el de las dependencias permitidas:
    /// cada una puede mirar a las anteriores de su propio módulo y a ninguna posterior.
    /// </summary>
    internal static readonly string[] Capas =
        ["Domain", "Contracts", "Application", "Infrastructure", "Endpoints"];

    /// <summary>
    /// Los dieciséis módulos del §5 con el estado que se espera de cada uno. La comparación es
    /// entera y en los dos sentidos: uno de más es un módulo que alguien ha empezado sin decirlo;
    /// uno de menos es una carpeta que ha desaparecido.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, Presencia> Modulos =
        new SortedDictionary<string, Presencia>(StringComparer.Ordinal)
        {
            ["Auditoria"] = Presencia.Montado,
            ["Identidad"] = Presencia.Montado,
            ["Organizacion"] = Presencia.Montado,

            ["Catalogo"] = Presencia.Andamio,
            ["Compras"] = Presencia.Andamio,
            ["Contabilidad"] = Presencia.Andamio,
            ["Crm"] = Presencia.Andamio,
            ["Facturacion"] = Presencia.Andamio,
            ["Informes"] = Presencia.Andamio,
            ["Inventario"] = Presencia.Andamio,
            ["Produccion"] = Presencia.Andamio,
            ["Rrhh"] = Presencia.Andamio,
            ["Terceros"] = Presencia.Andamio,
            ["Tesoreria"] = Presencia.Andamio,
            ["Ventas"] = Presencia.Andamio,

            // El decimosexto. No tiene carpeta, y eso está decidido y escrito en `docs/PLAN.md`
            // (tabla de esquemas del 0.4): su esquema `notificaciones` está nombrado y la carpeta
            // se creará con él. Aparece AQUÍ para que los dieciséis del §5 estén los dieciséis: si
            // se declararan quince, la comparación con el disco daría verde y el que falta no se
            // echaría en falta nunca.
            ["Notificaciones"] = Presencia.SinCarpeta,
        };

    /// <summary>
    /// Qué ensamblados de módulo llevan tipos HOY, como <c>Modulo.Capa</c>. Lo que no está aquí
    /// está vacío — y una regla sobre un ensamblado vacío no protege nada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La lista es corta y desigual a propósito, porque así está el proyecto: <b>Auditoría tiene
    /// cuatro de sus cinco capas vacías</b>. El módulo del 0.7 registra quién cambió qué desde un
    /// interceptor de EF Core, así que todo lo suyo vive en <c>Infrastructure</c> y sus proyectos
    /// de <c>Domain</c>, <c>Application</c>, <c>Contracts</c> y <c>Endpoints</c> compilan a un
    /// ensamblado sin un solo tipo dentro.
    /// </para>
    /// <para>
    /// Eso NO se arregla aquí y no es un fallo: es la forma que tiene ese módulo hoy. Lo que se
    /// arregla es que se sepa. Sin esta lista, cuatro de las fronteras de Auditoría saldrían
    /// verdes sin haber mirado nada y el informe diría que Auditoría cumple las cinco. El día que
    /// Auditoría estrene su primera entidad de dominio, este test se pone rojo y obliga a añadir
    /// la línea — que es exactamente el día en que esa regla empieza a proteger algo.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlySet<string> EnsambladosConTipos = new SortedSet<string>(
        StringComparer.Ordinal)
    {
        "Auditoria.Infrastructure",

        "Identidad.Application",
        "Identidad.Contracts",
        "Identidad.Domain",
        "Identidad.Endpoints",
        "Identidad.Infrastructure",

        "Organizacion.Application",
        "Organizacion.Contracts",
        "Organizacion.Domain",
        "Organizacion.Endpoints",
        "Organizacion.Infrastructure",
    };

    /// <summary>
    /// El bloque común, que no es un módulo pero sí tiene capas y sí las tiene que respetar. Sus
    /// tres ensamblados están nombrados a mano porque son tres y no van a crecer con las fases; y
    /// aun así se comparan, porque una regla que se aplique a dos de los tres no lo diría.
    /// </summary>
    internal static readonly IReadOnlySet<string> ComunesConTipos = new SortedSet<string>(
        StringComparer.Ordinal)
    {
        "BuildingBlocks.Application",
        "BuildingBlocks.Domain",
        "BuildingBlocks.Infrastructure",
    };

    /// <summary>
    /// Lo que un <c>Domain</c> no puede ver, y —esto es la mitad que importa— <b>dónde sí se ve</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La clave es el espacio de nombres prohibido; el valor, la capa donde ese mismo espacio de
    /// nombres tiene que aparecer de verdad. Prohibir algo que no existe en ninguna parte del
    /// proyecto es una regla que no puede dispararse nunca: sale verde con el dominio limpio y
    /// sale verde con el dominio lleno de EF Core, porque la cadena que busca está mal escrita y
    /// no casa con nada. <c>El_dominio_no_conoce_la_infraestructura_ni_el_framework</c> comprueba
    /// la prohibición; <c>La_prohibicion_al_dominio_puede_dispararse</c> comprueba el
    /// contraejemplo. Las dos, o ninguna vale.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, string> ProhibidasAlDominio =
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            // El acceso a datos. El dominio no sabe que existe una base de datos (§4, regla 2).
            ["Microsoft.EntityFrameworkCore"] = "Infrastructure",

            // El framework web. Un dominio que sabe de peticiones HTTP ya no es un dominio.
            ["Microsoft.AspNetCore"] = "Endpoints",

            // El proveedor concreto de PostgreSQL. Va aparte de EF Core porque son dos capas de
            // acoplamiento distintas: se puede estar atado a EF Core sin estarlo a Npgsql.
            ["Npgsql"] = "Infrastructure",

            // Y la infraestructura de casa, que es la que un dominio tiene MÁS a mano: está en la
            // misma solución, no hace falta añadir ningún paquete y el compilador la deja entrar
            // en cuanto alguien ponga la referencia de proyecto. Sin esta línea, la única puerta
            // que no hay que ir a buscar fuera se quedaba abierta.
            [Raiz + ".BuildingBlocks.Infrastructure"] = "Infrastructure",
        };

    /// <summary>
    /// El grafo de referencias de proyecto entre los quince <c>.csproj</c> de módulo, como
    /// <c>Origen -&gt; Destino</c>. Entero, y se compara entero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe por un agujero que encontró la batería de mutaciones de este mismo ítem, y se cuenta
    /// aquí porque es la clase de cosa que no se vuelve a descubrir sola: NetArchTest lee <b>IL</b>,
    /// y una referencia de proyecto que nadie usa no emite IL. Se le puede poner a
    /// <c>Bastion.Identidad.Application</c> una referencia a <c>Bastion.Organizacion.Domain</c> —el
    /// cruce que la regla 1 prohíbe— y las catorce reglas anteriores siguen verdes mientras no haya
    /// una línea que la ejerza. Tampoco avisa el compilador: una referencia sin usar no es un aviso.
    /// </para>
    /// <para>
    /// Y no es un caso rebuscado, es el orden natural de las cosas: primero se añade la referencia
    /// —porque se va a necesitar—, y la línea que la usa llega después, en otro commit, quizá de
    /// otra mano. Un carril que solo mire el IL da luz verde al primer commit y rojo al segundo, que
    /// es tarde: para entonces la autorización para cruzar ya estaba concedida y revisada.
    /// </para>
    /// <para>
    /// Así que se vigilan dos cosas distintas y por separado: el <b>uso</b>, en el IL, con las
    /// reglas de arriba; y el <b>permiso</b>, en el <c>.csproj</c>, aquí.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlySet<string> AristasDeProyecto = new SortedSet<string>(
        StringComparer.Ordinal)
    {
        "Auditoria.Application -> Auditoria.Contracts",
        "Auditoria.Application -> Auditoria.Domain",
        "Auditoria.Application -> BuildingBlocks.Application",
        "Auditoria.Domain -> BuildingBlocks.Domain",
        "Auditoria.Endpoints -> Auditoria.Application",
        "Auditoria.Infrastructure -> Auditoria.Application",
        "Auditoria.Infrastructure -> BuildingBlocks.Infrastructure",

        "Identidad.Application -> BuildingBlocks.Application",
        "Identidad.Application -> Identidad.Contracts",
        "Identidad.Application -> Identidad.Domain",

        // El único cruce entre módulos, y va por el `Contracts` del dueño. Es la misma frontera que
        // vigila `El_unico_cruce_entre_modulos_va_por_contratos`, pero vista un paso antes: allí se
        // comprueba lo que Identidad USA de Organización; aquí, lo que tiene PERMISO para usar.
        "Identidad.Application -> Organizacion.Contracts",

        "Identidad.Domain -> BuildingBlocks.Domain",
        "Identidad.Endpoints -> BuildingBlocks.Infrastructure",
        "Identidad.Endpoints -> Identidad.Application",
        "Identidad.Infrastructure -> BuildingBlocks.Infrastructure",
        "Identidad.Infrastructure -> Identidad.Application",

        "Organizacion.Application -> BuildingBlocks.Application",
        "Organizacion.Application -> Organizacion.Contracts",
        "Organizacion.Application -> Organizacion.Domain",
        "Organizacion.Contracts -> BuildingBlocks.Domain",
        "Organizacion.Domain -> BuildingBlocks.Domain",
        "Organizacion.Endpoints -> BuildingBlocks.Infrastructure",
        "Organizacion.Endpoints -> Organizacion.Application",
        "Organizacion.Infrastructure -> BuildingBlocks.Infrastructure",
        "Organizacion.Infrastructure -> Organizacion.Application",
    };

    /// <summary>
    /// Los cruces entre módulos que hay hoy, con su motivo. Se compara la lista entera: un cruce
    /// nuevo no puede aparecer sin escribir su línea aquí, y una línea que sobra delata un cruce
    /// que se quitó y una autorización que sigue concedida.
    /// </summary>
    /// <remarks>
    /// Vive aquí y no dentro de <c>LasFronterasEntreModulosTests</c> porque la lee <b>otra</b>
    /// regla: <c>LosIdentificadoresAjenosTests</c> exige que todo módulo que guarde un
    /// identificador de otro tenga su cruce declarado. Dos listas de cruces —una por regla— serían
    /// dos verdades que se separan el día que alguien actualice una.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, string> CrucesDeclarados =
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Identidad.Application -> Bastion.Organizacion.Contracts"] =
                "el único, y va por donde tiene que ir. Al abrir sesión o al cambiar de empresa, " +
                "Identidad pregunta a Organización si esa empresa existe y no está bloqueada " +
                "antes de meterla en el testigo. Lectura, por el contrato del dueño, resuelta en " +
                "proceso: ni un JOIN entre esquemas ni una llamada HTTP.",
        };

    /// <summary>
    /// Las puertas públicas de los <c>Contracts</c>: toda interfaz que un módulo ofrece a los
    /// demás, con lo que hace. Es la lista entera y se compara entera.
    /// </summary>
    /// <remarks>
    /// Es lo que este carril puede decir de la <b>regla 5</b> («escrituras entre módulos, solo por
    /// eventos»). Que un módulo no pueda LLAMAR a un caso de uso ajeno ya lo impide la regla 1: no
    /// alcanza su <c>Application</c>. Lo que la regla 1 no impide es que alguien publique en su
    /// propio <c>Contracts</c> un puerto que escriba, y eso sí es un hecho de tipos y sí se puede
    /// vigilar: aquí. Una puerta nueva no puede aparecer sin escribir su línea, y al escribirla
    /// hay que decir si lee o si escribe.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, string> PuertasPublicas =
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Bastion.Organizacion.Contracts.Divisas.IConsultaDeDivisas"] =
                "LECTURA: en qué estado está una divisa, para quien guarde su identificador — la " +
                "tarifa del §7.3, y detrás de ella todo lo que lleve importe. No escribe.",

            ["Bastion.Organizacion.Contracts.Empresas.IConsultaDeEmpresas"] =
                "LECTURA: Identidad le pregunta a Organización si una empresa existe y no está " +
                "bloqueada, para poder emitir un testigo con ella dentro. No escribe.",

            ["Bastion.Organizacion.Contracts.Impuestos.IConsultaDeImpuestos"] =
                "LECTURA: en qué estado está un tramo de impuesto para una fecha de devengo, " +
                "para quien guarde su identificador — el impuesto por defecto del artículo " +
                "(§7.3). No escribe.",

            ["Bastion.Organizacion.Contracts.Unidades.IConsultaDeUnidadesDeMedida"] =
                "LECTURA: en qué estado está una unidad de medida, para quien guarde su " +
                "identificador — la unidad base del artículo (§7.3). No escribe.",
        };

    /// <summary>
    /// Un identificador de otro módulo guardado en el dominio: a qué apunta, y por dónde se
    /// valida.
    /// </summary>
    /// <param name="Apunta">
    /// Nombre del tipo de dominio al que apunta, o cadena vacía si no apunta a ninguna entidad.
    /// </param>
    /// <param name="Puerto">
    /// Nombre completo de la interfaz del <c>Contracts</c> del dueño que lo valida. Vacío cuando el
    /// dueño es el propio módulo —ahí no hay frontera que cruzar— o cuando no apunta a nada.
    /// </param>
    /// <param name="Motivo">Por qué el nombre no se explica solo.</param>
    internal sealed record Identificador(string Apunta, string Puerto, string Motivo);

    /// <summary>
    /// Todo <c>Guid</c> del dominio acabado en <c>Id</c> cuyo nombre <b>no</b> case con el de un
    /// tipo del dominio, dicho a mano: a qué apunta y por dónde se valida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la <b>fuente declarada</b> del ADR-0024, y la razón por la que hay dos. El descubrimiento
    /// por nombre —<c>EmpresaId</c> → <c>Empresa</c>— es barato y no se olvida, pero
    /// <b>infradetecta por diseño</b>: <c>TokenDeRefresco.EmpresaActivaId</c> apunta a una empresa
    /// desde el 0.5 y ninguna heurística por nombre lo dice. El caso que viene, la <i>unidad
    /// base</i> del §7.3, se llamará <c>UnidadBaseId</c> y tampoco casará.
    /// </para>
    /// <para>
    /// Por eso la comparación <b>no es una igualdad</b>: el descubrimiento tiene que estar contenido
    /// en esta lista, no ser igual a ella. Lo que sí es simétrico es que una entrada que ya no
    /// corresponde a ninguna propiedad es roja: una declaración que sobrevive a su motivo es un
    /// permiso concedido sobre algo que cambió.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, Identificador> IdentificadoresDeclarados =
        new SortedDictionary<string, Identificador>(StringComparer.Ordinal)
        {
            ["ConversionUM.UnidadDestinoId"] = new(
                "UnidadMedida",
                "",
                "el papel va en el nombre —origen y destino— y por eso no casa con el del tipo. " +
                "Mismo módulo: no cruza ninguna frontera."),

            ["ConversionUM.UnidadOrigenId"] = new(
                "UnidadMedida",
                "",
                "el papel va en el nombre —origen y destino— y por eso no casa con el del tipo. " +
                "Mismo módulo: no cruza ninguna frontera."),

            ["EventoDeIntegracion.EventoId"] = new(
                "",
                "",
                "no apunta a nada: es su PROPIA identidad, la clave de deduplicación de la " +
                "bandeja. Se llama EventoId y no Id porque un evento no es una EntidadBase, y ese " +
                "nombre es justo el que engaña a una heurística de sufijos."),

            ["Membresia.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "el nombre SÍ casa, y aun así se declara: lo que la lista aporta aquí no es " +
                "descubrirlo, es decir POR DÓNDE se valida. Sin el puerto escrito, la regla sabría " +
                "que hay un cruce y no podría exigir que alguien lo compruebe."),

            ["TokenDeRefresco.EmpresaActivaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "la prueba viva de por qué hace falta esta lista: apunta a una empresa desde el " +
                "0.5 y el nombre no lo dice. Lo validan ConstructorDeSesion y RenovarSesion contra " +
                "el selector que sale del puerto."),

            ["TokenDeRefresco.SustituidoPorId"] = new(
                "TokenDeRefresco",
                "",
                "apunta a otro token de la misma cadena de rotación: mismo tipo y mismo módulo. " +
                "El nombre dice el papel —«la emisión que lo sustituyó»— y no el tipo, que es " +
                "otra vez el caso que ninguna heurística por nombre resuelve."),

            ["TokenDeRefresco.FamiliaId"] = new(
                "",
                "",
                "no apunta a ninguna entidad: agrupa la cadena de refrescos que nace de un mismo " +
                "inicio de sesión, para poder revocarla entera. Se declara porque un Guid sin " +
                "clasificar es exactamente lo que esta regla persigue."),

            ["TipoCambio.DivisaDestinoId"] = new(
                "Divisa",
                "",
                "el papel va en el nombre —origen y destino— y por eso no casa con el del tipo. " +
                "Mismo módulo: no cruza ninguna frontera."),

            ["TipoCambio.DivisaOrigenId"] = new(
                "Divisa",
                "",
                "el papel va en el nombre —origen y destino— y por eso no casa con el del tipo. " +
                "Mismo módulo: no cruza ninguna frontera."),
        };
}
