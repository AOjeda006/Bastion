using Bastion.Api.IntegrationTests.Api;
using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.Pruebas.Comun;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Exportacion;

/// <summary>
/// La foto que pide cada exportación, tal como la construye el host, abre contra PostgreSQL una
/// transacción en <c>REPEATABLE READ</c> y de solo lectura, en el contexto de su módulo (ADR-0073).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que no ve nadie más.</b> <c>LaExportacionEsHermanaDelListadoTests</c> dice que la foto es la
/// del módulo y que el host la construye, y <c>LaFotoDeLecturaTests</c> mide
/// <c>FotoDeLectura</c>, pero ninguno de los dos mira lo que hace la foto de cada módulo al abrirse.
/// Una que abriera una transacción cualquiera, en <c>READ COMMITTED</c> y de escritura, pasaba por
/// todos los casos de las exportaciones: fue la mutación 858 del 3.2.
/// </para>
/// <para>
/// <b>Se pregunta en el contexto del módulo</b>, resuelto del mismo ámbito que la foto, que es el
/// que leen sus listados. Que la transacción esté ahí es lo que dice que las páginas la ven.
/// </para>
/// <para>
/// <b>La pareja es la misma pregunta sin la foto</b>, que tiene que contestar lo de por omisión: si
/// contestara lo mismo con foto y sin ella, el caso no estaría midiendo la foto.
/// </para>
/// </remarks>
/// <param name="postgres">El contenedor con las migraciones de todos los módulos aplicadas.</param>
[Collection(ColeccionDeLaApi.Nombre)]
[Trait("Category", "Integracion")]
public sealed class LaFotoDeCadaModuloEsUnaFotoTests(PostgresConTodosLosModulos postgres) : IDisposable
{
    private const string Nivel = "transaction_isolation";

    private const string SoloLectura = "transaction_read_only";

    private readonly ApiDeVerdad _api = new(postgres);

    /// <inheritdoc/>
    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task La_foto_de_cada_exportacion_es_de_solo_lectura_y_en_repeatable_read()
    {
        List<Type> fotos =
        [
            .. ExportacionesDeLaApi.De(_api.Services).SelectMany(ExportacionesDeLaApi.Fotos).Distinct(),
        ];

        fotos.ShouldNotBeEmpty("el arnés: no se ve la foto de ninguna exportación");

        List<string> fallos = [];

        foreach (Type tipo in fotos)
        {
            await using AsyncServiceScope ambito = _api.Services.CreateAsyncScope();
            var foto = (IFotoDeLectura)ambito.ServiceProvider.GetRequiredService(tipo);
            DbContext contexto = ContextoDe(ambito, foto);

            // LA PAREJA: sin la foto, lo de por omisión.
            (await AjusteAsync(contexto, Nivel)).ShouldBe("read committed", tipo.Name);
            (await AjusteAsync(contexto, SoloLectura)).ShouldBe("off", tipo.Name);

            await using (await foto.AbrirAsync(CancellationToken.None))
            {
                string nivel = await AjusteAsync(contexto, Nivel);
                string soloLectura = await AjusteAsync(contexto, SoloLectura);

                if (contexto.Database.CurrentTransaction is null || nivel != "repeatable read" || soloLectura != "on")
                {
                    fallos.Add(
                        $"{tipo.Name}: abre {nivel}, solo lectura {soloLectura}, " +
                        $"y en su contexto hay transacción: {contexto.Database.CurrentTransaction is not null}");
                }
            }

            contexto.Database.CurrentTransaction.ShouldBeNull($"{tipo.Name}: al liberarla, la transacción se cierra");
        }

        fallos.ShouldBeEmpty(string.Join(Environment.NewLine, fallos));
    }

    /// <summary>
    /// El contexto del módulo de la foto: el único de su ensamblado, resuelto del mismo ámbito, que
    /// es la instancia que la foto recibió.
    /// </summary>
    private static DbContext ContextoDe(AsyncServiceScope ambito, IFotoDeLectura foto)
    {
        Type[] contextos =
        [
            .. foto.GetType().Assembly.GetTypes()
                .Where(tipo => typeof(DbContext).IsAssignableFrom(tipo) && !tipo.IsAbstract),
        ];

        contextos.Length.ShouldBe(1, $"el arnés: {foto.GetType().Assembly.GetName().Name} tiene un contexto");

        return (DbContext)ambito.ServiceProvider.GetRequiredService(contextos[0]);
    }

    private static Task<string> AjusteAsync(DbContext contexto, string ajuste) =>
        contexto.Database
            .SqlQueryRaw<string>("SELECT current_setting({0}) AS \"Value\"", ajuste)
            .SingleAsync();
}
