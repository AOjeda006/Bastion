using System.Reflection;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// La cuarta vía del ADR-0024: guardar el identificador de una fila de otro módulo sin nada que lo
/// valide.
/// </summary>
/// <remarks>
/// <para>
/// Hay cuatro maneras de que un módulo apunte a otro y <b>tres avisan</b>: la clave foránea entre
/// esquemas la rechaza el SQL; referenciar el <c>Domain</c> ajeno lo rechaza el compilador y
/// <c>LasCapasVanHaciaDentroTests</c>; referenciar su <c>Contracts</c> sin declarar el cruce lo
/// rechaza <c>LasFronterasEntreModulosTests</c>. La cuarta —una columna <c>uuid</c> con un valor
/// dentro— no la rechaza nadie: compila, migra, pasa los tests de arquitectura, pasa los de
/// integración y sirve peticiones. Lo único que no hace es garantizar que ese identificador
/// corresponda a algo que existe, y el día que no lo sea el fallo no aparece aquí sino en la
/// factura que lo usa, tres fases después.
/// </para>
/// <para>
/// <b>Dos fuentes independientes, y la comparación NO es una igualdad.</b> El descubrimiento por
/// nombre —<c>EmpresaId</c> → <c>Empresa</c>— no se olvida nunca pero <b>infradetecta por
/// diseño</b>: <c>TokenDeRefresco.EmpresaActivaId</c> apunta a una empresa desde el 0.5 y su nombre
/// no lo dice. La lista declarada tiene el agujero simétrico: solo sabe lo que alguien se acordó de
/// escribir. Así que lo que se exige es <b>contención</b> —todo lo descubierto está declarado— más
/// una simetría distinta: toda declaración sigue correspondiendo a una propiedad de verdad.
/// </para>
/// <para>
/// <b>Y una quinta afirmación que las cuatro anteriores no dan.</b> Con solo las cuatro, un
/// identificador ajeno con un nombre que no case —<c>DivisaPreferidaId</c>— y sin declarar sale
/// <b>verde</b>: el descubrimiento no lo ve y la lista no lo nombra, así que ninguna de las dos
/// fuentes tiene nada que decir. Es exactamente el hueco que la regla vino a cerrar. Por eso
/// <c>Ningun_identificador_del_dominio_se_queda_sin_clasificar</c> le da la vuelta a la carga de la
/// prueba: <b>todo</b> <c>Guid</c> acabado en <c>Id</c> tiene que decir a qué apunta, casando por
/// nombre o declarándose. Lo que no diga nada es rojo.
/// </para>
/// </remarks>
public sealed class LosIdentificadoresAjenosTests
{
    /// <summary>Un identificador encontrado en el dominio compilado.</summary>
    private sealed record Encontrado(string Tipo, string Propiedad, string Modulo)
    {
        /// <summary>Cómo se nombra en la lista declarada: <c>Membresia.EmpresaId</c>.</summary>
        internal string Clave => Tipo + "." + Propiedad;

        /// <summary>Lo que la heurística por nombre diría: <c>EmpresaId</c> → <c>Empresa</c>.</summary>
        internal string PorNombre => Propiedad[..^"Id".Length];
    }

    [Fact]
    public void Todo_identificador_de_otro_modulo_esta_declarado_con_su_puerto()
    {
        IReadOnlyList<string> sinDeclarar =
        [
            .. from cruce in Ajenos()
               where !Inventario.IdentificadoresDeclarados.TryGetValue(
                         cruce.Encontrado.Clave, out Inventario.Identificador? declarado)
                  || declarado.Puerto.Length == 0
               orderby cruce.Encontrado.Clave, StringComparer.Ordinal
               select $"{cruce.Encontrado.Modulo}.{cruce.Encontrado.Clave} apunta a " +
                      $"{cruce.Apunta}, que es de {cruce.Dueno}",
        ];

        // La contención, que es la afirmación 1: lo que la red por nombre caza y la lista no
        // nombra —o nombra sin puerto— es un identificador ajeno que nadie valida.
        sinDeclarar.ShouldBeEmpty(
            "hay identificadores de otro módulo sin declarar, o declarados sin decir por qué " +
            "puerto se validan. Un uuid guardado sin nadie que lo compruebe es la cuarta vía del " +
            "ADR-0024: compila, migra y sirve peticiones, y falla tres fases después:" +
            Environment.NewLine + Ensamblados.Enumerar(sinDeclarar));
    }

    [Fact]
    public void Toda_declaracion_sigue_correspondiendo_a_una_propiedad_del_dominio()
    {
        IReadOnlyDictionary<string, Encontrado> porClave = Identificadores()
            .ToDictionary(uno => uno.Clave, uno => uno, StringComparer.Ordinal);

        IReadOnlyList<string> tipos = NombresDeTipoDelDominio().Select(par => par.Key).ToList();

        List<string> rotas =
        [
            .. from declarado in Inventario.IdentificadoresDeclarados
               where !porClave.ContainsKey(declarado.Key)
               select $"{declarado.Key}: ya no hay ninguna propiedad Guid con ese nombre",

            .. from declarado in Inventario.IdentificadoresDeclarados
               where declarado.Value.Apunta.Length > 0
                  && !tipos.Contains(declarado.Value.Apunta, StringComparer.Ordinal)
               select $"{declarado.Key}: dice apuntar a «{declarado.Value.Apunta}», que no es " +
                      "ningún tipo del dominio",
        ];

        // La otra mitad de la simetría, que es la afirmación 2. Una entrada que sobrevive a su
        // motivo no es inofensiva: es una autorización concedida sobre algo que ya cambió, y
        // encima hace que la lista parezca más completa de lo que está.
        rotas.ShouldBeEmpty(
            "hay declaraciones que ya no corresponden a nada:" + Environment.NewLine +
            Ensamblados.Enumerar(rotas));
    }

    [Fact]
    public void Las_dos_fuentes_encuentran_algo()
    {
        IReadOnlyList<Encontrado> todos = Identificadores();
        ILookup<string, string> tipos = NombresDeTipoDelDominio();

        // Las tres maneras de que esta regla acabe comparando la nada, dichas una a una. Sin ellas
        // bastaría con que el filtro de propiedades dejara de casar —otro tipo, otro sufijo, otro
        // `BindingFlags`— para que todo lo demás saliera verde sin haber mirado un solo Guid.
        todos.ShouldNotBeEmpty(
            "no se ha encontrado ni un Guid acabado en Id en el dominio compilado, cuando no hay " +
            "una sola entidad del proyecto que no lleve alguno. El descubrimiento está roto, y " +
            "con él las cuatro afirmaciones que se apoyan en él");

        Inventario.IdentificadoresDeclarados.ShouldNotBeEmpty(
            "la lista declarada está vacía, así que la mitad que caza lo que el nombre no delata " +
            "no está cazando nada");

        todos.Count(uno => tipos.Contains(uno.PorNombre)).ShouldBeGreaterThan(
            0,
            "ningún identificador se resuelve por nombre, así que la heurística no está " +
            "funcionando y la lista declarada se ha quedado sola");

        Ajenos().ShouldNotBeEmpty(
            "no hay ningún identificador que cruce de módulo, así que esta regla no tiene sujeto: " +
            "saldría verde con un dominio limpio y con uno lleno de referencias sin validar");

        // Y la ambigüedad, que rompería la resolución por nombre en silencio: dos módulos con un
        // tipo del mismo nombre harían que `XId` apuntara a dos sitios y el desempate lo decidiera
        // el orden de carga de los ensamblados.
        IReadOnlyList<string> ambiguos =
        [
            .. from grupo in tipos
               where grupo.Distinct(StringComparer.Ordinal).Count() > 1
               orderby grupo.Key, StringComparer.Ordinal
               select $"{grupo.Key}: {string.Join(", ", grupo.Order(StringComparer.Ordinal))}",
        ];

        ambiguos.ShouldBeEmpty(
            "hay tipos de dominio con el mismo nombre en módulos distintos, así que resolver " +
            "«XId» por nombre ya no tiene una sola respuesta:" + Environment.NewLine +
            Ensamblados.Enumerar(ambiguos));
    }

    [Fact]
    public void Cada_modulo_de_la_lista_tiene_su_cruce_y_su_puerto()
    {
        IReadOnlyList<string> puertasDeclaradas = [.. Inventario.PuertasPublicas.Keys];

        List<string> faltan = [];

        foreach ((Encontrado encontrado, string apunta, string dueno) in Ajenos())
        {
            if (!Inventario.IdentificadoresDeclarados.TryGetValue(
                    encontrado.Clave, out Inventario.Identificador? declarado)
                || declarado.Puerto.Length == 0)
            {
                // Lo dice la afirmación 1; aquí no se repite el mismo rojo dos veces.
                continue;
            }

            if (!puertasDeclaradas.Contains(declarado.Puerto, StringComparer.Ordinal))
            {
                faltan.Add(
                    $"{encontrado.Clave}: su puerto «{declarado.Puerto}» no está entre las " +
                    "puertas públicas declaradas");
            }
            else if (!ExisteLaInterfaz(declarado.Puerto))
            {
                faltan.Add(
                    $"{encontrado.Clave}: su puerto «{declarado.Puerto}» no existe como interfaz " +
                    "pública en ningún Contracts compilado");
            }
            else if (!declarado.Puerto.StartsWith(
                Inventario.Raiz + "." + dueno + ".Contracts.", StringComparison.Ordinal))
            {
                faltan.Add(
                    $"{encontrado.Clave}: apunta a {apunta}, que es de {dueno}, y su puerto vive " +
                    $"en otro sitio: «{declarado.Puerto}»");
            }

            if (!HayCruceDeclarado(encontrado.Modulo, dueno))
            {
                faltan.Add(
                    $"{encontrado.Clave}: {encontrado.Modulo} guarda un identificador de {dueno} " +
                    $"y no tiene ningún cruce declarado hacia {Inventario.Raiz}.{dueno}.Contracts");
            }
        }

        // La afirmación 4, que es la que convierte esta regla en una protección y no en un
        // inventario: no basta con saber que el cruce existe; el puerto que lo valida tiene que
        // existir, ser el del dueño, estar declarado como puerta pública, y el cruce por el que se
        // llega hasta él tiene que estar autorizado.
        faltan.ShouldBeEmpty(
            "hay identificadores ajenos declarados cuyo puerto o cuyo cruce no se sostienen:" +
            Environment.NewLine + Ensamblados.Enumerar(faltan));
    }

    [Fact]
    public void Ningun_identificador_del_dominio_se_queda_sin_clasificar()
    {
        ILookup<string, string> tipos = NombresDeTipoDelDominio();

        IReadOnlyList<string> huerfanos =
        [
            .. from uno in Identificadores()
               where !tipos.Contains(uno.PorNombre)
                  && !Inventario.IdentificadoresDeclarados.ContainsKey(uno.Clave)
               orderby uno.Clave, StringComparer.Ordinal
               select $"{uno.Modulo}.{uno.Clave}",
        ];

        // La quinta afirmación, y la única que caza el caso que las otras cuatro dejan pasar: un
        // identificador ajeno con un nombre que no case y sin declarar. Sin esto, la regla sería
        // la que ya había —la heurística por nombre— con más pasos.
        huerfanos.ShouldBeEmpty(
            "hay Guid acabados en Id que no dicen a qué apuntan: ni casan con un tipo del dominio " +
            "ni están declarados. Si apuntan a algo, hay que decir a qué; si no apuntan a nada, " +
            "hay que decirlo también, porque un identificador sin dueño es indistinguible de uno " +
            "que se le olvidó a alguien:" + Environment.NewLine + Ensamblados.Enumerar(huerfanos));
    }

    /// <summary>
    /// Los identificadores que cruzan de módulo: los que apuntan a un tipo que vive en otro.
    /// </summary>
    private static IReadOnlyList<(Encontrado Encontrado, string Apunta, string Dueno)> Ajenos()
    {
        ILookup<string, string> tipos = NombresDeTipoDelDominio();

        List<(Encontrado, string, string)> ajenos = [];

        foreach (Encontrado uno in Identificadores())
        {
            // La declaración manda sobre la heurística: es la que sabe lo que el nombre no dice.
            string apunta =
                Inventario.IdentificadoresDeclarados.TryGetValue(
                    uno.Clave, out Inventario.Identificador? declarado)
                    ? declarado.Apunta
                    : uno.PorNombre;

            if (apunta.Length == 0 || !tipos.Contains(apunta))
            {
                continue;
            }

            string dueno = tipos[apunta].First();

            if (!string.Equals(dueno, uno.Modulo, StringComparison.Ordinal))
            {
                ajenos.Add((uno, apunta, dueno));
            }
        }

        return [.. ajenos.OrderBy(uno => uno.Item1.Clave, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Todo <c>Guid</c> —o <c>Guid?</c>— acabado en <c>Id</c> de los tipos del dominio compilado.
    /// </summary>
    /// <remarks>
    /// Se recorren <b>todos</b> los tipos del dominio y no solo los agregados, y hace falta: los
    /// dos identificadores ajenos que hay hoy viven en <c>Membresia</c> y en <c>TokenDeRefresco</c>,
    /// que son <b>entidades hijas</b> y no agregados. Una regla que solo mirara agregados no habría
    /// visto ninguno de los dos.
    /// </remarks>
    private static IReadOnlyList<Encontrado> Identificadores() =>
    [
        .. from clave in EnsambladosDeDominio()
           from tipo in Ensamblados.Todos[clave].GetTypes()
           where tipo.IsClass
           from propiedad in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
           where (propiedad.PropertyType == typeof(Guid)
                   || propiedad.PropertyType == typeof(Guid?))
              && propiedad.Name.EndsWith("Id", StringComparison.Ordinal)
              && !string.Equals(propiedad.Name, "Id", StringComparison.Ordinal)
           orderby tipo.Name + "." + propiedad.Name, StringComparer.Ordinal
           select new Encontrado(tipo.Name, propiedad.Name, clave.Split('.')[0]),
    ];

    /// <summary>Los tipos del dominio compilado, por nombre, con el módulo en el que viven.</summary>
    private static ILookup<string, string> NombresDeTipoDelDominio() =>
        (from clave in EnsambladosDeDominio()
         from tipo in Ensamblados.Todos[clave].GetTypes()
         where tipo.IsClass && !tipo.IsAbstract
         select new { tipo.Name, Modulo = clave.Split('.')[0] })
        .ToLookup(uno => uno.Name, uno => uno.Modulo, StringComparer.Ordinal);

    /// <summary>Los ensamblados de dominio del alcance: los de módulo con tipos, y el común.</summary>
    private static IReadOnlyList<string> EnsambladosDeDominio() =>
    [
        .. from clave in Ensamblados.Todos.Keys
           where clave.EndsWith(".Domain", StringComparison.Ordinal)
              && (Inventario.EnsambladosConTipos.Contains(clave)
                  || Inventario.ComunesConTipos.Contains(clave))
           orderby clave, StringComparer.Ordinal
           select clave,
    ];

    /// <summary>Si esa interfaz pública existe de verdad en algún <c>Contracts</c> compilado.</summary>
    private static bool ExisteLaInterfaz(string nombreCompleto) =>
        Ensamblados.ClavesConTipos("Contracts")
            .Select(clave => Ensamblados.Todos[clave])
            .SelectMany(ensamblado => ensamblado.GetTypes())
            .Any(tipo => tipo.IsInterface
                && tipo.IsPublic
                && string.Equals(tipo.FullName, nombreCompleto, StringComparison.Ordinal));

    /// <summary>
    /// Si hay un cruce declarado desde cualquier capa de <paramref name="consumidor"/> hacia el
    /// <c>Contracts</c> de <paramref name="dueno"/>.
    /// </summary>
    private static bool HayCruceDeclarado(string consumidor, string dueno) =>
        Inventario.CrucesDeclarados.Keys.Any(cruce =>
            cruce.StartsWith(consumidor + ".", StringComparison.Ordinal)
            && cruce.EndsWith(
                "-> " + Inventario.Raiz + "." + dueno + ".Contracts", StringComparison.Ordinal));
}
