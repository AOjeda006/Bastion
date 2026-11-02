using Npgsql;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Persistencia;

/// <summary>
/// La migración que le da al tercero una tarifa por rol, sobre fichas que ya tenían la suya.
/// </summary>
/// <remarks>
/// <para>
/// <b>La de venta es la columna de antes renombrada, y eso es lo que se prueba</b> (3.3). Un
/// renombrado y un «soltar y volver a crear» dejan la tabla con las mismas columnas y las mismas
/// filas; lo que los distingue es el valor, y
/// <see cref="LasMigracionesSobreTablasConFilasTests"/> cuenta filas, no valores. Aquí cada ficha
/// lleva su tarifa, y la tiene que conservar a la ida y a la vuelta.
/// </para>
/// <para>
/// <b>Una de las fichas no es cliente</b>, porque la decisión de la puerta del 3.3 es que toda
/// asignación de antes pasa a ser la de venta, sea cliente o no: la migración no mira el rol, y la
/// regla la aplica cada asignación nueva. Otra no tiene tarifa, para que una migración que pusiera
/// alguna donde no había no acierte.
/// </para>
/// <para>
/// <b>En una base propia migrada hasta la de antes</b>, con las filas escritas a mano, como
/// <see cref="LaDivisaDeLaTarifaSeTraduceAlMigrarTests"/>. Sin semillas de la API.
/// </para>
/// </remarks>
/// <param name="postgres">El servidor compartido; el caso, en una base suya.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaTarifaDelTerceroSeConservaAlMigrarTests(PostgresConTodosLosModulos postgres)
{
    /// <summary>La última migración de Terceros antes de la de las dos tarifas.</summary>
    private const string LaDeAntes = "20260913071206_TarifaAsignadaDelTercero";

    /// <summary>La que renombra la tarifa asignada a la de venta y añade la de compra.</summary>
    private const string LaDeLasDos = "20261102092839_LaTarifaDeVentaYLaDeCompraDelTercero";

    /// <summary>
    /// Cada ficha conserva su tarifa como la de venta, sin una de compra; y al deshacer la
    /// migración vuelve a ser la tarifa asignada, y la de compra se pierde.
    /// </summary>
    /// <remarks>
    /// Antes de volver, una ficha recibe una tarifa de compra, para que la vuelta tenga algo que
    /// perder: es lo que se pierde por diseño, porque el esquema de antes no la tiene.
    /// </remarks>
    [Fact]
    public async Task Cada_ficha_conserva_su_tarifa_como_la_de_venta_y_la_vuelta_se_la_devuelve()
    {
        string cadena = await postgres.CrearBaseNuevaAsync(migrada: false);
        await PostgresConTodosLosModulos.MigrarTercerosHastaAsync(cadena, LaDeAntes);

        var empresa = Guid.CreateVersion7();
        var cliente = Guid.CreateVersion7();
        var noCliente = Guid.CreateVersion7();
        var sinTarifa = Guid.CreateVersion7();
        var deCliente = Guid.CreateVersion7();
        var deNoCliente = Guid.CreateVersion7();

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await InsertarFichaAsync(conexion, cliente, empresa, "00000001R", esCliente: true, deCliente);
            await InsertarFichaAsync(conexion, noCliente, empresa, "00000002W", esCliente: false, deNoCliente);
            await InsertarFichaAsync(conexion, sinTarifa, empresa, "00000003A", esCliente: true, tarifa: null);
        }

        List<string> columnasDeAntes = await ColumnasAsync(cadena);

        await PostgresConTodosLosModulos.MigrarTercerosHastaAsync(cadena, LaDeLasDos);

        (await LeerAsync(cadena, "tarifa_de_venta_id", "tarifa_de_compra_id")).ShouldBe(
            new Dictionary<Guid, string>
            {
                [cliente] = $"{deCliente};-",
                [noCliente] = $"{deNoCliente};-",
                [sinTarifa] = "-;-",
            },
            ignoreOrder: true);

        await using (NpgsqlConnection conexion = await AbrirAsync(cadena))
        {
            await using NpgsqlCommand compra = new(
                "UPDATE terceros.terceros SET tarifa_de_compra_id = @tarifa WHERE id = @id", conexion);
            compra.Parameters.AddWithValue("tarifa", Guid.CreateVersion7());
            compra.Parameters.AddWithValue("id", noCliente);
            (await compra.ExecuteNonQueryAsync()).ShouldBe(1);
        }

        await PostgresConTodosLosModulos.MigrarTercerosHastaAsync(cadena, LaDeAntes);

        (await LeerAsync(cadena, "tarifa_asignada_id")).ShouldBe(
            new Dictionary<Guid, string>
            {
                [cliente] = deCliente.ToString(),
                [noCliente] = deNoCliente.ToString(),
                [sinTarifa] = "-",
            },
            ignoreOrder: true);
        (await ColumnasAsync(cadena)).ShouldBe(columnasDeAntes, "la vuelta deja la tabla con las columnas de antes");
    }

    private static async Task<NpgsqlConnection> AbrirAsync(string cadena)
    {
        NpgsqlConnection conexion = new(cadena);
        await conexion.OpenAsync();
        return conexion;
    }

    /// <summary>Una ficha como la guardaba la tabla antes del 3.3, con una sola tarifa.</summary>
    private static async Task InsertarFichaAsync(
        NpgsqlConnection conexion, Guid id, Guid empresaId, string nif, bool esCliente, Guid? tarifa)
    {
        await using NpgsqlCommand ficha = new(
            """
            INSERT INTO terceros.terceros
                (id, empresa_id, razon_social, es_cliente, es_proveedor, bloqueado,
                 domicilio_fiscal_calle, domicilio_fiscal_codigo_postal, domicilio_fiscal_pais,
                 domicilio_fiscal_poblacion, identificacion_numero, identificacion_pais,
                 identificacion_verificacion, criterio_de_caja, recargo_de_equivalencia,
                 sujeto_a_retencion_irpf, territorio_fiscal, tarifa_asignada_id, creado_en, modificado_en)
            VALUES (@id, @empresa, 'Razón Social', @cliente, NOT @cliente, false,
                    'Calle de la Prueba', '28001', 'ES',
                    'Madrid', @nif, 'ES',
                    'NoVerificado', false, false,
                    false, 'PeninsulaYBaleares', @tarifa, now(), now())
            """,
            conexion);

        ficha.Parameters.AddWithValue("id", id);
        ficha.Parameters.AddWithValue("empresa", empresaId);
        ficha.Parameters.AddWithValue("cliente", esCliente);
        ficha.Parameters.AddWithValue("nif", nif);
        ficha.Parameters.Add(new NpgsqlParameter<Guid?>("tarifa", tarifa));
        await ficha.ExecuteNonQueryAsync();
    }

    /// <summary>Las columnas de <c>terceros.terceros</c>, por nombre.</summary>
    private static async Task<List<string>> ColumnasAsync(string cadena)
    {
        await using NpgsqlConnection conexion = await AbrirAsync(cadena);
        await using NpgsqlCommand columnas = new(
            """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'terceros' AND table_name = 'terceros'
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

    /// <summary>Unas columnas de identificadores de cada ficha, por su identificador, con «-» por nulo.</summary>
    /// <param name="cadena">La base del caso.</param>
    /// <param name="columnas">Nombres escritos en este fichero, nunca entrada de nadie.</param>
    private static async Task<Dictionary<Guid, string>> LeerAsync(string cadena, params string[] columnas)
    {
        string expresion = string.Join(
            " || ';' || ", columnas.Select(columna => $"coalesce({columna}::text, '-')"));

        await using NpgsqlConnection conexion = await AbrirAsync(cadena);
        await using NpgsqlCommand lectura = new($"SELECT id, {expresion} FROM terceros.terceros", conexion);
        await using NpgsqlDataReader lector = await lectura.ExecuteReaderAsync();

        Dictionary<Guid, string> filas = [];

        while (await lector.ReadAsync())
        {
            filas.Add(lector.GetGuid(0), lector.GetString(1));
        }

        return filas;
    }
}
