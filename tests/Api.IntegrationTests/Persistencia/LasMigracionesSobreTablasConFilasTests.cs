using System.Globalization;
using System.Reflection;
using Bastion.Auditoria.Infrastructure.Persistencia;
using Bastion.Catalogo.Infrastructure.Persistencia;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Terceros.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// Las migraciones aplicadas UNA A UNA, en el orden en que se escribieron, sobre una base en la
/// que cada tabla ya tiene filas cuando le llega la siguiente.
/// </summary>
/// <remarks>
/// <para>
/// <b>Entra en el ítem 1.12, y el hueco que tapa es de los que no se ven desde dentro.</b> Hasta
/// aquí, todas las migraciones del repositorio se habían aplicado siempre sobre tablas vacías: el
/// contenedor de los tests las aplica de golpe a una base recién creada, y el humo de la CI
/// también. Una instalación de verdad no se encuentra eso nunca a partir del segundo despliegue.
/// Una columna <c>NOT NULL</c> añadida sin valor para las filas que ya están migra en verde en
/// todas las bases que la CI levanta y revienta en la primera que tenga un cliente dentro —y
/// revienta en el <c>migraciones</c> del compose, así que la API ni siquiera arranca—.
/// </para>
/// <para>
/// <b>El estado viejo se construye, no se recupera.</b> No se instala una versión anterior de
/// Bastion: se recorre la historia de migraciones de los cinco contextos entrelazada por su marca
/// de tiempo —que es lo que habría visto una instalación desplegada en cada commit— y antes de
/// cada paso se inventa una fila en cada tabla que exista en ese momento. Cuesta una base y unos
/// segundos, y no depende de que ninguna imagen vieja siga publicada.
/// </para>
/// <para>
/// <b>Las filas son inventadas y eso tiene un límite escrito.</b> Cumplen los tipos, las claves
/// ajenas y las restricciones <c>CHECK</c>, y nada más: una migración cuyo relleno dependa de que
/// los datos tengan sentido de negocio podría pasar aquí y fallar con datos reales, o al revés.
/// Por eso las <c>CHECK</c> no se sortean a ciegas: cada una está nombrada abajo con lo que la fila
/// necesita para cumplirla, y una que aparezca sin estar nombrada pone esto rojo antes de intentar
/// la fila — que es el momento en que alguien decide qué valor es verosímil.
/// </para>
/// </remarks>
/// <param name="postgres">El servidor compartido; el recorrido se hace en una base propia.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LasMigracionesSobreTablasConFilasTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>
    /// Los contextos que migran, con cómo se abren para hacerlo. Que no falte ninguno lo comprueba
    /// <see cref="Recorre_todos_los_contextos_que_tienen_migraciones"/> contra los ensamblados.
    /// </summary>
    private static readonly Contexto[] s_contextos =
    [
        new(typeof(AuditoriaDbContext), cadena => new AuditoriaDbContext(
            Opciones<AuditoriaDbContext>(cadena, AuditoriaDbContext.Configurar), new InquilinoFijo(null), new AccesoCerrado())),
        new(typeof(OrganizacionDbContext), cadena => new OrganizacionDbContext(
            Opciones<OrganizacionDbContext>(cadena, OrganizacionDbContext.Configurar), new InquilinoFijo(null), new AccesoCerrado())),
        new(typeof(IdentidadDbContext), cadena => new IdentidadDbContext(
            Opciones<IdentidadDbContext>(cadena, IdentidadDbContext.Configurar), new InquilinoFijo(null), new AccesoCerrado())),
        new(typeof(TercerosDbContext), cadena => new TercerosDbContext(
            Opciones<TercerosDbContext>(cadena, TercerosDbContext.Configurar), new InquilinoFijo(null), new AccesoCerrado())),
        new(typeof(CatalogoDbContext), cadena => new CatalogoDbContext(
            Opciones<CatalogoDbContext>(cadena, CatalogoDbContext.Configurar), new InquilinoFijo(null), new AccesoCerrado())),
        new(typeof(InventarioDbContext), cadena => new InventarioDbContext(
            Opciones<InventarioDbContext>(cadena, InventarioDbContext.Configurar), new InquilinoFijo(null), new AccesoCerrado())),
    ];

    /// <summary>
    /// Cada restricción <c>CHECK</c> de la base, con lo que una fila inventada necesita para
    /// cumplirla. Las que no necesitan nada también están, y a propósito: la lista se compara
    /// entera en los dos sentidos con las que el recorrido encuentra.
    /// </summary>
    private static readonly Dictionary<string, Relleno> s_restricciones = new(StringComparer.Ordinal)
    {
        ["ck_articulos_tipo"] = new([], [("tipo", "'Bien'")]),
        ["ck_bandeja_empresa_o_motivo"] = new(["empresa_id"], []),
        ["ck_categorias_padre_distinto_de_si_misma"] = Relleno.Ninguno,
        ["ck_condiciones_pago_plazo_legal"] = new([], [("dias_de_plazo", "30")]),
        ["ck_lineas_tarifa_articulo_o_categoria"] = new(["articulo_id"], []),
        ["ck_lineas_tarifa_cantidad_desde_no_negativa"] = Relleno.Ninguno,
        ["ck_lineas_tarifa_descuento_en_rango"] = Relleno.Ninguno,
        ["ck_lineas_tarifa_precio_no_negativo"] = Relleno.Ninguno,
        ["ck_lineas_tarifa_precio_o_descuento"] = new(["precio"], []),
        ["ck_movimiento_stock_cantidad_no_nula"] = Relleno.Ninguno,
        ["ck_movimiento_stock_cantidad_por_factor"] = Relleno.Ninguno,
        ["ck_registros_empresa_o_motivo"] = new(["empresa_id"], []),
        ["ck_tarifas_vigencia_no_invertida"] = Relleno.Ninguno,
        ["ck_terceros_limite_credito_completo"] = Relleno.Ninguno,
        ["ck_terceros_territorio_fiscal"] = new([], [("territorio_fiscal", "'PeninsulaYBaleares'")]),
    };

    [Fact]
    public async Task Una_a_una_y_sobre_tablas_con_filas_ninguna_falla_ni_se_lleva_una_fila()
    {
        Recorrido recorrido = await RecorrerAsync(conFilas: true);

        recorrido.Fallos.ShouldBeEmpty(
            "una instalación con datos no pasaría de aquí: " + string.Join(" | ", recorrido.Fallos));
        recorrido.Aplicadas.ShouldBe(recorrido.Escritas, "no se aplicaron todas las migraciones escritas");

        // Y las CHECK nombradas, en el otro sentido: una declarada que el recorrido no encuentra es
        // un valor que ya no protege nada, o una restricción que se ha quedado sin nadie que la
        // conozca por su nombre.
        List<string> sinRestriccion = [.. s_restricciones.Keys.Except(recorrido.Restricciones).Order(StringComparer.Ordinal)];
        sinRestriccion.ShouldBeEmpty(
            "estas restricciones están nombradas y no hay ninguna tabla que las tenga: " + string.Join(", ", sinRestriccion));
    }

    [Fact]
    public async Task Y_sobre_tablas_vacias_se_aplican_igual_que_en_la_base_de_los_tests()
    {
        // El contraste. Es el único camino que se había ejercido hasta el 1.12, y está aquí para
        // que una migración que solo falla con filas se vea roja en un caso y verde en este: si
        // los dos se pusieran rojos a la vez, lo roto sería el recorrido y no la migración.
        Recorrido recorrido = await RecorrerAsync(conFilas: false);

        recorrido.Fallos.ShouldBeEmpty(string.Join(" | ", recorrido.Fallos));
        recorrido.Aplicadas.ShouldBe(recorrido.Escritas);
    }

    [Fact]
    public void Recorre_todos_los_contextos_que_tienen_migraciones()
    {
        string directorio = AppContext.BaseDirectory;

        // El universo son las migraciones compiladas, no la lista de arriba: un módulo nuevo con
        // su contexto que nadie añadiera aquí se quedaría fuera del recorrido y con él sus tablas,
        // que son justo las que no tendrían a nadie mirándolas.
        List<string> conMigraciones =
        [
            .. Directory.EnumerateFiles(directorio, "Bastion.*.Infrastructure.dll")
                .Select(Assembly.LoadFrom)
                .SelectMany(ensamblado => ensamblado.GetTypes())
                .Where(tipo => tipo.IsSubclassOf(typeof(Migration)))
                .Select(tipo => tipo.GetCustomAttribute<DbContextAttribute>()?.ContextType.FullName)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        conMigraciones.ShouldNotBeEmpty(
            $"no hay ni una migración en los Bastion.*.Infrastructure.dll de {directorio}: lo roto es el descubrimiento");
        conMigraciones.ShouldBe([.. s_contextos.Select(contexto => contexto.Tipo.FullName!).Order(StringComparer.Ordinal)]);
    }

    private async Task<Recorrido> RecorrerAsync(bool conFilas)
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);

        List<(string Id, Contexto Contexto)> escritas = EnElOrdenEnQueSeEscribieron(cadena);
        HashSet<string> historiales = Historiales(cadena);
        FilasInventadas inventadas = new();
        List<string> fallos = [];
        HashSet<string> restricciones = new(StringComparer.Ordinal);
        int aplicadas = 0;

        foreach ((string id, Contexto contexto) in escritas)
        {
            Dictionary<string, long> antes;

            await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
            {
                IReadOnlyList<Tabla> tablas = await LeerTablasAsync(conexion, historiales);

                if (conFilas
                    && !await InventarUnaFilaEnCadaTablaAsync(
                        conexion, tablas, inventadas, restricciones, fallos, $"antes de {id}"))
                {
                    return new(fallos, restricciones, aplicadas, escritas.Count);
                }

                antes = await ContarAsync(conexion, tablas);
            }

            // El arnés del arnés: sin esto, un relleno que no insertara nada daría el mismo verde
            // que el camino de siempre, y el caso con filas no probaría nada que no probara el otro.
            List<string> malRellenas = [.. antes.Where(tabla => conFilas ? tabla.Value == 0 : tabla.Value > 0).Select(tabla => tabla.Key)];

            if (malRellenas.Count > 0)
            {
                fallos.Add(
                    $"antes de {id} {(conFilas ? "estaban vacías" : "tenían filas")}: {string.Join(", ", malRellenas)}");
                return new(fallos, restricciones, aplicadas, escritas.Count);
            }

            try
            {
                await using DbContext abierto = contexto.Abrir(cadena);
                await abierto.GetService<IMigrator>().MigrateAsync(id);
            }
            catch (PostgresException error)
            {
                fallos.Add($"{id} no se aplica {(conFilas ? "con filas" : "sin filas")}: {error.Message}");
                return new(fallos, restricciones, aplicadas, escritas.Count);
            }

            aplicadas++;

            await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
            {
                Dictionary<string, long> despues = await ContarAsync(conexion, await LeerTablasAsync(conexion, historiales));

                foreach ((string tabla, long filas) in antes)
                {
                    if (!despues.TryGetValue(tabla, out long quedan))
                    {
                        fallos.Add($"{id} se ha llevado la tabla {tabla} con {filas} filas dentro");
                    }
                    else if (quedan < filas)
                    {
                        fallos.Add($"{id} ha dejado {tabla} con {quedan} filas de {filas}");
                    }
                }
            }
        }

        // LA RONDA DE DESPUÉS, y tapa un agujero que solo se ve cuando se pregunta a quién no le
        // toca nunca: a las tablas de la ÚLTIMA migración escrita no les viene ningún paso detrás,
        // así que el recorrido de arriba no les inventa una fila jamás. Sus CHECK no se miran, sus
        // columnas no se rellenan y su nombre no entra en `restricciones` — de modo que una
        // restricción nombrada aquí para ellas saldría «declarada y sin dueño» hasta que otro
        // módulo migrara detrás, que es un rojo que llega tarde y en un ítem que no es el suyo.
        //
        // No prueba ninguna migración —no hay ninguna después—, y no es para eso: es lo que hace
        // que el censo de tablas y la lista de CHECK se comparen contra la base COMPLETA, que es la
        // forma que tiene una instalación de verdad.
        if (conFilas)
        {
            await using NpgsqlConnection conexion = await AbrirAsync(cadena);
            IReadOnlyList<Tabla> tablas = await LeerTablasAsync(conexion, historiales);

            if (!await InventarUnaFilaEnCadaTablaAsync(
                    conexion, tablas, inventadas, restricciones, fallos, "con todo migrado"))
            {
                return new(fallos, restricciones, aplicadas, escritas.Count);
            }

            Dictionary<string, long> llenas = await ContarAsync(conexion, tablas);
            List<string> vacias = [.. llenas.Where(tabla => tabla.Value == 0).Select(tabla => tabla.Key)];

            if (vacias.Count > 0)
            {
                fallos.Add($"con todo migrado seguían vacías: {string.Join(", ", vacias)}");
            }
        }

        return new(fallos, restricciones, aplicadas, escritas.Count);
    }

    /// <summary>Una fila inventada en cada tabla del censo, padres primero.</summary>
    /// <returns><c>false</c> si algo falló; el motivo queda en <paramref name="fallos"/>.</returns>
    private static async Task<bool> InventarUnaFilaEnCadaTablaAsync(
        NpgsqlConnection conexion,
        IReadOnlyList<Tabla> tablas,
        FilasInventadas inventadas,
        HashSet<string> restricciones,
        List<string> fallos,
        string momento)
    {
        foreach (Tabla tabla in PadresPrimero(tablas))
        {
            restricciones.UnionWith(tabla.Restricciones);

            List<string> desconocidas = [.. tabla.Restricciones.Where(nombre => !s_restricciones.ContainsKey(nombre))];

            if (desconocidas.Count > 0)
            {
                fallos.Add(
                    $"{momento}, {tabla.Nombre} tiene restricciones CHECK sin relleno nombrado: " +
                    string.Join(", ", desconocidas));
                return false;
            }

            try
            {
                await using NpgsqlCommand fila = new(inventadas.Insercion(tabla, s_restricciones), conexion);
                await fila.ExecuteNonQueryAsync();
            }
            catch (PostgresException error)
            {
                fallos.Add($"{momento}, no se pudo inventar una fila en {tabla.Nombre}: {error.Message}");
                return false;
            }
        }

        return true;
    }

    // Por la marca de tiempo del identificador, que es global: las cinco historias entrelazadas son
    // lo que habría visto una instalación desplegada en cada commit, con el migrador del compose
    // aplicando lo pendiente de cada contexto.
    private static List<(string Id, Contexto Contexto)> EnElOrdenEnQueSeEscribieron(string cadena)
    {
        List<(string Id, Contexto Contexto)> todas = [];

        foreach (Contexto contexto in s_contextos)
        {
            using DbContext abierto = contexto.Abrir(cadena);
            todas.AddRange(abierto.Database.GetMigrations().Select(id => (id, contexto)));
        }

        return [.. todas.OrderBy(migracion => migracion.Id, StringComparer.Ordinal)];
    }

    // Las tablas del historial no son datos: son de EF Core, y una fila inventada en ellas sería una
    // migración que nadie escribió. Se preguntan al contexto y no se teclean, porque aquí se llaman
    // como decidió cada módulo y no como las llama EF Core por omisión.
    private static HashSet<string> Historiales(string cadena)
    {
        HashSet<string> historiales = new(StringComparer.Ordinal);

        foreach (Contexto contexto in s_contextos)
        {
            using DbContext abierto = contexto.Abrir(cadena);
            var opciones = RelationalOptionsExtension.Extract(abierto.GetService<IDbContextOptions>());

            historiales.Add($"{opciones.MigrationsHistoryTableSchema ?? abierto.Model.GetDefaultSchema() ?? "public"}.{opciones.MigrationsHistoryTableName ?? "__EFMigrationsHistory"}");
        }

        return historiales;
    }

    private static DbContextOptions<T> Opciones<T>(string cadena, Action<DbContextOptionsBuilder, string> configurar)
        where T : DbContext
    {
        DbContextOptionsBuilder<T> opciones = new();
        configurar(opciones, cadena);
        return opciones.Options;
    }

    private static async Task<NpgsqlConnection> AbrirAsync(string cadena)
    {
        NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();
        return conexion;
    }

    /// <summary>El censo de tablas de la base, con sus columnas, sus claves y sus CHECK.</summary>
    /// <remarks>
    /// <para>
    /// <b>Una partición no es una tabla, y eso hay que decírselo al censo.</b>
    /// <c>information_schema.tables</c> da <c>BASE TABLE</c> tanto al padre particionado como a
    /// cada una de sus particiones, así que sin el <c>NOT rel.relispartition</c> el recorrido ve
    /// las catorce particiones de <c>inventario.movimiento_stock</c> como catorce tablas
    /// independientes e intenta inventar una fila EN CADA UNA, por su nombre. Medido: la fila
    /// inventada lleva <c>current_date</c> en la clave de partición, así que entra en el padre y en
    /// la partición del mes en curso, y las otras doce la rechazan con
    /// <c>23514 violates partition constraint</c>.
    /// </para>
    /// <para>
    /// <b>Lo que cambia es el censo, no la denuncia.</b> La comprobación de abajo —que después de
    /// inventar filas no quede ni una tabla vacía— sigue siendo absoluta y sin salvedades: no lleva
    /// una lista de tablas perdonadas, que es justo lo que la habría convertido en un sitio donde
    /// esconder una tabla de verdad. Una partición no se salta la comprobación: es que no es un
    /// sitio donde se escriba, se escribe en el padre y el motor decide dónde cae. Contar y llenar
    /// el padre cubre a todas sus particiones, y por eso la exclusión no abre ningún hueco.
    /// </para>
    /// <para>
    /// Y no vale mirar solo el nombre: <c>rel.relname</c> se ata también al esquema por
    /// <c>pg_namespace</c>, porque dos esquemas pueden tener una tabla con el mismo nombre y el
    /// censo quedaría cruzado.
    /// </para>
    /// </remarks>
    /// <param name="conexion">Conexión abierta a la base del recorrido.</param>
    /// <param name="historiales">Las tablas de historial de migraciones, que no cuentan.</param>
    /// <returns>Las tablas del censo, ordenadas por nombre.</returns>
    private static async Task<IReadOnlyList<Tabla>> LeerTablasAsync(NpgsqlConnection conexion, HashSet<string> historiales)
    {
        Dictionary<string, Tabla> tablas = new(StringComparer.Ordinal);

        await using (NpgsqlCommand columnas = new(
            """
            SELECT c.table_schema, c.table_name, c.column_name, c.udt_name, c.character_maximum_length,
                   c.is_nullable = 'YES',
                   c.column_default IS NOT NULL OR c.is_identity = 'YES' OR c.is_generated = 'ALWAYS'
            FROM information_schema.columns AS c
            JOIN information_schema.tables AS t
              ON t.table_schema = c.table_schema AND t.table_name = c.table_name
            JOIN pg_catalog.pg_class AS rel
              ON rel.relname = c.table_name
            JOIN pg_catalog.pg_namespace AS esquema
              ON esquema.oid = rel.relnamespace AND esquema.nspname = c.table_schema
            WHERE t.table_type = 'BASE TABLE'
              AND NOT rel.relispartition
              AND c.table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY c.table_schema, c.table_name, c.ordinal_position
            """,
            conexion))
        await using (NpgsqlDataReader lector = await columnas.ExecuteReaderAsync())
        {
            while (await lector.ReadAsync())
            {
                string nombre = $"{lector.GetString(0)}.{lector.GetString(1)}";

                if (historiales.Contains(nombre))
                {
                    continue;
                }

                if (!tablas.TryGetValue(nombre, out Tabla? tabla))
                {
                    tabla = new(nombre, $"\"{lector.GetString(0)}\".\"{lector.GetString(1)}\"", [], [], []);
                    tablas.Add(nombre, tabla);
                }

                tabla.Columnas.Add(new(
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.IsDBNull(4) ? null : lector.GetInt32(4),
                    lector.GetBoolean(5),
                    lector.GetBoolean(6)));
            }
        }

        // Por los OID de `pg_constraint`, como `NingunaClaveAjenaCruzaDeEsquemaEnLaBaseTests`, y con
        // las columnas en el orden de la clave: una clave compuesta se rellena desde UNA fila del
        // padre, no desde una columna de cada una.
        await using (NpgsqlCommand claves = new(
            """
            SELECT o.nspname || '.' || tc.relname,
                   r.contype::text,
                   r.conname,
                   COALESCE(d.nspname || '.' || td.relname, ''),
                   ARRAY(SELECT a.attname::text FROM unnest(r.conkey) WITH ORDINALITY AS k(n, i)
                         JOIN pg_catalog.pg_attribute AS a ON a.attrelid = r.conrelid AND a.attnum = k.n ORDER BY k.i),
                   ARRAY(SELECT a.attname::text FROM unnest(COALESCE(r.confkey, '{}')) WITH ORDINALITY AS k(n, i)
                         JOIN pg_catalog.pg_attribute AS a ON a.attrelid = r.confrelid AND a.attnum = k.n ORDER BY k.i)
            FROM pg_catalog.pg_constraint AS r
            JOIN pg_catalog.pg_class AS tc ON tc.oid = r.conrelid
            JOIN pg_catalog.pg_namespace AS o ON o.oid = tc.relnamespace
            LEFT JOIN pg_catalog.pg_class AS td ON td.oid = r.confrelid
            LEFT JOIN pg_catalog.pg_namespace AS d ON d.oid = td.relnamespace
            WHERE r.contype IN ('f', 'c') AND o.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY r.conname
            """,
            conexion))
        await using (NpgsqlDataReader lector = await claves.ExecuteReaderAsync())
        {
            while (await lector.ReadAsync())
            {
                if (!tablas.TryGetValue(lector.GetString(0), out Tabla? tabla))
                {
                    continue;
                }

                if (lector.GetString(1) == "c")
                {
                    tabla.Restricciones.Add(lector.GetString(2));
                }
                else
                {
                    tabla.Claves.Add(new(lector.GetString(3), lector.GetFieldValue<string[]>(4), lector.GetFieldValue<string[]>(5)));
                }
            }
        }

        return [.. tablas.Values.OrderBy(tabla => tabla.Nombre, StringComparer.Ordinal)];
    }

    private static List<Tabla> PadresPrimero(IReadOnlyList<Tabla> tablas)
    {
        var porNombre = tablas.ToDictionary(tabla => tabla.Nombre, StringComparer.Ordinal);
        List<Tabla> orden = [];
        HashSet<string> puestas = new(StringComparer.Ordinal);
        HashSet<string> enCurso = new(StringComparer.Ordinal);

        foreach (Tabla tabla in tablas)
        {
            Poner(tabla);
        }

        return orden;

        void Poner(Tabla tabla)
        {
            if (puestas.Contains(tabla.Nombre))
            {
                return;
            }

            if (!enCurso.Add(tabla.Nombre))
            {
                throw new InvalidOperationException($"hay un ciclo de claves ajenas que pasa por {tabla.Nombre}");
            }

            foreach (ClaveAjena clave in tabla.Claves.Where(clave => clave.Destino != tabla.Nombre))
            {
                Poner(porNombre[clave.Destino]);
            }

            enCurso.Remove(tabla.Nombre);
            puestas.Add(tabla.Nombre);
            orden.Add(tabla);
        }
    }

    private static async Task<Dictionary<string, long>> ContarAsync(NpgsqlConnection conexion, IReadOnlyList<Tabla> tablas)
    {
        Dictionary<string, long> filas = new(StringComparer.Ordinal);

        foreach (Tabla tabla in tablas)
        {
            // El nombre sale del catálogo del motor, no de ninguna entrada.
            await using NpgsqlCommand cuenta = new($"SELECT count(*) FROM {tabla.Citada}", conexion);
            filas.Add(tabla.Nombre, (long)(await cuenta.ExecuteScalarAsync())!);
        }

        return filas;
    }

    private sealed record Contexto(Type Tipo, Func<string, DbContext> Abrir);

    private sealed record Columna(string Nombre, string Tipo, int? Longitud, bool AdmiteNulo, bool TieneValorPropio);

    private sealed record ClaveAjena(string Destino, string[] Columnas, string[] ColumnasDestino);

    private sealed record Tabla(string Nombre, string Citada, List<Columna> Columnas, List<ClaveAjena> Claves, List<string> Restricciones);

    private sealed record Recorrido(IReadOnlyList<string> Fallos, IReadOnlySet<string> Restricciones, int Aplicadas, int Escritas);

    /// <summary>Lo que una restricción CHECK necesita de la fila inventada.</summary>
    /// <param name="AunqueAdmitanNulo">Columnas anulables que hay que rellenar igualmente.</param>
    /// <param name="Valores">Columnas con un valor fijo, como literal SQL.</param>
    private sealed record Relleno(string[] AunqueAdmitanNulo, (string Columna, string Valor)[] Valores)
    {
        public static Relleno Ninguno { get; } = new([], []);
    }

    /// <summary>
    /// Inventa filas: tipos, claves ajenas y CHECK nombradas, y lo mínimo — las columnas anulables
    /// se quedan en nulo, que es el caso difícil para una migración que las vuelva obligatorias.
    /// </summary>
    private sealed class FilasInventadas
    {
        private int _siguiente = 1;

        public string Insercion(Tabla tabla, IReadOnlyDictionary<string, Relleno> rellenos)
        {
            Relleno[] aplicables = [.. tabla.Restricciones.Select(nombre => rellenos[nombre])];
            HashSet<string> forzadas = new(aplicables.SelectMany(relleno => relleno.AunqueAdmitanNulo), StringComparer.Ordinal);
            var fijos = aplicables.SelectMany(relleno => relleno.Valores)
                .ToDictionary(valor => valor.Columna, valor => valor.Valor, StringComparer.Ordinal);

            var columnas = tabla.Columnas.ToDictionary(columna => columna.Nombre, StringComparer.Ordinal);
            List<string> nombres = [];
            List<string> valores = [];
            List<string> padres = [];
            HashSet<string> cubiertas = new(StringComparer.Ordinal);

            // La autorreferencia se queda en nulo: la fila no tiene todavía a quién apuntar.
            foreach (ClaveAjena clave in tabla.Claves.Where(clave => clave.Destino != tabla.Nombre))
            {
                if (!clave.Columnas.Any(columna => !columnas[columna].AdmiteNulo || forzadas.Contains(columna)))
                {
                    continue;
                }

                string alias = "padre" + padres.Count.ToString(CultureInfo.InvariantCulture);
                string[] destino = [.. clave.ColumnasDestino.Select(Citar)];
                string[] citadaDestino = clave.Destino.Split('.');

                // La fila MÁS NUEVA del padre, que es la que este mismo paso acaba de inventar: los
                // padres van primero. Con una cualquiera, dos pasos apuntarían a la misma y un índice
                // único sobre la clave ajena —el de las conversiones de unidades— rechazaría la
                // segunda. El `xmin` más alto es la última escritura, y ninguna la hay después.
                padres.Add(
                    $"(SELECT {string.Join(", ", destino)} FROM {Citar(citadaDestino[0])}.{Citar(citadaDestino[1])} " +
                    $"ORDER BY xmin::text::bigint DESC LIMIT 1) AS {alias}");

                for (int i = 0; i < clave.Columnas.Length; i++)
                {
                    nombres.Add(Citar(clave.Columnas[i]));
                    valores.Add($"{alias}.{destino[i]}");
                    cubiertas.Add(clave.Columnas[i]);
                }
            }

            foreach (Columna columna in tabla.Columnas)
            {
                bool hayQueRellenarla = !columna.AdmiteNulo || forzadas.Contains(columna.Nombre) || fijos.ContainsKey(columna.Nombre);

                if (cubiertas.Contains(columna.Nombre) || columna.TieneValorPropio || !hayQueRellenarla)
                {
                    continue;
                }

                nombres.Add(Citar(columna.Nombre));
                valores.Add(fijos.TryGetValue(columna.Nombre, out string? fijo) ? fijo : Inventar(tabla, columna));
            }

            if (nombres.Count == 0)
            {
                return $"INSERT INTO {tabla.Citada} DEFAULT VALUES";
            }

            string desde = padres.Count == 0 ? string.Empty : " FROM " + string.Join(", ", padres);

            return $"INSERT INTO {tabla.Citada} ({string.Join(", ", nombres)}) SELECT {string.Join(", ", valores)}{desde}";
        }

        // Distinto en cada fila allí donde puede entrar en un índice único; fijo donde no.
        private string Inventar(Tabla tabla, Columna columna)
        {
            int n = _siguiente++;

            return columna.Tipo switch
            {
                "uuid" => "gen_random_uuid()",
                "text" => $"'{Letras(n, 12)}'",
                "varchar" or "bpchar" => $"'{Letras(n, Math.Min(columna.Longitud ?? 12, 12))}'",
                "int2" or "int4" or "int8" => n.ToString(CultureInfo.InvariantCulture),
                "numeric" => "1",
                "bool" => "false",
                "timestamptz" or "timestamp" => "now()",
                "date" => "current_date",
                "jsonb" or "json" => "'{}'",
                "bytea" => "'\\x00'",
                _ => throw new InvalidOperationException(
                    $"{tabla.Nombre}.{columna.Nombre} es de tipo {columna.Tipo}, que el recorrido no sabe inventar: añádelo aquí"),
            };
        }

        private static string Letras(int n, int longitud)
        {
            char[] letras = new char[longitud];

            for (int i = longitud - 1; i >= 0; i--)
            {
                letras[i] = (char)('A' + (n % 26));
                n /= 26;
            }

            return new string(letras);
        }

        private static string Citar(string identificador) => $"\"{identificador}\"";
    }
}
