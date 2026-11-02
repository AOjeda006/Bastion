using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// La migración que pasa la divisa de la tarifa a su código ISO 4217, sobre tramos que ya estaban.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es la única migración que lee otro esquema</b> (ADR-0076): traduce el <c>divisa_id</c> de
/// cada tramo con <c>organizacion.divisas</c>, y se para si no puede. Lo que se prueba aquí es lo que
/// <see cref="LasMigracionesSobreTablasConFilasTests"/> no ve, porque sus filas son inventadas: que
/// cada tramo recibe el código de SU divisa, que la vuelta le devuelve su identificador, y que
/// ninguna de las paradas, a la ida o a la vuelta, se lleva nada por delante.
/// </para>
/// <para>
/// <b>Cada parada se afirma con su código y con que no es transitorio.</b> La primera versión de la
/// migración paraba con <c>object_not_in_prerequisite_state</c>, Npgsql lo cuenta como transitorio,
/// y lo que llegaba no era el <c>PostgresException</c> sino el <c>InvalidOperationException</c> con
/// que EF Core envuelve un fallo transitorio, proponiendo reintentar (ADR-0076). Lo vio el primer
/// run de estos casos.
/// </para>
/// <para>
/// <b>Cada caso, en una base propia migrada hasta la de antes</b>, con las filas escritas a mano: la
/// migración no tiene otra forma de encontrarse con tramos guardados. Sin semillas de la API, porque
/// no pasa por ella.
/// </para>
/// </remarks>
/// <param name="postgres">El servidor compartido; cada caso, en una base suya.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaDivisaDeLaTarifaSeTraduceAlMigrarTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>La última migración de Catálogo antes de la de la divisa.</summary>
    private const string LaDeAntes = "20261002113147_LosCodigosDeBarrasDelArticulo";

    /// <summary>La que pasa la divisa a su código y le pone el tipo a la tarifa.</summary>
    private const string LaDeLaDivisa = "20261102075227_ElTipoYLaDivisaDeLaTarifa";

    /// <summary>
    /// Cada tramo recibe el código de su divisa, también el de una retirada, y es de venta; y al
    /// deshacer la migración vuelve el identificador de cada uno.
    /// </summary>
    /// <remarks>
    /// Dos tramos con divisas distintas, para que una traducción que pusiera a todos el mismo código
    /// no acierte, y el dólar retirado, porque retirar una divisa no la borra: sus tramos siguen
    /// siendo suyos. La libra está y no la usa nadie.
    /// </remarks>
    [Fact]
    public async Task Cada_tramo_recibe_el_codigo_de_su_divisa_y_la_vuelta_le_devuelve_su_identificador()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);
        await PostgresConTodosLosModulos.MigrarOrganizacionAsync(cadena);
        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeAntes);

        var euro = Guid.CreateVersion7();
        var dolar = Guid.CreateVersion7();
        var enEuros = Guid.CreateVersion7();
        var enDolares = Guid.CreateVersion7();
        var empresa = Guid.CreateVersion7();

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await DarDeAltaLaDivisaAsync(conexion, euro, "EUR", retirada: false);
            await DarDeAltaLaDivisaAsync(conexion, dolar, "USD", retirada: true);
            await DarDeAltaLaDivisaAsync(conexion, Guid.CreateVersion7(), "GBP", retirada: false);
            await InsertarTramoAsync(conexion, enEuros, empresa, "GEN", euro);
            await InsertarTramoAsync(conexion, enDolares, empresa, "USA", dolar);
        }

        List<string> columnasDeAntes = await ColumnasAsync(cadena);

        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeLaDivisa);

        (await LeerAsync(cadena, "tipo || ';' || divisa")).ShouldBe(
            new Dictionary<Guid, string>
            {
                [enEuros] = "Venta;EUR",
                [enDolares] = "Venta;USD",
            },
            ignoreOrder: true);

        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeAntes);

        (await LeerAsync(cadena, "divisa_id::text")).ShouldBe(
            new Dictionary<Guid, string>
            {
                [enEuros] = euro.ToString(),
                [enDolares] = dolar.ToString(),
            },
            ignoreOrder: true);
        (await ColumnasAsync(cadena)).ShouldBe(columnasDeAntes, "la vuelta deja la tabla con las columnas de antes");
    }

    /// <summary>
    /// Un tramo cuya divisa no está en Organización para la migración, que dice cuál es, y no se
    /// pierde nada; con la divisa dada de alta, migra.
    /// </summary>
    /// <remarks>
    /// La parada llega después de añadir el tipo y la divisa, así que «no se pierde nada» incluye que
    /// la transacción de la migración los deshaga: las columnas tienen que ser las de antes, y no
    /// solo los identificadores.
    /// </remarks>
    [Fact]
    public async Task Un_tramo_cuya_divisa_no_esta_para_la_migracion_diciendo_cual_y_no_se_pierde_nada()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);
        await PostgresConTodosLosModulos.MigrarOrganizacionAsync(cadena);
        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeAntes);

        var euro = Guid.CreateVersion7();
        var queNoEsta = Guid.CreateVersion7();
        var bueno = Guid.CreateVersion7();
        var huerfano = Guid.CreateVersion7();

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await DarDeAltaLaDivisaAsync(conexion, euro, "EUR", retirada: false);
            await InsertarTramoAsync(conexion, bueno, Guid.CreateVersion7(), "GEN", euro);
            await InsertarTramoAsync(conexion, huerfano, Guid.CreateVersion7(), "GEN", queNoEsta);
        }

        List<string> columnasDeAntes = await ColumnasAsync(cadena);

        PostgresException parada = await Should.ThrowAsync<PostgresException>(
            () => PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeLaDivisa));

        parada.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation, parada.MessageText);
        parada.IsTransient.ShouldBeFalse("una parada que se reintenta no se arregla sola");
        parada.MessageText.ShouldContain($"{huerfano} (divisa {queNoEsta})");
        parada.MessageText.ShouldNotContain(bueno.ToString(), Case.Insensitive, "el tramo que sí tiene divisa no es de los que hay que arreglar");

        (await ColumnasAsync(cadena)).ShouldBe(columnasDeAntes, "la parada deshace también el tipo y la divisa");
        (await LeerAsync(cadena, "divisa_id::text")).ShouldBe(
            new Dictionary<Guid, string>
            {
                [bueno] = euro.ToString(),
                [huerfano] = queNoEsta.ToString(),
            },
            ignoreOrder: true);

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await DarDeAltaLaDivisaAsync(conexion, queNoEsta, "CHF", retirada: false);
        }

        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeLaDivisa);

        (await LeerAsync(cadena, "divisa")).ShouldBe(
            new Dictionary<Guid, string>
            {
                [bueno] = "EUR",
                [huerfano] = "CHF",
            },
            ignoreOrder: true);
    }

    /// <summary>
    /// En una base sin Organización, la migración pasa si no hay tramos y se para pidiéndola si los
    /// hay.
    /// </summary>
    /// <remarks>
    /// Sin tramos no hay nada que traducir, y la migración no lee nada: el orden de los módulos en
    /// el despliegue solo importa cuando hay datos.
    /// </remarks>
    [Fact]
    public async Task Sin_Organizacion_pasa_si_no_hay_tramos_y_si_los_hay_se_para_pidiendola()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);
        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeAntes);

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await InsertarTramoAsync(conexion, Guid.CreateVersion7(), Guid.CreateVersion7(), "GEN", Guid.CreateVersion7());
        }

        List<string> columnasDeAntes = await ColumnasAsync(cadena);

        PostgresException parada = await Should.ThrowAsync<PostgresException>(
            () => PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeLaDivisa));

        parada.SqlState.ShouldBe(PostgresErrorCodes.UndefinedTable, parada.MessageText);
        parada.IsTransient.ShouldBeFalse("una parada que se reintenta no se arregla sola");
        parada.MessageText.ShouldContain("Aplique antes las migraciones de Organización");
        (await ColumnasAsync(cadena)).ShouldBe(columnasDeAntes);

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await using NpgsqlCommand vaciar = new("DELETE FROM catalogo.tarifas", conexion);
            await vaciar.ExecuteNonQueryAsync();
        }

        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeLaDivisa);

        List<string> columnas = await ColumnasAsync(cadena);

        columnas.ShouldContain("tipo");
        columnas.ShouldContain("divisa");
        columnas.ShouldNotContain("divisa_id");
    }

    /// <summary>
    /// Al deshacer la migración, un tramo cuyo código no está en Organización la para, que dice
    /// cuál es, y no se pierde nada; con la divisa dada de alta, vuelve.
    /// </summary>
    /// <remarks>
    /// La vuelta tiene sus propias paradas, en su propio bloque, y la tanda del 3.3 las quitó una a
    /// una, la 979, la 980 y la 981, sin que ningún caso se pusiera rojo: la vuelta solo se ejercía
    /// con todas sus divisas.
    /// </remarks>
    [Fact]
    public async Task La_vuelta_de_un_tramo_cuya_divisa_no_esta_se_para_diciendo_cual_y_no_se_pierde_nada()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);
        await PostgresConTodosLosModulos.MigrarOrganizacionAsync(cadena);
        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeLaDivisa);

        var euro = Guid.CreateVersion7();
        var bueno = Guid.CreateVersion7();
        var huerfano = Guid.CreateVersion7();

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await DarDeAltaLaDivisaAsync(conexion, euro, "EUR", retirada: false);
            await InsertarTramoConSuCodigoAsync(conexion, bueno, "Venta", "EUR");
            await InsertarTramoConSuCodigoAsync(conexion, huerfano, "Compra", "CHF");
        }

        List<string> columnasDeAntes = await ColumnasAsync(cadena);

        PostgresException parada = await Should.ThrowAsync<PostgresException>(
            () => PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeAntes));

        parada.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation, parada.MessageText);
        parada.IsTransient.ShouldBeFalse("una parada que se reintenta no se arregla sola");
        parada.MessageText.ShouldContain($"{huerfano} (divisa CHF)");
        parada.MessageText.ShouldNotContain(bueno.ToString(), Case.Insensitive, "el tramo que sí tiene divisa no es de los que hay que arreglar");

        (await ColumnasAsync(cadena)).ShouldBe(columnasDeAntes, "la parada deshace también el identificador");
        (await LeerAsync(cadena, "tipo || ';' || divisa")).ShouldBe(
            new Dictionary<Guid, string>
            {
                [bueno] = "Venta;EUR",
                [huerfano] = "Compra;CHF",
            },
            ignoreOrder: true);

        var franco = Guid.CreateVersion7();

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await DarDeAltaLaDivisaAsync(conexion, franco, "CHF", retirada: false);
        }

        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeAntes);

        (await LeerAsync(cadena, "divisa_id::text")).ShouldBe(
            new Dictionary<Guid, string>
            {
                [bueno] = euro.ToString(),
                [huerfano] = franco.ToString(),
            },
            ignoreOrder: true);
    }

    /// <summary>
    /// En una base sin Organización, la vuelta pasa si no hay tramos y se para pidiéndola si los
    /// hay.
    /// </summary>
    [Fact]
    public async Task La_vuelta_sin_Organizacion_pasa_si_no_hay_tramos_y_si_los_hay_se_para_pidiendola()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);
        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeLaDivisa);

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await InsertarTramoConSuCodigoAsync(conexion, Guid.CreateVersion7(), "Venta", "EUR");
        }

        List<string> columnasDeAntes = await ColumnasAsync(cadena);

        PostgresException parada = await Should.ThrowAsync<PostgresException>(
            () => PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeAntes));

        parada.SqlState.ShouldBe(PostgresErrorCodes.UndefinedTable, parada.MessageText);
        parada.IsTransient.ShouldBeFalse("una parada que se reintenta no se arregla sola");
        parada.MessageText.ShouldContain("solo vuelve a su identificador con organizacion.divisas");
        (await ColumnasAsync(cadena)).ShouldBe(columnasDeAntes);

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await using NpgsqlCommand vaciar = new("DELETE FROM catalogo.tarifas", conexion);
            await vaciar.ExecuteNonQueryAsync();
        }

        await PostgresConTodosLosModulos.MigrarCatalogoHastaAsync(cadena, LaDeAntes);

        List<string> columnas = await ColumnasAsync(cadena);

        columnas.ShouldContain("divisa_id");
        columnas.ShouldNotContain("tipo");
        columnas.ShouldNotContain("divisa");
    }

    private static async Task<NpgsqlConnection> AbrirAsync(string cadena)
    {
        NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();
        return conexion;
    }

    private static async Task DarDeAltaLaDivisaAsync(NpgsqlConnection conexion, Guid id, string codigo, bool retirada)
    {
        await using NpgsqlCommand alta = new(
            """
            INSERT INTO organizacion.divisas (id, codigo, nombre, retirada, creado_en, modificado_en)
            VALUES (@id, @codigo, @codigo, @retirada, now(), now())
            """,
            conexion);

        alta.Parameters.AddWithValue("id", id);
        alta.Parameters.AddWithValue("codigo", codigo);
        alta.Parameters.AddWithValue("retirada", retirada);
        await alta.ExecuteNonQueryAsync();
    }

    /// <summary>Un tramo como lo guardaba la tabla antes del 3.3, con el identificador de la divisa.</summary>
    private static async Task InsertarTramoAsync(
        NpgsqlConnection conexion, Guid id, Guid empresaId, string codigo, Guid divisaId)
    {
        await using NpgsqlCommand tramo = new(
            """
            INSERT INTO catalogo.tarifas
                (id, empresa_id, codigo, nombre, divisa_id, vigente_desde, vigente_hasta, creado_en, modificado_en)
            VALUES (@id, @empresa, @codigo, @codigo, @divisa, DATE '2026-01-01', NULL, now(), now())
            """,
            conexion);

        tramo.Parameters.AddWithValue("id", id);
        tramo.Parameters.AddWithValue("empresa", empresaId);
        tramo.Parameters.AddWithValue("codigo", codigo);
        tramo.Parameters.AddWithValue("divisa", divisaId);
        await tramo.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Un tramo como lo guarda la tabla desde el 3.3, con su tipo y el código de su divisa, en una
    /// empresa suya: así dos tramos del mismo código no chocan con ninguna de las dos exclusiones.
    /// </summary>
    private static async Task InsertarTramoConSuCodigoAsync(
        NpgsqlConnection conexion, Guid id, string tipo, string divisa)
    {
        await using NpgsqlCommand tramo = new(
            """
            INSERT INTO catalogo.tarifas
                (id, empresa_id, codigo, nombre, tipo, divisa, vigente_desde, vigente_hasta, creado_en, modificado_en)
            VALUES (@id, @empresa, 'GEN', 'GEN', @tipo, @divisa, DATE '2026-01-01', NULL, now(), now())
            """,
            conexion);

        tramo.Parameters.AddWithValue("id", id);
        tramo.Parameters.AddWithValue("empresa", Guid.CreateVersion7());
        tramo.Parameters.AddWithValue("tipo", tipo);
        tramo.Parameters.AddWithValue("divisa", divisa);
        await tramo.ExecuteNonQueryAsync();
    }

    /// <summary>Las columnas de <c>catalogo.tarifas</c>, por nombre.</summary>
    private static async Task<List<string>> ColumnasAsync(string cadena)
    {
        await using NpgsqlConnection conexion = await AbrirAsync(cadena);
        await using NpgsqlCommand columnas = new(
            """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'catalogo' AND table_name = 'tarifas'
            ORDER BY column_name
            """,
            conexion);
        await using NpgsqlDataReader lector = await columnas.ExecuteReaderAsync();

        List<string> nombres = [];

        while (await lector.ReadAsync())
        {
            nombres.Add(lector.GetString(0));
        }

        return nombres;
    }

    /// <summary>Una expresión sobre cada tramo, por su identificador.</summary>
    /// <param name="cadena">La base del caso.</param>
    /// <param name="expresion">SQL escrito en este fichero, nunca entrada de nadie.</param>
    private static async Task<Dictionary<Guid, string>> LeerAsync(string cadena, string expresion)
    {
        await using NpgsqlConnection conexion = await AbrirAsync(cadena);
        await using NpgsqlCommand lectura = new($"SELECT id, {expresion} FROM catalogo.tarifas", conexion);
        await using NpgsqlDataReader lector = await lectura.ExecuteReaderAsync();

        Dictionary<Guid, string> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add(lector.GetGuid(0), lector.GetString(1));
        }

        return filas;
    }
}
