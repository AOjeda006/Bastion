using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// La foto de una exportación, contra PostgreSQL de verdad: lo que se lee dentro sale de un mismo
/// instante, y dentro no se puede escribir (ADR-0073).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que hay que ver es la otra transacción.</b> Que la foto sea <c>REPEATABLE READ</c> se nota
/// cuando otra conexión confirma algo entre dos lecturas de la foto: dentro no se ve, y fuera sí.
/// La pareja, la misma lectura sin foto, es la que dice que lo que confirma la otra conexión se ve
/// de verdad, y que el «no se ve» de dentro es cosa de la foto.
/// </para>
/// <para>
/// <b>Sobre una tabla propia, en un esquema propio</b>, que el caso crea y borra. Un maestro del
/// módulo necesitaría una empresa y su inquilinato para sembrar una fila, y aquí lo que se mira es
/// la transacción, no el modelo. El esquema no es <c>public</c>, que la regla de
/// <c>EsquemaDelModuloTests</c> quiere vacío.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor, con las migraciones del módulo aplicadas.</param>
[Trait("Category", "Integracion")]
[Collection(ColeccionDePostgres.Nombre)]
public sealed class LaFotoDeLecturaTests(PostgresDeVerdad postgres) : IAsyncLifetime
{
    private const string Contar = """SELECT count(*)::int AS "Value" FROM prueba_de_la_foto.filas""";

    private const string Escribir = "INSERT INTO prueba_de_la_foto.filas (n) VALUES (1)";

    /// <inheritdoc/>
    public Task InitializeAsync() => OrdenAsync(
        """
        DROP SCHEMA IF EXISTS prueba_de_la_foto CASCADE;
        CREATE SCHEMA prueba_de_la_foto;
        CREATE TABLE prueba_de_la_foto.filas (n int NOT NULL);
        INSERT INTO prueba_de_la_foto.filas (n) VALUES (1), (2), (3);
        """);

    /// <inheritdoc/>
    public Task DisposeAsync() => OrdenAsync("DROP SCHEMA IF EXISTS prueba_de_la_foto CASCADE");

    [Fact]
    public async Task Dentro_de_la_foto_no_se_ve_lo_que_otra_conexion_confirma_despues()
    {
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        await using (await FotoDeLectura.AbrirAsync(contexto, CancellationToken.None))
        {
            (await ContarAsync(contexto)).ShouldBe(3);

            await OrdenAsync("INSERT INTO prueba_de_la_foto.filas (n) VALUES (4), (5)");

            (await ContarAsync(contexto)).ShouldBe(
                3, "la segunda lectura de la foto ha visto lo que se confirmó después");
        }

        // LA PAREJA: el mismo contexto, sin foto, ve las dos filas que confirmó la otra conexión.
        (await ContarAsync(contexto)).ShouldBe(
            5, "lo que confirma la otra conexión no se ve ni fuera de la foto");
    }

    [Fact]
    public async Task Dentro_de_la_foto_no_se_puede_escribir()
    {
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();

        await using (await FotoDeLectura.AbrirAsync(contexto, CancellationToken.None))
        {
            PostgresException rechazo = await Should.ThrowAsync<PostgresException>(
                () => contexto.Database.ExecuteSqlRawAsync(Escribir));

            rechazo.SqlState.ShouldBe(PostgresErrorCodes.ReadOnlySqlTransaction);
        }

        // LA PAREJA: la misma orden, fuera de la foto, escribe.
        (await contexto.Database.ExecuteSqlRawAsync(Escribir)).ShouldBe(1);
    }

    [Fact]
    public async Task Una_foto_no_se_abre_dentro_de_otra_transaccion()
    {
        await using OrganizacionDbContext contexto = postgres.AbrirContexto();
        await using IDbContextTransaction abierta = await contexto.Database.BeginTransactionAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => FotoDeLectura.AbrirAsync(contexto, CancellationToken.None));
    }

    private static Task<int> ContarAsync(OrganizacionDbContext contexto) =>
        contexto.Database.SqlQueryRaw<int>(Contar).SingleAsync();

    // Por otra conexión, con su propia transacción implícita, que se confirma al acabar la orden.
    private async Task OrdenAsync(string sql)
    {
        await using NpgsqlConnection conexion = new(postgres.CadenaDeConexion);
        await conexion.OpenAsync();

        await using NpgsqlCommand orden = new(sql, conexion);
        await orden.ExecuteNonQueryAsync();
    }
}
