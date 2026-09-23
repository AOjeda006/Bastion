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
            ["Terceros"] = Presencia.Montado,
            ["Catalogo"] = Presencia.Montado,
            ["Inventario"] = Presencia.Montado,

            ["Compras"] = Presencia.Andamio,
            ["Contabilidad"] = Presencia.Andamio,
            ["Crm"] = Presencia.Andamio,
            ["Facturacion"] = Presencia.Andamio,
            ["Informes"] = Presencia.Andamio,
            ["Produccion"] = Presencia.Andamio,
            ["Rrhh"] = Presencia.Andamio,
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

        "Catalogo.Application",
        "Catalogo.Contracts",
        "Catalogo.Domain",
        "Catalogo.Endpoints",
        "Catalogo.Infrastructure",

        "Identidad.Application",
        "Identidad.Contracts",
        "Identidad.Domain",
        "Identidad.Endpoints",
        "Identidad.Infrastructure",

        // Inventario completa sus CINCO capas en el ítem 2.4, y hasta entonces esta lista dijo
        // que eran cuatro: el 2.3 trajo el libro, el documento, sus casos de uso y sus dos
        // eventos, pero `Inventario.Endpoints` compilaba vacío, y declararlo habría dado por
        // mirada una frontera que no protegía nada. Desde el 2.4 lleva un tipo —la confirmación
        // del ajuste, que es la única acción del borde— y las reglas del carril pasan a
        // aplicársele de verdad.
        "Inventario.Application",
        "Inventario.Contracts",
        "Inventario.Domain",
        "Inventario.Endpoints",
        "Inventario.Infrastructure",

        "Organizacion.Application",
        "Organizacion.Contracts",
        "Organizacion.Domain",
        "Organizacion.Endpoints",
        "Organizacion.Infrastructure",

        "Terceros.Application",
        "Terceros.Contracts",
        "Terceros.Domain",
        "Terceros.Endpoints",
        "Terceros.Infrastructure",
    };

    /// <summary>
    /// El bloque común, que no es un módulo pero sí tiene capas y sí las tiene que respetar. Sus
    /// ensamblados están nombrados a mano, y se comparan enteros: una regla que se aplique a tres
    /// de los cuatro no lo diría.
    /// </summary>
    /// <remarks>
    /// Hasta el ítem 1.3 esta lista tenía tres líneas y decía que «son tres y no van a crecer con
    /// las fases». Creció en el 1.3, con <c>BuildingBlocks.Contracts</c>. Aquella frase era una
    /// PREDICCIÓN escrita con la forma de una regla, y el mecanismo hizo lo suyo: el ensamblado
    /// nuevo puso el test rojo el mismo día y obligó a escribir su línea. Lo que se corrige aquí
    /// no es la lista —esa se corrige sola— sino la frase, que prometía un futuro que no le
    /// tocaba prometer.
    /// </remarks>
    internal static readonly IReadOnlySet<string> ComunesConTipos = new SortedSet<string>(
        StringComparer.Ordinal)
    {
        "BuildingBlocks.Application",
        "BuildingBlocks.Contracts",
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

        "Catalogo.Application -> BuildingBlocks.Application",

        // El TERCER cruce entre módulos, y por la misma puerta que los dos primeros. Un
        // artículo guarda tres identificadores de Organización —empresa, unidad base e
        // impuesto por defecto— y ninguno de los tres es clave ajena: viven en otro esquema
        // (regla 4). Quien dice si existen y si siguen ofreciéndose es Organización, por sus
        // tres puertos de lectura.
        "Catalogo.Application -> Organizacion.Contracts",

        "Catalogo.Application -> Catalogo.Contracts",
        "Catalogo.Application -> Catalogo.Domain",

        // El CUARTO cruce, y la mitad de ida del primero MUTUO. Un artículo guarda quién se lo
        // suministra —`ArticuloProveedor.TerceroId`— y ese Guid vive en el esquema `terceros`,
        // sin clave ajena (regla 4). Quien dice si ese tercero existe en esta empresa, si está
        // bloqueado y si hace de proveedor es Terceros, por `IConsultaDeTerceros`.
        "Catalogo.Application -> Terceros.Contracts",

        // Catalogo.Contracts SIGUE sin ninguna arista, y desde el 1.10 eso ya no es una
        // casualidad de un proyecto pequeño: aquí vive `IConsultaDeTarifas`, la mitad de vuelta
        // del primer cruce mutuo, y NO referencia `Terceros.Contracts` —que es donde vive la
        // otra mitad—. Las dos juntas serían un ciclo entre proyectos, y el compilador no lo
        // contaría como lo que es: se comprobó poniéndolas a la vez y lo que salió fue
        // «dependencia circular en el grafo de dependencias de destino» hablando de
        // `_GenerateRestoreProjectPathWalk`. Por eso por los dos puertos cruzan Guid, DateOnly
        // y enumerados propios, y ni un tipo compartido. El día que este proyecto necesite el
        // bloque común, la arista aparecerá aquí y habrá que escribirla; lo que no puede
        // aparecer nunca es la arista al Contracts del otro módulo de la pareja.
        "Catalogo.Domain -> BuildingBlocks.Domain",
        "Catalogo.Endpoints -> BuildingBlocks.Infrastructure",
        "Catalogo.Endpoints -> Catalogo.Application",
        "Catalogo.Infrastructure -> BuildingBlocks.Infrastructure",
        "Catalogo.Infrastructure -> Catalogo.Application",

        "Identidad.Application -> BuildingBlocks.Application",
        "Identidad.Application -> Identidad.Contracts",
        "Identidad.Application -> Identidad.Domain",

        // El único cruce entre módulos, y va por el `Contracts` del dueño. Es la misma frontera que
        // vigila `El_unico_cruce_entre_modulos_va_por_contratos`, pero vista un paso antes: allí se
        // comprueba lo que Identidad USA de Organización; aquí, lo que tiene PERMISO para usar.
        "Identidad.Application -> Organizacion.Contracts",

        // Los dos `Contracts` de módulo ven los contratos comunes desde el 1.3, y NO ven el
        // dominio de su propio módulo. La frontera del §4 prohíbe lo segundo, no lo primero:
        // `BuildingBlocks.Contracts` no es el interior de nadie, no referencia nada, y todos los
        // módulos lo alcanzan ya por sus otras capas. El motivo entero, en el ADR-0029.
        "Identidad.Contracts -> BuildingBlocks.Contracts",

        "Identidad.Domain -> BuildingBlocks.Domain",
        "Identidad.Endpoints -> BuildingBlocks.Infrastructure",
        "Identidad.Endpoints -> Identidad.Application",
        "Identidad.Infrastructure -> BuildingBlocks.Infrastructure",
        "Identidad.Infrastructure -> Identidad.Application",

        // Las doce de Inventario: diez hacia dentro de su módulo o hacia el bloque común, y DOS
        // que cruzan. Las dos que cruzan salen del `Application` y entran por el `Contracts` del
        // dueño, que es la única puerta (§4, frontera 1); ninguna toca un `Domain` ajeno.
        //
        // Y son exactamente las que el libro necesita para no tener claves ajenas. `almacen_id`,
        // `ubicacion_id`, `articulo_id` y `unidad_introducida_id` son `uuid` sueltos en el esquema
        // `inventario` —entre esquemas no se cruza—, así que lo único que impide que ahí acabe un
        // identificador inventado es que el alta lo pregunte por un puerto. Eso es la R7 leída al
        // derecho: el cruce no es lo que debilita la frontera, es lo que la sostiene.
        "Inventario.Application -> BuildingBlocks.Application",
        "Inventario.Application -> Catalogo.Contracts",
        "Inventario.Application -> Inventario.Contracts",
        "Inventario.Application -> Inventario.Domain",
        "Inventario.Application -> Organizacion.Contracts",
        "Inventario.Contracts -> BuildingBlocks.Contracts",
        "Inventario.Contracts -> BuildingBlocks.Domain",
        "Inventario.Domain -> BuildingBlocks.Domain",
        "Inventario.Endpoints -> BuildingBlocks.Infrastructure",
        "Inventario.Endpoints -> Inventario.Application",
        "Inventario.Infrastructure -> BuildingBlocks.Infrastructure",
        "Inventario.Infrastructure -> Inventario.Application",

        "Organizacion.Application -> BuildingBlocks.Application",
        "Organizacion.Application -> Organizacion.Contracts",
        "Organizacion.Application -> Organizacion.Domain",
        "Organizacion.Contracts -> BuildingBlocks.Contracts",
        "Organizacion.Contracts -> BuildingBlocks.Domain",
        "Organizacion.Domain -> BuildingBlocks.Domain",
        "Organizacion.Endpoints -> BuildingBlocks.Infrastructure",
        "Organizacion.Endpoints -> Organizacion.Application",
        "Organizacion.Infrastructure -> BuildingBlocks.Infrastructure",
        "Organizacion.Infrastructure -> Organizacion.Application",

        "Terceros.Application -> BuildingBlocks.Application",

        // El SEGUNDO cruce entre módulos, y va por donde el primero: el `Contracts` del
        // dueño. Un tercero es de la empresa que lo conoce y guarda su Guid sin clave ajena
        // —vive en otro esquema, regla 4—, así que quien comprueba que esa empresa existe
        // y no está bloqueada es Organización, a través de `IConsultaDeEmpresas`.
        "Terceros.Application -> Organizacion.Contracts",

        // La mitad de VUELTA del primer cruce mutuo. `Tercero.TarifaAsignadaId` es un Guid del
        // esquema `catalogo`, sin clave ajena, y quien dice si esa tarifa existe en esta empresa
        // y si su vigencia cubre el día de la asignación es Catálogo, por `IConsultaDeTarifas`.
        // Las dos flechas existen a la vez y no son un ciclo porque lo que se referencia es el
        // `Contracts` del otro, y los dos `Contracts` no se ven entre sí.
        "Terceros.Application -> Catalogo.Contracts",

        "Terceros.Application -> Terceros.Contracts",
        "Terceros.Application -> Terceros.Domain",
        "Terceros.Contracts -> BuildingBlocks.Contracts",
        "Terceros.Contracts -> BuildingBlocks.Domain",
        "Terceros.Domain -> BuildingBlocks.Domain",
        "Terceros.Endpoints -> BuildingBlocks.Infrastructure",
        "Terceros.Endpoints -> Terceros.Application",
        "Terceros.Infrastructure -> BuildingBlocks.Infrastructure",
        "Terceros.Infrastructure -> Terceros.Application",
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
            ["Catalogo.Application -> Bastion.Organizacion.Contracts"] =
                "el tercero, y el más cargado: un artículo cuelga de una empresa y guarda " +
                "una unidad base y un impuesto por defecto que son de Organización. Los " +
                "tres se preguntan por su puerto antes de construir el agregado, y los dos " +
                "últimos no preguntan «¿existe?» sino «¿en qué estado está?»: la retirada " +
                "del ADR-0023 solo significa algo si alguien distingue lo que se ofrece " +
                "para lo nuevo de lo que únicamente resuelve lo viejo, y ese alguien es " +
                "este módulo.",

            ["Catalogo.Application -> Bastion.Terceros.Contracts"] =
                "el cuarto, y la mitad de IDA del primero MUTUO del proyecto. Los tres " +
                "anteriores apuntaban todos a Organización: un módulo dueño publicaba una " +
                "lectura y los demás preguntaban, sin que nadie le preguntara a él. Aquí " +
                "Catálogo mira a Terceros para colgarle un proveedor a un artículo y Terceros " +
                "mira a Catálogo para asignarle una tarifa a un cliente, y las dos flechas " +
                "existen a la vez. No son un ciclo porque lo referenciado es el Contracts del " +
                "otro y los dos Contracts no se ven entre sí: por los puertos cruzan Guid, " +
                "DateOnly y enumerados propios. Y este puerto no pregunta «¿existe?» sino «¿en " +
                "qué estado está, para este papel?»: un tercero tiene dos ejes —qué papeles " +
                "hace y si está bloqueado por el art. 32— y los dos deciden.",

            ["Identidad.Application -> Bastion.Organizacion.Contracts"] =
                "el único, y va por donde tiene que ir. Al abrir sesión o al cambiar de empresa, " +
                "Identidad pregunta a Organización si esa empresa existe y no está bloqueada " +
                "antes de meterla en el testigo. Lectura, por el contrato del dueño, resuelta en " +
                "proceso: ni un JOIN entre esquemas ni una llamada HTTP.",

            ["Inventario.Application -> Bastion.Catalogo.Contracts"] =
                "el séptimo, y el primero que NO es de un maestro hacia otro maestro: aquí " +
                "pregunta un MOVIMIENTO. Cada línea del libro guarda el artículo que se mueve, y " +
                "el alta de un ajuste pregunta a Catálogo si contra ese artículo se pueden mover " +
                "existencias. La pregunta no es «¿existe?»: un Servicio existe y contesta " +
                "NoSeAlmacena, y un artículo retirado resuelve lo viejo y no se ofrece para lo " +
                "nuevo. Estrena el puerto que el 2.2 declaró sin consumidor, igual que el 1.8 " +
                "estrenó los del 1.2.",

            ["Inventario.Application -> Bastion.Organizacion.Contracts"] =
                "el sexto, y el más cargado después del de Catálogo: un ajuste cuelga de una " +
                "empresa y se hace en un almacén, y cada línea nombra una ubicación dentro de " +
                "ese almacén y la unidad en la que se escribió la cantidad. Los cuatro se " +
                "preguntan antes de construir el agregado. Y es aquí donde el ADR-0037 se cobra " +
                "POR EL EFECTO: un almacén bloqueado contesta SoloResuelveLoViejo, el alta lo " +
                "rechaza, y los movimientos YA escritos contra él se siguen leyendo por este " +
                "mismo puerto. Lo que el bloqueo reserva es la privacidad de una persona, no la " +
                "existencia de una estantería.",

            ["Terceros.Application -> Bastion.Catalogo.Contracts"] =
                "el quinto, y la mitad de VUELTA del primero mutuo. Un tercero puede tener " +
                "asignada la tarifa con la que se le factura, y ese Guid es del esquema de " +
                "Catálogo: sin clave ajena, porque entre esquemas no se cruza (regla 4). Lo que " +
                "impide que ahí acabe una tarifa inventada, de otra empresa o caducada es " +
                "IConsultaDeTarifas, y también pregunta por el ESTADO: una tarifa cuya vigencia " +
                "terminó sigue resolviendo los precios de lo ya emitido y no se asigna hoy. Es " +
                "la retirada del ADR-0023 con un sujeto nuevo.",

            ["Terceros.Application -> Bastion.Organizacion.Contracts"] =
                "el segundo, y por la misma puerta. Un tercero pertenece a la empresa que lo " +
                "conoce, así que el alta pregunta a Organización si la empresa del claim " +
                "existe y sigue activa antes de colgarle una ficha. Sin esta pregunta, la " +
                "ausencia de clave ajena entre esquemas sería un agujero en vez de una frontera.",
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
            ["Bastion.Catalogo.Contracts.Catalogo.IConsultaDeArticulos"] =
                "LECTURA: si se pueden mover existencias contra un artículo, para quien guarde su " +
                "identificador — cada movimiento del libro de la fase 2 (§7.4). Contesta con " +
                "enumerado PROPIO y de tres valores, no con `EstadoDeMaestro`: «¿se almacena?» es " +
                "una tercera pregunta y no una tercera respuesta, y un `Servicio` contesta " +
                "`NoSeAlmacena`. No escribe, y no publica ni el código ni la descripción.",

            ["Bastion.Catalogo.Contracts.Catalogo.IConsultaDeTarifas"] =
                "LECTURA: en qué estado está una tarifa para una fecha, para quien guarde su " +
                "identificador — la tarifa asignada del tercero (§7.2). Es la PRIMERA puerta " +
                "que no publica Organización, y la mitad de vuelta del primer cruce mutuo. No " +
                "escribe.",

            ["Bastion.Organizacion.Contracts.Almacenes.IConsultaDeAlmacenes"] =
                "LECTURA: en qué estado está un almacén, para quien guarde su identificador — cada " +
                "movimiento de existencias apunta al suyo para siempre (§7.4). Es el PRIMER puerto " +
                "que contesta `SoloResuelveLoViejo` de algo BLOQUEADO, al revés que el de terceros: " +
                "lo que el bloqueo reserva es la privacidad de una persona, no la existencia de " +
                "una estantería (ADR-0037). Para verlo abre el ámbito declarado del art. 32 con su " +
                "motivo propio. No escribe.",

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

            ["Bastion.Organizacion.Contracts.Series.IConsultaDeSeries"] =
                "LECTURA: en qué estado está una serie de numeración, para quien guarde su " +
                "identificador — el ajuste de inventario, que desde el ítem 2.4 dice por qué serie " +
                "se numerará (R5, §7.4). Una serie CERRADA contesta `SoloResuelveLoViejo`: sigue " +
                "resolviendo los documentos que ya numeró y no entrega ni un número más. Es el " +
                "único puerto cuya respuesta NO es la garantía que sostiene al consumidor: quien " +
                "confirma vuelve a comprobar la serie entera dentro de la sentencia que toma el " +
                "número, en su transacción y con la fila bloqueada. No escribe, y no publica ni " +
                "el contador ni el formato.",

            ["Bastion.Organizacion.Contracts.Ubicaciones.IConsultaDeUbicaciones"] =
                "LECTURA: en qué estado está una ubicación DENTRO DE UN ALMACÉN, para quien guarde " +
                "los dos identificadores. Recibe los dos porque que la ubicación cuelgue de ese " +
                "almacén es una condición que quien pregunta no puede comprobar: las dos tablas " +
                "están en `organizacion` y ninguna consulta cruza esquemas (regla 4). La ubicación " +
                "hereda el estado de su almacén y el suyo propio solo puede empeorarlo (ADR-0037). " +
                "No escribe.",

            ["Bastion.Organizacion.Contracts.Unidades.IConsultaDeUnidadesDeMedida"] =
                "LECTURA: en qué estado está una unidad de medida, para quien guarde su " +
                "identificador — la unidad base del artículo (§7.3). No escribe.",

            ["Bastion.Terceros.Contracts.Terceros.IConsultaDeTerceros"] =
                "LECTURA: en qué estado está un tercero para un papel —se puede tratar en esta " +
                "empresa, hace ese papel— y cuáles de un conjunto se pueden tratar hoy, para " +
                "quien guarde su identificador: el proveedor de un artículo (§7.3). Lo bloqueado " +
                "y lo de otra empresa contestan que no existe. No escribe, y no publica ni un " +
                "dato de la ficha.",
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
            ["Ajuste.AlmacenId"] = new(
                "Almacen",
                Raiz + ".Organizacion.Contracts.Almacenes.IConsultaDeAlmacenes",
                "el nombre casa, y lo que hay que escribir es POR DÓNDE se comprueba. Lo pregunta " +
                "AbrirAjuste, y pregunta por el ESTADO: un almacén bloqueado por el art. 32 " +
                "contesta que solo resuelve lo viejo, así que no admite un ajuste nuevo y sus " +
                "movimientos de ayer se siguen leyendo (ADR-0037)."),

            ["Ajuste.AnulaAId"] = new(
                "Ajuste",
                "",
                "el primero de la lista que apunta a un documento del PROPIO módulo, y por eso " +
                "el puerto va vacío: no hay frontera que cruzar y sí hay clave ajena de verdad " +
                "—misma tabla, mismo esquema—, así que el motor garantiza que la fila apuntada " +
                "existe. No casa por nombre porque lo que nombra es el papel: «a quién anulo». " +
                "Lo que la clave ajena NO dice —que esa fila esté anulada, que no haya dos " +
                "inversos del mismo original, que ningún anulado se quede sin quien le apunte— " +
                "lo sostiene LaDobleFlechaDeLaAnulacionTests, porque son afirmaciones sobre " +
                "parejas de filas y sobre un estado."),

            ["Ajuste.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "de la misma familia que el del artículo y el del tercero: un ajuste es de la " +
                "empresa que lo firma. Sale del claim en AbrirAjuste, nunca de la petición."),

            ["Ajuste.SerieId"] = new(
                "Serie",
                Raiz + ".Organizacion.Contracts.Series.IConsultaDeSeries",
                "el nombre casa, y lo que hay que escribir es POR DÓNDE se comprueba. Lo pregunta " +
                "AbrirAjuste, y pregunta por el ESTADO: una serie cerrada resuelve los documentos " +
                "que ya numeró y no admite uno nuevo. Con una diferencia que no tiene ningún otro " +
                "identificador de esta lista: aquí el puerto NO es lo que sostiene la invariante. " +
                "La R5 la sostiene el `WHERE` de la sentencia que toma el número al confirmar, " +
                "que vuelve a exigir las tres cosas —existe, es de esta empresa, sigue activa— " +
                "dentro de la transacción del documento. El puerto está porque una serie se puede " +
                "cerrar mientras el borrador espera, y sin él el alta dejaría nacer un borrador " +
                "apuntando a nada."),

            ["Articulo.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "tercero de la misma familia que el de la membresía y el del tercero: el nombre " +
                "casa, y lo que hay que escribir es POR DÓNDE se comprueba. Lo pregunta " +
                "CrearArticulo antes de construir el agregado."),

            ["Articulo.ImpuestoPorDefectoId"] = new(
                "Impuesto",
                Raiz + ".Organizacion.Contracts.Impuestos.IConsultaDeImpuestos",
                "el papel va en el nombre —«el que se propone si no se dice otro»— y por eso no " +
                "casa con el del tipo. Y el puerto no contesta «existe» sino en qué ESTADO está " +
                "el tramo para una fecha de devengo: un impuesto derogado sigue resolviendo las " +
                "facturas de cuando regía y no se ofrece para un artículo nuevo."),

            ["Articulo.UnidadBaseId"] = new(
                "UnidadMedida",
                Raiz + ".Organizacion.Contracts.Unidades.IConsultaDeUnidadesDeMedida",
                "el caso que el ADR-0024 anunció por su nombre antes de que existiera: «se " +
                "llamará UnidadBaseId y tampoco casará». No casa, en efecto, y por eso está " +
                "escrito. Se valida por el estado, igual que el impuesto."),

            ["ArticuloProveedor.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "gemelo del de la línea de tarifa: el suministro lleva la empresa aunque su " +
                "artículo ya la lleve, porque el filtro de la R8 se escribe por entidad y se " +
                "evalúa sobre las columnas de la fila. Y aquí importa el doble, porque lo que " +
                "saldría de otra empresa es con quién trabaja la competencia. Sale del claim en " +
                "AgregarProveedorAlArticulo, que es donde se comprueba la empresa activa."),

            ["ArticuloProveedor.TerceroId"] = new(
                "Tercero",
                Raiz + ".Terceros.Contracts.Terceros.IConsultaDeTerceros",
                "el cruce del ítem 1.10, y el primero de Catálogo que NO va a Organización. Sin " +
                "clave ajena y sin poder tenerla: el tercero vive en el esquema `terceros` " +
                "(regla 4 del §5). Lo que impide que ahí acabe un identificador inventado, el de " +
                "otra empresa o el de alguien con los datos reservados por el art. 32 es que " +
                "AgregarProveedorAlArticulo lo pregunte. Y pregunta por el ESTADO y por el " +
                "PAPEL: un tercero que no es proveedor no lo es por estar en esta tabla, y de " +
                "uno bloqueado no sale nada más que que está bloqueado."),

            ["Categoria.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "gemelo del del artículo: el árbol de clasificación es de la empresa que lo " +
                "monta, no del sistema. Lo comprueba CrearCategoria contra el mismo puerto."),

            ["Categoria.PadreId"] = new(
                "Categoria",
                "",
                "apunta a otra categoría del mismo árbol y el nombre dice el papel —«de quién " +
                "cuelga»— y no el tipo, así que ninguna heurística por nombre lo resuelve. " +
                "Mismo módulo y mismo esquema: aquí sí hay clave ajena, y encima una restricción " +
                "CHECK que impide que una categoría sea su propia madre. Lo que la base de datos " +
                "no puede ver —un ciclo de dos o más eslabones— lo comprueba " +
                "ElArbolSigueSiendoUnArbol, en el alta y en la modificación."),

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

            ["LineaDeAjuste.ArticuloId"] = new(
                "Articulo",
                Raiz + ".Catalogo.Contracts.Catalogo.IConsultaDeArticulos",
                "el nombre casa; lo que se declara es el puerto. Y este no contesta «¿existe?» " +
                "sino si contra ese artículo se pueden mover existencias: un Servicio existe y " +
                "no se almacena, y ponerle un movimiento sería inventarle un stock que nadie " +
                "puede contar."),

            ["LineaDeAjuste.UbicacionId"] = new(
                "Ubicacion",
                Raiz + ".Organizacion.Contracts.Ubicaciones.IConsultaDeUbicaciones",
                "la ubicación va en la LÍNEA y el almacén en la CABECERA, así que un ajuste " +
                "mueve varias estanterías del mismo almacén de una vez. El puerto resuelve las " +
                "dos cosas a la vez —que la ubicación cuelgue de ese almacén, y el peor de los " +
                "dos estados—, que es justo lo que decidió el ADR-0037."),

            ["LineaDeAjuste.UnidadIntroducidaId"] = new(
                "UnidadMedida",
                Raiz + ".Organizacion.Contracts.Unidades.IConsultaDeUnidadesDeMedida",
                "el papel va en el nombre —«aquella en la que lo escribió la persona»— y por eso " +
                "no casa con el del tipo. Hermano del UnidadBaseId del artículo, y por el mismo " +
                "puerto: se valida por el ESTADO, porque una unidad retirada sigue explicando las " +
                "líneas viejas y no se ofrece para una nueva (ADR-0023)."),

            ["LineaTarifa.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "una línea lleva la empresa aunque su tarifa ya la lleve, como la ubicación " +
                "respecto de su almacén: el filtro de la R8 se escribe por entidad y se evalúa " +
                "sobre las columnas de la fila, así que sin esta columna bastaría una consulta " +
                "que empezara por las líneas para que salieran las de otra empresa. La comprueba " +
                "CrearLineaTarifa contra el mismo puerto que todos los demás."),

            ["Membresia.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "el nombre SÍ casa, y aun así se declara: lo que la lista aporta aquí no es " +
                "descubrirlo, es decir POR DÓNDE se valida. Sin el puerto escrito, la regla sabría " +
                "que hay un cruce y no podría exigir que alguien lo compruebe."),

            ["MovimientoStock.AlmacenId"] = new(
                "Almacen",
                Raiz + ".Organizacion.Contracts.Almacenes.IConsultaDeAlmacenes",
                "la fila del libro REPITE el almacén de su documento, y no es redundancia: el " +
                "libro tiene que explicarse solo —un saldo se calcula sumando sus filas, sin " +
                "visitar el documento de cada una— y la fila de hace dos años no puede cambiar " +
                "porque alguien corrija una cabecera hoy. Quien pregunta es el alta del ajuste, " +
                "una vez y no una por línea."),

            ["MovimientoStock.ArticuloId"] = new(
                "Articulo",
                Raiz + ".Catalogo.Contracts.Catalogo.IConsultaDeArticulos",
                "gemelo del de la línea del ajuste, y por el mismo puerto. Es el único cruce del " +
                "libro que no va a Organización."),

            ["MovimientoStock.DocumentoOrigenId"] = new(
                "",
                "",
                "NO APUNTA A UN TIPO, y por eso va vacío en las dos casillas: cuál es el tipo lo " +
                "dice la columna de al lado, DocumentoOrigenTipo, así que hoy es un Ajuste y " +
                "mañana será un recuento o una recepción. Declararlo apuntando a «Ajuste» sería " +
                "mentira el día que haya un segundo documento, y ese día nadie vendría a " +
                "corregirlo. Que la flecha se sostenga no lo comprueba un puerto: lo comprueba la " +
                "R13 en los dos sentidos —ningún movimiento sin documento, ningún ajuste " +
                "confirmado sin movimientos—, y el documento vive en ESTE módulo, así que no hay " +
                "frontera que cruzar."),

            ["MovimientoStock.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "gemelo del de la cabecera del ajuste, repetido en la fila por lo mismo que el " +
                "almacén y por algo más: el filtro de la R8 se evalúa sobre las columnas de la " +
                "fila, y sin esta columna una consulta que empezara por el libro sumaría las " +
                "existencias de dos empresas de la misma instalación en un mismo saldo."),

            ["MovimientoStock.UbicacionId"] = new(
                "Ubicacion",
                Raiz + ".Organizacion.Contracts.Ubicaciones.IConsultaDeUbicaciones",
                "gemelo del de la línea del ajuste, y por el mismo puerto."),

            ["MovimientoStock.UnidadIntroducidaId"] = new(
                "UnidadMedida",
                Raiz + ".Organizacion.Contracts.Unidades.IConsultaDeUnidadesDeMedida",
                "gemelo del de la línea del ajuste. La fila guarda ADEMÁS el factor con el que se " +
                "pasó a la unidad base, precisamente para no tener que volver a preguntar: una " +
                "conversión se puede corregir mañana y la cantidad de ayer no puede cambiar de " +
                "valor por eso."),

            ["Tarifa.DivisaId"] = new(
                "Divisa",
                Raiz + ".Organizacion.Contracts.Divisas.IConsultaDeDivisas",
                "el cruce del ítem 1.9, y el que estrena el puerto que el 1.2 declaró «para la " +
                "tarifa del §7.3» y se quedó sin consumidor dos fases. No hay clave ajena y no " +
                "puede haberla: la divisa vive en el esquema de Organización y la tarifa en el " +
                "de Catálogo (regla 4 del §5), así que lo único que impide una tarifa en una " +
                "divisa inventada es que CrearTarifa lo pregunte. Y pregunta por el ESTADO, no " +
                "por la existencia: una divisa retirada sigue resolviendo las tarifas abiertas " +
                "cuando se usaba y no se ofrece para una nueva (ADR-0023)."),

            ["Tarifa.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "gemelo del del artículo y del de la categoría: una lista de precios es de la " +
                "empresa que la fija. Lo pregunta CrearTarifa antes de construir el agregado."),

            ["Tercero.EmpresaId"] = new(
                "Empresa",
                Raiz + ".Organizacion.Contracts.Empresas.IConsultaDeEmpresas",
                "gemelo del de la membresía, y por el mismo motivo: el nombre casa, pero lo que " +
                "hace falta escribir es POR DÓNDE se comprueba. Lo valida CrearTercero contra el " +
                "puerto antes de construir el agregado, porque un tercero colgado de una empresa " +
                "que no existe o que está bloqueada es una ficha que nadie va a volver a ver."),

            ["Tercero.TarifaAsignadaId"] = new(
                "Tarifa",
                Raiz + ".Catalogo.Contracts.Catalogo.IConsultaDeTarifas",
                "la mitad de vuelta del cruce mutuo del ítem 1.10, y el segundo caso —tras " +
                "TokenDeRefresco.EmpresaActivaId y Articulo.UnidadBaseId— en que la heurística " +
                "por nombre NO lo ve: se llama TarifaAsignadaId y no TarifaId, así que el " +
                "barrido lo sacó como HUÉRFANO y no como cruce. Esa es la infradetección que el " +
                "ADR-0024 documenta, y es la regla de los huérfanos —no la de los cruces— la " +
                "que obligó a escribir esta línea. Sin clave ajena: la tarifa vive en el esquema " +
                "`catalogo`. Se valida por el ESTADO y con la FECHA: una tarifa caducada sigue " +
                "resolviendo lo ya emitido y no se asigna hoy."),

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
