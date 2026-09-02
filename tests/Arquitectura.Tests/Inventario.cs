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
            ["Bastion.Organizacion.Contracts.Empresas.IConsultaDeEmpresas"] =
                "LECTURA: Identidad le pregunta a Organización si una empresa existe y no está " +
                "bloqueada, para poder emitir un testigo con ella dentro. No escribe.",
        };
}
